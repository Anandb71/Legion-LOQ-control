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
        private bool _has4ZoneRGB = false;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private void Log(string message)
        {
            TextLog.Text = message;
            System.Diagnostics.Debug.WriteLine(message);
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _detector.Detect();
            TextModel.Text = _detector.Model;
            
            // Detect keyboard type
            bool hasSpectrum = _spectrum.IsSupported;
            _has4ZoneRGB = _lighting.IsSupported;
            _useSpectrum = hasSpectrum;

            string kbType = hasSpectrum ? "Spectrum (Per-Key RGB)" :
                           _has4ZoneRGB ? "4-Zone RGB" : 
                           "None detected";
            
            Log($"Keyboard: Spectrum={hasSpectrum}, 4-Zone={_has4ZoneRGB}");
            
            TextStatus.Text = _detector.IsSupported ? $"Supported | KB: {kbType}" : "Unsupported";
            TextStatus.Foreground = _detector.IsSupported ? System.Windows.Media.Brushes.LightGreen : System.Windows.Media.Brushes.Orange;

            if (_custom.IsSupported)
            {
                try
                {
                    bool isFullSpeed = await _custom.GetFanFullSpeedAsync();
                    CheckFanFullSpeed.IsChecked = isFullSpeed;
                    Log($"Fan Full Speed: {isFullSpeed}");
                }
                catch (System.Exception ex)
                {
                    Log($"Fan read error: {ex.Message}");
                }
            }

            if (!_detector.IsSupported)
            {
                MessageBox.Show("Device not supported!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _spectrum.Dispose();
            _lighting.Dispose();
        }

        private async void OnFanControlChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                bool enable = CheckFanFullSpeed.IsChecked == true;
                bool success = await _custom.SetFanFullSpeedAsync(enable);
                Log($"Fan Full Speed -> {enable}: {(success ? "OK" : "FAILED")}");
            }
            catch (System.Exception ex)
            {
                Log($"Fan error: {ex.Message}");
            }
        }

        private void OnPowerChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CheckConservation.IsChecked == true)
                {
                    _battery.SetConservationMode(true);
                    Log("Conservation Mode: ON");
                }
                else
                {
                    _battery.SetConservationMode(false);
                    Log("Conservation Mode: OFF");
                }

                if (CheckRapidCharge.IsChecked == true)
                {
                    _battery.SetRapidCharge(true);
                    Log("Rapid Charge: ON");
                }
                else
                {
                    _battery.SetRapidCharge(false);
                    Log("Rapid Charge: OFF");
                }
            }
            catch (System.Exception ex)
            {
                Log($"Battery error: {ex.Message}");
            }
        }

        private async void BtnQuiet_Click(object sender, RoutedEventArgs e)
        {
            await _power.SetProfileAsync(PowerProfile.Quiet);
            Log("Thermal Mode: Quiet");
        }
        private async void BtnBalanced_Click(object sender, RoutedEventArgs e)
        {
            await _power.SetProfileAsync(PowerProfile.Balanced);
            Log("Thermal Mode: Balanced");
        }
        private async void BtnPerf_Click(object sender, RoutedEventArgs e)
        {
            await _power.SetProfileAsync(PowerProfile.Performance);
            Log("Thermal Mode: Performance");
        }

        private async void BtnTakeControl_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool success = await _lighting.SetLightingOwnerAsync(true);
                Log($"Take lighting control: {(success ? "OK" : "FAILED")}");
            }
            catch (System.Exception ex)
            {
                Log($"Take control error: {ex.Message}");
            }
        }

        private void BtnLightOff_Click(object sender, RoutedEventArgs e)
        {
            bool success;
            if (_useSpectrum)
            {
                success = _spectrum.SetBrightness(0);
                Log($"Spectrum brightness -> 0: {(success ? "OK" : "FAILED")}");
            }
            else if (_has4ZoneRGB)
            {
                success = _lighting.SetOff();
                Log($"4-Zone RGB -> OFF: {(success ? "OK" : "FAILED")}");
            }
            else
            {
                Log("No keyboard detected!");
            }
        }

        private void BtnLightLow_Click(object sender, RoutedEventArgs e)
        {
            bool success;
            if (_useSpectrum)
            {
                success = _spectrum.SetBrightness(3);
                Log($"Spectrum brightness -> 3: {(success ? "OK" : "FAILED")}");
            }
            else if (_has4ZoneRGB)
            {
                success = _lighting.SetValues(1, 255, 255, 255);
                Log($"4-Zone RGB -> Low/White: {(success ? "OK" : "FAILED")}");
            }
            else
            {
                Log("No keyboard detected!");
            }
        }

        private void BtnLightHigh_Click(object sender, RoutedEventArgs e)
        {
            bool success;
            if (_useSpectrum)
            {
                success = _spectrum.SetBrightness(9);
                Log($"Spectrum brightness -> 9: {(success ? "OK" : "FAILED")}");
            }
            else if (_has4ZoneRGB)
            {
                success = _lighting.SetValues(2, 255, 255, 255);
                Log($"4-Zone RGB -> High/White: {(success ? "OK" : "FAILED")}");
            }
            else
            {
                Log("No keyboard detected!");
            }
        }
    }
}