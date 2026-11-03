using System;
using System.Windows.Forms;
using Inventor;
using IPropertyManager.Core;

namespace IPropertyManager.UI
{
    /// <summary>
    /// Owns the dockable "iProperties" window and keeps the panel in it in step with Inventor: the active document, the
    /// selection, and changes made elsewhere. Events are collected and handled once after a short pause, so a box
    /// selection of many parts is read once rather than per part.
    /// </summary>
    internal static class DockHost
    {
        // Inventor saves the window's place under this name; changing it starts the user's layout over.
        private const string WindowName = "IPropertyManager.PropertiesPanel";
        private const int SettleMs = 150;

        /// <summary>The borderless top-level form that Inventor re-parents into the dockable window.</summary>
        private sealed class HostForm : Form
        {
            public HostForm()
            {
                FormBorderStyle = FormBorderStyle.None;
                ShowInTaskbar = false;
                StartPosition = FormStartPosition.Manual;
                Location = new System.Drawing.Point(-32000, -32000);   // never flashes on screen before it is adopted
            }
        }

        private static Inventor.Application _app;
        private static DockableWindow _window;
        private static HostForm _host;
        private static PropertiesPanel _panel;
        private static UserInputEvents _input;
        private static ApplicationEvents _appEvents;
        private static DockableWindowsEvents _windowEvents;
        private static readonly Timer _settle = new Timer { Interval = SettleMs };
        private static bool _selectionDue, _valuesDue;

        public static void Init(Inventor.Application app, string clientGuid)
        {
            _app = app;
            // Placement is left to Inventor's own layout, which restores the window per environment by itself; setting a
            // docking state here overrides it. A window still there from an unload in this session is reused, and
            // Shutdown leaves it intact so Inventor can save where it was.
            try { _window = app.UserInterfaceManager.DockableWindows[WindowName]; }
            catch { _window = app.UserInterfaceManager.DockableWindows.Add(clientGuid, WindowName, "iProperties"); }
            _window.ShowVisibilityCheckBox = true;   // listed under View > User Interface like Inventor's own panels
            _window.DisabledDockingStates = DockingStateEnum.kDockTop | DockingStateEnum.kDockBottom;   // a side column only

            _host = new HostForm();
            _panel = new PropertiesPanel(app, Theme.Detect(app));
            _host.Controls.Add(_panel);
            _host.Show(new InventorWindow(app.MainFrameHWND));   // owned by Inventor's frame before it is docked
            _window.AddChild(_host.Handle.ToInt64());

            _settle.Tick += OnSettled;
            _input = app.CommandManager.UserInputEvents;
            _input.OnSelect += OnSelect;
            _input.OnUnSelect += OnUnSelect;
            _appEvents = app.ApplicationEvents;
            _appEvents.OnActivateDocument += OnActivateDocument;
            _appEvents.OnCloseDocument += OnCloseDocument;
            _appEvents.OnDocumentChange += OnDocumentChange;
            _appEvents.OnApplicationOptionChange += OnOptionChange;
            _windowEvents = app.UserInterfaceManager.DockableWindows.Events;
            _windowEvents.OnShow += OnWindowShow;
            _windowEvents.OnHide += OnWindowHide;

            Schedule(selection: true);
        }

        private static bool Visible
        {
            get { try { return _window?.Visible == true; } catch { return false; } }
        }

        // ----- open at start -------------------------------------------------------------------------------------
        // Inventor restores the window's place but brings it back hidden, so whether the user wants it open is kept
        // here: turning it off or on (View > User Interface, or its close button) while a document is open is
        // remembered, and the first document of a session opens the panel unless it was turned off.

        private static void OnWindowShow(DockableWindow window, EventTimingEnum timing, NameValueMap context, out HandlingCodeEnum handling)
        {
            handling = HandlingCodeEnum.kEventNotHandled;
            if (timing != EventTimingEnum.kAfter || !IsOurs(window)) return;
            if (_shownThisSession && HasDocument()) WantOpen = true;
            Schedule(selection: true);
        }

        private static void OnWindowHide(DockableWindow window, EventTimingEnum timing, NameValueMap context, out HandlingCodeEnum handling)
        {
            handling = HandlingCodeEnum.kEventNotHandled;
            // Inventor also hides it on the start screen and when the last document closes; only a hide with a document
            // open, outside a close, is the user's choice.
            if (timing == EventTimingEnum.kAfter && IsOurs(window) && _shownThisSession && !_closing && HasDocument()) WantOpen = false;
        }

        private static bool IsOurs(DockableWindow window)
        {
            try { return window?.InternalName == WindowName; } catch { return false; }
        }

        private static bool HasDocument()
        {
            try { return _app?.ActiveDocument != null; } catch { return false; }
        }

        private static string OpenFlagPath => System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "IPropertyManager", "panel-closed");

        private static bool _shownThisSession, _closing;

