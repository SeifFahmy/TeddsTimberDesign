using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Tekla.Structural.InteropAssemblies.TeddsCalc;
using RtfPipe;


namespace TeddsTimberDesign
{
    public class TeddsApplication
    {
        #region Set-up

        static readonly string calcFileName = "$(SysLbrDir)Timber member design-EN1995-si-engb.lbr";
        static readonly string calcItemName = "Timber member design";
        static readonly Calculator calculator = new();

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
            calculator.Functions.SetVar("N_{s1}", 1); // N = number of sections forming timber member
            calculator.Functions.SetVar("_EnablePreview", 1);
            string defaultVariables = calculator.GetVariables();
            calculator.InitializeCalc(calcFileName, calcItemName, defaultVariables);
        }
        #endregion

        #region Member design

        class SectionSize
        {
            public int Width { get; set; }
            public int Depth { get; set; }
        }

        /// <summary>
        /// This takes the previously user-defined material values, adds to them the member-specific variables, and runs the calculation. 
        /// </summary> 
        public static List<Dictionary<string, object>> DesignMembers(List<MemberData> memberData)
        {
            var possibleSectionSizes = new List<SectionSize>() {
                new SectionSize {Width=120, Depth=280},
                new SectionSize {Width=160, Depth=360},
                new SectionSize {Width=160, Depth=440},
                new SectionSize {Width=200, Depth=520},
                new SectionSize {Width=280, Depth=600},
                new SectionSize {Width=320, Depth=680},
                new SectionSize {Width=320, Depth=760},
                new SectionSize {Width=320, Depth=840},
                new SectionSize {Width=360, Depth=920},
                new SectionSize {Width=360, Depth=1000},
            };

            calculator.Functions.SetVar("_CalcUI", 0);

            var results = new List<Dictionary<string, object>>();
            foreach (var member in memberData)
            {
                if (member.Axial >= 0) { calculator.Functions.SetVar("_AxialForce_{s1}", "Compression"); }
                else { calculator.Functions.SetVar("_AxialForce_{s1}", "Tension"); }

                calculator.Functions.SetVar("M_{y,d_s1}", Math.Abs(member.MomentMajor), "kNm");
                calculator.Functions.SetVar("M_{z,d_s1}", Math.Abs(member.MomentMinor), "kNm");
                calculator.Functions.SetVar("F_{y,d_s1}", Math.Abs(member.ShearMajor), "kN");
                calculator.Functions.SetVar("F_{z,d_s1}", Math.Abs(member.ShearMinor), "kN");
                calculator.Functions.SetVar("P_{d_s1}", Math.Abs(member.Axial), "kN");

                string result = "FAIL";
                double width, depth;
                for (int i = 0; i < possibleSectionSizes.Count(); i++)
                {
                    var section = possibleSectionSizes[i];
                    calculator.Functions.SetVar("b_{s1}", section.Width, "mm");
                    calculator.Functions.SetVar("h_{s1}", section.Depth, "mm");

                    string variables = calculator.GetVariables();
                    calculator.InitializeCalc(calcFileName, calcItemName, variables);

                    result = calculator.Functions.GetVar("_CalcResult").ToString().ToUpper();
                    if (result == "PASS")
                    {
                        break;
                    }

                    double currentUtil = calculator.Functions.GetVar("_OverallUtilisation_{s1}").ToDouble();
                    if (currentUtil > 2)
                    {
                        // given the section is far too small, skipping the next section saves time given it's likely also failing
                        i = Math.Min(i + 1, possibleSectionSizes.Count() - 2);
                    }
                }

                width = calculator.Functions.GetVar("b").ToDouble("mm");
                depth = calculator.Functions.GetVar("h").ToDouble("mm");
                double util = Math.Round(calculator.Functions.GetVar("_OverallUtilisation_{s1}").ToDouble(), 2);
                string designMessage = calculator.Functions.GetVar("_OverallStatusMessage_{s1}").ToString();
                string material = calculator.Functions.GetVar("MemberType").ToString();
                string strengthClass = calculator.Functions.GetVar("StrengthClass").ToString();

                string outputRtf = calculator.GetOutput();

                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                string outputHtml = Rtf.ToHtml(outputRtf);

                results.Add(new Dictionary<string, object>(){
                    { "id", member.Id },
                    { "section", $"{width}x{depth}" },
                    { "result", result },
                    { "designMessage", designMessage },
                    { "util", util },
                    { "material", material },
                    { "strength", strengthClass },
                    { "outputHtml", outputHtml }
                });
            }

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