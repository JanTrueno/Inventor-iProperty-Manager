using System;
using System.Collections.Generic;
using Inventor;

namespace IPropertyManager.Core
{
    /// <summary>
    /// Writes the edited fields (and optionally a material) to every target document as one undo step. Documents that
    /// cannot be changed (library, Content Center, read-only files) are skipped and counted.
    /// </summary>
    internal static class SaveJob
    {
        public sealed class Outcome
        {
            public int Changed, Skipped, Failed;
            public string FirstError;

            public string Summary()
            {
                string s = Changed == 1 ? "Saved 1 document." : "Saved " + Changed + " documents.";
                if (Skipped > 0) s += " " + Skipped + " read-only skipped.";
                if (Failed > 0) s += " " + Failed + " failed: " + FirstError;
                return s;
            }
        }

        public static Outcome Run(Inventor.Application app, IList<_Document> docs, IList<KeyValuePair<FieldSpec, object>> edits, string material)
        {
            var outcome = new Outcome();
            Transaction tx = null;
            using (Log.Timed("Save: start transaction"))
            {
                try { tx = app.TransactionManager.StartGlobalTransaction(app.ActiveDocument, "Edit iProperties"); }
                catch (Exception ex) { Log.Write("Could not start a transaction: " + ex.Message); }
            }

            try
            {
                foreach (_Document doc in docs)
                {
                    bool modifiable;
                    try { modifiable = doc.IsModifiable; } catch { modifiable = false; }
                    if (!modifiable) { outcome.Skipped++; continue; }

                    try
                    {
                        string name = doc.DisplayName;
                        foreach (var e in edits)
                            using (Log.Timed("Save: write " + e.Key.Name + " to " + name)) PropertyIO.Write(doc, e.Key, e.Value);
                        if (material != null && doc is PartDocument part)
                            using (Log.Timed("Save: material of " + name)) Materials.Apply(app, part, material);
                        outcome.Changed++;
                    }
                    catch (Exception ex)
                    {
                        outcome.Failed++;
                        outcome.FirstError = outcome.FirstError ?? doc.DisplayName + ": " + ex.Message;
                        Log.Write("Saving " + doc.DisplayName + " failed: " + ex);
                    }
                }
                using (Log.Timed("Save: end transaction")) tx?.End();
            }
            catch
            {
                try { tx?.Abort(); } catch { }
                throw;
            }

            // Only redraw: Document.Update rebuilds the whole document, which took 25 s in a large drawing. Parts lists
            // and title blocks pick up the new iProperties by themselves, as with Inventor's own iProperties dialog.
            using (Log.Timed("Save: redraw"))
            {
                try { app.ActiveView?.Update(); } catch { /* the view refreshes on its own */ }
            }
            return outcome;
        }
    }
}
