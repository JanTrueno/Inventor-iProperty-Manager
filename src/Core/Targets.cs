using System;
using System.Collections.Generic;
using Inventor;

namespace IPropertyManager.Core
{
    /// <summary>
    /// Works out which documents the panel is about: the documents behind the current selection (occurrences,
    /// drawing views, drawing curves), or the document being edited when nothing usable is selected.
    /// </summary>
    internal static class Targets
    {
        public static List<_Document> Resolve(Inventor.Application app, out bool fromSelection)
        {
            fromSelection = false;
            var docs = new List<_Document>();
            _Document top;
            try { top = app.ActiveDocument; } catch { top = null; }
            if (top == null) return docs;

            try
            {
                foreach (object item in top.SelectSet)
                {
                    _Document d = DocumentOf(item);
                    if (d != null && !docs.Contains(d)) docs.Add(d);
                }
            }
            catch (Exception ex) { Log.Write("Reading the selection failed: " + ex.Message); }

            if (docs.Count > 0) { fromSelection = true; return docs; }

            _Document edit;
            try { edit = app.ActiveEditDocument; } catch { edit = null; }
            docs.Add(edit ?? top);
            return docs;
        }

        private static _Document DocumentOf(object item)
        {
            try
            {
                switch (item)
                {
                    case ComponentOccurrence occ:
                        if (occ.Definition is VirtualComponentDefinition) return null;   // no document of its own
                        return occ.Definition?.Document as _Document;
                    case DrawingView view:
                        return view.ReferencedDocumentDescriptor?.ReferencedDocument as _Document;
                    case DrawingCurveSegment seg:
                        return FromModelGeometry(seg.Parent?.ModelGeometry);
                    case DrawingCurve curve:
                        return FromModelGeometry(curve.ModelGeometry);
                    default:
                        return null;
                }
            }
            catch { return null; }
        }

        /// <summary>Edge or face (or proxy of one) shown in a drawing view, back to the document that owns it.</summary>
        private static _Document FromModelGeometry(object geometry)
        {
            if (geometry == null) return null;
            dynamic g = geometry;
            try
            {
                ComponentOccurrence occ = g.ContainingOccurrence;
                if (occ != null) return occ.Definition?.Document as _Document;
            }
            catch { /* not a proxy */ }
            try { return g.Parent.ComponentDefinition.Document as _Document; } catch { return null; }
        }
    }
}
