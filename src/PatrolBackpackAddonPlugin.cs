using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

[assembly: InternalsVisibleTo("ZPatrolBackpackVerify")]

namespace ZedZone.PatrolBackpackAddon;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency(BaseModGuid, BepInDependency.DependencyFlags.HardDependency)]
public sealed class PatrolBackpackAddonPlugin : BasePlugin
{
    public const string PluginGuid = "com.druid.zedzone.patrolbackpackaddon";
    public const string PluginName = "Patrol Backpack Addon";
    public const string PluginVersion = "1.0.0";
    public const string BaseModGuid = "local.zedzone.backpack250k";

    internal static ManualLogSource LogSource;
    internal static Harmony HarmonyInstance;

    public override void Load()
    {
        LogSource = Log;
        AddonConfig.Bind(Config);

        if (!ZedBackpackBridge.Resolve(LogSource))
        {
            LogSource.LogError($"{PluginName} v{PluginVersion} disabled: base mod {BaseModGuid} (Workshop 3837676256) was not loaded.");
            return;
        }

        SafeHarmonyPatcher.ResetCounters();
        HarmonyInstance = new Harmony(PluginGuid);
        PatrolBackpackPatches.InstallAll(HarmonyInstance, LogSource);

        LogSource.LogInfo(
            $"{PluginName} v{PluginVersion} loaded: Harmony applied={SafeHarmonyPatcher.AppliedCount}, failed={SafeHarmonyPatcher.FailedCount}, " +
            $"patrol513={AddonConfig.EnablePatrolBackpack.Value}, ru={AddonConfig.EnableRussianLocalization.Value}, " +
            $"suppressGift={AddonConfig.SuppressOriginalFreeGift.Value}, hotkeys={AddonConfig.EnableQuickTabHotkeys.Value}, " +
            $"presetPack={AddonConfig.EnablePresetPackButton.Value}.");
    }
}
