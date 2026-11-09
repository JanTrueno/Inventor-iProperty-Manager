using System;
using System.Collections.Generic;
using Inventor;

namespace IPropertyManager.Core
{
    /// <summary>
    /// Whole-drawing actions: name every sheet after an iProperty of the model it shows, and number the models by sheet
    /// (001, 002, 003 in sheet order) into an iProperty. The model of a sheet is the one in its first view that has one.
    /// </summary>
    internal static class SheetTools
    {
        public sealed class Outcome
        {
            public int Done, NoModel, Skipped;
            public string Summary(string what)
            {
                string s = what + " " + Done + (Done == 1 ? " sheet." : " sheets.");
                if (NoModel > 0) s += " " + NoModel + " without a model view.";
                if (Skipped > 0) s += " " + Skipped + " skipped (empty or read-only).";
                return s;
            }
        }

        /// <summary>Renames each sheet to the model's value of <paramref name="spec"/>; Inventor adds the ":n" itself.</summary>
        public static Outcome NameSheets(Inventor.Application app, DrawingDocument drawing, FieldSpec spec)
        {
            var outcome = new Outcome();
            Run(app, drawing, "Name sheets", () =>
            {
                foreach (Sheet sheet in drawing.Sheets)
                {
                    _Document model = ModelOf(sheet);
                    if (model == null) { outcome.NoModel++; continue; }
                    Combined c = PropertyIO.Read(new[] { model }, spec);
                    string name = PropertyIO.Display(c.Value).Replace(':', '-').Trim();   // ':' is Inventor's separator
                    if (name.Length == 0) { outcome.Skipped++; continue; }
                    sheet.Name = name;
                    outcome.Done++;
                }
            });
            return outcome;
        }

        /// <summary>Writes the sheet's position as 001, 002, ... into <paramref name="spec"/> of the model on each sheet.</summary>
        public static Outcome NumberSheets(Inventor.Application app, DrawingDocument drawing, FieldSpec spec)
        {
            var outcome = new Outcome();
            var numbered = new Dictionary<_Document, string>();
            Run(app, drawing, "Number sheets", () =>
            {
                int index = 0;
                foreach (Sheet sheet in drawing.Sheets)
                {
                    index++;
                    _Document model = ModelOf(sheet);
                    if (model == null) { outcome.NoModel++; continue; }
                    bool modifiable;
                    try { modifiable = model.IsModifiable; } catch { modifiable = false; }
                    if (!modifiable) { outcome.Skipped++; continue; }

                    string number = index.ToString("000");
                    if (numbered.TryGetValue(model, out string earlier))
                        Log.Write(model.DisplayName + " is on sheet " + earlier + " and " + number + "; it keeps " + number + ".");
                    PropertyIO.Write(model, spec, number);
                    numbered[model] = number;
                    outcome.Done++;
                }
            });
            return outcome;
        }

        /// <summary>The model shown on the sheet: the document of the first view that references one.</summary>
        public static _Document ModelOf(Sheet sheet)
        {
            foreach (DrawingView view in sheet.DrawingViews)
            {
                try
                {
                    if (view.ReferencedDocumentDescriptor?.ReferencedDocument is _Document d &&
                        (d is PartDocument || d is AssemblyDocument)) return d;
                }
                catch { /* a view without a model, e.g. a draft view */ }
            }
            return null;
        }

        // One undo step over the drawing and the models it writes to.
        private static void Run(Inventor.Application app, DrawingDocument drawing, string undoName, Action work)
        {
            Transaction tx = null;
            try { tx = app.TransactionManager.StartGlobalTransaction((_Document)drawing, undoName); }
            catch (Exception ex) { Log.Write("Could not start a transaction: " + ex.Message); }
            try
            {
                using (Log.Timed(undoName)) work();
                tx?.End();
            }
            catch
            {
                try { tx?.Abort(); } catch { }
                throw;
            }
        }
    }
}
