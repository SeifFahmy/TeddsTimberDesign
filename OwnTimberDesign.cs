using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Tekla.Structural.InteropAssemblies.TeddsCalc;

namespace TeddsTimberDesign
{
    public class OwnTimberDesign
    {
        public class DeflectionResult
        {
            public string Result { get; set; }
            public double DeflectionUtil { get; set; }
            public string DeflectionHtml { get; set; }
        }

        public class StabilityResult
        {
            public string Result { get; set; }
            public double StabilityUtil { get; set; }

        }

        #region Deflection Checks
        public static double CalculateEffectiveUDL(double robotDeflection, double robotE, double robotG, double robotA, double robotI, double span)
        {
            // bending deflection = 5wL^4/384EI
            // shear deflection = wL^2 / 8AG
            // need to rearrange above equations to get w as a function of instataneous deflection
            // calculating deflections for different section sizes is based off this
            double effectiveUdlBendingFormula = 5 * Math.Pow(span, 4) / (384 * robotE * robotI);
            double effectiveUdlShearFormula = Math.Pow(span, 2) / (8 * robotA * robotG);
            double effectiveUdl = robotDeflection / (effectiveUdlBendingFormula + effectiveUdlShearFormula);

            return effectiveUdl;
        }

        public static DeflectionResult DeflectionCheck(Calculator calculator, double UDL, double span, double limitRatio)
        {
            // design to EC5-1 cl.2.2.3 and cl.7.2
            double A = calculator.Functions.GetVar("A_{s1}").ToDouble();
            double I = calculator.Functions.GetVar("I_{y_s1}").ToDouble();
            double E = calculator.Functions.GetVar("E_{0.mean}").ToDouble();
            double k_def = calculator.Functions.GetVar("k_{def_s1}").ToDouble();
            double quasiPermFactor = calculator.Functions.GetVar("""\79_{2_s1}""").ToDouble();

            double G;
            string material = calculator.Functions.GetVar("MemberType").ToString();
            if (material == "Glulam") { G = calculator.Functions.GetVar("G_{g.mean}").ToDouble(); }
            else { G = calculator.Functions.GetVar("G_{mean}").ToDouble(); }

            double instantDeflection = 5 * UDL * Math.Pow(span, 4) / (384 * E * I) + UDL * Math.Pow(span, 2) / (8 * A * G);
            double finalDeflection = instantDeflection * (1 + k_def);

            double deflectionLimit = span / limitRatio;
            string result = finalDeflection <= deflectionLimit ? "PASS" : "FAIL";
            string designMessage = result == "PASS" ? "PASS - Allowable deflection exceeds final deflection" : "";

            // based on html obtained from the strength verification
            string teddsCalcHtml = $"""
                <div style="font-size:12pt;font-family:Arial, sans-serif; background-color:gray;">
                    <h5 style="text-align:left;line-height:1.6;font-family:Arial, sans-serif;font-size:9pt;margin:5.3px 0 0 23.8px;">Check deflection - Section 7.2</h5>
                    <div style="display:flex; flex-flow: column nowrap;">
                        <div style="display:flex; flex-flow: row nowrap;text-align:left;line-height:1.6;font-family:Arial, sans-serif;font-size:9pt">
                            <p style="flex: 1 1 0;">Instantaneous deflection</p>
                            <p style="flex: 1 1 0;">δ<sub>y</sub> = {Math.Round(instantDeflection * 1000)} mm</p>
                        </div>
                        <div style="display:flex; flex-flow: row nowrap;text-align:left;line-height:1.6;font-family:Arial, sans-serif;font-size:9pt">
                            <p style="flex: 1 1 0;">Final deflection with creep</p>
                            <p style="flex: 1 1 0;">δ<sub>y,Final</sub> = δ<sub>y</sub> x (1 + k<sub>def</sub>) = {Math.Round(finalDeflection * 1000)} mm</p>
                        </div>
                        <div style="display:flex; flex-flow: row nowrap;text-align:left;line-height:1.6;font-family:Arial, sans-serif;font-size:9pt">
                            <p style="flex: 1 1 0;">Allowable deflection</p>
                            <p style="flex: 1 1 0;">δ<sub>y,Allowable</sub> = L / {limitRatio} = {Math.Round(deflectionLimit * 1000)} mm</p>
                        </div>
                        <div style="display:flex; flex-flow: row nowrap;text-align:left;line-height:1.6;font-family:Arial, sans-serif;font-size:9pt">
                            <p style="flex: 1 1 0;"> </p>
                            <p style="flex: 1 1 0;">δ<sub>y,Final</sub> / δ<sub>y,Allowable</sub> = {Math.Round(finalDeflection / deflectionLimit * 1000) / 1000}</p>
                        </div>
                        <p style="text-align:right; font-style:italic; font-size:9pt;"><strong><em>{designMessage}</em></strong></p>
                    </div>
                </div>
            """;

            return new DeflectionResult { Result = result, DeflectionUtil = finalDeflection, DeflectionHtml = teddsCalcHtml };
        }
        #endregion

