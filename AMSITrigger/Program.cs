using System;
using System.Runtime.InteropServices;
using System.IO;
using System.Diagnostics.Eventing.Reader;

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
        public static int pauseOutput = 0;
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
            string allArgs = System.Environment.CommandLine.Substring(System.Environment.CommandLine.IndexOf(" ") + 1);

            foreach (string fullarg in args)
            {

                if (fullarg == "-debug" || fullarg == "-d")
                {
                    debug = true;
                    allArgs = allArgs.Replace(fullarg, "");
                }
                else if (fullarg == "-h" || fullarg == "-help" || fullarg == "-?")
                {
                    showHelp();
                    return false;
                }
                else if (fullarg.IndexOf("=")==-1)
                {
                    Console.WriteLine("[-] Parameter Error:"+allArgs);
                    return false;
                }
                else
                {

                    string param = fullarg.Substring(0, fullarg.IndexOf("="));
                    switch (param)
                    {
                        case ("-i"):
                        case ("-inputfile"):
                            inScript = fullarg.Substring(fullarg.IndexOf("=") + 1);
                            allArgs = allArgs.Replace(fullarg, "");
                            break;

                        case ("-u"):
                        case ("-url"):
                            inURL = fullarg.Substring(fullarg.IndexOf("=") + 1);
                            allArgs = allArgs.Replace(fullarg, "");
                            break;

                        case ("-f"):
                        case ("-format"):
                            format = Int32.Parse((fullarg.Substring(fullarg.IndexOf("=") + 1)));
                            allArgs = allArgs.Replace(fullarg, "");
                            break;

                        case ("-m"):
                        case ("-maxsiglength"):
                            maxSignatureLength = Int32.Parse((fullarg.Substring(fullarg.IndexOf("=") + 1)));
                            allArgs = allArgs.Replace(fullarg, "");
                            break;

                        case ("-p"):
                        case ("-pause"):
                            pauseOutput = Int32.Parse((fullarg.Substring(fullarg.IndexOf("=") + 1)));
                            allArgs = allArgs.Replace(fullarg, "");
                            break;

                        case ("-c"):
                        case ("-chunksize"):
                            chunkSize = Int32.Parse((fullarg.Substring(fullarg.IndexOf("=") + 1)));
                            allArgs = allArgs.Replace(fullarg, "");
                            break;

                        case ("-h"):
                        case ("-help"):
                            showHelp();
                            break;

                        default:
                            showHelp();
                            break;
                    }
                }
            }

                if (inScript!=null && inURL!= null)
                {
                    Console.WriteLine("[-] Supply either -i or -u, not both");
                    return false;
                }
                if (format < 1 || format > 4)
                {
                    Console.WriteLine("[-] Format should be 1-4");
                    return false;
                }
                if (inURL != null && inURL.ToLower().Substring(0, 7) != "http://" && inURL.ToLower().Substring(0, 8) != "https://")
                {
                    Console.WriteLine("[+] Invalid URL - must begin with http:// or https://");
                    return false;
                }

                if (chunkSize < maxSignatureLength)
                {
                    Console.WriteLine("[+] chunksize should always be > maxSignatureLength");
                    return false;
                }


                if (inScript != null && !File.Exists(inScript))
                {
                    Console.WriteLine("[+] File not found");
                    return false;
                }
            
            return true;
        }


        public static void showHelp()
        {

            Console.WriteLine(@"     _    __  __ ____ ___ _____     _");
            Console.WriteLine(@"    / \  |  \/  / ___|_ _|_   _| __(_) __ _  __ _  ___ _ __ ");
            Console.WriteLine(@"   / _ \ | |\/| \___ \| |  | || '__| |/ _` |/ _` |/ _ \ '__|");
            Console.WriteLine(@"  / ___ \| |  | |___) | |  | || |  | | (_| | (_| |  __/ |   ");
            Console.WriteLine(@" /_/   \_\_|  |_|____/___| |_||_|  |_|\__, |\__, |\___|_|   ");
            Console.WriteLine(@"                                      |___/ |___/         v4");
            Console.WriteLine("@_RythmStick\n\n");

           
            Console.WriteLine("Show triggers in Powershell file or URL.\nUsage:");

            Console.WriteLine("-i|-inputfile= : Powershell filename or");
            Console.WriteLine("-u|-url= : URL eg. https://10.1.1.1/Invoke-NinjaCopy.ps1");
            Console.WriteLine("\n-f|-format= : Output Format:" + "\n\t1 - Only show Triggers\n\t2 - Show Triggers with line numbers\n\t3 - Show Triggers inline with code\n\t4 - Show AMSI calls (xmas tree mode)");
            Console.WriteLine("-d|-debug : Show debug info");
            Console.WriteLine("-m|-maxsiglength= : Maximum Signature Length to cater for, default=2048");
            Console.WriteLine("-c|-chunksize= : Chunk size to send to AMSIScanBuffer, default=4096");
            Console.WriteLine("-p|-pause= : Number of triggers which will pause execution");
            Console.WriteLine("-h|-?|-help : Show Help");

            
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


