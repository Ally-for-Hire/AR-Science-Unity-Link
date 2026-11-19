using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ARScience
{
    /// <summary>
    /// Tiny helpers for building uGUI from code (no scene or prefab editing needed).
    /// Positions use anchors: (0,0) is bottom-left of the parent, (1,1) is top-right.
    /// </summary>
    public class UiBuilder
    {
        public static readonly Color Background = new Color(0.07f, 0.09f, 0.13f, 1f);
        public static readonly Color ButtonColor = new Color(0.16f, 0.36f, 0.75f, 1f);
        public static readonly Color MutedColor = new Color(0.3f, 0.3f, 0.34f, 1f);

        readonly Font m_Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        /// <summary>Creates the root canvas and a child area that avoids notches / rounded corners.</summary>
        public RectTransform CreateSafeAreaRoot(Transform parent)
        {
            var canvasObject = new GameObject("Canvas");
            canvasObject.transform.SetParent(parent, false);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // Draw above anything else.

            // Scale the layout so it looks the same on any screen size.
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>(); // Lets buttons receive taps.

            var safeArea = NewRect("SafeArea", canvasObject.transform);
            var area = Screen.safeArea;
            safeArea.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
            return safeArea;
        }

        /// <summary>
        /// A full-size container. If not `opaque` it is invisible AND ignores taps,
        /// so touches pass through to whatever is behind it (here: the AR view).
        /// </summary>
        public RectTransform Panel(string name, Transform parent, bool opaque) =>
            Box(name, parent, Vector2.zero, Vector2.one, opaque);

        /// <summary>
        /// A rectangle given by its corners, as fractions of the parent:
        /// Box("Card", screen, new Vector2(0.05f, 0.2f), new Vector2(0.95f, 0.5f), true)
        /// covers the middle 90% of the width, from 20% to 50% of the height.
        /// Same `opaque` rule as Panel.
        /// </summary>
        public RectTransform Box(string name, Transform parent, Vector2 bottomLeft, Vector2 topRight, bool opaque)
        {
            var box = NewRect(name, parent);
            box.anchorMin = bottomLeft;
            box.anchorMax = topRight;
            box.offsetMin = box.offsetMax = Vector2.zero;

            var image = box.gameObject.AddComponent<Image>();
            image.color = opaque ? Background : Color.clear;
            image.raycastTarget = opaque;
            return box;
        }

        /// <summary>True if a screen position is on top of a button or opaque panel (so it isn't meant for the AR view).</summary>
        public static bool IsOverUi(Vector2 screenPosition)
        {
            var events = EventSystem.current;
            if (events == null) return false;
            s_UiHits.Clear();
            events.RaycastAll(new PointerEventData(events) { position = screenPosition }, s_UiHits);
            return s_UiHits.Count > 0;
        }
        static readonly List<RaycastResult> s_UiHits = new List<RaycastResult>();

        /// <summary>Centered text at an anchor position.</summary>
        public Text Label(Transform parent, string text, int fontSize, Vector2 anchor, Vector2 size)
        {
            var rect = NewRect("Label", parent);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;

            var label = rect.gameObject.AddComponent<Text>();
            label.font = m_Font;
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.supportRichText = true;     // Allows <size=..> in text.
            label.raycastTarget = false;      // Text should never block taps.
            return label;
        }

        /// <summary>A tappable button with a text label. onClick may be null.</summary>
        public Button Button(Transform parent, string text, Vector2 anchor, UnityAction onClick,
            float width = 700, float height = 150)
        {
            var rect = NewRect("Button", parent);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = new Vector2(width, height);

            var image = rect.gameObject.AddComponent<Image>();
            image.color = ButtonColor;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null) button.onClick.AddListener(onClick);

            // Label fills the whole button.
            var label = Label(rect, text, 46, new Vector2(0.5f, 0.5f), Vector2.zero);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        /// <summary>Greys a button out and stops it reacting to taps.</summary>
        public static void Disable(Button button)
        {
            button.interactable = false;
            button.image.color = MutedColor;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
    }
}
