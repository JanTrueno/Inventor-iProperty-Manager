using System;
using System.Runtime.InteropServices;
using Inventor;
using IPropertyManager.UI;

namespace IPropertyManager
{
    /// <summary>
    /// Inventor add-in entry point: a dockable "iProperties" panel for the active or selected documents, and a button
    /// on the Tools tab (part, assembly and drawing) that shows or hides it.
    /// </summary>
    [ComVisible(true)]
    [Guid(ClientId)]
    [ProgId("IPropertyManager.StandardAddInServer")]
    public class StandardAddInServer : ApplicationAddInServer
    {
        public const string ClientId = "7c2e9a41-5b8d-4f36-a1e0-93d6b4c8f215";
        private const string PanelName = "IPropertyManager.RibbonPanel";
        private static readonly string[] Ribbons = { "Part", "Assembly", "Drawing" };

        private Inventor.Application _app;
        private ButtonDefinition _button;
        private UserInterfaceEvents _uiEvents;

        public object Automation => null;

        public void Activate(ApplicationAddInSite addInSiteObject, bool firstTime)
        {
            _app = addInSiteObject.Application;
            string clientGuid = "{" + ClientId + "}";

            // Inventor's own iProperties icon, so the button reads as what it is
            object small = Icon("AppiPropertiesWrapperCmd", false), large = Icon("AppiPropertiesWrapperCmd", true);
            _button = _app.CommandManager.ControlDefinitions.AddButtonDefinition(
                "iProperty Panel", "IPropertyManager.TogglePanel", CommandTypesEnum.kQueryOnlyCmdType, clientGuid,
                "Show or hide the iProperties panel",
                "iProperty Panel\nA docked panel to view and edit the iProperties and material of the active document or the selected components.",
                small ?? Type.Missing, large ?? Type.Missing, ButtonDisplayEnum.kDisplayTextInLearningMode);
            _button.OnExecute += OnExecute;

            _uiEvents = _app.UserInterfaceManager.UserInterfaceEvents;
            _uiEvents.OnResetRibbonInterface += OnResetRibbon;

            DockHost.Init(_app, clientGuid);
            AddToRibbon();
        }

        public void Deactivate()
        {
            try { _uiEvents.OnResetRibbonInterface -= OnResetRibbon; } catch { }
            try { _button.OnExecute -= OnExecute; } catch { }
            try { DockHost.Shutdown(); } catch { }
            try { _button?.Delete(); } catch { }
            _button = null;
            _uiEvents = null;
            _app = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        public void ExecuteCommand(int commandID) { }

        private object Icon(string command, bool large)
        {
            try
            {
                ControlDefinition def = _app.CommandManager.ControlDefinitions[command];
                dynamic d = def;   // keeps stdole out of the references
                return large ? (object)d.LargeIcon : (object)d.StandardIcon;
            }
            catch { return null; }
        }

        // ----- ribbon -------------------------------------------------------------------------------------------

        private void OnResetRibbon(NameValueMap context) => AddToRibbon();

        private void AddToRibbon()
        {
            foreach (string name in Ribbons)
            {
                try
                {
                    RibbonTab tab = _app.UserInterfaceManager.Ribbons[name].RibbonTabs["id_TabTools"];
                    RibbonPanel panel;
                    try { panel = tab.RibbonPanels[PanelName]; }
                    catch { panel = tab.RibbonPanels.Add("iProperties", PanelName, "{" + ClientId + "}", "", false); }
                    try { panel.CommandControls.AddButton(_button, true, true, "", false); } catch { /* already added */ }
                }
                catch (Exception ex) { Core.Log.Write("Adding the button to the " + name + " ribbon failed: " + ex.Message); }
            }
        }

        private void OnExecute(NameValueMap context) => DockHost.Visible = !DockHost.Visible;
    }
}
