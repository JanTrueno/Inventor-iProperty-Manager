using System;
using System.Collections.Generic;
using System.Linq;

namespace IPropertyManager.Core
{
    internal enum FieldKind { Text, Number, Date, YesNo }

    /// <summary>One iProperty Inventor defines in every document: its set, API name, value kind and dialog tab.</summary>
    internal sealed class KnownProperty
    {
        public string Set;
        public string Name;
        public FieldKind Kind;
        public string Tab;   // the tab of Inventor's iProperties dialog it appears on
        public bool ReadOnly;
    }

    /// <summary>
    /// The standard iProperties, keyed by the names the API uses and grouped like Inventor's iProperties dialog.
    /// Anything not listed is treated as a custom property in the user-defined set.
    /// </summary>
    internal static class Catalog
    {
        public const string SummarySet = "Inventor Summary Information";
        public const string DocSummarySet = "Inventor Document Summary Information";
        public const string TrackingSet = "Design Tracking Properties";
        public const string CustomSet = "Inventor User Defined Properties";

        public static readonly string[] Tabs = { "Summary", "Project", "Status" };

        public static readonly IReadOnlyList<KnownProperty> All = Build();

        private static List<KnownProperty> Build()
        {
            var list = new List<KnownProperty>();
            void Add(string tab, string set, FieldKind kind, params string[] names)
            {
                foreach (string n in names) list.Add(new KnownProperty { Tab = tab, Set = set, Name = n, Kind = kind });
            }

            Add("Summary", SummarySet, FieldKind.Text, "Title", "Subject", "Author");
            Add("Summary", DocSummarySet, FieldKind.Text, "Manager", "Company", "Category");
            Add("Summary", SummarySet, FieldKind.Text, "Keywords", "Comments");
            Add("Project", TrackingSet, FieldKind.Text, "Part Number", "Stock Number", "Description");
            Add("Project", SummarySet, FieldKind.Text, "Revision Number");
            Add("Project", TrackingSet, FieldKind.Text, "Project", "Designer", "Engineer", "Authority", "Cost Center", "Vendor", "Catalog Web Link");
            Add("Project", TrackingSet, FieldKind.Date, "Creation Time");
            Add("Project", TrackingSet, FieldKind.Number, "Cost");
            Add("Status", TrackingSet, FieldKind.Text, "Checked By");
            Add("Status", TrackingSet, FieldKind.Date, "Date Checked");
            Add("Status", TrackingSet, FieldKind.Text, "Engr Approved By");
            Add("Status", TrackingSet, FieldKind.Date, "Engr Date Approved");
            Add("Status", TrackingSet, FieldKind.Text, "Mfg Approved By");
            Add("Status", TrackingSet, FieldKind.Date, "Mfg Date Approved");
            Add("Status", TrackingSet, FieldKind.Text, "User Status");
            return list;
        }

        public static KnownProperty Find(string name) =>
            All.FirstOrDefault(p => string.Equals(p.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
