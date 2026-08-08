using System.Windows;
using LegionLoqControl.Core.Device;
using LegionLoqControl.Core.Hardware;

namespace LegionLoqControl
{
    public partial class MainWindow : Window
    {
        private readonly DeviceDetector _detector = new();
        private readonly LightingController _lighting = new();
        private readonly SpectrumKeyboardController _spectrum = new();

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _detector.Detect();
            TextModel.Text = _detector.Model;

            bool hasSpectrum = _spectrum.IsSupported;
            bool has4ZoneRgb = _lighting.IsSupported;
            string keyboardType = hasSpectrum
                ? "Spectrum (Per-Key RGB)"
                : has4ZoneRgb
                    ? "4-Zone RGB"
                    : "None detected";

            System.Diagnostics.Debug.WriteLine($"Spectrum keyboard: {hasSpectrum}");
            System.Diagnostics.Debug.WriteLine($"4-Zone RGB keyboard: {has4ZoneRgb}");

            TextStatus.Text = _detector.IsSupported
                ? $"Detected candidate device (writes locked) | KB: {keyboardType}"
                : "Unverified device (read-only)";
            TextStatus.Foreground = _detector.IsSupported
                ? System.Windows.Media.Brushes.LightGreen
                : System.Windows.Media.Brushes.Orange;

            CheckConservation.IsChecked = null;
            CheckRapidCharge.IsChecked = null;
            CheckFanFullSpeed.IsChecked = null;
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _spectrum.Dispose();
            _lighting.Dispose();
        }
    }
}