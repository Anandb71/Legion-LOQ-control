using System.Windows;
using LegionLoqControl.Core.Device;
using LegionLoqControl.Core.Hardware;

namespace LegionLoqControl
{
    public partial class MainWindow : Window
    {
        private readonly DeviceDetector _detector = new();
        private readonly BatteryController _battery = new();
        private readonly PowerController _power = new();
        private readonly LightingController _lighting = new();
        private readonly SpectrumKeyboardController _spectrum = new();
        private readonly CustomModeController _custom = new();

        private bool _useSpectrum = false;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _detector.Detect();
            TextModel.Text = _detector.Model;
            
            // Detect keyboard type
            bool hasSpectrum = _spectrum.IsSupported;
            bool has4ZoneRGB = _lighting.IsSupported;
            _useSpectrum = hasSpectrum;

            string kbType = hasSpectrum ? "Spectrum (Per-Key RGB)" :
                           has4ZoneRGB ? "4-Zone RGB" : 
                           "None detected";
            
            System.Diagnostics.Debug.WriteLine($"Spectrum keyboard: {hasSpectrum}");
            System.Diagnostics.Debug.WriteLine($"4-Zone RGB keyboard: {has4ZoneRGB}");
            
            TextStatus.Text = _detector.IsSupported ? $"Supported | KB: {kbType}" : "Unsupported";
            TextStatus.Foreground = _detector.IsSupported ? System.Windows.Media.Brushes.LightGreen : System.Windows.Media.Brushes.Orange;

            if (_custom.IsSupported)
            {
               bool isFullSpeed = await _custom.GetFanFullSpeedAsync();
               CheckFanFullSpeed.IsChecked = isFullSpeed;
            }

            if (!_detector.IsSupported)
            {
                MessageBox.Show("Device not supported!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _spectrum.Dispose();
        }

        private async void OnFanControlChanged(object sender, RoutedEventArgs e)
        {
            if (CheckFanFullSpeed.IsChecked == true)
                await _custom.SetFanFullSpeedAsync(true);
            else
                await _custom.SetFanFullSpeedAsync(false);
        }

        private void OnPowerChanged(object sender, RoutedEventArgs e)
        {
            if (CheckConservation.IsChecked == true)
                _battery.SetConservationMode(true);
            else
                _battery.SetConservationMode(false);

            if (CheckRapidCharge.IsChecked == true)
                _battery.SetRapidCharge(true);
            else
                _battery.SetRapidCharge(false);
        }

        private async void BtnQuiet_Click(object sender, RoutedEventArgs e) => await _power.SetProfileAsync(PowerProfile.Quiet);
        private async void BtnBalanced_Click(object sender, RoutedEventArgs e) => await _power.SetProfileAsync(PowerProfile.Balanced);
        private async void BtnPerf_Click(object sender, RoutedEventArgs e) => await _power.SetProfileAsync(PowerProfile.Performance);

        private async void BtnTakeControl_Click(object sender, RoutedEventArgs e)
        {
            if (!_useSpectrum)
            {
                await _lighting.SetLightingOwnerAsync(true);
            }
            // Spectrum doesn't need "take control" - direct HID access
        }

        private void BtnLightOff_Click(object sender, RoutedEventArgs e)
        {
            if (_useSpectrum)
                _spectrum.SetBrightness(0);
            else
                _lighting.SetValues(0, 0, 0, 0);
        }

        private void BtnLightLow_Click(object sender, RoutedEventArgs e)
        {
            if (_useSpectrum)
                _spectrum.SetBrightness(3); // ~33%
            else
                _lighting.SetValues(1, 255, 255, 255);
        }

        private void BtnLightHigh_Click(object sender, RoutedEventArgs e)
        {
            if (_useSpectrum)
                _spectrum.SetBrightness(9); // 100%
            else
                _lighting.SetValues(2, 255, 255, 255);
        }
    }
}