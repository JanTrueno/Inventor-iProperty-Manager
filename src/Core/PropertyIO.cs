using System;
using System.Collections.Generic;
using System.Globalization;
using Inventor;

namespace IPropertyManager.Core
{
    /// <summary>What a field shows for a group of documents: one shared value, or a mix of values.</summary>
    internal sealed class Combined
    {
        public bool Mixed;
        public object Value;   // null when the (first) document does not have the property
    }

    /// <summary>Reading and writing one iProperty, looked up by set and name, falling back to any set.</summary>
    internal static class PropertyIO
    {
        /// <summary>Inventor stores "no date" as 1 January 1601.</summary>
        public static readonly DateTime NoDate = new DateTime(1601, 1, 1);

        public static Property Find(_Document doc, FieldSpec spec)
        {
            PropertySets sets = doc.PropertySets;
            if (!string.IsNullOrEmpty(spec.Set))
            {
                try { return sets[spec.Set][spec.Name]; } catch { /* look in the other sets */ }
            }
            foreach (PropertySet set in sets)
            {
                foreach (Property p in set)
                {
                    if (string.Equals(p.Name, spec.Name, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(p.DisplayName, spec.Name, StringComparison.OrdinalIgnoreCase))
                        return p;
                }
            }
            return null;
        }

        public static Combined Read(IEnumerable<_Document> docs, FieldSpec spec)
        {
            var result = new Combined();
            bool first = true;
            foreach (_Document doc in docs)
            {
                object v = null;
                try { v = Find(doc, spec)?.Value; } catch { /* unreadable counts as empty */ }
                if (first) { result.Value = v; first = false; }
                else if (!Same(result.Value, v)) result.Mixed = true;
            }
            return result;
        }

        private static bool Same(object a, object b)
        {
            if (IsEmpty(a) || IsEmpty(b)) return IsEmpty(a) && IsEmpty(b);
            if (a is DateTime da && b is DateTime db) return da.Date == db.Date;
            return Equals(a, b) ||
                   string.Equals(Convert.ToString(a, CultureInfo.InvariantCulture), Convert.ToString(b, CultureInfo.InvariantCulture));
        }

        private static bool IsEmpty(object v) =>
            v == null || v is string s && s.Length == 0 || v is DateTime d && d.Date <= NoDate;

        /// <summary>Writes one value, creating a custom property when the document does not have it yet.</summary>
        public static void Write(_Document doc, FieldSpec spec, object value)
        {
            Property p = Find(doc, spec);
            if (p == null)
            {
                if (IsEmpty(value)) return;   // nothing worth creating
                string setName = string.IsNullOrEmpty(spec.Set) ? Catalog.CustomSet : spec.Set;
                doc.PropertySets[setName].Add(value, spec.Name);
                return;
            }
            if (value == null && spec.Kind == FieldKind.Date) value = NoDate;
            if (value == null && spec.Kind == FieldKind.Number) value = 0.0;
            if (value == null) value = "";
            if (!Same(p.Value, value)) p.Value = value;
        }

        public static string Display(object v)
        {
            switch (v)
            {
                case null: return "";
                case DateTime d: return d.Date <= NoDate ? "" : d.ToShortDateString();
                case double x: return x.ToString("G", CultureInfo.CurrentCulture);
                case bool b: return b ? "Yes" : "No";
                default: return Convert.ToString(v, CultureInfo.CurrentCulture);
            }
        }

        /// <summary>Parses what was typed into a value of the field's kind; false when it does not parse.</summary>
        public static bool TryParse(string text, FieldKind kind, out object value)
        {
            text = (text ?? "").Trim();
            value = null;
            switch (kind)
            {
                case FieldKind.Number:
                    if (text.Length == 0) return true;
                    if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double x)) { value = x; return true; }
                    return false;
                case FieldKind.Date:
                    if (text.Length == 0) return true;
                    if (DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out DateTime d)) { value = d.Date; return true; }
                    return false;
                case FieldKind.YesNo:
                    if (text.Length == 0) return true;
                    if (text.Equals("yes", StringComparison.OrdinalIgnoreCase) || text == "1" || text.Equals("true", StringComparison.OrdinalIgnoreCase)) { value = true; return true; }
                    if (text.Equals("no", StringComparison.OrdinalIgnoreCase) || text == "0" || text.Equals("false", StringComparison.OrdinalIgnoreCase)) { value = false; return true; }
                    return false;
                default:
                    value = text;
                    return true;
            }
        }
    }
}
