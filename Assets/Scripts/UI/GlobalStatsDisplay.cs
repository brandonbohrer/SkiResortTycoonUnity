using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SkiResortTycoon.Core;
using SkiResortTycoon.UnityBridge;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Drives all top-bar stats pills: day/time, money, visitors,
    /// trails, lifts, lodges, and satisfaction percentage.
    /// Auto-finds SimulationRunner and LodgeManager if not wired.
    /// </summary>
    public class GlobalStatsDisplay : MonoBehaviour
    {
        [Header("References (auto-found if null)")]
        [SerializeField] private SimulationRunner _simulationRunner;
        [SerializeField] private LodgeManager     _lodgeManager;

        [Header("Day / Time Pill")]
        [SerializeField] private TextMeshProUGUI _dayText;
        [SerializeField] private TextMeshProUGUI _timeText;

        [Header("Money Pill")]
        [SerializeField] private TextMeshProUGUI _moneyText;
        [SerializeField] private float _moneyAnimationSpeed = 500f;

        [Header("Visitor Pill")]
        [SerializeField] private TextMeshProUGUI _visitorText;

        [Header("Trails / Lifts / Lodges Pills")]
        [SerializeField] private TextMeshProUGUI _trailsText;
        [SerializeField] private TextMeshProUGUI _liftsText;
        [SerializeField] private TextMeshProUGUI _lodgesText;

        [Header("Satisfaction Pill")]
        [SerializeField] private TextMeshProUGUI _satisfactionText;

        // Animation state
        private float _displayedMoney = 0f;

        void Start()
        {
            if (_simulationRunner == null)
                _simulationRunner = FindObjectOfType<SimulationRunner>();
            if (_lodgeManager == null)
                _lodgeManager = FindObjectOfType<LodgeManager>();

            if (_simulationRunner != null && _simulationRunner.Sim != null)
                _displayedMoney = _simulationRunner.Sim.State.Money;
            
            // Setup tooltips for stat displays
            SetupStatTooltips();
        }
        
        private void SetupStatTooltips()
        {
            if (_dayText != null)
                SetupTooltip(_dayText, TooltipTexts.Stats.DayHeader, TooltipTexts.Stats.DayContent);
            
            if (_timeText != null)
                SetupTooltip(_timeText, TooltipTexts.Stats.TimeHeader, TooltipTexts.Stats.TimeContent);
            
            if (_moneyText != null)
                SetupTooltip(_moneyText, TooltipTexts.Stats.MoneyHeader, TooltipTexts.Stats.MoneyContent);
            
            if (_visitorText != null)
                SetupTooltip(_visitorText, TooltipTexts.Stats.VisitorsHeader, TooltipTexts.Stats.VisitorsContent);
            
            if (_trailsText != null)
                SetupTooltip(_trailsText, TooltipTexts.Stats.TrailsHeader, TooltipTexts.Stats.TrailsContent);
            
            if (_liftsText != null)
                SetupTooltip(_liftsText, TooltipTexts.Stats.LiftsHeader, TooltipTexts.Stats.LiftsContent);
            
            if (_lodgesText != null)
                SetupTooltip(_lodgesText, TooltipTexts.Stats.LodgesHeader, TooltipTexts.Stats.LodgesContent);
            
            if (_satisfactionText != null)
                SetupTooltip(_satisfactionText, TooltipTexts.Stats.SatisfactionHeader, TooltipTexts.Stats.SatisfactionContent);
        }
        
        private void SetupTooltip(TextMeshProUGUI text, string header, string content)
        {
            if (text == null) return;
            
            // Enable raycast target so tooltip can detect hover
            text.raycastTarget = true;
            
            // Try to add tooltip to parent container first (the pill background)
            // This makes the entire pill hoverable, not just the text
            var parent = text.transform.parent;
            if (parent != null)
            {
                var parentTrigger = parent.GetComponent<TooltipTrigger>();
                if (parentTrigger == null)
                {
                    // Check if parent has an Image component (the pill background)
                    var parentImage = parent.GetComponent<UnityEngine.UI.Image>();
                    if (parentImage != null)
                    {
                        parentImage.raycastTarget = true;
                        parentTrigger = parent.gameObject.AddComponent<TooltipTrigger>();
                        parentTrigger.SetContent(header, content);
                        if (parent.GetComponent<UIWorldPassthrough>() == null)
                            parent.gameObject.AddComponent<UIWorldPassthrough>();
                        return;
                    }
                }
                else
                {
                    parentTrigger.SetContent(header, content);
                    if (parent.GetComponent<UIWorldPassthrough>() == null)
                        parent.gameObject.AddComponent<UIWorldPassthrough>();
                    return;
                }
            }
            
            // Fallback: add tooltip directly to text element
            var tooltipTrigger = text.GetComponent<TooltipTrigger>();
            if (tooltipTrigger == null)
            {
                tooltipTrigger = text.gameObject.AddComponent<TooltipTrigger>();
            }
            tooltipTrigger.SetContent(header, content);
            if (text.GetComponent<UIWorldPassthrough>() == null)
                text.gameObject.AddComponent<UIWorldPassthrough>();
        }

        void Update()
        {
            if (_simulationRunner == null || _simulationRunner.Sim == null) return;

            var state = _simulationRunner.Sim.State;
            var satisfaction = _simulationRunner.Sim.Satisfaction;

            UpdateTimeDisplay(state);
            UpdateMoneyDisplay(state);
            UpdateVisitorDisplay(state);
            UpdateStructureCounts(state);
            UpdateSatisfactionDisplay(satisfaction.Satisfaction);
        }

        // ── Time ──────────────────────────────────────────────────────────

        private void UpdateTimeDisplay(SimulationState state)
        {
            if (_dayText != null)
                _dayText.text = $"Day {state.DayIndex}";

            if (_timeText != null)
                _timeText.text = FormatTime(state.TimeMinutes);
        }

        // ── Money ─────────────────────────────────────────────────────────

        private void UpdateMoneyDisplay(SimulationState state)
        {
            if (_moneyText == null) return;

            float target = state.Money;

            // Animate toward target
            if (Mathf.Abs(_displayedMoney - target) > 1f)
            {
                float speed = Mathf.Max(_moneyAnimationSpeed, Mathf.Abs(target - _displayedMoney) * 2f);
                _displayedMoney = Mathf.MoveTowards(_displayedMoney, target, speed * Time.deltaTime);
            }
            else
            {
                _displayedMoney = target;
            }

            _moneyText.text = FormatMoney(Mathf.RoundToInt(_displayedMoney));
        }

        /// <summary>
        /// Compact 3-significant-figure format: 1.07k, 10.7k, 107k, 1.07M, etc.
        /// </summary>
        private static string FormatMoney(int amount)
        {
            if (amount < 0)
                return "-" + FormatMoney(-amount);
            if (amount >= 1_000_000)
                return (amount / 1_000_000f).ToString("G3") + "M";
            if (amount >= 1_000)
                return (amount / 1_000f).ToString("G3") + "k";
            return amount.ToString();
        }

        // ── Visitors (active skiers on mountain, from SimulationState) ────

        private void UpdateVisitorDisplay(SimulationState state)
        {
            if (_visitorText != null)
                _visitorText.text = state.ActiveSkierCount.ToString();
        }

        // ── Trails / Lifts / Lodges ───────────────────────────────────────

        private void UpdateStructureCounts(SimulationState state)
        {
            if (_trailsText != null)
                _trailsText.text = state.TrailsBuilt.ToString();

            if (_liftsText != null)
                _liftsText.text = state.LiftsBuilt.ToString();

            if (_lodgesText != null)
                _lodgesText.text = (_lodgeManager != null ? _lodgeManager.LodgeCount : 0).ToString();
        }

        // ── Satisfaction ──────────────────────────────────────────────────

        private void UpdateSatisfactionDisplay(float satisfaction)
        {
            if (_satisfactionText != null)
                _satisfactionText.text = $"{Mathf.RoundToInt(satisfaction)}%";
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private static string FormatTime(float totalMinutes)
        {
            int h24  = (int)(totalMinutes / 60f);
            int mins = (int)(totalMinutes % 60f);
            int h12  = h24 % 12;
            if (h12 == 0) h12 = 12;
            return $"{h12}:{mins:D2} {(h24 >= 12 ? "PM" : "AM")}";
        }

        private static Color GetDefaultSatisfactionColor(float satisfaction)
        {
            if (satisfaction >= 65f) return new Color(0.4f, 1f, 0.4f);
            if (satisfaction >= 45f) return Color.white;
            if (satisfaction >= 30f) return new Color(1f, 0.6f, 0f);
            return new Color(1f, 0.2f, 0.2f);
        }
    }
}
