using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

namespace ARScience.EditorTools
{
    /// <summary>
    /// Checks every module file in Resources/Modules. No phone needed.
    ///
    /// In Unity: menu  Tools > AR Science > Check Modules  (results pop up and go to the Console).
    /// From the command line:
    ///   Unity.exe -batchmode -nographics -projectPath ... -executeMethod ARScience.EditorTools.ARScienceValidate.Run
    ///   Exit code 0 = all checks passed.
    /// </summary>
    public static class ARScienceValidate
    {
        const string MainScene = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Tools/AR Science/Check Modules")]
        static void RunFromMenu()
        {
            var failures = Check();
            EditorUtility.DisplayDialog("AR Science",
                failures.Count == 0 ? "All modules look good!" : "Problems found:\n\n" + string.Join("\n", failures), "OK");
        }

        /// <summary>Command-line entry point. Quits Unity with 0 (pass) or 1 (fail).</summary>
        public static void Run() => EditorApplication.Exit(Check().Count == 0 ? 0 : 1);

        /// <summary>Runs every check and returns the problems (empty list = all good).</summary>
        static List<string> Check()
        {
            var failures = new List<string>();

            // Logs a PASS/FAIL line and remembers failures.
            void Expect(bool ok, string what)
            {
                if (!ok) failures.Add(what);
                Debug.Log((ok ? "PASS " : "FAIL ") + what);
            }

            // The shapes you can place are whatever is in the scene's Object Spawner list.
            var prefabs = PrefabsInScene();
            var items = prefabs.Keys;
            Expect(items.Count > 0, "Object Spawner found in " + MainScene + " with at least one prefab");

            // Every module file must be valid and playable from start to finish.
            var modules = ModuleCatalog.LoadAll();
            Expect(modules.Count > 0, "at least one module in Resources/Modules (found " + modules.Count + ")");
            foreach (var module in modules)
            {
                var errors = ModuleCatalog.Validate(module, items);
                Expect(errors.Count == 0, "module '" + module.id + "': " + (errors.Count == 0 ? "valid" : string.Join("; ", errors)));
                if (errors.Count > 0) continue;

                if (module.scenario.type == "explore")
                {
                    // Each part id must match a child object in the model's prefab, or it can never be tapped.
                    foreach (var model in module.scenario.models)
                    foreach (var part in model.parts)
                        Expect(HasChildNamed(prefabs[model.item.ToLowerInvariant()], part.id),
                            "module '" + module.id + "': part '" + part.id + "' exists in prefab '" + model.item + "'");
                    continue;
                }

                var runner = new ScenarioRunner(module.scenario);
                int placed = 0;
                while (!runner.IsComplete && placed <= ModuleCatalog.MaxPlacements) { runner.Advance(); placed++; }
                Expect(runner.IsComplete && placed == runner.Total, "module '" + module.id + "' plays through (" + placed + " placements)");
            }

            // Bad input must be rejected, not crash.
            Expect(ModuleCatalog.Validate(ModuleCatalog.Parse("not json"), null).Count > 0, "malformed JSON rejected");
            var unknownItem = ModuleCatalog.Parse(
                "{\"schemaVersion\":1,\"id\":\"x\",\"title\":\"x\",\"scenario\":{\"type\":\"placement\",\"steps\":[{\"item\":\"no-such-shape\"}]}}");
            Expect(ModuleCatalog.Validate(unknownItem, items).Count > 0, "unknown item rejected");

            return failures;
        }

        /// <summary>Item name -> prefab, read from the scene's Object Spawner the same way the app does.</summary>
        static Dictionary<string, GameObject> PrefabsInScene()
        {
            var prefabs = new Dictionary<string, GameObject>();
            var scene = EditorSceneManager.GetSceneByPath(MainScene);
            bool openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Additive);

            foreach (var root in scene.GetRootGameObjects())
            foreach (var spawner in root.GetComponentsInChildren<ObjectSpawner>(true))
            foreach (var prefab in spawner.objectPrefabs)
                if (prefab != null) prefabs[ARScenarioController.ItemName(prefab)] = prefab;
            prefabs.Remove("debugcube"); // The app hides this one too.

            if (openedHere) EditorSceneManager.CloseScene(scene, true);
            return prefabs;
        }

        /// <summary>True if the prefab has a child whose name matches (same forgiving match as the app).</summary>
        static bool HasChildNamed(GameObject prefab, string name)
        {
            foreach (var child in prefab.GetComponentsInChildren<Transform>(true))
                if (ModelRules.SameName(child.name, name)) return true;
            return false;
        }
    }
}
