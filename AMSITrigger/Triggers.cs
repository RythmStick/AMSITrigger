using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AmsiTrigger
{

    using static Globals;
    public class Triggers
    {
        [DllImport("Amsi.dll", EntryPoint = "AmsiScanBuffer", CallingConvention = CallingConvention.StdCall)]
        private static extern int AmsiScanBuffer(IntPtr amsiContext, byte[] buffer, uint length, string contentName, IntPtr session, out AMSI_RESULT result);

        private enum AMSI_RESULT
        {
            AMSI_RESULT_CLEAN = 0,
            AMSI_RESULT_NOT_DETECTED = 1,
            AMSI_RESULT_DETECTED = 32768
        }

        private static byte[] bigSample;
        private static byte[] chunkSample;
        private static int chunkSize = 1024;

        public static void FindTriggers()
        {
            int triggerStart;
            int triggerEnd;
            AMSI_RESULT result;
 

            if (!protectionEnabled(amsiContext))
            {
                return;
            }


            if (inScript != null)
            {
                bigSample = File.ReadAllBytes(inScript);
            }
            else
            {
                try
                {
                    WebClient client = new WebClient();
                    client.Proxy = WebRequest.GetSystemWebProxy();
                    client.Proxy.Credentials = CredentialCache.DefaultCredentials;
                    bigSample = client.DownloadData(inURL);


                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e.Message);
                    return;
                }

            }


            result = scanBuffer(bigSample, amsiContext);
            if (result != AMSI_RESULT.AMSI_RESULT_DETECTED)
            {
                return;
            }




                while (sampleIndex < bigSample.Length)
            {
                if (sampleIndex + chunkSize > bigSample.Length)
                {
                    chunkSize = bigSample.Length - sampleIndex;
                }
                chunkSample = new byte[chunkSize];
                Array.Copy(bigSample, sampleIndex, chunkSample, 0, chunkSize);
                
                sampleIndex += chunkSize;
                result = scanBuffer(chunkSample, amsiContext);




                if (result != AMSI_RESULT.AMSI_RESULT_DETECTED)  // Chunk is clean
                {
                    showText(chunkSample, 0, chunkSize/2, false);
                    sampleIndex -= chunkSize/2;

                }
                else   // This line contains trigger(s), scrutinize it to find individual triggers
                {
                    
                    triggerEnd = findTriggerEnd(chunkSample);
                    
                    if (triggerEnd > 0)
                    {

                        triggerStart=findTriggerStart(chunkSample, triggerEnd);
                        showText(chunkSample, 0, triggerStart, false);
                        showText(chunkSample, triggerStart, triggerEnd-triggerStart, true);
                        sampleIndex += triggerEnd - chunkSize;
                    }

                }



            }
        }




        private static int findTriggerEnd(byte[] smallSample)
        {
            
            AMSI_RESULT result;
            byte[] tmpSample;

            for (int sampleIndex = 2; sampleIndex < smallSample.Length; sampleIndex++)
            {

                tmpSample = new byte[sampleIndex];
                Array.Copy(chunkSample, 0, tmpSample, 0, sampleIndex);
                string ssstring = Encoding.Default.GetString(tmpSample);
                result = scanBuffer(tmpSample, amsiContext);

                if (result == AMSI_RESULT.AMSI_RESULT_DETECTED)
                {
                    return sampleIndex;
                }
                                             
            }

            return 0;
        }







        private static int findTriggerStart(byte[] smallSample,int triggerEnd)
        {
            AMSI_RESULT result;
            byte[] tmpSample;

            for (int sampleIndex = triggerEnd-1; sampleIndex > 0; sampleIndex--)
            {

                tmpSample = new byte[triggerEnd-sampleIndex];
                Array.Copy(chunkSample, sampleIndex, tmpSample, 0, triggerEnd - sampleIndex);
                string ssstring = Encoding.Default.GetString(tmpSample);
                result = scanBuffer(tmpSample, amsiContext);

                if (result == AMSI_RESULT.AMSI_RESULT_DETECTED)
                {
                    return sampleIndex;
                }

            }

            return 0;
        }




        


        private static void showText(byte[] output, int start, int length, Boolean highLight) 
            {
                
                byte[] tmpSample = new byte[length];
                Array.Copy(output, start, tmpSample, 0, length);
               switch (format)
                    {


                    case 1:
                            if (highLight)
                            {
                                Console.ForegroundColor = System.ConsoleColor.Gray;
                                Console.WriteLine(Encoding.Default.GetString(tmpSample));
                            }
                            break;
                    case 2:
                           if (highLight)
                           {
                                byte[] tmp2Sample = new byte[sampleIndex + start + length - chunkSize];
                                Array.Copy(bigSample, 0, tmp2Sample, 0, sampleIndex - chunkSize + length + start);
                                lineNumber = returnsInSample(tmp2Sample) + 1;
                                Console.ForegroundColor = System.ConsoleColor.Gray;
                                Console.WriteLine("(" + lineNumber + ")\t\"" + Encoding.Default.GetString(tmpSample) + "\"");
                           }
                           break;

                    case 3:
                          if (highLight)
                          { 
                              Console.ForegroundColor = System.ConsoleColor.Red;
                              Console.Write(Encoding.Default.GetString(tmpSample));
                    }
                          else
                          {
                              Console.ForegroundColor = System.ConsoleColor.Gray;
                              Console.Write(Encoding.Default.GetString(tmpSample));
                    }
                    break;
                    
                    case 4:
                        Console.ForegroundColor = System.ConsoleColor.Gray;
                        Console.WriteLine(Encoding.Default.GetString(tmpSample));
                        break;

            }
        }

             
   
        private static int returnsInSample(byte[] sample)
        {
          
            return new Regex(@"\n").Matches(Encoding.Default.GetString(sample)).Count;



        }
        private static AMSI_RESULT scanBuffer(byte[] sample, IntPtr amsiContext)
        {
            AMSI_RESULT result = 0;
            int returnValue;
            IntPtr session = IntPtr.Zero;

            if (format==4)
            {
                showText(sample, 0, sample.Length, false);
            }


            returnValue = AmsiScanBuffer(amsiContext, sample, (uint)sample.Length, "Sample", IntPtr.Zero, out result);
            return result;
        }

        private static Boolean protectionEnabled(IntPtr amsiContext)
        {

            byte[] sample = Encoding.UTF8.GetBytes("AMSIScanBuffer");
            AMSI_RESULT result = Triggers.scanBuffer(sample, amsiContext);

            if (result == AMSI_RESULT.AMSI_RESULT_NOT_DETECTED)
            {
                Console.WriteLine("[+] Check Real Time protection is enabled");
                return false;
            }
            else
            {
                return true;
            }

        }

    }
}

