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
    public class DragonInputTextPreProcessor : ConsumerProducer<String, Utterance>
    {
        private int ReloadMessageIDCurrent = 0;

        public DragonInputTextPreProcessor(Pipeline pipeline)
            : base(pipeline)
        {
            this.UiInput = pipeline.CreateReceiver<string>(this, ReceiveUiInput, nameof(this.UiInput));
        }

        public Receiver<string> UiInput { get; private set; }

        private void ReceiveUiInput(string arg1, Envelope arg2)
        {
            handleInput(arg1, 1.0, StringResultSource.ui, arg2);
        }

        protected override void Receive(string result, Envelope e)
        {
            handleInput(result, 1.0, StringResultSource.speech, e);
        }

        private void handleInput(string message, double confidence, StringResultSource source, Envelope e) {
            string updated_text = message;
            this.Out.Post(new Utterance(updated_text, confidence, source), e.Time);
        }

        private void reloadGrammars()
        {
            Console.WriteLine($"[DragonInputTextPreProcessor] Grammar is not supported.");
        }
    }
}
