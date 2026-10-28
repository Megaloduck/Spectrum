using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Spectrum.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Spectrum.ViewModels
{
    /// <summary>
    /// Ctrl+4 · Export — format selection, the export preview and writing the
    /// palette to files (text, ASE / ACO, PNG / JPG swatch sheet). Palette save /
    /// load and library import / export are shared shell actions and stay on
    /// <see cref="MainWindowViewModel"/> / <see cref="PaletteLibraryViewModel"/>.
    /// </summary>
    public partial class ExportWorkspaceViewModel : ViewModelBase
    {
        /// <summary>Refreshes are coalesced so dragging a color/alpha slider does
        /// not re-render the sheet on every tick.</summary>
        private const int SheetRefreshDelayMs = 200;

        private readonly MainWindowViewModel _shell;
        private DispatcherTimer? _sheetTimer;

        public ExportWorkspaceViewModel(MainWindowViewModel shell) => _shell = shell;

        public Array ExportFormats { get; } = Enum.GetValues(typeof(ExportFormat));

        [ObservableProperty]
        private ExportFormat _selectedExportFormat = ExportFormat.Json;

        [ObservableProperty]
        private string _exportPreview = string.Empty;

        // ---------------- Swatch sheet preview (§6) ----------------
        // The sheet is rendered up front instead of only at save time, so the
        // Export workspace shows exactly the image the Save button writes.

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsSheetPreviewEmpty))]
        private IImage? _sheetPreview;

        [ObservableProperty]
        private string _sheetSizeLabel = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsPngSheet))]
        [NotifyPropertyChangedFor(nameof(IsJpgSheet))]
        [NotifyPropertyChangedFor(nameof(SaveSheetLabel))]
        private SheetImageFormat _sheetFormat = SheetImageFormat.Png;

        public bool IsSheetPreviewEmpty => SheetPreview is null;

        public bool IsPngSheet => SheetFormat == SheetImageFormat.Png;

        public bool IsJpgSheet => SheetFormat == SheetImageFormat.Jpg;

        public string SaveSheetLabel => SheetFormat == SheetImageFormat.Jpg ? "Save JPG…" : "Save PNG…";

        [RelayCommand]
        private void SelectSheetFormat(SheetImageFormat format) => SheetFormat = format;

        /// <summary>
        /// Renders the sheet for the current palette. Called when the Export
        /// workspace is entered and (debounced) whenever the palette changes —
        /// never from the constructor, because RenderTargetBitmap needs the
        /// Avalonia renderer to be up.
        /// </summary>
        public void RefreshSheetPreview()
        {
            _sheetTimer?.Stop();
            if (Application.Current is null) return; // design-time previewer

            var count = _shell.Palette.Count;
            if (count == 0)
            {
                ClearSheetPreview();
                return;
            }

            try
            {
                ShowSheet(SwatchSheetService.Render(_shell.Palette), count);
            }
            catch (Exception ex)
            {
                ClearSheetPreview();
                _shell.StatusMessage = $"Couldn't render the sheet preview: {ex.Message}";
            }
        }

        /// <summary>Queues a preview refresh (palette edits, workspace entry).</summary>
        public void ScheduleSheetPreviewRefresh()
        {
            if (Application.Current is null) return;

            if (_sheetTimer is null)
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(SheetRefreshDelayMs) };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    RefreshSheetPreview();
                };
                _sheetTimer = timer;
            }

            _sheetTimer.Stop();
            _sheetTimer.Start();
        }

        /// <summary>Stops the debounce timer and releases the preview bitmap.</summary>
        public void Detach()
        {
            _sheetTimer?.Stop();
            _sheetTimer = null;

            if (SheetPreview is IDisposable disposable) disposable.Dispose();
            SheetPreview = null;
        }

        /// <summary>Shows a rendered sheet, retiring the previous bitmap once the
        /// frame that used it has been drawn.</summary>
        private void ShowSheet(RenderTargetBitmap sheet, int count)
        {
            var previous = SheetPreview;
            SheetPreview = sheet;
            SheetSizeLabel =
                $"{count} color{(count == 1 ? "" : "s")} · {sheet.PixelSize.Width} × {sheet.PixelSize.Height} px";

            // Avalonia may still be compositing the old source for a frame, so
            // it is disposed when the UI thread goes idle rather than inline.
            if (previous is IDisposable disposable)
                Dispatcher.UIThread.Post(disposable.Dispose, DispatcherPriority.Background);
        }

        private void ClearSheetPreview()
        {
            var previous = SheetPreview;
            SheetPreview = null;
            SheetSizeLabel = string.Empty;

            if (previous is IDisposable disposable)
                Dispatcher.UIThread.Post(disposable.Dispose, DispatcherPriority.Background);
        }

        // ---------------- Text export ----------------

        [RelayCommand]
        private void BuildExportPreview()
        {
            ExportPreview = PaletteExportService.Export(_shell.Palette, SelectedExportFormat);
            _shell.StatusMessage = $"Export preview built ({SelectedExportFormat}).";
        }

        [RelayCommand]
        private async Task CopyExportAsync()
        {
            if (string.IsNullOrEmpty(ExportPreview)) BuildExportPreview();
            await ClipboardHelper.SetTextAsync(ExportPreview);
            _shell.StatusMessage = "Copied export text to clipboard.";
        }

        /// <summary>§6 "Export presets": writes the selected format straight to a local file
        /// (text formats directly, ASE/ACO as their binary encodings).</summary>
        [RelayCommand]
        private async Task ExportToFileAsync()
        {
            var format = SelectedExportFormat;
            var extension = PaletteExportService.ExtensionFor(format);
            var path = await PaletteFileService.PickSaveFileAsync(
                $"palette-{DateTime.Now:yyyyMMdd-HHmmss}", extension, $"{format} (*.{extension})");
            if (string.IsNullOrEmpty(path))
            {
                _shell.StatusMessage = "Export cancelled.";
                return;
            }

            try
            {
                switch (format)
                {
                    case ExportFormat.Ase:
                        await System.IO.File.WriteAllBytesAsync(path, PaletteBinaryExportService.WriteAse(_shell.Palette));
                        break;
                    case ExportFormat.Aco:
                        await System.IO.File.WriteAllBytesAsync(path, PaletteBinaryExportService.WriteAco(_shell.Palette));
                        break;
                    default:
                        await System.IO.File.WriteAllTextAsync(path, PaletteExportService.Export(_shell.Palette, format));
                        break;
                }

                _shell.StatusMessage = $"Exported {_shell.Palette.Count} color{(_shell.Palette.Count == 1 ? "" : "s")} to {System.IO.Path.GetFileName(path)}.";
            }
            catch (Exception ex)
            {
                _shell.StatusMessage = $"Couldn't export: {ex.Message}";
            }
        }

        // ---------------- Swatch sheet file ----------------

        /// <summary>§6 "Export as PNG (swatch sheet)" — writes the sheet as PNG or JPG.</summary>
        [RelayCommand]
        private async Task SaveSheetAsync()
        {
            var count = _shell.Palette.Count;
            if (count == 0)
            {
                _shell.StatusMessage = "Nothing to export yet — the palette has no colors.";
                return;
            }

            var format = SheetFormat;
            var extension = SwatchSheetService.ExtensionFor(format);
            var path = await PaletteFileService.PickSaveFileAsync(
                $"{PaletteFileStem()}-sheet", extension,
                $"{SwatchSheetService.DisplayName(format)} image (*.{extension})");
            if (string.IsNullOrEmpty(path))
            {
                _shell.StatusMessage = "Export cancelled.";
                return;
            }

            try
            {
                // Render again at save time so the file matches the palette as it
                // is now, then show that render as the preview.
                var sheet = SwatchSheetService.Render(_shell.Palette);
                SwatchSheetService.Save(sheet, path, format);
                ShowSheet(sheet, count);

                _shell.StatusMessage =
                    $"Saved {count} color{(count == 1 ? "" : "s")} as {SwatchSheetService.DisplayName(format)} · {System.IO.Path.GetFileName(path)}.";
            }
            catch (Exception ex)
            {
                _shell.StatusMessage = $"Couldn't save the {SwatchSheetService.DisplayName(format)} sheet: {ex.Message}";
            }
        }

        /// <summary>File-name stem from the active palette (falls back to "palette").</summary>
        private string PaletteFileStem()
        {
            var name = _shell.Library.SelectedPalette?.Name;
            if (string.IsNullOrWhiteSpace(name)) return "palette";

            var invalid = System.IO.Path.GetInvalidFileNameChars();
            var clean = new string(name.Select(c => invalid.Contains(c) ? '-' : c).ToArray()).Trim();
            return string.IsNullOrWhiteSpace(clean) ? "palette" : clean;
        }
    }
}
