using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using Spectrum.ViewModels;
using System;
using System.Collections.ObjectModel;

namespace Spectrum.Views.Widgets
{
    public partial class SliderRow : UserControl
    {
        public static readonly StyledProperty<ObservableCollection<SliderRowItem>> SliderRowsProperty =
            AvaloniaProperty.Register<SliderRow, ObservableCollection<SliderRowItem>>(nameof(SliderRows));

        public ObservableCollection<SliderRowItem> SliderRows
        {
            get => GetValue(SliderRowsProperty);
            set => SetValue(SliderRowsProperty, value);
        }

        public SliderRow()
        {
            InitializeComponent();
            SliderRows = new ObservableCollection<SliderRowItem>();
        }

        /// <summary>
        /// Populates the slider rows from the view model's color-panel sliders.
        /// Each row carries a snapshot of label/value/min/max/track-brush at the
        /// moment BindTo is called; the slider's value is bound TwoWay so moves
        /// flow back up to MainWindowViewModel automatically.
        /// </summary>
        public void BindTo(MainWindowViewModel vm)
        {
            var rows = new ObservableCollection<SliderRowItem>
            {
                new SliderRowItem
                {
                    Label = "Hue",
                    Value = vm.Hue.ToString("N0"),
                    Minimum = 0,
                    Maximum = 360,
                    TrackBrush = vm.HueTrackBrush,
                    Tooltip = "Hue angle (0–360°)"
                },
                new SliderRowItem
                {
                    Label = "Saturation",
                    Value = vm.Saturation.ToString("N0"),
                    Minimum = 0,
                    Maximum = 100,
                    TrackBrush = vm.SaturationTrackBrush,
                    Tooltip = "Saturation (0–100%)"
                },
                new SliderRowItem
                {
                    Label = "Brightness",
                    Value = vm.Lightness.ToString("N0"),
                    Minimum = 0,
                    Maximum = 100,
                    TrackBrush = vm.BrightnessTrackBrush,
                    Tooltip = "Lightness (0–100%)"
                },
                new SliderRowItem
                {
                    Label = "Temperature",
                    Value = vm.Temperature.ToString("N0"),
                    Minimum = -100,
                    Maximum = 100,
                    TrackBrush = vm.TemperatureTrackBrush,
                    Tooltip = "Warm–cool temperature (-100…+100)"
                },
                new SliderRowItem
                {
                    Label = "Opacity",
                    Value = vm.Alpha.ToString("N0"),
                    Minimum = 0,
                    Maximum = 100,
                    TrackBrush = vm.AlphaTrackBrush,
                    Tooltip = "Alpha (0–100%)"
                },
            };

            SliderRows.Clear();
            foreach (var row in rows)
            {
                SliderRows.Add(row);
            }
        }
    }

    /// <summary>
    /// One row of the slider control: label, value caption, gradient track,
    /// and the interactive slider. Bound to a <see cref="MainWindowViewModel"/>
    /// data source so the track and the slider keep correct values as the
    /// color panel changes.
    /// </summary>
    public partial class SliderRowItem : ObservableObject
    {
        [ObservableProperty]
        private string _label = string.Empty;

        [ObservableProperty]
        private string _value = "0";

        [ObservableProperty]
        private double _minimum;

        [ObservableProperty]
        private double _maximum;

        [ObservableProperty]
        private IBrush _trackBrush = Brushes.Transparent;

        [ObservableProperty]
        private string _tooltip = string.Empty;
    }
}
