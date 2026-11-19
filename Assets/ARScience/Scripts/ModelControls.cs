using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace ARScience
{
    /// <summary>
    /// Makes a placed "explore" model interactive. Added automatically by ARScenarioController.
    ///   Phone:  one-finger drag = turn,  pinch = zoom,  tap = select a part.
    ///   Editor: left-drag = turn,  mouse wheel = zoom,  click = select a part.
    /// Which children are parts, the zoom limits, and when parts show/hide all come from the module JSON.
    /// </summary>
    public class ModelControls : MonoBehaviour
    {
        [Tooltip("How fast dragging turns the model.")]
        public float degreesPerPixel = 0.3f;
        [Tooltip("How much one mouse-wheel notch zooms, 1.1 = 10% (editor only).")]
        public float wheelZoomStep = 1.1f;

        // A press counts as a tap if it is short and barely moves (about 4 mm, so it feels the same on any phone).
        const float TapMaxSeconds = 0.35f;
        static float TapMaxPixels => Mathf.Max(25f, Screen.dpi * 0.15f);
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        /// <summary>Raised when the student taps a part.</summary>
        public event Action<PartDefinition> PartTapped;

        /// <summary>Raised when zooming hides the part that was selected, so its info can close.</summary>
        public event Action SelectionHidden;

        /// <summary>Current zoom. 1 = the size it was placed at.</summary>
        public float Zoom { get; private set; } = 1f;

        ModelDefinition m_Model;
        Vector3 m_PlacedScale;
        readonly List<(PartDefinition part, GameObject obj)> m_Parts = new List<(PartDefinition, GameObject)>();
        readonly List<Renderer> m_Highlighted = new List<Renderer>();
        GameObject m_Selected; // The last tapped part, or null.

        // The press currently in progress (finger or mouse).
        bool m_Pressing, m_Moved;
        Vector2 m_PressStart;
        float m_PressTime;

        /// <summary>Connects this object to its description in the module. Call once, right after placing.</summary>
        public void Setup(ModelDefinition model)
        {
            m_Model = model;
            m_PlacedScale = transform.localScale;

            // Find every child whose name matches a part id in the JSON.
            foreach (var child in GetComponentsInChildren<Transform>(true))
            {
                var part = ModelRules.FindPart(model, child.name);
                if (part == null) continue;
                m_Parts.Add((part, child.gameObject));
                AddCollidersIfMissing(child.gameObject);
            }
            SetZoom(1f);
        }

        /// <summary>Changes the size, staying inside the module's limits, and shows/hides parts to match.</summary>
        public void SetZoom(float zoom)
        {
            Zoom = ModelRules.ClampZoom(zoom, m_Model);
            transform.localScale = m_PlacedScale * Zoom;
            foreach (var (part, obj) in m_Parts) obj.SetActive(ModelRules.IsVisibleAt(part, Zoom));

            // Zoomed so far that the selected part disappeared: deselect it.
            if (m_Selected != null && !m_Selected.activeInHierarchy)
            {
                ClearHighlight();
                SelectionHidden?.Invoke();
            }
        }

        /// <summary>Removes the highlight from the last tapped part.</summary>
        public void ClearHighlight()
        {
            foreach (var r in m_Highlighted) if (r != null) r.SetPropertyBlock(null);
            m_Highlighted.Clear();
            m_Selected = null;
        }

        // ------------------------------------------------------------ input

        void OnEnable() => EnhancedTouchSupport.Enable();
        void OnDisable() => EnhancedTouchSupport.Disable();

        void Update()
        {
            var touches = Touch.activeTouches;
            if (touches.Count >= 2) { Pinch(touches[0], touches[1]); return; }
            if (touches.Count == 1)
            {
                var t = touches[0];
                Pointer(t.screenPosition, t.delta, t.phase == TouchPhase.Began,
                    t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled);
                return;
            }

            // No fingers on screen: use the mouse (Unity editor / XR Simulation).
            var mouse = Mouse.current;
            if (mouse == null) return;
            var position = mouse.position.ReadValue();
            float wheel = mouse.scroll.ReadValue().y;
            // Only the direction is used: wheel step sizes differ between computers.
            if (wheel != 0 && !UiBuilder.IsOverUi(position)) SetZoom(wheel > 0 ? Zoom * wheelZoomStep : Zoom / wheelZoomStep);
            if (mouse.leftButton.isPressed || mouse.leftButton.wasReleasedThisFrame)
                Pointer(position, mouse.delta.ReadValue(), mouse.leftButton.wasPressedThisFrame, mouse.leftButton.wasReleasedThisFrame);
        }

        /// <summary>One finger (or the mouse): drag to turn, or tap to select.</summary>
        void Pointer(Vector2 position, Vector2 delta, bool began, bool ended)
        {
            if (began)
            {
                m_Pressing = !UiBuilder.IsOverUi(position); // Presses that start on a button are not for us.
                m_Moved = false;
                m_PressStart = position;
                m_PressTime = Time.unscaledTime;
            }
            if (!m_Pressing) return;

            if ((position - m_PressStart).magnitude > TapMaxPixels) m_Moved = true;
            if (m_Moved) transform.Rotate(0f, -delta.x * degreesPerPixel, 0f, Space.World); // Turn around "up".

            if (ended)
            {
                if (!m_Moved && Time.unscaledTime - m_PressTime <= TapMaxSeconds) Tap(position);
                m_Pressing = false;
            }
        }

        /// <summary>Two fingers: zoom by how much they moved apart or together.</summary>
        void Pinch(Touch a, Touch b)
        {
            m_Moved = true; // A pinch is never a tap.
            if (UiBuilder.IsOverUi(a.startScreenPosition) || UiBuilder.IsOverUi(b.startScreenPosition)) return; // Fingers on a button.
            float now = Vector2.Distance(a.screenPosition, b.screenPosition);
            float before = Vector2.Distance(a.screenPosition - a.delta, b.screenPosition - b.delta);
            if (before > 1f) SetZoom(Zoom * now / before);
        }

        /// <summary>Finds which part is under the tap, highlights it and tells listeners.</summary>
        void Tap(Vector2 position)
        {
            var camera = Camera.main;
            if (camera == null || !Physics.Raycast(camera.ScreenPointToRay(position), out var hit, 100f)) return;
            var hitObject = hit.collider.transform; // (hit.transform would give the Rigidbody's object instead.)
            if (!hitObject.IsChildOf(transform)) return; // Hit something else, like the floor.

            // Walk up from what was hit until we reach a part (parts can contain smaller meshes).
            for (var t = hitObject; t != null && t != transform.parent; t = t.parent)
            {
                foreach (var (part, obj) in m_Parts)
                {
                    if (obj != t.gameObject) continue;
                    Highlight(obj);
                    PartTapped?.Invoke(part);
                    return;
                }
            }
        }

        // ------------------------------------------------------------ helpers

        /// <summary>Tints a part toward yellow without changing its material asset.</summary>
        void Highlight(GameObject obj)
        {
            ClearHighlight();
            m_Selected = obj;
            foreach (var r in obj.GetComponentsInChildren<Renderer>())
            {
                var material = r.sharedMaterial;
                if (material == null || !material.HasProperty(BaseColor)) continue;
                var block = new MaterialPropertyBlock();
                block.SetColor(BaseColor, Color.Lerp(material.GetColor(BaseColor), Color.yellow, 0.5f));
                r.SetPropertyBlock(block);
                m_Highlighted.Add(r);
            }
        }

        /// <summary>
        /// Taps need colliders. If a part has none, give each of its meshes a box that fits it.
        /// (Boxes work for every mesh. For exact shapes, add your own colliders in the prefab.)
        /// </summary>
        static void AddCollidersIfMissing(GameObject part)
        {
            if (part.GetComponentInChildren<Collider>(true) != null) return;
            foreach (var filter in part.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var box = filter.gameObject.AddComponent<BoxCollider>();
                box.center = filter.sharedMesh.bounds.center;
                box.size = filter.sharedMesh.bounds.size;
            }
        }
    }
}
