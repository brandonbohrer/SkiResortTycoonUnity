namespace SkiResortTycoon.Core
{
    /// <summary>
    /// Resolves in-progress lift research when the calendar day advances (after <see cref="Simulation.EndDay"/>).
    /// </summary>
    public static class LiftResearchProgress
    {
        public static void ProcessNewDay(SimulationState state)
        {
            if (state == null) return;
            TryCompleteSlot(state, 0);
            TryCompleteSlot(state, 1);
            TryCompleteSlot(state, 2);
        }

        private static void TryCompleteSlot(SimulationState state, int slot)
        {
            int completionDay = state.GetLiftResearchCompletionDay(slot);
            if (completionDay < 0 || state.DayIndex < completionDay)
                return;

            int pendingUnlock = state.GetLiftResearchPendingUnlockType(slot);
            if (pendingUnlock >= 0 && pendingUnlock <= (int)LiftType.TwoSeatHighSpeed)
                LiftBuildUnlocks.SetUnlocked(state, (LiftType)pendingUnlock, true);

            state.ClearLiftResearchInProgress(slot);
            state.SetLiftResearchSlotDone(slot, true);
        }
    }
}
