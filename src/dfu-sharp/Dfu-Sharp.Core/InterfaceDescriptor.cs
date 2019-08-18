using System;
namespace WildernessLabs.DfuSharp
{
    public struct InterfaceDescriptor
    {
        public byte Length;
        public byte DescriptorType;
        public byte InterfaceNumber;
        public byte AlternateSetting;
        public byte NumEndpoints;
        public byte InterfaceClass;
        public byte InterfaceSubClass;
        public byte InterfaceProtocol;
        public byte @Interface;
        public IntPtr Endpoint;
        public IntPtr Extra;
        public int Extra_length;
    }
}
