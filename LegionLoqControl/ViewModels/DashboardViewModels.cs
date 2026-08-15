using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LegionLoqControl.Application.Automation;
using LegionLoqControl.Application.Broker;
using LegionLoqControl.Application.Diagnostics;
using LegionLoqControl.Application.Hardware;
using LegionLoqControl.Application.Profiles;
using LegionLoqControl.Contracts.Broker;
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

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyOptionCommand))]
    private bool _canApply;

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

    public ObservableCollection<HardwareStateOptionViewModel> Options { get; } = [];

    public Func<string, Task>? ApplyAsync { get; set; }

    [RelayCommand(CanExecute = nameof(CanApplyOption))]
    private async Task ApplyOptionAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || ApplyAsync is null)
            return;

        await ApplyAsync(token).ConfigureAwait(true);
    }

    private bool CanApplyOption() => CanApply && ApplyAsync is not null;

    public void Apply<T>(
        HardwareReadResult<T> result,
        Func<T, string> formatter,
        string? successDetail = null)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(formatter);

        if (result.Status == HardwareReadStatus.Success && result.Value.HasValue)
        {
            Value = formatter(result.Value.Value);
            Detail = successDetail ??
                "Verified. Choose a value to apply it through the session broker.";
            State = DashboardStateKind.Success;
            CanApply = ApplyAsync is not null;
            ApplyOptionCommand.NotifyCanExecuteChanged();
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
        CanApply = false;
        ApplyOptionCommand.NotifyCanExecuteChanged();
    }
}

