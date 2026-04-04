using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Buildings;
using StardewValley.Menus;
using System.Collections.Generic;
using System;
using System.Linq;

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
            prefix: new HarmonyMethod(typeof(CarpenterMenuPatcher), nameof(receiveLeftClick_Prefix)),
            postfix: new HarmonyMethod(typeof(CarpenterMenuPatcher), nameof(receiveLeftClick_Postfix))
        );

        harmony.Patch(
            original: AccessTools.Method(typeof(Building), nameof(Building.isTilePassable)),
            postfix: new HarmonyMethod(typeof(CarpenterMenuPatcher), nameof(isTilePassable_Postfix))
        );

        harmony.Patch(
            original: AccessTools.Method(typeof(Building), nameof(Building.FinishConstruction)),
            postfix: new HarmonyMethod(typeof(CarpenterMenuPatcher), nameof(FinishConstruction_Postfix))
        );

        harmony.Patch(
            original: AccessTools.Method(typeof(CarpenterMenu), nameof(CarpenterMenu.returnToCarpentryMenu)),
            prefix: new HarmonyMethod(typeof(CarpenterMenuPatcher), nameof(returnToCarpentryMenu_Prefix))
        );

        harmony.Patch(
            original: AccessTools.Method(typeof(Building), nameof(Building.GetData)),
            postfix: new HarmonyMethod(typeof(CarpenterMenuPatcher), nameof(GetData_Postfix))
        );
    }

    private static PendingUpgradeCost _pendingCost;
    private static bool _skipNextPostfix;

    private record PendingUpgradeCost(int Gold, List<Item> Materials, int OldTilesWide, int OldTilesHigh, Building Building);

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

        _pendingCost = new PendingUpgradeCost(
            __instance.Blueprint.BuildCost,
            __instance.Blueprint.BuildMaterials
                .Select(m => ItemRegistry.Create(m.ItemId, m.Amount))
                .ToList(),
            toUpgrade.tilesWide.Value,
            toUpgrade.tilesHigh.Value,
            toUpgrade
        );

        toUpgrade.upgradeName.Value = __instance.Blueprint.Id;
        toUpgrade.daysUntilUpgrade.Value = __instance.Blueprint.BuildDays;
        toUpgrade.showUpgradeAnimation(__instance.TargetLocation);
        Game1.netWorldState.Value.MarkUnderConstruction(__instance.Builder, toUpgrade);
        Game1.Multiplayer.globalChatInfoMessage(
            "BuildingBuild",
            Game1.player.Name,
            "aOrAn:" + __instance.Blueprint.TokenizedDisplayName,
            __instance.Blueprint.TokenizedDisplayName,
            Game1.player.farmName.Value
        );

        if (Game1.buildingData.TryGetValue(__instance.Blueprint.Id, out BuildingData newData))
        {
            toUpgrade.tilesWide.Value = newData.Size.X;
            toUpgrade.tilesHigh.Value = newData.Size.Y;
            toUpgrade.modData[$"{CarpenterMenuPatcher.CustomFieldKey}/PendingTilesWide"] = newData.Size.X.ToString();
            toUpgrade.modData[$"{CarpenterMenuPatcher.CustomFieldKey}/PendingTilesHigh"] = newData.Size.Y.ToString();
        }
        else
        {
            ModEntry.ModMonitor.Log(
                $"Could not find building data for '{__instance.Blueprint.Id}' to update footprint dimensions" +
                $"Move mode may show incorrect placement overlay.",
                LogLevel.Warn
            );
        }

        __instance.Action = CarpenterMenu.CarpentryAction.Move;
        toUpgrade.isMoving = true;
        __instance.buildingToMove = toUpgrade;

        Game1.playSound("axchop");

        ModEntry.ModMonitor.Log(
            $"Upgrade of '{__instance.Blueprint.Id}' queued. " +
            $"Entering move mode for repositioning.",
            LogLevel.Debug
        );
        _skipNextPostfix = true;

        return false;
    }

    private static void returnToCarpentryMenu_Prefix(CarpenterMenu __instance)
    {
        if (_pendingCost == null)
            return;

        _pendingCost.Building.upgradeName.Value = null;
        _pendingCost.Building.daysUntilUpgrade.Value = 0;
        _pendingCost.Building.tilesWide.Value = _pendingCost.OldTilesWide;
        _pendingCost.Building.tilesHigh.Value = _pendingCost.OldTilesHigh;
        _pendingCost.Building.modData.Remove($"{CustomFieldKey}/PendingTilesWide");
        _pendingCost.Building.modData.Remove($"{CustomFieldKey}/PendingTilesHigh");
        Game1.netWorldState.Value.UpdateUnderConstruction();

        _pendingCost = null;
        _skipNextPostfix = false;
    }

    [HarmonyPostfix]
    private static void receiveLeftClick_Postfix(CarpenterMenu __instance)
    {
        if (_pendingCost == null)
            return;

        if (_skipNextPostfix)
        {
            _skipNextPostfix = false;
            return;
        }

        if (__instance.buildingToMove != null)
            return;

        Game1.player.Money -= _pendingCost.Gold;

        foreach (Item material in _pendingCost.Materials)
            Game1.player.Items.ReduceId(material.QualifiedItemId, material.Stack);
        
        _pendingCost.Building.tilesWide.Value = _pendingCost.OldTilesWide;
        _pendingCost.Building.tilesHigh.Value = _pendingCost.OldTilesHigh;

        if (_pendingCost.Building.daysUntilUpgrade.Value == 0)
            {
                _pendingCost.Building.daysUntilUpgrade.Value = 1;
                _pendingCost.Building.FinishConstruction();
            }

        ModEntry.ModMonitor.Log(
            $"Building placed successfully. Build costs deducted.",
            LogLevel.Debug
        );

        _pendingCost = null;

        __instance.returnToCarpentryMenuAfterSuccessfulBuild();
    }

    [HarmonyPatch(typeof(Building), nameof(Building.GetData))]
    private static void GetData_Postfix(Building __instance, ref BuildingData __result)
    {
        if (__result == null)
            return;
        
        string upgradeName = __instance.upgradeName.Value;
        if (string.IsNullOrEmpty(upgradeName))
            return;

        if (!__instance.modData.ContainsKey($"{CustomFieldKey}/PendingTilesWide"))
            return;

        if (!Game1.buildingData.TryGetValue(upgradeName, out BuildingData upgradeData))
        {
            ModEntry.ModMonitor.Log(
                $"Could not find building data for '{upgradeName}' to read upgrade sign position",
                LogLevel.Warn
            );
            return;
        }

        __result.UpgradeSignTile = upgradeData.UpgradeSignTile;
        __result.UpgradeSignHeight = upgradeData.UpgradeSignHeight;
    }

    [HarmonyPatch(typeof(Building), nameof(Building.isTilePassable))]
    private static void isTilePassable_Postfix(Building __instance, Vector2 tile, ref bool __result)
    {
        if (!__result)
            return;
        
        if (!__instance.modData.TryGetValue($"{CustomFieldKey}/PendingTilesWide", out string wideStr)
            || !__instance.modData.TryGetValue($"{CustomFieldKey}/PendingTilesHigh", out string highStr))
            return;

        if (!int.TryParse(wideStr, out int pendingWide) || !int.TryParse(highStr, out int pendingHigh))
            return;
        
        int x = (int)tile.X;
        int y = (int)tile.Y;

        bool inPendingFootprint = x >= __instance.tileX.Value
            && x < __instance.tileX.Value + pendingWide
            && y >= __instance.tileY.Value
            && y < __instance.tileY.Value + pendingHigh;

        if (inPendingFootprint)
            __result = false;
    }

    [HarmonyPatch(typeof(Building), nameof(Building.FinishConstruction))]
    private static void FinishConstruction_Postfix(Building __instance)
    {
        __instance.modData.Remove($"{CustomFieldKey}/PendingTilesWide");
        __instance.modData.Remove($"{CustomFieldKey}/PendingTilesHigh");
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