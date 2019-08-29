using System;
using CommandLine;

namespace WildernessLabs.DfuSharp.Cli
{
    class Program
    {
        private static CliApp app;


        static void Main(string[] args)
        {
            if (args.Length == 0) {
                args = new string[] { "--help" };
            }
            Options options = null;
            Parser.Default.ParseArguments<Options>(args)
            .WithParsed<Options>(optionsOut => {
                //Console.WriteLine("Hey yo, options are a go");
                //
                options = optionsOut;
            });

            using (app = new CliApp(options)) {
                app.Run();
            }

            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }
    }
}