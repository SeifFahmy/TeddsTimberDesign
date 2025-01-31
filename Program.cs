
using System;
using System.Collections.Generic;
using Newtonsoft.Json;


namespace TeddsTimberDesign
{
    class Program
    {
        static string Main(string[] args)
        {
            if (args.Length != 2)
            {
                return "invalid number of arguments passed";
            }

            string parentWindow = args[0];
            string jsonData = args[1];

            TeddsApplication.SetUpTeddsWindow(parentWindow);
            TeddsApplication.ShowInitialWindow();

            var parsedJson = JsonConvert.DeserializeObject<List<MemberData>>(jsonData);

            var results = TeddsApplication.DesignMembers(parsedJson);
            return JsonConvert.SerializeObject(results);
        }

    }

    public class MemberData
    {
        public int Id { get; set; }
        public double MomentMajor { get; set; }
        public double MomentMinor { get; set; }
        public double ShearMajor { get; set; }
        public double ShearMinor { get; set; }
        public double Axial { get; set; }
        public double Length { get; set; }
    }
}