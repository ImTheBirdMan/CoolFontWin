﻿#define ROBUST
//#define EFFICIENT

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CoolFontUdp;
using vJoyInterfaceWrap;

namespace CoolFontWin
{
    class Program
    {
        /**<summary>
         * Main program
         * </summary>*/

        /* vJoy globals */
        // Declaring one joystick (Device id 1) and a position structure. 
        static public vJoy joystick;
        static public vJoy.JoystickState iReport;
        static public uint id = 1;

        /* other globals */
        static string PORT_FILE = "../../../../last-port.txt";

        static void Main(string[] args)
        {
            int tryport;
            if (args.Length == 0)
            {
                try
                {
                    System.IO.StreamReader file = new System.IO.StreamReader(PORT_FILE);
                    string hdr = file.ReadLine();
                    tryport = Convert.ToInt32(file.ReadLine());
                    file.Close();
                }
                catch (Exception e)
                {
                    tryport = 0; // UdpListener will decide the port
                }
            }
            else // port was passed as an arg
            {
                tryport = Convert.ToInt32(args[0]);
            } 
        
            /* Instantiate listener using port */

            UdpListener listener = new UdpListener(tryport);
            int port = listener.port;

            if (port > 0 & listener.isBound) // write successful port to file
            {
                System.IO.StreamWriter file = new System.IO.StreamWriter(PORT_FILE);
                string hdr = "Last successful port:";
                string port_string = String.Format("{0}", port);
                file.WriteLine(hdr);
                file.WriteLine(port_string);
                file.Close();
            }

            Process myProcess = new Process();
            int exitCode = 0;
            try
            {
                myProcess.StartInfo.UseShellExecute = false;
                myProcess.StartInfo.RedirectStandardError = true;
                myProcess.StartInfo.RedirectStandardOutput = true;
                myProcess.StartInfo.CreateNoWindow = true;

                myProcess.StartInfo.FileName = "java.exe"; // for some reason VS calls java 7
                String jarfile = "../../../../testapp-java.jar";
                String arg0 = String.Format("{0}", port); // -r: register, -u: unregister, -b: both (not useful?)
                String arg1 = "-r";
                myProcess.StartInfo.Arguments = String.Format("-jar {0} {1} {2}", jarfile, arg0, arg1);
                
                myProcess.Start();
                // This code assumes the process you are starting will terminate itself. 
                // Given that is is started without a window so you cannot terminate it 
                // on the desktop, it must terminate itself or you can do it programmatically
                // from this application using the Kill method.

                string stdoutx = myProcess.StandardOutput.ReadToEnd();
                string stderrx = myProcess.StandardError.ReadToEnd();
                myProcess.WaitForExit();
                exitCode = myProcess.ExitCode;
                Console.WriteLine("Exit code : {0}", exitCode);
                Console.WriteLine("Stdout : {0}", stdoutx);
                Console.WriteLine("Stderr : {0}", stderrx);
            }
            catch (System.ComponentModel.Win32Exception w)
            {
                Console.WriteLine(w.Message);
                Console.WriteLine(w.ErrorCode.ToString());
                Console.WriteLine(w.NativeErrorCode.ToString());
                Console.WriteLine(w.StackTrace);
                Console.WriteLine(w.Source);
                Exception e = w.GetBaseException();
                Console.WriteLine(e.Message);
            }

            if (exitCode == 1)
            {
                Console.WriteLine("DNS service failed to register. Check location of testapp-java.jar");
                Console.WriteLine("Press any key to quit");
                Console.ReadKey();
                return;
            }
            Console.WriteLine("Called java program");

            /* Now set up vJoy device */
            UInt32 id = 1;

            setUpVJoy(id);

            int X, Y, rX, rY, POV;
            uint count = 0;
            long maxX = 0;
            long maxY = 0;
            long maxRX = 0;
            long maxRY = 0;
            long maxPOV = 0;

            double POV_f;
            double X_f;

            string rcvd;
            char[] delimiters = { ':' };
            int[] vals;

#if ROBUST
            bool res;
            // Reset this device to default values
            joystick.ResetVJD(id);
#endif

            joystick.GetVJDAxisMax(id, HID_USAGES.HID_USAGE_X, ref maxX);
            joystick.GetVJDAxisMax(id, HID_USAGES.HID_USAGE_Y, ref maxY);
            joystick.GetVJDAxisMax(id, HID_USAGES.HID_USAGE_RX, ref maxRX);
            joystick.GetVJDAxisMax(id, HID_USAGES.HID_USAGE_RY, ref maxRY);
            joystick.GetVJDAxisMax(id, HID_USAGES.HID_USAGE_POV, ref maxPOV);

            int ContPovNumber = joystick.GetVJDContPovNumber(id);

            int t = 0; // number of packets to receive
            while (true)
            {
      
                /* Receive one string synchronously */
                rcvd = listener.receiveStringSync();
                /* Parse to int[] */
                vals = listener.parseString2Ints(rcvd, delimiters);

                /*update joystick*/
                Y = -vals[0]*(int)maxY/1000/2 + (int)maxY/2;
                X_f = Math.Cos(vals[1] / 1000.0 * Math.PI / 180.0);
                X = (int)(X_f*maxX/2 + maxX/2);
                rX = (int)maxRX/2;
                rY = (int)maxRX/2;
                POV_f = vals[1] / 1000.0 / 360.0 * maxPOV;
                POV = (int)POV_f;
                res = joystick.SetAxis(X, id, HID_USAGES.HID_USAGE_X);
                res = joystick.SetAxis(Y, id, HID_USAGES.HID_USAGE_Y);
                res = joystick.SetAxis(rX, id, HID_USAGES.HID_USAGE_RX);
                res = joystick.SetAxis(rY, id, HID_USAGES.HID_USAGE_RY);

                if (ContPovNumber > 0)
                {
                    res = joystick.SetContPov(POV, id, 1);
                }

                    /* Display X,Y,t values  */
                    Console.WriteLine("X {0} Y {1} t {2}", X, Y, t);
                t++;
            }
            listener.Close();

        }

