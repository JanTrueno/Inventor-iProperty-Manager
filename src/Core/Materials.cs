using System;
using System.Collections.Generic;
using System.Linq;
using Inventor;

namespace IPropertyManager.Core
{
    /// <summary>Material of part documents: what is available, what a part has, and changing it.</summary>
    internal static class Materials
    {
        /// <summary>Display names of the materials in the active library and in the given parts, sorted.</summary>
        public static List<string> Available(Inventor.Application app, IEnumerable<PartDocument> parts)
        {
            var names = new SortedSet<string>(StringComparer.CurrentCultureIgnoreCase);
            try
            {
                foreach (Asset a in app.ActiveMaterialLibrary.MaterialAssets) names.Add(a.DisplayName);
            }
            catch (Exception ex) { Log.Write("Reading the material library failed: " + ex.Message); }
            foreach (PartDocument p in parts)
            {
                try { foreach (Asset a in p.MaterialAssets) names.Add(a.DisplayName); } catch { }
            }
            return names.ToList();
        }

        public static string Current(PartDocument part)
        {
            try { return part.ActiveMaterial?.DisplayName; } catch { return null; }
        }

        /// <summary>Gives the part the named material, copying it from the active library when the part lacks it.</summary>
        public static void Apply(Inventor.Application app, PartDocument part, string displayName)
        {
            if (string.Equals(Current(part), displayName, StringComparison.Ordinal)) return;

            Asset asset = Find(part.MaterialAssets, displayName);
            if (asset == null)
            {
                Asset source = Find(app.ActiveMaterialLibrary.MaterialAssets, displayName)
                               ?? throw new InvalidOperationException("Material \"" + displayName + "\" is not in the active library.");
                asset = source.CopyTo(part, false);
            }
            part.ActiveMaterial = asset;
        }

        private static Asset Find(AssetsEnumerator assets, string displayName)
        {
            foreach (Asset a in assets)
                if (string.Equals(a.DisplayName, displayName, StringComparison.Ordinal)) return a;
            return null;
        }
    }
}
