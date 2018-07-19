using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Psi;

namespace NU.Kqml
{
    class KQMLTest
    {

        public static void Main(string[] args)
        {
            using (Pipeline pipeline = Pipeline.Create())
            {
                string facilitatorIP = "165.124.181.100";
                int facilitatorPort = 9000;
                int localPort = 8090;

                if (args.Length > 0)
                {
                    facilitatorIP = args[0];
                    facilitatorPort = int.Parse(args[1]);
                    localPort = int.Parse(args[2]);
                }
                var kqmlComp = new SocketStringConsumer(pipeline, facilitatorIP, facilitatorPort, localPort);

                kqmlComp.Out.Do(x => Console.WriteLine(x));

                // Run the pipeline
                pipeline.RunAsync();

                Console.WriteLine("Press any key to exit...");
                Console.ReadKey(true);
            }

        }
    }

    


}
