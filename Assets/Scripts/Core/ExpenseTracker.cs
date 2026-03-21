namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Calculates daily operating expenses from infrastructure counts.
    /// Stateless — call Calculate() each day with current counts.
    /// All rates exposed as properties for easy tuning.
    /// </summary>
    public class ExpenseTracker
    {
        // ── Per-day cost rates (tunable) ────────────────────────────────
        // Daily operating costs the PLAYER pays to keep infrastructure running.
        // Lifts need electricity + maintenance, trails need grooming, lodges need staff + supplies.
        public float CostPerLift { get; set; } = 1200f;
        public float CostPerLodge { get; set; } = 1700f;
        public float CostPerTrail { get; set; } = 420f;
        public float BaseOperationsCost { get; set; } = 1800f;
        public float ComplexitySurchargePerUnit { get; set; } = 95f;
        
        /// <summary>
        /// Calculates itemized daily expenses.
        /// </summary>
        public DailyExpenses Calculate(int liftCount, int lodgeCount, int trailCount)
        {
            int totalUnits = liftCount + lodgeCount + trailCount;
            float complexityUnits = System.Math.Max(0, totalUnits - 5);
            float complexitySurcharge = complexityUnits * ComplexitySurchargePerUnit;

            return new DailyExpenses
            {
                LiftExpenses = liftCount * CostPerLift,
                LodgeExpenses = lodgeCount * CostPerLodge,
                TrailExpenses = trailCount * CostPerTrail + BaseOperationsCost + complexitySurcharge
            };
        }
    }
    
    /// <summary>
    /// Itemized expense breakdown for one day.
    /// </summary>
    public struct DailyExpenses
    {
        public float LiftExpenses;
        public float LodgeExpenses;
        public float TrailExpenses;
        public float Total => LiftExpenses + LodgeExpenses + TrailExpenses;
    }
}
