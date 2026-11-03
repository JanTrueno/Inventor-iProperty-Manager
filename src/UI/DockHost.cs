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
            // Inventor does not remember where an add-in's window was, and at start-up the Model browser it may be docked
            // against does not exist yet. So the placement saved on exit (or, the first time, bottom left under the Model
            // browser) is applied a moment later, retrying until the browser is there.
            _window.DockingState = DockingStateEnum.kDockLeft;
            _place.Tick += OnPlace;
            _place.Start();

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
            _place.Stop();
            _place.Tick -= OnPlace;
            if (_window != null && _placed) Placement.Save(_window);
            try { _window?.Delete(); } catch { }
            try { _host?.Dispose(); } catch { }
            _window = null; _host = null; _panel = null; _input = null; _appEvents = null; _app = null;
        }

        // ----- placement ----------------------------------------------------------------------------------------

        private static readonly Timer _place = new Timer { Interval = 1000 };
        private static int _placeTries;
        private static bool _placed;

        private static void OnPlace(object sender, EventArgs e)
        {
            if (_window == null) { _place.Stop(); return; }
            Placement saved = Placement.Load();
            DockableWindow target = Placement.Find(_app, saved != null ? saved.DockedTo : Placement.BrowserName(_app));
            bool needsTarget = saved == null || !string.IsNullOrEmpty(saved.DockedTo);
            if (needsTarget && target == null && ++_placeTries < 20) return;   // the browser is not there yet

            _place.Stop();
            try
            {
                if (saved != null) saved.Apply(_window, target);
                else if (target != null) _window.SetDockingState(DockingStateEnum.kDockBottom, target);
                Log.Write("Placed panel: " + (saved != null ? "saved " + saved.State : "default") +
                          (target != null ? " against " + target.InternalName : " (no window to dock against)"));
            }
            catch (Exception ex) { Log.Write("Placing the panel failed: " + ex.Message); }
            _placed = true;
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
