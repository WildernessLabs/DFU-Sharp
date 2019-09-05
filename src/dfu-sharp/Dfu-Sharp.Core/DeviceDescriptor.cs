using System;
using System.Runtime.InteropServices;

namespace WildernessLabs.DfuSharp
{
    [StructLayoutAttribute(LayoutKind.Sequential, Pack = NativeMethods.Pack)]
    public struct DeviceDescriptor
    {
        public byte Length;
        public byte DescriptorType;
        public ushort bcdUSB;
        public byte DeviceClass;
        public byte DeviceSubClass;
        public byte DeviceProtocol;
        public byte MaxPacketSize0;
        public ushort VendorID;
        public ushort ProductID;
        public ushort bcdDevice;
        public byte Manufacturer;
        public byte Product;
        public byte SerialNumberIndex;
        public byte NumConfigurations;
    }
}
