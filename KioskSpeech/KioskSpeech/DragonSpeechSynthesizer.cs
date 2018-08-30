using DNSTools;
using Microsoft.Psi;
using Microsoft.Psi.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Microsoft.Psi.Speech.SystemSpeechSynthesizer;

namespace NU.Kiosk.Speech
{
    class DragonSpeechSynthesizer : IConsumer<string>, IStartable, IDisposable
    {
        public Receiver<string> In { get; }

        DgnVoiceTxt dgnVoiceTxt;
        string postFixIdentifier;
        public Emitter<SpeakCompletedEventData> SpeakCompleted { get; private set; }
        public Emitter<SpeakStartedEventData> SpeakStarted { get; private set; }

        public DragonSpeechSynthesizer(Pipeline pipeline)
        {
            this.In = pipeline.CreateReceiver<string>(this, this.speak, nameof(this.In));

            postFixIdentifier = DateTime.Now.ToLongTimeString();
            this.SpeakCompleted = pipeline.CreateEmitter<SpeakCompletedEventData>(this, "SpeakCompleted"+ postFixIdentifier);
            this.SpeakStarted = pipeline.CreateEmitter<SpeakStartedEventData>(this, "SpeakStarted" + postFixIdentifier);
        }
        
        public void speak(string utterance)
        {
            dgnVoiceTxt.Speak(utterance);            
        }

        private void speechHasStarted()
        {
            //if (this.SpeakStarted.Name != null)
            this.SpeakStarted.Post(new SpeakStartedEventData(), DateTime.Now);
        }

        private void speechIsDone() {
            //if (this.SpeakCompleted.Name != null)
            this.SpeakCompleted.Post(new SpeakCompletedEventData(), DateTime.Now);
        }

        public void Start(Action onCompleted, ReplayDescriptor descriptor)
        {
            dgnVoiceTxt = new DgnVoiceTxt();
            dgnVoiceTxt.Register("Kiosk", "TTS"+ postFixIdentifier);
            dgnVoiceTxt.SpeakingStarted += speechHasStarted;
            dgnVoiceTxt.SpeakingDone += speechIsDone;
            dgnVoiceTxt.Enabled = true;
        }

        public void Stop()
        {
            dgnVoiceTxt.Enabled = false;
        }

        public void Dispose()
        {
            dgnVoiceTxt.UnRegister();
            dgnVoiceTxt = null;
        }
    }
}
