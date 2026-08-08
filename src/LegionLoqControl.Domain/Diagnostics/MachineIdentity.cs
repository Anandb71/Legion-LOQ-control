namespace LegionLoqControl.Domain.Diagnostics;

public sealed record MachineIdentity
{
    public MachineIdentity(
        Observation manufacturer,
        Observation productName,
        Observation model,
        Observation machineType,
        Observation biosVersion)
    {
        Manufacturer = manufacturer ?? throw new ArgumentNullException(nameof(manufacturer));
        ProductName = productName ?? throw new ArgumentNullException(nameof(productName));
        Model = model ?? throw new ArgumentNullException(nameof(model));
        MachineType = machineType ?? throw new ArgumentNullException(nameof(machineType));
        BiosVersion = biosVersion ?? throw new ArgumentNullException(nameof(biosVersion));
    }

    public Observation Manufacturer { get; }

    public Observation ProductName { get; }

    public Observation Model { get; }

    public Observation MachineType { get; }

    public Observation BiosVersion { get; }
}
