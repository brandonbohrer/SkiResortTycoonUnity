namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Pure C# economy system.
    /// Handles end-of-day revenue calculation.
    /// Money is ONLY granted once per day at the end of the day.
    /// </summary>
    public class EconomySystem
    {
        private float _dollarsPerVisitor = 25f;
        
        public float DollarsPerVisitor
        {
            get => _dollarsPerVisitor;
            set => _dollarsPerVisitor = value;
        }
        
        /// <summary>
        /// Computes end-of-day revenue based on visitors.
        /// </summary>
        public int ComputeEndOfDayRevenue(SimulationState state)
        {
            return (int)(state.VisitorsToday * _dollarsPerVisitor);
        }
        
        /// <summary>
        /// Applies the computed revenue to the state's money.
        /// </summary>
        public void ApplyRevenue(SimulationState state, int revenue)
        {
            state.Money += revenue;
        }
    }
}

