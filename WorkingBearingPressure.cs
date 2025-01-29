
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
    class WorkingBearingPressure
    {
        static void Code()
        {
            //Create calculator and initialize for setting up input variables only
            Calculator calculator = new Calculator();

            calculator.Initialize();

            //Set all required input variables
            calculator.Functions.SetVar("Lx", 2000, "mm");
            calculator.Functions.SetVar("Ly", 2500, "mm");
            calculator.Functions.SetVar("Pz", 150, "kN");
            calculator.Functions.SetVar("ex", 600, "mm");
            calculator.Functions.SetVar("ey", 550, "mm");

            //If all the input required has already been specified you can hide the user interface
            //of the calculation using a special variable which is supported automatically by all
            //calculations "_CalcUI"
            //Uncomment the following line to disable the calculations user interface
            calculator.Functions.SetVar("_CalcUI", 0);

            //Get variables as XML string
            string variables = calculator.GetVariables();

            //Initialize for a second time but this time with calculation to start and variables
            string calcFileName = "$(SysLbrDir)Bearing pressure.lbr";
            string calcItemName = "Bearing pressures";
            calculator.InitializeCalc(calcFileName, calcItemName, variables);


            //Query the calculation results
            double qmax = calculator.Functions.GetVar("qmax").ToDouble("kN/m^(2)");
            double bearing = calculator.Functions.GetVar("BearingPercentage").ToDouble();
            System.Console.WriteLine(qmax);
            System.Console.WriteLine(bearing);
        }
    }
}