namespace SkiResortTycoon.Core.SatisfactionFactors
{
    /// <summary>
    /// Satisfaction factor based on whether a skier gets to ski trails
    /// matching their skill level. This is one of the biggest real-world
    /// satisfaction drivers — beginners hate being stuck on blacks,
    /// experts are bored on greens.
    /// 
    /// Evaluates:
    /// 1. What % of runs were on preferred difficulty vs forced/transit trails
    /// 2. Whether the skier completed enough runs overall
    /// 3. Penalty if skier couldn't find ANY trails (no path, wrong difficulty only)
    /// 
    /// Reads from SkierNeeds: RunsCompleted, DesiredRuns, PreferredRunsCompleted,
    /// SkillLevel, UnfulfilledNeedAttempts.
    /// </summary>
    public class SkillMatchFactor : ISatisfactionFactor
    {
        public string Name => "SkillMatch";
        public float Weight => 1.2f;

        private readonly SkillLevel _skill;

        public SkillMatchFactor(SkillLevel skill)
        {
            _skill = skill;
        }

        public float Evaluate(SkierNeeds needs)
        {
            if (needs.RunsCompleted == 0)
            {
                if (needs.DesiredRuns > 0)
                    return 0.3f;
                return 0.7f;
            }

            float score = 1.0f;

            // Preferred run ratio: what fraction of completed runs were on good trails?
            float preferredRatio = (float)needs.PreferredRunsCompleted / needs.RunsCompleted;

            // Beginners and intermediates care MORE about skill match than experts
            // (experts can enjoy a blue for fun; beginners on a black is dangerous)
            float mismatchSeverity = _skill <= SkillLevel.Intermediate ? 0.45f : 0.3f;

            score -= (1.0f - preferredRatio) * mismatchSeverity;

            // Completion satisfaction: did they get enough runs?
            float completionRatio = (float)needs.RunsCompleted / System.Math.Max(1, needs.DesiredRuns);
            completionRatio = System.Math.Min(1f, completionRatio);

            // Not completing desired runs is frustrating, especially if they're leaving early
            if (completionRatio < 0.5f)
                score -= 0.2f;
            else if (completionRatio < 0.8f)
                score -= 0.1f;

            return System.Math.Max(0f, System.Math.Min(1f, score));
        }
    }
}
