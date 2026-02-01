using System.Collections.Generic;

namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Types of goals a skier can pursue.
    /// </summary>
    public enum GoalType
    {
        None = 0,               // No active goal
        SkiPreferredTrail,      // Find and ski a trail matching skill preference
        SkiSpecificTrail,       // Ski a specific trail (en-route to destination)
        RideLift,               // Board and ride a specific lift
        WaitInQueue,            // Waiting in lift queue
        ReturnToBase,           // Head back to base area
        FindFood,               // Navigate to restaurant (future)
        FindBathroom,           // Navigate to bathroom (future)
        LeaveResort             // Done for the day, exiting
    }
    
    /// <summary>
    /// Represents a skier's current goal and the plan to achieve it.
    /// </summary>
    public class SkierGoal
    {
        /// <summary>
        /// The type of goal being pursued.
        /// </summary>
        public GoalType Type { get; set; }
        
        /// <summary>
        /// Target entity ID (TrailId, LiftId, AmenityId depending on goal type).
        /// </summary>
        public int TargetId { get; set; }
        
        /// <summary>
        /// The ultimate destination trail the skier wants to reach.
        /// This is the "dream run" they're working toward.
        /// </summary>
        public int DestinationTrailId { get; set; }
        
        /// <summary>
        /// Planned sequence of steps to reach the destination.
        /// Each step is a (type, id) pair where type is "lift" or "trail".
        /// </summary>
        public List<PathStep> PlannedPath { get; private set; }
        
        /// <summary>
        /// Current position in the planned path (0-based index).
        /// </summary>
        public int CurrentPathIndex { get; set; }
        
        /// <summary>
        /// Priority/urgency of this goal (higher = more important).
        /// Used when deciding whether to interrupt current goal.
        /// </summary>
        public float Priority { get; set; }
        
        /// <summary>
        /// Has this goal been completed?
        /// </summary>
        public bool IsComplete { get; set; }
        
        public SkierGoal()
        {
            Type = GoalType.None;
            TargetId = -1;
            DestinationTrailId = -1;
            PlannedPath = new List<PathStep>();
            CurrentPathIndex = 0;
            Priority = 1f;
            IsComplete = false;
        }
        
        /// <summary>
        /// Creates a goal to ski a specific preferred trail, with a path to get there.
        /// </summary>
        public static SkierGoal CreateSkiGoal(int destinationTrailId, List<PathStep> path)
        {
            var goal = new SkierGoal
            {
                Type = GoalType.SkiPreferredTrail,
                DestinationTrailId = destinationTrailId,
                Priority = 1f
            };
            goal.PlannedPath.AddRange(path);
            
            // Set initial target based on first step
            if (path.Count > 0)
            {
                goal.TargetId = path[0].EntityId;
                goal.Type = path[0].StepType == PathStepType.RideLift ? GoalType.RideLift : GoalType.SkiSpecificTrail;
            }
            
            return goal;
        }
        
        /// <summary>
        /// Advances to the next step in the planned path.
        /// Returns true if there are more steps, false if goal is complete.
        /// </summary>
        public bool AdvanceToNextStep()
        {
            CurrentPathIndex++;
            
            if (CurrentPathIndex >= PlannedPath.Count)
            {
                IsComplete = true;
                Type = GoalType.None;
                return false;
            }
            
            var nextStep = PlannedPath[CurrentPathIndex];
            TargetId = nextStep.EntityId;
            Type = nextStep.StepType == PathStepType.RideLift ? GoalType.RideLift : GoalType.SkiSpecificTrail;
            
            return true;
        }
        
        /// <summary>
        /// Gets the current step in the path, or null if no steps remain.
        /// </summary>
        public PathStep GetCurrentStep()
        {
            if (CurrentPathIndex < PlannedPath.Count)
            {
                return PlannedPath[CurrentPathIndex];
            }
            return null;
        }
        
        /// <summary>
        /// Clears the goal and path.
        /// </summary>
        public void Clear()
        {
            Type = GoalType.None;
            TargetId = -1;
            DestinationTrailId = -1;
            PlannedPath.Clear();
            CurrentPathIndex = 0;
            Priority = 1f;
            IsComplete = false;
        }
    }
    
    /// <summary>
    /// Types of steps in a path.
    /// </summary>
    public enum PathStepType
    {
        RideLift,
        SkiTrail
    }
    
    /// <summary>
    /// A single step in a skier's planned path.
    /// </summary>
    public class PathStep
    {
        public PathStepType StepType { get; set; }
        public int EntityId { get; set; }  // LiftId or TrailId
        public string EntityName { get; set; }
        
        public PathStep(PathStepType type, int id, string name = null)
        {
            StepType = type;
            EntityId = id;
            EntityName = name ?? $"{type} {id}";
        }
        
        public override string ToString()
        {
            return $"{StepType}:{EntityName}";
        }
    }
}
