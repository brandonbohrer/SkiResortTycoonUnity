using UnityEngine;
using SkiResortTycoon.Core;

namespace SkiResortTycoon.ScriptableObjects
{
    /// <summary>
    /// ScriptableObject holding ALL tunable weights for the hybrid skier AI system.
    /// Create via Assets > Create > Ski Resort Tycoon > Skier AI Config.
    /// Assign to SkierVisualizer in the Inspector.
    ///
    /// Changes take effect immediately at runtime — the decision engine reads
    /// from this asset every frame.
    /// </summary>
    [CreateAssetMenu(fileName = "SkierAIConfig", menuName = "Ski Resort Tycoon/Skier AI Config")]
    public class SkierAIConfig : ScriptableObject
    {
        // ═══════════════════════════════════════════════════════════════
        //  SOFTMAX TEMPERATURE  (THE most important slider)
        // ═══════════════════════════════════════════════════════════════
        
        [Header("═══ SOFTMAX TEMPERATURE ═══")]
        [Tooltip("Controls randomness vs. determinism in ALL skier decisions.\n" +
                 "Low (0.1) = nearly deterministic, everyone picks the best option.\n" +
                 "Medium (1.0) = moderate variety, still prefers better options.\n" +
                 "High (3.0) = near-uniform, maximum traffic spread.")]
        [Range(0.05f, 5f)] public float softmaxTemperature = 1.5f;
        
        // ═══════════════════════════════════════════════════════════════
        //  SCORING FACTOR WEIGHTS  (how much each factor matters)
        // ═══════════════════════════════════════════════════════════════
        
        [Header("═══ SCORING WEIGHTS ═══")]
        [Tooltip("Skier's innate preference for this trail difficulty.\n" +
                 "Experts like blacks, beginners like greens.\n" +
                 "Lower = skiers care less about difficulty match.")]
        [Range(0f, 3f)] public float difficultyPreferenceStrength = 1.0f;
        
        [Tooltip("Value of terrain reachable BEYOND this trail.\n" +
                 "Higher = experts eagerly take connector greens to reach doubles.\n" +
                 "This drives multi-hop mountain traversal.")]
        [Range(0f, 3f)] public float downstreamValueStrength = 1.0f;
        
        [Tooltip("Global traffic deficit bonus — THE key slider for traffic spread.\n" +
                 "Higher = under-used trails/lifts attract more skiers.\n" +
                 "0 = no balancing. 2+ = strong balancing across all infrastructure.")]
        [Range(0f, 5f)] public float deficitBonusStrength = 2.5f;
        
        [Tooltip("How much skiers follow their planned multi-hop route.\n" +
                 "Higher = goals strongly influence decisions.\n" +
                 "Lower = skiers make more local, reactive choices.")]
        [Range(0f, 3f)] public float goalAlignmentStrength = 0.5f;
        
        [Tooltip("Bonus for trails/lifts the skier hasn't used yet.\n" +
                 "Higher = more mountain exploration, less repetition.")]
        [Range(0f, 3f)] public float noveltyBonusStrength = 0.5f;
        
        [Tooltip("Penalty for trails/lifts that are currently crowded.\n" +
                 "Higher = skiers avoid packed trails.")]
        [Range(0f, 3f)] public float crowdingPenaltyStrength = 1.0f;
        
        [Tooltip("Willingness to take easy connector trails to reach better terrain.\n" +
                 "Higher = experts more eagerly ski greens/blues as transit.")]
        [Range(0f, 3f)] public float traversalWillingness = 0.8f;
        
        [Tooltip("Penalty when recent skiers all chose the same trail/lift.\n" +
                 "Prevents herding (5 skiers in a row picking the same option).\n" +
                 "Higher = more aggressive de-herding.")]
        [Range(0f, 5f)] public float herdingPenaltyStrength = 1.5f;
        
        // ═══════════════════════════════════════════════════════════════
        //  SKILL PREFERENCES  (direct "how much do I like this trail?")
        // ═══════════════════════════════════════════════════════════════
        
        [Header("═══ BEGINNER PREFERENCES ═══")]
        [Range(0f, 1f)] public float beginnerGreen = 0.75f;
        [Range(0f, 1f)] public float beginnerBlue = 0.25f;
        [Range(0f, 1f)] public float beginnerBlack = 0.0f;
        [Range(0f, 1f)] public float beginnerDoubleBlack = 0.0f;
        
