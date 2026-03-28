using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Buildings;
using StardewValley.Menus;
using System.Collections.Generic;
using System;

namespace BuildingFootprintSwapper;

internal static class CarpenterMenuPatcher
{
    /// <summary>
    /// The CustomFields key for modders to set on their buildings to use this mod's functionality.
    ///
    /// Usage in Content Patcher:
    ///   "CustomFields": {
    ///       "bobkalonger.BFS_util/ForceMove": "true"
    ///   }
    /// </summary>
    public const string CustomFieldKey = "bobkalonger.BFS_util/ForceMove";

    public static void Apply(Harmony harmony)
    {
        harmony.Patch(
            original: AccessTools.Method(typeof(CarpenterMenu), nameof(CarpenterMenu.receiveLeftClick)),
            prefix: new HarmonyMethod(typeof(CarpenterMenuPatcher), nameof(receiveLeftClick_Prefix))
        );
    }

    [HarmonyPrefix]
    private static bool receiveLeftClick_Prefix(CarpenterMenu __instance, int x, int y)
    {
        if (!__instance.onFarm || __instance.Action != CarpenterMenu.CarpentryAction.Upgrade || __instance.freeze || Game1.IsFading())
            return true;

        if (!IsForceMoveBlueprintUpgrade(__instance.Blueprint))
            return true;

        Building toUpgrade = __instance.TargetLocation.getBuildingAt(
            new Vector2(
                (Game1.viewport.X + Game1.getMouseX(ui_scale: false)) / 64,
                (Game1.viewport.Y + Game1.getMouseY(ui_scale: false)) / 64
            )
        );

        if (toUpgrade == null || toUpgrade.buildingType.Value != __instance.Blueprint.UpgradeFrom)
            return true;

        // --- Custom upgrade-then-move flow ---

        // 1. Deduct resources exactly as the normal upgrade flow would
        __instance.ConsumeResources();

        // 2. Set up the upgrade and finish it instantly so the building reflects
        //    the new type and footprint before we hand it to move mode
        toUpgrade.upgradeName.Value = __instance.Blueprint.Id;
        toUpgrade.daysUntilUpgrade.Value = 0;
        toUpgrade.FinishConstruction();

        __instance.Action = CarpenterMenu.CarpentryAction.Move;
        toUpgrade.isMoving = true;
        __instance.buildingToMove = toUpgrade;

        Game1.playSound("axchop");

        ModEntry.ModMonitor.Log(
            $"Upgrade of '{toUpgrade.buildingType.Value}' finished instantly. " +
            $"Entering move mode for repositioning.",
            LogLevel.Debug
        );

        return false;
    }

    private static bool IsForceMoveBlueprintUpgrade(CarpenterMenu.BlueprintEntry blueprint)
    {
        if (!blueprint.IsUpgrade)
            return false;

        if (Game1.buildingData.TryGetValue(blueprint.Id, out BuildingData liveData))
        {
            return HasForceMoveField(liveData.CustomFields);
        }

        ModEntry.ModMonitor.Log(
            $"Building '{blueprint.Id}' not found in Game1.buildingData at click time. " +
            $"Falling back to cached blueprint data. " +
            $"If a config change occurred while the menu was open, CustomFields may be stale.",
            LogLevel.Warn
        );

        return HasForceMoveField(blueprint.Data.CustomFields);
    }

    private static bool HasForceMoveField(Dictionary<string, string> customFields)
    {
        return customFields != null
            && customFields.TryGetValue(CustomFieldKey, out string val)
            && val.Equals("true", StringComparison.OrdinalIgnoreCase);
    }
}
