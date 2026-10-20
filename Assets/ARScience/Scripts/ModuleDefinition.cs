using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARScience
{
    // ---------------------------------------------------------------------
    // Data model. These classes mirror the JSON files in Resources/Modules.
    // JsonUtility fills public fields directly, so no parsing code is needed.
    // ---------------------------------------------------------------------

    /// <summary>One thing the user must do: place `count` copies of `item`.</summary>
    [Serializable]
    public class StepDefinition
    {
        public string item;     // Name of a spawnable object (e.g. "cube").
        public int count;       // How many to place. 0 / missing means 1.
        public string prompt;   // Text shown to the user. Optional.
        public float scale;     // Size multiplier. 0 / missing means 1.
    }

    /// <summary>An ordered list of steps plus some shared settings.</summary>
    [Serializable]
    public class ScenarioDefinition
    {
        public string type;                     // Kind of scenario. Only "placement" exists today.
        public string instructions;             // General help text shown during the scenario.
        public bool requireHorizontalSurface;   // Only allow placing on floors / tables.
        public List<StepDefinition> steps = new List<StepDefinition>();
    }

    /// <summary>A module is a named, described scenario - the thing shown in the Modules list.</summary>
    [Serializable]
    public class ModuleDefinition
    {
        public int schemaVersion;   // Lets us change the format later without breaking old files.
        public string id;
        public string title;
        public string description;
        public ScenarioDefinition scenario;
    }

    /// <summary>Loads and checks module JSON files.</summary>
    public static class ModuleCatalog
    {
        /// <summary>Folder under any "Resources" directory that holds module files.</summary>
        public const string ResourceFolder = "Modules";

        /// <summary>Safety limit so a typo can't ask for thousands of objects.</summary>
        public const int MaxPlacements = 20;

        /// <summary>Turns JSON text into a module. Returns null if the text is not valid JSON.</summary>
        public static ModuleDefinition Parse(string json)
        {
            try { return JsonUtility.FromJson<ModuleDefinition>(json); }
            catch (ArgumentException) { return null; }
        }

        /// <summary>Loads every module file. Unreadable files still appear (flagged invalid) so the user can see them.</summary>
        public static List<ModuleDefinition> LoadAll()
        {
            var modules = new List<ModuleDefinition>();
            foreach (var file in Resources.LoadAll<TextAsset>(ResourceFolder))
            {
                var module = Parse(file.text);
                if (module == null)
                    module = new ModuleDefinition { id = file.name, title = file.name, description = "Invalid JSON" };
                modules.Add(module);
            }
            return modules;
        }

        /// <summary>
        /// Returns a list of problems found in the module (empty list = valid).
        /// Pass the names of items that can actually be spawned, or null to skip that check.
        /// </summary>
        public static List<string> Validate(ModuleDefinition module, ICollection<string> knownItems)
        {
            var errors = new List<string>();
            if (module == null) { errors.Add("Could not parse JSON"); return errors; }

            if (module.schemaVersion != 1) errors.Add("schemaVersion must be 1");
            if (string.IsNullOrEmpty(module.id)) errors.Add("id is required");
            if (string.IsNullOrEmpty(module.title)) errors.Add("title is required");

            var scenario = module.scenario;
            if (scenario == null) { errors.Add("scenario is required"); return errors; }
            if (scenario.type != "placement") errors.Add("scenario.type must be \"placement\"");
            if (scenario.steps == null || scenario.steps.Count == 0)
            {
                errors.Add("scenario needs at least one step");
                return errors;
            }

            int totalPlacements = 0;
            foreach (var step in scenario.steps)
            {
                int count = step.count == 0 ? 1 : step.count;
                if (count < 1 || count > 5) errors.Add("step count must be 1-5");
                if (step.scale != 0 && (step.scale < 0.25f || step.scale > 3f)) errors.Add("step scale must be 0.25-3");
                if (knownItems != null && (string.IsNullOrEmpty(step.item) || !knownItems.Contains(step.item.ToLowerInvariant())))
                    errors.Add("unknown item '" + step.item + "'");
                totalPlacements += count;
            }
            if (totalPlacements > MaxPlacements) errors.Add("more than " + MaxPlacements + " placements in total");

            return errors;
        }
    }
}
