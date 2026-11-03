using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IPropertyManager.Core
{
    /// <summary>A row of the panel: the iProperty name as typed. Set and kind come from the catalog when known.</summary>
    internal sealed class FieldSpec
    {
        public string Name { get; set; } = "";
        public string Set { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public FieldKind Kind { get; set; } = FieldKind.Text;

        public static FieldSpec For(string name) => new FieldSpec { Name = name }.Normalize();

        /// <summary>Fills Set and Kind from the catalog; unknown names are custom text properties.</summary>
        public FieldSpec Normalize()
        {
            Name = (Name ?? "").Trim();
            KnownProperty k = Catalog.Find(Name);
            if (k != null) { Name = k.Name; Set = k.Set; Kind = k.Kind; }
            else if (string.IsNullOrWhiteSpace(Set)) Set = Catalog.CustomSet;
            return this;
        }

        public bool IsReadOnly => Catalog.Find(Name)?.ReadOnly == true;
    }

    /// <summary>
    /// The rows the user added in the panel, remembered between sessions in %APPDATA%\IPropertyManager\fields.json.
    /// There is no settings dialog; the panel adds and removes rows itself.
    /// </summary>
    internal static class FieldStore
    {
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true };

        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IPropertyManager", "fields.json");

        private static readonly string[] Starters = { "Part Number", "Description", "Revision Number" };

        public static List<FieldSpec> Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    List<FieldSpec> list = JsonSerializer.Deserialize<List<FieldSpec>>(File.ReadAllText(FilePath), Json);
                    if (list != null)
                        return list.Where(f => !string.IsNullOrWhiteSpace(f?.Name)).Select(f => f.Normalize()).ToList();
                }
            }
            catch (Exception ex) { Log.Write("Reading " + FilePath + " failed: " + ex.Message); }
            return Starters.Select(FieldSpec.For).ToList();
        }

        public static void Save(IEnumerable<FieldSpec> fields)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, JsonSerializer.Serialize(fields.ToList(), Json));
            }
            catch (Exception ex) { Log.Write("Writing " + FilePath + " failed: " + ex.Message); }
        }
    }
}
