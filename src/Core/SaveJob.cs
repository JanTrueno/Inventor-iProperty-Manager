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
            try { tx = app.TransactionManager.StartGlobalTransaction(app.ActiveDocument, "Edit iProperties"); }
            catch (Exception ex) { Log.Write("Could not start a transaction: " + ex.Message); }

            try
            {
                foreach (_Document doc in docs)
                {
                    bool modifiable;
                    try { modifiable = doc.IsModifiable; } catch { modifiable = false; }
                    if (!modifiable) { outcome.Skipped++; continue; }

                    try
                    {
                        foreach (var e in edits) PropertyIO.Write(doc, e.Key, e.Value);
                        if (material != null && doc is PartDocument part) Materials.Apply(app, part, material);
                        outcome.Changed++;
                    }
                    catch (Exception ex)
                    {
                        outcome.Failed++;
                        outcome.FirstError = outcome.FirstError ?? doc.DisplayName + ": " + ex.Message;
                        Log.Write("Saving " + doc.DisplayName + " failed: " + ex);
                    }
                }
                tx?.End();
            }
            catch
            {
                try { tx?.Abort(); } catch { }
                throw;
            }

            try { app.ActiveDocument.Update(); } catch { /* the view refreshes on its own */ }
            return outcome;
        }
    }
}
