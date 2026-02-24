using System.Collections.Generic;
using UnityEngine;
using TMPro;
using SkiResortTycoon.Core;
using SkiResortTycoon.UnityBridge;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Drives the Finances tab in the Manager screen.
    ///
    /// Displays two groups:
    ///   REVENUE:  Revenue Today, Total Cash, Net Today
    ///             Ticket Revenue, Lodge (Food) Revenue
    ///   EXPENSES: Total Expenses, Lift Upkeep, Trail Upkeep, Lodge Upkeep
    ///   METRICS:  Profit Margin, Average Profit per Guest
    ///
    /// Also feeds the LineChartRenderer with per-day history data.
    ///
    /// Live (intra-day) values are estimates; they settle to exact numbers at end-of-day.
    /// </summary>
    public class FinancesTabDisplay : MonoBehaviour
    {
        // ── Revenue group ────────────────────────────────────────────────
        [Header("Revenue")]
        [SerializeField] private TextMeshProUGUI _revenueTodayText;    // Total revenue today
        [SerializeField] private TextMeshProUGUI _totalExpensesTopText; // Total expenses (top section)
        [SerializeField] private TextMeshProUGUI _netTodayText;         // Revenue − Expenses
        [SerializeField] private TextMeshProUGUI _revenueBreakdownText;// Repeat revenue label (second column)
        [SerializeField] private TextMeshProUGUI _ticketRevenueText;   // Ticket portion
        [SerializeField] private TextMeshProUGUI _lodgeRevenueText;    // Lodge / food portion

        // ── Expenses group ───────────────────────────────────────────────
        [Header("Expenses")]
        [SerializeField] private TextMeshProUGUI _totalExpensesText;
        [SerializeField] private TextMeshProUGUI _liftUpkeepText;
        [SerializeField] private TextMeshProUGUI _trailUpkeepText;
        [SerializeField] private TextMeshProUGUI _lodgeUpkeepText;

        // ── Metrics group ────────────────────────────────────────────────
        [Header("Metrics")]
        [SerializeField] private TextMeshProUGUI _profitMarginText;
        [SerializeField] private TextMeshProUGUI _avgProfitPerGuestText;

        // ── Chart ────────────────────────────────────────────────────────
        [Header("Chart")]
        [SerializeField] private LineChartRenderer _chart;

        // ── References ───────────────────────────────────────────────────
        [Header("References (auto-found if null)")]
        [SerializeField] private SimulationRunner _simulationRunner;
        [SerializeField] private LodgeManager     _lodgeManager;

        void Start()
        {
            if (_simulationRunner == null)
                _simulationRunner = FindObjectOfType<SimulationRunner>();
            if (_lodgeManager == null)
                _lodgeManager = FindObjectOfType<LodgeManager>();
        }

        void Update()
        {
            if (_simulationRunner == null || _simulationRunner.Sim == null) return;

            var state   = _simulationRunner.Sim.State;
            var economy = _simulationRunner.Sim.EconomySystem;
            int lodges  = _lodgeManager != null ? _lodgeManager.LodgeCount : 0;

            // ── Live intra-day estimates ──────────────────────────────────
            float ticketRevenue = state.VisitorsToday * economy.TicketPricing.TicketPrice;
            float lodgeRevenue  = state.TodayLodgeRevenue;
            float totalRevenue  = ticketRevenue + lodgeRevenue;

            // Compute expenses live from current infrastructure
            var expenses = economy.ExpenseTracker.Calculate(
                state.LiftsBuilt, lodges, state.TrailsBuilt);

            float net           = totalRevenue - expenses.Total;
            float margin        = totalRevenue > 0f ? (net / totalRevenue) * 100f : 0f;
            float avgPerGuest   = state.VisitorsToday > 0
                ? net / state.VisitorsToday : 0f;

            // ── Revenue group ─────────────────────────────────────────────
            Set(_revenueTodayText,    FormatMoney(totalRevenue));
            Set(_totalExpensesTopText, FormatMoney(expenses.Total));
            Set(_netTodayText,         FormatMoneySign(net));
            Set(_revenueBreakdownText, FormatMoney(totalRevenue));
            Set(_ticketRevenueText,   FormatMoney(ticketRevenue));
            Set(_lodgeRevenueText,    FormatMoney(lodgeRevenue));

            // ── Expenses group ────────────────────────────────────────────
            Set(_totalExpensesText,   FormatMoney(expenses.Total));
            Set(_liftUpkeepText,      FormatMoney(expenses.LiftExpenses));
            Set(_trailUpkeepText,     FormatMoney(expenses.TrailExpenses));
            Set(_lodgeUpkeepText,     FormatMoney(expenses.LodgeExpenses));

            // ── Metrics ───────────────────────────────────────────────────
            Set(_profitMarginText,      $"{margin:F1}%");
            Set(_avgProfitPerGuestText, FormatMoneySign(avgPerGuest));

            // ── Chart (only update occasionally to save cost) ─────────────
            UpdateChart(economy);
        }

        // ── Chart update ─────────────────────────────────────────────────

        private int _lastRenderedDay = -1;

        private void UpdateChart(EconomySystem economy)
        {
            if (_chart == null) return;

            var history = economy.History;
            // Only re-render when a new day of data is available
            if (history.Count == _lastRenderedDay) return;
            _lastRenderedDay = history.Count;

            var revenueData  = new List<float>();
            var expenseData  = new List<float>();

            foreach (var record in history)
            {
                revenueData.Add(record.TotalRevenue);
                expenseData.Add(record.TotalExpenses);
            }

            _chart.RenderChart(revenueData, expenseData);
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private static void Set(TextMeshProUGUI label, string value)
        {
            if (label != null) label.text = value;
        }

        /// <summary>Full number with commas and $ prefix. e.g. $1,234</summary>
        private static string FormatMoney(float amount)
        {
            int rounded = Mathf.RoundToInt(amount);
            if (rounded < 0) return "-$" + (-rounded).ToString("N0");
            return "$" + rounded.ToString("N0");
        }

        /// <summary>With explicit +/- sign. e.g. +$1,234 or -$500</summary>
        private static string FormatMoneySign(float amount)
        {
            int rounded = Mathf.RoundToInt(amount);
            if (rounded < 0) return "-$" + (-rounded).ToString("N0");
            return "+$" + rounded.ToString("N0");
        }
    }
}
