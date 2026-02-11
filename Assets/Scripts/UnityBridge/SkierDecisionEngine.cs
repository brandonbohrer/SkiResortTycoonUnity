using UnityEngine;
using System;
using System.Collections.Generic;
using SkiResortTycoon.Core;
using SkiResortTycoon.ScriptableObjects;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Context struct passed to scoring functions. Captures everything
    /// about a skier's current state that affects decision-making.
    /// </summary>
    public struct SkierContext
    {
        public int SkierId;
        public SkillLevel Skill;
        public int GoalTrailId;    // -1 if no goal or goal doesn't specify a trail
        public int GoalLiftId;     // -1 if no goal or goal doesn't specify a lift
        public HashSet<int> LiftsRidden;
        public HashSet<int> TrailsSkied;
        
        /// <summary>
        /// Per-skier personality offsets for each scoring factor.
        /// Generated once at spawn from the skier's ID as seed.
        /// Indices: 0=difficulty, 1=downstream, 2=deficit, 3=goal,
        ///          4=novelty, 5=crowding, 6=traversal, 7=herding
        /// </summary>
        public float[] PersonalityOffsets;
        
        /// <summary>
        /// Number of personality offset slots.
        /// </summary>
        public const int PERSONALITY_SLOTS = 8;
        
        /// <summary>
        /// Generates personality offsets from a skier ID.
        /// Small random shifts in range [-magnitude, +magnitude] per factor.
        /// Uses deterministic seeding so the same skier always has the same personality.
        /// </summary>
        public static float[] GeneratePersonality(int skierId, float magnitude = 0.3f)
        {
            var offsets = new float[PERSONALITY_SLOTS];
            var rng = new System.Random(skierId * 31337 + 7919); // deterministic seed
            for (int i = 0; i < PERSONALITY_SLOTS; i++)
            {
                offsets[i] = (float)(rng.NextDouble() * 2.0 - 1.0) * magnitude;
            }
            return offsets;
        }
    }
    
    /// <summary>
    /// Unified decision engine for skier AI. Replaces the scattered scoring
    /// logic across SkierVisualizer with a single, consistent pipeline:
    ///
    /// 1. Compute per-factor scores (all normalized to ~[0,1])
    /// 2. Combine with weighted sum using SkierAIConfig weights
    /// 3. Select via softmax (temperature-controlled probabilistic choice)
    ///
    /// This ensures every trail/lift gets some traffic (via deficit bonus)
    /// while skiers still feel intelligent (via difficulty preference + downstream).
    /// </summary>
    public static class SkierDecisionEngine
    {
        // ─────────────────────────────────────────────────────────────
        //  Trail selection (at lift top or trail-to-trail junction)
        // ─────────────────────────────────────────────────────────────
        
        /// <summary>
        /// Scores all candidate trails and selects one via softmax.
        /// Used at lift top (HandleRidingLift) and trail-to-trail connections.
        /// </summary>
        /// <param name="candidates">Available trails at this decision point</param>
        /// <param name="ctx">Skier context (skill, goal, history)</param>
        /// <param name="config">Tunable weights</param>
        /// <param name="traffic">Global traffic state (for deficit/crowding)</param>
        /// <param name="distribution">Skill-difficulty preference lookup</param>
        /// <param name="getDownstream">Function to get cached downstream value for a trail</param>
        /// <returns>The chosen trail, or null if no candidates</returns>
        public static TrailData ChooseTrail(
            List<TrailData> candidates,
            SkierContext ctx,
            SkierAIConfig config,
            ResortTrafficState traffic,
            SkierDistribution distribution,
            Func<SkillLevel, TrailData, float> getDownstream)
        {
            if (candidates == null || candidates.Count == 0) return null;
            if (candidates.Count == 1) return candidates[0];
            
            // Jerry mode: completely random
            if (config != null && UnityEngine.Random.value < config.jerryChance)
            {
                return candidates[UnityEngine.Random.Range(0, candidates.Count)];
            }
            
            var scored = new List<(TrailData trail, float score)>();
            
            foreach (var trail in candidates)
            {
                float score = ScoreTrail(trail, ctx, config, traffic, distribution, getDownstream);
                scored.Add((trail, score));
            }
            
            float temperature = config != null ? config.softmaxTemperature : 1.5f;
            int chosen = SoftmaxSelect(scored, temperature);
            
            if (config != null && (config.logTrailScores || (config.debugSkierId >= 0 && ctx.SkierId == config.debugSkierId)))
            {
                LogTrailScores(ctx, scored, chosen, temperature);
            }
            
            return scored[chosen].trail;
        }
        
        /// <summary>
        /// Computes the unified score for a single trail.
        /// </summary>
        public static float ScoreTrail(
            TrailData trail,
            SkierContext ctx,
            SkierAIConfig config,
            ResortTrafficState traffic,
            SkierDistribution distribution,
            Func<SkillLevel, TrailData, float> getDownstream)
        {
            // Hard block: skill not allowed on this difficulty
            if (!distribution.IsAllowed(ctx.Skill, trail.Difficulty)) return 0f;
            if (distribution.IsDesperateOnly(ctx.Skill, trail.Difficulty)) return 0.01f;
            
            float wDiff = config != null ? config.difficultyPreferenceStrength : 1.0f;
            float wDown = config != null ? config.downstreamValueStrength : 1.0f;
            float wDeficit = config != null ? config.deficitBonusStrength : 2.5f;
            float wGoal = config != null ? config.goalAlignmentStrength : 0.5f;
            float wNovelty = config != null ? config.noveltyBonusStrength : 0.5f;
            float wCrowding = config != null ? config.crowdingPenaltyStrength : 1.0f;
            float wTraversal = config != null ? config.traversalWillingness : 0.8f;
            
            // ── Factor 1: Difficulty preference (0-1) ──
            float diffPref = distribution.GetPreference(ctx.Skill, trail.Difficulty);
            
            // ── Factor 2: Downstream terrain value (0-1) ──
            float downstream = 0f;
            if (getDownstream != null)
            {
                downstream = getDownstream(ctx.Skill, trail);
            }
            
            // Dead-end penalty: low downstream means fewer options beyond this trail,
            // but it should NOT kill the score entirely. The trail's own difficulty
            // preference, deficit bonus, etc. should still matter.
            // A trail with downstream=0 just doesn't get the downstream bonus.
            
            // ── Factor 3: Deficit bonus (-1 to 1, typically -0.5 to 0.5) ──
            float deficit = 0f;
            if (traffic != null)
            {
                deficit = traffic.GetTrailDeficit(trail.TrailId);
            }
            
            // ── Factor 4: Goal alignment (0 or 1) ──
            float goalAlign = (ctx.GoalTrailId >= 0 && trail.TrailId == ctx.GoalTrailId) ? 1.0f : 0.0f;
            
            // ── Factor 5: Novelty (0 or 1) ──
            // Trails the skier hasn't skied recently get a novelty bonus
            float novelty = 0f;
            if (ctx.TrailsSkied != null && !ctx.TrailsSkied.Contains(trail.TrailId))
            {
                novelty = 1.0f;
            }
            
            // ── Factor 6: Crowding (0 to 1+) ──
            float crowding = 0f;
            if (traffic != null)
            {
                crowding = traffic.GetTrailCrowding(trail.TrailId);
            }
            
            // ── Factor 7: Traversal willingness ──
            // How willing the skier is to take this as a connector trail.
            // Based on skill gap: easier trails below your level are fine as transit.
            float traversal = ComputeTraversalScore(ctx.Skill, trail.Difficulty);
            
            // ── Factor 8: Herding penalty ──
            // If the last N skiers all chose this trail, strongly discourage it.
            // This prevents the "5 skiers in a row pick the same trail" problem.
            float herding = 0f;
            if (traffic != null)
            {
                herding = traffic.GetTrailRecentPopularity(trail.TrailId);
            }
            float wHerding = config != null ? config.herdingPenaltyStrength : 1.5f;
            
            // ── Apply per-skier personality offsets ──
            // Each skier has small random shifts to their weight sensitivities,
            // creating natural variation even with identical inputs.
            if (ctx.PersonalityOffsets != null && ctx.PersonalityOffsets.Length >= SkierContext.PERSONALITY_SLOTS)
            {
                wDiff     += ctx.PersonalityOffsets[0];
                wDown     += ctx.PersonalityOffsets[1];
                wDeficit  += ctx.PersonalityOffsets[2];
                wGoal     += ctx.PersonalityOffsets[3];
                wNovelty  += ctx.PersonalityOffsets[4];
                wCrowding += ctx.PersonalityOffsets[5];
                wTraversal += ctx.PersonalityOffsets[6];
                wHerding  += ctx.PersonalityOffsets[7];
            }
            
            // ── Combine all factors ──
            float score = diffPref * wDiff
                        + downstream * wDown
                        + deficit * wDeficit
                        + goalAlign * wGoal
                        + novelty * wNovelty
                        - crowding * wCrowding
                        + traversal * wTraversal
                        - herding * wHerding;
            
            // Floor: no option should be truly zero (softmax needs positive values)
            return Mathf.Max(score, 0.01f);
        }
        
        // ─────────────────────────────────────────────────────────────
        //  Lift selection (at trail end)
        // ─────────────────────────────────────────────────────────────
        
        /// <summary>
        /// Scores all candidate lifts and selects one via softmax.
        /// Used at trail end (OnTrailFinished).
        /// </summary>
        public static LiftData ChooseLift(
            List<LiftData> candidates,
            SkierContext ctx,
            SkierAIConfig config,
            ResortTrafficState traffic,
            SkierDistribution distribution,
            Func<SkillLevel, LiftData, float> getBestTrailValue)
        {
            if (candidates == null || candidates.Count == 0) return null;
            if (candidates.Count == 1) return candidates[0];
            
            if (config != null && UnityEngine.Random.value < config.jerryChance)
            {
                return candidates[UnityEngine.Random.Range(0, candidates.Count)];
            }
            
            var scored = new List<(LiftData lift, float score)>();
            
            foreach (var lift in candidates)
            {
                float score = ScoreLift(lift, ctx, config, traffic, distribution, getBestTrailValue);
                scored.Add((lift, score));
            }
            
            float temperature = config != null ? config.softmaxTemperature : 1.5f;
            int chosen = SoftmaxSelect(scored, temperature);
            
            if (config != null && (config.logLiftScores || (config.debugSkierId >= 0 && ctx.SkierId == config.debugSkierId)))
            {
                LogLiftScores(ctx, scored, chosen, temperature);
            }
            
            return scored[chosen].lift;
        }
        
        /// <summary>
        /// Computes the unified score for a single lift.
        /// </summary>
        public static float ScoreLift(
            LiftData lift,
            SkierContext ctx,
            SkierAIConfig config,
            ResortTrafficState traffic,
            SkierDistribution distribution,
            Func<SkillLevel, LiftData, float> getBestTrailValue)
        {
            float wDiff = config != null ? config.difficultyPreferenceStrength : 1.0f;
            float wDeficit = config != null ? config.deficitBonusStrength : 2.5f;
            float wGoal = config != null ? config.goalAlignmentStrength : 0.5f;
            float wNovelty = config != null ? config.noveltyBonusStrength : 0.5f;
            float wCrowding = config != null ? config.crowdingPenaltyStrength : 1.0f;
            
            // ── Factor 1: Best trail value at this lift's top ──
            float bestTrailValue = 0f;
            if (getBestTrailValue != null)
            {
                bestTrailValue = getBestTrailValue(ctx.Skill, lift);
            }
            
            // ── Factor 2: Deficit ──
            float deficit = 0f;
            if (traffic != null)
            {
                deficit = traffic.GetLiftDeficit(lift.LiftId);
            }
            
            // ── Factor 3: Goal alignment ──
            float goalAlign = (ctx.GoalLiftId >= 0 && lift.LiftId == ctx.GoalLiftId) ? 1.0f : 0.0f;
            
            // ── Factor 4: Novelty ──
            float novelty = 0f;
            if (ctx.LiftsRidden != null && !ctx.LiftsRidden.Contains(lift.LiftId))
            {
                novelty = 1.0f;
            }
            
            // ── Factor 5: Crowding ──
            float crowding = 0f;
            if (traffic != null)
            {
                crowding = traffic.GetLiftCrowding(lift.LiftId);
            }
            
            // ── Factor 6: Herding penalty ──
            float herding = 0f;
            if (traffic != null)
            {
                herding = traffic.GetLiftRecentPopularity(lift.LiftId);
            }
            float wHerding = config != null ? config.herdingPenaltyStrength : 1.5f;
            
            // ── Apply per-skier personality offsets ──
            if (ctx.PersonalityOffsets != null && ctx.PersonalityOffsets.Length >= SkierContext.PERSONALITY_SLOTS)
            {
                wDiff     += ctx.PersonalityOffsets[0];
                wDeficit  += ctx.PersonalityOffsets[2];
                wGoal     += ctx.PersonalityOffsets[3];
                wNovelty  += ctx.PersonalityOffsets[4];
                wCrowding += ctx.PersonalityOffsets[5];
                wHerding  += ctx.PersonalityOffsets[7];
            }
            
            float score = bestTrailValue * wDiff
                        + deficit * wDeficit
                        + goalAlign * wGoal
                        + novelty * wNovelty
                        - crowding * wCrowding
                        - herding * wHerding;
            
            return Mathf.Max(score, 0.01f);
        }
        
        // ─────────────────────────────────────────────────────────────
        //  Softmax selection
        // ─────────────────────────────────────────────────────────────
        
        /// <summary>
        /// Selects an index from a scored list using softmax probabilities.
        /// Returns the chosen index.
        ///
        /// p(i) = exp(score_i / T) / sum(exp(score_j / T))
        ///
        /// Low T → near-deterministic (picks highest score)
        /// High T → near-uniform (random spread)
        /// </summary>
        public static int SoftmaxSelect<T>(List<(T item, float score)> items, float temperature)
        {
            if (items.Count == 0) return -1;
            if (items.Count == 1) return 0;
            
            temperature = Mathf.Max(temperature, 0.01f); // prevent division by zero
            
            // Find max score for numerical stability (subtract max before exp)
            float maxScore = float.MinValue;
            foreach (var (_, s) in items)
            {
                if (s > maxScore) maxScore = s;
            }
            
            // Compute exp(score / T) for each item
            float totalExp = 0f;
            var exps = new float[items.Count];
            for (int i = 0; i < items.Count; i++)
            {
                exps[i] = Mathf.Exp((items[i].score - maxScore) / temperature);
                totalExp += exps[i];
            }
            
            // Weighted random selection based on softmax probabilities
            float roll = UnityEngine.Random.value * totalExp;
            float cumulative = 0f;
            for (int i = 0; i < items.Count; i++)
            {
                cumulative += exps[i];
                if (roll <= cumulative) return i;
            }
            
            return items.Count - 1; // fallback
        }
        
        // ─────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────
        
        /// <summary>
        /// How willing a skier is to use this trail as a connector.
        /// Expert on green = high traversal score. Beginner on black = 0.
        /// Returns 0-1.
        /// </summary>
        private static float ComputeTraversalScore(SkillLevel skill, TrailDifficulty difficulty)
        {
            int skillLevel = (int)skill;
            int diffLevel = (int)difficulty;
            
            if (diffLevel <= skillLevel)
            {
                // Trail is at or below skill level — easy transit
                int gap = skillLevel - diffLevel;
                // Expert on green (gap=3): 0.9, Expert on blue (gap=2): 0.7, etc.
                return Mathf.Clamp01(0.3f + gap * 0.2f);
            }
            else if (diffLevel == skillLevel + 1)
            {
                // One step above: manageable but uncomfortable
                return 0.1f;
            }
            
            return 0f; // trail is way above skill
        }
        
        // ─────────────────────────────────────────────────────────────
        //  Debug logging
        // ─────────────────────────────────────────────────────────────
        
        private static void LogTrailScores(SkierContext ctx, List<(TrailData trail, float score)> scored, int chosenIdx, float temperature)
        {
            string lines = "";
            for (int i = 0; i < scored.Count; i++)
            {
                var (t, s) = scored[i];
                string marker = i == chosenIdx ? ">>>" : "   ";
                string goalTag = (ctx.GoalTrailId >= 0 && t.TrailId == ctx.GoalTrailId) ? " [GOAL]" : "";
                lines += $"\n  {marker} Trail {t.TrailId} ({t.Difficulty}): {s:F3}{goalTag}";
            }
            Debug.Log($"[DecisionEngine] Skier {ctx.SkierId} ({ctx.Skill}) trail choice (T={temperature:F2}):{lines}");
        }
        
        private static void LogLiftScores(SkierContext ctx, List<(LiftData lift, float score)> scored, int chosenIdx, float temperature)
        {
            string lines = "";
            for (int i = 0; i < scored.Count; i++)
            {
                var (l, s) = scored[i];
                string marker = i == chosenIdx ? ">>>" : "   ";
                string goalTag = (ctx.GoalLiftId >= 0 && l.LiftId == ctx.GoalLiftId) ? " [GOAL]" : "";
                lines += $"\n  {marker} Lift {l.LiftId}: {s:F3}{goalTag}";
            }
            Debug.Log($"[DecisionEngine] Skier {ctx.SkierId} ({ctx.Skill}) lift choice (T={temperature:F2}):{lines}");
        }
    }
}
