using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SkiResortTycoon.Core;
using SkiResortTycoon.UnityBridge;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Drives the Guests tab in the Manager screen.
    /// 
    /// All satisfaction metrics are live — computed every 2 seconds from
    /// active skier data and displayed with qualitative ratings.
    /// </summary>
    public class GuestsTabDisplay : MonoBehaviour
    {
        // ── Core stats ──────────────────────────────────────────────────
        [Header("Core Stats")]
        [SerializeField] private TextMeshProUGUI _skiersText;
        [SerializeField] private TextMeshProUGUI _satisfactionText;

        // ── Skill Distribution bars ───────────────────────────────────────
        [Header("Skill Distribution - Bar Fills")]
        [Tooltip("Image component with Type=Filled, Method=Horizontal, Origin=Left")]
        [SerializeField] private Image           _beginnerFill;
        [SerializeField] private TextMeshProUGUI _beginnerPct;

        [SerializeField] private Image           _intermediateFill;
        [SerializeField] private TextMeshProUGUI _intermediatePct;

        [SerializeField] private Image           _advancedFill;
        [SerializeField] private TextMeshProUGUI _advancedPct;

        [SerializeField] private Image           _expertFill;
        [SerializeField] private TextMeshProUGUI _expertPct;

        // ── Satisfaction Indicators ──────────────────────────────────────
        [Header("Satisfaction Indicators")]
        [SerializeField] private TextMeshProUGUI _avgLiftWaitText;
        [SerializeField] private TextMeshProUGUI _waitTimeLowText;
        [SerializeField] private TextMeshProUGUI _waitTimeMedText;
        [SerializeField] private TextMeshProUGUI _waitTimeHighText;
        [SerializeField] private TextMeshProUGUI _liftTraversalText;
        [SerializeField] private TextMeshProUGUI _trailAccessText;
        [SerializeField] private TextMeshProUGUI _foodSatisfactionText;
        [SerializeField] private TextMeshProUGUI _priceFairnessText;
        [SerializeField] private TextMeshProUGUI _trailVarietyText;

        // ── References ───────────────────────────────────────────────────
        [Header("References (auto-found if null)")]
        [SerializeField] private SimulationRunner _simulationRunner;
        [SerializeField] private SkierVisualizer  _skierVisualizer;

        void Start()
        {
            if (_simulationRunner == null)
                _simulationRunner = FindObjectOfType<SimulationRunner>();
            if (_skierVisualizer == null)
                _skierVisualizer = FindObjectOfType<SkierVisualizer>();
        }

        void Update()
        {
            if (_simulationRunner == null || _simulationRunner.Sim == null) return;

            var state        = _simulationRunner.Sim.State;
            var satisfaction = _simulationRunner.Sim.Satisfaction;

            Set(_skiersText,       state.ActiveSkierCount.ToString("N0"));
            Set(_satisfactionText, $"{Mathf.RoundToInt(satisfaction.Satisfaction)}%");

            // ── Skill distribution bars ───────────────────────────────────
            if (_skierVisualizer != null)
            {
                var counts = _skierVisualizer.GetSkillCounts();
                int total  = _skierVisualizer.ActiveSkierCount;

                SetBar(_beginnerFill,     _beginnerPct,      counts[SkillLevel.Beginner],     total);
                SetBar(_intermediateFill, _intermediatePct,  counts[SkillLevel.Intermediate], total);
                SetBar(_advancedFill,     _advancedPct,      counts[SkillLevel.Advanced],     total);
                SetBar(_expertFill,       _expertPct,        counts[SkillLevel.Expert],       total);
                
                UpdateSatisfactionIndicators();
            }
        }
        
        private void UpdateSatisfactionIndicators()
        {
            var stats = _skierVisualizer.GuestStats;
            if (stats == null || stats.SkierCount == 0)
            {
                Set(_avgLiftWaitText,      "--");
                Set(_waitTimeLowText,      "--");
                Set(_waitTimeMedText,      "--");
                Set(_waitTimeHighText,     "--");
                Set(_liftTraversalText,    "--");
                Set(_trailAccessText,      "--");
                Set(_foodSatisfactionText, "--");
                Set(_priceFairnessText,    "--");
                Set(_trailVarietyText,     "--");
                return;
            }
            
            // Average wait time: show as readable time string
            float avgWait = stats.AvgWaitTimeSeconds;
            if (avgWait < 1f)
                Set(_avgLiftWaitText, "None");
            else if (avgWait < 60f)
                Set(_avgLiftWaitText, $"{avgWait:F0}s");
            else
                Set(_avgLiftWaitText, $"{avgWait / 60f:F1} min");
            
            // Wait times: Low/Medium/High show how guests perceive wait times
            // These are categorical — only one lights up based on the aggregate score
            float waitSat = stats.AvgWaitTimeSatisfaction;
            Set(_waitTimeLowText,  waitSat >= 0.65f ? "Low"    : "--");
            Set(_waitTimeMedText,  waitSat >= 0.35f && waitSat < 0.65f ? "Medium" : "--");
            Set(_waitTimeHighText, waitSat < 0.35f ? "High"   : "--");
            
            // Satisfaction indicators with qualitative ratings
            Set(_liftTraversalText,    GuestSatisfactionStats.GetRating(stats.AvgLiftTraversal));
            Set(_trailAccessText,      GuestSatisfactionStats.GetRating(stats.AvgTrailAccess));
            Set(_foodSatisfactionText, GuestSatisfactionStats.GetRating(stats.AvgFoodSatisfaction));
            Set(_priceFairnessText,    GuestSatisfactionStats.GetRating(stats.AvgPriceFairness));
            Set(_trailVarietyText,     GuestSatisfactionStats.GetRating(stats.AvgTrailVariety));
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private static void SetBar(Image fill, TextMeshProUGUI label, int count, int total)
        {
            float pct = total > 0 ? (float)count / total : 0f;
            if (fill  != null) fill.fillAmount = pct;
            if (label != null) label.text = Mathf.RoundToInt(pct * 100f) + "%";
        }

        private static void Set(TextMeshProUGUI label, string value)
        {
            if (label != null) label.text = value;
        }
    }
}

