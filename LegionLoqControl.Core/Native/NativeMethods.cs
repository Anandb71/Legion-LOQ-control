using global::System;
using global::System.Runtime.InteropServices;
using LegionLoqControl.Core.Safety;
using Microsoft.Win32.SafeHandles;

namespace LegionLoqControl.Core.Native
{
    public static class NativeMethods
    {
        public const uint GENERIC_READ = 0x80000000;
        public const uint GENERIC_WRITE = 0x40000000;
        public const uint FILE_SHARE_READ = 0x00000001;
        public const uint FILE_SHARE_WRITE = 0x00000002;
        public const uint OPEN_EXISTING = 3;
        public const uint FILE_ATTRIBUTE_NORMAL = 0x80;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern SafeFileHandle CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern unsafe bool DeviceIoControl(
            SafeFileHandle hDevice,
            uint dwIoControlCode,
            void* lpInBuffer,
            uint nInBufferSize,
            void* lpOutBuffer,
            uint nOutBufferSize,
            uint* lpBytesReturned,
            IntPtr lpOverlapped);

        /// <summary>
        /// Generic DeviceIoControl matching LLT's PInvokeExtensions pattern.
        /// </summary>
        public static unsafe bool DeviceIoControl<TIn, TOut>(SafeFileHandle hDevice, uint dwIoControlCode, TIn inVal, out TOut outVal)
            where TIn : struct
            where TOut : struct
        {
            HardwareWritePolicy.Demand($"EnergyDrv IOCTL 0x{dwIoControlCode:X8}");

            if (hDevice.IsInvalid || hDevice.IsClosed)
                throw new InvalidOperationException("The EnergyDrv handle is not usable.");

            var lpInBuffer = IntPtr.Zero;
            var lpOutBuffer = IntPtr.Zero;
            outVal = default;

            try
            {
                var nInBufferSize = Marshal.SizeOf<TIn>();
                var nOutBufferSize = Marshal.SizeOf<TOut>();

                lpInBuffer = Marshal.AllocHGlobal(nInBufferSize);
                lpOutBuffer = Marshal.AllocHGlobal(nOutBufferSize);

                Marshal.StructureToPtr(inVal, lpInBuffer, false);
                new Span<byte>(lpOutBuffer.ToPointer(), nOutBufferSize).Clear();

                uint bytesReturned = 0;
                var ret = DeviceIoControl(
                    hDevice,
                    dwIoControlCode,
                    lpInBuffer.ToPointer(),
                    (uint)nInBufferSize,
                    lpOutBuffer.ToPointer(),
                    (uint)nOutBufferSize,
                    &bytesReturned,
                    IntPtr.Zero);

                if (ret && bytesReturned <= nOutBufferSize)
                    outVal = Marshal.PtrToStructure<TOut>(lpOutBuffer);

                return ret && bytesReturned <= nOutBufferSize;
            }
            finally
            {
                Marshal.FreeHGlobal(lpInBuffer);
                Marshal.FreeHGlobal(lpOutBuffer);
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);
    }
}
