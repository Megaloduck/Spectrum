using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Spectrum.ViewModels;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Spectrum.Views.Widgets
{
    public partial class Toast : UserControl, INotifyPropertyChanged
    {
        public new event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Raises the PropertyChanged event for the given property.</summary>
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>How long the toast stays before fading out (ms).</summary>
        public static readonly StyledProperty<int> ToastDurationMsProperty =
            AvaloniaProperty.Register<Toast, int>(nameof(ToastDurationMs), 6000);

        public int ToastDurationMs
        {
            get => GetValue(ToastDurationMsProperty);
            set => SetValue(ToastDurationMsProperty, value);
        }

        private string _toastMessage = string.Empty;

        public string ToastMessage
        {
            get => _toastMessage;
            set
            {
                if (_toastMessage == value) return;
                _toastMessage = value;
                OnPropertyChanged(nameof(ToastMessage));
            }
        }

        [RelayCommand]
        private void Dismiss()
        {
            _dismissing = true;
            IsVisible = false;
            _timer.Stop();
        }

        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
        private bool _dismissing;

        public Toast()
        {
            InitializeComponent();
            ToastDurationMs = 6000;

            _timer.Tick += (_, _) =>
            {
                if (IsVisible)
                {
                    // When the toast is shown and the duration elapses, fade it out.
                    if (!_dismissing)
                    {
                        Opacity = 1.0;
                        _dismissing = true;
                    }
                }
                else if (_dismissing)
                {
                    // Fade out fully.
                    Opacity = 0.0;
                    _dismissing = false;
                    _timer.Stop();
                }
            };
        }

        public void Show(string message)
        {
            ToastMessage = message;
            Opacity = 0.0;
            IsVisible = true;
            _timer.Start();
        }


    }
}
