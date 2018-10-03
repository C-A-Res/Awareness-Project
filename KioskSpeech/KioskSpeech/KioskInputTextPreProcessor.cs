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
using NU.Kiosk.SharedObject;
using log4net;
using System.Reflection;

namespace NU.Kqml
{
    public class KioskInputTextPreProcessor : ConsumerProducer<IStreamingSpeechRecognitionResult, Utterance>
    {
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static bool isUsingIsAccepting = false;
        private GrammarRecognizerWrapper recognizer;
        private int ReloadMessageIDCurrent = 0;
        private static Regex quote_s = new Regex("[ ][']s");
        private static Regex space_qmark = new Regex("[ ][?]");
        private static Regex course_num1 = new Regex(@"(\d)\s+(\d)0\s+(\d)");
        private static Regex course_num2 = new Regex(@"(\d)\s+(\d+)");

        public KioskInputTextPreProcessor(Pipeline pipeline, GrammarRecognizerWrapper rec)
            : base(pipeline)
        {
            this.recognizer = rec;

            this.UiInput = pipeline.CreateReceiver<string>(this, ReceiveUiInput, nameof(this.UiInput));
        }

        public Receiver<string> UiInput { get; private set; }

        private void ReceiveUiInput(string arg1, Envelope arg2)
        {
            _log.Info($"Received UI Text input: \"{arg1}\"");
            handleInput(arg1, 1.0, StringResultSource.ui, arg2);
        }

        protected override void Receive(IStreamingSpeechRecognitionResult result, Envelope e)
        {
            string message = quote_s.Replace(result.Text, "'s");
            var lower = message.ToLower();
            if (lower.StartsWith("where") || lower.StartsWith("what") || lower.StartsWith("how") 
                || lower.StartsWith("who") || lower.StartsWith("is") || lower.StartsWith("are")
                || lower.StartsWith("when") || lower.StartsWith("will"))
            {
                message += "?";
            } else if (lower.StartsWith("show") || lower.StartsWith("tell"))
            {
                message += ".";
            }
            double confidence = result.Confidence.Value;
            _log.Info($"Received Speech Input \"{message}\" with confidence {confidence}; ");
            handleInput(message, confidence, StringResultSource.speech, e);
        }

        private void handleInput(string message, double confidence, StringResultSource source, Envelope e) {
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
                    _log.Info($"Discarding message: {message}");
                    break;
                case "Reload grammars":
                    recognizer.ReloadGrammar();
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
    }
}
