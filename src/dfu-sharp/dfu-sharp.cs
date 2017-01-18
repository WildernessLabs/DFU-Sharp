using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DfuSharp
{
    enum Consts
    {
        USB_DT_DFU = 0x21
    }

    public enum LogLevel
    {
        None = 0,
        Error,
        Warning,
        Info,
        Debug
    }

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

    struct DeviceDescriptor
    {
        public byte bLength;
        public byte bDescriptorType;
        public ushort bcdUSB;
        public byte bDeviceClass;
        public byte bDeviceSubClass;
        public byte bDeviceProtocol;
        public byte bMaxPacketSize0;
        public ushort idVendor;
        public ushort idProduct;
        public ushort bcdDevice;
        public byte iManufacturer;
        public byte iProduct;
        public byte iSerialNumber;
        public byte bNumConfigurations;
    }

    struct ConfigDescriptor
    {
        public byte bLength;
        public byte bDescriptorType;
        public ushort wTotalLength;
        public byte bNumInterfaces;
        public byte bConfigurationValue;
        public byte iConfiguration;
        public byte bmAttributes;
        public byte MaxPower;
        public IntPtr interfaces;
        public IntPtr extra;
        public int extra_length;
    }

    struct @Interface
    {
        public IntPtr altsetting;
        public int num_altsetting;

        public InterfaceDescriptor[] Altsetting
        {
            get
            {
                var descriptors = new InterfaceDescriptor[num_altsetting];
                for (int i = 0; i < num_altsetting; i++)
                {
                    descriptors[i] = Marshal.PtrToStructure<InterfaceDescriptor>(altsetting + i * Marshal.SizeOf<InterfaceDescriptor>());
                }

                return descriptors;
            }
        }
    }

    public struct InterfaceDescriptor
    {
        public byte bLength;
        public byte bDescriptorType;
        public byte bInterfaceNumber;
        public byte bAlternateSetting;
        public byte bNumEndpoints;
        public byte bInterfaceClass;
        public byte bInterfaceSubClass;
        public byte bInterfaceProtocol;
        public byte iInterface;
        public IntPtr endpoint;
        public IntPtr extra;
        public int extra_length;
    }

    public struct DfuFunctionDescriptor
    {
        public byte bLength;
        public byte bDescriptorType;
        public byte bmAttributes;
        public ushort wDetachTimeOut;
        public ushort wTransferSize;
        public ushort bcdDFUVersion;
    }

    public delegate void UploadingEventHandler(object sender, UploadingEventArgs e);

    public class UploadingEventArgs : EventArgs
    {
        public int BytesUploaded { get; private set; }

        public UploadingEventArgs(int bytesUpladed)
        {
            this.BytesUploaded = bytesUpladed;
        }
    }

    public class DfuDevice : IDisposable
    {
        // FIXME: Figure out why dfu_function_descriptor.wTransferSize isn't right and why STM isn't reporting flash_size right
        const int flash_size = 0x200000;
        const int transfer_size = 0x800;
        const int address = 0x08000000;

        IntPtr handle;
        InterfaceDescriptor interface_descriptor;
        DfuFunctionDescriptor dfu_descriptor;

        public DfuDevice(IntPtr device, InterfaceDescriptor interface_descriptor, DfuFunctionDescriptor dfu_descriptor)
        {
            this.interface_descriptor = interface_descriptor;
            this.dfu_descriptor = dfu_descriptor;
            if (NativeMethods.libusb_open(device, ref handle) < 0)
                throw new Exception("Error opening device");
        }

        public event UploadingEventHandler Uploading;

        protected virtual void OnUploaded(UploadingEventArgs e)
        {
            if (Uploading != null)
                Uploading(this, e);
        }
        public void ClaimInterface()
        {
            NativeMethods.libusb_claim_interface(handle, interface_descriptor.bInterfaceNumber);
        }

        public void SetInterfaceAltSetting(int alt_setting)
        {
            NativeMethods.libusb_set_interface_alt_setting(handle, interface_descriptor.bInterfaceNumber, alt_setting);
        }

        public void Clear()
        {
            var state = (byte)0xff;

            while (state != 0 && state != 2)
            {
                state = GetStatus(handle, interface_descriptor.bInterfaceNumber);

                switch (state)
                {
                    case 5:
                    case 9:
                        Abort(handle, interface_descriptor.bInterfaceNumber);
                        break;
                    case 10:
                        ClearStatus(handle, interface_descriptor.bInterfaceNumber);
                        break;
                    default:
                        break;
                }
            }
        }

        public void Upload(FileStream file)
        {
            var buffer = new byte[transfer_size];
            var mem = Marshal.AllocHGlobal(transfer_size);

            try
            {
                using (var reader = new BinaryReader(file))
                {
                    for (var pos = 0; pos < flash_size; pos += transfer_size)
                    {
                        int write_address = address + pos;
                        var count = reader.Read(buffer, 0, transfer_size);

                        if (count == 0)
                            return;

                        EraseSector(write_address);
                        SetAddress(write_address);

                        Marshal.Copy(buffer, 0, mem, count);


                        var ret = NativeMethods.libusb_control_transfer(
                                                    handle,
                                                    0x00 /*LIBUSB_ENDPOINT_OUT*/ | (0x1 << 5) /*LIBUSB_REQUEST_TYPE_CLASS*/ | 0x01 /*LIBUSB_RECIPIENT_INTERFACE*/,
                                                    1 /*DFU_DNLOAD*/,
                                                    2,
                                                    interface_descriptor.bInterfaceNumber,
                                                    mem,
                                                    (ushort)count,
                                                    5000);

                        if (ret < 0)
                            throw new Exception(string.Format("Error with WRITE_SECTOR: {0}", ret));
                        var status = GetStatus(handle, interface_descriptor.bInterfaceNumber);

                        while (status == 4)
                        {
                            Thread.Sleep(100);
                            status = GetStatus(handle, interface_descriptor.bInterfaceNumber);
                        }
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(mem);
            }
        }

        public void Upload(byte[] data, int? baseAddress = null)
        {
            var mem = Marshal.AllocHGlobal(transfer_size);

            try
            {
                for (var pos = 0; pos < flash_size; pos += transfer_size)
                {
                    int write_address = (baseAddress ?? address) + pos;
                    var count = Math.Min(data.Length - pos, transfer_size);

                    if (count <= 0)
                        return;

                    SetAddress(write_address);

                    Marshal.Copy(data, pos, mem, count);

                    var ret = NativeMethods.libusb_control_transfer(
                                                handle,
                                                0x00 /*LIBUSB_ENDPOINT_OUT*/ | (0x1 << 5) /*LIBUSB_REQUEST_TYPE_CLASS*/ | 0x01 /*LIBUSB_RECIPIENT_INTERFACE*/,
                                                1 /*DFU_DNLOAD*/,
                                                2,
                                                interface_descriptor.bInterfaceNumber,
                                                mem,
                                                (ushort)count,
                                                5000);

                    if (ret < 0)
                        throw new Exception(string.Format("Error with WRITE_SECTOR: {0}", ret));
                    var status = GetStatus(handle, interface_descriptor.bInterfaceNumber);

                    while (status == 4)
                    {
                        Thread.Sleep(100);
                        status = GetStatus(handle, interface_descriptor.bInterfaceNumber);
                    }
                    Uploading(this, new UploadingEventArgs(pos));
                }
            }
            finally
            {
            Marshal.FreeHGlobal(mem);
            }
        }

        public void Download(FileStream file)
        {
            var buffer = new byte[transfer_size];
            var mem = Marshal.AllocHGlobal(transfer_size);

            try
            {
                int count = 0;
                ushort transaction = 2;
                using (var writer = new BinaryWriter(file))
                {
                    while (count < flash_size)
                    {
                        int ret = NativeMethods.libusb_control_transfer(
                                                                handle,
                                                                0x80 /*LIBUSB_ENDPOINT_IN*/ | (0x1 << 5) /*LIBUSB_REQUEST_TYPE_CLASS*/ | 0x01 /*LIBUSB_RECIPIENT_INTERFACE*/,
                                                                2 /*DFU_UPLOAD*/,
                                                                transaction++,
                                                                interface_descriptor.bInterfaceNumber,
                                                                mem,
                                                                transfer_size,
                                                                5000);
                        if (ret < 0)
                            throw new Exception(string.Format("Error with DFU_UPLOAD: {0}", ret));

                        count += ret;
                        Marshal.Copy(mem, buffer, 0, ret);
                        writer.Write(buffer, 0, ret);
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(mem);
            }
        }

        void EraseSector(int address)
        {
            var mem = Marshal.AllocHGlobal(5);

            try
            {
                Marshal.WriteByte(mem, 0, 0x41);
                Marshal.WriteByte(mem, 1, (byte)((address >> 0) & 0xff));
                Marshal.WriteByte(mem, 2, (byte)((address >> 8) & 0xff));
                Marshal.WriteByte(mem, 3, (byte)((address >> 16) & 0xff));
                Marshal.WriteByte(mem, 4, (byte)((address >> 24) & 0xff));


                var ret = NativeMethods.libusb_control_transfer(
                                        handle,
                                        0x00 /*LIBUSB_ENDPOINT_OUT*/ | (0x1 << 5) /*LIBUSB_REQUEST_TYPE_CLASS*/ | 0x01 /*LIBUSB_RECIPIENT_INTERFACE*/,
                                        1 /*DFU_DNLOAD*/,
                                        0,
                                        interface_descriptor.bInterfaceNumber,
                                        mem,
                                        5,
                                        5000);

                if (ret < 0)
                    throw new Exception(string.Format("Error with ERASE_SECTOR: {0}", ret));

                var status = GetStatus(handle, interface_descriptor.bInterfaceNumber);

                while (status == 4)
                {
                    Thread.Sleep(100);
                    status = GetStatus(handle, interface_descriptor.bInterfaceNumber);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(mem);
            }
        }

        void SetAddress(int address)
        {
            var mem = Marshal.AllocHGlobal(5);

            try
            {
                Marshal.WriteByte(mem, 0, 0x21);
                Marshal.WriteByte(mem, 1, (byte)((address >> 0) & 0xff));
                Marshal.WriteByte(mem, 2, (byte)((address >> 8) & 0xff));
                Marshal.WriteByte(mem, 3, (byte)((address >> 16) & 0xff));
                Marshal.WriteByte(mem, 4, (byte)((address >> 24) & 0xff));


                var ret = NativeMethods.libusb_control_transfer(
                                        handle,
                                        0x00 /*LIBUSB_ENDPOINT_OUT*/ | (0x1 << 5) /*LIBUSB_REQUEST_TYPE_CLASS*/ | 0x01 /*LIBUSB_RECIPIENT_INTERFACE*/,
                                        1 /*DFU_DNLOAD*/,
                                        0,
                                        interface_descriptor.bInterfaceNumber,
                                        mem,
                                        5,
                                        5000);

                if (ret < 0)
                    throw new Exception(string.Format("Error with ERASE_SECTOR: {0}", ret));

                var status = GetStatus(handle, interface_descriptor.bInterfaceNumber);

                while (status == 4)
                {
                    Thread.Sleep(100);
                    status = GetStatus(handle, interface_descriptor.bInterfaceNumber);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(mem);
            }
        }

        static byte GetStatus(IntPtr dev, ushort interface_number)
        {
            var buffer = Marshal.AllocHGlobal(6);

            try
            {
                int ret = NativeMethods.libusb_control_transfer(
                    dev,
                    0x80 /*LIBUSB_ENDPOINT_IN*/ | (0x1 << 5) /*LIBUSB_REQUEST_TYPE_CLASS*/ | 0x01 /*LIBUSB_RECIPIENT_INTERFACE*/,
                    3 /*DFU_GETSTATUS*/,
                    0,
                    interface_number,
                    buffer,
                    6,
                    5000);

                if (ret == 6)
                    return Marshal.ReadByte(buffer, 4);

                return 0xff;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        static void Abort(IntPtr dev, ushort interface_number)
        {
            int ret = NativeMethods.libusb_control_transfer(
                dev,
                0x00 /*LIBUSB_ENDPOINT_OUT*/ | (0x1 << 5) /*LIBUSB_REQUEST_TYPE_CLASS*/ | 0x01 /*LIBUSB_RECIPIENT_INTERFACE*/,
                6 /*DFU_ABORT*/,
                0,
                interface_number,
                IntPtr.Zero,
                0,
                5000);
        }
        static void ClearStatus(IntPtr dev, ushort interface_number)
        {
            int ret = NativeMethods.libusb_control_transfer(
               dev,
               0x00 /*LIBUSB_ENDPOINT_OUT*/ | (0x1 << 5) /*LIBUSB_REQUEST_TYPE_CLASS*/ | 0x01 /*LIBUSB_RECIPIENT_INTERFACE*/,
               4 /*DFU_GETSTATUS*/,
               0,
               interface_number,
               IntPtr.Zero,
               0,
               5000);
        }
        public void Dispose()
        {
            NativeMethods.libusb_close(handle);
        }
    }

    public class Context : IDisposable
    {
        IntPtr handle;
        public Context(LogLevel debug_level = LogLevel.None)
        {
            var ret = NativeMethods.libusb_init(ref handle);

            NativeMethods.libusb_set_debug (handle, debug_level);
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
            for (int i = 0; i < ret; i++)
            {
                var device_descriptor = new DeviceDescriptor();
                var ptr = IntPtr.Zero;

                if (NativeMethods.libusb_get_device_descriptor(devices[i], ref device_descriptor) != 0)
                    continue;

                if (device_descriptor.idVendor != idVendor && device_descriptor.idProduct != idProduct)
                    continue;

                for (int j = 0; j < device_descriptor.bNumConfigurations; j++)
                {
                    ret = NativeMethods.libusb_get_config_descriptor(devices[i], (ushort)j, out ptr);

                    if (ret < 0)
                        throw new Exception(string.Format("Error: {0} while trying to get the config descriptor", ret));

                    var config_descriptor = Marshal.PtrToStructure<ConfigDescriptor>(ptr);

                    for (int k = 0; k < config_descriptor.bNumInterfaces; k++)
                    {
                        var p = config_descriptor.interfaces + j * Marshal.SizeOf<@Interface>();

                        if (p == IntPtr.Zero)
                            continue;

                        var @interface = Marshal.PtrToStructure<@Interface>(p);
                        for (int l = 0; l < @interface.num_altsetting; l++)
                        {
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

            while (p + 1 < list_len)
            {
                int len, type;

                len = Marshal.ReadByte(desc_list, p);
                type = Marshal.ReadByte(desc_list, p + 1);

                if (type == desc_type)
                {
                    return Marshal.PtrToStructure<DfuFunctionDescriptor>(desc_list + p);
                }
                p += len;
            }

            return null;
        }

    }
}
