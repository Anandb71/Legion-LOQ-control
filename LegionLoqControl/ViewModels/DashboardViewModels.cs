using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LegionLoqControl.Application.Automation;
using LegionLoqControl.Application.Broker;
using LegionLoqControl.Application.Diagnostics;
using LegionLoqControl.Application.Profiles;
using LegionLoqControl.Domain.Capabilities;
using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Diagnostics;
using LegionLoqControl.Domain.Results;
using LegionLoqControl.Infrastructure.Windows.Automation;
using LegionLoqControl.Infrastructure.Windows.Diagnostics;
using LegionLoqControl.Infrastructure.Windows.Profiles;
using LegionLoqControl.Services;

namespace LegionLoqControl.ViewModels;

public enum DashboardStateKind
{
    Pending = 0,
    Success = 1,
    Warning = 2,
    Error = 3,
    Unavailable = 4,
}

public sealed partial class HardwareStateCardViewModel : ObservableObject
{
    [ObservableProperty]
    private string _value = "Not read";

    [ObservableProperty]
    private string _detail;

    [ObservableProperty]
    private DashboardStateKind _state = DashboardStateKind.Pending;

    public HardwareStateCardViewModel(
        string label,
        string description,
        string pendingDetail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(pendingDetail);

        Label = label;
        Description = description;
        _detail = pendingDetail;
    }

    public string Label { get; }

    public string Description { get; }

    public void Apply<T>(
        HardwareReadResult<T> result,
        Func<T, string> formatter)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(formatter);

        if (result.Status == HardwareReadStatus.Success && result.Value.HasValue)
        {
            Value = formatter(result.Value.Value);
            Detail = "Verified by the elevated read broker";
            State = DashboardStateKind.Success;
            return;
        }

        (Value, Detail, State) = result.Status switch
        {
            HardwareReadStatus.AccessDenied =>
                ("Access denied", "The provider rejected this execution context", DashboardStateKind.Warning),
            HardwareReadStatus.Unsupported =>
                ("Unsupported", "The required getter is not available", DashboardStateKind.Unavailable),
            HardwareReadStatus.Unavailable =>
                ("Unavailable", "The required device or provider was not found", DashboardStateKind.Unavailable),
            HardwareReadStatus.InvalidData =>
                ("Invalid response", "Firmware returned a value outside the validated contract", DashboardStateKind.Error),
            HardwareReadStatus.TimedOut =>
                ("Timed out", "The bounded hardware read did not finish", DashboardStateKind.Error),
            _ => ("Read failed", "The hardware state could not be verified", DashboardStateKind.Error),
        };
    }
}