        #region Beam stability check
        public static StabilityResult StabilityCheck(Calculator calculator, double length, bool isColumn)
        {
            if (isColumn) { return ColumnStabilityCheck(calculator, length); }
            else { return BeamStabilityCheck(calculator, length); }
        }

        public static StabilityResult BeamStabilityCheck(Calculator calculator, double length)
        {
            // design to EC5-1 cl.6.3.3
            double Iz = calculator.Functions.GetVar("I_{z_s1}").ToDouble();
            double Itor = calculator.Functions.GetVar("I_{tor_s1}").ToDouble();
            double Wy = calculator.Functions.GetVar("W_{y_s1}").ToDouble();

            double E0_05;
            string material = calculator.Functions.GetVar("MemberType").ToString();
            if (material == "Glulam") { E0_05 = calculator.Functions.GetVar("E_{0.g.05}").ToDouble(); }
            else { E0_05 = calculator.Functions.GetVar("E_{0.05}").ToDouble(); }

            double G0_05 = E0_05 / 16;
            double effectiveLength = length * 0.9; // assuming UDL on simply supported beam - table 6.1
            double criticalBendingStressMajor = Math.PI * Math.Sqrt(E0_05 * Iz * G0_05 * Itor) / (effectiveLength * Wy);

            double f_mk = calculator.Functions.GetVar("f_{m.k}").ToDouble();
            double relativeBendingSlenderness = Math.Sqrt(f_mk / criticalBendingStressMajor);

            double k_crit;
            if (relativeBendingSlenderness <= 0.75)
            {
                k_crit = 1;
            }
            else if (0.75 < relativeBendingSlenderness && relativeBendingSlenderness <= 1.4)
            {
                k_crit = 1.56 - 0.75 * relativeBendingSlenderness;
            }
            else
            {
                k_crit = 1 / Math.Pow(relativeBendingSlenderness, 2);
            }

            double iz = calculator.Functions.GetVar("i_{z_s1}").ToDouble();

            double f_c0k;
            if (material == "Glulam") { f_c0k = calculator.Functions.GetVar("f_{c.0.g.k}").ToDouble(); }
            else { f_c0k = calculator.Functions.GetVar("f_{c.0.k}").ToDouble(); }

            double slendernessMinor = effectiveLength / iz;
            double relativeCompressionSlenderness = slendernessMinor / Math.PI * Math.Sqrt(f_c0k / E0_05);

            double beta_c;
            if (material == "Glulam")
            {
                beta_c = 0.1;
            }
            else
            {
                beta_c = 0.2;
            }

            double k_z = 0.5 * (1 + beta_c * (relativeCompressionSlenderness - 0.3) + Math.Pow(relativeCompressionSlenderness, 2));
            double k_cz = 1 / (k_z + Math.Sqrt(Math.Pow(k_z, 2) - Math.Pow(relativeCompressionSlenderness, 2)));

            double bendingStress = calculator.Functions.GetVar("""\73_{m,y,d_s1}""").ToDouble();
            double bendingStrength = calculator.Functions.GetVar("f_{m,y,d_s1}").ToDouble();
            double compressiveStress = calculator.Functions.GetVar("""\73_{c,0,d_s1}""").ToDouble();
            double compressiveStrength = calculator.Functions.GetVar("f_{c,0,d_s1}").ToDouble();

            double stabilityCheck = Math.Pow(bendingStress / (k_crit * bendingStrength), 2) + compressiveStress / (k_cz * compressiveStrength);
            string result = stabilityCheck <= 1 ? "PASS" : "FAIL";

            return new StabilityResult { Result = result, StabilityUtil = stabilityCheck };
        }
        #endregion