        private static bool WantOpen
        {
            get { try { return !System.IO.File.Exists(OpenFlagPath); } catch { return true; } }
            set
            {
                try
                {
                    if (value) System.IO.File.Delete(OpenFlagPath);
                    else
                    {
                        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(OpenFlagPath));
                        System.IO.File.WriteAllText(OpenFlagPath, "");
                    }
                }
                catch (Exception ex) { Log.Write("Remembering the panel state failed: " + ex.Message); }
            }
        }

        private static void ShowOnFirstDocument()
        {
            if (_shownThisSession || _window == null) return;
            _shownThisSession = true;
            try { if (WantOpen && !_window.Visible) _window.Visible = true; } catch { }
        }

        /// <summary>
        /// Stops listening to Inventor. The window and its content are left alone so Inventor can save their placement
        /// after the add-in has unloaded; a leftover from an unload in the add-in manager is replaced by Init.
        /// </summary>
        public static void Shutdown()
        {
            _settle.Stop();
            _settle.Tick -= OnSettled;
            try
            {
                _input.OnSelect -= OnSelect;
                _input.OnUnSelect -= OnUnSelect;
                _appEvents.OnActivateDocument -= OnActivateDocument;
                _appEvents.OnCloseDocument -= OnCloseDocument;
                _appEvents.OnDocumentChange -= OnDocumentChange;
                _appEvents.OnApplicationOptionChange -= OnOptionChange;
                _windowEvents.OnShow -= OnWindowShow;
                _windowEvents.OnHide -= OnWindowHide;
            }
            catch { }
            _input = null; _appEvents = null; _windowEvents = null; _app = null;
        }

        // ----- events -------------------------------------------------------------------------------------------

        private static void OnSelect(ObjectsEnumerator justSelected, ref ObjectCollection moreSelected, SelectionDeviceEnum device,
            Inventor.Point modelPosition, Point2d viewPosition, Inventor.View view) => Schedule(selection: true);

        private static void OnUnSelect(ObjectsEnumerator unSelected, SelectionDeviceEnum device,
            Inventor.Point modelPosition, Point2d viewPosition, Inventor.View view) => Schedule(selection: true);

        private static void OnActivateDocument(_Document doc, EventTimingEnum timing, NameValueMap context, out HandlingCodeEnum handling)
        {
            handling = HandlingCodeEnum.kEventNotHandled;
            if (timing != EventTimingEnum.kAfter) return;
            ShowOnFirstDocument();
            Schedule(selection: true);
        }

        private static void OnCloseDocument(_Document doc, string fullName, EventTimingEnum timing, NameValueMap context, out HandlingCodeEnum handling)
        {
            handling = HandlingCodeEnum.kEventNotHandled;
            _closing = timing == EventTimingEnum.kBefore;
            if (timing != EventTimingEnum.kAfter) return;
            bool none;
            try { none = _app.Documents.VisibleDocuments.Count == 0; } catch { none = true; }
            if (none) _panel?.Clear();
            else Schedule(selection: true);
        }

        private static void OnDocumentChange(_Document doc, EventTimingEnum timing, CommandTypesEnum reasons, NameValueMap context, out HandlingCodeEnum handling)
        {
            handling = HandlingCodeEnum.kEventNotHandled;
            if (timing == EventTimingEnum.kAfter) Schedule(selection: false);
        }

        // The theme is changed in Application Options; there is no theme event, so it is checked on every refresh too.
        private static void OnOptionChange(EventTimingEnum timing, NameValueMap context, out HandlingCodeEnum handling)
        {
            handling = HandlingCodeEnum.kEventNotHandled;
            if (timing == EventTimingEnum.kAfter) Schedule(selection: true);
        }

        /// <summary>Rebuilds the panel in the new colours when the user has switched Inventor's theme.</summary>
        private static void FollowTheme()
        {
            string name = Theme.ActiveName(_app);
            if (name.Length == 0 || name == _panel.ThemeName || _panel.HasEdits) return;
            var old = _panel;
            _host.SuspendLayout();
            Log.Write("Theme changed to " + name);
            _panel = new PropertiesPanel(_app, Theme.Detect(_app));
            _host.Controls.Add(_panel);
            _host.Controls.Remove(old);
            _host.ResumeLayout();
            old.Dispose();
        }

        private static void Schedule(bool selection)
        {
            if (selection) _selectionDue = true; else _valuesDue = true;
            _settle.Stop();
            _settle.Start();
        }

        private static void OnSettled(object sender, EventArgs e)
        {
            _settle.Stop();
            bool selection = _selectionDue, values = _valuesDue;
            _selectionDue = _valuesDue = false;
            if (_panel == null || _app == null || !Visible) return;   // catch up when shown
            try
            {
                string before = _panel.ThemeName;
                FollowTheme();
                if (_panel.ThemeName != before) selection = true;   // a new panel shows nothing yet
                if (selection) _panel.FollowSelection();
                else if (values) _panel.ReloadValues();
            }
            catch (Exception ex) { Log.Write("Refreshing the panel failed: " + ex); }
        }
    }
}
