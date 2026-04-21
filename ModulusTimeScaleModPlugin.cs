using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using ModulusModLoader;
using UnityEngine;

namespace ModulusTimeScaleMod;

[BepInPlugin(PluginInfo.PluginGuid, PluginInfo.PluginName, PluginInfo.PluginVersion)]
[BepInDependency(LoaderPluginInfo.Guid)]
public class ModulusTimeScaleModPlugin : BaseUnityPlugin
{
    internal static ManualLogSource? ModLog { get; private set; }

    private Harmony? _harmony;

    private void Awake()
    {
        ModLog = Logger;
        ModLog.LogInfo($"{PluginInfo.PluginName} v{PluginInfo.PluginVersion}");

        try
        {
            _harmony = new Harmony(PluginInfo.PluginGuid);
            _harmony.PatchAll(typeof(ModulusTimeScaleModPlugin).Assembly);
            ModLog.LogDebug("Harmony patches applied.");
        }
        catch (Exception ex)
        {
            ModLog.LogError($"Harmony setup failed: {ex}");
        }

        // FactoryLoaded fires after FinishLoadLevel restores the multiplier from 0 and
        // the entire HUD is active — safe to inject UI and apply speed here.
        ModGameLifecycle.FactoryLoaded += OnFactoryLoaded;

        // FactoryClearing fires before the level is wiped; clean up injected UI so
        // TryInject can start fresh on the next load.
        ModGameLifecycle.FactoryClearing += OnFactoryClearing;
    }

    private void OnDestroy()
    {
        ModGameLifecycle.FactoryLoaded   -= OnFactoryLoaded;
        ModGameLifecycle.FactoryClearing -= OnFactoryClearing;
        TimeScaleToolbarInjector.Cleanup();

        try { _harmony?.UnpatchSelf(); }
        catch (Exception ex) { ModLog?.LogWarning($"Unpatch: {ex.Message}"); }
    }

    private static void OnFactoryLoaded()
    {
        // Inject the speed buttons now that the HUD is fully active.
        // FindObjectOfType works whether DayNightDropdown is in the Factory scene
        // or DontDestroyOnLoad — avoids timing issues with Awake patch ordering.
        var dnd = UnityEngine.Object.FindFirstObjectByType<Presentation.UI.HUD.DayNightDropdown>();
        if (dnd != null)
            TimeScaleToolbarInjector.TryInject(dnd);
        else
            ModLog?.LogWarning("DayNightDropdown not found in scene; speed buttons not injected.");

        // Highlight follows the game's current multiplier (from save / last session state).
        TimeScaleToolbarInjector.RefreshHighlight();
    }

    private static void OnFactoryClearing()
    {
        TimeScaleToolbarInjector.Cleanup();
    }
}
