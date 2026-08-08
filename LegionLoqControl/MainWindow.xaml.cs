using System.Windows;
using System.Windows.Media;
using LegionLoqControl.Application.Diagnostics;
using LegionLoqControl.Domain.Capabilities;
using LegionLoqControl.Domain.Diagnostics;
using LegionLoqControl.Infrastructure.Windows.Diagnostics;

namespace LegionLoqControl;

public partial class MainWindow : Window
{
    private readonly CancellationTokenSource _lifetime = new();
    private readonly MachineDiagnosticsService _diagnostics = new(
        new WindowsMachineIdentitySource(),
        [new WindowsCapabilityProbe()]);

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            MachineSnapshot snapshot = await _diagnostics.CaptureAsync(_lifetime.Token);
            string model = Display(snapshot.Identity.Model);
            string machineType = Display(snapshot.Identity.MachineType);
            string bios = Display(snapshot.Identity.BiosVersion);
            TextModel.Text = $"{model} ({machineType}) · BIOS {bios}";

            CapabilityEvidence[] candidates = snapshot.Capabilities
                .Where(static evidence => evidence.Support is CapabilitySupport.Unknown or CapabilitySupport.Supported)
                .ToArray();

            TextStatus.Text = $"Read-only scan complete · {candidates.Length} candidate interfaces · writes locked";
            TextStatus.Foreground = Brushes.Goldenrod;
            TextEvidence.Text = candidates.Length == 0
                ? "No compatible hardware interfaces were detected."
                : string.Join(" · ", candidates.Select(static evidence => evidence.Capability));
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            TextModel.Text = "Unavailable";
            TextStatus.Text = $"Read-only scan failed ({exception.GetType().Name})";
            TextStatus.Foreground = Brushes.Orange;
            TextEvidence.Text = "No hardware writes were attempted.";
        }
        finally
        {
            CheckConservation.IsChecked = null;
            CheckRapidCharge.IsChecked = null;
            CheckFanFullSpeed.IsChecked = null;
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _lifetime.Cancel();
    }

    private static string Display(Observation observation) =>
        observation.State == ObservationState.Observed ? observation.Value! : "Unknown";
}
