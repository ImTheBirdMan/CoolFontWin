﻿#define EFFICIENT
//#define ROBUST

using System;
using System.Diagnostics;

using WindowsInput;
using vJoyInterfaceWrap;
using SharpDX.XInput;

namespace CoolFontWin
{
    class CoolFontSimulator
    {
        private long maxX = 0;
        private long maxY = 0;
        private long maxRX = 0;
        private long maxRY = 0;
        private long maxPOV = 0;
        private long maxZ = 0;
        private long maxRZ = 0;

        private vJoy joystick;
        private vJoy.JoystickState iReport;
        private int ContPovNumber;

        private InputSimulator kbm;

        private int X;
        private int Y;
        private int rX;
        private int rY;
        private int lT;
        private int rT;
        private int Z;
        private int rZ;
        private int buttons;

        private int POV = 0;
        private int d_theta = 0;
        private double POV_f, d_theta_f;
        private bool res;
        private byte[] pov;

        private bool _leftMouseButtonDown = false;
        private bool _rightMouseButtonDown = false;

        public bool logOutput = false;
        public int[] neutralVals { get; set; }


        public CoolFontSimulator(Config.MovementModes MODE)
        {
            ConfigureVJoy(Config.ID);
            StartVJoy(Config.ID);
            SetUpVJoy(Config.ID);
            kbm = new InputSimulator();
            ResetValues();
        }

        public void ResetValues()
        {
            X = (int)maxX / 2;
            Y = (int)maxY / 2;
            rX = (int)maxRX / 2;
            rY = (int)maxRY / 2;
            Z = 0;
            rZ = 0;
            lT = 0;
            rT = 0;
            buttons = 0;
        }

        public void AddValues(int[] vals)
        {
            // Receive data from iPhone, parse it, and translate it to the correct inputs
            /* vals[0]: 0-1000: represents user running at 0 to 100% speed.
             * vals[1]: 0-360,000: represents the direction user is facing (in degrees)
             * vals[2]: always 0
             * vals[3]: -infinity to infinity: user rotation rate in radians per second (x1000)
             */

            switch (Config.Mode)
            {
                //TODO: Hold CTRL+W when under running threshold but over walking threshold
                case Config.MovementModes.KeyboardMouse:
                    kbm.Mouse.MoveMouseBy((int)(10.0 * vals[3] / 1000.0), 0); // dx, dy (pixels)

                    if (vals[0] >= Config.THRESH_RUN)
                    {
                        kbm.Keyboard.KeyDown(WindowsInput.Native.VirtualKeyCode.VK_W);
                    }
                    else
                    {
                        kbm.Keyboard.KeyUp(WindowsInput.Native.VirtualKeyCode.VK_W);
                    }

                    if (false) //TODO: Implement jumping on iPhone
                    {
                        kbm.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.SPACE);
                    }

                    if (logOutput)
                    {
                        Console.Write("W?: {0} Mouse: {1}", 
                            kbm.InputDeviceState.IsKeyDown(WindowsInput.Native.VirtualKeyCode.VK_W), 
                            (int)(10.0 * vals[3] / 1000.0));
                    }
                    break;

                case Config.MovementModes.JoystickMove:

                    POV_f = vals[1] / 1000.0;

                    while (POV_f > 360)
                    {
                        POV_f -= 360;
                    }
                    while (POV_f < 0)
                    {
                        POV_f += 360;
                    }


                    /* no strafing */
                    X += 0; 
                    Y += -vals[0] * (int)maxY / 1000 / 2;
                    rX += 0;
                    rY += 0;

                    if (logOutput)
                    {
                        Console.Write("X:{0} Y:{1} dir:{2}", X, Y, (int)POV_f);
                    }
                    break;

                case Config.MovementModes.JoystickStrafe:

                    POV_f = vals[1] / 1000.0;

                    while (POV_f > 360)
                    {
                        POV_f -= 360;
                    }
                    while (POV_f < 0)
                    {
                        POV_f += 360;
                    }

                    POV_f = POV_f / 360;

                    /* strafing */
                    double X_f = -Math.Sin(POV_f * Math.PI * 2) * vals[0] * (int)maxX / 1000 / 2;
                    double Y_f = -Math.Cos(POV_f * Math.PI * 2) * vals[0] * (int)maxY / 1000 / 2;

                    X += (int)X_f;
                    Y += (int)Y_f;

                    rX += 0;
                    rY += 0;
                    
                    if (logOutput)
                    {
                        Console.WriteLine("X:{0} Y:{1} dir:{2}", X, Y, POV_f);
                    }
                    break;

                case Config.MovementModes.JoystickMoveAndLook:
                    // NOT FINISHED YET
                    POV_f = vals[1] / 1000.0;

                    while (POV_f > 360)
                    {
                        POV_f -= 360;
                    }
                    while (POV_f < 0)
                    {
                        POV_f += 360;
                    }

                    POV_f *= maxPOV / 360;

                    X += 0; // no strafing
                    Y += -vals[0] * (int)maxY / 1000 / 2;
            
                    rX += 0; // needs to change
                    rY += 0; // look up/down

                    kbm.Mouse.MoveMouseBy(-(int)(1 * vals[3] / 1000.0 * Config.mouseSens), // negative because device is not assumed upside down
                                         0); // dx, dy (pixels)

                    if (logOutput)
                    {
                        Console.Write("Y:{0} dir:{1}", Y, (int)POV_f);
                    }
                    break;

                case Config.MovementModes.Mouse2D:

                    kbm.Mouse.MoveMouseBy(-(int)(1 * vals[3] / 1000.0 * Config.mouseSens), // negative because device is not assumed upside down
                                          -(int)(2 * vals[2] / 1000.0 * Config.mouseSens)); // dx, dy (pixels)
                    if (logOutput)
                    {
                        Console.Write("dx:{0} dy:{1}",
                            (int)(30.0 * vals[3] / 1000.0), (int)(60.0 * vals[2] / 1000.0));
                    }

                    break;
            }
        }

