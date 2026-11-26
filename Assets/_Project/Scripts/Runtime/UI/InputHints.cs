namespace Game.UI
{
    /// <summary>
    /// Control hints that match the device: keyboard names on desktop, on-screen button names on touch.
    /// Keyed on <see cref="TouchControls.ShouldShow"/>, so it flips once the screen is touched.
    /// </summary>
    public static class InputHints
    {
        public static bool Touch => TouchControls.ShouldShow;

        /// <summary>The Interact control: "E" or the USE button.</summary>
        public static string Interact => Touch ? "USE" : "E";

        public static string CombatControls => Touch
            ? "Tap a tile, tap it again to confirm   BACK cancel   UNIT switch character   END TURN when done"
            : "WASD cursor   Space confirm   1/2 or click: action   C cancel   Shift switch character   E end turn";

        public static string DialogueChoose => Touch ? "Stick choose   NEXT select" : "W/S choose   Space select";
        public static string DialogueContinue => Touch ? "Tap or NEXT to continue" : "Space continue";
    }
}
