using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ARScience.EditorTools
{
    /// <summary>
    /// Quick self-test that runs without a device or a scene.
    /// Run from the command line with:
    ///   Unity.exe -batchmode -nographics -projectPath ... -executeMethod ARScience.EditorTools.ARScienceValidate.Run
    /// Exit code 0 = all checks passed.
    /// </summary>
    public static class ARScienceValidate
    {
        // Items the template's spawner provides (checked against the module files).
        static readonly string[] k_Items = { "cube", "pyramid", "torus", "wedge", "arch", "cylinder" };

        public static void Run()
        {
            var failures = new List<string>();

            // Logs a PASS/FAIL line and remembers failures.
            void Check(bool ok, string what)
            {
                if (!ok) failures.Add(what);
                Debug.Log((ok ? "PASS " : "FAIL ") + what);
            }

            // Module files: there should be exactly one, and it should be valid.
            var modules = ModuleCatalog.LoadAll();
            Check(modules.Count == 1, "exactly one module loads (found " + modules.Count + ")");
            if (modules.Count == 1)
            {
                var errors = ModuleCatalog.Validate(modules[0], new HashSet<string>(k_Items));
                Check(errors.Count == 0, "module is valid: " + string.Join("; ", errors));

                // Walk the whole scenario and confirm the order of actions.
                var runner = new ScenarioRunner(modules[0].scenario);
                var order = new List<string>();
                while (!runner.IsComplete) { order.Add(runner.Current.item); runner.Advance(); }
                Check(string.Join(",", order) == "cube,cylinder,pyramid,pyramid,torus", "action order: " + string.Join(",", order));
            }

            // Bad input must be rejected, not crash.
            Check(ModuleCatalog.Validate(ModuleCatalog.Parse("not json"), null).Count > 0, "malformed JSON rejected");
            var unknownItem = ModuleCatalog.Parse(
                "{\"schemaVersion\":1,\"id\":\"x\",\"title\":\"x\",\"scenario\":{\"type\":\"placement\",\"steps\":[{\"item\":\"sphere\"}]}}");
            Check(ModuleCatalog.Validate(unknownItem, new HashSet<string>(k_Items)).Count > 0, "unknown item rejected");

            EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
        }
    }
}
