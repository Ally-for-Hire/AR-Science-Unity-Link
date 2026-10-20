using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Interaction.Toolkit.Samples.ARStarterAssets;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

namespace ARScience
{
    /// <summary>
    /// Connects a ScenarioRunner (what to do) to the AR template (how to place things).
    /// The template already handles tapping and finding surfaces; this class only
    /// decides WHICH object gets placed next and reacts when one has been placed.
    /// </summary>
    public class ARScenarioController : MonoBehaviour
    {
        // Template parts we drive. Found automatically if left empty.
        public ObjectSpawner spawner;               // Spawns the prefab when the user taps.
        public ARInteractorSpawnTrigger trigger;    // Turns taps into spawn requests.
        public ARPlaneManager planeManager;         // Detects and draws surfaces.

        /// <summary>Raised whenever progress changes, so the UI can redraw.</summary>
        public event Action Progress;

        public ScenarioRunner Runner { get; private set; }
        public bool Active { get; private set; }

        // Maps an item name ("cube") to its position in the spawner's prefab list.
        readonly Dictionary<string, int> m_ItemIndex = new Dictionary<string, int>();

        /// <summary>Names of every item the spawner can place.</summary>
        public ICollection<string> KnownItems => m_ItemIndex.Keys;

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
        static string ItemName(GameObject prefab) => prefab.name.Replace("Variant", "").ToLowerInvariant();

        // ------------------------------------------------------------ public actions

        /// <summary>Starts a module's scenario.</summary>
        public void Begin(ModuleDefinition module)
        {
            Runner = new ScenarioRunner(module.scenario);
            if (trigger != null) trigger.requireHorizontalUpSurface = module.scenario.requireHorizontalSurface;
            Active = true;
            SetAREnabled(true);
            PrepareNextItem();
            Progress?.Invoke();
        }

        /// <summary>Removes everything placed and starts the same scenario again.</summary>
        public void Restart()
        {
            if (Runner == null) return;
            ClearPlacedObjects();
            Runner.Reset();
            PrepareNextItem();
            Progress?.Invoke();
        }

        /// <summary>Stops the scenario and cleans up.</summary>
        public void End()
        {
            Active = false;
            ClearPlacedObjects();
            SetAREnabled(false);
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
            if (spawner == null) return;
            for (int i = spawner.transform.childCount - 1; i >= 0; i--)
                Destroy(spawner.transform.GetChild(i).gameObject);
        }

        /// <summary>Tells the spawner which prefab the next tap should place.</summary>
        void PrepareNextItem()
        {
            if (spawner == null || Runner == null) return;

            // Nothing left to place: stop accepting taps.
            if (Runner.IsComplete)
            {
                if (trigger != null) trigger.enabled = false;
                return;
            }

            if (m_ItemIndex.TryGetValue(Runner.Current.item.ToLowerInvariant(), out int index))
            {
                spawner.spawnOptionIndex = index;
                if (trigger != null) trigger.enabled = true;
            }
            else
            {
                // Unknown item: skip it. (An invalid index would make the spawner pick randomly.)
                Debug.LogWarning("ARScience: unknown item '" + Runner.Current.item + "', skipping");
                Runner.Advance();
                PrepareNextItem();
            }
        }

        /// <summary>Called by the spawner after each successful placement.</summary>
        void OnObjectSpawned(GameObject placed)
        {
            if (!Active || Runner == null || Runner.IsComplete) return;
            placed.transform.localScale *= Runner.CurrentScale;
            Runner.Advance();
            PrepareNextItem();
            Progress?.Invoke();
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
