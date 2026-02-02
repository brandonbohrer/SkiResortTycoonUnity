using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Applies UITheme colors to UI elements at runtime.
    /// Attach to any UI element that should use theme colors.
    /// </summary>
    public class ThemeApplicator : MonoBehaviour
    {
        public enum ThemeColor
        {
            Primary,
            Secondary,
            Accent,
            Success,
            Warning,
            Error,
            TextPrimary,
            TextSecondary,
            TextDisabled,
            PanelBackground,
            PanelHeaderBackground,
            PanelBorder,
            ButtonNormal,
            ButtonHover,
            ButtonPressed,
            ButtonDisabled,
            ButtonSelected
        }
        
        [Header("Target Elements")]
        [SerializeField] private Image[] _images;
        [SerializeField] private TextMeshProUGUI[] _texts;
        [SerializeField] private Button[] _buttons;
        
        [Header("Color Settings")]
        [SerializeField] private ThemeColor _imageColor = ThemeColor.PanelBackground;
        [SerializeField] private ThemeColor _textColor = ThemeColor.TextPrimary;
        [SerializeField] private ThemeColor _buttonNormalColor = ThemeColor.ButtonNormal;
        [SerializeField] private ThemeColor _buttonSelectedColor = ThemeColor.ButtonSelected;
        
        [Header("Apply On")]
        [SerializeField] private bool _applyOnStart = true;
        [SerializeField] private bool _applyOnEnable = false;
        
        void Start()
        {
            if (_applyOnStart)
            {
                ApplyTheme();
            }
        }
        
        void OnEnable()
        {
            if (_applyOnEnable)
            {
                ApplyTheme();
            }
        }
        
        public void ApplyTheme()
        {
            var theme = UIManager.Instance?.Theme;
            if (theme == null) return;
            
            // Apply to images
            Color imgColor = GetColor(theme, _imageColor);
            foreach (var img in _images)
            {
                if (img != null)
                {
                    img.color = imgColor;
                }
            }
            
            // Apply to texts
            Color txtColor = GetColor(theme, _textColor);
            foreach (var txt in _texts)
            {
                if (txt != null)
                {
                    txt.color = txtColor;
                }
            }
            
            // Apply to buttons
            Color btnNormal = GetColor(theme, _buttonNormalColor);
            Color btnSelected = GetColor(theme, _buttonSelectedColor);
            foreach (var btn in _buttons)
            {
                if (btn != null)
                {
                    var colors = btn.colors;
                    colors.normalColor = btnNormal;
                    colors.highlightedColor = theme.ButtonHover;
                    colors.pressedColor = theme.ButtonPressed;
                    colors.disabledColor = theme.ButtonDisabled;
                    btn.colors = colors;
                }
            }
        }
        
        private Color GetColor(UITheme theme, ThemeColor colorType)
        {
            switch (colorType)
            {
                case ThemeColor.Primary: return theme.PrimaryColor;
                case ThemeColor.Secondary: return theme.SecondaryColor;
                case ThemeColor.Accent: return theme.AccentColor;
                case ThemeColor.Success: return theme.SuccessColor;
                case ThemeColor.Warning: return theme.WarningColor;
                case ThemeColor.Error: return theme.ErrorColor;
                case ThemeColor.TextPrimary: return theme.TextPrimary;
                case ThemeColor.TextSecondary: return theme.TextSecondary;
                case ThemeColor.TextDisabled: return theme.TextDisabled;
                case ThemeColor.PanelBackground: return theme.PanelBackground;
                case ThemeColor.PanelHeaderBackground: return theme.PanelHeaderBackground;
                case ThemeColor.PanelBorder: return theme.PanelBorder;
                case ThemeColor.ButtonNormal: return theme.ButtonNormal;
                case ThemeColor.ButtonHover: return theme.ButtonHover;
                case ThemeColor.ButtonPressed: return theme.ButtonPressed;
                case ThemeColor.ButtonDisabled: return theme.ButtonDisabled;
                case ThemeColor.ButtonSelected: return theme.ButtonSelected;
                default: return Color.white;
            }
        }
    }
}
