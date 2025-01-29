
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Tekla.Structural.InteropAssemblies.TeddsCalc;


namespace TeddsTimberDesign
{
    class TimberDesign
    {
        static void Code()
        {

            //Create calculator and initialize for setting up input variables only
            Calculator calculator = new Calculator();
            calculator.Initialize();

            //Set all required input variables
            string calcFileName = "$(SysLbrDir)Timber beam design-BS5268-si-engb.lbr";
            string calcItemName = "Timber member design";

            // calculator.Functions.SetVar("M_{x}", 100, "kNm");
            // calculator.Functions.SetVar("M_{y}", 100, "kNm");
            // calculator.Functions.SetVar("P", 100, "kN"); // axial
            // calculator.Functions.SetVar("R", 100, "kN"); // reaction
            // calculator.Functions.SetVar("F", 100, "kN"); // shear
            // System.Console.WriteLine("forces");
            // calculator.Functions.SetVar("L_{ex}", 2, "m"); // effective length over x axis
            // calculator.Functions.SetVar("L_{ey}", 2, "m");  // effective length over y axis
            // System.Console.WriteLine("length");
            // calculator.Functions.SetVar("_tim.Type", "Glulam");
            // calculator.Functions.SetVar("b", 44, "mm"); // width of section (if section made of gluing multiple sections side by side)
            // calculator.Functions.SetVar("b_{b}", 44, "mm"); // total width of member
            // calculator.Functions.SetVar("h", 195, "mm"); // total depth of section
            // calculator.Functions.SetVar("N_{lam}", 3); // number of laminations
            // calculator.Functions.SetVar("N", 3); // number of sections (across width)
            // System.Console.WriteLine("section");
            // calculator.Functions.SetVar("Strength_Class", "Strength Class C24"); // timber strength grade
            // System.Console.WriteLine("strength");



            //If all the input required has already been specified you can hide the user interface
            //of the calculation using a special variable which is supported automatically by all
            //calculations "_CalcUI"
            //Uncomment the following line to disable the calculations user interface
            // calculator.Functions.SetVar("_CalcUI", 1);
            // calculator.Functions.Eval($"EvalCalcItem( \"{calcFileName}\", \"{calcItemName}\" )");

            //Get variables as XML string
            // string variables = calculator.GetVariables();

            //Initialize for a second time but this time with calculation to start and variables
            // calculator.InitializeCalc(calcFileName, calcItemName, variables);
            System.Console.WriteLine("2");
            // calculator.InitializeCalc(calcFileName, calcItemName, "");
            calculator.Functions.Eval($"EvalCalcItem( \"{calcFileName}\", \"{calcItemName}\" )");


            //Query the calculation results
            // string combinedCheck = calculator.Functions.GetVar("_tmp.CombinedResul").ToString();

            // System.Console.WriteLine(combinedCheck);
        }
    }
}
