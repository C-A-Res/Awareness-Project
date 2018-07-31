using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Psi;
using Microsoft.Psi.Components;
using WebSocketSharp;
using WebSocketSharp.Server;
using Newtonsoft.Json;

namespace KioskUI
{
    public class KioskUIServer : WebSocketBehavior
    {
        private string nextMsg = "{\"user\" : \"companion\", \"content\" :  \"This is the first\"}";

        private KioskUI kioskUI;

        public KioskUIServer(KioskUI kioskUI)
        {
            this.kioskUI = kioskUI;
        }

        protected override void OnMessage (MessageEventArgs e)
        {
            var input = e.Data;

            string msg = "";

            if (this.kioskUI != null)
            {
                List<(string,string)> nextUtts = kioskUI.getUtterances();
                List<Dictionary<string, string>> forJson = new List<Dictionary<string, string>>();
                foreach (var n in nextUtts)
                {
                    Dictionary<string, string> dict = new Dictionary<string, string> { { "user", n.Item1 }, { "content", n.Item2 } };
                    forJson.Add(dict);
                }
                msg = JsonConvert.SerializeObject(forJson);
            } else
            {
                List<Dictionary<string, string>> forJson = new List<Dictionary<string, string>>();
                Dictionary<string, string> tdict = new Dictionary<string, string> { { "user", "other" }, { "content", "Where does Prof Forbus work?" } };
                forJson.Add(tdict);
                tdict = new Dictionary<string, string> { { "user", "kiosk" }, { "content", "His office is somewhere." } };
                forJson.Add(tdict);
                msg = JsonConvert.SerializeObject(forJson);
            }

            Send(msg);
        }

        
    }

    public class KioskUI : IStartable
    {

        private readonly Pipeline pipeline;

        private WebSocketServer wssv;

        private List<(string,string)> utterances = new List<(string,string)>();

        public KioskUI(Pipeline pipeline)
        {
            this.pipeline = pipeline;

            this.Updating = pipeline.CreateEmitter<bool>(this, nameof(this.Updating));

            this.UserInput = pipeline.CreateReceiver<string>(this, ReceiveUserInput, nameof(this.UserInput));
            this.CompResponse = pipeline.CreateReceiver<string>(this, ReceiveCompResponse, nameof(this.CompResponse));
        }

        public Receiver<string> UserInput { get; private set; }
        public Receiver<string> CompResponse { get; private set; }

        /// <summary>
        /// Not used yet
        /// </summary>
        public Emitter<bool> Updating { get; private set; }

        private void ReceiveUserInput(string message, Envelope e)
        {
            this.utterances.Add(("other", message));
        }

        private void ReceiveCompResponse(string message, Envelope e)
        {
            this.utterances.Add(("kiosk", message));
        }

        public void Start(Action onCompleted, ReplayDescriptor descriptor)
        {
            wssv = new WebSocketServer("ws://127.0.0.1:9791");
            wssv.AddWebSocketService<KioskUIServer>("/dialog", () => new KioskUIServer(this));
            wssv.Start();
        }

        public void Stop()
        {
            wssv.Stop();
        }

        public List<(string, string)> getUtterances()
        {
            List<(string, string)> copy = new List<(string, string)>(utterances);
            utterances.Clear();
            return copy;
        }
    }

    public class Program
    {
        static bool psi = true;

        static void Main(string[] args)
        {
            if (psi)
            {
                using (Pipeline pipeline = Pipeline.Create())
                {
                    var ui = new KioskUI(pipeline);
                    var timer = new Timer<string>(pipeline, 7100, TestGen);
                    timer.Do(t => Console.WriteLine(t));                    
                    timer.PipeTo(ui.CompResponse);

                    pipeline.RunAsync();

                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey(true);

                }
            } else
            {
                var wssv = new WebSocketServer("ws://127.0.0.1:9791");
                wssv.AddWebSocketService<KioskUIServer>("/dialog", () => new KioskUIServer(null));
                wssv.Start();
                Console.ReadKey(true);
                wssv.Stop();
            }
        }

        private static int c = 0;

        private static string TestGen(DateTime arg1, TimeSpan arg2)
        {
            return "test" + c++;
        }
    }
}
