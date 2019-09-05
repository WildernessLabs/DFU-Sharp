#nullable enable

using System;

namespace WildernessLabs.DfuSharp.Cli
{
    public class CliApp : IDisposable
    {
        //public Context DfuContext => DeviceManager.Instance;
        //public AlpenServiceManager Alpen => AlpenServiceManager.Instance;

        protected Context _dfuContext;

        protected Options? _options;

        public CliApp(Options? options)
        {
            _options = options;
            
            _dfuContext = new Context();

           
        }

        public void Run()
        {
            if (_options != null) {
                // -l (List) Command
                if (_options.List) {
                    // STM32F7 devices only (0x483 = ST, 0xdf11 = F7)
                    //var devices = _dfuContext.GetDfuDevices(0x483, 0xdf11);
                    var devices = _dfuContext.GetDfuDevices();

                    if (devices.Count <= 0) {
                        Console.WriteLine("No DFU devices found.");
                    }

                    Console.WriteLine($"Found ({devices.Count}) devices.");

                    foreach (var d in devices) {
                        Console.WriteLine($"Found Device; {d.DeviceDescriptor.Manufacturer} 0x{d.DeviceDescriptor.ProductID.ToString("x")}, serial: {d.Serial}.");
                        ///Console.WriteLine($"Serial: {d.DeviceDescriptor.SerialNumberIndex}");

                        Console.WriteLine($"DFU Function Descriptor: {{");
                        Console.WriteLine($"  DFUVersion: {d.DfuFunctionDescriptor.DFUVersion}");
                        Console.WriteLine($"  Attributes: {d.DfuFunctionDescriptor.Attributes.ToString()}");
                        Console.WriteLine($"  Length: 0x{d.DfuFunctionDescriptor.Length.ToString("x")}");
                        Console.WriteLine($"  TransferSize: 0x{d.DfuFunctionDescriptor.TransferSize.ToString("x")}");
                        Console.WriteLine($"}}");

                        d.Dispose();
                    }

                    _dfuContext.Dispose();
                }
                // -t TEST command.
                // super hacky stuff.
                if (_options.Test) {

                    string serial = "3367337A3036";
                    string kernalFilePath = "/Temp/Nuttx/nuttx.bin";
                    int kernalAddress = 0x08000000;
                    string userFilePath = "/Temp/Nuttx/nuttx_user.bin";
                    int userAddress = 0x08040000;
                    
                    var device = _dfuContext.GetDfuDevice(serial);

                    if (device != null) {
                        Console.WriteLine($"Found device: {serial}.");

                        // this is useless. it just outputs 2048 each time. would need to sum()
                        // for it to be useful.
                        //device.Uploading += (object sender, UploadingEventArgs e) => {
                        //    Console.WriteLine($"{e.BytesUploaded}|");
                        //};

                        // upload the kernal
                        device.ClaimInterface();
                        device.SetInterfaceAltSetting(0);
                        device.Clear();

                        Console.WriteLine($"Uploading {kernalFilePath}.");
                        device.Upload(System.IO.File.OpenRead(kernalFilePath), kernalAddress);
                        Console.WriteLine($"Done.");
                        device.Clear();

                        Console.WriteLine($"Uploading {kernalFilePath}.");
                        device.Upload(System.IO.File.OpenRead(userFilePath), userAddress);
                        Console.WriteLine($"Done.");

                        device.Dispose();
                    } else {
                        Console.WriteLine($"Could not find device: {serial}.");
                    }
                    _dfuContext.Dispose();
                }
            } else {
                Console.WriteLine("No options. Nothing to do.");
            }


        }













        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue) {
                if (disposing) {
                    // TODO: dispose managed state (managed objects).
                    _dfuContext.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~CliApp()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
