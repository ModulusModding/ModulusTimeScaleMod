using System;
using System.Collections.Generic;
using System.Reflection;
using Data.GameState;
using Data.Variables;
using Presentation.UI.HUD;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ModulusTimeScaleMod;

/// <summary>
/// Injects 1x / 2x / 4x buttons into the factory top bar, immediately after the
/// DayNightDropdown control. Buttons are built from scratch (never cloned) to avoid
/// the Awake-recursion stack-overflow that cloning a DayNightDropdown would cause.
/// </summary>
internal static class TimeScaleToolbarInjector
{
    private static readonly FieldInfo? DayNightButtonField =
        typeof(DayNightDropdown).GetField("_button", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly List<GameObject> InjectedRows = new();
    private static TimeScaleUiSync? _sync;

    internal static void TryInject(DayNightDropdown host)
    {
        InjectedRows.RemoveAll(static g => g == null);
        if (InjectedRows.Count > 0 || host == null)
            return;

        Transform? parent = host.transform.parent;
        if (parent == null)
        {
            ModulusTimeScaleModPlugin.ModLog?.LogWarning(
                "DayNightDropdown has no parent transform; speed buttons not injected.");
            return;
        }

        // Extract visual style from the host button — style only, never clone.
        Button? templateBtn = DayNightButtonField?.GetValue(host) as Button;
        HudButtonStyle style = templateBtn != null
            ? HudButtonStyle.From(templateBtn)
            : HudButtonStyle.Fallback();

        ModulusTimeScaleModPlugin.ModLog?.LogDebug(
            $"Injecting speed buttons. Parent: {parent.name}, " +
            $"DayNight sibling: {host.transform.GetSiblingIndex()}, " +
            $"Style fontSize: {style.FontSize:F1}, hasFont: {style.Font != null}");

        int insertIndex = host.transform.GetSiblingIndex() + 1;

        for (int i = 0; i < TimeScaleConfig.SpeedValues.Length; i++)
        {
            int speed = TimeScaleConfig.SpeedValues[i];
            GameObject go = BuildButton(parent, insertIndex + i, speed, style);
            InjectedRows.Add(go);
        }

        if (InjectedRows.Count > 0)
            _sync = InjectedRows[0].AddComponent<TimeScaleUiSync>();

        RefreshHighlight();

        ModulusTimeScaleModPlugin.ModLog?.LogInfo(
            $"Speed buttons (1x / 2x / 4x) injected after '{host.gameObject.name}'.");
    }

    private static GameObject BuildButton(
        Transform parent, int siblingIndex, int speed, HudButtonStyle style)
    {
        var go = new GameObject($"ModulusTimeScale_{speed}x", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.transform.SetSiblingIndex(siblingIndex);

        // LayoutElement: modest width for "1x"/"2x"/"4x" with a little side margin.
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth  = SpeedButtonPreferredWidth;
        le.preferredHeight = style.BtnHeight > 4f ? style.BtnHeight : 32f;

        // Background image (optional sprite from template, otherwise a solid rectangle).
        var img = go.AddComponent<Image>();
        if (style.BgSprite != null)
        {
            img.sprite = style.BgSprite;
            img.type   = style.BgType;
        }
        img.color = NormalColor;

        // Button component.
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        // Use neutral ColorBlock so our manual color tints are clear.
        btn.colors = ColorBlock.defaultColorBlock;
        int captured = speed;
        btn.onClick.AddListener(() => OnSpeedClicked(captured));

        // Text label.
        var labelGo   = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        var labelRect = (RectTransform)labelGo.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        // Small horizontal inset so glyphs don't sit on the dividers.
        labelRect.offsetMin = new Vector2(LabelHorizontalPadding, 0f);
        labelRect.offsetMax = new Vector2(-LabelHorizontalPadding, 0f);

        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.text      = $"{speed}x";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = LabelColor;
        if (style.Font != null)    tmp.font     = style.Font;
        tmp.fontSize = style.FontSize > 2f ? style.FontSize : 13f;

        return go;
    }

    private static void OnSpeedClicked(int speed)
    {
        TimeScaleGameApplier.ApplySpeed(speed);
        RefreshHighlight();
    }

    internal static void RefreshHighlight()
    {
        int current = TimeScaleGameApplier.GetDisplayedSpeed();
        foreach (var go in InjectedRows)
        {
            if (go == null) continue;
            if (!go.name.StartsWith("ModulusTimeScale_", StringComparison.Ordinal)) continue;
            var tail    = go.name["ModulusTimeScale_".Length..];
            var numPart = tail.EndsWith("x", StringComparison.Ordinal) ? tail[..^1] : tail;
            if (!int.TryParse(numPart, out int speed)) continue;

            var img = go.GetComponent<Image>();
            if (img != null) img.color = speed == current ? SelectedBgColor : NormalColor;

            var tmp = go.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null) tmp.color = speed == current ? SelectedLabelColor : LabelColor;
        }
    }

    private const float SpeedButtonPreferredWidth = 40f;
    private const float LabelHorizontalPadding    = 3f;

    // Normal: dark panel matching HUD background, semi-transparent.
    private static readonly Color NormalColor       = new(0.15f, 0.16f, 0.21f, 0.85f);
    // Selected: warm amber that pops against the dark HUD.
    private static readonly Color SelectedBgColor   = new(0.80f, 0.55f, 0.10f, 1.00f);
    private static readonly Color LabelColor        = new(0.88f, 0.88f, 0.88f, 1.00f);
    private static readonly Color SelectedLabelColor = new(0.10f, 0.06f, 0.02f, 1.00f);

    internal static void Cleanup()
    {
        if (_sync != null) { Object.Destroy(_sync); _sync = null; }
        foreach (var go in InjectedRows) { if (go != null) Object.Destroy(go); }
        InjectedRows.Clear();
    }

    // ── Captured button style ─────────────────────────────────────────────────

    private readonly struct HudButtonStyle
    {
        internal readonly Sprite?        BgSprite;
        internal readonly Image.Type     BgType;
        internal readonly TMP_FontAsset? Font;
        internal readonly float          FontSize;
        internal readonly float          BtnWidth;
        internal readonly float          BtnHeight;

        internal static HudButtonStyle From(Button btn)
        {
            Sprite?        bgSprite = null;
            Image.Type     bgType   = Image.Type.Simple;
            TMP_FontAsset? font     = null;
            float          fontSize = 13f;
            float          w = 36f, h = 32f;

            // Background sprite (optional — skip if this is an icon-only button).
            var img = btn.GetComponent<Image>();
            if (img != null && img.sprite != null)
            { bgSprite = img.sprite; bgType = img.type; }

            // RectTransform size (may be zero inside a LayoutGroup — that's fine,
            // we fall back to the LayoutElement or our defaults).
            var rt = btn.GetComponent<RectTransform>();
            if (rt != null)
            {
                if (rt.sizeDelta.x > 4f) w = rt.sizeDelta.x;
                if (rt.sizeDelta.y > 4f) h = rt.sizeDelta.y;
            }

            // Check LayoutElement on the button or a sibling for height hint.
            var le = btn.GetComponent<LayoutElement>();
            if (le != null)
            {
                if (le.preferredWidth  > 4f) w = le.preferredWidth;
                if (le.preferredHeight > 4f) h = le.preferredHeight;
            }

            // Font from any TMP child of the button's parent HUD row.
            var tmp = btn.GetComponentInChildren<TMP_Text>(true)
                      ?? btn.transform.parent?.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null) { font = tmp.font; fontSize = tmp.fontSize > 2f ? tmp.fontSize : fontSize; }

            return new HudButtonStyle(bgSprite, bgType, font, fontSize, w, h);
        }

        internal static HudButtonStyle Fallback() =>
            new(null, Image.Type.Simple, null, 13f, 36f, 32f);

        private HudButtonStyle(Sprite? sp, Image.Type bt, TMP_FontAsset? fn,
                               float fs, float w, float h)
        {
            BgSprite = sp; BgType = bt; Font = fn; FontSize = fs; BtnWidth = w; BtnHeight = h;
        }
    }

