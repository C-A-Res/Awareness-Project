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
                msg = buildJsonResponse(kioskUI.getUtterances(), kioskUI.getState(), kioskUI.getDebug());
            } else { 
                List<(string, string)> utts = new List<(string,string)>();
                utts.Add(("other", "Where does Prof Forbus work?"));
                utts.Add(("kiosk", "His office is somewehre"));
                string state = "listening";
                msg = buildJsonResponse(utts, state, "false");
            }

            Send(msg);
        }

        private string buildJsonResponse(List<(string,string)> utts, string state, string debug)
        {
            List<Dictionary<string, string>> speechForJson = new List<Dictionary<string, string>>();
            foreach (var n in utts)
            {
                Dictionary<string, string> dict = new Dictionary<string, string> { { "user", n.Item1 }, { "content", n.Item2 } };
                speechForJson.Add(dict);
            }
            Dictionary<string, string> avatarForJson = new Dictionary<string, string> { { "state", state } };
            Dictionary<string, object> forJson = new Dictionary<string, object> { { "avatar", avatarForJson }, { "speech", speechForJson }, { "debug", debug } };
            return JsonConvert.SerializeObject(forJson);
        }

        
    }

    public class KioskUI : IStartable
    {

        private readonly Pipeline pipeline;

        private WebSocketServer wssv;

        private List<(string,string)> utterances = new List<(string,string)>();

        private bool faceDetected = false;
        private bool thinking = false;
        private bool speaking = false;

        public KioskUI(Pipeline pipeline)
        {
            this.pipeline = pipeline;

            this.Updating = pipeline.CreateEmitter<bool>(this, nameof(this.Updating));

            this.UserInput = pipeline.CreateReceiver<string>(this, ReceiveUserInput, nameof(this.UserInput));
            this.CompResponse = pipeline.CreateReceiver<string>(this, ReceiveCompResponse, nameof(this.CompResponse));
            this.FaceDetected = pipeline.CreateReceiver<bool>(this, ReceiveFaceDetected, nameof(this.FaceDetected));
        }

        public Receiver<string> UserInput { get; private set; }
        public Receiver<string> CompResponse { get; private set; }
        public Receiver<bool> FaceDetected { get; private set; }

        /// <summary>
        /// Not used yet
        /// </summary>
        public Emitter<bool> Updating { get; private set; }

        private void ReceiveUserInput(string message, Envelope e)
        {
            this.utterances.Add(("other", message));
            thinking = true;
        }

        private void ReceiveCompResponse(string message, Envelope e)
        {
            this.utterances.Add(("kiosk", message));
            thinking = false;
            speaking = true;
        }

        private void ReceiveFaceDetected(bool message, Envelope e)
        {
            this.faceDetected = message;
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

        public string getState()
        {
            if (speaking)
            {
                speaking = false;
                return "speaking";
            } else if (thinking)
            {
                return "thinking";
            } else if (faceDetected)
            {
                return "listening";
            } else
            {
                return "sleeping";
            }
        }

        public string getDebug()
        {
            return faceDetected ? "true"  : "false";
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
                    var timer = new Timer<string>(pipeline, 13100, TestGen);
                    var timer2 = new Timer<string>(pipeline, 12100, TestGen);
                    var timer3 = new Timer<bool>(pipeline, 17100, TestBool);

                    timer.Do(t => Console.WriteLine(t));                    
                    timer.PipeTo(ui.CompResponse);
                    timer2.PipeTo(ui.UserInput);
                    timer3.PipeTo(ui.FaceDetected);

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

        private static bool face = false;
        private static bool TestBool(DateTime arg1, TimeSpan arg2)
        {
            face = !face;
            return face;
        }
    }
}
