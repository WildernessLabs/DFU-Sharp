using System;
using System.Runtime.InteropServices;

namespace WildernessLabs.DfuSharp
{
    class NativeMethods
    {
        const string LIBUSB_LIBRARY = "libusb-1.0";

        [DllImport(LIBUSB_LIBRARY)]
        internal static extern int libusb_init(ref IntPtr ctx);

        [DllImport(LIBUSB_LIBRARY)]
        internal static extern void libusb_exit(IntPtr ctx);

        [DllImport(LIBUSB_LIBRARY)]
        internal static extern void libusb_set_debug(IntPtr ctx, LogLevel level);

        [DllImport(LIBUSB_LIBRARY)]
        internal static extern int libusb_get_device_list(IntPtr ctx, ref IntPtr list);

        [DllImport(LIBUSB_LIBRARY)]
        internal static extern int libusb_free_device_list(IntPtr list, int free_devices);

        [DllImport(LIBUSB_LIBRARY)]
        internal static extern int libusb_get_device_descriptor(IntPtr dev, ref DeviceDescriptor desc);

        [DllImport(LIBUSB_LIBRARY)]
        internal static extern int libusb_get_config_descriptor(IntPtr dev, ushort config_index, out IntPtr desc);

        [DllImport(LIBUSB_LIBRARY)]
        internal static extern int libusb_open(IntPtr dev, ref IntPtr handle);

        [DllImport(LIBUSB_LIBRARY)]
        internal static extern int libusb_close(IntPtr handle);

        [DllImport(LIBUSB_LIBRARY)]
        internal static extern int libusb_claim_interface(IntPtr dev, int interface_number);

        [DllImport(LIBUSB_LIBRARY)]
        internal static extern int libusb_set_interface_alt_setting(IntPtr dev, int interface_number, int alternate_setting);

        [DllImport(LIBUSB_LIBRARY)]
        internal static extern int libusb_control_transfer(IntPtr dev, byte bmRequestType, byte bRequest, ushort wValue, ushort wIndex, IntPtr data, ushort wLength, uint timeout);
    }
}
