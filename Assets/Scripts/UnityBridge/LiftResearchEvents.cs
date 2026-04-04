namespace SkiResortTycoon.UnityBridge
{
    /// <summary>Fired when lift research or unlock state changes (dock + research UI refresh).</summary>
    public static class LiftResearchEvents
    {
        public static event System.Action Changed;

        public static void Raise() => Changed?.Invoke();
    }
}