        [Header("═══ INTERMEDIATE PREFERENCES ═══")]
        [Range(0f, 1f)] public float intermediateGreen = 0.20f;
        [Range(0f, 1f)] public float intermediateBlue = 0.60f;
        [Range(0f, 1f)] public float intermediateBlack = 0.20f;
        [Range(0f, 1f)] public float intermediateDoubleBlack = 0.0f;
        
        [Header("═══ ADVANCED PREFERENCES ═══")]
        [Range(0f, 1f)] public float advancedGreen = 0.05f;
        [Range(0f, 1f)] public float advancedBlue = 0.25f;
        [Range(0f, 1f)] public float advancedBlack = 0.55f;
        [Range(0f, 1f)] public float advancedDoubleBlack = 0.15f;
        
        [Header("═══ EXPERT PREFERENCES ═══")]
        [Range(0f, 1f)] public float expertGreen = 0.02f;
        [Range(0f, 1f)] public float expertBlue = 0.10f;
        [Range(0f, 1f)] public float expertBlack = 0.30f;
        [Range(0f, 1f)] public float expertDoubleBlack = 0.58f;
        
        // ═══════════════════════════════════════════════════════════════
        //  TRAFFIC / DEFICIT SYSTEM
        // ═══════════════════════════════════════════════════════════════
        
        [Header("═══ TRAFFIC SYSTEM ═══")]
        [Tooltip("Trail capacity = WorldLength / this value.\n" +
                 "Lower = higher capacity per trail (more skiers expected).\n" +
                 "50 = 1 'slot' per 50 meters of trail.")]
        [Range(10f, 200f)] public float trailCapacityPerMeter = 50f;
        
        [Tooltip("Minimum capacity for any trail (prevents tiny trails from being ignored).")]
        [Range(1f, 10f)] public float minimumTrailCapacity = 2f;
        
        [Tooltip("Lift capacity divisor: LiftData.Capacity / this value.\n" +
                 "Scales the existing capacity field to a reasonable slot count.")]
        [Range(50f, 500f)] public float liftCapacityDivisor = 200f;
        
        [Tooltip("How quickly deficit responds to skier movements.\n" +
                 "Higher = more reactive, lower = smoother.")]
        [Range(0.1f, 2f)] public float deficitResponsiveness = 1.0f;
        
        // ═══════════════════════════════════════════════════════════════
        //  DOWNSTREAM LOOKAHEAD
        // ═══════════════════════════════════════════════════════════════
        
        [Header("═══ DOWNSTREAM LOOKAHEAD ═══")]
        [Tooltip("How many lift-hops ahead the skier evaluates terrain.\n" +
                 "1 = only sees trails at the next lift.\n" +
                 "5+ = effectively sees the entire mountain.")]
        [Range(1, 8)] public int downstreamDepth = 5;
        
        [Tooltip("Value multiplier for terrain 1 hop away")]
        [Range(0f, 1.5f)] public float depthDiscount1Hop = 1.0f;
        
        [Tooltip("Value multiplier for terrain 2 hops away")]
        [Range(0f, 1.5f)] public float depthDiscount2Hop = 0.7f;
        
        [Tooltip("Value multiplier for terrain 3 hops away")]
        [Range(0f, 1.5f)] public float depthDiscount3Hop = 0.45f;
        
        [Tooltip("Value multiplier for terrain 4+ hops away")]
        [Range(0f, 1.5f)] public float depthDiscountFarther = 0.3f;
        
        [Tooltip("Score assigned to dead-end trails (no safe exit for this skill level).")]
        [Range(0f, 0.5f)] public float deadEndScore = 0.02f;
        
        // ═══════════════════════════════════════════════════════════════
        //  TRANSIT WILLINGNESS
        // ═══════════════════════════════════════════════════════════════
        
        [Header("═══ TRANSIT WILLINGNESS ═══")]
        [Tooltip("Minimum weight for trails at or below skier's skill level.")]
        [Range(0f, 0.5f)] public float transitFloorBase = 0.15f;
        
        [Tooltip("Extra transit willingness per skill gap level.")]
        [Range(0f, 0.1f)] public float transitFloorGapBonus = 0.03f;
        
        [Tooltip("Transit floor for trails ONE step above skill level.")]
        [Range(0f, 0.3f)] public float transitFloorStretch = 0.08f;
        
        [Tooltip("Multiplier for downstream bonus in GetEffectiveWeight.")]
        [Range(0f, 2f)] public float downstreamBonusMultiplier = 0.6f;
        
