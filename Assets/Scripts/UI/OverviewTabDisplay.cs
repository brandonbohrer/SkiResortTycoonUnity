using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SkiResortTycoon.Core;
using SkiResortTycoon.UnityBridge;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Drives the Overview tab in the Manager screen.
    /// Displays total cash (full, commas), today's net profit,
    /// total visitors today, and satisfaction percentage.
    /// Also wires the manager close button.
    /// </summary>
    public class OverviewTabDisplay : MonoBehaviour
    {
        [Header("Stat Text Fields")]
        [SerializeField] private TextMeshProUGUI _cashText;
        [SerializeField] private TextMeshProUGUI _netProfitText;
        [SerializeField] private TextMeshProUGUI _visitorsText;
        [SerializeField] private TextMeshProUGUI _satisfactionText;

        [Header("References (auto-found if null)")]
        [SerializeField] private SimulationRunner _simulationRunner;

        void Start()
        {
            if (_simulationRunner == null)
                _simulationRunner = FindObjectOfType<SimulationRunner>();
        }

        void Update()
        {
            if (_simulationRunner == null || _simulationRunner.Sim == null) return;

            var state        = _simulationRunner.Sim.State;
            var economy      = _simulationRunner.Sim.EconomySystem;
            var satisfaction = _simulationRunner.Sim.Satisfaction;

            if (_cashText != null)
                _cashText.text = FormatCash(state.Money);

            if (_netProfitText != null)
            {
                // Live estimate: visitors so far × ticket price, minus current infrastructure expenses
                float liveRevenue  = state.VisitorsToday * economy.TicketPricing.TicketPrice;
                float liveExpenses = state.TodayExpenses; // written by last end-of-day; 0 on day 1
                float profit       = liveRevenue - liveExpenses;
                string prefix      = profit >= 0 ? "+" : "";
                _netProfitText.text = prefix + FormatCash(Mathf.RoundToInt(profit));
            }

            if (_visitorsText != null)
                _visitorsText.text = state.VisitorsToday.ToString("N0");

            if (_satisfactionText != null)
                _satisfactionText.text = $"{Mathf.RoundToInt(satisfaction.Satisfaction)}%";
        }

        /// <summary>Full number with commas — e.g. $1,234,567</summary>
        private static string FormatCash(int amount)
        {
            if (amount < 0)
                return "-$" + (-amount).ToString("N0");
            return "$" + amount.ToString("N0");
        }
    }
}
