using System.Reflection;
using Data.GameState;
using Data.Variables;
using StompyRobot.SROptions;
using UnityEngine;

namespace ModulusTimeScaleMod;

/// <summary>
/// Sets simulation speed via <see cref="SROptionsReferences.GlobalUpdateMultiplier"/>.
/// When paused (multiplier == 0), updates <see cref="PauseStateData"/>'s stored resume
/// value via reflection so that unpausing restores the requested speed.
/// </summary>
internal static class TimeScaleGameApplier
{
    private static readonly FieldInfo? PauseStoredMultiplierField =
        typeof(PauseStateData).GetField("_currentGlobalUpdateMultiplier",
            BindingFlags.Instance | BindingFlags.NonPublic);

    internal static bool TryGetMultiplier(out IntVariableSO? mult)
    {
        mult = null;
        var refs = SROptionsReferences.Instance;
        if (refs == null) return false;
        mult = refs.GlobalUpdateMultiplier;
        return mult != null;
    }

    /// <summary>
    /// Effective speed for UI highlighting: live multiplier when running,
    /// or stored pause-resume value when simulation is paused (value == 0).
    /// </summary>
    internal static int GetDisplayedSpeed()
    {
        if (!TryGetMultiplier(out var mult) || mult == null)
            return 1;

        if (mult.Value > 0)
            return mult.Value;

        foreach (var pause in Resources.FindObjectsOfTypeAll<PauseStateData>())
        {
            if (pause == null) continue;
            if (PauseStoredMultiplierField?.GetValue(pause) is int stored && stored > 0)
                return stored;
        }

        return 1;
    }

    internal static void ApplySpeed(int speed)
    {
        if (speed != 1 && speed != 2 && speed != 4)
            speed = 1;

        if (!TryGetMultiplier(out var mult) || mult == null)
            return;

        if (mult.Value == 0)
        {
            // Game is paused: update the stored resume multiplier via reflection so that
            // when PauseStateData.SetPauseState(false) fires, it restores our speed.
            foreach (var pause in Resources.FindObjectsOfTypeAll<PauseStateData>())
            {
                if (pause == null) continue;
                PauseStoredMultiplierField?.SetValue(pause, speed);
            }

            ModulusTimeScaleModPlugin.ModLog?.LogDebug(
                $"Time scale (paused) — stored resume multiplier set to {speed}x.");
            return;
        }

        // Skip no-op to avoid spurious ValueChanged callbacks during loading.
        if (mult.Value == speed)
            return;

        mult.SetValue(speed);
        ModulusTimeScaleModPlugin.ModLog?.LogDebug($"Time scale set to {speed}x.");
    }
}
