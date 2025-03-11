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
            public string DeflectionMessage { get; set; }
        }

        public class StabilityResult
        {
            public string Result { get; set; }
            public double StabilityUtil { get; set; }
            public string StabilityHtml { get; set; }
            public string StabilityMessage { get; set; }

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
            string designMessage = result == "PASS" ? "PASS - Allowable deflection greater than final deflection" : "FAIL - Final deflection greater than allowable deflection";

            // based on html obtained from the strength verification and "member analysis and design" calc
            string teddsCalcHtml = $"""
                <div style="font-size:12pt;font-family:Arial, sans-serif;">
                    <h5 style="text-align:left;font-family:Arial, sans-serif;font-size:9pt;margin:5.3px 0 0 23.8px;">Check deflection - Section 7.2</h5>
                    <div style="display:flex; flex-flow: column nowrap;">
                        <div style="display:flex; flex-flow: row nowrap;text-align:left;font-family:Arial, sans-serif;font-size:9pt">
                            <p style="flex: 1 1 0;">Instantaneous deflection</p>
                            <p style="flex: 1 1 0;">δ<sub>y</sub> = {Math.Round(instantDeflection * 1000)} mm</p>
                        </div>
                        <div style="display:flex; flex-flow: row nowrap;text-align:left;font-family:Arial, sans-serif;font-size:9pt">
                            <p style="flex: 1 1 0;">Final deflection with creep</p>
                            <p style="flex: 1 1 0;">δ<sub>y,Final</sub> = δ<sub>y</sub> x (1 + k<sub>def</sub>) = {Math.Round(finalDeflection * 1000)} mm</p>
                        </div>
                        <div style="display:flex; flex-flow: row nowrap;text-align:left;font-family:Arial, sans-serif;font-size:9pt">
                            <p style="flex: 1 1 0;">Allowable deflection</p>
                            <p style="flex: 1 1 0;">δ<sub>y,Allowable</sub> = L / {limitRatio} = {Math.Round(deflectionLimit * 1000)} mm</p>
                        </div>
                        <div style="display:flex; flex-flow: row nowrap;text-align:left;font-family:Arial, sans-serif;font-size:9pt">
                            <p style="flex: 1 1 0;"> </p>
                            <p style="flex: 1 1 0;">δ<sub>y,Final</sub> / δ<sub>y,Allowable</sub> = {Math.Round(finalDeflection / deflectionLimit * 1000) / 1000}</p>
                        </div>
                        <p style="text-align:right; font-style:italic; font-size:9pt;"><strong><em>{designMessage}</em></strong></p>
                    </div>
                </div>
            """;
            teddsCalcHtml = teddsCalcHtml.ReplaceLineEndings("");

            return new DeflectionResult { Result = result, DeflectionUtil = finalDeflection / deflectionLimit, DeflectionHtml = teddsCalcHtml, DeflectionMessage = designMessage };
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

            // tedds doesn't calculate a stress or strength (e.g. bending) if its force (e.g. moment) isn't applied, so inspecting the stress value throws an error
            bool momentMajorApplied = calculator.Functions.GetVar("M_{y,d_s1}").ToDouble() == 0 ? false : true;
            bool axialApplied = calculator.Functions.GetVar("P_{d_s1}").ToDouble() == 0 ? false : true;

            double bendingStress = momentMajorApplied ? calculator.Functions.GetVar("""\73_{m,y,d_s1}""").ToDouble() : 0;
            double bendingStrength = momentMajorApplied ? calculator.Functions.GetVar("f_{m,y,d_s1}").ToDouble() : 1;
            double compressiveStress = axialApplied ? calculator.Functions.GetVar("""\73_{c,0,d_s1}""").ToDouble() : 0;
            double compressiveStrength = axialApplied ? calculator.Functions.GetVar("f_{c,0,d_s1}").ToDouble() : 1;

            double stabilityCheck = Math.Pow(bendingStress / (k_crit * bendingStrength), 2) + compressiveStress / (k_cz * compressiveStrength);
            string result = stabilityCheck <= 1 ? "PASS" : "FAIL";
            string designMessage = result == "PASS" ? "PASS - Beam stability is acceptable" : "FAIL - Beam stability is not acceptable";

            // based on html obtained from the strength verification and "member analysis and design" calc
            string teddsCalcHtml = $"""
                <div style="font-size:12pt;font-family:Arial, sans-serif;">
                    <h5 style="text-align:left;font-family:Arial, sans-serif;font-size:9pt;margin:5.3px 0 0 23.8px;">Check beams subject to either bending or combined bending and compression - Section 6.3.3</h5>
                    <div style="display:flex; flex-flow: column nowrap;">
                        <div style="display:flex; flex-flow: row nowrap;text-align:left;font-family:Arial, sans-serif;font-size:9pt">
                            <p style="flex: 1 1 0;">Fifth percentile shear modulus parallel to the grain</p>
                            <p style="flex: 1 1 0;">G<sub>0.05</sub> = E<sub>0.05</sub> / 16 = {Math.Round(G0_05 / 1_000_000)} N/mm<sup>2</sup></p>
                        </div>
                        <div style="display:flex; flex-flow: row nowrap;text-align:left;font-family:Arial, sans-serif;font-size:9pt">
                            <p style="flex: 1 1 0;">Torsional moment of inertia</p>
                            <p style="flex: 1 1 0;">I<sub>tor</sub> = {Math.Round(Itor * Math.Pow(1000, 4))} mm<sup>4</sup></p>
                        </div>
                        <div style="display:flex; flex-flow: row nowrap;text-align:left;font-family:Arial, sans-serif;font-size:9pt">
                            <p style="flex: 1 1 0;">Lateral buckling factor - exp 6.34</p>
                            <p style="flex: 1 1 0;">k<sub>crit</sub> = {Math.Round(k_crit * 1000) / 1000}</p>
                        </div>
                        <div style="display:flex; flex-flow: row nowrap;text-align:left;font-family:Arial, sans-serif;font-size:9pt">
                            <p style="flex: 1 1 0;">Beam stability check - exp 6.35</p>
                            <p style="flex: 1 1 0;">(σ<sub>m,y,d</sub> / (k<sub>crit</sub> x f<sub>m,y,d</sub>))<sup>2</sup> + σ<sub>c,0,d</sub> / (k<sub>c,z</sub> x f<sub>c,0,d</sub>) = {Math.Round(stabilityCheck * 1000) / 1000}</p>
                        </div>
                        <p style="text-align:right; font-style:italic; font-size:9pt;"><strong><em>{designMessage}</em></strong></p>
                    </div>
                </div>
            """;
            teddsCalcHtml = teddsCalcHtml.ReplaceLineEndings("");

            return new StabilityResult { Result = result, StabilityUtil = stabilityCheck, StabilityHtml = teddsCalcHtml, StabilityMessage = designMessage };
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
                // based on html obtained from the strength verification and "member analysis and design" calc
                string teddsCalcHtmlCheckNotRequired = $"""
                    <div style="font-size:12pt;font-family:Arial, sans-serif;">
                        <h5 style="text-align:left;font-family:Arial, sans-serif;font-size:9pt;margin:5.3px 0 0 23.8px;">Check columns subjected to either bending or combined bending and compression - Section 6.3.2</h5>
                        <div style="display:flex; flex-flow: column nowrap;">
                            <div style="display:flex; flex-flow: row nowrap;text-align:left;font-family:Arial, sans-serif;font-size:9pt">
                                <p style="flex: 1 1 0;">Effective length for y-axis bending</p>
                                <p style="flex: 1 1 0;">L<sub>e,y</sub> = 0.9 x {Math.Round(length * 1000)} mm = {Math.Round(effectiveLength * 1000)} mm</p>
                            </div>
                            <div style="display:flex; flex-flow: row nowrap;text-align:left;font-family:Arial, sans-serif;font-size:9pt">
                                <p style="flex: 1 1 0;">Slenderness ratio</p>
                                <p style="flex: 1 1 0;">λ<sub>y</sub> = L<sub>e,y</sub> / i<sub>y</sub> = {Math.Round(slendernessMajor * 1000) / 1000}</p>
                            </div>
                            <div style="display:flex; flex-flow: row nowrap;text-align:left;font-family:Arial, sans-serif;font-size:9pt">
                                <p style="flex: 1 1 0;">Relative slenderness ratio - exp 6.21</p>
                                <p style="flex: 1 1 0;">λ<sub>rel,y</sub> = λ<sub>y</sub> / π x √(f<sub>c,0,k</sub> / E<sub>0.05</sub>) = {Math.Round(relativeSlendernessMajor * 1000) / 1000}</p>
                            </div>
                            <div style="display:flex; flex-flow: row nowrap;text-align:left;font-family:Arial, sans-serif;font-size:9pt">
                                <p style="flex: 1 1 0;">Effective length for z-axis bending</p>
                                <p style="flex: 1 1 0;">L<sub>e,y</sub> = 0.9 x {Math.Round(length * 1000)} mm = {Math.Round(effectiveLength * 1000)} mm</p>
                            </div>
                            <div style="display:flex; flex-flow: row nowrap;text-align:left;font-family:Arial, sans-serif;font-size:9pt">
                                <p style="flex: 1 1 0;">Slenderness ratio</p>
                                <p style="flex: 1 1 0;">λ<sub>z</sub> = L<sub>e,z</sub> / i<sub>z</sub> = {Math.Round(slendernessMinor * 1000) / 1000}</p>
                            </div>
                            <div style="display:flex; flex-flow: row nowrap;text-align:left;font-family:Arial, sans-serif;font-size:9pt">
                                <p style="flex: 1 1 0;">Relative slenderness ratio - exp 6.22</p>
                                <p style="flex: 1 1 0;">λ<sub>rel,z</sub> = λ<sub>z</sub> / π x √(f<sub>c,0,k</sub> / E<sub>0.05</sub>) = {Math.Round(relativeSlendernessMinor * 1000) / 1000}</p>
                            </div>
                            <p style="text-align:right; font-style:italic; font-size:9pt;"><strong><em>Both λ<sub>rel,y</sub> and λ<sub>rel,z</sub> ≤ 0.3, column stability check not required</em></strong></p>
                        </div>
                    </div>
                """;
                teddsCalcHtmlCheckNotRequired = teddsCalcHtmlCheckNotRequired.ReplaceLineEndings("");


                return new StabilityResult { Result = "PASS", StabilityUtil = Math.Max(relativeSlendernessMajor, relativeSlendernessMinor), StabilityHtml = teddsCalcHtmlCheckNotRequired, StabilityMessage = "Column stability check not required" };
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
            string designMessage = result == "PASS" ? "PASS - Column stability is acceptable" : "FAIL - Column stability is not acceptable";

            // based on html obtained from the strength verification and "member analysis and design" calc
            string teddsCalcHtml = $"""
                    <div style="font-size:12pt;font-family:Arial, sans-serif;">
                        <h5 style="text-align:left;font-family:Arial, sans-serif;font-size:9pt;margin:5.3px 0 0 23.8px;">Check columns subjected to either bending or combined bending and compression - Section 6.3.2</h5>
                        <div style="display:flex; flex-flow: column nowrap;">
                            <div style="display:flex; flex-flow: row nowrap;text-align:left;font-family:Arial, sans-serif;font-size:9pt">
                                <p style="flex: 1 1 0;">Effective length for y-axis bending</p>
                                <p style="flex: 1 1 0;">L<sub>e,y</sub> = 0.9 x {Math.Round(length * 1000)} mm = {Math.Round(effectiveLength * 1000)} mm</p>
                            </div>
                            <div style="display:flex; flex-flow: row nowrap;text-align:left;font-family:Arial, sans-serif;font-size:9pt">
                                <p style="flex: 1 1 0;">Slenderness ratio</p>
                                <p style="flex: 1 1 0;">λ<sub>y</sub> = L<sub>e,y</sub> / i<sub>y</sub> = {Math.Round(slendernessMajor * 1000) / 1000}</p>
                            </div>
                            <div style="display:flex; flex-flow: row nowrap;text-align:left;font-family:Arial, sans-serif;font-size:9pt">
                                <p style="flex: 1 1 0;">Relative slenderness ratio - exp 6.21</p>
                                <p style="flex: 1 1 0;">λ<sub>rel,y</sub> = λ<sub>y</sub> / π x √(f<sub>c,0,k</sub> / E<sub>0.05</sub>) = {Math.Round(relativeSlendernessMajor * 1000) / 1000}</p>
                            </div>
                            <div style="display:flex; flex-flow: row nowrap;text-align:left;font-family:Arial, sans-serif;font-size:9pt">
                                <p style="flex: 1 1 0;">Effective length for z-axis bending</p>
                                <p style="flex: 1 1 0;">L<sub>e,y</sub> = 0.9 x {Math.Round(length * 1000)} mm = {Math.Round(effectiveLength * 1000)} mm</p>
                            </div>
                            <div style="display:flex; flex-flow: row nowrap;text-align:left;font-family:Arial, sans-serif;font-size:9pt">
                                <p style="flex: 1 1 0;">Slenderness ratio</p>
                                <p style="flex: 1 1 0;">λ<sub>z</sub> = L<sub>e,z</sub> / i<sub>z</sub> = {Math.Round(slendernessMinor * 1000) / 1000}</p>
                            </div>
                            <div style="display:flex; flex-flow: row nowrap;text-align:left;font-family:Arial, sans-serif;font-size:9pt">
                                <p style="flex: 1 1 0;">Relative slenderness ratio - exp 6.22</p>
                                <p style="flex: 1 1 0;">λ<sub>rel,z</sub> = λ<sub>z</sub> / π x √(f<sub>c,0,k</sub> / E<sub>0.05</sub>) = {Math.Round(relativeSlendernessMinor * 1000) / 1000}</p>
                            </div>
                            <p style="text-align:right; font-style:italic; font-size:9pt;"><strong><em>λ<sub>rel,y</sub> > 0.3, column stability check is required</em></strong></p>
                            <div style="display:flex; flex-flow: row nowrap;text-align:left;font-family:Arial, sans-serif;font-size:9pt">
                                <p style="flex: 1 1 0;">Slenderness factor</p>
                                <p style="flex: 1 1 0;">β<sub>c</sub> = {beta_c}</p>
                            </div>
                            <div style="display:flex; flex-flow: row nowrap;text-align:left;font-family:Arial, sans-serif;font-size:9pt">
                                <p style="flex: 1 1 0;">Instability factors - exp 6.25, 6.26, 6.27, & 6.28</p>
                                <p style="flex: 1 1 0;">k<sub>y</sub> = 0.5 x (1 + β<sub>c</sub> x (λ<sub>rel,y</sub> - 0.3) + λ<sub>rel,y</sub><sup>2</sup>) = {Math.Round(k_y * 1000) / 1000}</p>
                            </div>
                            <div style="display:flex; flex-flow: row nowrap;text-align:left;font-family:Arial, sans-serif;font-size:9pt">
                                <p style="flex: 1 1 0;"> </p>
                                <p style="flex: 1 1 0;">k<sub>z</sub> = 0.5 x (1 + β<sub>c</sub> x (λ<sub>rel,z</sub> - 0.3) + λ<sub>rel,z</sub><sup>2</sup>) = {Math.Round(k_z * 1000) / 1000}</p>
                            </div>
                            <div style="display:flex; flex-flow: row nowrap;text-align:left;font-family:Arial, sans-serif;font-size:9pt">
                                <p style="flex: 1 1 0;"> </p>
                                <p style="flex: 1 1 0;">k<sub>c,y</sub> = 1 / (k<sub>y</sub> + √(k<sub>y</sub><sup>2</sup> - λ<sub>rel,y</sub><sup>2</sup>)) = {Math.Round(k_cy * 1000) / 1000}</p>
                            </div>
                            <div style="display:flex; flex-flow: row nowrap;text-align:left;font-family:Arial, sans-serif;font-size:9pt">
                                <p style="flex: 1 1 0;"> </p>
                                <p style="flex: 1 1 0;">k<sub>c,z</sub> = 1 / (k<sub>z</sub> + √(k<sub>z</sub><sup>2</sup> - λ<sub>rel,z</sub><sup>2</sup>)) = {Math.Round(k_cz * 1000) / 1000}</p>
                            </div>
                            <div style="display:flex; flex-flow: row nowrap;text-align:left;font-family:Arial, sans-serif;font-size:9pt">
                                <p style="flex: 1 1 0;">Column stability checks - exp 6.23 & 6.24</p>
                                <p style="flex: 1 1 0;">σ<sub>c,0,d</sub> / (k<sub>c,y</sub> x f<sub>c,0,d</sub>) + σ<sub>m,y,d</sub> / f<sub>m,y,d</sub> + k<sub>m</sub> x σ<sub>m,z,d</sub> / f<sub>m,z,d</sub> = {Math.Round(stabilityCheckMajor * 1000) / 1000}</p>
                            </div>
                            <div style="display:flex; flex-flow: row nowrap;text-align:left;font-family:Arial, sans-serif;font-size:9pt">
                                <p style="flex: 1 1 0;"> </p>
                                <p style="flex: 1 1 0;">σ<sub>c,0,d</sub> / (k<sub>c,z</sub> x f<sub>c,0,d</sub>) + k<sub>m</sub> x σ<sub>m,y,d</sub> / f<sub>m,y,d</sub> + σ<sub>m,z,d</sub> / f<sub>m,z,d</sub> = {Math.Round(stabilityCheckMinor * 1000) / 1000}</p>
                            </div>
                            <p style="text-align:right; font-style:italic; font-size:9pt;"><strong><em>{designMessage}</em></strong></p>
                        </div>
                    </div>
                """;
            teddsCalcHtml = teddsCalcHtml.ReplaceLineEndings("");

            return new StabilityResult { Result = result, StabilityUtil = stabilityCheck, StabilityHtml = teddsCalcHtml, StabilityMessage = designMessage };
        }
        #endregion

    }
}