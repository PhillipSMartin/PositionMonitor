using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PositionMonitorHost
{
    class Program
    {
        static void Main(string[] args)
        {
            Host host = null;

            try
            {
                host = new Host();
                host.Run();

                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());

                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
            }
            finally
            {
                if (host != null)
                {
                    host.Dispose();
                    host = null;
                }
            }
        }
    }
}
