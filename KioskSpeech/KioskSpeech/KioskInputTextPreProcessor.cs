using Microsoft.Psi;
using Microsoft.Psi.Audio;
using Microsoft.Psi.Components;
using Microsoft.Psi.Speech;
using NU.Kiosk.Speech;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NU.Kqml
{
    public class KioskInputTextPreProcessor : ConsumerProducer<IStreamingSpeechRecognitionResult, Utterance>
    {
        public static bool isUsingIsAccepting = false;
        private SystemSpeechRecognizer recognizer;
        private int ReloadMessageIDCurrent = 0;
        private static Regex quote_s = new Regex("[ ][']s");
        private static Regex course_num1 = new Regex(@"(\d)\s+(\d)0\s+(\d)");
        private static Regex course_num2 = new Regex(@"(\d)\s+(\d+)");

        public KioskInputTextPreProcessor(Pipeline pipeline, SystemSpeechRecognizer rec)
            : base(pipeline)
        {
            this.recognizer = rec;

            this.UiInput = pipeline.CreateReceiver<string>(this, ReceiveUiInput, nameof(this.UiInput));
        }

        public Receiver<string> UiInput { get; private set; }

        private void ReceiveUiInput(string arg1, Envelope arg2)
        {
            handleInput(arg1, 1.0, "ui", arg2);
        }

        protected override void Receive(IStreamingSpeechRecognitionResult result, Envelope e)
        {
            string message = quote_s.Replace(result.Text, "'s");
            double confidence = result.Confidence.Value;
            handleInput(message, confidence, "speech", e);
        }

        private void handleInput(string message, double confidence, string source, Envelope e) {
            Console.WriteLine($"[KioskInputTextPreProcessor] Received \"{message}\" with confidence {confidence}; ");// isAccepting {isAccepting}.");
            switch (message)
            {
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
                    // Filter out a few things
                    Console.WriteLine($"[KioskInputTextPreProcessor] Discarding message: {message}");
                    break;
                case "Reload grammars":
                    reloadGrammars();
                    break;
                default:
                    // fix course numbers
                    var x = course_num1.Replace(message, m => string.Format("{0}{1}{2}", m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value));
                    var updated_text = course_num2.Replace(x, m => string.Format("{0}{1}", m.Groups[1].Value, m.Groups[2].Value));
                    // finally, post
                    this.Out.Post(new Utterance(updated_text, confidence, source), e.Time);
                    break;
            }
        }

        private void reloadGrammars()
        {
            Console.WriteLine($"[KioskInputTextPreProcessor] Reloading grammar.");
            var gw = new Kiosk.AllXMLGrammarWriter(@"Resources\BaseGrammar.grxml");
            gw.ReadFileAndConvert();
            string updatedGrammar = gw.GetResultString();

            DateTime post_time = new DateTime();

            Message<System.Collections.Generic.IEnumerable<String>> updateRequest =
                new Message<System.Collections.Generic.IEnumerable<String>>(
                    new String[] {
                        updatedGrammar
                    }, post_time, post_time, 9876, ReloadMessageIDCurrent++);
            recognizer.SetGrammars(updateRequest);
            gw.WriteToFile();
        }
    }
}
