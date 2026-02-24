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
    /// IMPLEMENTED (live data):
    ///   - Skiers Today
    ///   - Satisfaction (overall %)
    ///   - Skill Distribution bars (Beginner / Intermediate / Advanced / Expert)
    ///
    /// WIRED BUT NOT YET IMPLEMENTED (shows "--"):
    ///   - Average Wait for Lifts
    ///   - Wait Times breakdown (Low / Medium / High)
    ///   - Lift Traversal rating
    ///   - Trail Access rating
    ///   - Food Satisfaction
    ///   - Price Fairness
    ///   - Trail Variety
    /// </summary>
    public class GuestsTabDisplay : MonoBehaviour
    {
        // ── Implemented ──────────────────────────────────────────────────
        [Header("Implemented")]
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

        // ── Not yet implemented (will show "--") ─────────────────────────
        [Header("Not Yet Implemented")]
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

            // Pre-fill unimplemented fields once
            Set(_avgLiftWaitText,      "--");
            Set(_waitTimeLowText,      "--");
            Set(_waitTimeMedText,      "--");
            Set(_waitTimeHighText,     "--");
            Set(_liftTraversalText,    "--");
            Set(_trailAccessText,      "--");
            Set(_foodSatisfactionText, "--");
            Set(_priceFairnessText,    "--");
            Set(_trailVarietyText,     "--");
        }

        void Update()
        {
            if (_simulationRunner == null || _simulationRunner.Sim == null) return;

            var state        = _simulationRunner.Sim.State;
            var satisfaction = _simulationRunner.Sim.Satisfaction;

            Set(_skiersText,       state.VisitorsToday.ToString("N0"));
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
            }
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

