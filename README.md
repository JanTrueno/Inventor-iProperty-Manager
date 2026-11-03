# Inventor iProperty Manager

Small Autodesk Inventor add-in that puts the iProperties you use most in a docked panel, so you can read and change them
without opening the iProperties dialog. The panel is built in Inventor's own style and follows your theme. It works in
parts, assemblies and drawings.

## Features

- **Always at hand**: shows the iProperties of the active part, assembly or drawing (IDW/DWG), or of the components you select.
- **Drawings**: select a view, or an edge in a view, to see and edit the part or assembly behind it, including parts inside subassemblies.
- **Several at once**: select more components and edit them together. Shared values are shown, and differing ones show *(multiple values)* and are left alone unless you type over them.
- **Your own rows**: **+** adds a row from the Summary, Project or Status iProperties, grouped like Inventor's iProperties dialog, or any custom name. Custom properties are created when you save. Right-click a row label to move it or remove it from the panel.
- **Material and mass**: change the material of the selected parts from the active material library, and see their mass (the total when several are selected).
- **Quick actions**: copy the file name, or open its folder in Explorer.
- **One undo step**: **Save** writes all changes to all shown documents as a single undo. **Enter** or **Tab** in a field also saves, and Tab then moves on to the next field. **Revert** or **Esc** discards the edits. Read-only documents (library, Content Center) are skipped.

## Requirements

- Windows with Autodesk Inventor 2025 (the add-in runs on .NET 8)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) to build it

## Building

The project references `Autodesk.Inventor.Interop.dll` from the Inventor installation at
`C:\Program Files\Autodesk\Inventor 2025\Bin\Public Assemblies`. If Inventor is installed somewhere else, change the `HintPath` in `IPropertyManager.csproj` to match.

```powershell
git clone https://github.com/JanTrueno/Inventor-iProperty-Manager.git
cd Inventor-iProperty-Manager
dotnet build IPropertyManager.csproj -c Release
```

The output goes to `bin\Release\net8.0-windows\`: `IPropertyManager.dll`, `.pdb` and `.deps.json`. You can also open `IPropertyManager.csproj` in Visual Studio 2022 and build it there.

## Install

The easiest way is the installer: download `IPropertyManager-Setup-<version>.exe` from [Releases](https://github.com/JanTrueno/Inventor-iProperty-Manager/releases) and run it.
It installs for the current user without admin rights, and you can uninstall it from Windows **Settings → Apps**.

### Building the installer

You need [Inno Setup 6](https://jrsoftware.org/isinfo.php). Install it with `winget install JRSoftware.InnoSetup`, then run:

```powershell
powershell -ExecutionPolicy Bypass -File build-installer.ps1
```

This builds the add-in and writes `dist\IPropertyManager-Setup-<version>.exe`. The version comes from `deploy\PackageContents.xml`, or you can pass `-Version 1.2.0`.

### Installing from source

Close Inventor (it locks the add-in DLL while running), then run:

```powershell
powershell -ExecutionPolicy Bypass -File install.ps1
```

This builds the add-in and installs it as an app bundle in `%APPDATA%\Autodesk\ApplicationPlugins\IPropertyManager.bundle`.

- `install.ps1 -SkipBuild` installs the last build without building again.
- `install.ps1 -Remove` uninstalls it.

## Usage

1. Start Inventor and open a part, assembly or drawing. The **iProperties** panel opens with the first document.
   It has no ribbon button: turn it on or off with **View → User Interface → iProperties**, like Inventor's own panels.
2. Dock it where you like. Inventor remembers the place; a split right under the Model browser is not always kept
   across restarts, but the sides and tabs (for example next to iLogic) are.
3. Select components, or a view or edge in a drawing, to show their iProperties.
4. Type the new values and press **Tab**, **Enter** or **Save**.

The rows you add are kept in `%APPDATA%\IPropertyManager\fields.json`. A log is written to `%LOCALAPPDATA%\IPropertyManager\log.txt`.

## License

GPL-3.0, see [LICENSE](LICENSE). The section chevrons come from Autodesk Weave (Apache-2.0), see [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).
