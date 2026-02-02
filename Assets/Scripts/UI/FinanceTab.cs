using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SkiResortTycoon.Core;
using SkiResortTycoon.UnityBridge;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Resort panel tab showing financial information.
    /// Displays revenue, expenses, and income breakdown.
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
        [SerializeField] private TextMeshProUGUI _foodRevenueText;
        [SerializeField] private Image _foodRevenueBar;
        [SerializeField] private TextMeshProUGUI _rentalRevenueText;
        [SerializeField] private Image _rentalRevenueBar;
        
        [Header("Expense Breakdown")]
        [SerializeField] private TextMeshProUGUI _staffExpenseText;
        [SerializeField] private TextMeshProUGUI _maintenanceExpenseText;
        [SerializeField] private TextMeshProUGUI _utilityExpenseText;
        
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
            
            if (_totalMoneyText != null)
            {
                _totalMoneyText.text = $"${state.Money:N0}";
            }
            
            // These would come from a proper financial tracking system
            // For now, estimate based on visitor count
            float estimatedRevenue = state.VisitorsToday * 75f; // $75 per visitor
            float estimatedExpenses = state.VisitorsToday * 20f + 500f; // Variable + fixed
            float netIncome = estimatedRevenue - estimatedExpenses;
            
            if (_todayRevenueText != null)
            {
                _todayRevenueText.text = $"${estimatedRevenue:N0}";
                _todayRevenueText.color = _positiveColor;
            }
            
            if (_todayExpensesText != null)
            {
                _todayExpensesText.text = $"${estimatedExpenses:N0}";
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
            var state = _simulationRunner.Sim.State;
            
            // Estimate breakdowns
            float totalRevenue = state.VisitorsToday * 75f;
            float ticketRevenue = totalRevenue * 0.6f;   // 60% from tickets
            float foodRevenue = totalRevenue * 0.25f;    // 25% from food
            float rentalRevenue = totalRevenue * 0.15f;  // 15% from rentals
            
            if (_ticketRevenueText != null)
            {
                _ticketRevenueText.text = $"${ticketRevenue:N0}";
            }
            if (_ticketRevenueBar != null && totalRevenue > 0)
            {
                _ticketRevenueBar.fillAmount = ticketRevenue / totalRevenue;
            }
            
            if (_foodRevenueText != null)
            {
                _foodRevenueText.text = $"${foodRevenue:N0}";
            }
            if (_foodRevenueBar != null && totalRevenue > 0)
            {
                _foodRevenueBar.fillAmount = foodRevenue / totalRevenue;
            }
            
            if (_rentalRevenueText != null)
            {
                _rentalRevenueText.text = $"${rentalRevenue:N0}";
            }
            if (_rentalRevenueBar != null && totalRevenue > 0)
            {
                _rentalRevenueBar.fillAmount = rentalRevenue / totalRevenue;
            }
        }
        
        private void UpdateExpenseBreakdown()
        {
            var state = _simulationRunner.Sim.State;
            
            // Estimate breakdowns
            float totalExpenses = state.VisitorsToday * 20f + 500f;
            float staffExpense = totalExpenses * 0.5f;
            float maintenanceExpense = totalExpenses * 0.3f;
            float utilityExpense = totalExpenses * 0.2f;
            
            if (_staffExpenseText != null)
            {
                _staffExpenseText.text = $"${staffExpense:N0}";
            }
            
            if (_maintenanceExpenseText != null)
            {
                _maintenanceExpenseText.text = $"${maintenanceExpense:N0}";
            }
            
            if (_utilityExpenseText != null)
            {
                _utilityExpenseText.text = $"${utilityExpense:N0}";
            }
        }
    }
}
