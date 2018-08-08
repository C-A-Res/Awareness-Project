using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Psi;
using Microsoft.Psi.Components;

namespace NU.Kiosk.Speech
{
    public class SpeechMerger : ConsumerProducer<string, string>
    {
        private readonly Pipeline pipeline;

        public SpeechMerger(Pipeline pipeline) : base(pipeline)
        {
            this.pipeline = pipeline;

            this.OtherIn = pipeline.CreateReceiver<string>(this, ReceiveOtherInput, nameof(this.OtherIn));
        }

        public Receiver<string> OtherIn { get; private set; }

        private void ReceiveOtherInput(string arg1, Envelope arg2)
        {
            Out.Post(arg1, arg2.OriginatingTime);
        }

        protected override void Receive(string message, Envelope e)
        {
            Out.Post(message, e.OriginatingTime);
        }


        static void Main(string[] args)
        {
            using (Pipeline pipeline = Pipeline.Create())
            {
                var merger = new SpeechMerger(pipeline);
                var timer = new Timer<string>(pipeline, 1100, TestGen);
                var timer2 = new Timer<string>(pipeline, 2100, TestGen);

                timer.PipeTo(merger);
                timer2.PipeTo(merger.OtherIn);
                merger.Do(x => Console.WriteLine(x));

                pipeline.RunAsync();

                Console.WriteLine("Press any key to exit...");
                Console.ReadKey(true);
            }
        }

        private static int c = 0;

        private static string TestGen(DateTime arg1, TimeSpan arg2)
        {
            return "test" + c++;
        }
    }
}
