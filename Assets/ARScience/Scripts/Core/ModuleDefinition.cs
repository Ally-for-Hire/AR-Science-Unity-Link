using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARScience
{
    // ---------------------------------------------------------------------
    // Data model. These classes mirror the JSON files in Resources/Modules.
    // JsonUtility fills public fields directly, so no parsing code is needed.
    // A missing number in the JSON arrives here as 0, so "0" always means "use the default".
    // The same rules are written for editors (VS Code, the web editor) in Assets/ARScience/module.schema.json.
    // ---------------------------------------------------------------------

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

    /// <summary>What the student does in a module. `type` picks which list is used.</summary>
    [Serializable]
    public class ScenarioDefinition
    {
        public string type;                     // "placement" (uses steps) or "explore" (uses models).
        public string instructions;             // General help text shown during the scenario.
        public bool requireHorizontalSurface;   // Only allow placing on floors / tables.
        public List<StepDefinition> steps = new List<StepDefinition>();     // For "placement".
        public List<ModelDefinition> models = new List<ModelDefinition>();  // For "explore".
    }

    /// <summary>"placement": one thing the user must do - place `count` copies of `item`.</summary>
    [Serializable]
    public class StepDefinition
    {
        public string item;     // Name of a spawnable object (e.g. "cube").
        public int count;       // How many to place. 0 / missing means 1.
        public string prompt;   // Text shown to the user. Optional.
        public float scale;     // Size multiplier. 0 / missing means 1.
    }

    /// <summary>"explore": one model the student places, turns, zooms and taps.</summary>
    [Serializable]
    public class ModelDefinition
    {
        public string item;     // Name of a spawnable object, like a step's item (e.g. "celldemo").
        public string title;    // Shown while this model is on screen (e.g. "Animal cell").
        public float minZoom;   // Smallest size allowed by pinching, 1 = as placed. 0 / missing means 0.5.
        public float maxZoom;   // Biggest size allowed by pinching. 0 / missing means 3.
        public List<PartDefinition> parts = new List<PartDefinition>();
    }

    /// <summary>A tappable piece of a model. `id` is the name of a child object inside the model's prefab.</summary>
    [Serializable]
    public class PartDefinition
    {
        public string id;           // Child object name in the prefab, e.g. "Nucleus". Case and spaces don't matter.
        public string name;         // Shown as the title when tapped.
        public string description;  // Shown under the title when tapped.
        public float showFromZoom;  // Hidden until zoomed in this far (inner parts). 0 / missing = always shown.
        public float hideFromZoom;  // Hidden once zoomed in this far (outer shells). 0 / missing = never hidden.
    }

    /// <summary>Loads and checks module JSON files.</summary>
    public static class ModuleCatalog
    {
        /// <summary>Folder under any "Resources" directory that holds module files.</summary>
        public const string ResourceFolder = "Modules";

        /// <summary>Safety limit so a typo can't ask for thousands of objects.</summary>
        public const int MaxPlacements = 20;

        /// <summary>Upper limit for maxZoom, so a model can't be blown up to fill a room.</summary>
        public const float MaxAllowedZoom = 10f;

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

            if (scenario.type == "placement") ValidatePlacement(scenario, knownItems, errors);
            else if (scenario.type == "explore") ValidateExplore(scenario, knownItems, errors);
            else errors.Add("scenario.type must be \"placement\" or \"explore\"");
            return errors;
        }

        static void ValidatePlacement(ScenarioDefinition scenario, ICollection<string> knownItems, List<string> errors)
        {
            if (scenario.steps == null || scenario.steps.Count == 0) { errors.Add("scenario needs at least one step"); return; }

            int totalPlacements = 0;
            foreach (var step in scenario.steps)
            {
                int count = step.count == 0 ? 1 : step.count;
                if (count < 1 || count > 5) errors.Add("step count must be 1-5");
                if (step.scale != 0 && (step.scale < 0.25f || step.scale > 3f)) errors.Add("step scale must be 0.25-3");
                CheckItem(step.item, knownItems, errors);
                totalPlacements += count;
            }
            if (totalPlacements > MaxPlacements) errors.Add("more than " + MaxPlacements + " placements in total");
        }

        static void ValidateExplore(ScenarioDefinition scenario, ICollection<string> knownItems, List<string> errors)
        {
            if (scenario.models == null || scenario.models.Count == 0) { errors.Add("scenario needs at least one model"); return; }

            foreach (var model in scenario.models)
            {
                CheckItem(model.item, knownItems, errors);
                string where = "model '" + model.item + "': ";
                float min = ModelRules.MinZoom(model), max = ModelRules.MaxZoom(model);
                if (model.minZoom < 0 || model.maxZoom < 0) errors.Add(where + "zoom values can't be negative");
                if (min > max) errors.Add(where + "minZoom is bigger than maxZoom");
                if (max > MaxAllowedZoom) errors.Add(where + "maxZoom must be " + MaxAllowedZoom + " or less");

                var seen = new List<string>();
                foreach (var part in model.parts ?? new List<PartDefinition>())
                {
                    if (string.IsNullOrEmpty(part.id)) { errors.Add(where + "every part needs an id"); continue; }
                    if (seen.Exists(id => ModelRules.SameName(id, part.id))) errors.Add(where + "part '" + part.id + "' is listed twice");
                    seen.Add(part.id);
                    if (string.IsNullOrEmpty(part.name)) errors.Add(where + "part '" + part.id + "' needs a name");
                    if (part.showFromZoom > 0 && part.hideFromZoom > 0 && part.showFromZoom >= part.hideFromZoom)
                        errors.Add(where + "part '" + part.id + "' would never be visible (showFromZoom >= hideFromZoom)");
                }
            }
        }

        static void CheckItem(string item, ICollection<string> knownItems, List<string> errors)
        {
            if (knownItems != null && (string.IsNullOrEmpty(item) || !knownItems.Contains(item.ToLowerInvariant())))
                errors.Add("unknown item '" + item + "'");
        }
    }
}
