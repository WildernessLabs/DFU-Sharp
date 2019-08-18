using System;
namespace WildernessLabs.DfuSharp
{
    public struct DfuFunctionDescriptor
    {
        public byte bLength;
        public byte bDescriptorType;
        public byte bmAttributes;
        public ushort wDetachTimeOut;
        public ushort wTransferSize;
        public ushort bcdDFUVersion;
    }
}
