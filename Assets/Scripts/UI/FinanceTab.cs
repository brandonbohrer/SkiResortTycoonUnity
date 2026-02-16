using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SkiResortTycoon.Core;
using SkiResortTycoon.UnityBridge;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Resort panel tab showing financial information.
    /// Reads real data from EconomySystem instead of placeholder estimates.
    /// </summary>
    public class FinanceTab : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SimulationRunner _simulationRunner;
        
        [Header("Summary")]
        [SerializeField] private TextMeshProUGUI _totalMoneyText;
        [SerializeField] private TextMeshProUGUI _todayRevenueText;
        [SerializeField] private TextMeshProUGUI _todayExpensesText;
        [SerializeField] private TextMeshProUGUI _netIncomeText;
        
        [Header("Revenue Breakdown")]
        [SerializeField] private TextMeshProUGUI _ticketRevenueText;
        [SerializeField] private Image _ticketRevenueBar;
        [SerializeField] private TextMeshProUGUI _foodRevenueText;  // Repurposed for lodge revenue
        [SerializeField] private Image _foodRevenueBar;
        [SerializeField] private TextMeshProUGUI _rentalRevenueText; // Unused for now
        [SerializeField] private Image _rentalRevenueBar;
        
        [Header("Expense Breakdown")]
        [SerializeField] private TextMeshProUGUI _staffExpenseText;       // Repurposed for lift expenses
        [SerializeField] private TextMeshProUGUI _maintenanceExpenseText; // Repurposed for lodge expenses
        [SerializeField] private TextMeshProUGUI _utilityExpenseText;     // Repurposed for trail expenses
        
        [Header("Visual Settings")]
        [SerializeField] private Color _positiveColor = new Color(0.4f, 1f, 0.4f);
        [SerializeField] private Color _negativeColor = new Color(1f, 0.4f, 0.4f);
        [SerializeField] private Color _neutralColor = Color.white;
        
        void Update()
        {
            if (_simulationRunner == null || _simulationRunner.Sim == null)
                return;
            
            UpdateSummary();
            UpdateRevenueBreakdown();
            UpdateExpenseBreakdown();
        }
        
        private void UpdateSummary()
        {
            var state = _simulationRunner.Sim.State;
            var economy = _simulationRunner.Sim.EconomySystem;
            var record = economy.TodayRecord;
            
            if (_totalMoneyText != null)
            {
                _totalMoneyText.text = $"${state.Money:N0}";
            }
            
            // Use real data from EconomySystem if available, otherwise show live estimate
            float ticketRevenue = 0f;
            float lodgeRevenue = 0f;
            float totalExpenses = 0f;
            
            if (record != null)
            {
                // We have a completed day's record
                ticketRevenue = record.TicketRevenue;
                lodgeRevenue = record.LodgeRevenue;
                totalExpenses = record.TotalExpenses;
            }
            else
            {
                // Day in progress — show live estimate
                ticketRevenue = state.VisitorsToday * economy.TicketPricing.TicketPrice;
                // Lodge revenue is tracked live on LodgeFacility objects — we can't access it here
                // so show ticket revenue only during the day
            }
            
            float totalRevenue = ticketRevenue + lodgeRevenue;
            float netIncome = totalRevenue - totalExpenses;
            
            if (_todayRevenueText != null)
            {
                _todayRevenueText.text = $"${totalRevenue:N0}";
                _todayRevenueText.color = _positiveColor;
            }
            
            if (_todayExpensesText != null)
            {
                _todayExpensesText.text = $"${totalExpenses:N0}";
                _todayExpensesText.color = _negativeColor;
            }
            
            if (_netIncomeText != null)
            {
                _netIncomeText.text = $"{(netIncome >= 0 ? "+" : "")}${netIncome:N0}";
                _netIncomeText.color = netIncome >= 0 ? _positiveColor : _negativeColor;
            }
        }
        
        private void UpdateRevenueBreakdown()
        {
            var economy = _simulationRunner.Sim.EconomySystem;
            var record = economy.TodayRecord;
            
            float ticketRevenue = 0f;
            float lodgeRevenue = 0f;
            float totalRevenue = 0f;
            
            if (record != null)
            {
                ticketRevenue = record.TicketRevenue;
                lodgeRevenue = record.LodgeRevenue;
                totalRevenue = record.TotalRevenue;
            }
            else
            {
                var state = _simulationRunner.Sim.State;
                ticketRevenue = state.VisitorsToday * economy.TicketPricing.TicketPrice;
                totalRevenue = ticketRevenue;
            }
            
            if (_ticketRevenueText != null)
            {
                _ticketRevenueText.text = $"${ticketRevenue:N0}";
            }
            if (_ticketRevenueBar != null && totalRevenue > 0)
            {
                _ticketRevenueBar.fillAmount = ticketRevenue / totalRevenue;
            }
            
            // Lodge revenue (mapped to the "food revenue" UI field)
            if (_foodRevenueText != null)
            {
                _foodRevenueText.text = $"${lodgeRevenue:N0}";
            }
            if (_foodRevenueBar != null && totalRevenue > 0)
            {
                _foodRevenueBar.fillAmount = lodgeRevenue / totalRevenue;
            }
            
            // Rental revenue slot unused for now
            if (_rentalRevenueText != null)
            {
                _rentalRevenueText.text = "$0";
            }
            if (_rentalRevenueBar != null)
            {
                _rentalRevenueBar.fillAmount = 0f;
            }
        }
        
        private void UpdateExpenseBreakdown()
        {
            var economy = _simulationRunner.Sim.EconomySystem;
            var record = economy.TodayRecord;
            
            float liftExpenses = 0f;
            float lodgeExpenses = 0f;
            float trailExpenses = 0f;
            
            if (record != null)
            {
                liftExpenses = record.LiftExpenses;
                lodgeExpenses = record.LodgeExpenses;
                trailExpenses = record.TrailExpenses;
            }
            
            // Lift expenses (mapped to "staff" UI field)
            if (_staffExpenseText != null)
            {
                _staffExpenseText.text = $"${liftExpenses:N0}";
            }
            
            // Lodge expenses (mapped to "maintenance" UI field)
            if (_maintenanceExpenseText != null)
            {
                _maintenanceExpenseText.text = $"${lodgeExpenses:N0}";
            }
            
            // Trail expenses (mapped to "utility" UI field)
            if (_utilityExpenseText != null)
            {
                _utilityExpenseText.text = $"${trailExpenses:N0}";
            }
        }
    }
}