        [Tooltip("Weight assigned to dead-end trails in GetEffectiveWeight.")]
        [Range(0f, 0.5f)] public float deadEndWeight = 0.02f;
        
        // ═══════════════════════════════════════════════════════════════
        //  GOAL SYSTEM
        // ═══════════════════════════════════════════════════════════════
        
        [Header("═══ GOAL SYSTEM ═══")]
        [Tooltip("Re-plan the skier's multi-hop route after finishing each trail.")]
        public bool replanAfterEveryRun = true;
        
        [Tooltip("Re-plan if goal is stale when reaching a lift top.")]
        public bool replanAtLiftTop = true;
        
        [Tooltip("Weight boost for strongly-preferred destination trails in goal planning.")]
        [Range(1f, 3f)] public float preferredDifficultyBoost = 1.5f;
        
        // ═══════════════════════════════════════════════════════════════
        //  JUNCTION SWITCHING
        // ═══════════════════════════════════════════════════════════════
        
        [Header("═══ JUNCTION SWITCHING ═══")]
        [Tooltip("If false, skiers commit to trails from start to end.\n" +
                 "No mid-trail bail-outs. Decisions only at trail endpoints and lift tops.\n" +
                 "Enable only for testing — mid-trail switching causes erratic behavior.")]
        public bool allowMidTrailSwitching = false;
        
        [Tooltip("Chance to switch when a much better trail crosses nearby")]
        [Range(0f, 1f)] public float junctionMajorSwitchChance = 0.50f;
        
        [Range(0f, 1f)] public float junctionMajorThreshold = 0.25f;
        
        [Range(0f, 1f)] public float junctionModerateSwitchChance = 0.25f;
        
        [Range(0f, 1f)] public float junctionModerateThreshold = 0.1f;
        
        [Range(0f, 0.5f)] public float junctionExplorationChance = 0.12f;
        
        [Range(0f, 1f)] public float junctionExplorationMinValue = 0.2f;
        
        // ═══════════════════════════════════════════════════════════════
        //  SEARCH RADII
        // ═══════════════════════════════════════════════════════════════
        
        [Header("═══ SEARCH RADII ═══")]
        [Range(5f, 200f)] public float trailStartSearchRadius = 50f;
        [Range(5f, 200f)] public float liftSearchRadius = 50f;
        [Range(10f, 200f)] public float goalLiftWalkRadius = 60f;
        [Range(5f, 100f)] public float junctionDetectionRadius = 15f;
        [Range(10f, 100f)] public float networkSnapRadius = 25f;
        
        // ═══════════════════════════════════════════════════════════════
        //  SKIER POPULATION
        // ═══════════════════════════════════════════════════════════════
        
        [Header("═══ SKIER POPULATION ═══")]
        [Range(0f, 1f)] public float beginnerPercent = 0.20f;
        [Range(0f, 1f)] public float intermediatePercent = 0.30f;
        [Range(0f, 1f)] public float advancedPercent = 0.30f;
        [Range(0f, 1f)] public float expertPercent = 0.20f;
        
        // ═══════════════════════════════════════════════════════════════
        //  MISCELLANEOUS
        // ═══════════════════════════════════════════════════════════════
        
        [Header("═══ MISCELLANEOUS ═══")]
        [Tooltip("Chance of completely random trail/lift selection (chaos/variety).")]
        [Range(0f, 0.2f)] public float jerryChance = 0.02f;
        
        [Tooltip("Lodge visit chance after finishing a trail.")]
        [Range(0f, 0.5f)] public float lodgeVisitChance = 0.15f;
        
        // ═══════════════════════════════════════════════════════════════
        //  DEBUG
        // ═══════════════════════════════════════════════════════════════
        
        [Header("═══ DEBUG ═══")]
        public bool enableDebugLogs = false;
        
        [Tooltip("If >= 0, only log decisions for this specific skier ID")]
        public int debugSkierId = -1;
        
        public bool logTrailScores = false;
        public bool logLiftScores = false;
        public bool logDeficitValues = false;
        
