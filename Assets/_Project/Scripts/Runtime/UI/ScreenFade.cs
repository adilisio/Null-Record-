using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// A full-screen black overlay that fades out, to soften hard cuts (lift rides, respawns).
    /// The caller moves things first and then calls <see cref="FromBlack"/>, so gameplay timing is unchanged.
    /// </summary>
    public class ScreenFade : MonoBehaviour
    {
        private static ScreenFade _instance;
        private Image _image;
        private Coroutine _running;

        /// <summary>Current overlay alpha (tests).</summary>
        public static float Alpha => _instance != null && _instance._image != null ? _instance._image.color.a : 0f;

        public static void FromBlack(float seconds = 0.4f)
        {
            if (_instance == null)
            {
                var go = new GameObject("ScreenFade");
                _instance = go.AddComponent<ScreenFade>();
            }
            _instance.Begin(seconds);
        }

        private void Build()
        {
            Canvas canvas = MenuWidgets.Canvas(transform, "FadeCanvas", 95, Camera.main);
            _image = MenuWidgets.Panel(canvas.transform, "Black", Color.black);
            _image.raycastTarget = false;
            MenuWidgets.Stretch(_image.rectTransform, Vector2.zero, Vector2.zero);
        }

        private void Begin(float seconds)
        {
            if (_image == null) Build();
            Canvas c = _image.canvas;
            if (c.worldCamera == null) c.worldCamera = Camera.main;
            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(Fade(seconds));
        }

        private IEnumerator Fade(float seconds)
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds))
            {
                _image.color = new Color(0f, 0f, 0f, 1f - t * t);
                yield return null;
            }
            _image.color = new Color(0f, 0f, 0f, 0f);
            _running = null;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
