using System;
using System.Diagnostics;
using System.Windows;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Tekla.Structural.InteropAssemblies.TeddsCalc;
using Tekla.Structural.InteropAssemblies.Tedds;



namespace TeddsTimberDesign
{
    public class TeddsApplication
    {
        #region Set-up

        static readonly string calcFileName = "$(SysLbrDir)Timber beam design-BS5268-si-engb.lbr";
        static readonly string calcItemName = "Timber member design";
        static readonly Calculator calculator = new Calculator();

        /// <summary>
        /// This class brings the tedds window to the front and makes it a child of some chosen application.
        /// This means whenever the parent window is minimised/maximised, brought into/out of focus, the tedds
        /// window will follow.
        /// </summary> 
        /// <returns>Returns 1 if the process was unsuccessful, and 0 if it was.</returns>
        public static int SetUpTeddsWindow(string parentWindowName)
        {
            User32Native.SetForegroundWindow((IntPtr)calculator.WindowHandle);

            Process[] parentProcesses = Process.GetProcessesByName(parentWindowName);

            if (parentProcesses.Length == 0)
            {
                Console.WriteLine($"No {parentWindowName} processes found.");
                return 1;
            }

            IntPtr parentWindowHandle = IntPtr.Zero;

            foreach (Process process in parentProcesses)
            {
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    parentWindowHandle = process.MainWindowHandle;
                    break;
                }
            }

            if (parentWindowHandle == IntPtr.Zero)
            {
                Console.WriteLine($"{parentWindowName} main window handle not found or not ready.");
                return 1;
            }

            calculator.SetOwnerWindow((int)parentWindowHandle);
            return 0;

        }

        /// <summary>
        /// This initial window is for the user to set up variables like the strength class and whether it's Glulam or Solid Timber.
        /// The variables the user defines in this window are saved in the calculator instance to be used by future design calculations.
        /// </summary> 

        public static void ShowInitialWindow()
        {
            calculator.Initialize();
            string defaultVariables = calculator.GetVariables();
            calculator.InitializeCalc(calcFileName, calcItemName, defaultVariables);
        }
        #endregion

        #region Member design
        /// <summary>
        /// This takes the previously user-defined material values, adds to them the member-specific variables, and runs the calculation. 
        /// </summary> 
        public static Dictionary<string, object> DesignMember(List<MemberData> memberData)
        {
            calculator.Functions.SetVar("_CalcUI", 0);

            foreach (var member in memberData)
            {
                // var id = member["id"];
                // var moment = 
                System.Console.WriteLine(member.Moment);
            }
            calculator.Functions.SetVar("b", 600, "mm");
            string variables = calculator.GetVariables();

            calculator.InitializeCalc(calcFileName, calcItemName, variables);

            //Query the calculation results
            string width = calculator.Functions.GetVar("b").ToDouble("mm").ToString();
            string moment = calculator.Functions.GetVar("M_{x}").ToDouble("kNm").ToString();

            var results = new Dictionary<string, object>
            {
                { "width", width },
                {"moment", moment}
            };
            return results;
        }
        #endregion
    }

    #region Misc
    /// <summary>This class was present in Tedds's own API Tester tool - it brings the Tedds window to the front.</summary> 
    internal static class User32Native
    {
        [DllImport(User32Dll)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        private const string User32Dll = "user32.dll";
    }
    #endregion
}