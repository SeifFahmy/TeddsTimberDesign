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
        /// <summary>
        /// This takes the previously user-defined material values, adds to them the member-specific variables, and runs the calculation. 
        /// </summary> 
        public static List<Dictionary<string, object>> DesignMembers(DesignData designData)
        {
            calculator.Functions.SetVar("_CalcUI", 0);

            var memberData = designData.RobotMemberData;
            var robotMaterialData = designData.RobotMaterialData;
            var deflectionLimit = designData.BeamDeflectionLimitRatio;

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

                string material = calculator.Functions.GetVar("MemberType").ToString();

                List<SectionSizeData> possibleSectionSizes;
                if (material == "Glulam" && member.IsAxialMember)
                {
                    possibleSectionSizes = SectionSizes.glulamColumnSectionSizes;
                }
                else if (material == "Glulam" && !member.IsAxialMember)
                {
                    possibleSectionSizes = SectionSizes.glulamBeamSectionSizes;
                }
                else if (material == "Timber" && member.IsAxialMember)
                {
                    possibleSectionSizes = SectionSizes.timberColumnSectionSizes;
                }
                else
                {
                    possibleSectionSizes = SectionSizes.timberBeamSectionSizes;
                }

                double effectiveUdl = OwnTimberDesign.CalculateEffectiveUDL(member.Deflection, robotMaterialData.RobotE, robotMaterialData.RobotG, member.Area, member.SecondMomentOfArea, member.Length);

                var stabilityCheck = new OwnTimberDesign.StabilityResult { Result = "FAIL", StabilityUtil = -1 };
                var deflectionCheck = new OwnTimberDesign.DeflectionResult { Result = member.IsAxialMember ? "PASS" : "FAIL", DeflectionUtil = -1 }; // no deflection check for columns
                string result = "FAIL";
                for (int i = 0; i < possibleSectionSizes.Count; i++)
                {
                    var section = possibleSectionSizes[i];
                    calculator.Functions.SetVar("b_{s1}", section.Width, "mm");
                    calculator.Functions.SetVar("h_{s1}", section.Depth, "mm");

                    string variables = calculator.GetVariables();
                    calculator.InitializeCalc(calcFileName, calcItemName, variables);

                    string strengthResult = calculator.Functions.GetVar("_CalcResult").ToString().ToUpper();
                    stabilityCheck = OwnTimberDesign.StabilityCheck(calculator, member.Length, member.IsAxialMember);

                    if (!member.IsAxialMember)
                    {
                        deflectionCheck = OwnTimberDesign.DeflectionCheck(calculator, effectiveUdl, member.Length, deflectionLimit);
                    }

                    if (strengthResult == "PASS" && stabilityCheck.Result == "PASS" && deflectionCheck.Result == "PASS")
                    {
                        result = "PASS";
                        break;
                    }

                    double currentUtil = calculator.Functions.GetVar("_OverallUtilisation_{s1}").ToDouble();
                    if (currentUtil > 2 && i < possibleSectionSizes.Count)
                    {
                        // given the section is far too small, skipping the next section saves time given it's likely also failing
                        i = Math.Min(i + 1, possibleSectionSizes.Count - 2);
                    }
                    // break;
                }

                double width = calculator.Functions.GetVar("b").ToDouble("mm");
                double depth = calculator.Functions.GetVar("h").ToDouble("mm");

                double strengthUtil = Math.Round(calculator.Functions.GetVar("_OverallUtilisation_{s1}").ToDouble(), 2);
                double util = Math.Max(strengthUtil, stabilityCheck.StabilityUtil);

                string designMessage = calculator.Functions.GetVar("_OverallStatusMessage_{s1}").ToString();
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

    #region Section Sizes
    class SectionSizeData
    {
        public int Width { get; set; }
        public int Depth { get; set; }
    }

    static class SectionSizes
    {
        public static readonly List<SectionSizeData> glulamBeamSectionSizes = new() {
            new SectionSizeData {Width=120, Depth=280},
            new SectionSizeData {Width=160, Depth=360},
            new SectionSizeData {Width=160, Depth=440},
            new SectionSizeData {Width=200, Depth=520},
            new SectionSizeData {Width=280, Depth=600},
            new SectionSizeData {Width=320, Depth=680},
            new SectionSizeData {Width=320, Depth=760},
            new SectionSizeData {Width=320, Depth=840},
            new SectionSizeData {Width=360, Depth=920},
            new SectionSizeData {Width=360, Depth=1000},
            };
        public static readonly List<SectionSizeData> timberBeamSectionSizes = new() {
            new SectionSizeData {Width=75, Depth=150},
            new SectionSizeData {Width=100, Depth=200},
            new SectionSizeData {Width=100, Depth=250},
            new SectionSizeData {Width=150, Depth=275},
            new SectionSizeData {Width=150, Depth=300},
        };
        public static readonly List<SectionSizeData> glulamColumnSectionSizes = new() {
            new SectionSizeData {Width=200, Depth=200},
            new SectionSizeData {Width=240, Depth=240},
            new SectionSizeData {Width=280, Depth=280},
            new SectionSizeData {Width=320, Depth=320},
            new SectionSizeData {Width=400, Depth=400},
        };
        public static readonly List<SectionSizeData> timberColumnSectionSizes = new() {
            new SectionSizeData {Width=100, Depth=100},
            new SectionSizeData {Width=150, Depth=150},
            new SectionSizeData {Width=200, Depth=200},
            new SectionSizeData {Width=250, Depth=250},
            new SectionSizeData {Width=300, Depth=300},
        };
    }
    #endregion

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