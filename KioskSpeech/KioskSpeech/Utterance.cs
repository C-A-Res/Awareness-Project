using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NU.Kiosk.Speech
{
    public enum StringResultSource
    {
        ui,
        speech
    }

    public class Utterance
    {
        public Utterance(string text, double confidence, StringResultSource source)
        {
            this.Text = text;
            this.Confidence = confidence;
            this.Source = source;
        }

        public string Text { get; private set; }
        public double Confidence { get; private set; }
        public StringResultSource Source { get; private set; }
    }
}
