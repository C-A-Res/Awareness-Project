using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Psi.Samples.SpeechSample
{
    using System;
    using System.IO;
    using System.Linq;
    using Microsoft.Psi.Audio;
    using Microsoft.Psi.Data;
    using Microsoft.Psi.Speech;
    using NU.Kqml;

    public static class ProgramTest
    {
        public static void Main(string[] args)
        {
            using (var p = Pipeline.Create())
            {
                //var myConsumer = new MyConsumer(p);
                var myConsumer = new SocketStringConsumer(p, "localhost", 9000, 8099);
                var s = Generators.Repeat(p, "Test", 10);
                s.PipeTo(myConsumer.In);
                p.Run();
                Console.ReadKey(true);
            }
        }


    }

    public class MyConsumer : IConsumer<string>

    {

        public MyConsumer(Pipeline pipeline)
        {
            this.In = pipeline.CreateReceiver<string>(this, ReceiveMethod, nameof(this.In));
        }
        
        public Receiver<string> In { get; private set; }
        
        private void ReceiveMethod(string message, Envelope envelope)
        {
            // this will be called for every message on the input stream. For now, I’m just printing the message.
            Console.WriteLine(message);
        }

    }
}
