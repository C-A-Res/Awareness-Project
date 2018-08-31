using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NU.Kiosk.Speech
{
    public class Utterance
    {
        public Utterance(string text, double confidence, string source)
        {
            this.Text = text;
            this.Confidence = confidence;
            this.Source = source;
        }

        public string Text { get; private set; }
        public double Confidence { get; private set; }
        public string Source { get; private set; }
    }
}
