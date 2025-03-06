using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tekla.Structural.InteropAssemblies.TeddsCalc;

namespace TeddsTimberDesign
{
    public class OwnTimberDesign
    {
        public class DeflectionResult
        {
            public string Result { get; set; }
            public double Deflection { get; set; }
        }

        public class StabilityResult
        {
            public string Result { get; set; }
            public double StabilityRatio { get; set; }
        }

        public static double CalculateEffectiveUDL(double robotDeflection, double robotE, double robotG, double robotA, double robotI, double span)
        {
            // bending deflection = 5wL^4/384EI
            // shear deflection = wL^2 / 8AG
            // need to rearrange above equations to get w as a function of instataneous deflection
            // calculating deflections for different section sizes is based off this
            double effective_udl_bending_formula = 5 * Math.Pow(span, 4) / (384 * robotE * robotI);
            double effective_udl_shear_formula = Math.Pow(span, 2) / (8 * robotA * robotG);
            double effective_udl = robotDeflection / (effective_udl_bending_formula + effective_udl_shear_formula);

            return effective_udl;
        }

        public static DeflectionResult DeflectionCheck(Calculator calculator, double UDL, double span, double limitRatio)
        {
            // design to EC5-1 cl.2.2.3 and cl.7.2
            double A = calculator.Functions.GetVar("A_{s1}").ToDouble();
            double I = calculator.Functions.GetVar("I_{y_s1}").ToDouble();
            double E = calculator.Functions.GetVar("E_{0.mean}").ToDouble();
            double G = calculator.Functions.GetVar("G_{g.mean}").ToDouble();
            double k_def = calculator.Functions.GetVar("k_{def_s1}").ToDouble();
            double quasiPermFactor = calculator.Functions.GetVar("""\79_{2_s1}""").ToDouble();

            double instantDeflection = 5 * UDL * Math.Pow(span, 4) / (384 * E * I) + UDL * Math.Pow(span, 2) / (8 * A * G);
            double finalDeflection = instantDeflection * (1 + k_def);

            double deflectionLimit = span * 1000 / limitRatio;
            string result = finalDeflection <= deflectionLimit ? "PASS" : "FAIL";

            return new DeflectionResult { Result = result, Deflection = finalDeflection };
        }

        public static StabilityResult BeamStabilityCheck(Calculator calculator, double length)
        {
            // design to EC5-1 cl.6.3.3
            double E0_05 = calculator.Functions.GetVar("E_{0.g.05}").ToDouble();
            double Iz = calculator.Functions.GetVar("I_{z_s1}").ToDouble();
            double Itor = calculator.Functions.GetVar("I_{tor_s1}").ToDouble();
            double Wy = calculator.Functions.GetVar("W_{y_s1}").ToDouble();

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

            double iy = calculator.Functions.GetVar("i_{y_s1}").ToDouble();
            double f_c0k = calculator.Functions.GetVar("f_{c.0.k}").ToDouble();

            double slendernessMinor = effectiveLength / iy;
            double relativeCompressionSlenderness = slendernessMinor / Math.PI * Math.Sqrt(f_c0k / E0_05);

            string material = calculator.Functions.GetVar("MemberType").ToString();
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

            return new StabilityResult { Result = result, StabilityRatio = stabilityCheck };
        }

        public static StabilityResult ColumnStabilityCheck(Calculator calculator, double length)
        {


            return new StabilityResult { Result = "result", StabilityRatio = 1 };
        }

    }
}