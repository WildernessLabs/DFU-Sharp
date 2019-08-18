using System;
namespace WildernessLabs.DfuSharp
{
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
        public byte SerialNumber;
        public byte NumConfigurations;
    }
}
