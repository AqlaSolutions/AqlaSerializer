using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace RevisionGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                int year = int.Parse(args[0], CultureInfo.InvariantCulture);
                int month = int.Parse(args[1], CultureInfo.InvariantCulture);
                TimeSpan diff = (DateTime.UtcNow - new DateTime(year, month, 1, 1, 1, 1, DateTimeKind.Utc));
                Console.WriteLine(((int)(diff.TotalDays * 2f)).ToString(CultureInfo.InvariantCulture));
                Console.ReadLine();
            }
            catch
            {
                Console.WriteLine("Specify project start date: yyyy mm");
                Environment.Exit(-1);
            }
        }
    }
}
