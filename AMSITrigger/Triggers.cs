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
        private static int chunkSize = 4096;
        private static int triggerStart = 0;
        private static int triggerEnd;
        private static int startIndex = 0;
        public static void FindTriggers()
        {
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
                Console.WriteLine(string.Format("[+] {0}", result));
                return;
            }




            while (startIndex + chunkSize < bigSample.Length)
            {
                chunkSample = new byte[chunkSize];
                Array.Copy(bigSample, startIndex, chunkSample, 0, chunkSize);
                processChunk(chunkSample);
            }

 
            while (startIndex < bigSample.Length)
            {
                chunkSample = new byte[bigSample.Length - startIndex];
                Array.Copy(bigSample, startIndex, chunkSample, 0, chunkSample.Length);
                processChunk(chunkSample);
            }
        }




        private static void processChunk(byte[] chunkSample )
        {
            AMSI_RESULT result;


            result = scanBuffer(chunkSample, amsiContext);


            if (result != AMSI_RESULT.AMSI_RESULT_DETECTED)  
            {
                if (chunkSample.Length > maxSignatureLength)
                {
                    showText(chunkSample, 0, chunkSize - maxSignatureLength, false);
                     startIndex += chunkSize - maxSignatureLength;
                } else
                {
                    showText(chunkSample, 0, chunkSample.Length, false); 
                    startIndex+=chunkSample.Length;
                }

                return;
            }
            triggerEnd = findTriggerEnd(chunkSample) + 1;
            triggerStart = findTriggerStart(chunkSample, triggerEnd);
            
            showText(chunkSample, 0, triggerStart, false);
            showText(chunkSample, triggerStart, triggerEnd-triggerStart, true);
            startIndex += triggerEnd;
            return;
                       
        }

            private static int findTriggerEnd(byte[] smallSample)
        {

            AMSI_RESULT result;
            byte[] tmpSample;
            int lastBytes;

            for (int sampleIndex = 2; sampleIndex < smallSample.Length + minSignatureLength; sampleIndex += minSignatureLength)
            {
                if (sampleIndex> smallSample.Length) {
                    sampleIndex = smallSample.Length;
                }
                tmpSample = new byte[sampleIndex];
                Array.Copy(chunkSample, 0, tmpSample, 0, sampleIndex);
                string ssstring = Encoding.Default.GetString(tmpSample);
                result = scanBuffer(tmpSample, amsiContext);




                if (result == AMSI_RESULT.AMSI_RESULT_DETECTED)
                {

                    for (lastBytes = 0; lastBytes < minSignatureLength; lastBytes++)
                    {

                        tmpSample = new byte[sampleIndex - lastBytes];
                        Array.Copy(chunkSample, 0, tmpSample, 0, sampleIndex - lastBytes);
                        ssstring = Encoding.Default.GetString(tmpSample);
                        result = scanBuffer(tmpSample, amsiContext);
                        if (result != AMSI_RESULT.AMSI_RESULT_DETECTED)
                        {
                            return sampleIndex - lastBytes;
                        }
                    }
                    return sampleIndex - lastBytes;
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
                                Console.WriteLine("[+] \"" + Encoding.Default.GetString(tmpSample) + "\"");
                    }
                            break;
                    case 2:
                           if (highLight)
                           {
                                Console.ForegroundColor = System.ConsoleColor.Gray;
                                Console.WriteLine("[" + lineNumber + "]\t\"" + Encoding.Default.GetString(tmpSample) + "\"");
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

            if (format == 2) { lineNumber += returnsInSample(tmpSample, length);}

        }



        private static int returnsInSample(byte[] sample,int numBytes)
        {
            
            return new Regex(@"\n").Matches(Encoding.Default.GetString(sample).Substring(0,numBytes)).Count;



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
            amsiCalls++;

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

