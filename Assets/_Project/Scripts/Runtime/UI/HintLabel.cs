using UnityEngine;

namespace Game.UI
{
    /// <summary>A world-space sign (TextMesh) whose wording follows the device: keyboard on desktop, on-screen buttons on touch.</summary>
    [RequireComponent(typeof(TextMesh))]
    public class HintLabel : MonoBehaviour
    {
        [SerializeField] private string keyboardText = "";
        [SerializeField] private string touchText = "";

        private TextMesh _text;
        private bool? _shownTouch;

        public void Configure(string keyboard, string touch)
        {
            keyboardText = keyboard;
            touchText = touch;
        }

        private void Awake() => _text = GetComponent<TextMesh>();

        private void Update()
        {
            bool touch = InputHints.Touch;
            if (_shownTouch == touch) return;
            _shownTouch = touch;
            _text.text = touch ? touchText : keyboardText;
        }
    }
}
