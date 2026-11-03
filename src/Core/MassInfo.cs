using System;
using System.Collections.Generic;
using Inventor;

namespace IPropertyManager.Core
{
    /// <summary>Mass of parts and assemblies, totalled over the shown documents, in the active document's mass units.</summary>
    internal static class MassInfo
    {
        /// <summary>The formatted total, or null when a shown document has no mass (a drawing or presentation).</summary>
        public static string Total(Inventor.Application app, IEnumerable<_Document> docs)
        {
            double total = 0;
            int count = 0;
            foreach (_Document d in docs)
            {
                MassProperties mp;
                if (d is PartDocument p) mp = p.ComponentDefinition.MassProperties;
                else if (d is AssemblyDocument a) mp = a.ComponentDefinition.MassProperties;
                else return null;
                try { total += mp.Mass; count++; }
                catch (Exception ex) { Log.Write("Mass of " + d.DisplayName + " failed: " + ex.Message); return null; }
            }
            if (count == 0) return null;

            UnitsOfMeasure uom = app.ActiveDocument.UnitsOfMeasure;
            return uom.GetStringFromValue(total, uom.MassUnits) + (count > 1 ? "  (total)" : "");
        }
    }
}
