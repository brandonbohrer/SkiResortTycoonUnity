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
    /// Represents a single visitor/skier.
    /// Pure C# data structure - no Unity types.
    /// </summary>
    public class Skier
    {
        public int SkierId { get; set; }
        public SkillLevel Skill { get; set; }
        public int RunsCompleted { get; set; }
        public bool WasServed { get; set; }  // Did they get to ski at least once?
        
        public Skier(int id, SkillLevel skill)
        {
            SkierId = id;
            Skill = skill;
            RunsCompleted = 0;
            WasServed = false;
        }
    }
}

