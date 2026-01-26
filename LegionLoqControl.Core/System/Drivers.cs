using global::System;
using Microsoft.Win32.SafeHandles;
using LegionLoqControl.Core.Native;

namespace LegionLoqControl.Core.System
{
    public static class Drivers
    {
        public const uint IOCTL_ENERGY_BATTERY_CHARGE_MODE = 0x831020F8;

        private static readonly object Lock = new();
        private static SafeFileHandle? _energy;

        public static SafeFileHandle GetEnergy()
        {
            if (_energy is not null)
                return _energy;

            lock (Lock)
            {
                if (_energy is not null)
                    return _energy;

                var handle = NativeMethods.CreateFile(@"\\.\EnergyDrv",
                    NativeMethods.GENERIC_READ | NativeMethods.GENERIC_WRITE,
                    NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    NativeMethods.OPEN_EXISTING,
                    NativeMethods.FILE_ATTRIBUTE_NORMAL,
                    IntPtr.Zero);

                if (handle.IsInvalid)
                    throw new InvalidOperationException("Energy driver handle is invalid. Ensure Energy driver is installed.");

                _energy = handle;
            }

            return _energy;
        }
    }
}
