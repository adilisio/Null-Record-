using System;
using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Settings stub shared by the title and pause menus: volume, fullscreen, debug HUD.</summary>
    public sealed class SettingsPanel
    {
        public RectTransform Root { get; }
        public Slider Volume { get; }
        public Toggle Fullscreen { get; }
        public Toggle DebugHud { get; }
        public Button Back { get; }

        public SettingsPanel(Transform parent, Action onBack)
        {
            Root = MenuWidgets.Rect(parent, "Settings");
            // Same area as the main menu column, below the title.
            MenuWidgets.Anchor(Root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(106f, 80f), new Vector2(640f, 400f));

            Text title = MenuWidgets.Label(Root, "Title", "SETTINGS", 30, MenuWidgets.Accent);
            MenuWidgets.Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -40f), new Vector2(0f, 0f));

            Volume = MenuWidgets.Slider(Row(Root, "Master volume", 0), "Slider", GameSettings.MasterVolume, v => GameSettings.MasterVolume = v);
            Fullscreen = MenuWidgets.Toggle(Row(Root, "Fullscreen", 1), "Toggle", GameSettings.Fullscreen, v => GameSettings.Fullscreen = v);
            DebugHud = MenuWidgets.Toggle(Row(Root, "Debug HUD", 2), "Toggle", GameSettings.DebugHud, v => GameSettings.DebugHud = v);

            Back = MenuWidgets.Button(Root, "Back", "Back", () =>
            {
                GameSettings.Save();
                onBack?.Invoke();
            });
            MenuWidgets.Anchor((RectTransform)Back.transform, new Vector2(0f, 1f), new Vector2(0.6f, 1f), new Vector2(0f, -290f), new Vector2(0f, -244f));

            MenuWidgets.ChainVertical(Volume, Fullscreen, DebugHud, Back);
            Root.gameObject.SetActive(false);
        }

        public Selectable First => Volume;

        private static Transform Row(Transform parent, string label, int index)
        {
            float top = -64f - index * 56f;
            RectTransform row = MenuWidgets.Rect(parent, label);
            MenuWidgets.Anchor(row, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, top - 44f), new Vector2(0f, top));
            Text t = MenuWidgets.Label(row, "Label", label, 22, MenuWidgets.TextColor);
            MenuWidgets.Anchor(t.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(18f, 0f), Vector2.zero);
            RectTransform control = MenuWidgets.Rect(row, "Control");
            MenuWidgets.Anchor(control, new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(10f, 8f), new Vector2(-10f, -8f));
            return control;
        }
    }
}
