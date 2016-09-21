﻿using System;
using System.Runtime.InteropServices;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpDX.XInput;
using CoolFontUdp;
using CoolFontIO;
using CoolFontUtils;

namespace CoolFontWin
{
    class Program
    {
        [DllImport("Kernel32")]
        public static extern bool SetConsoleCtrlHandler(HandlerRoutine Handler, bool Add);

        // A delegate type to be used as the handler routine 
        // for SetConsoleCtrlHandler.
        public delegate bool HandlerRoutine(CtrlTypes CtrlType);

        // An enumerated type for the control messages
        // sent to the handler routine.
        public enum CtrlTypes
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT,
            CTRL_CLOSE_EVENT,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT
        }


        static void Main(string[] args)
        {
            SetConsoleCtrlHandler(new HandlerRoutine(ConsoleCtrlCheck), true);
            int tryport = ProcessArgs(args);

            if (tryport == 0)
            {
                tryport = FileManager.ReadPortFromFile(Config.PORT_FILE);
            }

            if (tryport == 0)
            {
                Console.WriteLine("No port given or found: Will select random port.");
            }

            /* Instantiate listener using port */
            UdpListener listener = new UdpListener(tryport);
            int port = listener.port;      

            if (port > 0 & listener.isBound)
            {
                // write successful port to file for next time
                FileManager.WritePortToFile(port, Config.PORT_FILE);
            }

            /* Register DNS service through Java */
            JavaProc.StartDnsService(port); // blocks


            State state;
            /* Set up the simulator */
            XInputDevice dev = new XInputDevice(); // whhat happens if no controller is plugged in?
            Config.Mode = Config.MovementModes.Mouse2D; // try this first 
            CoolFontSimulator sim = new CoolFontSimulator(Config.Mode); // will change Mode if necessary

            string rcvd;
            int[] vals = { 0, 0, 0, 0 };
            int[] vals_last = vals;
            int buttons = 0; // bitmask
            int modeIn = (int)Config.Mode;
            int T = 0; // total time
            int timeout = 30; // set to -1 to block on every socket read
            int tries = timeout + 1;

            sim.logOutput = false;
            bool logRcvd = false;


            //TODO: execute loop in background thread and allow user to break out
            while (true)
            {
                // do not block, returns "" if nothing to rcv
                //rcvd = listener.receiveStringAsync();
                rcvd = listener.pollSocket(Config.socketPollInterval);


                try
                {
                    vals = listener.parseString2Ints(rcvd);
                    buttons = listener.parseButtons(rcvd);
                    modeIn = listener.parseMode(rcvd, (int)Config.Mode); // Config.Mode is a fallback
                    vals = Algorithm.LowPassFilter(vals,vals_last,Config.RCFilterStrength,Config.dt);
                    vals_last = vals;
                    tries = 0;

                    if (logRcvd && (T % 20 == 0))
                        Console.WriteLine("{0} \n {1}{2}{3} \n {4}",modeIn, vals[0], vals[1], vals[2] , buttons);
                }
                catch
                {
                     // assume empty packet
                    if (tries <= timeout)
                    {
                        vals = vals_last;
                    }
                    tries++; // number of empty packets
                }

                /* Get input from connected XInput device */


                sim.UpdateMode(modeIn);
                sim.AddValues(vals);
                sim.AddButtons(buttons);

                if (dev.controller != null && dev.controller.IsConnected)
                {
                    state = dev.controller.GetState();
                    sim.AddControllerState(state);
                }

                sim.FeedVJoy();
                sim.ResetValues();
                if (sim.logOutput)
                {
                    Console.Write(" ({0}) \n", tries);
                }
                T++;

            }
        }

        private static bool ConsoleCtrlCheck(CtrlTypes ctrlType)
        {
            if (ctrlType == CtrlTypes.CTRL_CLOSE_EVENT)
            {
                JavaProc.Kill();
            }
            return true;
        }

        static int ProcessArgs(string[] args)
        {
            if (args.Length > 0) // no port given
            {
                return Convert.ToInt32(args[0]);
            }
            return 0;
        }
    }
}
