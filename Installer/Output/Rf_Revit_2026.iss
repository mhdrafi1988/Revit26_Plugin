; ============================================================
;  Rf_Revit_2026 - Inno Setup Script
;  Rafi | Revit 2026 Plugin Installer
; ============================================================

; --- UPDATE THIS LINE each time you build ---
; It is the date-stamped folder inside bin\Release\net8.0-windows\
#define BuildStamp "09-06-000"

#define MyAppName      "Rf Revit 2026 Plugin"
#define MyAppVersion   "4.0.0"
#define MyAppPublisher "Rafi"
#define MyAppExeName   ""
#define RevitAddinDir  "{commonappdata}\Autodesk\Revit\Addins\2026"
#define PluginSubDir   "Rf_Revit_2026"
#define SourceBase     "C:\Users\Rafi\source\repos\Revit26_Plugin\bin\Release\net8.0-windows\" + BuildStamp + "\Release\net8.0-windows"

[Setup]
AppId={{a4e8c4c7-9f8b-4a6a-9d31-532f8c980d41}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={#RevitAddinDir}\{#PluginSubDir}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=C:\Users\Rafi\source\repos\Revit26_Plugin\Installer\Output
OutputBaseFilename=Rf_Revit_2026_Setup_v{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\SloperPro.Modelling.dll
WizardStyle=modern
; Optional: set your own icon below (must be a .ico file)
; SetupIconFile=C:\Path\To\YourIcon.ico

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

; ============================================================
;  FILES — all DLLs + deps.json go into the plugin subfolder
; ============================================================
[Files]
; --- Plugin assemblies: shared core + the two add-ins ---
Source: "{#SourceBase}\SloperPro.Core.dll";               DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\SloperPro.Core.deps.json";         DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\SloperPro.Modelling.dll";          DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\SloperPro.Modelling.deps.json";    DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\SloperPro.Documentation.dll";      DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\SloperPro.Documentation.deps.json";DestDir: "{app}"; Flags: ignoreversion

; --- CommunityToolkit ---
Source: "{#SourceBase}\CommunityToolkit.Mvvm.dll";   DestDir: "{app}"; Flags: ignoreversion

; --- ControlzEx ---
Source: "{#SourceBase}\ControlzEx.dll";              DestDir: "{app}"; Flags: ignoreversion

; --- EPPlus ---
Source: "{#SourceBase}\EPPlus.dll";                  DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\EPPlus.Interfaces.dll";       DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\EPPlus.System.Drawing.dll";   DestDir: "{app}"; Flags: ignoreversion

; --- MahApps.Metro core ---
Source: "{#SourceBase}\MahApps.Metro.dll";           DestDir: "{app}"; Flags: ignoreversion

; --- MahApps IconPacks ---
Source: "{#SourceBase}\MahApps.Metro.IconPacks.dll";                      DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.Core.dll";                 DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.BootstrapIcons.dll";       DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.BoxIcons.dll";             DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.BoxIcons2.dll";            DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.CircumIcons.dll";          DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.Codicons.dll";             DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.Coolicons.dll";            DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.Entypo.dll";               DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.EvaIcons.dll";             DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.FeatherIcons.dll";         DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.FileIcons.dll";            DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.Fontaudio.dll";            DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.FontAwesome.dll";          DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.FontAwesome5.dll";         DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.FontAwesome6.dll";         DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.Fontisto.dll";             DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.ForkAwesome.dll";          DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.GameIcons.dll";            DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.Ionicons.dll";             DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.JamIcons.dll";             DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.KeyruneIcons.dll";         DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.Lucide.dll";               DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.Material.dll";             DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.MaterialDesign.dll";       DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.MaterialLight.dll";        DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.MemoryIcons.dll";          DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.Microns.dll";              DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.MingCuteIcons.dll";        DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.Modern.dll";               DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.MynaUIIcons.dll";          DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.Octicons.dll";             DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.PhosphorIcons.dll";        DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.PicolIcons.dll";           DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.PixelartIcons.dll";        DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.RadixIcons.dll";           DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.RemixIcon.dll";            DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.RPGAwesome.dll";           DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.SimpleIcons.dll";          DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.Typicons.dll";             DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.Unicons.dll";              DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.VaadinIcons.dll";          DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.WeatherIcons.dll";         DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MahApps.Metro.IconPacks.Zondicons.dll";            DestDir: "{app}"; Flags: ignoreversion

; --- MaterialDesign ---
Source: "{#SourceBase}\MaterialDesignColors.dll";         DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\MaterialDesignThemes.Wpf.dll";     DestDir: "{app}"; Flags: ignoreversion

; --- MIConvexHull (used by RoofRidgeLines) ---
Source: "{#SourceBase}\MIConvexHull.dll";                 DestDir: "{app}"; Flags: ignoreversion

; --- Microsoft.Extensions ---
Source: "{#SourceBase}\Microsoft.Extensions.Configuration.Abstractions.dll";  DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\Microsoft.Extensions.Configuration.dll";               DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\Microsoft.Extensions.Configuration.FileExtensions.dll";DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\Microsoft.Extensions.Configuration.Json.dll";          DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\Microsoft.Extensions.FileProviders.Abstractions.dll";  DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\Microsoft.Extensions.FileProviders.Physical.dll";      DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\Microsoft.Extensions.FileSystemGlobbing.dll";          DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\Microsoft.Extensions.Primitives.dll";                  DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceBase}\Microsoft.IO.RecyclableMemoryStream.dll";              DestDir: "{app}"; Flags: ignoreversion

; --- XAML Behaviors ---
Source: "{#SourceBase}\Microsoft.Xaml.Behaviors.dll";    DestDir: "{app}"; Flags: ignoreversion

; ============================================================
;  .ADDIN MANIFEST — written fresh by the installer
;  Points to {commonappdata}\Autodesk\Revit\Addins\2026\Rf_Revit_2026\
;  Placed directly in the Addins\2026 root (NOT inside the subfolder)
; ============================================================
[INI]
; Nothing here — we use [Code] to write the XML manifest

[InstallDelete]
; Plugin 3.x shipped one Revit26_Plugin.dll; remove it so an upgrade leaves no stale copy.
Type: files; Name: "{app}\Revit26_Plugin.dll"
Type: files; Name: "{app}\Revit26_Plugin.deps.json"

[Dirs]
Name: "{#RevitAddinDir}";           Permissions: everyone-full
Name: "{#RevitAddinDir}\{#PluginSubDir}"; Permissions: everyone-full

; ============================================================
;  CODE — writes the .addin manifest on install, deletes on uninstall
; ============================================================
[Code]

const
  ModellingAddinFileName = 'Rf_Revit_2026.addin';
  DocumentationAddinFileName = 'Rf_Revit_2026_Documentation.addin';

function GetAddinFilePath(FileName: String): String;
begin
  Result := ExpandConstant('{commonappdata}\Autodesk\Revit\Addins\2026\' + FileName);
end;

function GetDllInstallPath(DllName: String): String;
begin
  Result := ExpandConstant('{commonappdata}\Autodesk\Revit\Addins\2026\{#PluginSubDir}\' + DllName);
end;

// Writes one .addin manifest. Both add-ins use the class Revit26_Plugin.App, each in its own assembly.
procedure WriteAddinManifest(FileName, AddinName, DllName, AddinId: String);
var
  Lines: TArrayOfString;
  FilePath: String;
begin
  FilePath := GetAddinFilePath(FileName);
  SetArrayLength(Lines, 10);
  Lines[0] := '<?xml version="1.0" encoding="utf-8"?>';
  Lines[1] := '<RevitAddIns>';
  Lines[2] := '  <AddIn Type="Application">';
  Lines[3] := '    <Name>' + AddinName + '</Name>';
  Lines[4] := '    <Assembly>' + GetDllInstallPath(DllName) + '</Assembly>';
  Lines[5] := '    <AddInId>' + AddinId + '</AddInId>';
  Lines[6] := '    <FullClassName>Revit26_Plugin.App</FullClassName>';
  Lines[7] := '    <VendorId>RAFI</VendorId>';
  Lines[8] := '    <VendorDescription>By Rafi</VendorDescription>';
  Lines[9] := '  </AddIn>';
  // Note: closing tag written separately to avoid Inno string limit issues
  SaveStringsToFile(FilePath, Lines, False);
  // Append the closing tag
  SaveStringToFile(FilePath, '</RevitAddIns>', True);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // SloperPro Modelling keeps the add-in id the single plugin used before 4.0.0.
    WriteAddinManifest(ModellingAddinFileName, 'SloperPro Modelling', 'SloperPro.Modelling.dll', 'a4e8c4c7-9f8b-4a6a-9d31-532f8c980d41');
    WriteAddinManifest(DocumentationAddinFileName, 'SloperPro Documentation', 'SloperPro.Documentation.dll', 'fcefe864-ac11-49e9-8936-d3349b645b0f');
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  FilePath: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    FilePath := GetAddinFilePath(ModellingAddinFileName);
    if FileExists(FilePath) then
      DeleteFile(FilePath);
    FilePath := GetAddinFilePath(DocumentationAddinFileName);
    if FileExists(FilePath) then
      DeleteFile(FilePath);
  end;
end;
