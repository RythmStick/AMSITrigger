using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.IO;
using System.Drawing;
using NDesk.Options;

namespace AmsiTrigger
{
    class Program
    {



        public enum AMSI_RESULT
        {
            AMSI_RESULT_CLEAN = 0,
            AMSI_RESULT_NOT_DETECTED = 1,
            AMSI_RESULT_DETECTED = 32768
        }




        [DllImport("Amsi.dll", EntryPoint = "AmsiInitialize", CallingConvention = CallingConvention.StdCall)]
        public static extern int AmsiInitialize([MarshalAs(UnmanagedType.LPWStr)]string appName, out IntPtr amsiContext);
        [DllImport("Amsi.dll", EntryPoint = "AmsiUninitialize", CallingConvention = CallingConvention.StdCall)]
        public static extern void AmsiUninitialize(IntPtr amsiContext);
        [DllImport("Amsi.dll", EntryPoint = "AmsiScanBuffer", CallingConvention = CallingConvention.StdCall)]
        public static extern int AmsiScanBuffer(IntPtr amsiContext, byte[] buffer, uint length, string contentName, IntPtr session, out AMSI_RESULT result);




        public static AMSI_RESULT scanBuffer(byte[] sample, IntPtr amsiContext)
        {
            AMSI_RESULT result = 0;
            int returnValue;
            IntPtr session = IntPtr.Zero;

            // returnValue = AmsiOpenSession(amsiContext, out session);
            returnValue = AmsiScanBuffer(amsiContext, sample, (uint)sample.Length, "Sample", IntPtr.Zero, out result);
            //  AmsiCloseSession(amsiContext, session);
            return result;
        }

        public static Boolean protectionDisabled(IntPtr amsiContext)
        {

            byte[] sample = Encoding.UTF8.GetBytes("AMSIScanBuffer");
            AMSI_RESULT result = scanBuffer(sample, amsiContext);

            if (result == AMSI_RESULT.AMSI_RESULT_NOT_DETECTED)
            {
                Console.WriteLine("[+] Check Real Time protection is enabled");
                return true;
            }
            else
            {
                return false;
            }

        }


        public static void processFile(string inFile, int format, int maxLength, IntPtr amsiContext)
        {
            byte[] sample;
            AMSI_RESULT result;
            int startIndex;
            int lineNumber=0;
            string line;


            System.IO.StreamReader file = new System.IO.StreamReader(inFile);

            while ((line = file.ReadLine()) != null)
            {

                lineNumber += 1;
                if (line.Length > maxLength)
                {
                    line = line.Substring(0, maxLength);
                }

                sample = Encoding.UTF8.GetBytes(line);

                if (format == 4)
                {
                    Console.WriteLine(Encoding.Default.GetString(sample));
                }
                result = scanBuffer(sample, amsiContext);


                if (result != AMSI_RESULT.AMSI_RESULT_DETECTED)  // Line is clean
                {
                    if (format > 2)
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.WriteLine(Encoding.Default.GetString(sample));
                    }

                }
                else   // This line contains trigger(s), scrutinize it to find individual triggers
                {

                    startIndex = 0;

                    // search for end of trigger
                    for (int sampleLength = 2; sampleLength < line.Length + 1; sampleLength++)
                    {
                        sample = Encoding.UTF8.GetBytes(line.Substring(startIndex, sampleLength));
                        if (format == 4)
                        {
                            Console.WriteLine(Encoding.Default.GetString(sample));
                        }
                        result = scanBuffer(sample, amsiContext);


                        if (result == AMSI_RESULT.AMSI_RESULT_DETECTED)  // We've got where triggger ends - now find where it starts
                        {

                            while (result == AMSI_RESULT.AMSI_RESULT_DETECTED)
                            {
                                startIndex += 1;
                                sample = Encoding.UTF8.GetBytes(line.Substring(startIndex, sampleLength - startIndex));
                                if (format == 4)
                                {
                                    Console.WriteLine(Encoding.Default.GetString(sample));
                                }
                                result = scanBuffer(sample, amsiContext);
                            }



                            // now we have full trigger string

                            if (format == 3)
                            {
                                Console.ForegroundColor = ConsoleColor.White;
                                Console.Write(line.Substring(0, startIndex - 1));
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.Write(line.Substring(startIndex - 1, sampleLength - startIndex + 1));
                            }
                            if (format == 2)
                            {

                                Console.WriteLine("(" + lineNumber + ")\t\"" + line.Substring(startIndex - 1, sampleLength - startIndex + 1) + "\"");
                            }
                            if (format == 1)
                            {
                                Console.WriteLine("\"" + line.Substring(startIndex - 1, sampleLength - startIndex + 1) + "\"");
                            }
                            line = line.Substring(sampleLength);
                            startIndex = 0;
                            sampleLength = 1;
                        }
                    }
                    if (format == 3)
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.WriteLine(line);
                    }
                }
            }
            file.Close();
        }

        public static void showHelp(OptionSet p)
        {

            Console.WriteLine(@"     _    __  __ ____ ___ _____     _");
            Console.WriteLine(@"    / \  |  \/  / ___|_ _|_   _| __(_) __ _  __ _  ___ _ __ ");
            Console.WriteLine(@"   / _ \ | |\/| \___ \| |  | || '__| |/ _` |/ _` |/ _ \ '__|");
            Console.WriteLine(@"  / ___ \| |  | |___) | |  | || |  | | (_| | (_| |  __/ |   ");
            Console.WriteLine(@" /_/   \_\_|  |_|____/___| |_||_|  |_|\__, |\__, |\___|_|   ");
            Console.WriteLine(@"                                      |___/ |___/");
            Console.WriteLine("@_RythmStick\n\n\n");


            Console.WriteLine("Usage:");
            p.WriteOptionDescriptions(Console.Out);
        }




        static void Main(string[] args)
        {
            IntPtr amsiContext;
            int returnValue;
            int maxLength = 2048;
            var help = false;
            string infile = string.Empty;
            int format = 1;
            int max = 0;


            var options = new OptionSet(){
                {"i|inputfile=", "Powershell filename", o => infile = o},
                {"f|format=", "Output Format:"+"\n1 - Only show Triggers\n2 - Show Triggers with line numbers\n3 - Show Triggers inline with code\n4 - Show AMSI calls (xmas tree mode)", (int o) => format = o},
                {"m|max=", "Maximum Line Length (default 2048)", (int o) => max = o},
                {"h|?|help","Show Help", o => help = true},
            };

            try
            {
                options.Parse(args);

                if (help || args.Length == 0)
                {
                    showHelp(options);
                    return;
                }

                if (format < 1 || format > 4)
                {
                    showHelp(options);
                    return;
                }

            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
                showHelp(options);
                return;
            }


            if (!File.Exists(infile))
            {
                Console.WriteLine("[+] File not found");
                return;
            }


            returnValue = AmsiInitialize(@"PowerShell_C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe_10.0.18362.1", out amsiContext);



            if (protectionDisabled(amsiContext))
            {
                return;
            }



            if (max > 0)
            {
                maxLength = max;
            }

            
            processFile(infile, format, maxLength, amsiContext); 

            AmsiUninitialize(amsiContext);
 
            
        }
    }
}


