using DNSTools;
using Microsoft.Psi;
using Microsoft.Psi.Components;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Speech.Synthesis;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Microsoft.Psi.Speech.SystemSpeechSynthesizer;

namespace NU.Kiosk.Speech
{
    public class DragonSpeechSynthesizer : IDisposable, IPipeUtilUsers
    {
        private string listener_pipe_name;
        private PipeListener listener;

        private string destination_pipe_name;
        private PipeSender sender;

        private DragonRecognizer recognizer;        
        DgnVoiceTxt dgnVoiceTxt;
        
        string postFixIdentifier;

        public DragonSpeechSynthesizer(DragonRecognizer rec)
        {
            listener_pipe_name = NU.Kiosk.Speech.Program.isDebug ? "dragon_processed_text_pipe" : "dragon_synthesizer_pipe";
            destination_pipe_name = NU.Kiosk.Speech.Program.isDebug ? "dragon_synthesizer_pipe" : "dragon_synthesizer_state_pipe";

            this.recognizer = rec;
            postFixIdentifier = DateTime.Now.ToLongTimeString();
            listener = new PipeListener(Speak, listener_pipe_name, this);
            sender = new PipeSender(destination_pipe_name);
        }

        public void Dispose()
        {
            Console.WriteLine("[DragonSpeechSynthesizer] Dispose");
            listener.Dispose();
            sender.Dispose();

            dgnVoiceTxt.Enabled = false;
            dgnVoiceTxt.UnRegister();
            dgnVoiceTxt = null;
        }

        public void ReconnectSenders()
        {
            sender.Reconnect();
        }

        private void speechHasStarted()
        {
            sender.Send("Start");
            recognizer.setNotAccepting();
        }

        private void speechIsDone()
        {
            Console.WriteLine("[DragonSpeechSynthesizer] Speak is done");
            recognizer.setAccepting();
            sender.Send("Done");
        }

        public void Initialize()
        {
            dgnVoiceTxt = new DgnVoiceTxt();
            dgnVoiceTxt.Register("Kiosk", "TTS"+ postFixIdentifier);
            dgnVoiceTxt.SpeakingStarted += speechHasStarted;
            dgnVoiceTxt.SpeakingDone += speechIsDone;
            dgnVoiceTxt.Enabled = true;

            new Task(() => listener.Initialize()).Start();
            new Task(() => sender.Initialize()).Start();
        }

        public void Speak(string utterance)
        {
            if (utterance != null && utterance.Length > 0)
            {
                dgnVoiceTxt.Speak(utterance);
            }
        }
    }
}
