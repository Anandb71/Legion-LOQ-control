using System.Collections.Frozen;
using HidSharp;

namespace LegionLoqControl.Infrastructure.Windows.Hid;

internal interface IHidDeviceInventory
{
    IReadOnlySet<int> GetProductIds(int vendorId);
}

internal sealed class SystemHidDeviceInventory : IHidDeviceInventory
{
    public IReadOnlySet<int> GetProductIds(int vendorId) =>
        DeviceList.Local
            .GetHidDevices(vendorId)
            .Select(static device => device.ProductID)
            .ToFrozenSet();
}
