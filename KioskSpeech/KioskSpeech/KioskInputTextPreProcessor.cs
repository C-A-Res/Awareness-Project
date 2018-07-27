using Microsoft.Psi;
using Microsoft.Psi.Components;
using Microsoft.Psi.Speech;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NU.Kiosk.Speech
{
    class KioskInputTextPreProcessor : ConsumerProducer<IStreamingSpeechRecognitionResult, string>
    {
        private bool isAccepting;

        public KioskInputTextPreProcessor(Pipeline pipeline)
            : base(pipeline)
        {
            // not much needs to be done
            this.isAccepting = true;
        }

        protected override void Receive(IStreamingSpeechRecognitionResult result, Envelope e)
        {
            string message = result.Text;
            double confidence = result.Confidence.Value;
            Console.WriteLine($"[SpeechInputTextPreProcessor] Received {message} with confidence {confidence}; isAccepting {isAccepting}.");
            if (isAccepting && confidence > 0.6)
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
