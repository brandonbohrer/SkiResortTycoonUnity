namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Player response to the one-time Powder Day morning event.
    /// </summary>
    public enum PowderDayChoice
    {
        None = 0,
        /// <summary>Yes — temporarily lower ticket prices and add capacity (staffing/ops) to handle the rush.</summary>
        Accepted = 1,
        /// <summary>No — run a normal day; no special demand or presentation.</summary>
        Declined = 2
    }
}
