using System;

namespace WildernessLabs.DfuSharp
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
}
