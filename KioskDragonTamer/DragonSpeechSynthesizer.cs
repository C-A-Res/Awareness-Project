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
    public class DragonSpeechSynthesizer : IDisposable
    {
        private const string listener_pipe_name = "dragon_synthesizer_pipe";

        private const int NumThreads = 2;
        private NamedPipeServerStream pipeServer;
        private StreamString ss;
        private Thread listenerThread;
        private DragonRecognizer recognizer;
        
        DgnVoiceTxt dgnVoiceTxt;
        string postFixIdentifier;

        public DragonSpeechSynthesizer(DragonRecognizer rec)
        {
            this.recognizer = rec;
            postFixIdentifier = DateTime.Now.ToLongTimeString();
        }

        public void Dispose()
        {
            Console.WriteLine("[DragonSpeechSynthesizer] Dispose");
            listenerThread.Abort();

            dgnVoiceTxt.Enabled = false;
            if (pipeServer.CanWrite)
            {
                pipeServer.Disconnect();
            }
            pipeServer.Dispose();

            dgnVoiceTxt.UnRegister();
            dgnVoiceTxt = null;
        }

        private void speechHasStarted()
        {
            // do nothing. Or later send a message back to dialog manager
        }

        private void speechIsDone()
        {
            recognizer.setAccepting();
            // and... do nothing. Or later send a message back to dialog manager
        }

        public void Initialize()
        {
            dgnVoiceTxt = new DgnVoiceTxt();
            dgnVoiceTxt.Register("Kiosk", "TTS"+ postFixIdentifier);
            dgnVoiceTxt.SpeakingStarted += speechHasStarted;
            dgnVoiceTxt.SpeakingDone += speechIsDone;
            dgnVoiceTxt.Enabled = true;

            pipeServer = new NamedPipeServerStream(listener_pipe_name, PipeDirection.In, NumThreads);
            Console.WriteLine($"[DragonSpeechSynthesizer] Waiting for connection... ");
            pipeServer.WaitForConnection();
            Console.WriteLine($"[DragonSpeechSynthesizer] Synthesizer Connected!");

            ss = new StreamString(pipeServer);
            listenerThread = new Thread(StartListening);
            listenerThread.Start();
        }

        private void StartListening(object o)
        {
            while (true)
            {
                var res = ss.ReadString();
                Console.WriteLine($"[DragonSpeechSynthesizer] received: {res}");
                Speak(res);
            }
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
