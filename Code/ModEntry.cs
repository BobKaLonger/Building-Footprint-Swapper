using HarmonyLib;
using StardewModdingAPI;

namespace BuildingFootprintSwapper;

internal class ModEntry : Mod
{
    internal static IMonitor ModMonitor { get; private set; } = null!;

    public override void Entry(IModHelper helper)
    {
        ModMonitor = this.Monitor;

        var harmony = new Harmony(this.ModManifest.UniqueID);
        CarpenterMenuPatcher.Apply(harmony);

        this.Monitor.Log("Building Footprint Swapper initialized.", LogLevel.Debug);
    }
}