        #region Column stability check
        public static StabilityResult ColumnStabilityCheck(Calculator calculator, double length)
        {
            // design to EC5-1 cl.6.3.2
            double iy = calculator.Functions.GetVar("i_{y_s1}").ToDouble();
            double iz = calculator.Functions.GetVar("i_{z_s1}").ToDouble();

            string material = calculator.Functions.GetVar("MemberType").ToString();

            double E0_05;
            if (material == "Glulam") { E0_05 = calculator.Functions.GetVar("E_{0.g.05}").ToDouble(); }
            else { E0_05 = calculator.Functions.GetVar("E_{0.05}").ToDouble(); }

            double f_c0k;
            if (material == "Glulam") { f_c0k = calculator.Functions.GetVar("f_{c.0.g.k}").ToDouble(); }
            else { f_c0k = calculator.Functions.GetVar("f_{c.0.k}").ToDouble(); }

            double effectiveLength = length * 0.9; // assuming UDL on simply supported beam - table 6.1

            double slendernessMinor = effectiveLength / iz;
            double slendernessMajor = effectiveLength / iy;

            double relativeSlendernessMinor = slendernessMinor / Math.PI * Math.Sqrt(f_c0k / E0_05);
            double relativeSlendernessMajor = slendernessMajor / Math.PI * Math.Sqrt(f_c0k / E0_05);

            if (relativeSlendernessMajor <= 0.3 && relativeSlendernessMinor <= 0.3)
            {
                return new StabilityResult { Result = "PASS", StabilityUtil = Math.Max(relativeSlendernessMajor, relativeSlendernessMinor) };
            }

            double beta_c;
            if (material == "Glulam")
            {
                beta_c = 0.1;
            }
            else
            {
                beta_c = 0.2;
            }

            double k_z = 0.5 * (1 + beta_c * (relativeSlendernessMinor - 0.3) + Math.Pow(relativeSlendernessMinor, 2));
            double k_cz = 1 / (k_z + Math.Sqrt(Math.Pow(k_z, 2) - Math.Pow(relativeSlendernessMinor, 2)));

            double k_y = 0.5 * (1 + beta_c * (relativeSlendernessMajor - 0.3) + Math.Pow(relativeSlendernessMajor, 2));
            double k_cy = 1 / (k_y + Math.Sqrt(Math.Pow(k_y, 2) - Math.Pow(relativeSlendernessMajor, 2)));

            double majorBendingStress = calculator.Functions.GetVar("""\73_{m,y,d_s1}""").ToDouble();
            double majorBendingStrength = calculator.Functions.GetVar("f_{m,y,d_s1}").ToDouble();

            double minorBendingStress = calculator.Functions.GetVar("""\73_{m,z,d}""").ToDouble();
            double minorBendingStrength = calculator.Functions.GetVar("f_{m,z,d}").ToDouble();

            double compressiveStress = calculator.Functions.GetVar("""\73_{c,0,d_s1}""").ToDouble();
            double compressiveStrength = calculator.Functions.GetVar("f_{c,0,d_s1}").ToDouble();

            double km = 0.7; // rectangular section

            double minorBendingComponent = minorBendingStress == 0 ? 0 : minorBendingStress / minorBendingStrength;
            double stabilityCheckMajor = compressiveStress / (k_cy * compressiveStrength) + majorBendingStress / majorBendingStrength + km * minorBendingComponent;
            double stabilityCheckMinor = compressiveStress / (k_cz * compressiveStrength) + km * majorBendingStress / majorBendingStrength + minorBendingComponent;

            double stabilityCheck = Math.Max(stabilityCheckMajor, stabilityCheckMinor);
            string result = stabilityCheck <= 1 ? "PASS" : "FAIL";

            return new StabilityResult { Result = result, StabilityUtil = stabilityCheck };
        }
        #endregion

    }
}