
using System;
using System.Collections.Generic;
using Newtonsoft.Json;


namespace TeddsTimberDesign
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                throw new Exception("invalid number of arguments passed");
            }

            string parentWindow = args[0];
            string jsonData = args[1];

            TeddsApplication.SetUpTeddsWindow(parentWindow);
            TeddsApplication.ShowInitialWindow();

            var parsedJson = JsonConvert.DeserializeObject<DesignData>(jsonData);

            var results = TeddsApplication.DesignMembers(parsedJson);
            var jsonResults = JsonConvert.SerializeObject(results);
            System.Console.WriteLine(jsonResults);
        }

    }

    public class DesignData
    {
        public RobotMaterialData RobotMaterialData { get; set; }
        public List<RobotMemberData> RobotMemberData { get; set; }
        public double BeamDeflectionLimitRatio { get; set; }
    }

    public class RobotMemberData
    {
        public int Id { get; set; }
        public double MomentMajor { get; set; }
        public double MomentMinor { get; set; }
        public double ShearMajor { get; set; }
        public double ShearMinor { get; set; }
        public double Axial { get; set; }
        public bool IsAxialMember { get; set; }
        public double Deflection { get; set; }
        public double Area { get; set; }
        public double SecondMomentOfArea { get; set; }
        public double Length { get; set; }
    }

    public class RobotMaterialData
    {
        public double RobotE { get; set; }
        public double RobotG { get; set; }
    }
}