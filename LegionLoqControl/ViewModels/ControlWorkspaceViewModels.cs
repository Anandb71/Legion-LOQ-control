using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LegionLoqControl.Application.Hardware;
using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Infrastructure.Windows.Hardware;

namespace LegionLoqControl.ViewModels;

public sealed record NamedValue(string Label, string Value)
{
    public override string ToString() => Label;
}

public sealed partial class FanCurvePointViewModel : ObservableObject
{
    [ObservableProperty]
    private int _speed;

    public FanCurvePointViewModel(int index, byte speed, byte sensor)
    {
        Index = index;
        _speed = speed;
        Sensor = sensor;
    }

    public int Index { get; }

    public byte Sensor { get; }

    public string SpeedName => $"Fan point {Index + 1} speed";
}

public sealed partial class LightingWorkspaceViewModel : ObservableObject
{
    private readonly Func<string, Task> _apply;

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyLightingCommand))]
    private bool _canApply;

    [ObservableProperty]
    private string _effect = nameof(FourZoneEffect.Static);

    [ObservableProperty]
    private string _brightness = nameof(FourZoneKeyboardMode.High);

    [ObservableProperty]
    private byte _speed = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDividedColors))]
    [NotifyPropertyChangedFor(nameof(ShowSingleColor))]
    private bool _divideArea;

    [ObservableProperty]
    private string _zone1Hex = RgbColor.White.ToHex();

    [ObservableProperty]
    private string _zone2Hex = RgbColor.White.ToHex();

    [ObservableProperty]
    private string _zone3Hex = RgbColor.White.ToHex();

    [ObservableProperty]
    private string _zone4Hex = RgbColor.White.ToHex();

    public LightingWorkspaceViewModel(Func<string, Task> apply)
    {
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
    }

    public bool ShowDividedColors => IsVisible && DivideArea;

    public bool ShowSingleColor => IsVisible && !DivideArea;

    public IReadOnlyList<NamedValue> EffectOptions { get; } =
    [
        new("Off", nameof(FourZoneEffect.Off)),
        new("Static", nameof(FourZoneEffect.Static)),
        new("Breath", nameof(FourZoneEffect.Breath)),
        new("Wave", nameof(FourZoneEffect.Wave)),
        new("Smooth", nameof(FourZoneEffect.Smooth)),
    ];

    public IReadOnlyList<NamedValue> BrightnessOptions { get; } =
    [
        new("Off", nameof(FourZoneKeyboardMode.Off)),
        new("Low", nameof(FourZoneKeyboardMode.Low)),
        new("High", nameof(FourZoneKeyboardMode.High)),
    ];

    public IReadOnlyList<byte> SpeedOptions { get; } = [1, 2, 3, 4];

    public void Sync(FourZoneLightingState? state, bool available)
    {
        IsVisible = available;
        CanApply = available;
        if (state is null)
            return;

        FourZoneLightingState value = state.Value.Brightness == FourZoneKeyboardMode.Unknown
            ? FourZoneLightingState.Default
            : state.Value;
        Effect = value.Effect.ToString();
        Brightness = value.Brightness.ToString();
        Speed = value.Speed is 0 or > FourZoneLightingState.MaximumSpeed ? (byte)1 : value.Speed;
        DivideArea = value.DivideArea;
        Zone1Hex = value.Zone1.ToHex();
        Zone2Hex = value.Zone2.ToHex();
        Zone3Hex = value.Zone3.ToHex();
        Zone4Hex = value.Zone4.ToHex();
    }

    [RelayCommand(CanExecute = nameof(CanApplyLighting))]
    private Task ApplyLightingAsync()
    {
        if (!TryBuild(out FourZoneLightingState state))
            return Task.CompletedTask;

        return _apply(HardwareStateTokens.FormatLighting(state));
    }

    [RelayCommand]
    private Task ResetLightingToDefaultAsync() =>
        _apply(HardwareStateTokens.FormatLighting(FourZoneLightingState.Default));

    private bool CanApplyLighting() => CanApply && IsVisible;

    private bool TryBuild(out FourZoneLightingState state)
    {
        state = default;
        if (!Enum.TryParse(Effect, out FourZoneEffect effect) ||
            !Enum.IsDefined(effect) ||
            !Enum.TryParse(Brightness, out FourZoneKeyboardMode brightness) ||
            !Enum.IsDefined(brightness) ||
            brightness == FourZoneKeyboardMode.Unknown ||
            Speed is 0 or > FourZoneLightingState.MaximumSpeed ||
            !RgbColor.TryParseHex(Zone1Hex, out RgbColor zone1) ||
            !RgbColor.TryParseHex(DivideArea ? Zone2Hex : Zone1Hex, out RgbColor zone2) ||
            !RgbColor.TryParseHex(DivideArea ? Zone3Hex : Zone1Hex, out RgbColor zone3) ||
            !RgbColor.TryParseHex(DivideArea ? Zone4Hex : Zone1Hex, out RgbColor zone4))
        {
            return false;
        }

        state = new FourZoneLightingState(
            effect,
            brightness,
            Speed,
            DivideArea,
            zone1,
            zone2,
            zone3,
            zone4);
        return true;
    }
}