public sealed record CapabilityItemViewModel(
    string Name,
    string Support,
    string EvidenceCode,
    DashboardStateKind State);

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IDashboardDataSource _dataSource;
    private readonly CancellationTokenSource _lifetime = new();
    private bool _initialized;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshHardwareStateCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _deviceName = "Detecting machine";

    [ObservableProperty]
    private string _deviceMetadata = "Reading serial-free identity and interface evidence";

    [ObservableProperty]
    private string _inventoryStatus = "Inventory pending";

    [ObservableProperty]
    private string _brokerInstallStatus = "Broker install not assessed";

    [ObservableProperty]
    private string _bannerTitle = "Read-only safety mode";

    [ObservableProperty]
    private string _bannerMessage =
        "Inventory runs without elevation. Hardware state is read only after you approve Windows elevation.";

    [ObservableProperty]
    private DashboardStateKind _bannerState = DashboardStateKind.Warning;

    [ObservableProperty]
    private string _refreshButtonText = "Read hardware state";

    [ObservableProperty]
    private string _lastUpdated = "Not read";

    public MainWindowViewModel()
        : this(
            new DashboardDataSource(),
            new MachineSessionViewModel())
    {
    }

    internal MainWindowViewModel(
        IDashboardDataSource dataSource,
        MachineSessionViewModel? session = null,
        IProfileStore? profileStore = null,
        IAutomationRuleStore? automationRuleStore = null,
        PowerSourceService? powerSourceService = null,
        AutomationPreviewService? automationPreviewService = null)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        Session = session ?? new MachineSessionViewModel();
        DiagnosticsExport = new DiagnosticsExportViewModel(
            Session,
            new DiagnosticsExportService(),
            new JsonDiagnosticsExportWriter(),
            new DiagnosticsExportDestinationPicker(),
            GetProductVersion());
        IProfileStore sharedProfileStore =
            profileStore ?? JsonProfileStore.CreateDefault();
        ProfileWorkspace = new ProfileWorkspaceViewModel(
            sharedProfileStore,
            new ProfilePreviewService(),
            Session);
        AutomationWorkspace = new AutomationWorkspaceViewModel(
            automationRuleStore ?? JsonAutomationRuleStore.CreateDefault(),
            sharedProfileStore,
            powerSourceService ?? new PowerSourceService(new WindowsPowerSourceReader()),
            automationPreviewService ?? new AutomationPreviewService());
        ProfileWorkspace.Profiles.CollectionChanged += Profiles_CollectionChanged;

        Battery = new HardwareStateCardViewModel(
            "BATTERY",
            "Charge behavior",
            "Broker-only EnergyDrv read");
        Thermal = new HardwareStateCardViewModel(
            "THERMAL",
            "Firmware performance profile",
            "Elevation required by Lenovo WMI");
        DisplayOverdrive = new HardwareStateCardViewModel(
            "DISPLAY",
            "Panel overdrive",
            "Elevation required by Lenovo WMI");
        IntegratedGpu = new HardwareStateCardViewModel(
            "GPU MODE",
            "Current graphics topology",
            "Elevation required by Lenovo WMI");
    }

    public HardwareStateCardViewModel Battery { get; }

    public HardwareStateCardViewModel Thermal { get; }

    public HardwareStateCardViewModel DisplayOverdrive { get; }

    public HardwareStateCardViewModel IntegratedGpu { get; }

    public MachineSessionViewModel Session { get; }

    public DiagnosticsExportViewModel DiagnosticsExport { get; }

    public ProfileWorkspaceViewModel ProfileWorkspace { get; }

    public AutomationWorkspaceViewModel AutomationWorkspace { get; }

    public ObservableCollection<CapabilityItemViewModel> Capabilities { get; } = [];

    public async Task InitializeAsync()
    {
        if (_initialized)
            return;

        _initialized = true;
        IsBusy = true;
        try
        {
            MachineSnapshot snapshot = await _dataSource
                .CaptureMachineAsync(_lifetime.Token)
                .ConfigureAwait(true);
            Session.UpdateMachineSnapshot(snapshot);
            ApplyIdentity(snapshot.Identity);
            ApplyCapabilities(snapshot.Capabilities);

            int candidateCount = snapshot.Capabilities.Count(static evidence =>
                evidence.Support is CapabilitySupport.Unknown or CapabilitySupport.Supported);
            InventoryStatus = $"Inventory complete · {candidateCount} candidate interfaces";
            ApplyBrokerInstall(_dataSource.AssessBrokerInstall());
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            DeviceName = "Machine identity unavailable";
            DeviceMetadata = "The serial-free inventory could not be completed";
            InventoryStatus = "Inventory failed · no hardware writes attempted";
            BannerTitle = "Inventory unavailable";
            BannerMessage = "The app stayed read-only. Restart to retry the local inventory.";
            BannerState = DashboardStateKind.Error;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void Cancel()
    {
        _lifetime.Cancel();
        ProfileWorkspace.Profiles.CollectionChanged -= Profiles_CollectionChanged;
        DiagnosticsExport.Dispose();
        ProfileWorkspace.Dispose();
        AutomationWorkspace.Dispose();
    }

    [RelayCommand(CanExecute = nameof(CanRefreshHardwareState))]
    private async Task RefreshHardwareStateAsync()
    {
        IsBusy = true;
        RefreshButtonText = "Waiting for Windows approval…";
        BannerTitle = "Privileged read requested";
        BannerMessage =
            "Windows will ask for approval. The broker accepts one read request and cannot change hardware.";
        BannerState = DashboardStateKind.Warning;

        try
        {
            HardwareStateSnapshot snapshot = await _dataSource
                .ReadHardwareStateAsync(_lifetime.Token)
                .ConfigureAwait(true);
            Session.UpdateHardwareStateSnapshot(snapshot);
            Battery.Apply(snapshot.BatteryChargeMode, FormatBatteryMode);
            Thermal.Apply(snapshot.ThermalMode, FormatThermalMode);
            DisplayOverdrive.Apply(snapshot.DisplayOverdrive, FormatToggle);
            IntegratedGpu.Apply(snapshot.IntegratedGpuMode, FormatGpuMode);

            HardwareReadStatus[] statuses =
            [
                snapshot.BatteryChargeMode.Status,
                snapshot.ThermalMode.Status,
                snapshot.DisplayOverdrive.Status,
                snapshot.IntegratedGpuMode.Status,
            ];
            int successCount = statuses.Count(static status => status == HardwareReadStatus.Success);
            LastUpdated = snapshot.ObservedAt.ToLocalTime().ToString("HH:mm:ss");
            BannerTitle = successCount == statuses.Length
                ? "Hardware state verified"
                : "Hardware state partially verified";
            BannerMessage =
                $"{successCount}/{statuses.Length} reads succeeded · observed at {LastUpdated} · no writes attempted";
            BannerState = successCount == statuses.Length
                ? DashboardStateKind.Success
                : DashboardStateKind.Warning;
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (DashboardDataSourceException exception)
        {
            ApplyBrokerFailure(exception.ErrorCode);
        }
        catch (Exception)
        {
            ApplyBrokerFailure("broker_read_failed");
        }
        finally
        {
            RefreshButtonText = "Read hardware state";
            IsBusy = false;
        }
    }

    private bool CanRefreshHardwareState() => !IsBusy && _initialized;

    private void Profiles_CollectionChanged(
        object? sender,
        NotifyCollectionChangedEventArgs e) =>
        AutomationWorkspace.SynchronizeProfiles(ProfileWorkspace.Profiles);

    private void ApplyIdentity(MachineIdentity identity)
    {
        string model = Display(identity.Model);
        string machineType = Display(identity.MachineType);
        string bios = Display(identity.BiosVersion);
        DeviceName = model;
        DeviceMetadata = $"Machine type {machineType} · BIOS {bios}";
    }

    private void ApplyCapabilities(IReadOnlyList<CapabilityEvidence> evidence)
    {
        Capabilities.Clear();
        foreach (CapabilityEvidence item in evidence.OrderBy(static item => item.Capability))
        {
            Capabilities.Add(new CapabilityItemViewModel(
                FormatCapability(item.Capability),
                FormatSupport(item.Support),
                item.EvidenceCode,
                item.Support switch
                {
                    CapabilitySupport.Supported => DashboardStateKind.Success,
                    CapabilitySupport.Degraded => DashboardStateKind.Warning,
                    CapabilitySupport.Unsupported => DashboardStateKind.Unavailable,
                    _ => DashboardStateKind.Pending,
                }));
        }
    }

    private void ApplyBrokerFailure(string errorCode)
    {
        BannerTitle = errorCode == "broker_elevation_cancelled"
            ? "Elevation cancelled"
            : "Hardware state unavailable";
        BannerMessage = errorCode switch
        {
            "broker_elevation_cancelled" =>
                "Windows approval was cancelled. No privileged hardware read ran.",
            "broker_not_found" =>
                "The read broker is not included in this package or is missing from the " +
                "application directory.",
            "broker_timeout" or "broker_lifetime_timeout" =>
                "The broker reached its time limit and stopped without changing hardware.",
            "broker_peer_mismatch" or "broker_authorization_failed" =>
                "The broker rejected the connection because its security checks did not match.",
            "broker_install_unprotected" =>
                "The broker is not in an administrator-protected directory, so this " +
                "production-mode read was refused.",
            "broker_unsigned" =>
                "The broker is unsigned, so this production-mode read was refused.",
            "broker_signature_invalid" =>
                "The broker signature is invalid, so no privileged read ran.",
            _ => "The read broker could not verify hardware state. No write was attempted.",
        };
        BannerState = errorCode == "broker_elevation_cancelled"
            ? DashboardStateKind.Warning
            : DashboardStateKind.Error;
    }

    private void ApplyBrokerInstall(BrokerInstallAssessment assessment)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        BrokerInstallStatus = assessment.StatusCode switch
        {
            "broker_not_found" =>
                "Broker absent · preview package stays unelevated",
            "broker_install_development" =>
                "Development broker · unsigned sibling, not a public install",
            "broker_install_protected" =>
                "Protected broker · signed install directory",
            "broker_unsigned" =>
                "Broker layout is protected but the executable is unsigned",
            "broker_signature_invalid" =>
                "Broker signature is invalid · privileged reads stay blocked",
            _ =>
                "Broker install is unprotected · production reads stay blocked",
        };
    }

    private static string Display(Observation observation) =>
        observation.State == ObservationState.Observed ? observation.Value! : "Unknown";

    private static string GetProductVersion()
    {
        Assembly assembly = typeof(MainWindowViewModel).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                   ?.InformationalVersion
               ?? assembly.GetName().Version?.ToString()
               ?? "unknown";
    }

    private static string FormatBatteryMode(BatteryChargeMode value) =>
        value switch
        {
            BatteryChargeMode.RapidCharge => "Rapid charge",
            BatteryChargeMode.Conservation => "Conservation",
            _ => "Normal",
        };

    private static string FormatThermalMode(ThermalMode value) =>
        value switch
        {
            ThermalMode.Quiet => "Quiet",
            ThermalMode.Balanced => "Balanced",
            ThermalMode.Performance => "Performance",
            ThermalMode.Extreme => "Extreme",
            ThermalMode.Custom => "Custom",
            _ => value.ToString(),
        };

    private static string FormatToggle(ToggleState value) =>
        value == ToggleState.Enabled ? "Enabled" : "Disabled";

    private static string FormatGpuMode(IntegratedGpuMode value) =>
        value switch
        {
            IntegratedGpuMode.Default => "Default",
            IntegratedGpuMode.IntegratedOnly => "Integrated only",
            IntegratedGpuMode.Automatic => "Automatic",
            _ => value.ToString(),
        };

    private static string FormatCapability(HardwareCapability value) =>
        value switch
        {
            HardwareCapability.BatteryConservationMode => "Battery conservation",
            HardwareCapability.BatteryRapidCharge => "Battery rapid charge",
            HardwareCapability.ThermalMode => "Thermal mode",
            HardwareCapability.FanControl => "Fan control",
            HardwareCapability.WhiteKeyboardBacklight => "White keyboard backlight",
            HardwareCapability.FourZoneRgbKeyboard => "Four-zone RGB keyboard",
            HardwareCapability.SpectrumKeyboard => "Spectrum keyboard",
            HardwareCapability.DisplayOverdrive => "Display overdrive",
            HardwareCapability.HybridGraphicsMode => "Hybrid graphics",
            HardwareCapability.GpuWorkingMode => "GPU working mode",
            _ => value.ToString(),
        };

    private static string FormatSupport(CapabilitySupport value) =>
        value switch
        {
            CapabilitySupport.Supported => "VERIFIED",
            CapabilitySupport.Unsupported => "NOT FOUND",
            CapabilitySupport.Degraded => "DEGRADED",
            _ => "CANDIDATE",
        };
}
