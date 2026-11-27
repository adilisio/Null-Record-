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

        /// <summary>The Attack control: "J or click" or the ATTACK button.</summary>
        public static string Attack => Touch ? "ATTACK" : "J / click";

        public static string DialogueChoose => Touch ? "Stick choose   NEXT select" : "W/S choose   Space select";
        public static string DialogueContinue => Touch ? "Tap or NEXT to continue" : "Space continue";
    }
}
