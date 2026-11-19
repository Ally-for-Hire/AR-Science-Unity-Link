using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Interaction.Toolkit.Samples.ARStarterAssets;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

namespace ARScience
{
    /// <summary>
    /// Runs a module in AR. The template already handles tapping and finding surfaces; this class
    /// decides WHICH object gets placed and what happens once it is placed.
    ///   "placement" modules: place each step's items in order (progress kept by ScenarioRunner).
    ///   "explore" modules:   place one model, then turn / zoom / tap its parts (ModelControls).
    /// The screen reads PromptText, StatusText, IsComplete and CanSwitchModel, so it never needs
    /// to know which kind of module is running.
    /// </summary>
    public class ARScenarioController : MonoBehaviour
    {
        // Template parts we drive. Found automatically if left empty.
        public ObjectSpawner spawner;               // Spawns the prefab when the user taps.
        public ARInteractorSpawnTrigger trigger;    // Turns taps into spawn requests.
        public ARPlaneManager planeManager;         // Detects and draws surfaces.

        /// <summary>Raised whenever progress changes, so the UI can redraw.</summary>
        public event Action Progress;

        /// <summary>Raised when the student taps a part of an explore model.</summary>
        public event Action<PartDefinition> PartTapped;

        /// <summary>Raised when zooming hides the tapped part, so its info should close.</summary>
        public event Action SelectionHidden;

        public ScenarioRunner Runner { get; private set; }   // Only for "placement" modules.
        public bool Active { get; private set; }

        // Maps an item name ("cube") to its position in the spawner's prefab list.
        readonly Dictionary<string, int> m_ItemIndex = new Dictionary<string, int>();

        // "explore" state: which model is showing, and the placed copy (null until placed).
        ModuleDefinition m_Module;
        int m_ModelIndex;
        ModelControls m_Placed;

        /// <summary>Names of every item the spawner can place.</summary>
        public ICollection<string> KnownItems => m_ItemIndex.Keys;

        bool IsExplore => m_Module != null && m_Module.scenario.type == "explore";
        List<ModelDefinition> Models => m_Module.scenario.models;

        /// <summary>The explore model currently showing, or null in a placement module.</summary>
        public ModelDefinition CurrentModel => IsExplore ? Models[m_ModelIndex] : null;

        // ------------------------------------------------------------ what the screen shows

        /// <summary>The main instruction line, e.g. "Place a cube".</summary>
        public string PromptText
        {
            get
            {
                if (!IsExplore) return Runner?.CurrentPrompt ?? "";
                return m_Placed == null ? "Tap a surface to place the model" : "Drag to turn, pinch to zoom, tap a part";
            }
        }

        /// <summary>The small status line, e.g. "2/5" or "Animal cell (1/2)".</summary>
        public string StatusText
        {
            get
            {
                if (!IsExplore) return Runner == null ? "" : Runner.Done + "/" + Runner.Total;
                string title = string.IsNullOrEmpty(CurrentModel.title) ? CurrentModel.item : CurrentModel.title;
                return Models.Count > 1 ? title + " (" + (m_ModelIndex + 1) + "/" + Models.Count + ")" : title;
            }
        }

        /// <summary>True when a placement module is finished. Explore modules never "finish".</summary>
        public bool IsComplete => !IsExplore && Runner != null && Runner.IsComplete;

        /// <summary>True when the "Next model" button makes sense.</summary>
        public bool CanSwitchModel => IsExplore && m_Placed != null && Models.Count > 1;

        // ------------------------------------------------------------ setup

        void Awake()
        {
            if (spawner == null) spawner = FindAnyObjectByType<ObjectSpawner>();
            if (trigger == null) trigger = FindAnyObjectByType<ARInteractorSpawnTrigger>();
            if (planeManager == null) planeManager = FindAnyObjectByType<ARPlaneManager>();

            // Build the name -> index table from the prefabs themselves, so adding a
            // prefab to the spawner makes it available to JSON files with no code change.
            if (spawner != null)
            {
                for (int i = 0; i < spawner.objectPrefabs.Count; i++)
                {
                    string id = ItemName(spawner.objectPrefabs[i]);
                    if (id != "debugcube") m_ItemIndex[id] = i;
                }
                spawner.objectSpawned += OnObjectSpawned;
            }

            if (planeManager != null) planeManager.trackablesChanged.AddListener(OnPlanesChanged);
            AppSettings.Changed += ApplyPlaneVisibility;

            SetAREnabled(false); // Idle until a scenario starts.
        }

        void OnDestroy()
        {
            if (spawner != null) spawner.objectSpawned -= OnObjectSpawned;
            if (planeManager != null) planeManager.trackablesChanged.RemoveListener(OnPlanesChanged);
            AppSettings.Changed -= ApplyPlaneVisibility;
        }

        /// <summary>"CubeVariant" -> "cube". The JSON uses this lowercase name.</summary>
        public static string ItemName(GameObject prefab) => prefab.name.Replace("Variant", "").ToLowerInvariant();

        // ------------------------------------------------------------ public actions