    // ── Event sync ───────────────────────────────────────────────────────────

    private sealed class TimeScaleUiSync : MonoBehaviour
    {
        private IntVariableSO? _mult;
        private readonly List<PauseStateData> _pauseList = new();
        private bool _attached;

        private void Update()
        {
            if (_attached) return;
            TryAttach();
        }

        private void TryAttach()
        {
            if (!TimeScaleGameApplier.TryGetMultiplier(out var m) || m == null)
                return;

            _mult = m;
            _mult.ValueChanged += OnMultiplierChanged;

            foreach (var p in Resources.FindObjectsOfTypeAll<PauseStateData>())
            {
                if (p == null) continue;
                p.PauseStateChanged += OnPauseChanged;
                _pauseList.Add(p);
            }

            _attached = true;
            TimeScaleToolbarInjector.RefreshHighlight();
            ModulusTimeScaleModPlugin.ModLog?.LogDebug("TimeScaleUiSync attached to multiplier events.");
        }

        private void OnDestroy()
        {
            if (_mult != null) _mult.ValueChanged -= OnMultiplierChanged;
            foreach (var p in _pauseList) { if (p != null) p.PauseStateChanged -= OnPauseChanged; }
            _pauseList.Clear();
        }

        private void OnMultiplierChanged(int _) => TimeScaleToolbarInjector.RefreshHighlight();
        private void OnPauseChanged(bool _)     => TimeScaleToolbarInjector.RefreshHighlight();
    }
}
