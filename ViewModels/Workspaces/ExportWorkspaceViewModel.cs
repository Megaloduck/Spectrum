using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Spectrum.Services;
using System;
using System.Threading.Tasks;

namespace Spectrum.ViewModels
{
    /// <summary>
    /// Ctrl+4 · Export — format selection, the export preview and writing the
    /// palette to files (text, ASE / ACO, PNG sheet). Palette save / load and
    /// library import / export are shared shell actions and stay on
    /// <see cref="MainWindowViewModel"/> / <see cref="PaletteLibraryViewModel"/>.
    /// </summary>
    public partial class ExportWorkspaceViewModel : ViewModelBase
    {
        private readonly MainWindowViewModel _shell;

        public ExportWorkspaceViewModel(MainWindowViewModel shell) => _shell = shell;

        public Array ExportFormats { get; } = Enum.GetValues(typeof(ExportFormat));

        [ObservableProperty]
        private ExportFormat _selectedExportFormat = ExportFormat.Json;

        [ObservableProperty]
        private string _exportPreview = string.Empty;

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

        /// <summary>§6 "Export as PNG (swatch sheet)".</summary>
        [RelayCommand]
        private async Task ExportPngAsync()
        {
            var path = await PaletteFileService.PickSaveFileAsync(
                $"palette-{DateTime.Now:yyyyMMdd-HHmmss}", "png", "PNG swatch sheet (*.png)");
            if (string.IsNullOrEmpty(path))
            {
                _shell.StatusMessage = "Export cancelled.";
                return;
            }

            try
            {
                PngExportService.Export(_shell.Palette, path);
                _shell.StatusMessage = $"PNG swatch sheet saved to {System.IO.Path.GetFileName(path)}.";
            }
            catch (Exception ex)
            {
                _shell.StatusMessage = $"Couldn't render PNG: {ex.Message}";
            }
        }
    }
}
