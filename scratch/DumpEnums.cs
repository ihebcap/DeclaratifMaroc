using System;
using System.Reflection;

class Program
{
    static void Main()
    {
        try
        {
            var asm = Assembly.LoadFrom(@"D:\_vibe\GRF\GRFWinform\Tresorerie.Core.dll");
            foreach (var type in asm.GetTypes())
            {
                if (type.IsEnum && (type.Name.Contains("Domaine") || type.Name.Contains("Impaye") || type.Name.Contains("Compta") || type.Name.Contains("Paiement") || type.Name.Contains("Nature")))
                {
                    Console.WriteLine($"\nEnum: {type.FullName}");
                    foreach (var name in Enum.GetNames(type))
                    {
                        var val = Convert.ToInt32(Enum.Parse(type, name));
                        Console.WriteLine($"  {name} = {val}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
}
