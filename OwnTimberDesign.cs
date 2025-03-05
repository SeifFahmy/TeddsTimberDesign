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

        public static DeflectionResult DeflectionCheck(double w, double teddsE, double teddsG, double teddsKdef, double teddsQuasiPermFactor, double width, double depth, double span, double limitRatio)
        {
            double A = width * depth;
            double I = width * Math.Pow(depth, 3) / 12;

            double instantDeflection = 5 * w * Math.Pow(span, 4) / (384 * teddsE * I) + w * Math.Pow(span, 2) / (8 * A * teddsG);
            double finalDeflection = instantDeflection * (1 + teddsKdef);

            double deflectionLimit = span * 1000 / limitRatio;
            string result = finalDeflection <= deflectionLimit ? "PASS" : "FAIL";

            return new DeflectionResult { Result = result, Deflection = finalDeflection };
        }
    }
}