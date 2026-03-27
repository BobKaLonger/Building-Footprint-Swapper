using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Buildings;
using StardewValley.Menus;

namespace BuildingFootprintSwapper;

/// <summary>Harmony patches for <see cref="CarpenterMenu"/>.</summary>
internal static class CarpenterMenuPatcher
{
    /*********
    ** Fields
    *********/
    /// <summary>
    /// The CustomFields key modders set on their building in Data/Buildings to opt into
    /// the forced-move behavior when this building is the result of an upgrade.
    ///
    /// Usage in Content Patcher:
    ///   "CustomFields": {
    ///       "bobkalonger.BFS_util/ForceMove": "true"
    ///   }
    /// </summary>
    public const string CustomFieldKey = "bobkalonger.BFS_util/ForceMove";


    /*********
    ** Public methods
    *********/
    /// <summary>Apply the Harmony patches.</summary>
    /// <param name="harmony">The Harmony instance.</param>
    public static void Apply(Harmony harmony)
    {
        harmony.Patch(
            original: AccessTools.Method(typeof(CarpenterMenu), nameof(CarpenterMenu.receiveLeftClick)),
            prefix: new HarmonyMethod(typeof(CarpenterMenuPatcher), nameof(receiveLeftClick_Prefix))
        );
    }


    /*********
    ** Private methods
    *********/
    /// <summary>
    /// Before the carpenter menu handles a left click, check whether this is an upgrade
    /// that should trigger forced move mode instead of the normal construction queue.
    /// If so, immediately finish the upgrade and pivot the menu into move mode so the
    /// player can reposition the building with the new (potentially larger) footprint.
    /// </summary>
    [HarmonyPrefix]
    private static bool receiveLeftClick_Prefix(CarpenterMenu __instance, int x, int y)
    {
        // Only intercept when we're on the farm in upgrading mode and the menu isn't busy
        if (!__instance.onFarm || !__instance.upgrading || __instance.freeze || Game1.IsFading())
            return true; // run original

        // Check if this blueprint is opted into forced-move behavior
        if (!IsForceMoveBlueprintUpgrade(__instance.Blueprint))
            return true; // run original, normal upgrade flow

        // Determine which building the player clicked
        Building? toUpgrade = __instance.TargetLocation.getBuildingAt(
            new Vector2(
                (Game1.viewport.X + Game1.getMouseX(ui_scale: false)) / 64,
                (Game1.viewport.Y + Game1.getMouseY(ui_scale: false)) / 64
            )
        );

        // If they clicked the wrong building type, let the original handle the error message
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

        // 3. Pivot the menu from upgrading mode into move mode,
        //    with the newly upgraded building already selected for placement
        __instance.upgrading = false;
        __instance.moving = true;
        toUpgrade.isMoving = true;
        __instance.buildingToMove = toUpgrade;

        Game1.playSound("axchop");

        ModEntry.ModMonitor.Log(
            $"Upgrade of '{toUpgrade.buildingType.Value}' finished instantly. " +
            $"Entering move mode for repositioning.",
            LogLevel.Debug
        );

        return false; // suppress original receiveLeftClick for this interaction
    }

    /// <summary>
    /// Check whether a blueprint is opted into forced-move behavior.
    /// Reads from Game1.buildingData (live asset state) first to handle mods that
    /// invalidate Data/Buildings at runtime (e.g. config-driven content packs).
    /// Falls back to the blueprint's cached data with a warning if the live data
    /// is unavailable — this means the player will get the behavior that was
    /// correct when they opened the menu, not a hard failure.
    /// </summary>
    /// <param name="blueprint">The blueprint to check.</param>
    private static bool IsForceMoveBlueprintUpgrade(CarpenterMenu.BlueprintEntry blueprint)
    {
        // Must actually be an upgrade blueprint for this to apply at all
        if (!blueprint.IsUpgrade)
            return false;

        // Prefer live data so runtime asset invalidation is respected
        if (Game1.buildingData.TryGetValue(blueprint.Id, out BuildingData? liveData))
        {
            return HasForceMoveField(liveData.CustomFields);
        }

        // Live data unavailable — fall back to the cached snapshot from menu open time
        ModEntry.ModMonitor.Log(
            $"Building '{blueprint.Id}' not found in Game1.buildingData at click time. " +
            $"Falling back to cached blueprint data. " +
            $"If a config change occurred while the menu was open, CustomFields may be stale.",
            LogLevel.Warn
        );

        return HasForceMoveField(blueprint.Data.CustomFields);
    }

    /// <summary>Check whether a CustomFields dictionary contains the ForceMove key set to true.</summary>
    /// <param name="customFields">The CustomFields dictionary to check, or null.</param>
    private static bool HasForceMoveField(Dictionary<string, string>? customFields)
    {
        return customFields != null
            && customFields.TryGetValue(CustomFieldKey, out string? val)
            && val.Equals("true", StringComparison.OrdinalIgnoreCase);
    }
}
