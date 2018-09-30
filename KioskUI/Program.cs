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
using NU.Kiosk.SharedObject;

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
            string input = e.Data;
            if (input.Length > 0)
            {
                if (input.Equals(":wake"))
                {
                    Console.WriteLine("Received wake from UI");
                    kioskUI.Wake.Post(true, DateTime.Now);
                } else
                {
                    Console.WriteLine("Received from UI: " + input);
                    kioskUI.TouchInput.Post(input, DateTime.Now);
                }
            }

            string msg = "";

            if (this.kioskUI != null)
            {
                msg = buildJsonResponse(kioskUI.getUtterances(), kioskUI.getState(), kioskUI.getMapData(), kioskUI.getUrl(), kioskUI.getCalendar(), kioskUI.getDebug());
            } else { 
                List<(string, string)> utts = new List<(string,string)>();
                utts.Add(("other", "Where does Prof Forbus work?"));
                utts.Add(("kiosk", "His office is somewehre"));
                string state = "listening";
                msg = buildJsonResponse(utts, state,  ("",""), "", "", "false");
            }

            Send(msg);
        }

        private string buildJsonResponse(List<(string, string)> utts, string state, (string, string) mapdata, string url, string calendar, string debug)
        {
            var forJson = new List<Dictionary<string, object>>();

            foreach (var n in utts)
            {
                var speech = new Dictionary<string, string> { { "user", n.Item1 }, { "content", n.Item2 } };
                var command_speech = new Dictionary<string, object> { { "command", "displayText" }, { "args", speech } };
                forJson.Add(command_speech);
            }

            var command = new Dictionary<string, object> { { "command", "setAvatarState" }, { "args", state } };
            forJson.Add(command);

            if (url != "")
            {
                Dictionary<string, object> urlForJson = new Dictionary<string, object> { { "command", "displayUrl" }, {"args", url } };
                forJson.Add(urlForJson);
            }

            if (mapdata.Item1 != "")
            {
                var mapargs = new Dictionary<string, string> { { "name", mapdata.Item1 }, { "id", mapdata.Item2 } };
                var command_map = new Dictionary<string, object> { { "command", "displayMap" }, { "args", mapargs } };
                forJson.Add(command_map);
            }

            if (calendar != "")
            {
                Dictionary<string, object> calForJson = new Dictionary<string, object> { { "command", "displayCalendar" }, { "args", calendar } };
                forJson.Add(calForJson);
            }

            if (debug.Length > 0)
            {
                var command_debug = new Dictionary<string, object> { { "command", "debug" }, { "args", debug } };
                forJson.Add(command_debug);
            }

            return JsonConvert.SerializeObject(forJson);
        }
    }

    public class KioskUI : IStartable
    {

        private readonly Pipeline pipeline;

        private WebSocketServer wssv;

        private List<(string,string)> utterances = new List<(string,string)>();

        private bool isDetectingFace;
        private bool faceDetected = false;
        private bool thinking = false;
        private bool speaking = false;
        private string state = "sleeping";
        private string url = "";
        private string mapLabel = "";
        private string mapID = "";
        private string calendar = "";

        public KioskUI(Pipeline pipeline, bool isDetectingFace = true)
        {
            this.pipeline = pipeline;

            this.TouchInput = pipeline.CreateEmitter<string>(this, nameof(this.TouchInput));
            this.Wake = pipeline.CreateEmitter<bool>(this, nameof(this.Wake));

            this.UserInput = pipeline.CreateReceiver<string>(this, ReceiveUserInput, nameof(this.UserInput));
            this.FaceDetected = pipeline.CreateReceiver<bool>(this, ReceiveFaceDetected, nameof(this.FaceDetected));
            this.DialogStateChanged = pipeline.CreateReceiver<string>(this, ReceiveDialogStateChanged, nameof(this.DialogStateChanged));
            this.ActionCommand = pipeline.CreateReceiver<NU.Kiosk.SharedObject.Action>(this, ReceiveActionCommand, nameof(this.ActionCommand));
        }

        public Receiver<string> UserInput { get; private set; }
        public Receiver<string> CompResponse { get; private set; }
        public Receiver<bool> FaceDetected { get; private set; }
        public Receiver<string> DialogStateChanged { get; private set; }
        public Receiver<NU.Kiosk.SharedObject.Action> ActionCommand { get; private set; }

        /// <summary>
        /// Input from user via the touch screen screen on the UI
        /// </summary>
        public Emitter<string> TouchInput { get; private set; }

        public Emitter<bool> Wake { get; private set; }

        private void ReceiveUserInput(string message, Envelope e)
        {
            this.utterances.Add(("other", message));
            thinking = true;
        }

        private void ReceiveFaceDetected(bool message, Envelope e)
        {
            this.faceDetected = message;
        }

        private void ReceiveDialogStateChanged(string arg1, Envelope arg2)
        {
            state = arg1;
        }

        private void ReceiveCompResponse(string message, Envelope e)
        {
            this.utterances.Add(("kiosk", message));
            thinking = false;
            speaking = true;
        }

        private void ReceiveActionCommand(NU.Kiosk.SharedObject.Action arg1, Envelope arg2)
        {
            switch (arg1.Name)
            {
                case "psikiSayText":
                    utterances.Add(("kiosk", (string)arg1.Args[0]));
                    break;
                case "psikiShowMap":
                    mapLabel = (string)arg1.Args[0];
                    mapID = (string)arg1.Args[1];
                    break;
                case "psikiShowUrl":
                    url = (string)arg1.Args[0];
                    break;
                case "psikiShowCalendar":
                    calendar = (string)arg1.Args[0];
                    break;
                default:
                    Console.WriteLine("Invalid action received by UrlCommand" + arg1.Name);
                    break;
            }
            
        }

        public void Start(System.Action onCompleted, ReplayDescriptor descriptor)
        {
            wssv = new WebSocketServer("ws://127.0.0.1:9791");
            wssv.AddWebSocketService<KioskUIServer>("/dialog", () => new KioskUIServer(this));
            wssv.Start();
            Console.WriteLine("Websocket Server Ready");
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
            return state;
        }

        public string getUrl()
        {
            var retval = url;
            url = "";
            return retval;
        }

        public (string,string) getMapData()
        {
            var retval = (mapLabel, mapID);
            mapLabel = "";
            mapID = "";
            return retval;
        }

        public string getCalendar()
        {
            var retval = calendar;
            calendar = "";
            return retval;
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

                    //timer.Do(t => Console.WriteLine(t));                    
                    timer.PipeTo(ui.CompResponse);
                    timer2.PipeTo(ui.UserInput);
                    timer3.PipeTo(ui.FaceDetected);
                    ui.TouchInput.Do(x => Console.WriteLine(x));

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
