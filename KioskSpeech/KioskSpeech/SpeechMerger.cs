using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Psi;
using Microsoft.Psi.Components;

namespace NU.Kiosk.Speech
{
    public class SpeechMerger : ConsumerProducer<string, string>, IStartable
    {
        private readonly Pipeline pipeline;

        public SpeechMerger(Pipeline pipeline) : base(pipeline)
        {
            this.pipeline = pipeline;

            this.OtherIn = pipeline.CreateReceiver<string>(this, ReceiveOtherInput, nameof(this.OtherIn));
            this.LastOut = pipeline.CreateReceiver<string>(this, ReceiveLastOutput, nameof(this.LastOut));

            this.InitTimer();
        }

        public Receiver<string> OtherIn { get; private set; }
        public Receiver<string> LastOut { get; private set; }

        private void ReceiveOtherInput(string arg1, Envelope arg2)
        {
            Out.Post(arg1, arg2.OriginatingTime);
            setAccepting();
        }

        private void ReceiveLastOutput(string arg1, Envelope arg2)
        {
            setNotAccepting();
        }

        protected override void Receive(string message, Envelope e)
        {
            Out.Post(message, e.OriginatingTime);
            setAccepting();
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

        public void Start(Action onCompleted, ReplayDescriptor descriptor)
        {
            Out.Post("System starting. Please stand by.", DateTime.Now);
        }

        public void Stop()
        {
            //throw new NotImplementedException();
        }












        bool isAccepting = true;
        System.Timers.Timer timer = new System.Timers.Timer(1);

        private void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            StopTimer();
            Console.WriteLine("Time's up");
            Out.Post("Sorry, does not compute.", DateTime.Now);
        }

        private void InitTimer()
        {
            timer = new System.Timers.Timer(10000);
            timer.Elapsed += this.OnTimedEvent;
        }

        private void StartTimer()
        {
            timer.Enabled = true;
            Console.WriteLine("[Merger: StartTimer] Timer Enabled.");
        }

        private void StopTimer()
        {
            timer.Enabled = false;
            Console.WriteLine("[Merger: StopTimer] Timer Disabled");
        }

        public void setAccepting()
        {
            Console.WriteLine("[Merger: setAccepting] Yes! Accepting!");
            isAccepting = true;
            StopTimer();
        }

        public void setNotAccepting()
        {
            Console.WriteLine("[Merger: setAccepting] No! Not accepting!");
            isAccepting = false;
            StartTimer();
        }

        public void Start(System.Action onCompleted, ReplayDescriptor descriptor)
        {
            throw new NotImplementedException();
        }
    }
}
