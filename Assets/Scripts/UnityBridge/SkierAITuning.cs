using UnityEngine;
using SkiResortTycoon.Core;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Runtime-tunable sliders for the skier AI system.
    /// Add this to any GameObject in your scene (or the same object as SkierVisualizer).
    /// Adjust values in the Inspector while playing to immediately see behavior changes.
    /// 
    /// HOW IT WORKS:
    /// - SkierVisualizer reads from SkierAITuning.Instance every decision frame
    /// - When you change a slider, the downstream cache is cleared and new decisions
    ///   use the updated values immediately
    /// - If this component doesn't exist, all values fall back to hardcoded defaults
    /// </summary>
    public class SkierAITuning : MonoBehaviour
    {
        public static SkierAITuning Instance { get; private set; }
        
        /// <summary>
        /// Incremented whenever a value changes. SkierVisualizer watches this
        /// to know when to clear caches and re-sync preferences.
        /// </summary>
        public int Version { get; private set; } = 0;

        // ═══════════════════════════════════════════════════════════════
        //  SKILL PREFERENCES  (direct "how much do I like this trail?")
        // ═══════════════════════════════════════════════════════════════
        
        [Header("═══ BEGINNER PREFERENCES ═══")]
        [Tooltip("How much beginners prefer green trails (0-1)")]
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
        //  SCORING FORMULA  (how direct pref + downstream combine)
        // ═══════════════════════════════════════════════════════════════
        
        [Header("═══ SCORING FORMULA ═══")]
        [Tooltip("Weight of the trail's own difficulty preference in the final score.\n" +
                 "Higher = skiers care more about THIS trail's difficulty.\n" +
                 "Lower = skiers care more about what's BEYOND this trail.")]
        [Range(0f, 3f)] public float directPreferenceWeight = 1.0f;
        
        [Tooltip("Weight of downstream terrain value in the final score.\n" +
                 "Higher = skiers will eagerly take easy trails to reach great terrain.\n" +
                 "This is THE key slider for mountain traversal behavior.")]
        [Range(0f, 3f)] public float downstreamWeight = 1.0f;
        
        [Tooltip("Score assigned to dead-end trails (no safe exit for this skill level).\n" +
                 "Lower = beginners more strongly avoid death-trap greens.\n" +
                 "0.02 = virtually never chosen. 0.3 = sometimes chosen.")]
        [Range(0f, 0.5f)] public float deadEndScore = 0.02f;
        
        // ═══════════════════════════════════════════════════════════════
        //  DOWNSTREAM LOOKAHEAD  (how far ahead skiers "see")
        // ═══════════════════════════════════════════════════════════════
        
        [Header("═══ DOWNSTREAM LOOKAHEAD ═══")]
        [Tooltip("How many lift-hops ahead the skier evaluates terrain.\n" +
                 "1 = only sees trails at the next lift.\n" +
                 "3 = sees terrain 3 lifts away.\n" +
                 "5+ = effectively sees the entire mountain.")]
        [Range(1, 8)] public int downstreamDepth = 3;
        
        [Tooltip("Value multiplier for terrain 1 hop away (default 1.0 = full value)")]
        [Range(0f, 1.5f)] public float depthDiscount1Hop = 1.0f;
        
        [Tooltip("Value multiplier for terrain 2 hops away")]
        [Range(0f, 1.5f)] public float depthDiscount2Hop = 0.65f;
        
        [Tooltip("Value multiplier for terrain 3 hops away")]
        [Range(0f, 1.5f)] public float depthDiscount3Hop = 0.40f;
        
        [Tooltip("Value multiplier for terrain 4+ hops away")]
        [Range(0f, 1.5f)] public float depthDiscountFarther = 0.25f;
        
        // ═══════════════════════════════════════════════════════════════
        //  TRANSIT WILLINGNESS  (taking easy trails as connectors)
        // ═══════════════════════════════════════════════════════════════
        
        [Header("═══ TRANSIT WILLINGNESS ═══")]
        [Tooltip("Minimum weight for trails at or below skier's skill level.\n" +
                 "Higher = more willing to cruise easy trails.\n" +
                 "An expert with transitFloor=0.3 will happily take any green.")]
        [Range(0f, 0.5f)] public float transitFloorBase = 0.15f;
        
        [Tooltip("Extra transit willingness per skill gap level.\n" +
                 "Expert on green: gap=3, so transitFloor = base + 3*gapBonus")]
        [Range(0f, 0.1f)] public float transitFloorGapBonus = 0.03f;
        
        [Tooltip("Transit floor for trails ONE step above skill level.\n" +
                 "e.g., intermediate on black")]
        [Range(0f, 0.3f)] public float transitFloorStretch = 0.08f;
        
        [Tooltip("Multiplier for downstream bonus in GetEffectiveWeight.\n" +
                 "Higher = stronger pull toward trails leading to good terrain.")]
        [Range(0f, 2f)] public float downstreamBonusMultiplier = 0.6f;
        
        [Tooltip("Weight assigned to dead-end trails in GetEffectiveWeight.\n" +
                 "Should match deadEndScore for consistency.")]
        [Range(0f, 0.5f)] public float deadEndWeight = 0.02f;
        
        // ═══════════════════════════════════════════════════════════════
        //  GOAL SYSTEM  (route planning behavior)
        // ═══════════════════════════════════════════════════════════════
        
        [Header("═══ GOAL SYSTEM ═══")]
        [Tooltip("Re-plan the skier's multi-hop route after finishing each trail.\n" +
                 "ON = skiers constantly evaluate the best next destination.\n" +
                 "OFF = skiers only plan once at spawn (old behavior).")]
        public bool replanAfterEveryRun = true;
        
        [Tooltip("Re-plan if goal is stale when reaching a lift top.\n" +
                 "Ensures skiers always have a valid plan.")]
        public bool replanAtLiftTop = true;
        
        [Tooltip("Weight boost for strongly-preferred destination trails.\n" +
                 "Makes experts target blacks/doubles instead of randomly picking greens.")]
        [Range(1f, 3f)] public float preferredDifficultyBoost = 1.5f;
        
        [Tooltip("Bonus multiplier for the goal's suggested trail at lift top.\n" +
                 "1.0 = no bonus. 1.2 = 20% bonus. Goal is a tiebreaker, not a veto.")]
        [Range(1f, 2f)] public float goalTrailBonus = 1.2f;
        
        // ═══════════════════════════════════════════════════════════════
        //  RANDOMNESS & EXPLORATION
        // ═══════════════════════════════════════════════════════════════
        
        [Header("═══ RANDOMNESS ═══")]
        [Tooltip("Chance a skier ignores all logic and picks randomly (Jerry behavior).\n" +
                 "Higher = more chaos, more variety, less 'optimal' routing.")]
        [Range(0f, 0.2f)] public float jerryChance = 0.02f;
        
        [Tooltip("Minimum score floor for any trail (prevents 0-weight options).\n" +
                 "Higher = more randomness in trail selection.")]
        [Range(0.001f, 0.1f)] public float minimumTrailScore = 0.01f;
        
        // ═══════════════════════════════════════════════════════════════
        //  LIFT SELECTION
        // ═══════════════════════════════════════════════════════════════
        
        [Header("═══ LIFT SELECTION ═══")]
        [Tooltip("Score multiplier for lifts the skier hasn't ridden yet.\n" +
                 "Higher = more mountain exploration.")]
        [Range(1f, 3f)] public float liftVarietyNewBonus = 1.4f;
        
        [Tooltip("Score multiplier for lifts the skier has already ridden.\n" +
                 "Lower = stronger push to explore new lifts.")]
        [Range(0.3f, 1f)] public float liftVarietyRepeatPenalty = 0.85f;
        
        // ═══════════════════════════════════════════════════════════════
        //  JUNCTION SWITCHING  (mid-trail decisions)
        // ═══════════════════════════════════════════════════════════════
        
        [Header("═══ JUNCTION SWITCHING ═══")]
        [Tooltip("Chance to switch when a much better trail crosses nearby")]
        [Range(0f, 1f)] public float junctionMajorSwitchChance = 0.50f;
        
        [Tooltip("Value improvement threshold for 'major' junction switch")]
        [Range(0f, 1f)] public float junctionMajorThreshold = 0.25f;
        
        [Tooltip("Chance to switch for moderate improvement")]
        [Range(0f, 1f)] public float junctionModerateSwitchChance = 0.25f;
        
        [Tooltip("Value improvement threshold for 'moderate' junction switch")]
        [Range(0f, 1f)] public float junctionModerateThreshold = 0.1f;
        
        [Tooltip("Chance to switch to any decent nearby trail (exploration)")]
        [Range(0f, 0.5f)] public float junctionExplorationChance = 0.12f;
        
        [Tooltip("Minimum trail value for exploration switching")]
        [Range(0f, 1f)] public float junctionExplorationMinValue = 0.2f;
        
        // ═══════════════════════════════════════════════════════════════
        //  SEARCH RADII  (how far skiers look for things)
        // ═══════════════════════════════════════════════════════════════
        
        [Header("═══ SEARCH RADII ═══")]
        [Tooltip("How far from a lift top to look for trail starts (3D units)")]
        [Range(5f, 200f)] public float trailStartSearchRadius = 25f;
        
        [Tooltip("How far from a trail end to look for lift bottoms (3D units)")]
        [Range(5f, 200f)] public float liftSearchRadius = 25f;
        
        [Tooltip("How far to walk to a goal's suggested lift (3D units)")]
        [Range(10f, 200f)] public float goalLiftWalkRadius = 40f;
        
        [Tooltip("How far to look for mid-trail junction segments (3D units)")]
        [Range(5f, 100f)] public float junctionDetectionRadius = 15f;
        
        [Tooltip("How far the BFS network graph connects snap points (3D units).\n" +
                 "This affects pathfinding - higher = more connections found.")]
        [Range(10f, 100f)] public float networkSnapRadius = 25f;
        
        // ═══════════════════════════════════════════════════════════════
        //  SKIER POPULATION
        // ═══════════════════════════════════════════════════════════════
        
        [Header("═══ SKIER POPULATION ═══")]
        [Tooltip("Percentage of skiers that are beginners")]
        [Range(0f, 1f)] public float beginnerPercent = 0.20f;
        [Range(0f, 1f)] public float intermediatePercent = 0.30f;
        [Range(0f, 1f)] public float advancedPercent = 0.30f;
        [Range(0f, 1f)] public float expertPercent = 0.20f;
        
        // ═══════════════════════════════════════════════════════════════
        //  DEBUG
        // ═══════════════════════════════════════════════════════════════
        
        [Header("═══ DEBUG ═══")]
        [Tooltip("Enable detailed debug logs for all skier decisions")]
        public bool enableDebugLogs = false;
        
        [Tooltip("If >= 0, only log decisions for this specific skier ID")]
        public int debugSkierId = -1;
        
        [Tooltip("Show a summary of trail scores at each lift top in the console")]
        public bool logTrailScores = false;
        
        [Tooltip("Show a summary of lift scores at each trail end in the console")]
        public bool logLiftScores = false;

        // ═══════════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ═══════════════════════════════════════════════════════════════
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[SkierAITuning] Duplicate instance destroyed. Only one should exist.");
                Destroy(this);
                return;
            }
            Instance = this;
        }
        
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
        
        /// <summary>
        /// Called by Unity when any Inspector value changes (editor only, but also
        /// works at runtime via script changes).
        /// </summary>
        private void OnValidate()
        {
            Version++;
        }
        
        /// <summary>
        /// Applies all preference weights and tunable parameters to a SkierDistribution.
        /// Called by SkierVisualizer when the version changes.
        /// </summary>
        public void ApplyToDistribution(SkierDistribution dist)
        {
            if (dist == null) return;
            
            // Skill preferences
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
            
            // Population distribution
            dist.BeginnerPercent = beginnerPercent;
            dist.IntermediatePercent = intermediatePercent;
            dist.AdvancedPercent = advancedPercent;
            dist.ExpertPercent = expertPercent;
            
            // Transit / downstream / dead-end parameters
            dist.TransitFloorBase = transitFloorBase;
            dist.TransitFloorGapBonus = transitFloorGapBonus;
            dist.TransitFloorStretch = transitFloorStretch;
            dist.DownstreamBonusMultiplier = downstreamBonusMultiplier;
            dist.DeadEndWeight = deadEndWeight;
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
    }
}
