using UnityEngine;
using SkiResortTycoon.Core;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// ScriptableObject defining the visual style for the entire UI.
    /// Centralizes all colors, fonts, and styling to ensure consistency.
    /// </summary>
    [CreateAssetMenu(fileName = "UITheme", menuName = "Ski Resort Tycoon/UI Theme")]
    public class UITheme : ScriptableObject
    {
        [Header("Primary Colors")]
        [Tooltip("Main color for headers and active elements")]
        public Color PrimaryColor = new Color(0.129f, 0.588f, 0.953f, 1f); // #2196F3
        
        [Tooltip("Darker shade for panel backgrounds")]
        public Color SecondaryColor = new Color(0.149f, 0.196f, 0.220f, 1f); // #263238
        
        [Tooltip("Bright highlight color for buttons and accents")]
        public Color AccentColor = new Color(0f, 0.737f, 0.831f, 1f); // #00BCD4
        
        [Header("Semantic Colors")]
        [Tooltip("Positive feedback and success states")]
        public Color SuccessColor = new Color(0.298f, 0.686f, 0.314f, 1f); // #4CAF50
        
        [Tooltip("Warning states and alerts")]
        public Color WarningColor = new Color(1f, 0.596f, 0f, 1f); // #FF9800
        
        [Tooltip("Error states and invalid actions")]
        public Color ErrorColor = new Color(0.957f, 0.263f, 0.212f, 1f); // #F44336
        
        [Header("Text Colors")]
        public Color TextPrimary = Color.white;
        public Color TextSecondary = new Color(1f, 1f, 1f, 0.7f);
        public Color TextDisabled = new Color(1f, 1f, 1f, 0.38f);
        
        [Header("Panel Backgrounds")]
        [Tooltip("Background color for main panels")]
        public Color PanelBackground = new Color(0.1f, 0.12f, 0.14f, 0.92f);
        
        [Tooltip("Background color for panel headers")]
        public Color PanelHeaderBackground = new Color(0.15f, 0.18f, 0.22f, 0.95f);
        
        [Tooltip("Color for panel borders")]
        public Color PanelBorder = new Color(1f, 1f, 1f, 0.1f);
        
        [Header("Button Colors")]
        public Color ButtonNormal = new Color(0.2f, 0.24f, 0.28f, 1f);
        public Color ButtonHover = new Color(0.28f, 0.32f, 0.36f, 1f);
        public Color ButtonPressed = new Color(0.16f, 0.19f, 0.22f, 1f);
        public Color ButtonDisabled = new Color(0.15f, 0.17f, 0.19f, 0.5f);
        public Color ButtonSelected = new Color(0f, 0.737f, 0.831f, 1f); // AccentColor
        
        [Header("Satisfaction Colors")]
        public Color SatisfactionHigh = new Color(0.298f, 0.686f, 0.314f, 1f); // Green
        public Color SatisfactionNormal = Color.white;
        public Color SatisfactionLow = new Color(1f, 0.596f, 0f, 1f); // Orange
        public Color SatisfactionCritical = new Color(0.957f, 0.263f, 0.212f, 1f); // Red
        
        [Header("Trail Difficulty Colors")]
        public Color TrailGreen = new Color(0.2f, 0.8f, 0.2f, 1f); // Beginner
        public Color TrailBlue = new Color(0.2f, 0.5f, 1f, 1f); // Intermediate
        public Color TrailBlack = new Color(0.15f, 0.15f, 0.15f, 1f); // Advanced
        public Color TrailDoubleBlack = new Color(0.9f, 0.2f, 0.2f, 1f); // Expert
        
        [Header("Animation Settings")]
        [Tooltip("Duration for panel expand/collapse animations")]
        public float PanelAnimationDuration = 0.25f;
        
        [Tooltip("Duration for tab switching animations")]
        public float TabSwitchDuration = 0.15f;
        
        [Tooltip("Duration for button hover animations")]
        public float ButtonHoverDuration = 0.1f;
        
        [Tooltip("Animation curve for panel animations")]
        public AnimationCurve PanelAnimationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        [Header("Sizing")]
        [Tooltip("Standard padding inside panels")]
        public float PanelPadding = 12f;
        
        [Tooltip("Standard spacing between elements")]
        public float ElementSpacing = 8f;
        
        [Tooltip("Standard corner radius for rounded elements")]
        public float CornerRadius = 6f;
        
        [Tooltip("Height of panel headers")]
        public float HeaderHeight = 36f;
        
        [Tooltip("Standard button height")]
        public float ButtonHeight = 32f;
        
        /// <summary>
        /// Returns the appropriate color for a satisfaction value
        /// </summary>
        public Color GetSatisfactionColor(float satisfaction)
        {
            if (satisfaction >= 1.1f)
                return SatisfactionHigh;
            else if (satisfaction >= 0.9f)
                return SatisfactionNormal;
            else if (satisfaction >= 0.7f)
                return SatisfactionLow;
            else
                return SatisfactionCritical;
        }
        
        /// <summary>
        /// Returns the appropriate color for a trail difficulty
        /// </summary>
        public Color GetDifficultyColor(TrailDifficulty difficulty)
        {
            switch (difficulty)
            {
                case TrailDifficulty.Green:
                    return TrailGreen;
                case TrailDifficulty.Blue:
                    return TrailBlue;
                case TrailDifficulty.Black:
                    return TrailBlack;
                case TrailDifficulty.DoubleBlack:
                    return TrailDoubleBlack;
                default:
                    return TrailGreen;
            }
        }
    }
}
