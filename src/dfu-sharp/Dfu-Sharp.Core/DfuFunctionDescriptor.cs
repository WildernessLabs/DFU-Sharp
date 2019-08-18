using System;
namespace WildernessLabs.DfuSharp
{
    public struct DfuFunctionDescriptor
    {
        public byte Length;
        public byte DescriptorType;
        public byte Attributes;
        public ushort DetachTimeOut;
        public ushort TransferSize;
        public ushort DFUVersion;
    }
}
