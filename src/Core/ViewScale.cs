using System;
using System.Collections.Generic;
using System.Linq;
using Inventor;

namespace IPropertyManager.Core
{
    /// <summary>
    /// Steps the scale of the selected drawing views up or down through the standard scales. A view that takes its scale
    /// from its parent (projected, section, detail views) scales the view it follows, so the set stays in step.
    /// </summary>
    internal static class ViewScale
    {
        // Standard drawing scales, smallest first, as a model-to-paper factor (1:20 = 0.05).
        private static readonly double[] Steps =
        {
            1.0 / 1000, 1.0 / 500, 1.0 / 250, 1.0 / 200, 1.0 / 150, 1.0 / 100, 1.0 / 75, 1.0 / 50, 1.0 / 40, 1.0 / 30,
            1.0 / 25, 1.0 / 20, 1.0 / 15, 1.0 / 10, 1.0 / 5, 1.0 / 2.5, 1.0 / 2, 1, 2, 5, 10, 20, 50
        };

        /// <summary>The views behind the selection in the active drawing: selected views, or the views of selected edges.</summary>
        public static List<DrawingView> Selected(Inventor.Application app)
        {
            var views = new List<DrawingView>();
            if (!(SafeActive(app) is DrawingDocument drawing)) return views;
            try
            {
                foreach (object item in drawing.SelectSet)
                {
                    DrawingView v = item switch
                    {
                        DrawingView view => view,
                        DrawingCurveSegment seg => seg.Parent?.Parent,
                        DrawingCurve curve => curve.Parent,
                        _ => null
                    };
                    if (v != null && !views.Contains(v)) views.Add(v);
                }
            }
            catch (Exception ex) { Log.Write("Reading the selected views failed: " + ex.Message); }
            return views;
        }

        /// <summary>The scale shown for the views, or null when they differ.</summary>
        public static string Shown(IList<DrawingView> views)
        {
            string[] scales = views.Select(v => { try { return v.ScaleString; } catch { return "?"; } }).Distinct().ToArray();
            return scales.Length == 1 ? scales[0] : null;
        }

        /// <summary>Moves each view (or the view it follows) one standard scale up (+1) or down (-1), as one undo step.</summary>
        public static void Step(Inventor.Application app, IList<DrawingView> views, int direction)
        {
            var targets = new List<DrawingView>();
            foreach (DrawingView v in views)
            {
                DrawingView t = ScaleOwner(v);
                if (!targets.Contains(t)) targets.Add(t);
            }

            Transaction tx = null;
            try { tx = app.TransactionManager.StartTransaction((_Document)app.ActiveDocument, direction > 0 ? "Scale up views" : "Scale down views"); }
            catch (Exception ex) { Log.Write("Could not start a transaction: " + ex.Message); }
            try
            {
                using (Log.Timed("View scale: " + targets.Count + " view(s)"))
                    foreach (DrawingView t in targets)
                    {
                        double next = Next(t.Scale, direction);
                        if (next > 0) t.Scale = next;
                    }
                tx?.End();
            }
            catch
            {
                try { tx?.Abort(); } catch { }
                throw;
            }
        }

        /// <summary>The view whose scale this one follows, up to the base view.</summary>
        private static DrawingView ScaleOwner(DrawingView v)
        {
            for (int depth = 0; depth < 16; depth++)
            {
                DrawingView parent;
                bool follows;
                try { parent = v.ParentView; follows = v.ScaleFromBase; } catch { break; }
                if (parent == null || !follows) break;
                v = parent;
            }
            return v;
        }

        /// <summary>The next standard scale above or below the current one; an odd scale snaps to its neighbour.</summary>
        private static double Next(double current, int direction)
        {
            const double tolerance = 1e-6;
            if (direction > 0)
            {
                foreach (double s in Steps) if (s > current * (1 + tolerance)) return s;
            }
            else
            {
                for (int i = Steps.Length - 1; i >= 0; i--) if (Steps[i] < current * (1 - tolerance)) return Steps[i];
            }
            return -1;   // already at the end of the list
        }

        private static object SafeActive(Inventor.Application app)
        {
            try { return app.ActiveDocument; } catch { return null; }
        }
    }
}