        public void AddButtons(int buttonsDown)
        {
            switch (Config.Mode)
            {

                case Config.MovementModes.JoystickMove:
                case Config.MovementModes.JoystickMoveAndLook:
                case Config.MovementModes.JoystickStrafe:
                    if ((buttonsDown & 32768) != 0) // Y button pressed on Phone
                    {
                        buttonsDown = (short.MinValue | buttonsDown & ~32768); // Y button pressed in terms of XInput
                    }

                    buttons = buttons | buttonsDown;
                    break;

                case Config.MovementModes.Mouse2D:
                    if ((buttonsDown & 4096) != 0 & !_leftMouseButtonDown) // A button pressed on phone
                    {
                        kbm.Mouse.LeftButtonDown();
                        _leftMouseButtonDown = true;
                    }
                    if ((buttonsDown & 4096) == 0 & _leftMouseButtonDown)
                    {
                        kbm.Mouse.LeftButtonUp();
                        _leftMouseButtonDown = false;
                    }

                    if ((buttonsDown & 8192) != 0 & !_rightMouseButtonDown) // B button pressed on phone
                    {
                        kbm.Mouse.RightButtonDown();
                        _rightMouseButtonDown = true;
                    }
                    if ((buttonsDown & 8192) == 0 & _rightMouseButtonDown)
                    {
                        kbm.Mouse.RightButtonUp();
                        _rightMouseButtonDown = false;
                    }
                    break;
             }
        
        }

        public void UpdateMode(int new_mode)
        {
            if(new_mode == (int)Config.Mode) { return; }

            Config.Mode = (Config.MovementModes)new_mode;
        }

        public void AddControllerState(State state)
        {
            X += state.Gamepad.LeftThumbX;
            Y -= state.Gamepad.LeftThumbY; // inverted for some reason
            rX += state.Gamepad.RightThumbX;
            rY += state.Gamepad.RightThumbY;
            Z += state.Gamepad.LeftTrigger; // not the right scale
            rZ += state.Gamepad.RightTrigger; // not the right scale
            buttons = (short)state.Gamepad.Buttons;      
        }

        public void FeedVJoy()
        {
            if (Config.Mode == (Config.MovementModes.Mouse2D | Config.MovementModes.Paused | Config.MovementModes.KeyboardMouse))
            {
                return;
            }
#if ROBUST
            /* incomplete now */
                    res = joystick.SetAxis(X, Config.ID, HID_USAGES.HID_USAGE_X);
                    res = joystick.SetAxis(Y, Config.ID, HID_USAGES.HID_USAGE_Y);
                    res = joystick.SetAxis(rX, Config.ID, HID_USAGES.HID_USAGE_RX);
                    res = joystick.SetAxis(rY, Config.ID, HID_USAGES.HID_USAGE_RY);

                    if (ContPovNumber > 0)
                    {
                        res = joystick.SetContPov((int)POV_f, Config.ID, 1);
                    }
#endif
#if EFFICIENT
            iReport.bDevice = (byte)Config.ID;

            iReport.AxisX = X;
            iReport.AxisY = Y;
            iReport.AxisXRot = rX;
            iReport.AxisYRot = rY;
            iReport.AxisZ = Z;
            iReport.AxisZRot = rZ;

            // Press/Release Buttons
            iReport.Buttons = (uint)(buttons);

            if (ContPovNumber > 0)
            {
                iReport.bHats = ((uint)POV_f);
                //iReport.bHats = 0xFFFFFFFF; // Neutral state
            }

            /*Feed the driver with the position packet - is fails then wait for input then try to re-acquire device */
            if (!joystick.UpdateVJD(Config.ID, ref iReport))
            {
                Console.WriteLine("Feeding vJoy device number {0} failed - try to enable device then press enter\n", Config.ID);
                Console.ReadKey(true);
                joystick.AcquireVJD(Config.ID);
            }
#endif
        }

