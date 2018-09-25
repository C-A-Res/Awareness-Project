using System;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Synthesis;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Psi;
using Microsoft.Psi.Components;
using NU.Kiosk.SharedObject;

namespace NU.Kiosk.Speech
{
    public class Responder
    {
        private readonly Pipeline pipeline;

        private int repeatCount = 0;

        public Responder(Pipeline pipeline)
        {
            this.pipeline = pipeline;

            // Dialog connections
            this.UserInput = pipeline.CreateReceiver<Utterance>(this, ReceiveUserInput, nameof(this.UserInput));
            this.CompResponse = pipeline.CreateEmitter<string>(this, nameof(this.CompResponse));

            // KQML connections
            this.KQMLResponse = pipeline.CreateReceiver<NU.Kiosk.SharedObject.Action>(this, ReceiveKQMLResponse, nameof(this.KQMLResponse));
            this.KQMLRequest = pipeline.CreateEmitter<string>(this, nameof(this.KQMLRequest));

            // UI connection
            this.ActionResponse = pipeline.CreateEmitter<Action>(this, nameof(this.ActionResponse));
        }

        // Dialog
        public Receiver<Utterance> UserInput { get; private set; }
        public Emitter<string> CompResponse { get; private set; }

        // UI
        public Emitter<Action> ActionResponse { get; private set; } 

        // KQML
        public Receiver<NU.Kiosk.SharedObject.Action> KQMLResponse { get; private set; }
        public Emitter<string> KQMLRequest { get; private set; }

        private void ReceiveUserInput(Utterance arg1, Envelope arg2)
        {
            if (!generateAutoResponse(arg1, arg2))
            {
                KQMLRequest.Post(arg1.Text, arg2.OriginatingTime);
            }
        }

        private void ReceiveKQMLResponse(NU.Kiosk.SharedObject.Action arg1, Envelope arg2)
        {
            CompResponse.Post((string)arg1.Args[0], DateTime.Now);
        }

        private bool generateAutoResponse(Utterance arg1, Envelope arg2)
        {
            var text = arg1.Text;
            var confidence = arg1.Confidence;
            if (confidence < 0.3)
            {
                if (repeatCount <= 1)
                {
                    CompResponse.Post("Could you please repeat that?", DateTime.Now);
                }
                else if (repeatCount <= 3)
                {
                    CompResponse.Post("Please try to rephrase.", DateTime.Now);
                }
                else
                {
                    CompResponse.Post("Please try again.", DateTime.Now);
                }
                repeatCount++;
                return true;
            }
            else
            {
                repeatCount = 0;
                switch (text)
                {
                    case "Hi":
                    case "Hello":
                    case "Greetings":
                    case "Good morning":
                    case "Sup":
                        CompResponse.Post("Hello", DateTime.Now);
                        return true;
                    case "What can you do?":
                    case "What do you do?":
                    case "How can you help?":
                        generateHelpResponse(arg2);
                        return true;
                    case "What time is it?":
                    case "What's the time?":
                        var time = DateTime.Now.ToString("h:mm tt");
                        CompResponse.Post($"It is {time}", DateTime.Now);
                        return true;
                    case "":
                    case "okay":
                    case "hm":
                    case "um":
                    case "ah":
                    case "cool":
                    case "huh?":
                    case "wow!":
                    case "Huck you":
                    case "bye":
                    case "bye bye":
                        Console.WriteLine($"[KioskInputTextPreProcessor] Discarding message: {text}");
                        return true;
                }
            }
            return false;
        }

        private void generateHelpResponse(Envelope arg2)
        {
            CompResponse.Post("I can answer questions about where someone's office is and how to contact a professor.", arg2.OriginatingTime);
        }

        private void actionInterpreter(NU.Kiosk.SharedObject.Action a, DateTime dt)
        {
            switch (a.Name)
            {
                case "psikiSayText":
                    CompResponse.Post(a.Args[0].ToString(), dt);
                    ActionResponse.Post(a, dt);
                    break;
                case "psikiDisplayMap":
                case "psikiDisplayUrl":
                    ActionResponse.Post(a, dt);
                    break;
                default:
                    Console.WriteLine("Unrecognized action: " + a.Name);
                    break;
            }
        }
    }
}