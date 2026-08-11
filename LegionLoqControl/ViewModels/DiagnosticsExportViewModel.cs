using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LegionLoqControl.Application.Diagnostics;
using LegionLoqControl.Domain.Diagnostics;
using LegionLoqControl.Services;

namespace LegionLoqControl.ViewModels;

public sealed partial class DiagnosticsExportViewModel : ObservableObject, IDisposable
{
    private readonly MachineSessionViewModel _session;
    private readonly DiagnosticsExportService _exportService;
    private readonly IDiagnosticsExportWriter _writer;
    private readonly IDiagnosticsExportDestinationPicker _destinationPicker;
    private readonly string _productVersion;
    private readonly CancellationTokenSource _lifetime = new();
    private bool _disposed;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _buttonText = "Export diagnostics";

    [ObservableProperty]
    private string _statusMessage = "Waiting for serial-free inventory";

    internal DiagnosticsExportViewModel(
        MachineSessionViewModel session,
        DiagnosticsExportService exportService,
        IDiagnosticsExportWriter writer,
        IDiagnosticsExportDestinationPicker destinationPicker,
        string productVersion)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _destinationPicker = destinationPicker ??
            throw new ArgumentNullException(nameof(destinationPicker));
        ArgumentException.ThrowIfNullOrWhiteSpace(productVersion);
        _productVersion = productVersion.Trim();

        _session.PropertyChanged += Session_PropertyChanged;
        UpdateAvailabilityMessage();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _session.PropertyChanged -= Session_PropertyChanged;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportAsync()
    {
        MachineSnapshot? machine = _session.MachineSnapshot;
        if (machine is null)
            return;

        HardwareStateSnapshot? retainedHardwareState = _session.HardwareStateSnapshot;
        IsBusy = true;
        ButtonText = "Exporting…";

        try
        {
            string? destinationPath = _destinationPicker.PickDestination();
            if (destinationPath is null)
            {
                StatusMessage = "Export cancelled · no file created";
                return;
            }

            DiagnosticsExportDocument document = _exportService.Create(
                machine,
                retainedHardwareState,
                _productVersion);
            await _writer.WriteAsync(
                document,
                destinationPath,
                DiagnosticsExportWriteMode.ReplaceExisting,
                _lifetime.Token);

            StatusMessage = "Diagnostics exported · review the JSON before sharing";
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (DiagnosticsExportException exception)
        {
            StatusMessage = MapFailure(exception.ErrorCode);
        }
        catch (Exception)
        {
            StatusMessage = "Export failed · no diagnostics file was completed";
        }
        finally
        {
            ButtonText = "Export diagnostics";
            IsBusy = false;
        }
    }

    private bool CanExport() =>
        !_disposed && !IsBusy && _session.MachineSnapshot is not null;

    private void Session_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MachineSessionViewModel.MachineSnapshot)
            or nameof(MachineSessionViewModel.HardwareStateSnapshot))
        {
            ExportCommand.NotifyCanExecuteChanged();
            if (!IsBusy)
                UpdateAvailabilityMessage();
        }
    }

    private void UpdateAvailabilityMessage()
    {
        StatusMessage = _session.MachineSnapshot is null
            ? "Waiting for serial-free inventory"
            : _session.HardwareStateSnapshot is null
                ? "Exports serial-free inventory · no new hardware read"
                : "Includes retained hardware state · no new hardware read";
    }

    private static string MapFailure(string errorCode) =>
        errorCode switch
        {
            "diagnostics_export_access_denied" =>
                "Export blocked by Windows file permissions",
            "diagnostics_export_directory_missing" =>
                "Export folder is no longer available",
            "diagnostics_export_destination_exists" =>
                "Export stopped because the destination already exists",
            "diagnostics_export_too_large" =>
                "Export stopped because the report exceeded its safety limit",
            _ => "Export failed · no diagnostics file was completed",
        };
}