        private void ConfigureVJoy(uint id)
        {
            /* Enable and Configure a vJoy device by calling 2 external processes
             * Requires path to the vJoy dll directory */
            String filename = "C:\\Program Files\\vJoy\\x64\\vJoyConfig";
            String enableArgs = "enable on";
            String createArgs = String.Format("{0}", id);
            String configArgs = String.Format("{0} -f -a x y rx ry z rz -b 14 -p 1", id);

            ProcessStartInfo[] infos = new ProcessStartInfo[]
            {
              //  new ProcessStartInfo(filename, enableArgs),
              //  new ProcessStartInfo(filename, configArgs),
              //  new ProcessStartInfo(filename, createArgs),
            };

            Process vJoyConfigProc;
            foreach(ProcessStartInfo info in infos)
            {
                //Vista or higher check
                if (Environment.OSVersion.Version.Major >= 6)
                {
                    info.Verb = "runas";
                }

                info.UseShellExecute = true;
                info.RedirectStandardError = false;
                info.RedirectStandardOutput = false;
                info.CreateNoWindow = true;
                vJoyConfigProc = Process.Start(info);
                vJoyConfigProc.WaitForExit();
            }
        }
        
        private void StartVJoy(UInt32 id)
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
                Console.WriteLine("Defaulting to KBM simulation.");
                return;
            }
            else
            {
                Console.WriteLine("Vendor: {0}\nProduct :{1}\nVersion Number:{2}\n", joystick.GetvJoyManufacturerString(), joystick.GetvJoyProductString(), joystick.GetvJoySerialNumberString());
                Config.Mode = Config.MovementModes.JoystickStrafe;
            }

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
            {
                Console.WriteLine("Version of Driver Matches DLL Version ({0:X})\n", DllVer);
            }
            else
            {
                Console.WriteLine("Version of Driver ({0:X}) does NOT match DLL Version ({1:X})\n", DrvVer, DllVer);
            }


            // Acquire the target
            if ((status == VjdStat.VJD_STAT_OWN) || ((status == VjdStat.VJD_STAT_FREE) && (!joystick.AcquireVJD(id))))
            {
                Console.WriteLine("Failed to acquire vJoy device number {0}.\n", id);
                return;
            }
            else
            {
                Console.WriteLine("Acquired: vJoy device number {0}.\n", id);
            }

        }

        private void SetUpVJoy(UInt32 id)
        {
#if ROBUST
            // Reset this device to default values
            joystick.ResetVJD(Config.ID);
#endif
#if EFFICIENT
            pov = new byte[4];
#endif
            // get max range of joysticks
            // neutral position is max/2
            joystick.GetVJDAxisMax(id, HID_USAGES.HID_USAGE_X, ref maxX);
            joystick.GetVJDAxisMax(id, HID_USAGES.HID_USAGE_Y, ref maxY);
            joystick.GetVJDAxisMax(id, HID_USAGES.HID_USAGE_RX, ref maxRX);
            joystick.GetVJDAxisMax(id, HID_USAGES.HID_USAGE_RY, ref maxRY);
            joystick.GetVJDAxisMax(id, HID_USAGES.HID_USAGE_POV, ref maxPOV);
            joystick.GetVJDAxisMax(id, HID_USAGES.HID_USAGE_Z, ref maxZ);
            joystick.GetVJDAxisMax(id, HID_USAGES.HID_USAGE_RZ, ref maxRZ);
            ContPovNumber = joystick.GetVJDContPovNumber(id);
        }

        public void DisableVJoy(uint id)
        {
            /* Enable and Configure a vJoy device by calling 2 external processes
             * Requires path to the vJoy dll directory */
            String filename = "C:\\Program Files\\vJoy\\x64\\vJoyConfig";
            String disableArgs = "enable off";
            String deleteArgs = String.Format("-d {0}", id);

            ProcessStartInfo[] infos = new ProcessStartInfo[]
            {
                new ProcessStartInfo(filename, deleteArgs),
                new ProcessStartInfo(filename, disableArgs),
            };

            Process vJoyConfigProc;
            foreach (ProcessStartInfo info in infos)
            {
                //Vista or higher check
                if (Environment.OSVersion.Version.Major >= 6)
                {
                    info.Verb = "runas";
                }

                info.UseShellExecute = true;
                info.RedirectStandardError = false;
                info.RedirectStandardOutput = false;
                info.CreateNoWindow = true;
                vJoyConfigProc = Process.Start(info);
                vJoyConfigProc.WaitForExit();
            }
        }
    }
}
