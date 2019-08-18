using System;
using System.Runtime.InteropServices;

namespace WildernessLabs.DfuSharp
{
    struct @Interface
    {
        public IntPtr altsetting;
        public int num_altsetting;

        public InterfaceDescriptor[] Altsetting {
            get {
                var descriptors = new InterfaceDescriptor[num_altsetting];
                for (int i = 0; i < num_altsetting; i++) {
                    descriptors[i] = Marshal.PtrToStructure<InterfaceDescriptor>(altsetting + i * Marshal.SizeOf<InterfaceDescriptor>());
                }

                return descriptors;
            }
        }
    }
}
