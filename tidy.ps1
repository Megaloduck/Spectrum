# tidy.ps1 -- run from the repo root (next to Spectrum.csproj).
# Pure file moves via `git mv`. Namespaces and x:Class names are NOT touched,
# so the build is unaffected (the SDK globs every .cs / .axaml automatically).
$ErrorActionPreference = 'Stop'

function Move-Group {
    param(
        [string]$Dest,
        [string]$Src,
        [string[]]$Names,
        [string[]]$Ext = @('.cs')
    )
    New-Item -ItemType Directory -Force -Path $Dest | Out-Null
    foreach ($n in $Names) {
        foreach ($e in $Ext) {
            git mv "$Src/$n$e" "$Dest/$n$e"
        }
    }
}

# ---------- Services ----------
# (folder is "ColorScience", not "Color": a future Spectrum.Services.Color
#  namespace would shadow Avalonia.Media.Color)
Move-Group 'Services/ColorScience' 'Services' @(
    'ColorMathService', 'ColorFormatService', 'ColorHarmonyService',
    'ColorNamingService', 'ColorBlindnessService', 'ContrastService', 'GradientService')

Move-Group 'Services/Imaging' 'Services' @(
    'ImageColorExtractionService', 'ImageSampler')

Move-Group 'Services/Storage' 'Services' @(
    'ILibraryStore', 'JsonLibraryStore', 'SqliteLibraryStore', 'PaletteLibraryService')

Move-Group 'Services/Files' 'Services' @(
    'PaletteImportService', 'PaletteExportService', 'PaletteBinaryExportService',
    'PngExportService', 'PaletteFileService', 'FilePickerHelper', 'ClipboardHelper')

Move-Group 'Services/Platform' 'Services' @(
    'ScreenPickerService', 'GlobalHotkeyService')

Move-Group 'Services/Settings' 'Services' @(
    'AppSettings', 'SettingsService', 'UpdateService')

# ---------- Models ----------
Move-Group 'Models/Dto' 'Models' @('PaletteSwatchDto', 'PaletteLibraryDto')

# ---------- ViewModels ----------
Move-Group 'ViewModels/Library' 'ViewModels' @(
    'PaletteLibraryViewModel', 'PaletteItemViewModel')

# ---------- Views ----------
Move-Group 'Views/Dialogs' 'Views' @(
    'ColorPickerDialog', 'CommandPaletteWindow', 'CompareWindow', 'ContrastMatrixWindow',
    'GradientStudioWindow', 'ImageEyedropperWindow', 'ScreenPickerWindow', 'SettingsWindow'
) @('.axaml', '.axaml.cs')

Move-Group 'Views/Workspaces' 'Views' @('DockedWorkspaceView')

git status --short
Write-Host "`nDone. Build to confirm, then commit the moves on their own."
