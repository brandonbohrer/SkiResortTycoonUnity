namespace SkiResortTycoon.Saving
{
    /// <summary>
    /// Passes "load this save" from Main Menu to the game scene.
    /// Main menu sets PendingSavePath and loads the game scene; game scene reads and clears it.
    /// </summary>
    public static class GameLoadBootstrap
    {
        /// <summary>
        /// Full path to the save file to load when the game scene starts. Null = new game.
        /// Set from main menu when user picks "Load" on a save slot; cleared after apply.
        /// </summary>
        public static string PendingSavePath { get; set; }
    }
}
