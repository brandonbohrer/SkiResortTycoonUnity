using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using SkiResortTycoon.Core;
using SkiResortTycoon.UnityBridge;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Displays high-level game stats in the top bar.
    /// Shows time, money (with animation), visitors, and satisfaction.
    /// </summary>
    public class GlobalStatsDisplay : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SimulationRunner _simulationRunner;
        
        [Header("Time Display")]
        [SerializeField] private TextMeshProUGUI _dayText;
        [SerializeField] private TextMeshProUGUI _timeText;
        
        [Header("Money Display")]
        [SerializeField] private TextMeshProUGUI _moneyText;
        [SerializeField] private float _moneyAnimationSpeed = 500f;
        
        [Header("Visitor Display")]
        [SerializeField] private TextMeshProUGUI _visitorText;
        [SerializeField] private Image _visitorTrendIcon;
        [SerializeField] private Sprite _trendUpSprite;
        [SerializeField] private Sprite _trendDownSprite;
        [SerializeField] private Sprite _trendFlatSprite;
        
        [Header("Satisfaction Display")]
        [SerializeField] private TextMeshProUGUI _satisfactionText;
        [SerializeField] private Image _satisfactionBar;
        [SerializeField] private Image _satisfactionIcon;
        
        // Animation state
        private float _displayedMoney = 0f;
        private int _lastVisitorCount = 0;
        private float _lastVisitorCheckTime = 0f;
        private int _visitorTrend = 0; // -1, 0, 1
        
        void Start()
        {
            // Initialize displayed money to current value
            if (_simulationRunner != null && _simulationRunner.Sim != null)
            {
                _displayedMoney = _simulationRunner.Sim.State.Money;
            }
        }
        
        void Update()
        {
            if (_simulationRunner == null || _simulationRunner.Sim == null)
                return;
            
            var state = _simulationRunner.Sim.State;
            var satisfaction = _simulationRunner.Sim.Satisfaction;
            
            UpdateTimeDisplay(state);
            UpdateMoneyDisplay(state);
            UpdateVisitorDisplay(state);
            UpdateSatisfactionDisplay(satisfaction.Satisfaction);
        }
        
        private void UpdateTimeDisplay(SimulationState state)
        {
            if (_dayText != null)
            {
                _dayText.text = $"Day {state.DayIndex}";
            }
            
            if (_timeText != null)
            {
                _timeText.text = FormatTime(state.TimeMinutes);
            }
        }
        
        private void UpdateMoneyDisplay(SimulationState state)
        {
            if (_moneyText == null) return;
            
            float targetMoney = state.Money;
            
            // Animate toward target
            if (Mathf.Abs(_displayedMoney - targetMoney) > 1f)
            {
                float direction = targetMoney > _displayedMoney ? 1f : -1f;
                float speed = Mathf.Max(_moneyAnimationSpeed, Mathf.Abs(targetMoney - _displayedMoney) * 2f);
                _displayedMoney += direction * speed * Time.deltaTime;
                
                // Clamp to prevent overshooting
                if ((direction > 0 && _displayedMoney > targetMoney) ||
                    (direction < 0 && _displayedMoney < targetMoney))
                {
                    _displayedMoney = targetMoney;
                }
            }
            else
            {
                _displayedMoney = targetMoney;
            }
            
            // Color based on change
            Color textColor = Color.white;
            if (targetMoney > _displayedMoney + 10)
            {
                textColor = new Color(0.4f, 1f, 0.4f); // Green - gaining money
            }
            else if (targetMoney < _displayedMoney - 10)
            {
                textColor = new Color(1f, 0.4f, 0.4f); // Red - losing money
            }
            
            _moneyText.text = $"${Mathf.RoundToInt(_displayedMoney):N0}";
            _moneyText.color = textColor;
        }
        
        private void UpdateVisitorDisplay(SimulationState state)
        {
            if (_visitorText != null)
            {
                _visitorText.text = $"{state.VisitorsToday}";
            }
            
            // Update trend every 5 seconds
            if (Time.time - _lastVisitorCheckTime > 5f)
            {
                int delta = state.VisitorsToday - _lastVisitorCount;
                _visitorTrend = delta > 0 ? 1 : (delta < 0 ? -1 : 0);
                _lastVisitorCount = state.VisitorsToday;
                _lastVisitorCheckTime = Time.time;
                
                // Update trend icon
                if (_visitorTrendIcon != null)
                {
                    if (_visitorTrend > 0 && _trendUpSprite != null)
                    {
                        _visitorTrendIcon.sprite = _trendUpSprite;
                        _visitorTrendIcon.color = new Color(0.4f, 1f, 0.4f);
                    }
                    else if (_visitorTrend < 0 && _trendDownSprite != null)
                    {
                        _visitorTrendIcon.sprite = _trendDownSprite;
                        _visitorTrendIcon.color = new Color(1f, 0.4f, 0.4f);
                    }
                    else if (_trendFlatSprite != null)
                    {
                        _visitorTrendIcon.sprite = _trendFlatSprite;
                        _visitorTrendIcon.color = Color.white;
                    }
                }
            }
        }
        
        private void UpdateSatisfactionDisplay(float satisfaction)
        {
            UITheme theme = UIManager.Instance?.Theme;
            Color satisfactionColor = theme != null ? 
                theme.GetSatisfactionColor(satisfaction) : 
                GetDefaultSatisfactionColor(satisfaction);
            
            if (_satisfactionText != null)
            {
                _satisfactionText.text = $"{satisfaction:P0}";
                _satisfactionText.color = satisfactionColor;
            }
            
            if (_satisfactionBar != null)
            {
                _satisfactionBar.fillAmount = Mathf.Clamp01(satisfaction);
                _satisfactionBar.color = satisfactionColor;
            }
            
            if (_satisfactionIcon != null)
            {
                _satisfactionIcon.color = satisfactionColor;
            }
        }
        
        private Color GetDefaultSatisfactionColor(float satisfaction)
        {
            if (satisfaction >= 1.1f)
                return new Color(0.4f, 1f, 0.4f); // Green
            else if (satisfaction >= 0.9f)
                return Color.white;
            else if (satisfaction >= 0.7f)
                return new Color(1f, 0.6f, 0f); // Orange
            else
                return new Color(1f, 0.2f, 0.2f); // Red
        }
        
        private string FormatTime(float totalMinutes)
        {
            int hours24 = (int)(totalMinutes / 60f);
            int minutes = (int)(totalMinutes % 60f);
            
            int hours12 = hours24 % 12;
            if (hours12 == 0) hours12 = 12;
            
            string amPm = hours24 >= 12 ? "PM" : "AM";
            
            return $"{hours12}:{minutes:D2} {amPm}";
        }
    }
}
