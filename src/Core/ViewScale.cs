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
        // Drawing scales, smallest first, as a model-to-paper factor (1:20 = 0.05). Steps of roughly 10-25 %, so a view
        // can be fitted to the sheet closely.
        private static readonly double[] Steps = Build(
            new[] { 1000, 800, 750, 600, 500, 400, 300, 250, 200, 175, 150, 125, 100, 90, 80, 75, 70, 60, 50, 45, 40, 35,
                    30, 25, 20, 15, 12.5, 10, 9, 8, 7.5, 7, 6, 5, 4, 3, 2.5, 2, 1.5 },
            new[] { 1, 1.5, 2, 2.5, 3, 4, 5, 6, 8, 10, 15, 20, 25, 50 });

        private static double[] Build(double[] reductions, double[] enlargements) =>
            reductions.Select(d => 1.0 / d).Concat(enlargements).ToArray();

        /// <summary>
        /// The views to scale in the active drawing: the selected views (or the views of selected edges); with the sheet
        /// selected, or nothing at all, every view on the sheet (<paramref name="wholeSheet"/>).
        /// </summary>
        public static List<DrawingView> Targeted(Inventor.Application app, out bool wholeSheet)
        {
            wholeSheet = false;
            var views = new List<DrawingView>();
            if (!(SafeActive(app) is DrawingDocument drawing)) return views;
            try
            {
                Sheet sheet = null;
                int count = 0;
                foreach (object item in drawing.SelectSet)
                {
                    count++;
                    if (item is Sheet s) { sheet = s; continue; }
                    DrawingView v = item switch
                    {
                        DrawingView view => view,
                        DrawingCurveSegment seg => seg.Parent?.Parent,
                        DrawingCurve curve => curve.Parent,
                        _ => null
                    };
                    if (v != null && !views.Contains(v)) views.Add(v);
                }
                if (views.Count == 0 && (sheet != null || count == 0))
                {
                    foreach (DrawingView v in (sheet ?? drawing.ActiveSheet).DrawingViews) views.Add(v);
                    wholeSheet = views.Count > 0;
                }
            }
            catch (Exception ex) { Log.Write("Reading the selected views failed: " + ex.Message); }
            return views;
        }

        /// <summary>The scale shown for the views (the scale they follow), or null when they differ.</summary>
        public static string Shown(IList<DrawingView> views)
        {
            string[] scales = Owners(views).Select(v => { try { return v.ScaleString; } catch { return "?"; } }).Distinct().ToArray();
            return scales.Length == 1 ? scales[0] : null;
        }

        /// <summary>Moves each view (or the view it follows) one standard scale up (+1) or down (-1), as one undo step.</summary>
        public static void Step(Inventor.Application app, IList<DrawingView> views, bool wholeSheet, int direction) =>
            Apply(app, views, wholeSheet, direction > 0 ? "Scale up views" : "Scale down views", current => Next(current, direction));

        /// <summary>Gives each view (or the view it follows) the scale typed in, as one undo step.</summary>
        public static void Set(Inventor.Application app, IList<DrawingView> views, bool wholeSheet, double scale) =>
            Apply(app, views, wholeSheet, "Scale views", current => scale);

        /// <summary>
        /// Reads a typed scale: "1:20", "1/20", "2:1", or a bare number, which is read as 1:n ("20" is 1:20) and below 1
        /// as the factor itself ("0.5" is 1:2). Both "." and "," work as the decimal sign.
        /// </summary>
        public static bool TryParse(string text, out double scale)
        {
            scale = 0;
            string s = (text ?? "").Trim().Replace(" ", "").Replace(',', '.');
            if (s.Length == 0) return false;
            int sep = s.IndexOfAny(new[] { ':', '/' });
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            const System.Globalization.NumberStyles num = System.Globalization.NumberStyles.Float;
            if (sep >= 0)
            {
                if (!double.TryParse(s.Substring(0, sep), num, inv, out double a) ||
                    !double.TryParse(s.Substring(sep + 1), num, inv, out double b) || a <= 0 || b <= 0) return false;
                scale = a / b;
            }
            else
            {
                if (!double.TryParse(s, num, inv, out double n) || n <= 0) return false;
                scale = n >= 1 ? 1 / n : n;
            }
            return scale >= 1e-4 && scale <= 1e4;
        }

        /// <summary>The views whose scale changes: each view, or the view it follows, once.</summary>
        private static List<DrawingView> Owners(IEnumerable<DrawingView> views)
        {
            var owners = new List<DrawingView>();
            foreach (DrawingView v in views)
            {
                DrawingView t = ScaleOwner(v);
                if (!owners.Contains(t)) owners.Add(t);
            }
            return owners;
        }

        private static void Apply(Inventor.Application app, IList<DrawingView> views, bool wholeSheet, string undoName, Func<double, double> newScale)
        {
            List<DrawingView> targets = Owners(views);

            Transaction tx = null;
            try { tx = app.TransactionManager.StartTransaction((_Document)app.ActiveDocument, undoName); }
            catch (Exception ex) { Log.Write("Could not start a transaction: " + ex.Message); }
            try
            {
                using (Log.Timed("View scale: " + targets.Count + " view(s)"))
                    foreach (DrawingView t in targets)
                    {
                        double next = newScale(t.Scale);
                        if (next > 0 && Math.Abs(next - t.Scale) > t.Scale * 1e-9) t.Scale = next;
                    }
                tx?.End();
            }
            catch
            {
                try { tx?.Abort(); } catch { }
                throw;
            }
            if (!wholeSheet) Reselect(app, views);   // a whole sheet was not a selection of views
        }

        /// <summary>Inventor clears the selection when a view is rescaled; select the views again so the next click works.</summary>
        private static void Reselect(Inventor.Application app, IList<DrawingView> views)
        {
            if (!(SafeActive(app) is DrawingDocument drawing)) return;
            try
            {
                List<DrawingView> still = Targeted(app, out bool sheet);
                if (sheet) still.Clear();   // nothing selected any more
                SelectSet set = drawing.SelectSet;
                foreach (DrawingView v in views)
                    if (!still.Contains(v)) set.Select(v);
            }
            catch (Exception ex) { Log.Write("Selecting the views again failed: " + ex.Message); }
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
