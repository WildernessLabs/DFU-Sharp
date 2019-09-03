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

            // set the LibUSB Log level
            NativeMethods.libusb_set_debug(handle, debug_level);
            if (ret != 0) {
                throw new Exception(string.Format("Error: {0} while trying to initialize libusb", ret));
            }
        }

        public void Dispose()
        {
            NativeMethods.libusb_exit(handle);
        }

        /// <summary>
        /// Enumerates all attached DFU devices.
        /// </summary>
        /// <returns></returns>
        public List<DfuDevice> GetDfuDevices()
        {
            return GetDfuDevices(0, 0);
        }

        /// <summary>
        /// Enumerates attached DFU devices that match the passed VendorID and
        /// ProductID.
        /// </summary>
        /// <param name="vendorID">Vendor ID to match. Pass 0 for all.</param>
        /// <param name="productID">Product ID to match. Pass 0 for all.</param>
        /// <returns></returns>
        public List<DfuDevice> GetDfuDevices(ushort vendorID, ushort productID)
        {
            var list = IntPtr.Zero;
            var dfu_devices = new List<DfuDevice>();

            // get the device list (returns a pointer)
            //var ret = NativeMethods.libusb_get_device_list(handle, ref list);
            var num_devices = NativeMethods.libusb_get_device_list(handle, ref list);

            // err check
            if (num_devices < 0) {
                throw new Exception(string.Format($"Error: {num_devices} while trying to get the device list"));
            }


            // make a copy of the device pointers
            var devices = new IntPtr[num_devices];
            Marshal.Copy(list, devices, 0, num_devices);

            // This is awful nested looping -- we should fix it.
            // loop through all the devices.
            //Console.WriteLine($"Total of ({num_devices}) devices found.");
            for (int i = 0; i < num_devices; i++) {
                //Console.WriteLine($"Checking device #{i}");

                var device_descriptor = new DeviceDescriptor();
                var ptr = IntPtr.Zero;

                // if the device descriptor is 0, go to next item.
                if (NativeMethods.libusb_get_device_descriptor(devices[i], ref device_descriptor) != 0) {
                    //Console.WriteLine($"Descriptor is empty, moving on.");
                    continue;
                }

                // filter on vendorID or productID if passed
                if (vendorID != 0 && device_descriptor.VendorID != vendorID
                    ||
                    productID != 0 && device_descriptor.ProductID != productID) {
                    ///Console.WriteLine($"Device doesn't match vendor (is:0x{device_descriptor.VendorID.ToString("x")}) or product id (is:0x{device_descriptor.ProductID.ToString("x")})");
                    continue;
                }
              
                //Console.WriteLine($"Found a matching device (vendor is:0x{device_descriptor.VendorID.ToString("x")}) (product is:0x{device_descriptor.ProductID.ToString("x")}).");

                // BUGBUG: serial number only has one digit. not getting marshalled correctly.
                //Console.WriteLine($"VendorID: {device_descriptor.VendorID}, ProductID: {device_descriptor.ProductID}, Serial: {device_descriptor.SerialNumber}");

                // loop through all configurations for the device
                for (int j = 0; j < device_descriptor.NumConfigurations; j++) {
                    //Console.WriteLine($"Found ({device_descriptor.NumConfigurations}) configs.");

                    // get the descriptor
                    var ret = NativeMethods.libusb_get_config_descriptor(devices[i], (ushort)j, out ptr);

                    // err check
                    if (ret < 0) {
                        throw new Exception(string.Format("Error: {0} while trying to get the config descriptor", ret));
                    }

                    // marshal the descriptor to our ConfigDescriptor struct
                    var config_descriptor = Marshal.PtrToStructure<ConfigDescriptor>(ptr);

                    // loop through all the interfaces in the Config descriptor
                    for (int k = 0; k < config_descriptor.bNumInterfaces; k++) {
                        // magic math
                        var p = config_descriptor.interfaces + j * Marshal.SizeOf<@Interface>();

                        // if empty, move to the next device
                        if (p == IntPtr.Zero) { continue; }

                        // get the interface
                        var @interface = Marshal.PtrToStructure<@Interface>(p);

                        // loop through all the alt settings in the interface
                        for (int l = 0; l < @interface.num_altsetting; l++) {
                            var interface_descriptor = @interface.Altsetting[l];

                            // Ensure this interface has DFU
                            if (interface_descriptor.InterfaceClass != 0xfe
                                ||
                                interface_descriptor.InterfaceSubClass != 0x1) {
                                continue;
                            }

                            // get the DFU descriptor for the interface
                            var dfu_descriptor = FindDescriptor(
                                interface_descriptor.Extra,
                                interface_descriptor.Extra_length,
                                (byte)Consts.USB_DT_DFU);

                            // finally, if we got here; add the device to the list
                            // BUGBUG? this is the DFU descriptor for just this particular
                            // interface, but what if they're different between interfaces?
                            if (dfu_descriptor != null) {
                                dfu_devices.Add(new DfuDevice(
                                    devices[i],
                                    device_descriptor,
                                    interface_descriptor,
                                    dfu_descriptor.Value));
                            }
                        }
                    }
                }

                //Console.WriteLine($"Moving on to the next device in the list, i={i}.");
            }

            // release the device list on LibUsb
            // TODO: put this in a finally?
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
