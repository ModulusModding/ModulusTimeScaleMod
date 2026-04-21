namespace ModulusTimeScaleMod;

/// <summary>
/// Speed options for the toolbar. No persistence — the player adjusts per session;
/// the live value comes from the game's <c>GlobalUpdateMultiplier</c>.
/// </summary>
internal static class TimeScaleConfig
{
    internal static readonly int[] SpeedValues = { 1, 2, 4 };
}
