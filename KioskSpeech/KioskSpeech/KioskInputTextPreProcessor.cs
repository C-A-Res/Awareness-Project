using Microsoft.Psi;
using Microsoft.Psi.Components;
using Microsoft.Psi.Speech;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NU.Kqml
{
    public class KioskInputTextPreProcessor : ConsumerProducer<IStreamingSpeechRecognitionResult, string>
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
            Console.WriteLine($"[KioskInputTextPreProcessor] Received {message} with confidence {confidence}; isAccepting {isAccepting}.");
            if (isAccepting && confidence > 0.3)
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
                        Console.WriteLine($"[KioskInputTextPreProcessor] Discarding message: {message}");
                        isAccepting = true;
                        break;
                    case "Reload grammars":
                        Console.WriteLine($"[KioskInputTextPreProcessor] Command handle not implemented: {message}");
                        isAccepting = true;
                        break;
                    default:
                        this.Out.Post(message, DateTime.Now);
                        Console.WriteLine($"[KioskInputTextPreProcessor] Starting timer.");
                        restartAcceptingInMs(10000);                        
                        break;
                }
            }
        }

        async Task delay(int ms)
        {
            await Task.Delay(ms);
        }

        private async void restartAcceptingInMs(int ms)
        {
            await delay(ms);
            if (this.isAccepting == false)
            {
                setAccepting();
                Console.WriteLine($"[KioskInputTextPreProcessor] Timer stopped; once again accepting.");
            }
            else {
                Console.WriteLine($"[KioskInputTextPreProcessor] Timer stopped; already accepting.");
            }
        }

        public void setAccepting()
        {
            this.isAccepting = true;
        }
    }
}