        // ═══════════════════════════════════════════════════════════════
        //  HELPER METHODS
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Gets the preference for a specific skill/difficulty combo from the config sliders.
        /// </summary>
        public float GetPreference(SkillLevel skill, TrailDifficulty difficulty)
        {
            switch (skill)
            {
                case SkillLevel.Beginner:
                    switch (difficulty)
                    {
                        case TrailDifficulty.Green: return beginnerGreen;
                        case TrailDifficulty.Blue: return beginnerBlue;
                        case TrailDifficulty.Black: return beginnerBlack;
                        case TrailDifficulty.DoubleBlack: return beginnerDoubleBlack;
                    }
                    break;
                case SkillLevel.Intermediate:
                    switch (difficulty)
                    {
                        case TrailDifficulty.Green: return intermediateGreen;
                        case TrailDifficulty.Blue: return intermediateBlue;
                        case TrailDifficulty.Black: return intermediateBlack;
                        case TrailDifficulty.DoubleBlack: return intermediateDoubleBlack;
                    }
                    break;
                case SkillLevel.Advanced:
                    switch (difficulty)
                    {
                        case TrailDifficulty.Green: return advancedGreen;
                        case TrailDifficulty.Blue: return advancedBlue;
                        case TrailDifficulty.Black: return advancedBlack;
                        case TrailDifficulty.DoubleBlack: return advancedDoubleBlack;
                    }
                    break;
                case SkillLevel.Expert:
                    switch (difficulty)
                    {
                        case TrailDifficulty.Green: return expertGreen;
                        case TrailDifficulty.Blue: return expertBlue;
                        case TrailDifficulty.Black: return expertBlack;
                        case TrailDifficulty.DoubleBlack: return expertDoubleBlack;
                    }
                    break;
            }
            return 0f;
        }
        
        /// <summary>
        /// Gets the depth discount for a given remaining depth.
        /// </summary>
        public float GetDepthDiscount(int maxDepth, int totalDepth)
        {
            int hopsAway = totalDepth - maxDepth + 1;
            switch (hopsAway)
            {
                case 1: return depthDiscount1Hop;
                case 2: return depthDiscount2Hop;
                case 3: return depthDiscount3Hop;
                default: return depthDiscountFarther;
            }
        }
        
        /// <summary>
        /// Applies all preference weights and tunable parameters to a SkierDistribution.
        /// </summary>
        public void ApplyToDistribution(SkierDistribution dist)
        {
            if (dist == null) return;
            
            dist.SetPreference(SkillLevel.Beginner, TrailDifficulty.Green, beginnerGreen);
            dist.SetPreference(SkillLevel.Beginner, TrailDifficulty.Blue, beginnerBlue);
            dist.SetPreference(SkillLevel.Beginner, TrailDifficulty.Black, beginnerBlack);
            dist.SetPreference(SkillLevel.Beginner, TrailDifficulty.DoubleBlack, beginnerDoubleBlack);
            
            dist.SetPreference(SkillLevel.Intermediate, TrailDifficulty.Green, intermediateGreen);
            dist.SetPreference(SkillLevel.Intermediate, TrailDifficulty.Blue, intermediateBlue);
            dist.SetPreference(SkillLevel.Intermediate, TrailDifficulty.Black, intermediateBlack);
            dist.SetPreference(SkillLevel.Intermediate, TrailDifficulty.DoubleBlack, intermediateDoubleBlack);
            
            dist.SetPreference(SkillLevel.Advanced, TrailDifficulty.Green, advancedGreen);
            dist.SetPreference(SkillLevel.Advanced, TrailDifficulty.Blue, advancedBlue);
            dist.SetPreference(SkillLevel.Advanced, TrailDifficulty.Black, advancedBlack);
            dist.SetPreference(SkillLevel.Advanced, TrailDifficulty.DoubleBlack, advancedDoubleBlack);
            
            dist.SetPreference(SkillLevel.Expert, TrailDifficulty.Green, expertGreen);
            dist.SetPreference(SkillLevel.Expert, TrailDifficulty.Blue, expertBlue);
            dist.SetPreference(SkillLevel.Expert, TrailDifficulty.Black, expertBlack);
            dist.SetPreference(SkillLevel.Expert, TrailDifficulty.DoubleBlack, expertDoubleBlack);
            
            dist.BeginnerPercent = beginnerPercent;
            dist.IntermediatePercent = intermediatePercent;
            dist.AdvancedPercent = advancedPercent;
            dist.ExpertPercent = expertPercent;
            
            dist.TransitFloorBase = transitFloorBase;
            dist.TransitFloorGapBonus = transitFloorGapBonus;
            dist.TransitFloorStretch = transitFloorStretch;
            dist.DownstreamBonusMultiplier = downstreamBonusMultiplier;
            dist.DeadEndWeight = deadEndWeight;
        }
    }
}
