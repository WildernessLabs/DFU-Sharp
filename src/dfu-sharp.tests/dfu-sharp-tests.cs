using System;
using System.IO;

using Xunit;
using DfuSharp;

namespace DfuSharp.Tests {
    public class Tests {
        [Fact]
        void NewContextTest () {
            var ctx = new Context();
        }

        [Fact]
        void GetDfuDevicesTest () {
            var ctx = new Context();
            var devices = ctx.GetDfuDevices (0x483, 0xdf11);
        }

        [Fact]
        void ClaimInterfaceTest () {
            var ctx = new Context();
            var devices = ctx.GetDfuDevices (0x483, 0xdf11);

            devices.ForEach(x => x.ClaimInterface ());
        }

        [Fact]
        void SetInterfaceAltSettingTest () {
            var ctx = new Context();
            var devices = ctx.GetDfuDevices (0x483, 0xdf11);

            devices.ForEach(x => {
                    x.ClaimInterface ();
                    x.SetInterfaceAltSetting (0);
            });
        }

        [Fact]
        void DownloadTest () {
            var filename = Path.GetTempFileName();
            var file = File.Open (filename, FileMode.Create);

            try {
                var ctx = new Context();
                var devices = ctx.GetDfuDevices (0x483, 0xdf11);

                devices.ForEach(x => {
                        x.ClaimInterface ();
                        x.SetInterfaceAltSetting (0);
                        x.Clear ();
                        x.Download (file);
                });
            } finally {
                File.Delete (filename);
            }
        }

        [Fact]
        void RoundTripTest () {
            var filename = Path.GetTempFileName();
            var file = File.Open (filename, FileMode.Create);

            try {
                var ctx = new Context();
                var devices = ctx.GetDfuDevices (0x483, 0xdf11);

                devices.ForEach(x => {
                        x.ClaimInterface ();
                        x.SetInterfaceAltSetting (0);
                        x.Clear ();
                        x.Download (file);
                        file.Dispose ();
                        x.Clear ();
                        x.Upload (File.OpenRead (filename));
                });
            } finally {
                File.Delete (filename);
            }
        }
    }
}
