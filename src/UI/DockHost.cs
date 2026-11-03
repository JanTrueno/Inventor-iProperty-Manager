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
        private const string WindowName = "IPropertyManager.DockableWindow";
        private const int SettleMs = 150;

        private sealed class HostControl : UserControl
        {
            public HostControl() { Dock = DockStyle.Fill; }
        }

        private static Inventor.Application _app;
        private static DockableWindow _window;
        private static HostControl _host;
        private static PropertiesPanel _panel;
        private static UserInputEvents _input;
        private static ApplicationEvents _appEvents;
        private static readonly Timer _settle = new Timer { Interval = SettleMs };
        private static bool _selectionDue, _valuesDue;

        public static void Init(Inventor.Application app, string clientGuid)
        {
            _app = app;
            try { app.UserInterfaceManager.DockableWindows[WindowName].Delete(); } catch { /* none left from before */ }

            _host = new HostControl();
            _ = _host.Handle; // make sure the native window exists before Inventor adopts it
            _window = app.UserInterfaceManager.DockableWindows.Add(clientGuid, WindowName, "iProperties");
            _window.ShowVisibilityCheckBox = true;   // listed under View > User Interface like Inventor's own panels
            _window.SetMinimumSize(220, 200);
            _window.AddChild(_host.Handle.ToInt32());
            // Dock on the right only the very first time; after that Inventor restores wherever the user put it.
            if (FirstRun()) _window.DockingState = DockingStateEnum.kDockRight;
            else _window.DockingState = DockingStateEnum.kDockLastKnown;

            _panel = new PropertiesPanel(app, Theme.Detect(app));
            _host.Controls.Add(_panel);

            _settle.Tick += OnSettled;
            _input = app.CommandManager.UserInputEvents;
            _input.OnSelect += OnSelect;
            _input.OnUnSelect += OnUnSelect;
            _appEvents = app.ApplicationEvents;
            _appEvents.OnActivateDocument += OnActivateDocument;
            _appEvents.OnCloseDocument += OnCloseDocument;
            _appEvents.OnDocumentChange += OnDocumentChange;

            Schedule(selection: true);
        }

        public static bool Visible
        {
            get { try { return _window?.Visible == true; } catch { return false; } }
            set
            {
                if (_window == null) return;
                _window.Visible = value;
                if (value) Schedule(selection: true);
            }
        }

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
            }
            catch { }
            // The window is not deleted: Inventor saves its position and size on exit, and deleting it would discard
            // that. A leftover from an unload in the add-in manager is removed by Init on the next load.
            try { _host?.Dispose(); } catch { }
            _window = null; _host = null; _panel = null; _input = null; _appEvents = null; _app = null;
        }

        /// <summary>True once per user: leaves a marker so later starts keep the user's placement.</summary>
        private static bool FirstRun()
        {
            string marker = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "IPropertyManager", "placed");
            try
            {
                if (System.IO.File.Exists(marker)) return false;
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(marker));
                System.IO.File.WriteAllText(marker, "");
            }
            catch { /* worst case it docks right again */ }
            return true;
        }

        // ----- events -------------------------------------------------------------------------------------------

        private static void OnSelect(ObjectsEnumerator justSelected, ref ObjectCollection moreSelected, SelectionDeviceEnum device,
            Inventor.Point modelPosition, Point2d viewPosition, Inventor.View view) => Schedule(selection: true);

        private static void OnUnSelect(ObjectsEnumerator unSelected, SelectionDeviceEnum device,
            Inventor.Point modelPosition, Point2d viewPosition, Inventor.View view) => Schedule(selection: true);

        private static void OnActivateDocument(_Document doc, EventTimingEnum timing, NameValueMap context, out HandlingCodeEnum handling)
        {
            handling = HandlingCodeEnum.kEventNotHandled;
            if (timing == EventTimingEnum.kAfter) Schedule(selection: true);
        }

        private static void OnCloseDocument(_Document doc, string fullName, EventTimingEnum timing, NameValueMap context, out HandlingCodeEnum handling)
        {
            handling = HandlingCodeEnum.kEventNotHandled;
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
            if (_panel == null || !Visible) return;   // catch up when shown
            try
            {
                if (selection) _panel.FollowSelection();
                else if (values) _panel.ReloadValues();
            }
            catch (Exception ex) { Log.Write("Refreshing the panel failed: " + ex); }
        }
    }
}
