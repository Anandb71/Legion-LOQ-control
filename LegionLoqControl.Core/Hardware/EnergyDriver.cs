using System;
using System.Runtime.InteropServices;
using LegionLoqControl.Core.Native;
using Microsoft.Win32.SafeHandles;

namespace LegionLoqControl.Core.Hardware
{
    public class EnergyDriver : IDisposable
    {
        private const string DRIVER_PATH = @"\\.\EnergyDrv";
        private SafeFileHandle _handle;

        public bool Open()
        {
            _handle = NativeMethods.CreateFile(
                DRIVER_PATH,
                NativeMethods.GENERIC_READ | NativeMethods.GENERIC_WRITE,
                NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
                IntPtr.Zero,
                NativeMethods.OPEN_EXISTING,
                NativeMethods.FILE_ATTRIBUTE_NORMAL,
                IntPtr.Zero);

            return !_handle.IsInvalid;
        }

        public bool SetConservationMode(bool enabled)
        {
            // From LLT: Conservation Mode is ID 3 or 5?
            // Rust version used hardcoded logic.
            // LLT: SetBatteryChargeMode(Storage.GetConservationModeId(), enabled)
            // Usually Conservation Mode is ID 3.
            // Packet: [ID, Enable/Disable]
            return SendCommand(0x03, enabled ? 1 : 0);
        }

        public bool SetRapidCharge(bool enabled)
        {
            // Rapid Charge is usually ID 5.
            return SendCommand(0x05, enabled ? 1 : 0);
        }

        private bool SendCommand(byte featureId, int value)
        {
            if (_handle == null || _handle.IsInvalid)
            {
                if (!Open()) return false;
            }

            var input = new byte[8];
            Array.Copy(BitConverter.GetBytes((int)featureId), 0, input, 0, 4);
            Array.Copy(BitConverter.GetBytes(value), 0, input, 4, 4);

            uint bytesReturned;
            return NativeMethods.DeviceIoControl(
                _handle,
                NativeMethods.IOCTL_ENERGY_BATTERY_CHARGE_MODE,
                input,
                (uint)input.Length,
                null,
                0,
                out bytesReturned,
                IntPtr.Zero);
        }

        public void Dispose()
        {
            _handle?.Dispose();
        }
    }
}
