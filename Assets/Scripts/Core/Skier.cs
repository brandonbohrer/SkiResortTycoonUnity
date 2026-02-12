namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Skill levels for visitors.
    /// Distribution: Beginner 20%, Intermediate 30%, Advanced 30%, Expert 20%
    /// </summary>
    public enum SkillLevel
    {
        Beginner = 0,
        Intermediate = 1,
        Advanced = 2,
        Expert = 3
    }
    
    /// <summary>
    /// State of a skier during real-time simulation.
    /// </summary>
    public enum SkierState
    {
        AtBase = 0,
        WalkingToLift = 1,
        InQueue = 2,
        RidingLift = 3,
        SkiingTrail = 4,
        AtAmenity = 5
    }
    
    /// <summary>
    /// Represents a single visitor/skier.
    /// Pure C# data structure - no Unity types.
    /// </summary>
    public class Skier
    {
        public int SkierId { get; set; }
        public SkillLevel Skill { get; set; }
        public int RunsCompleted { get; set; }
        public bool WasServed { get; set; }  // Did they get to ski at least once?
        
        // Real-time state tracking
        public SkierState CurrentState { get; set; }
        public int CurrentLiftId { get; set; }
        public int CurrentTrailId { get; set; }
        public float PathProgress { get; set; } // 0-1 along current segment
        
        // Skier Intelligence System
        public SkierNeeds Needs { get; private set; }
        public SkierSatisfaction SatisfactionTracker { get; private set; }
        public SkierGoal CurrentGoal { get; set; }
        public float TimeOnMountain { get; set; }  // Minutes since arrival
        public int DesiredRuns { get; set; }       // How many runs they want to complete today
        public int PreferredRunsCompleted { get; set; } // Runs on preferred difficulty
        
        public Skier(int id, SkillLevel skill)
        {
            SkierId = id;
            Skill = skill;
            RunsCompleted = 0;
            WasServed = false;
            CurrentState = SkierState.AtBase;
            CurrentLiftId = -1;
            CurrentTrailId = -1;
            PathProgress = 0f;
            
            // Initialize intelligence systems
            Needs = new SkierNeeds();
            SatisfactionTracker = new SkierSatisfaction();
            CurrentGoal = new SkierGoal();
            TimeOnMountain = 0f;
            DesiredRuns = GetRandomDesiredRuns(skill);
            PreferredRunsCompleted = 0;
        }
        
        /// <summary>
        /// Gets the skier's current individual satisfaction (0-1).
        /// Uses the modular factor system if factors are registered,
        /// falls back to raw Needs.Satisfaction otherwise.
        /// </summary>
        public float GetSatisfaction()
        {
            return SatisfactionTracker.Calculate(Needs);
        }
        
        /// <summary>
        /// Checks if the skier wants to keep skiing or is ready to leave.
        /// </summary>
        public bool WantsToKeepSkiing()
        {
            // Leave if exhausted, or completed desired runs
            if (Needs.Fatigue >= 0.9f) return false;
            if (RunsCompleted >= DesiredRuns) return false;
            if (Needs.Satisfaction <= 0.2f) return false; // Too unhappy, leaving
            return true;
        }
        
        /// <summary>
        /// Gets a random number of desired runs based on skill level.
        /// Experts tend to want more runs.
        /// </summary>
        private static int GetRandomDesiredRuns(SkillLevel skill)
        {
            // Base runs plus skill modifier
            int baseRuns = 5;
            int skillBonus = (int)skill * 2; // 0, 2, 4, 6
            
            // Add some randomness (+/- 3 runs)
            int variance = new System.Random().Next(-3, 4);
            
            return System.Math.Max(3, baseRuns + skillBonus + variance);
        }
    }
}
