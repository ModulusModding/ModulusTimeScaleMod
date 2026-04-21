using System;
using System.Runtime.CompilerServices;
using Data.SaveData.PersistentSOs;
using HarmonyLib;

namespace ModulusTimeScaleMod.Patches;

/// <summary>
/// Replaces <see cref="SaveInfoPersistentSO.GetUpdatedTotalPlaytime"/> so accumulated play time
/// advances with <see cref="UnityEngine.Time.realtimeSinceStartupAsDouble"/> instead of
/// <c>DateTime.Now - _loadSaveDateTime</c>. That keeps save / UI play time aligned with real
/// wall time even when <see cref="UnityEngine.Time.timeScale"/> or scaled waits change how often
/// saves flush while simulation speed (<c>GlobalUpdateMultiplier</c>) is raised.
/// </summary>
[HarmonyPatch(typeof(SaveInfoPersistentSO), nameof(SaveInfoPersistentSO.GetUpdatedTotalPlaytime))]
internal static class SaveInfoPlaytimeRealtimePatch
{
    private static readonly ConditionalWeakTable<SaveInfoPersistentSO, StrongBox<double>> LastRealtimeBySave =
        new();

    [HarmonyPrefix]
    private static bool Prefix(SaveInfoPersistentSO __instance, ref double __result)
    {
        var traverse = Traverse.Create(__instance);
        double total = traverse.Field<double>("_totalPlayTimeMins").Value;
        double paused = traverse.Field<double>("_pausedDuration").Value;
        double nowRt = UnityEngine.Time.realtimeSinceStartupAsDouble;

        if (!LastRealtimeBySave.TryGetValue(__instance, out StrongBox<double>? lastRtBox))
        {
            lastRtBox = new StrongBox<double>(BootstrapLastRealtime(traverse, nowRt));
            LastRealtimeBySave.Add(__instance, lastRtBox);
        }

        double deltaMins = (nowRt - lastRtBox.Value) / 60.0;
        total += deltaMins - paused;
        traverse.Field("_totalPlayTimeMins").SetValue(total);
        traverse.Field("_loadSaveDateTime").SetValue(DateTime.Now);
        traverse.Field("_pausedDuration").SetValue(0.0);
        lastRtBox.Value = nowRt;
        __result = total;
        return false;
    }

    /// <summary>
    /// Aligns the realtime anchor with the game's <c>_loadSaveDateTime</c> when we did not see
    /// load or reset yet (no anchor row in <see cref="LastRealtimeBySave"/>).
    /// </summary>
    private static double BootstrapLastRealtime(Traverse traverse, double nowRt)
    {
        DateTime loadTime = traverse.Field<DateTime>("_loadSaveDateTime").Value;
        double wallSinceLoadMin = (DateTime.Now - loadTime).TotalMinutes;
        return nowRt - wallSinceLoadMin * 60.0;
    }

    internal static void ResetRealtimeAnchor(SaveInfoPersistentSO instance)
    {
        double nowRt = UnityEngine.Time.realtimeSinceStartupAsDouble;
        if (!LastRealtimeBySave.TryGetValue(instance, out StrongBox<double>? box))
        {
            box = new StrongBox<double>(nowRt);
            LastRealtimeBySave.Add(instance, box);
        }
        else
            box.Value = nowRt;
    }
}

[HarmonyPatch(typeof(SaveInfoPersistentSO), "ApplyLoadedSaveData")]
internal static class SaveInfoPlaytimeApplyLoadedPatch
{
    [HarmonyPostfix]
    private static void Postfix(SaveInfoPersistentSO __instance) =>
        SaveInfoPlaytimeRealtimePatch.ResetRealtimeAnchor(__instance);
}

[HarmonyPatch(typeof(SaveInfoPersistentSO), nameof(SaveInfoPersistentSO.ResetToDefaults))]
internal static class SaveInfoPlaytimeResetDefaultsPatch
{
    [HarmonyPostfix]
    private static void Postfix(SaveInfoPersistentSO __instance) =>
        SaveInfoPlaytimeRealtimePatch.ResetRealtimeAnchor(__instance);
}
