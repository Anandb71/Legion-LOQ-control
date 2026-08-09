using CommunityToolkit.Mvvm.ComponentModel;
using LegionLoqControl.Domain.Diagnostics;

namespace LegionLoqControl.ViewModels;

public sealed class MachineSessionViewModel : ObservableObject
{
    private MachineSnapshot? _machineSnapshot;
    private HardwareStateSnapshot? _hardwareStateSnapshot;

    public MachineSnapshot? MachineSnapshot
    {
        get => _machineSnapshot;
        private set => SetProperty(ref _machineSnapshot, value);
    }

    public HardwareStateSnapshot? HardwareStateSnapshot
    {
        get => _hardwareStateSnapshot;
        private set => SetProperty(ref _hardwareStateSnapshot, value);
    }

    public void UpdateMachineSnapshot(MachineSnapshot snapshot) =>
        MachineSnapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));

    public void UpdateHardwareStateSnapshot(HardwareStateSnapshot snapshot) =>
        HardwareStateSnapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
}

