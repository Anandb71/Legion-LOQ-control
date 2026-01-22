using System.Windows;
using LegionLoqControl.Core.Device;
using LegionLoqControl.Core.Hardware;

namespace LegionLoqControl
{
    public partial class MainWindow : Window
    {
        private readonly DeviceDetector _detector = new();
        private readonly EnergyDriver _energy = new();
        private readonly PowerController _power = new();
        private readonly LightingController _lighting = new();

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
            TextStatus.Text = _detector.IsSupported ? "Supported" : "Unsupported";
            TextStatus.Foreground = _detector.IsSupported ? System.Windows.Media.Brushes.LightGreen : System.Windows.Media.Brushes.Orange;

            if (!_detector.IsSupported)
            {
                MessageBox.Show("Device not supported!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _energy.Dispose();
        }

        private void OnPowerChanged(object sender, RoutedEventArgs e)
        {
            if (CheckConservation.IsChecked == true)
                _energy.SetConservationMode(true);
            else
                _energy.SetConservationMode(false);

            if (CheckRapidCharge.IsChecked == true)
                _energy.SetRapidCharge(true);
            else
                _energy.SetRapidCharge(false);
        }

        private void BtnQuiet_Click(object sender, RoutedEventArgs e) => _power.SetProfile(PowerProfile.Quiet);
        private void BtnBalanced_Click(object sender, RoutedEventArgs e) => _power.SetProfile(PowerProfile.Balanced);
        private void BtnPerf_Click(object sender, RoutedEventArgs e) => _power.SetProfile(PowerProfile.Performance);

        private void BtnTakeControl_Click(object sender, RoutedEventArgs e) => _lighting.SetLightingOwner(true);

        private void BtnLightOff_Click(object sender, RoutedEventArgs e) => _lighting.SetDimensions(0, 0, 0, 0);
        private void BtnLightLow_Click(object sender, RoutedEventArgs e) => _lighting.SetDimensions(1, 255, 255, 255);
        private void BtnLightHigh_Click(object sender, RoutedEventArgs e) => _lighting.SetDimensions(2, 255, 255, 255);
    }
}