namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Centralized tooltip text strings for the entire UI.
    /// All tooltip text should be defined here for easy maintenance and localization.
    /// </summary>
    public static class TooltipTexts
    {
        // ── Time Controls ────────────────────────────────────────────────────
        public static class TimeControls
        {
            public const string PauseHeader = "Pause";
            public const string PauseContent = "Pause or resume the simulation.\nPress Space to toggle pause.";
            
            public const string Speed1xHeader = "1x Speed";
            public const string Speed1xContent = "Set simulation speed to normal (1x).\nPress 1 to set speed.";
            
            public const string Speed2xHeader = "2x Speed";
            public const string Speed2xContent = "Set simulation speed to 2x.\nPress 2 to set speed.";
            
            public const string Speed3xHeader = "3x Speed";
            public const string Speed3xContent = "Set simulation speed to 3x.\nPress 3 to set speed.";
        }
        
        // ── Menu Buttons ─────────────────────────────────────────────────────
        public static class Menu
        {
            public const string MenuHeader = "Menu";
            public const string MenuContent = "Open the main menu.\nPress ESC to toggle menu.";
            
            public const string ResumeHeader = "Resume";
            public const string ResumeContent = "Close the menu and resume playing.";
            
            public const string QuitHeader = "Quit";
            public const string QuitContent = "Exit the game.";
            
            public const string ManagerHeader = "Manager";
            public const string ManagerContent = "Open the resort manager screen.";
        }
        
        // ── Structure Details Panel ──────────────────────────────────────────
        public static class StructureDetails
        {
            public const string CloseHeader = "Close";
            public const string CloseContent = "Close this details panel.";
            
            public const string RenameHeader = "Rename";
            public const string RenameContent = "Rename this structure.\n(Coming soon)";
            
            public const string DeleteHeader = "Delete";
            public const string DeleteContent = "Permanently delete this structure.";
            
            public const string UpgradeHeader = "Upgrade";
            public const string UpgradeContent = "Upgrade this structure.\n(Coming soon)";
        }
        
        // ── Dock Controller ──────────────────────────────────────────────────
        public static class Dock
        {
            public const string CloseHeader = "Close";
            public const string CloseContent = "Close the dock panel.";
            
            public const string PaintModeHeader = "Paint Mode";
            public const string PaintModeContent = "Draw trails by painting with your mouse. Great for freeform trails.";
            
            public const string LineModeHeader = "Line Mode";
            public const string LineModeContent = "Draw straight line trails between two points.";
            
            public const string PenModeHeader = "Pen Mode";
            public const string PenModeContent = "Draw smooth curved trails. Click to place points, right-click to finish.";
            
            public const string TrailWidthHeader = "Trail Width";
            public const string TrailWidthContent = "Adjust the width of trails you're drawing.";

            public const string OneSeatLowSpeedHeader = "1-Seat Low Speed";
            public const string OneSeatLowSpeedContent = "Build a 1-seat low-speed lift.\nLowest capacity option.";

            public const string OneSeatHighSpeedHeader = "1-Seat High Speed";
            public const string OneSeatHighSpeedContent = "Build a 1-seat high-speed lift.\n1.5x chair speed and higher capacity than low-speed.";

            public const string TwoSeatLowSpeedHeader = "2-Seat Low Speed";
            public const string TwoSeatLowSpeedContent = "Build a 2-seat low-speed lift.\nNot implemented yet.";

            public const string TwoSeatHighSpeedHeader = "2-Seat High Speed";
            public const string TwoSeatHighSpeedContent = "Build a 2-seat high-speed lift.\nNot implemented yet.";
            
            public static string GetCategoryContent(string categoryName)
            {
                // Clean up category name (remove "SubRow" suffix if present)
                string cleanName = categoryName;
                if (cleanName.EndsWith("SubRow", System.StringComparison.OrdinalIgnoreCase))
                {
                    cleanName = cleanName.Substring(0, cleanName.Length - 6);
                }
                
                // Capitalize first letter
                if (cleanName.Length > 0)
                {
                    cleanName = char.ToUpper(cleanName[0]) + cleanName.Substring(1).ToLower();
                }
                
                return $"Open to see {cleanName} options.";
            }
        }
        
        // ── Context Window ───────────────────────────────────────────────────
        public static class ContextWindow
        {
            public const string CloseHeader = "Close";
            public const string CloseContent = "Close this context window.";
            
            public const string TrailStatusHeader = "Trail Status";
            public const string TrailStatusContent = "Toggle whether this trail is open or closed to skiers.";
            
            public const string LiftStatusHeader = "Lift Status";
            public const string LiftStatusContent = "Toggle whether this lift is open or closed.";
            
            public const string LodgeStatusHeader = "Lodge Status";
            public const string LodgeStatusContent = "Toggle whether this lodge is open or closed.";
            
            // Action Buttons
            public const string DemolishHeader = "Demolish";
            public const string DemolishContent = "Permanently delete this structure.";
            
            public const string FindHeader = "Find";
            public const string FindContent = "Center the camera on this structure.";
            
            public const string FollowHeader = "Follow";
            public const string FollowContent = "Enter first-person view and see through this skier's eyes.\nExit with Escape or WASD.";
            
            // Build Buttons
            public const string ConfirmHeader = "Confirm";
            public const string ConfirmContent = "Confirm and build this structure.";
            
            public const string CancelHeader = "Cancel";
            public const string CancelContent = "Cancel building this structure.";
            
            // Difficulty Picker Main Button (dynamic - shows current difficulty)
            public static string GetDifficultyPickerHeader(string difficultyName) => $"Trail Difficulty: {difficultyName}";
            public static string GetDifficultyPickerContent(string difficultyName) => $"This trail is designated as {difficultyName}.\nClick to change difficulty (may affect guest satisfaction).";
            
            // Difficulty Buttons
            public const string GreenDifficultyHeader = "Green Circle";
            public const string GreenDifficultyContent = "Set trail difficulty to Green Circle (easiest).";
            
            public const string BlueDifficultyHeader = "Blue Square";
            public const string BlueDifficultyContent = "Set trail difficulty to Blue Square (intermediate).";
            
            public const string BlackDifficultyHeader = "Black Diamond";
            public const string BlackDifficultyContent = "Set trail difficulty to Black Diamond (advanced).";
            
            public const string DoubleBlackDifficultyHeader = "Double Black";
            public const string DoubleBlackDifficultyContent = "Set trail difficulty to Double Black (expert).";
            
            // Buffet Buttons
            public const string BuffetDecrementHeader = "Decrease Buffet";
            public const string BuffetDecrementContent = "Decrease buffet capacity.";
            
            public const string BuffetIncrementHeader = "Increase Buffet";
            public const string BuffetIncrementContent = "Increase buffet capacity.";
        }
        
        // ── Manager Tabs ─────────────────────────────────────────────────────
        public static class ManagerTabs
        {
            public const string CloseHeader = "Close Manager";
            public const string CloseContent = "Close the manager screen.";
            
            public const string OverviewHeader = "Overview";
            public const string OverviewContent = "View overall resort statistics and performance.";
            
            public const string FinancesHeader = "Finances";
            public const string FinancesContent = "Track income, expenses, and financial trends.";
            
            public const string PricingHeader = "Pricing";
            public const string PricingContent = "Manage pricing for lodge amenities.";
            
            public const string GuestsHeader = "Guests";
            public const string GuestsContent = "Monitor guest satisfaction and demographics.";
        }
        
        // ── Global Stats ─────────────────────────────────────────────────────
        public static class Stats
        {
            public const string DayHeader = "Day";
            public const string DayContent = "Current day of the season.";
            
            public const string TimeHeader = "Time";
            public const string TimeContent = "Current time of day.";
            
            public const string MoneyHeader = "Money";
            public const string MoneyContent = "Your current cash balance. Earn money by building lifts and lodges.";
            
            public const string VisitorsHeader = "Visitors Today";
            public const string VisitorsContent = "Number of skiers who visited your resort today.";
            
            public const string TrailsHeader = "Trails";
            public const string TrailsContent = "Total number of ski trails you've built.";
            
            public const string LiftsHeader = "Lifts";
            public const string LiftsContent = "Total number of chairlifts you've built.";
            
            public const string LodgesHeader = "Lodges";
            public const string LodgesContent = "Total number of lodges you've built.";
            
            public const string SatisfactionHeader = "Satisfaction";
            public const string SatisfactionContent = "Overall guest satisfaction percentage. Higher satisfaction attracts more visitors.";
        }
        
        // ── Build Action Bar ──────────────────────────────────────────────────
        public static class BuildActionBar
        {
            public static string GetTabContent(string tabName) => $"Switch to {tabName} tab";
        }
    }
}
