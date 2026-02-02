using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using SkiResortTycoon.Core;
using SkiResortTycoon.UnityBridge;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Resort panel tab showing performance metrics.
    /// Displays lift utilization, trail popularity, and bottlenecks.
    /// </summary>
    public class PerformanceTab : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SimulationRunner _simulationRunner;
        [SerializeField] private LiftBuilder _liftBuilder;
        [SerializeField] private TrailDrawer _trailDrawer;
        
        [Header("Summary")]
        [SerializeField] private TextMeshProUGUI _avgLiftUtilText;
        [SerializeField] private TextMeshProUGUI _avgWaitTimeText;
        [SerializeField] private TextMeshProUGUI _bottleneckText;
        
        [Header("Lift Section")]
        [SerializeField] private Transform _liftListContainer;
        [SerializeField] private GameObject _liftEntryPrefab;
        
        [Header("Trail Section")]
        [SerializeField] private Transform _trailListContainer;
        [SerializeField] private GameObject _trailEntryPrefab;
        
        [Header("Update Settings")]
        [SerializeField] private float _updateInterval = 1f;
        
        private float _lastUpdateTime;
        private List<GameObject> _liftEntries = new List<GameObject>();
        private List<GameObject> _trailEntries = new List<GameObject>();
        
        void OnEnable()
        {
            // Rebuild lists when tab becomes visible
            RebuildLiftList();
            RebuildTrailList();
        }
        
        void Update()
        {
            if (Time.time - _lastUpdateTime < _updateInterval)
                return;
            
            _lastUpdateTime = Time.time;
            UpdateSummary();
            UpdateLiftStats();
            UpdateTrailStats();
        }
        
        private void UpdateSummary()
        {
            // TODO: Track actual lift utilization in real-time
            // For now, use placeholder values
            float avgUtilization = 0f;
            float avgWaitTime = 0f;
            string bottleneck = "None";
            
            if (_liftBuilder != null && _liftBuilder.LiftSystem != null)
            {
                var lifts = _liftBuilder.LiftSystem.GetAllLifts();
                if (lifts.Count > 0)
                {
                    // Estimate based on visitor count
                    int visitors = _simulationRunner?.Sim?.State.VisitorsToday ?? 0;
                    avgUtilization = Mathf.Clamp01(visitors / Mathf.Max(1f, lifts.Count * 200f));
                }
            }
            
            if (_avgLiftUtilText != null)
            {
                _avgLiftUtilText.text = $"{avgUtilization:P0}";
                
                // Color code
                if (avgUtilization > 0.9f)
                    _avgLiftUtilText.color = new Color(1f, 0.2f, 0.2f); // Red - overloaded
                else if (avgUtilization > 0.7f)
                    _avgLiftUtilText.color = new Color(1f, 0.6f, 0f); // Orange
                else
                    _avgLiftUtilText.color = new Color(0.4f, 1f, 0.4f); // Green
            }
            
            if (_avgWaitTimeText != null)
            {
                _avgWaitTimeText.text = $"{avgWaitTime:F1} min";
            }
            
            if (_bottleneckText != null)
            {
                _bottleneckText.text = bottleneck;
                _bottleneckText.color = bottleneck == "None" ? Color.white : new Color(1f, 0.6f, 0f);
            }
        }
        
        private void RebuildLiftList()
        {
            // Clear existing entries
            foreach (var entry in _liftEntries)
            {
                Destroy(entry);
            }
            _liftEntries.Clear();
            
            if (_liftListContainer == null || _liftEntryPrefab == null) return;
            if (_liftBuilder == null || _liftBuilder.LiftSystem == null) return;
            
            var lifts = _liftBuilder.LiftSystem.GetAllLifts();
            
            foreach (var lift in lifts)
            {
                var entry = Instantiate(_liftEntryPrefab, _liftListContainer);
                
                // Set name
                var nameText = entry.transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
                if (nameText != null)
                {
                    nameText.text = lift.Name ?? $"Lift {lift.LiftId}";
                }
                
                // Store reference
                entry.name = $"Lift_{lift.LiftId}";
                _liftEntries.Add(entry);
            }
        }
        
        private void UpdateLiftStats()
        {
            if (_liftBuilder == null || _liftBuilder.LiftSystem == null) return;
            
            var lifts = _liftBuilder.LiftSystem.GetAllLifts();
            
            // TODO: Track actual lift utilization per lift
            // Estimate based on total visitors
            int visitors = _simulationRunner?.Sim?.State.VisitorsToday ?? 0;
            float baseUtilization = Mathf.Clamp01(visitors / Mathf.Max(1f, lifts.Count * 200f));
            
            for (int i = 0; i < _liftEntries.Count && i < lifts.Count; i++)
            {
                var entry = _liftEntries[i];
                var lift = lifts[i];
                
                // Use estimated utilization with some variance
                float utilization = baseUtilization + Random.Range(-0.1f, 0.1f);
                utilization = Mathf.Clamp01(utilization);
                
                // Update utilization bar
                var bar = entry.transform.Find("UtilizationBar")?.GetComponent<Image>();
                if (bar != null)
                {
                    bar.fillAmount = utilization;
                    
                    // Color code
                    if (utilization > 0.9f)
                        bar.color = new Color(1f, 0.2f, 0.2f);
                    else if (utilization > 0.7f)
                        bar.color = new Color(1f, 0.6f, 0f);
                    else
                        bar.color = new Color(0.4f, 1f, 0.4f);
                }
                
                // Update utilization text
                var text = entry.transform.Find("Utilization")?.GetComponent<TextMeshProUGUI>();
                if (text != null)
                {
                    text.text = $"{utilization:P0}";
                }
            }
        }
        
        private void RebuildTrailList()
        {
            // Clear existing entries
            foreach (var entry in _trailEntries)
            {
                Destroy(entry);
            }
            _trailEntries.Clear();
            
            if (_trailListContainer == null || _trailEntryPrefab == null) return;
            if (_trailDrawer == null || _trailDrawer.TrailSystem == null) return;
            
            var trails = _trailDrawer.TrailSystem.GetAllTrails();
            
            // Only show first few trails to avoid cluttering UI
            int maxTrails = 5;
            int count = 0;
            
            foreach (var trail in trails)
            {
                if (count >= maxTrails) break;
                
                var entry = Instantiate(_trailEntryPrefab, _trailListContainer);
                
                // Set name with difficulty color
                var nameText = entry.transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
                if (nameText != null)
                {
                    nameText.text = trail.Name ?? $"Trail {trail.TrailId}";
                    nameText.color = UIManager.Instance?.Theme?.GetDifficultyColor(trail.Difficulty) ?? Color.white;
                }
                
                entry.name = $"Trail_{trail.TrailId}";
                _trailEntries.Add(entry);
                count++;
            }
        }
        
        private void UpdateTrailStats()
        {
            if (_trailDrawer == null || _trailDrawer.TrailSystem == null) return;
            
            var trails = _trailDrawer.TrailSystem.GetAllTrails();
            
            for (int i = 0; i < _trailEntries.Count && i < trails.Count; i++)
            {
                var entry = _trailEntries[i];
                var trail = trails[i];
                
                // Update runs count
                var runsText = entry.transform.Find("Runs")?.GetComponent<TextMeshProUGUI>();
                if (runsText != null)
                {
                    // This would need proper tracking per trail
                    runsText.text = "0 runs";
                }
                
                // Update difficulty icon/color
                var diffIcon = entry.transform.Find("DifficultyIcon")?.GetComponent<Image>();
                if (diffIcon != null)
                {
                    diffIcon.color = UIManager.Instance?.Theme?.GetDifficultyColor(trail.Difficulty) ?? Color.white;
                }
            }
        }
    }
}