        /// <summary>Starts a module's scenario.</summary>
        public void Begin(ModuleDefinition module)
        {
            m_Module = module;
            m_ModelIndex = 0;
            m_Placed = null;
            Runner = IsExplore ? null : new ScenarioRunner(module.scenario);
            if (trigger != null) trigger.requireHorizontalUpSurface = module.scenario.requireHorizontalSurface;
            Active = true;
            SetAREnabled(true);
            PrepareNextItem();
            Progress?.Invoke();
        }

        /// <summary>Removes everything placed and starts the same scenario again.</summary>
        public void Restart()
        {
            if (m_Module == null) return;
            ClearPlacedObjects();
            Runner?.Reset();
            m_ModelIndex = 0;
            PrepareNextItem();
            Progress?.Invoke();
        }

        /// <summary>Stops the scenario and cleans up.</summary>
        public void End()
        {
            Active = false;
            ClearPlacedObjects();
            SetAREnabled(false);
            m_Module = null;
            Runner = null;
        }

        /// <summary>Explore modules: swap the placed model for the next one, in the same spot.</summary>
        public void NextModel()
        {
            if (!CanSwitchModel) return;
            var old = m_Placed.transform;
            var (position, rotation, parent) = (old.position, old.rotation, old.parent);
            Destroy(old.gameObject);
            m_Placed = null;

            m_ModelIndex = (m_ModelIndex + 1) % Models.Count;
            if (m_ItemIndex.TryGetValue(CurrentModel.item.ToLowerInvariant(), out int index))
                MakeInteractive(Instantiate(spawner.objectPrefabs[index], position, rotation, parent));
            else
                PrepareNextItem(); // Unknown item: fall back to tap-to-place (it will warn).
            Progress?.Invoke();
        }

        /// <summary>Removes the highlight from the last tapped part (e.g. when its info is closed).</summary>
        public void ClearSelection()
        {
            if (m_Placed != null) m_Placed.ClearHighlight();
        }

        // ------------------------------------------------------------ internals

        /// <summary>Turns tap-to-place and surface detection on or off together.</summary>
        void SetAREnabled(bool on)
        {
            if (trigger != null) trigger.enabled = on;
            if (planeManager != null) planeManager.enabled = on;
            if (on) ApplyPlaneVisibility();
        }

        /// <summary>Spawned objects are children of the spawner, so deleting its children clears them all.</summary>
        void ClearPlacedObjects()
        {
            m_Placed = null;
            if (spawner == null) return;
            for (int i = spawner.transform.childCount - 1; i >= 0; i--)
                Destroy(spawner.transform.GetChild(i).gameObject);
        }

        /// <summary>Tells the spawner which prefab the next tap should place (or stops taps when done).</summary>
        void PrepareNextItem()
        {
            if (spawner == null || m_Module == null) return;

            if (IsExplore)
            {
                // One model at a time: accept taps only until it is placed.
                if (m_Placed == null) SelectItem(CurrentModel.item);
                else if (trigger != null) trigger.enabled = false;
                return;
            }

            // Placement: nothing left to place, so stop accepting taps.
            if (Runner.IsComplete)
            {
                if (trigger != null) trigger.enabled = false;
                return;
            }

            if (!SelectItem(Runner.Current.item))
            {
                // Unknown item: skip it. (An invalid index would make the spawner pick randomly.)
                Runner.Advance();
                PrepareNextItem();
            }
        }

        /// <summary>Points the spawner at an item by name. Returns false (and turns taps off) if no such item.</summary>
        bool SelectItem(string item)
        {
            if (m_ItemIndex.TryGetValue(item.ToLowerInvariant(), out int index))
            {
                spawner.spawnOptionIndex = index;
                if (trigger != null) trigger.enabled = true;
                return true;
            }
            Debug.LogWarning("ARScience: unknown item '" + item + "', skipping");
            if (trigger != null) trigger.enabled = false;
            return false;
        }

        /// <summary>Called by the spawner after each successful placement.</summary>
        void OnObjectSpawned(GameObject placed)
        {
            if (!Active || m_Module == null) return;

            if (IsExplore)
            {
                if (m_Placed != null) return;
                MakeInteractive(placed);
            }
            else
            {
                if (Runner.IsComplete) return;
                placed.transform.localScale *= Runner.CurrentScale;
                Runner.Advance();
            }
            PrepareNextItem();
            Progress?.Invoke();
        }

        /// <summary>Adds turn / zoom / tap controls to a placed explore model.</summary>
        void MakeInteractive(GameObject placed)
        {
            m_Placed = placed.AddComponent<ModelControls>();
            m_Placed.Setup(CurrentModel);
            m_Placed.PartTapped += part => PartTapped?.Invoke(part);
            m_Placed.SelectionHidden += () => SelectionHidden?.Invoke();
        }

        /// <summary>Shows or hides the detected-surface overlay according to the user's setting.</summary>
        void ApplyPlaneVisibility()
        {
            if (planeManager == null) return;
            foreach (var plane in planeManager.trackables) plane.gameObject.SetActive(AppSettings.ShowPlanes);
        }

        /// <summary>Newly detected surfaces must also respect the setting.</summary>
        void OnPlanesChanged(ARTrackablesChangedEventArgs<ARPlane> change)
        {
            if (AppSettings.ShowPlanes) return;
            foreach (var plane in change.added) plane.gameObject.SetActive(false);
        }
    }
}
