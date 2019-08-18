using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace WildernessLabs.DfuSharp
{
    public class DfuDevice : IDisposable
    {
        // TODO:
        // FIXME: Figure out why dfu_function_descriptor.wTransferSize isn't right and why STM isn't reporting flash_size right
        const int flash_size = 0x200000;
        const int transfer_size = 0x800;
        const int address = 0x08000000;

        IntPtr handle;

        public DeviceDescriptor DeviceDescriptor {
            get => _deviceDescriptor;
            protected set => _deviceDescriptor = value;
        } protected DeviceDescriptor _deviceDescriptor;

        public InterfaceDescriptor InterfaceDescriptor {
            get => interface_descriptor;
            set => interface_descriptor = value;
        }
        InterfaceDescriptor interface_descriptor;

        public DfuFunctionDescriptor DfuFunctionDescriptor {
            get => dfu_descriptor;
            set => dfu_descriptor = value;
        }
        DfuFunctionDescriptor dfu_descriptor;


        public DfuDevice(
            IntPtr device,
            DeviceDescriptor descriptor,
            InterfaceDescriptor interface_descriptor,
            DfuFunctionDescriptor dfu_descriptor)
        {
            this._deviceDescriptor = descriptor;
            this.interface_descriptor = interface_descriptor;
            this.dfu_descriptor = dfu_descriptor;
            if (NativeMethods.libusb_open(device, ref handle) < 0)
                throw new Exception("Error opening device");
        }

        public event UploadingEventHandler Uploading;

        protected virtual void OnUploading(UploadingEventArgs e)
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

            while (state != 0 && state != 2) {
                state = GetStatus(handle, interface_descriptor.bInterfaceNumber);

                switch (state) {
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

        public void Upload(FileStream file, int? baseAddress = null)
        {
            var buffer = new byte[transfer_size];

            using (var reader = new BinaryReader(file)) {
                for (var pos = 0; pos < flash_size; pos += transfer_size) {
                    int write_address = (baseAddress ?? address) + pos;
                    var count = reader.Read(buffer, 0, transfer_size);

                    if (count == 0)
                        return;

                    Upload(buffer, write_address);
                }
            }
        }

        public void Upload(byte[] data, int? baseAddress = null)
        {
            var mem = Marshal.AllocHGlobal(transfer_size);

            try {
                for (var pos = 0; pos < flash_size; pos += transfer_size) {
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

                    while (status == 4) {
                        Thread.Sleep(100);
                        status = GetStatus(handle, interface_descriptor.bInterfaceNumber);
                    }
                    OnUploading(new UploadingEventArgs(count));
                }
            } finally {
                Marshal.FreeHGlobal(mem);
            }
        }

        public void Download(FileStream file)
        {
            var buffer = new byte[transfer_size];
            var mem = Marshal.AllocHGlobal(transfer_size);

            try {
                int count = 0;
                ushort transaction = 2;
                using (var writer = new BinaryWriter(file)) {
                    while (count < flash_size) {
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
            } finally {
                Marshal.FreeHGlobal(mem);
            }
        }

        void EraseSector(int address)
        {
            var mem = Marshal.AllocHGlobal(5);

            try {
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

                while (status == 4) {
                    Thread.Sleep(100);
                    status = GetStatus(handle, interface_descriptor.bInterfaceNumber);
                }
            } finally {
                Marshal.FreeHGlobal(mem);
            }
        }

        void SetAddress(int address)
        {
            var mem = Marshal.AllocHGlobal(5);

            try {
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

                while (status == 4) {
                    Thread.Sleep(100);
                    status = GetStatus(handle, interface_descriptor.bInterfaceNumber);
                }
            } finally {
                Marshal.FreeHGlobal(mem);
            }
        }

        static byte GetStatus(IntPtr dev, ushort interface_number)
        {
            var buffer = Marshal.AllocHGlobal(6);

            try {
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
            } finally {
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
}
