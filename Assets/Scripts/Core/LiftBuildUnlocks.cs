namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Which lift types are available in the build dock (1-seat low is always available).
    /// </summary>
    public static class LiftBuildUnlocks
    {
        public static bool IsUnlocked(SimulationState state, LiftType type)
        {
            if (state == null) return type == LiftType.OneSeatLowSpeed;
            switch (type)
            {
                case LiftType.OneSeatLowSpeed:
                    return true;
                case LiftType.OneSeatHighSpeed:
                    return state.UnlockedLiftOneSeatHighSpeed;
                case LiftType.TwoSeatLowSpeed:
                    return state.UnlockedLiftTwoSeatLowSpeed;
                case LiftType.TwoSeatHighSpeed:
                    return state.UnlockedLiftTwoSeatHighSpeed;
                default:
                    return false;
            }
        }

        public static void SetUnlocked(SimulationState state, LiftType type, bool unlocked = true)
        {
            if (state == null) return;
            switch (type)
            {
                case LiftType.OneSeatLowSpeed:
                    return;
                case LiftType.OneSeatHighSpeed:
                    state.UnlockedLiftOneSeatHighSpeed = unlocked;
                    break;
                case LiftType.TwoSeatLowSpeed:
                    state.UnlockedLiftTwoSeatLowSpeed = unlocked;
                    break;
                case LiftType.TwoSeatHighSpeed:
                    state.UnlockedLiftTwoSeatHighSpeed = unlocked;
                    break;
            }
        }
    }
}
