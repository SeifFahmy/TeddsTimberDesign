
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
using Newtonsoft.Json;


namespace TeddsTimberDesign
{
    class Program
    {
        static void Main(string[] args)
        {
            string parentWindow = "Code";
            TeddsApplication.SetUpTeddsWindow(parentWindow);
            TeddsApplication.ShowInitialWindow();

            // simulate data from electron
            string jsonData = @"[
                {
                    ""id"": 1,
                    ""moment"": 123,
                    ""length"": 3
                },
                {
                    ""id"": 2,
                    ""moment"": 50,
                    ""length"": 5
                },
            ]";
            var parsedJson = JsonConvert.DeserializeObject<List<MemberData>>(jsonData);

            var results = TeddsApplication.DesignMember(parsedJson);
            var resultsJson = JsonConvert.SerializeObject(results);

            Console.WriteLine(resultsJson);
        }

    }

    public class MemberData
    {
        public string Id { get; set; }
        public int Moment { get; set; }
        public bool Length { get; set; }
    }
}