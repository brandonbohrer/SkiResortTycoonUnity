namespace SkiResortTycoon.Core.SatisfactionFactors
{
    /// <summary>
    /// Satisfaction factor based on how many times a skier has fallen.
    /// Falls on trails where the player overrode the difficulty label downward
    /// (making a hard trail appear easier) incur a harsher penalty.
    /// </summary>
    public class FallingFactor : ISatisfactionFactor
    {
        public string Name => "Falling";
        public float Weight => 1.0f;

        private const float PenaltyPerFall = 0.15f;
        private const float ExtraPenaltyPerMislabeledFall = 0.15f;

        public float Evaluate(SkierNeeds needs)
        {
            float score = 1.0f;

            score -= needs.FallCount * PenaltyPerFall;
            score -= needs.FallsOnMislabeledTrails * ExtraPenaltyPerMislabeledFall;

            return System.Math.Max(0f, System.Math.Min(1f, score));
        }
    }
}
