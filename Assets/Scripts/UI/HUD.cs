using UnityEngine;
using TMPro;
using SkiResortTycoon.Core;
using SkiResortTycoon.UnityBridge;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Displays simulation data in the UI.
    /// Updates every frame to show current day, time, visitors, and money.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SimulationRunner _simulationRunner;
        [SerializeField] private TextMeshProUGUI _hudText;
        
        void Update()
        {
            // Null checks
            if (_simulationRunner == null || _simulationRunner.Sim == null || _hudText == null)
            {
                return;
            }
            
            SimulationState state = _simulationRunner.Sim.State;
            float satisfaction = _simulationRunner.Sim.Satisfaction.Satisfaction;
            
            // Format time as HH:MM AM/PM
            string timeFormatted = FormatTime(state.TimeMinutes);
            
            // Format satisfaction with color
            string satisfactionText = GetSatisfactionText(satisfaction);
            
            // Build the HUD display with nice formatting
            _hudText.text = $"Day {state.DayIndex}\n" +
                           $"Time: {timeFormatted}\n" +
                           $"Visitors Today: {state.VisitorsToday}\n" +
                           $"Money: ${state.Money:N0}\n" +
                           $"Satisfaction: {satisfactionText}";
        }
        
        private string GetSatisfactionText(float satisfaction)
        {
            string color = "white";
            
            if (satisfaction >= 1.1f)
                color = "#00ff00"; // Green - very satisfied
            else if (satisfaction >= 0.9f)
                color = "white"; // Normal
            else if (satisfaction >= 0.7f)
                color = "#ffaa00"; // Orange - warning
            else
                color = "#ff0000"; // Red - unhappy
            
            return $"<color={color}>{satisfaction:F2}</color>";
        }
        
        /// <summary>
        /// Converts minutes since midnight to formatted time string (e.g., "9:05 AM").
        /// </summary>
        private string FormatTime(float totalMinutes)
        {
            int hours24 = (int)(totalMinutes / 60f);
            int minutes = (int)(totalMinutes % 60f);
            
            // Convert to 12-hour format
            int hours12 = hours24 % 12;
            if (hours12 == 0) hours12 = 12; // Handle midnight and noon
            
            string amPm = hours24 >= 12 ? "PM" : "AM";
            
            return $"{hours12}:{minutes:D2} {amPm}";
        }
    }
}

