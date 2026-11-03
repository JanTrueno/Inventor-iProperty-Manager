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
                        Log.Write("Selection not mapped to a document: " + TypeOf(item));
                        return null;
                }
            }
            catch (Exception ex)
            {
                Log.Write("Resolving a selected " + TypeOf(item) + " failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Edge, face or vertex shown in a drawing view, back to the part that owns it. In an assembly view the geometry is
        /// a proxy (one level per subassembly); its native object is followed down to the part's own geometry. Typed casts
        /// only: converting COM objects through <c>dynamic</c> fails on .NET 8.
        /// </summary>
        private static _Document FromModelGeometry(object geometry)
        {
            object g = geometry;
            for (int depth = 0; depth < 32 && g != null; depth++)
            {
                if (g is EdgeProxy ep) g = ep.NativeObject;
                else if (g is FaceProxy fp) g = fp.NativeObject;
                else if (g is VertexProxy vp) g = vp.NativeObject;
                else break;
            }

            SurfaceBody body = g switch
            {
                Edge e => e.Parent,
                Face f => f.Parent,
                Vertex v => v.Parent,
                _ => null
            };
            if (body != null) return body.ComponentDefinition?.Document as _Document;

            if (geometry != null) Log.Write("Drawing geometry not mapped to a part: " + TypeOf(g));
            return null;
        }

        private static string TypeOf(object o)
        {
            try { return ((ObjectTypeEnum)o.GetType().InvokeMember("Type", System.Reflection.BindingFlags.GetProperty, null, o, null)).ToString(); }
            catch { return o?.GetType().Name ?? "null"; }
        }
    }
}
