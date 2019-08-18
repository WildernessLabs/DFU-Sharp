using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace WildernessLabs.DfuSharp
{
    public class Context : IDisposable
    {
        IntPtr handle;
        public Context(LogLevel debug_level = LogLevel.None)
        {
            var ret = NativeMethods.libusb_init(ref handle);

            NativeMethods.libusb_set_debug(handle, debug_level);
            if (ret != 0)
                throw new Exception(string.Format("Error: {0} while trying to initialize libusb", ret));
        }

        public void Dispose()
        {
            NativeMethods.libusb_exit(handle);
        }

        public List<DfuDevice> GetDfuDevices(ushort idVendor, ushort idProduct)
        {
            var list = IntPtr.Zero;
            var dfu_devices = new List<DfuDevice>();
            var ret = NativeMethods.libusb_get_device_list(handle, ref list);

            if (ret < 0)
                throw new Exception(string.Format("Error: {0} while trying to get the device list", ret));

            var devices = new IntPtr[ret];
            Marshal.Copy(list, devices, 0, ret);

            // This is awful nested looping -- we should fix it.
            for (int i = 0; i < ret; i++) {
                var device_descriptor = new DeviceDescriptor();
                var ptr = IntPtr.Zero;

                if (NativeMethods.libusb_get_device_descriptor(devices[i], ref device_descriptor) != 0)
                    continue;

                if (device_descriptor.idVendor != idVendor && device_descriptor.idProduct != idProduct)
                    continue;

                for (int j = 0; j < device_descriptor.bNumConfigurations; j++) {
                    ret = NativeMethods.libusb_get_config_descriptor(devices[i], (ushort)j, out ptr);

                    if (ret < 0)
                        throw new Exception(string.Format("Error: {0} while trying to get the config descriptor", ret));

                    var config_descriptor = Marshal.PtrToStructure<ConfigDescriptor>(ptr);

                    for (int k = 0; k < config_descriptor.bNumInterfaces; k++) {
                        var p = config_descriptor.interfaces + j * Marshal.SizeOf<@Interface>();

                        if (p == IntPtr.Zero)
                            continue;

                        var @interface = Marshal.PtrToStructure<@Interface>(p);
                        for (int l = 0; l < @interface.num_altsetting; l++) {
                            var interface_descriptor = @interface.Altsetting[l];

                            // Ensure this is a DFU descriptor
                            if (interface_descriptor.bInterfaceClass != 0xfe || interface_descriptor.bInterfaceSubClass != 0x1)
                                continue;

                            var dfu_descriptor = FindDescriptor(interface_descriptor.extra, interface_descriptor.extra_length, (byte)Consts.USB_DT_DFU);
                            if (dfu_descriptor != null)
                                dfu_devices.Add(new DfuDevice(devices[i], interface_descriptor, dfu_descriptor.Value));
                        }
                    }
                }
            }

            NativeMethods.libusb_free_device_list(list, 1);
            return dfu_devices;
        }

        static DfuFunctionDescriptor? FindDescriptor(IntPtr desc_list, int list_len, byte desc_type)
        {
            int p = 0;

            while (p + 1 < list_len) {
                int len, type;

                len = Marshal.ReadByte(desc_list, p);
                type = Marshal.ReadByte(desc_list, p + 1);

                if (type == desc_type) {
                    return Marshal.PtrToStructure<DfuFunctionDescriptor>(desc_list + p);
                }
                p += len;
            }

            return null;
        }

    }
}