public sealed record HardwareStateOptionViewModel(string Label, string Token);

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

    partial void OnIsBusyChanged(bool value)
    {
        if (value)
        {
            SetAppliesEnabled(false);
            return;
        }

        if (Session.HardwareStateSnapshot is { } snapshot)
            ApplyVerifiedSnapshot(snapshot);
    }

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
        "Inventory is unelevated. Windows will ask once so the session broker can read and apply hardware.";

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
        var profilePreviewService = new ProfilePreviewService();
        ProfileWorkspace = new ProfileWorkspaceViewModel(
            sharedProfileStore,
            profilePreviewService,
            Session,
            ApplyProfileBatchAsync);
        AutomationWorkspace = new AutomationWorkspaceViewModel(
            automationRuleStore ?? JsonAutomationRuleStore.CreateDefault(),
            sharedProfileStore,
            powerSourceService ?? new PowerSourceService(new WindowsPowerSourceReader()),
            automationPreviewService ?? new AutomationPreviewService(),
            Session,
            profilePreviewService,
            new AutomationRunService(),
            ApplyProfileBatchAsync);
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
        Keyboard = new HardwareStateCardViewModel(
            "KEYBOARD",
            "4-zone RGB brightness",
            "Elevation required for the ITE lighting controller");
        Fans = new HardwareStateCardViewModel(
            "FANS",
            "OEM firmware table",
            "Elevation required by Lenovo WMI");

        AddOptions(
            Battery,
            ("Normal", nameof(BatteryChargeMode.Normal)),
            ("Conservation", nameof(BatteryChargeMode.Conservation)),
            ("Rapid charge", nameof(BatteryChargeMode.RapidCharge)));
        AddOptions(Thermal, ("Quiet", nameof(ThermalMode.Quiet)), ("Balanced", nameof(ThermalMode.Balanced)), ("Performance", nameof(ThermalMode.Performance)), ("Extreme", nameof(ThermalMode.Extreme)));
        AddOptions(DisplayOverdrive, ("Off", nameof(ToggleState.Disabled)), ("On", nameof(ToggleState.Enabled)));
        AddOptions(IntegratedGpu, ("Default", nameof(IntegratedGpuMode.Default)), ("Integrated only", nameof(IntegratedGpuMode.IntegratedOnly)), ("Automatic", nameof(IntegratedGpuMode.Automatic)));
        AddOptions(
            Keyboard,
            ("Off", nameof(FourZoneKeyboardMode.Off)),
            ("Low", nameof(FourZoneKeyboardMode.Low)),
            ("High", nameof(FourZoneKeyboardMode.High)));
        Battery.ApplyAsync = token => ApplyWriteAsync(HardwareWriteTarget.BatteryChargeMode, token);
        Thermal.ApplyAsync = token => ApplyWriteAsync(HardwareWriteTarget.ThermalMode, token);
        DisplayOverdrive.ApplyAsync = token => ApplyWriteAsync(HardwareWriteTarget.DisplayOverdrive, token);
        IntegratedGpu.ApplyAsync = token => ApplyWriteAsync(HardwareWriteTarget.IntegratedGpuMode, token);
        Keyboard.ApplyAsync = token => ApplyWriteAsync(HardwareWriteTarget.FourZoneKeyboard, token);
    }

    public HardwareStateCardViewModel Battery { get; }

    public HardwareStateCardViewModel Thermal { get; }

    public HardwareStateCardViewModel DisplayOverdrive { get; }

    public HardwareStateCardViewModel IntegratedGpu { get; }

    public HardwareStateCardViewModel Keyboard { get; }

    public HardwareStateCardViewModel Fans { get; }

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
            await StartHardwareSessionAsync().ConfigureAwait(true);
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
        _dataSource.Dispose();
    }

    [RelayCommand(CanExecute = nameof(CanRefreshHardwareState))]
    private async Task RefreshHardwareStateAsync()
    {
        IsBusy = true;
        RefreshButtonText = "Reading hardware…";
        BannerTitle = "Hardware refresh";
        BannerMessage = "Reusing the session broker. Windows will not ask again unless the session dropped.";
        BannerState = DashboardStateKind.Warning;

        try
        {
            HardwareStateSnapshot snapshot = await _dataSource
                .ReadHardwareStateAsync(_lifetime.Token)
                .ConfigureAwait(true);
            ApplyVerifiedSnapshot(snapshot);

            HardwareReadStatus[] statuses =
            [
                snapshot.BatteryChargeMode.Status,
                snapshot.ThermalMode.Status,
                snapshot.DisplayOverdrive.Status,
                snapshot.IntegratedGpuMode.Status,
                snapshot.FourZoneKeyboard.Status,
                snapshot.FanTable.Status,
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

    private async Task StartHardwareSessionAsync()
    {
        RefreshButtonText = "Starting hardware session…";
        BannerTitle = "Hardware session requested";
        BannerMessage =
            "Windows will ask once. The first read can take a few seconds; later changes reuse this session.";
        BannerState = DashboardStateKind.Warning;
        try
        {
            HardwareStateSnapshot snapshot = await _dataSource
                .ReadHardwareStateAsync(_lifetime.Token)
                .ConfigureAwait(true);
            ApplyVerifiedSnapshot(snapshot);

            HardwareReadStatus[] statuses =
            [
                snapshot.BatteryChargeMode.Status,
                snapshot.ThermalMode.Status,
                snapshot.DisplayOverdrive.Status,
                snapshot.IntegratedGpuMode.Status,
                snapshot.FourZoneKeyboard.Status,
                snapshot.FanTable.Status,
            ];
            int successCount = statuses.Count(static status => status == HardwareReadStatus.Success);
            BannerTitle = successCount == statuses.Length
                ? "Hardware session ready"
                : "Hardware session partially ready";
            BannerMessage =
                $"{successCount}/{statuses.Length} reads succeeded · later changes will not ask again unless the session drops";
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
        }
    }

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
        BannerTitle = errorCode switch
        {
            "broker_elevation_cancelled" => "Elevation cancelled",
            "write_in_progress" => "Write already running",
            _ => "Hardware state unavailable",
        };
        BannerMessage = errorCode switch
        {
            "broker_elevation_cancelled" =>
                "Windows approval was cancelled. No privileged hardware read ran.",
            "write_in_progress" =>
                "Another hardware write is already running. Wait for it to finish.",
            "write_batch_invalid" or "profile_apply_blocked" =>
                "The write batch was rejected before any setter ran.",
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
            "thermal_expected_mismatch" or "overdrive_expected_mismatch" or
                "integrated_gpu_expected_mismatch" or "keyboard_expected_mismatch" =>
                "Hardware changed before the write. Refresh, then apply again.",
            "thermal_readback_mismatch" or "overdrive_readback_mismatch" or
                "integrated_gpu_readback_mismatch" or "keyboard_readback_mismatch" =>
                "The setter ran, but readback did not match the requested value.",
            "battery_expected_mismatch" =>
                "Battery mode changed before the write. Refresh, then apply again.",
            "battery_readback_mismatch" =>
                "The battery setter ran, but readback did not match the requested value.",
            "thermal_custom_unsupported" =>
                "Custom thermal mode is not writable from this app.",
            _ => "The broker could not complete the privileged request.",
        };
        BannerState = errorCode is "broker_elevation_cancelled" or "write_in_progress"
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

    private async Task ApplyWriteAsync(HardwareWriteTarget target, string desired)
    {
        HardwareStateSnapshot? current = Session.HardwareStateSnapshot;
        if (current is null)
            return;

        string? expected = target switch
        {
            HardwareWriteTarget.ThermalMode when
                current.ThermalMode is { Status: HardwareReadStatus.Success, Value: { } value } =>
                value.ToString(),
            HardwareWriteTarget.DisplayOverdrive when
                current.DisplayOverdrive is { Status: HardwareReadStatus.Success, Value: { } value } =>
                value.ToString(),
            HardwareWriteTarget.IntegratedGpuMode when
                current.IntegratedGpuMode is { Status: HardwareReadStatus.Success, Value: { } value } =>
                value.ToString(),
            HardwareWriteTarget.BatteryChargeMode when
                current.BatteryChargeMode is { Status: HardwareReadStatus.Success, Value: { } value } =>
                value.ToString(),
            HardwareWriteTarget.FourZoneKeyboard when
                current.FourZoneKeyboard is { Status: HardwareReadStatus.Success, Value: { } value } =>
                value.ToString(),
            _ => null,
        };
        if (expected is null ||
            string.Equals(expected, desired, StringComparison.Ordinal))
            return;

        IsBusy = true;
        RefreshButtonText = "Applying change…";
        BannerTitle = "Hardware change requested";
        BannerMessage =
            "The session broker applies one typed change, then reads it back. No extra Windows prompt.";
        BannerState = DashboardStateKind.Warning;
        try
        {
            HardwareStateSnapshot snapshot = await _dataSource
                .ApplyHardwareWriteAsync(target, expected, desired, _lifetime.Token)
                .ConfigureAwait(true);
            ApplyVerifiedSnapshot(snapshot);
            BannerTitle = "Hardware change verified";
            BannerMessage = $"Applied {desired} · read back at {LastUpdated}";
            BannerState = DashboardStateKind.Success;
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
            ApplyBrokerFailure("broker_write_failed");
        }
        finally
        {
            RefreshButtonText = "Read hardware state";
            IsBusy = false;
        }
    }

    private async ValueTask<HardwareStateSnapshot> ApplyProfileBatchAsync(
        IReadOnlyList<HardwareWritePlanItem> operations,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operations);
        HardwareWriteOperation[] batch = operations
            .Select(static item => new HardwareWriteOperation(
                MapWriteTarget(item.Kind),
                item.Expected,
                item.Desired))
            .ToArray();

        IsBusy = true;
        RefreshButtonText = "Applying profile…";
        BannerTitle = "Hardware change requested";
        BannerMessage = operations.Count == 1
            ? "The session broker applies one typed change, then reads it back. No extra Windows prompt."
            : "The session broker applies the would-change targets, then reads them back. No extra Windows prompt.";
        BannerState = DashboardStateKind.Warning;
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _lifetime.Token);
            HardwareStateSnapshot snapshot = await _dataSource
                .ApplyHardwareWriteBatchAsync(batch, linked.Token)
                .ConfigureAwait(true);
            ApplyVerifiedSnapshot(snapshot);
            BannerTitle = "Hardware change verified";
            BannerMessage = operations.Count == 1
                ? $"Applied {operations[0].Desired} · read back at {LastUpdated}"
                : $"Applied {operations.Count} profile targets · read back at {LastUpdated}";
            BannerState = DashboardStateKind.Success;
            return snapshot;
        }
        catch (DashboardDataSourceException exception)
        {
            ApplyBrokerFailure(exception.ErrorCode);
            throw;
        }
        catch (Exception)
        {
            ApplyBrokerFailure("broker_write_failed");
            throw;
        }
        finally
        {
            RefreshButtonText = "Read hardware state";
            IsBusy = false;
        }
    }

    private void ApplyVerifiedSnapshot(HardwareStateSnapshot snapshot)
    {
        HardwareStateSnapshot? current = Session.HardwareStateSnapshot;
        if (snapshot.FanTable.Status != HardwareReadStatus.Success &&
            current?.FanTable.Status == HardwareReadStatus.Success)
        {
            snapshot = snapshot with { FanTable = current.FanTable };
        }

        Session.UpdateHardwareStateSnapshot(snapshot);
        Battery.Apply(snapshot.BatteryChargeMode, FormatBatteryMode);
        Thermal.Apply(snapshot.ThermalMode, FormatThermalMode);
        DisplayOverdrive.Apply(snapshot.DisplayOverdrive, FormatToggle);
        IntegratedGpu.Apply(snapshot.IntegratedGpuMode, FormatGpuMode);
        Keyboard.Apply(snapshot.FourZoneKeyboard, FormatKeyboardMode);
        Fans.Apply(
            snapshot.FanTable,
            FormatFanTable,
            "Read-only OEM table. Curve writes stay disabled.");
        LastUpdated = snapshot.ObservedAt.ToLocalTime().ToString("HH:mm:ss");
        if (IsBusy)
            SetAppliesEnabled(false);
    }

    private void SetAppliesEnabled(bool enabled)
    {
        if (enabled)
            return;

        Battery.CanApply = false;
        Thermal.CanApply = false;
        DisplayOverdrive.CanApply = false;
        IntegratedGpu.CanApply = false;
        Keyboard.CanApply = false;
    }

    private static HardwareWriteTarget MapWriteTarget(HardwareWriteKind kind) =>
        kind switch
        {
            HardwareWriteKind.ThermalMode => HardwareWriteTarget.ThermalMode,
            HardwareWriteKind.DisplayOverdrive => HardwareWriteTarget.DisplayOverdrive,
            HardwareWriteKind.IntegratedGpuMode => HardwareWriteTarget.IntegratedGpuMode,
            HardwareWriteKind.BatteryChargeMode => HardwareWriteTarget.BatteryChargeMode,
            HardwareWriteKind.FourZoneKeyboard => HardwareWriteTarget.FourZoneKeyboard,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    private static void AddOptions(
        HardwareStateCardViewModel card,
        params (string Label, string Token)[] options)
    {
        foreach ((string label, string token) in options)
            card.Options.Add(new HardwareStateOptionViewModel(label, token));
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

    private static string FormatFanTable(FanTableSnapshot value)
    {
        int count = value.PointCount;
        if (count <= 0 || value.Points is null)
            return "Empty";

        byte minSpeed = value.Points.Min(static point => point.Speed);
        byte maxSpeed = value.Points.Max(static point => point.Speed);
        byte minSensor = value.Points.Min(static point => point.Sensor);
        byte maxSensor = value.Points.Max(static point => point.Sensor);
        return count == 1
            ? $"1 OEM point · speed {minSpeed} · sensor {minSensor}"
            : $"{count} OEM points · speed {minSpeed}–{maxSpeed} · sensor {minSensor}–{maxSensor}";
    }

    private static string FormatKeyboardMode(FourZoneKeyboardMode value) =>
        value switch
        {
            FourZoneKeyboardMode.Off => "Off",
            FourZoneKeyboardMode.Low => "Low",
            FourZoneKeyboardMode.High => "High",
            _ => "Present",
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
