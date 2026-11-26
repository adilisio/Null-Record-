using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Builds the menu UI in code (no prefabs yet) with one consistent look.
    /// Canvases are Screen Space - Camera so menus render with the game camera.
    /// </summary>
    public static class MenuWidgets
    {
        public static readonly Color Background = new Color(0.035f, 0.04f, 0.05f, 1f);
        public static readonly Color Scrim = new Color(0.02f, 0.025f, 0.03f, 0.82f);
        public static readonly Color Accent = new Color(1f, 0.74f, 0.45f);
        public static readonly Color TextColor = new Color(0.86f, 0.88f, 0.9f);
        public static readonly Color Muted = new Color(0.5f, 0.53f, 0.57f);
        public static readonly Color Danger = new Color(1f, 0.38f, 0.32f);

        private static Font _font;
        public static Font Font => _font != null ? _font : _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static Canvas Canvas(Transform parent, string name, int order, Camera cam)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceCamera;
            c.worldCamera = cam;
            c.planeDistance = 0.5f;
            c.sortingOrder = order;
            var s = go.AddComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1280f, 720f);
            s.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return c;
        }

        /// <summary>Makes sure the scene has an EventSystem driven by the Input System (mouse, keyboard, gamepad).</summary>
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image Panel(Transform parent, string name, Color color)
        {
            RectTransform rt = Rect(parent, name);
            Image img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text Label(Transform parent, string name, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            RectTransform rt = Rect(parent, name);
            Text t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(Transform parent, string name, string text, Action onClick)
        {
            Image bg = Panel(parent, name, new Color(1f, 1f, 1f, 0f));
            var b = bg.gameObject.AddComponent<Button>();
            ColorBlock cb = b.colors;
            cb.normalColor = new Color(1f, 1f, 1f, 0f);
            cb.highlightedColor = new Color(1f, 0.74f, 0.45f, 0.14f);
            cb.selectedColor = new Color(1f, 0.74f, 0.45f, 0.2f);
            cb.pressedColor = new Color(1f, 0.74f, 0.45f, 0.32f);
            cb.disabledColor = new Color(1f, 1f, 1f, 0f);
            cb.fadeDuration = 0.06f;
            b.colors = cb;
            b.targetGraphic = bg;
            Text label = Label(bg.transform, "Label", text, 26, TextColor);
            Stretch(label.rectTransform, new Vector2(18f, 0f), Vector2.zero);
            b.gameObject.AddComponent<MenuButtonStyle>().Init(b, label);
            if (onClick != null) b.onClick.AddListener(() => onClick());
            return b;
        }

        public static Slider Slider(Transform parent, string name, float value, Action<float> onChange)
        {
            RectTransform root = Rect(parent, name);
            Stretch(root, Vector2.zero, Vector2.zero);
            Image track = Panel(root, "Track", new Color(1f, 1f, 1f, 0.12f));
            Anchor(track.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -3f), new Vector2(0f, 3f));
            RectTransform fillArea = Rect(root, "FillArea");
            Anchor(fillArea, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -3f), new Vector2(0f, 3f));
            Image fill = Panel(fillArea, "Fill", Accent);
            Stretch(fill.rectTransform, Vector2.zero, Vector2.zero);
            RectTransform handleArea = Rect(root, "HandleArea");
            Stretch(handleArea, Vector2.zero, Vector2.zero);
            Image handle = Panel(handleArea, "Handle", TextColor);
            handle.rectTransform.sizeDelta = new Vector2(12f, 6f); // the slider stretches it to the row height; this adds a little

            var s = root.gameObject.AddComponent<Slider>();
            s.fillRect = fill.rectTransform;
            s.handleRect = handle.rectTransform;
            s.targetGraphic = handle;
            s.minValue = 0f;
            s.maxValue = 1f;
            s.value = value;
            if (onChange != null) s.onValueChanged.AddListener(v => onChange(v));
            return s;
        }

        public static Toggle Toggle(Transform parent, string name, bool value, Action<bool> onChange)
        {
            RectTransform root = Rect(parent, name);
            Stretch(root, Vector2.zero, Vector2.zero);
            Image box = Panel(root, "Box", new Color(1f, 1f, 1f, 0.12f));
            Anchor(box.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, -12f), new Vector2(24f, 12f));
            Image check = Panel(box.transform, "Check", Accent);
            Stretch(check.rectTransform, new Vector2(5f, 5f), new Vector2(-5f, -5f));
            var t = root.gameObject.AddComponent<Toggle>();
            t.targetGraphic = box;
            t.graphic = check;
            t.isOn = value;
            if (onChange != null) t.onValueChanged.AddListener(v => onChange(v));
            return t;
        }

        public static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
        }

        public static void Stretch(RectTransform rt, Vector2 offMin, Vector2 offMax) => Anchor(rt, Vector2.zero, Vector2.one, offMin, offMax);

        /// <summary>Vertical stack of fixed-height rows, top-down, from a top-left origin.</summary>
        public static void Stack(RectTransform parent, float rowHeight, float spacing)
        {
            var layout = parent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.spacing = spacing;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            foreach (Transform child in parent)
            {
                LayoutElement le = child.gameObject.GetComponent<LayoutElement>();
                if (le == null) le = child.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = rowHeight;
            }
        }

        /// <summary>Wires up/down navigation explicitly so keyboard and gamepad move predictably.</summary>
        public static void ChainVertical(params Selectable[] items)
        {
            var list = new System.Collections.Generic.List<Selectable>();
            foreach (Selectable s in items)
            {
                if (s != null && s.gameObject.activeSelf) list.Add(s);
            }
            for (int i = 0; i < list.Count; i++)
            {
                Navigation n = new Navigation { mode = Navigation.Mode.Explicit };
                n.selectOnUp = list[(i - 1 + list.Count) % list.Count];
                n.selectOnDown = list[(i + 1) % list.Count];
                if (list[i] is Slider)
                {
                    // Left/right adjusts the slider; up/down still moves between rows.
                    n.selectOnLeft = null;
                    n.selectOnRight = null;
                }
                list[i].navigation = n;
            }
        }
    }

    /// <summary>Accent colour and a leading marker on the selected/hovered button; dimmed when disabled.</summary>
    public class MenuButtonStyle : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private Button _button;
        private Text _label;
        private string _text;
        private bool _selected;
        private bool _hovered;
        private bool _wasInteractable = true;

        public void Init(Button b, Text label)
        {
            _button = b;
            _label = label;
            _text = label.text;
        }

        public void SetText(string text)
        {
            _text = text;
            Refresh();
        }

        public void OnSelect(BaseEventData e) { _selected = true; Refresh(); }
        public void OnDeselect(BaseEventData e) { _selected = false; Refresh(); }
        public void OnPointerEnter(PointerEventData e) { _hovered = true; Refresh(); }
        public void OnPointerExit(PointerEventData e) { _hovered = false; Refresh(); }

        private void OnEnable() => Refresh();
        private void Update()
        {
            // Interactable can change from code; keep the dimmed look in sync.
            if (_button != null && _button.interactable != _wasInteractable) Refresh();
        }

        private void Refresh()
        {
            if (_label == null || _button == null) return;
            _wasInteractable = _button.interactable;
            bool on = _button.interactable && (_selected || _hovered);
            _label.text = (on ? "> " : "   ") + _text;
            _label.color = !_button.interactable ? new Color(0.35f, 0.37f, 0.4f) : on ? MenuWidgets.Accent : MenuWidgets.TextColor;
        }
    }
}
