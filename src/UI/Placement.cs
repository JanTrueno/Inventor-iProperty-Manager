using System;
using System.IO;
using System.Text.Json;
using Inventor;
using IPropertyManager.Core;
using File = System.IO.File;

namespace IPropertyManager.UI
{
    /// <summary>
    /// Where the panel was when Inventor closed: docked side, the window it was docked against (e.g. under the Model
    /// browser), size, floating position and whether it was open. Kept in %APPDATA%\IPropertyManager\window.json because
    /// Inventor does not keep that for add-in windows between sessions.
    /// </summary>
    internal sealed class Placement
    {
        public string State { get; set; }
        public string DockedTo { get; set; }
        public int Left { get; set; }
        public int Top { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool Visible { get; set; } = true;

        private static string FilePath => System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "IPropertyManager", "window.json");

        public static void Save(DockableWindow w)
        {
            try
            {
                w.GetDockingState(out DockingStateEnum state, out object dockTo);
                var p = new Placement
                {
                    State = state.ToString(),
                    DockedTo = (dockTo as DockableWindow)?.InternalName,
                    Left = w.Left, Top = w.Top, Width = w.Width, Height = w.Height,
                    Visible = w.Visible
                };
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, JsonSerializer.Serialize(p, new JsonSerializerOptions { WriteIndented = true }));
                Log.Write("Saved panel placement: " + p.State + (p.DockedTo != null ? " of " + p.DockedTo : "") + ", " + p.Width + "x" + p.Height);
            }
            catch (Exception ex) { Log.Write("Saving the panel placement failed: " + ex.Message); }
        }

        /// <summary>The placement saved last time, or null on the first run.</summary>
        public static Placement Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                Placement p = JsonSerializer.Deserialize<Placement>(File.ReadAllText(FilePath));
                return p != null && Enum.TryParse(p.State, out DockingStateEnum _) ? p : null;
            }
            catch (Exception ex) { Log.Write("Reading the panel placement failed: " + ex.Message); return null; }
        }

        public void Apply(DockableWindow w, DockableWindow target)
        {
            var state = (DockingStateEnum)Enum.Parse(typeof(DockingStateEnum), State);
            if (state == DockingStateEnum.kFloat)
            {
                w.DockingState = DockingStateEnum.kFloat;
                if (Width > 0 && Height > 0) w.Move(Top, Left, Height, Width);
            }
            else
            {
                if (target != null) w.SetDockingState(state, target);
                else w.DockingState = state;
                if (Width > 0) try { w.Width = Width; } catch { }
                if (Height > 0) try { w.Height = Height; } catch { }
            }
            w.Visible = Visible;
        }

        public static DockableWindow Find(Inventor.Application app, string internalName)
        {
            if (string.IsNullOrEmpty(internalName)) return null;
            try { return app.UserInterfaceManager.DockableWindows[internalName]; } catch { return null; }
        }

        /// <summary>Internal name of the Model browser window, once Inventor has created it.</summary>
        public static string BrowserName(Inventor.Application app)
        {
            try
            {
                foreach (DockableWindow w in app.UserInterfaceManager.DockableWindows)
                {
                    string name = "", title = "";
                    try { name = w.InternalName; title = w.Title; } catch { }
                    if (string.Equals(name, "model", StringComparison.OrdinalIgnoreCase) || string.Equals(title, "Model", StringComparison.OrdinalIgnoreCase))
                        return name;
                }
            }
            catch { }
            return null;
        }
    }
}
