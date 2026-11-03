using System;
using System.Runtime.InteropServices;
using Inventor;
using IPropertyManager.UI;

namespace IPropertyManager
{
    /// <summary>
    /// Inventor add-in entry point: a dockable "iProperties" panel for the active or selected documents. It has no
    /// ribbon button; it is shown or hidden from View > User Interface like Inventor's own panels.
    /// </summary>
    [ComVisible(true)]
    [Guid(ClientId)]
    [ProgId("IPropertyManager.StandardAddInServer")]
    public class StandardAddInServer : ApplicationAddInServer
    {
        public const string ClientId = "7c2e9a41-5b8d-4f36-a1e0-93d6b4c8f215";

        public object Automation => null;

        public void Activate(ApplicationAddInSite addInSiteObject, bool firstTime)
        {
            DockHost.Init(addInSiteObject.Application, "{" + ClientId + "}");
        }

        public void Deactivate()
        {
            try { DockHost.Shutdown(); } catch { }
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        public void ExecuteCommand(int commandID) { }
    }
}
