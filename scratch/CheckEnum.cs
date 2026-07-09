using System;
using System.Reflection;
using System.Linq;

namespace CheckEnum {
    class Program {
        static void Main() {
            Assembly asm = Assembly.LoadFrom(@"D:\_vibe\GRF\GRFWinform\Tresorerie.Core.dll");
            var enums = asm.GetTypes().Where(t => t.IsEnum && (t.Name.Contains("Domaine") || t.Name.Contains("Compta") || t.Name.Contains("Impaye")));
            foreach (var t in enums) {
                Console.WriteLine("Enum: " + t.FullName);
                foreach (var v in Enum.GetValues(t)) {
                    Console.WriteLine(string.Format("  {0} = {1}", v, Convert.ToInt32(v)));
                }
            }
        }
    }
}
