using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Spectrum.Models;
using Spectrum.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace Spectrum.Views
{
    /// <summary>One row in the command palette.</summary>
    public class CommandEntry
    {
        public CommandEntry(string title, string hint, Action action)
        {
            Title = title;
            Hint = hint;
            Action = action;
        }

        public string Title { get; }
        public string Hint { get; }
        public Action Action { get; }
    }

    /// <summary>
    /// §7 "Command palette (Ctrl+K)": fuzzy-searchable list of every command in
    /// the app, with keyboard navigation. Opens even while typing in a TextBox.
    /// </summary>
    public partial class CommandPaletteWindow : Window
    {
        private readonly List<CommandEntry> _all = new();
        private List<CommandEntry> _visible = new();

        /// <summary>Design-time only — the real window is created with a ViewModel.</summary>
        public CommandPaletteWindow()
        {
            InitializeComponent();

            ResultsList.ItemsSource = _visible;
            Opened += (_, _) =>
            {
                SearchBox.Focus();
                if (ResultsList.ItemCount > 0) ResultsList.SelectedIndex = 0;
            };
            KeyDown += OnKeyDown;
        }

        public CommandPaletteWindow(MainWindowViewModel vm) : this()
        {
            _all.AddRange(BuildEntries(vm));
            _visible = _all;
            ResultsList.ItemsSource = _visible;
        }

        private void OnSearchChanged(object? sender, TextChangedEventArgs e)
        {
            var query = SearchBox.Text?.Trim() ?? string.Empty;
            _visible = string.IsNullOrEmpty(query)
                ? _all
                : _all
                    .Select(entry => (Entry: entry, Score: Score(entry, query)))
                    .Where(x => x.Score > 0)
                    .OrderByDescending(x => x.Score)
                    .Select(x => x.Entry)
                    .ToList();

            ResultsList.ItemsSource = _visible;
            ResultsList.SelectedIndex = _visible.Count > 0 ? 0 : -1;
        }

        private static int Score(CommandEntry entry, string query)
        {
            var title = entry.Title.ToLowerInvariant();
            var needle = query.ToLowerInvariant();

            // Substring beats subsequence; longer matched prefixes beat later hits.
            var index = title.IndexOf(needle, StringComparison.Ordinal);
            if (index >= 0) return 1000 - index + entry.Title.Length;

            // Fallback: subsequence match (letters in order).
            var i = 0;
            foreach (var ch in title)
            {
                if (ch == needle[i] && ++i == needle.Length) return 100;
            }

            // Last resort: match against the hint (e.g. "undo").
            return entry.Hint.ToLowerInvariant().Contains(needle, StringComparison.Ordinal) ? 50 : 0;
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    Close(null);
                    e.Handled = true;
                    break;

                case Key.Down when ResultsList.ItemCount > 0:
                    ResultsList.SelectedIndex = Math.Min(ResultsList.SelectedIndex + 1, ResultsList.ItemCount - 1);
                    ResultsList.ScrollIntoView(ResultsList.SelectedIndex);
                    e.Handled = true;
                    break;

                case Key.Up when ResultsList.ItemCount > 0:
                    ResultsList.SelectedIndex = Math.Max(ResultsList.SelectedIndex - 1, 0);
                    ResultsList.ScrollIntoView(ResultsList.SelectedIndex);
                    e.Handled = true;
                    break;

                case Key.Enter:
                    ActivateSelection();
                    e.Handled = true;
                    break;
            }
        }

        private void OnResultActivated(object? sender, RoutedEventArgs e) => ActivateSelection();

        private void ActivateSelection()
        {
            if (ResultsList.SelectedItem is not CommandEntry entry) return;
            Close(null);
            entry.Action();
        }

        // ---------------- Catalog ----------------

        private static List<CommandEntry> BuildEntries(MainWindowViewModel vm)
        {
            var entries = new List<CommandEntry>();

            void Add(string title, string hint, ICommand command, object? parameter = null)
            {
                entries.Add(new CommandEntry(title, hint, () =>
                {
                    if (command.CanExecute(parameter)) command.Execute(parameter);
                }));
            }

            void AddAction(string title, string hint, Action action)
            {
                entries.Add(new CommandEntry(title, hint, action));
            }

            // Palette generation & editing
            Add("Generate / harmonize palette", "Space", vm.Studio.GeneratePaletteCommand);
            Add("Add base color to palette", "color panel", vm.Studio.AddCurrentColorCommand);
            Add("Open color picker", "spectrum + RGB/HSL/HSV", vm.Studio.OpenColorPickerCommand);
            Add("Paste color from clipboard", "HEX / RGB / HSL", vm.Studio.PasteColorCommand);
            Add("Add tints of base color", "color science", vm.Analyze.AddTintsCommand);
            Add("Add shades of base color", "color science", vm.Analyze.AddShadesCommand);
            Add("Add tones of base color", "color science", vm.Analyze.AddTonesCommand);
            Add("Clear palette", "remove all swatches", vm.ClearPaletteCommand);
            Add("Undo", "Ctrl+Z", vm.UndoCommand);
            Add("Redo", "Ctrl+Y", vm.RedoCommand);

            // Picking
            Add("Pick color from screen", "Ctrl+Alt+P", vm.Studio.PickFromScreenCommand);
            Add("Pick color from image", "eyedropper", vm.Studio.PickFromImageCommand);
            Add("Extract palette from image", "histogram", vm.Studio.ExtractFromImageCommand);

            // Library
            Add("New palette", "Ctrl+N", vm.Library.NewPaletteCommand);
            Add("Duplicate palette", "library", vm.Library.DuplicatePaletteCommand, null);
            Add("Delete palette", "library", vm.Library.DeletePaletteCommand, null);
            Add("Pin / unpin palette", "library", vm.Library.TogglePinCommand, null);
            AddAction("Show pinned palettes", "filter", () => vm.Library.SelectedFilter = LibraryFilter.Pinned);
            AddAction("Show recent palettes", "filter", () => vm.Library.SelectedFilter = LibraryFilter.Recent);
            AddAction("Show all palettes", "filter", () => vm.Library.SelectedFilter = LibraryFilter.All);
            AddAction("Focus palette search", "library", () => vm.Library.SearchText = string.Empty);

            // Files & export
            Add("Save palette to file", "Ctrl+S", vm.SavePaletteCommand);
            Add("Load palette from file", "Ctrl+O", vm.LoadPaletteCommand);
            Add("Export selected format to file", "Ctrl+E", vm.Export.ExportToFileCommand);
            Add("Export PNG swatch sheet", "Ctrl+J", vm.Export.ExportPngCommand);
            Add("Build export preview", "footer", vm.Export.BuildExportPreviewCommand);
            Add("Copy export to clipboard", "footer", vm.Export.CopyExportCommand);
            Add("Copy whole palette as text", "footer", vm.CopyAllHexCommand);
            Add("Import palette file (CSS/GPL/ASE/ACO)", "library menu", vm.Library.ImportPaletteFileCommand);
            Add("Export whole library", "library menu", vm.Library.ExportLibraryCommand);
            Add("Import library", "library menu", vm.Library.ImportLibraryCommand);

            // Workspaces
            Add("Workspace: Studio — build the palette", "Ctrl+1", vm.SetWorkspaceCommand, WorkspaceMode.Studio);
            Add("Workspace: Preview — mockup & compare", "Ctrl+2", vm.SetWorkspaceCommand, WorkspaceMode.Preview);
            Add("Workspace: Analyze — contrast & science", "Ctrl+3", vm.SetWorkspaceCommand, WorkspaceMode.Analyze);
            Add("Workspace: Export — files in and out", "Ctrl+4", vm.SetWorkspaceCommand, WorkspaceMode.Export);

            // Analyze & studio tools
            Add("Open contrast matrix", "Analyze — every pair scored", vm.Analyze.OpenContrastMatrixCommand);
            Add("Open gradient studio", "build & export a gradient", vm.Studio.OpenGradientStudioCommand);

            // Window & view
            Add("Toggle light / dark theme", "appearance", vm.ToggleThemeCommand);
            Add("Toggle library panel", "Ctrl+B", vm.ToggleLibraryPanelCommand);
            Add("Toggle inspector panel", "Ctrl+I", vm.ToggleInspectorCommand);
            Add("Detach palette into new window", "multi-window", vm.DetachWindowCommand);

            return entries;
        }
    }
}
