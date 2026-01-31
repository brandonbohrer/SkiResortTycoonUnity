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
        }
    }
}
