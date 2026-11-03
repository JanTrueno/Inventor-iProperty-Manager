# iProperty Manager for Inventor

A docked **iProperties** panel for Autodesk Inventor 2025, styled like Inventor's own property panels.

- Shows the iProperties of the active part, assembly or drawing (IDW/DWG), or of the selected components. In a drawing, selecting a view or an edge shows the model behind it.
- Several components selected: shared values are shown, differing ones show *(multiple values)* and are left alone unless you type over them.
- **+** adds a row from the Summary, Project or Status iProperties, or any custom name (custom properties are created on save). Right-click a row label to move or remove it.
- **Material** of the selected parts can be changed from the active material library; **Mass** shows the weight (the total when several are shown).
- The buttons next to the name copy it, or open its folder in Explorer.
- **Save** (or Enter / Ctrl+S) writes everything as one undo step; **Revert** (or Esc) discards the edits. Read-only documents (library, Content Center) are skipped.

Turn it on or off with **View > User Interface > iProperties** (there is no ribbon button). It opens with the first
document of a session unless it was turned off. Inventor itself remembers where it is docked; a split under the Model
browser is not always kept across restarts, the sides and tabs are.

## Build and install

Requires the .NET 8 SDK and Inventor 2025. Close Inventor, then:

```
powershell -ExecutionPolicy Bypass -File install.ps1
```

`install.ps1 -Remove` uninstalls. `build-installer.ps1` builds `dist\IPropertyManager-Setup-<version>.exe` with Inno Setup 6.

Diagnostics go to `%LOCALAPPDATA%\IPropertyManager\log.txt`; the panel's rows are kept in `%APPDATA%\IPropertyManager\fields.json`.
