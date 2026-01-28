using System;
using System.Collections.Generic;

namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Configuration for skier skill distribution and trail preferences.
    /// All values are configurable for easy tuning.
    /// </summary>
    public class SkierDistribution
    {
        // Skill level distribution (should sum to 1.0)
        public float BeginnerPercent { get; set; } = 0.20f;
        public float IntermediatePercent { get; set; } = 0.30f;
        public float AdvancedPercent { get; set; } = 0.30f;
        public float ExpertPercent { get; set; } = 0.20f;
        
        // Trail preference weights (per skill level)
        // Key: SkillLevel, Inner Key: TrailDifficulty, Value: Weight (0-1)
        private Dictionary<SkillLevel, Dictionary<TrailDifficulty, float>> _preferences;
        
        // Hard caps: what difficulties each skill level is ALLOWED to ski
        private Dictionary<SkillLevel, HashSet<TrailDifficulty>> _allowedDifficulties;
        
        public SkierDistribution()
        {
            InitializeDefaultPreferences();
            InitializeHardCaps();
        }
        
        private void InitializeDefaultPreferences()
        {
            _preferences = new Dictionary<SkillLevel, Dictionary<TrailDifficulty, float>>();
            
            // Beginner: Green 75%, Blue 25%, Black 0%, DoubleBlack 0%
            _preferences[SkillLevel.Beginner] = new Dictionary<TrailDifficulty, float>
            {
                { TrailDifficulty.Green, 0.75f },
                { TrailDifficulty.Blue, 0.25f },
                { TrailDifficulty.Black, 0.0f },
                { TrailDifficulty.DoubleBlack, 0.0f }
            };
            
            // Intermediate: Green 20%, Blue 60%, Black 20%, DoubleBlack 0%
            _preferences[SkillLevel.Intermediate] = new Dictionary<TrailDifficulty, float>
            {
                { TrailDifficulty.Green, 0.20f },
                { TrailDifficulty.Blue, 0.60f },
                { TrailDifficulty.Black, 0.20f },
                { TrailDifficulty.DoubleBlack, 0.0f }
            };
            
            // Advanced: Green 5%, Blue 25%, Black 55%, DoubleBlack 15%
            _preferences[SkillLevel.Advanced] = new Dictionary<TrailDifficulty, float>
            {
                { TrailDifficulty.Green, 0.05f },
                { TrailDifficulty.Blue, 0.25f },
                { TrailDifficulty.Black, 0.55f },
                { TrailDifficulty.DoubleBlack, 0.15f }
            };
            
            // Expert: Green 2%, Blue 10%, Black 30%, DoubleBlack 58%
            _preferences[SkillLevel.Expert] = new Dictionary<TrailDifficulty, float>
            {
                { TrailDifficulty.Green, 0.02f },
                { TrailDifficulty.Blue, 0.10f },
                { TrailDifficulty.Black, 0.30f },
                { TrailDifficulty.DoubleBlack, 0.58f }
            };
        }
        
        private void InitializeHardCaps()
        {
            _allowedDifficulties = new Dictionary<SkillLevel, HashSet<TrailDifficulty>>();
            
            // Beginner: Green, Blue only
            _allowedDifficulties[SkillLevel.Beginner] = new HashSet<TrailDifficulty>
            {
                TrailDifficulty.Green,
                TrailDifficulty.Blue
            };
            
            // Intermediate: Green, Blue, Black
            _allowedDifficulties[SkillLevel.Intermediate] = new HashSet<TrailDifficulty>
            {
                TrailDifficulty.Green,
                TrailDifficulty.Blue,
                TrailDifficulty.Black
            };
            
            // Advanced: All
            _allowedDifficulties[SkillLevel.Advanced] = new HashSet<TrailDifficulty>
            {
                TrailDifficulty.Green,
                TrailDifficulty.Blue,
                TrailDifficulty.Black,
                TrailDifficulty.DoubleBlack
            };
            
            // Expert: All
            _allowedDifficulties[SkillLevel.Expert] = new HashSet<TrailDifficulty>
            {
                TrailDifficulty.Green,
                TrailDifficulty.Blue,
                TrailDifficulty.Black,
                TrailDifficulty.DoubleBlack
            };
        }
        
        /// <summary>
        /// Generates a random skill level based on distribution percentages.
        /// </summary>
        public SkillLevel GetRandomSkillLevel(Random random)
        {
            float roll = (float)random.NextDouble();
            
            if (roll < BeginnerPercent)
                return SkillLevel.Beginner;
            else if (roll < BeginnerPercent + IntermediatePercent)
                return SkillLevel.Intermediate;
            else if (roll < BeginnerPercent + IntermediatePercent + AdvancedPercent)
                return SkillLevel.Advanced;
            else
                return SkillLevel.Expert;
        }
        
        /// <summary>
        /// Gets the preference weight for a skill level and trail difficulty.
        /// </summary>
        public float GetPreference(SkillLevel skill, TrailDifficulty difficulty)
        {
            if (_preferences.ContainsKey(skill) && _preferences[skill].ContainsKey(difficulty))
            {
                return _preferences[skill][difficulty];
            }
            return 0f;
        }
        
        /// <summary>
        /// Checks if a skill level is allowed to ski a trail difficulty (hard cap).
        /// </summary>
        public bool IsAllowed(SkillLevel skill, TrailDifficulty difficulty)
        {
            if (_allowedDifficulties.ContainsKey(skill))
            {
                return _allowedDifficulties[skill].Contains(difficulty);
            }
            return false;
        }
        
        /// <summary>
        /// Sets a custom preference weight.
        /// </summary>
        public void SetPreference(SkillLevel skill, TrailDifficulty difficulty, float weight)
        {
            if (!_preferences.ContainsKey(skill))
            {
                _preferences[skill] = new Dictionary<TrailDifficulty, float>();
            }
            _preferences[skill][difficulty] = weight;
        }
    }
}

