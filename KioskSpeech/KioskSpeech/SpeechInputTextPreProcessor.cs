using Microsoft.Psi;
using Microsoft.Psi.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NU.Kiosk.Speech
{
    class SpeechInputTextPreProcessor : ConsumerProducer<string, string>
    {
        private bool isAccepting;

        public SpeechInputTextPreProcessor(Pipeline pipeline)
            : base(pipeline)
        {
            // not much needs to be done
            this.isAccepting = true;
        }

        protected override void Receive(string message, Envelope e)
        {
            Console.WriteLine($"[SpeechInputTextPreProcessor] Received {message}; isAccepting {isAccepting}.");
            if (isAccepting)
            {
                isAccepting = false;
                switch (message)
                {
                    case "":
                    case "Hello":
                    case "Greetings":
                    case "Good morning":
                    case "Sup":
                    case "okay":
                    case "hm":
                    case "um":
                    case "ah":
                    case "cool":
                    case "huh?":
                    case "wow!":
                    case "huck you":
                        Console.WriteLine($"[SpeechInputTextPreProcessor] Discarding message: {message}");
                        isAccepting = true;
                        break;
                    case "Reload grammars":
                        Console.WriteLine($"[SpeechInputTextPreProcessor] Command handle not implemented: {message}");
                        isAccepting = true;
                        break;
                    default:
                        this.Out.Post(message, DateTime.Now);
                        break;
                }
            }
        }

        public void setAccepting()
        {
            this.isAccepting = true;
        }
    }
}