public sealed partial class FanCurveWorkspaceViewModel : ObservableObject
{
    private readonly Func<string, Task> _apply;
    private readonly Func<FanTableSnapshot?> _tryReadOem;
    private byte _fanId;
    private byte _sensorId;

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyFanCurveCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyOemFanTableCommand))]
    private bool _canApply;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyOemFanTableCommand))]
    private bool _canRestoreOem;

    public FanCurveWorkspaceViewModel(
        Func<string, Task> apply,
        Func<FanTableSnapshot?>? tryReadOem = null)
    {
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _tryReadOem = tryReadOem ?? (() => new OemFanTableStore().TryRead());
        Points.CollectionChanged += Points_CollectionChanged;
    }

    public ObservableCollection<FanCurvePointViewModel> Points { get; } = [];

    public void Sync(FanTableSnapshot? table)
    {
        if (table is null ||
            table.Value.PointCount is < 1 or > FanTableSnapshot.MaximumPoints ||
            table.Value.Points is null)
        {
            IsVisible = false;
            CanApply = false;
            CanRestoreOem = false;
            Points.Clear();
            return;
        }

        _fanId = table.Value.FanId;
        _sensorId = table.Value.SensorId;
        Points.Clear();
        for (int index = 0; index < table.Value.PointCount; index++)
        {
            FanTablePoint point = table.Value.Points[index];
            Points.Add(new FanCurvePointViewModel(index, point.Speed, point.Sensor));
        }

        IsVisible = true;
        CanApply = true;
        CanRestoreOem = _tryReadOem() is not null;
    }

    [RelayCommand(CanExecute = nameof(CanApplyFanCurve))]
    private Task ApplyFanCurveAsync()
    {
        if (!TryBuild(out FanTableSnapshot snapshot))
            return Task.CompletedTask;

        return _apply(HardwareStateTokens.FormatFanTable(snapshot));
    }

    [RelayCommand(CanExecute = nameof(CanRestore))]
    private Task ApplyOemFanTableAsync()
    {
        FanTableSnapshot? oem = _tryReadOem();
        return oem is null
            ? Task.CompletedTask
            : _apply(HardwareStateTokens.FormatFanTable(oem.Value));
    }

    private bool CanApplyFanCurve() => CanApply && IsVisible && Points.Count > 0;

    private bool CanRestore() => CanApply && CanRestoreOem;

    private bool TryBuild(out FanTableSnapshot snapshot)
    {
        snapshot = default;
        if (Points.Count is < 1 or > FanTableSnapshot.MaximumPoints)
            return false;

        var points = new FanTablePoint[Points.Count];
        for (int index = 0; index < Points.Count; index++)
        {
            int speed = Points[index].Speed;
            if (speed is < 0 or > 255)
                return false;

            points[index] = new FanTablePoint((byte)speed, Points[index].Sensor);
        }

        snapshot = new FanTableSnapshot(_fanId, _sensorId, points);
        return true;
    }

    private void Points_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        ApplyFanCurveCommand.NotifyCanExecuteChanged();
}
