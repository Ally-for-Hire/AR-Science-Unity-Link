using System;
using System.Text;

namespace ARScience
{
    /// <summary>
    /// The rules for "explore" models, as plain functions: no Unity objects, no AR.
    /// That makes them easy to read and easy to unit test (see Assets/ARScience/Tests).
    /// </summary>
    public static class ModelRules
    {
        /// <summary>Zoom limits used when a module leaves minZoom / maxZoom out.</summary>
        public const float DefaultMinZoom = 0.5f, DefaultMaxZoom = 3f;

        /// <summary>The smallest zoom allowed for this model.</summary>
        public static float MinZoom(ModelDefinition model) => model.minZoom > 0 ? model.minZoom : DefaultMinZoom;

        /// <summary>The biggest zoom allowed for this model.</summary>
        public static float MaxZoom(ModelDefinition model) => model.maxZoom > 0 ? model.maxZoom : DefaultMaxZoom;

        /// <summary>Keeps a zoom value inside the model's limits. 1 = the size it was placed at.</summary>
        public static float ClampZoom(float zoom, ModelDefinition model) =>
            Math.Max(MinZoom(model), Math.Min(MaxZoom(model), zoom));

        /// <summary>Should this part be showing at this zoom? Inner parts appear, outer shells disappear.</summary>
        public static bool IsVisibleAt(PartDefinition part, float zoom) =>
            (part.showFromZoom <= 0 || zoom >= part.showFromZoom) &&
            (part.hideFromZoom <= 0 || zoom < part.hideFromZoom);

        /// <summary>
        /// Compares names the forgiving way: "Cell Membrane", "cell_membrane" and "cellMembrane" all match.
        /// Used to match a part's `id` in the JSON with a child object's name in the prefab.
        /// </summary>
        public static bool SameName(string a, string b) =>
            a != null && b != null && Simplify(a) == Simplify(b);

        /// <summary>Finds the part whose id matches an object's name, or null if it isn't a part.</summary>
        public static PartDefinition FindPart(ModelDefinition model, string objectName)
        {
            if (model?.parts == null) return null;
            foreach (var part in model.parts)
                if (SameName(part.id, objectName)) return part;
            return null;
        }

        /// <summary>Lower case, with spaces, underscores and dashes removed.</summary>
        static string Simplify(string name)
        {
            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
                if (c != ' ' && c != '_' && c != '-') sb.Append(char.ToLowerInvariant(c));
            return sb.ToString();
        }
    }
}
