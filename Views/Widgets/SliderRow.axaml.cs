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
        /// Populates the slider rows from Studio's color-panel sliders.
        /// Each row carries a snapshot of label/value/min/max/track-brush at the
        /// moment BindTo is called; the slider's value is bound TwoWay so moves
        /// flow back up to StudioWorkspaceViewModel automatically.
        /// </summary>
        public void BindTo(StudioWorkspaceViewModel studio)
        {
            var rows = new ObservableCollection<SliderRowItem>
            {
                new SliderRowItem
                {
                    Label = "Hue",
                    Value = studio.Hue.ToString("N0"),
                    Minimum = 0,
                    Maximum = 360,
                    TrackBrush = studio.HueTrackBrush,
                    Tooltip = "Hue angle (0–360°)"
                },
                new SliderRowItem
                {
                    Label = "Saturation",
                    Value = studio.Saturation.ToString("N0"),
                    Minimum = 0,
                    Maximum = 100,
                    TrackBrush = studio.SaturationTrackBrush,
                    Tooltip = "Saturation (0–100%)"
                },
                new SliderRowItem
                {
                    Label = "Brightness",
                    Value = studio.Lightness.ToString("N0"),
                    Minimum = 0,
                    Maximum = 100,
                    TrackBrush = studio.BrightnessTrackBrush,
                    Tooltip = "Lightness (0–100%)"
                },
                new SliderRowItem
                {
                    Label = "Temperature",
                    Value = studio.Temperature.ToString("N0"),
                    Minimum = -100,
                    Maximum = 100,
                    TrackBrush = studio.TemperatureTrackBrush,
                    Tooltip = "Warm–cool temperature (-100…+100)"
                },
                new SliderRowItem
                {
                    Label = "Opacity",
                    Value = studio.Alpha.ToString("N0"),
                    Minimum = 0,
                    Maximum = 100,
                    TrackBrush = studio.AlphaTrackBrush,
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
    /// and the interactive slider. Snapshot of a <see cref="StudioWorkspaceViewModel"/>
    /// color-panel value so the track and the slider show the right thing as
    /// the color panel changes.
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
