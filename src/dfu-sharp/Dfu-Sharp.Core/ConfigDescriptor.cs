using System;
namespace WildernessLabs.DfuSharp
{
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
}
