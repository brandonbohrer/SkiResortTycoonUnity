using UnityEngine;
using UnityEngine.UI;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Subtle uGUI <see cref="Button"/> ColorTint hover/press without breaking prefabs that use
    /// white <see cref="ColorBlock.normalColor"/> and put the real tint on <see cref="Image.color"/>.
    /// </summary>
    public static class ButtonHoverFeedback
    {
        private const float WhiteThreshold = 0.97f;

        // Slight but visible hover — keep blends low so we do not overpower the art tint.
        private const float HoverBlendDark = 0.16f;
        private const float HoverBlendLight = 0.11f;
        private const float LumDarkThreshold = 0.42f;

        // Defaults when no UITheme (e.g. main menu) — match UITheme field initializers.
        private static readonly Color DefaultNavBlueHoverLightenTarget = new Color(0.88f, 0.95f, 1f, 1f);
        private const float DefaultNavBlueHoverLightenBlend = 0.4f;

        // Press = darken toward black only (no lerp into theme blues — that skews hue to green on orange/blue buttons).
        private const float PressDarkenAmount = 0.12f;
        private const float PressDarkenAmountBlueIdle = 0.05f; // same navy family — avoid a “black” flash on click

        /// <summary>
        /// Updates highlighted/pressed/selected/fade on the button. Does <b>not</b> change
        /// <see cref="ColorBlock.normalColor"/> — that stays whatever the prefab or caller set.
        /// </summary>
        public static void Apply(Button button, UITheme theme = null)
        {
            if (button == null) return;

            ColorBlock cb = button.colors;
            Color baseline = GetVisualBaseline(button, cb);

            float lum = baseline.r * 0.299f + baseline.g * 0.587f + baseline.b * 0.114f;

            Color hoverRef = theme != null ? theme.ButtonHover : new Color(0.678f, 0.847f, 0.902f, baseline.a);
            bool blueIdle = IsUnselectedBlueNavStyle(baseline);

            Color navBlueTarget = theme != null ? theme.ButtonHoverNavBlueLightenTarget : DefaultNavBlueHoverLightenTarget;
            float navBlueBlend = theme != null ? theme.ButtonHoverNavBlueLightenBlend : DefaultNavBlueHoverLightenBlend;

            Color highlighted;
            if (blueIdle)
            {
                navBlueTarget.a = baseline.a;
                highlighted = Color.Lerp(baseline, navBlueTarget, navBlueBlend);
                highlighted.a = baseline.a;
            }
            else
            {
                float hoverBlend = lum < LumDarkThreshold ? HoverBlendDark : HoverBlendLight;
                highlighted = Color.Lerp(baseline, hoverRef, hoverBlend);
            }

            Color pressed = Color.Lerp(baseline, Color.black, blueIdle ? PressDarkenAmountBlueIdle : PressDarkenAmount);
            pressed.a = baseline.a;

            cb.highlightedColor = highlighted;
            cb.pressedColor = pressed;
            // Selected (post-click / keyboard focus) must match resting look — if this equals highlighted,
            // the button stays “lit” until something else is selected (very noticeable in the Manager).
            cb.selectedColor = baseline;
            if (theme != null)
                cb.disabledColor = theme.ButtonDisabled;
            cb.colorMultiplier = 1f;
            cb.fadeDuration = theme != null ? theme.ButtonHoverDuration : 0.12f;

            button.colors = cb;
        }

        /// <summary>
        /// Sets highlighted/pressed/selected to the current visual baseline so the button does not
        /// change tint on hover or press (e.g. locked dock options that stay greyed out).
        /// Call after setting the target <see cref="Image.color"/> if the baseline comes from the image.
        /// </summary>
        public static void ApplyWithoutHoverTint(Button button, UITheme theme = null)
        {
            if (button == null) return;

            ColorBlock cb = button.colors;
            Color baseline = GetVisualBaseline(button, cb);
            cb.highlightedColor = baseline;
            cb.pressedColor = baseline;
            cb.selectedColor = baseline;
            if (theme != null)
                cb.disabledColor = theme.ButtonDisabled;
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0f;
            button.colors = cb;
        }

        /// <summary>
        /// When the color block normal is white, the visible tint usually lives on the <see cref="Image"/>.
        /// Otherwise the configured <see cref="ColorBlock.normalColor"/> is the baseline (e.g. build bar grays).
        /// </summary>
        private static Color GetVisualBaseline(Button button, ColorBlock cb)
        {
            var img = button.targetGraphic as Image;
            if (img == null)
                return cb.normalColor;

            if (!IsApproximatelyWhite(cb.normalColor))
                return cb.normalColor;

            return img.color;
        }

        private static bool IsApproximatelyWhite(Color c)
        {
            return c.r >= WhiteThreshold && c.g >= WhiteThreshold && c.b >= WhiteThreshold;
        }

        /// <summary>
        /// Matches prefab idle state for pause/speed controls (e.g. r≈0.13, g≈0.31, b≈0.45).
        /// Excludes teal/green difficulty chips (higher R or G) and neutral grays (B not ahead of R).
        /// Orange selected state is unchanged — it does not satisfy these constraints.
        /// </summary>
        private static bool IsUnselectedBlueNavStyle(Color c)
        {
            float lum = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
            if (lum > 0.48f)
                return false;
            if (c.r > 0.24f)
                return false;
            if (c.b < 0.32f)
                return false;
            if (c.g > 0.44f)
                return false;
            return c.b >= c.r + 0.1f;
        }

        public static void ApplyUnder(Transform root, UITheme theme = null)
        {
            if (root == null) return;
            foreach (var b in root.GetComponentsInChildren<Button>(true))
                Apply(b, theme);
        }

        public static void ApplyAllInScene(UITheme theme = null)
        {
            foreach (var b in Object.FindObjectsOfType<Button>(true))
                Apply(b, theme);
        }
    }
}
