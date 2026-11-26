using System;

namespace Game.Core
{
    /// <summary>Short on-screen notifications ("Acquired Level 2 keycard"). The HUD listens.</summary>
    public static class GameMessages
    {
        public static event Action<string> Posted;

        public static void Show(string message) => Posted?.Invoke(message);
    }
}
