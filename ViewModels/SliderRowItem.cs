using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Media;
using System;

namespace Spectrum.ViewModels
{
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
