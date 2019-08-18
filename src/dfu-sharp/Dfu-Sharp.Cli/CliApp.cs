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
                if (_options.List) {
                    var devices = _dfuContext.GetDfuDevices(0x483, 0xdf11);

                    if (devices.Count <= 0) {
                        Console.WriteLine("No DFU devices found.");
                    }

                    foreach (var d in devices) {
                        Console.WriteLine($"Found Device; 0x{d.DeviceDescriptor.idProduct.ToString("x")}.");
                        
                    }
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
