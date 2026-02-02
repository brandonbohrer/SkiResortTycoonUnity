using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SkiResortTycoon.Core;
using SkiResortTycoon.UnityBridge;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Resort panel tab showing visitor statistics.
    /// Displays skier counts, skill distribution, and satisfaction breakdown.
    /// </summary>
    public class VisitorsTab : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SimulationRunner _simulationRunner;
        [SerializeField] private SkierVisualizer _skierVisualizer;
        
        [Header("Text Elements")]
        [SerializeField] private TextMeshProUGUI _totalVisitorsText;
        [SerializeField] private TextMeshProUGUI _onMountainText;
        [SerializeField] private TextMeshProUGUI _avgSatisfactionText;
        [SerializeField] private TextMeshProUGUI _runsCompletedText;
        
        [Header("Skill Distribution")]
        [SerializeField] private Image _beginnerBar;
        [SerializeField] private Image _intermediateBar;
        [SerializeField] private Image _advancedBar;
        [SerializeField] private Image _expertBar;
        [SerializeField] private TextMeshProUGUI _beginnerPercentText;
        [SerializeField] private TextMeshProUGUI _intermediatePercentText;
        [SerializeField] private TextMeshProUGUI _advancedPercentText;
        [SerializeField] private TextMeshProUGUI _expertPercentText;
        
        [Header("Satisfaction Distribution")]
        [SerializeField] private Image _satisfiedBar;
        [SerializeField] private Image _neutralBar;
        [SerializeField] private Image _dissatisfiedBar;
        
        void Update()
        {
            if (_simulationRunner == null || _simulationRunner.Sim == null)
                return;
            
            UpdateVisitorCounts();
            UpdateSkillDistribution();
            UpdateSatisfactionDistribution();
        }
        
        private void UpdateVisitorCounts()
        {
            var state = _simulationRunner.Sim.State;
            
            if (_totalVisitorsText != null)
            {
                _totalVisitorsText.text = state.VisitorsToday.ToString();
            }
            
            // Count active skiers if we have reference to visualizer
            if (_onMountainText != null && _skierVisualizer != null)
            {
                int activeCount = _skierVisualizer.ActiveSkierCount;
                _onMountainText.text = activeCount.ToString();
            }
            
            if (_avgSatisfactionText != null)
            {
                float satisfaction = _simulationRunner.Sim.Satisfaction.Satisfaction;
                _avgSatisfactionText.text = $"{satisfaction:P0}";
                
                // Color code
                Color color = UIManager.Instance?.Theme?.GetSatisfactionColor(satisfaction) ?? Color.white;
                _avgSatisfactionText.color = color;
            }
            
            if (_runsCompletedText != null)
            {
                // TODO: Track aggregate run stats in simulation
                // For now, show placeholder text
                _runsCompletedText.text = "—";
            }
        }
        
        private void UpdateSkillDistribution()
        {
            // Default distribution percentages
            // TODO: Get from actual simulation distribution when available
            float beginner = 0.20f;
            float intermediate = 0.30f;
            float advanced = 0.30f;
            float expert = 0.20f;
            
            float total = beginner + intermediate + advanced + expert;
            if (total <= 0) total = 1f;
            
            // Update bars
            if (_beginnerBar != null)
            {
                _beginnerBar.fillAmount = beginner / total;
            }
            if (_intermediateBar != null)
            {
                _intermediateBar.fillAmount = intermediate / total;
            }
            if (_advancedBar != null)
            {
                _advancedBar.fillAmount = advanced / total;
            }
            if (_expertBar != null)
            {
                _expertBar.fillAmount = expert / total;
            }
            
            // Update percent labels
            if (_beginnerPercentText != null)
            {
                _beginnerPercentText.text = $"{(beginner / total * 100):F0}%";
            }
            if (_intermediatePercentText != null)
            {
                _intermediatePercentText.text = $"{(intermediate / total * 100):F0}%";
            }
            if (_advancedPercentText != null)
            {
                _advancedPercentText.text = $"{(advanced / total * 100):F0}%";
            }
            if (_expertPercentText != null)
            {
                _expertPercentText.text = $"{(expert / total * 100):F0}%";
            }
        }
        
        private void UpdateSatisfactionDistribution()
        {
            // Would need aggregate satisfaction data from all skiers
            // For now, use overall satisfaction as a proxy
            float satisfaction = _simulationRunner?.Sim?.Satisfaction?.Satisfaction ?? 1f;
            
            float satisfied = Mathf.Clamp01(satisfaction);
            float neutral = Mathf.Clamp01(1f - Mathf.Abs(satisfaction - 1f));
            float dissatisfied = Mathf.Clamp01(1f - satisfaction);
            
            if (_satisfiedBar != null)
            {
                _satisfiedBar.fillAmount = satisfied;
            }
            if (_neutralBar != null)
            {
                _neutralBar.fillAmount = neutral;
            }
            if (_dissatisfiedBar != null)
            {
                _dissatisfiedBar.fillAmount = dissatisfied;
            }
        }
    }
}
