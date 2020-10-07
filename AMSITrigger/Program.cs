using System;
using System.Runtime.InteropServices;
using System.IO;
using NDesk.Options;

namespace AmsiTrigger
{

    using static Globals;

    public static class Globals
    {
        public static int minSignatureLength = 6;       // Playing with these can result in quicker execution time and less AMSIScanBuffer calls. It can also reduce the accuracy of trigger identification.
        public static int maxSignatureLength = 2048;    // Setting maxSignatureLength will ensure that signatures split over data chunks dont get missed as only the first (chunkSize - maxSignatureLength) will be reported as clean
        public static int format = 1;
        public static int chunkSize = 4096;
        public static int max = 0;
        public static Boolean help = false;
        public static Boolean debug = false;
        public static string inScript;
        public static IntPtr amsiContext;
        public static string inURL;
        public static int lineNumber = 1;
        public static int sampleIndex = 0; 
        public static int amsiCalls = 0;
        public static int chunksProcessed = 0;
        public static int triggersFound = 0;
    }




    class Program
    {
                          
        [DllImport("Amsi.dll", EntryPoint = "AmsiInitialize", CallingConvention = CallingConvention.StdCall)]
        public static extern int AmsiInitialize([MarshalAs(UnmanagedType.LPWStr)]string appName, out IntPtr amsiContext);
        [DllImport("Amsi.dll", EntryPoint = "AmsiUninitialize", CallingConvention = CallingConvention.StdCall)]
        public static extern void AmsiUninitialize(IntPtr amsiContext);
  
                            


        static void Main(string[] args)
        {
            
            string infile = string.Empty;


            var watch = System.Diagnostics.Stopwatch.StartNew();


            if (!validParameters(args))
            {
                return;
            }

            if (!AmsiInitialize())
            {
                return;
            }

            Triggers.FindTriggers();

            AmsiUninitialize(amsiContext);

            watch.Stop();

            if (debug)
            {
                Console.ForegroundColor = System.ConsoleColor.Gray;
                Console.WriteLine($"\n\r\n\rChunks Processed: {chunksProcessed}");
                Console.WriteLine($"Triggers Found: {triggersFound}");
                Console.WriteLine($"AmsiScanBuffer Calls: {amsiCalls}");
                Console.WriteLine($"Total Execution Time: {watch.Elapsed.TotalSeconds} s");
            }

        }



        public static Boolean validParameters(string[] args)
        {


            var options = new OptionSet(){
                {"i|inputfile=", "Powershell filename or", o => inScript = o},
                {"u|url=", "URL eg. https://10.1.1.1/Invoke-NinjaCopy.ps1", o => inURL = o},
                {"f|format=", "Output Format:"+"\n1 - Only show Triggers\n2 - Show Triggers with line numbers\n3 - Show Triggers inline with code\n4 - Show AMSI calls (xmas tree mode)", (int o) => format = o},
                {"d|debug","Show debug info", o => debug = true},
                {"m|maxsiglength=","Maximum Signature Length to cater for, default=2048", (int o) => maxSignatureLength = o},
                {"c|chunksize=","Chunk size to send to AMSIScanBuffer, default=4096", (int o) => chunkSize = o},
                {"h|?|help","Show Help", o => help = true},
            };

            try
            {
                options.Parse(args);

                if (help || args.Length == 0)
                {
                    showHelp(options);
                    return false;
                }

                if (format < 1 || format > 4)
                {
                    showHelp(options);
                    return false;
                }

            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
                showHelp(options);
                return false;
            }


            if (inScript!=null && inURL!=null)
            {
                Console.WriteLine("[+] Supply either -i or -u, not both");
                return false;
            }

            if (inURL!=null && inURL.ToLower().Substring(0,7)!="http://" && inURL.ToLower().Substring(0, 8) != "https://") 
            {
                Console.WriteLine("[+] Invalid URL - must begin with http:// or https://");
                return false;
            }

            if (chunkSize < maxSignatureLength)
            {
                Console.WriteLine("[+] chunksize should always be > maxSignatureLength");
                return false;
            }


            if (inScript!=null && !File.Exists(inScript))
            {
                Console.WriteLine("[+] File not found");
                return false;
            }
            return true;
        }


        public static void showHelp(OptionSet p)
        {

            Console.WriteLine(@"     _    __  __ ____ ___ _____     _");
            Console.WriteLine(@"    / \  |  \/  / ___|_ _|_   _| __(_) __ _  __ _  ___ _ __ ");
            Console.WriteLine(@"   / _ \ | |\/| \___ \| |  | || '__| |/ _` |/ _` |/ _ \ '__|");
            Console.WriteLine(@"  / ___ \| |  | |___) | |  | || |  | | (_| | (_| |  __/ |   ");
            Console.WriteLine(@" /_/   \_\_|  |_|____/___| |_||_|  |_|\__, |\__, |\___|_|   ");
            Console.WriteLine(@"                                      |___/ |___/         v3");
            Console.WriteLine("@_RythmStick\n\n\n");

           
            Console.WriteLine("Show triggers in Powershell file or URL.\nUsage:");
            p.WriteOptionDescriptions(Console.Out);
        }


        public static Boolean AmsiInitialize()
        {
            
            int returnValue = AmsiInitialize(@"PowerShell_C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe_10.0.18362.1", out amsiContext);
            if (returnValue==0)
            {
                return true;
            }
            return false;
        }
   
    }
}


