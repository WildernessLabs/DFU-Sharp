using System;
namespace WildernessLabs.DfuSharp
{
    public delegate void UploadingEventHandler(object sender, UploadingEventArgs e);

    public class UploadingEventArgs : EventArgs
    {
        public int BytesUploaded { get; private set; }

        public UploadingEventArgs(int bytesUploaded)
        {
            this.BytesUploaded = bytesUploaded;
        }
    }
}