        static void setUpVJoy(UInt32 id)
        {
            // Create one joystick object and a position structure.
            joystick = new vJoy();
            iReport = new vJoy.JoystickState();

            if (id <= 0 || id > 16)
            {
                Console.WriteLine("Illegal device ID {0}\nExit!", id);
                return;
            }

            // Get the driver attributes (Vendor ID, Product ID, Version Number)
            if (!joystick.vJoyEnabled())
            {
                Console.WriteLine("vJoy driver not enabled: Failed Getting vJoy attributes.\n");
                return;
            }
            else
                Console.WriteLine("Vendor: {0}\nProduct :{1}\nVersion Number:{2}\n", joystick.GetvJoyManufacturerString(), joystick.GetvJoyProductString(), joystick.GetvJoySerialNumberString());

            // Get the state of the requested device
            VjdStat status = joystick.GetVJDStatus(id);
            switch (status)
            {
                case VjdStat.VJD_STAT_OWN:
                    Console.WriteLine("vJoy Device {0} is already owned by this feeder\n", id);
                    break;
                case VjdStat.VJD_STAT_FREE:
                    Console.WriteLine("vJoy Device {0} is free\n", id);
                    break;
                case VjdStat.VJD_STAT_BUSY:
                    Console.WriteLine("vJoy Device {0} is already owned by another feeder\nCannot continue\n", id);
                    return;
                case VjdStat.VJD_STAT_MISS:
                    Console.WriteLine("vJoy Device {0} is not installed or disabled\nCannot continue\n", id);
                    return;
                default:
                    Console.WriteLine("vJoy Device {0} general error\nCannot continue\n", id);
                    return;
            };

            // Check which axes are supported
            bool AxisX = joystick.GetVJDAxisExist(id, HID_USAGES.HID_USAGE_X);
            bool AxisY = joystick.GetVJDAxisExist(id, HID_USAGES.HID_USAGE_Y);
            bool AxisZ = joystick.GetVJDAxisExist(id, HID_USAGES.HID_USAGE_Z);
            bool AxisRX = joystick.GetVJDAxisExist(id, HID_USAGES.HID_USAGE_RX);
            bool AxisRZ = joystick.GetVJDAxisExist(id, HID_USAGES.HID_USAGE_RZ);
            // Get the number of buttons and POV Hat switchessupported by this vJoy device
            int nButtons = joystick.GetVJDButtonNumber(id);
            int ContPovNumber = joystick.GetVJDContPovNumber(id);
            int DiscPovNumber = joystick.GetVJDDiscPovNumber(id);

            // Print results
            Console.WriteLine("\nvJoy Device {0} capabilities:\n", id);
            Console.WriteLine("Numner of buttons\t\t{0}\n", nButtons);
            Console.WriteLine("Numner of Continuous POVs\t{0}\n", ContPovNumber);
            Console.WriteLine("Numner of Descrete POVs\t\t{0}\n", DiscPovNumber);
            Console.WriteLine("Axis X\t\t{0}\n", AxisX ? "Yes" : "No");
            Console.WriteLine("Axis Y\t\t{0}\n", AxisX ? "Yes" : "No");
            Console.WriteLine("Axis Z\t\t{0}\n", AxisX ? "Yes" : "No");
            Console.WriteLine("Axis Rx\t\t{0}\n", AxisRX ? "Yes" : "No");
            Console.WriteLine("Axis Rz\t\t{0}\n", AxisRZ ? "Yes" : "No");

            // Test if DLL matches the driver
            UInt32 DllVer = 0, DrvVer = 0;
            bool match = joystick.DriverMatch(ref DllVer, ref DrvVer);
            if (match)
                Console.WriteLine("Version of Driver Matches DLL Version ({0:X})\n", DllVer);
            else
                Console.WriteLine("Version of Driver ({0:X}) does NOT match DLL Version ({1:X})\n", DrvVer, DllVer);


            // Acquire the target
            if ((status == VjdStat.VJD_STAT_OWN) || ((status == VjdStat.VJD_STAT_FREE) && (!joystick.AcquireVJD(id))))
            {
                Console.WriteLine("Failed to acquire vJoy device number {0}.\n", id);
                return;
            }
            else
                Console.WriteLine("Acquired: vJoy device number {0}.\n", id);

        }

    }

}
