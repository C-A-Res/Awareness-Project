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
        public Emitter<SpeakCompletedEventData> SpeakCompleted { get; private set; }
        public Emitter<SpeakStartedEventData> SpeakStarted { get; private set; }

        public DragonSpeechSynthesizer(Pipeline pipeline)
        {
            this.In = pipeline.CreateReceiver<string>(this, this.speak, nameof(this.In));

            this.SpeakCompleted = pipeline.CreateEmitter<SpeakCompletedEventData>(this, nameof(this.SpeakCompleted));
            this.SpeakStarted = pipeline.CreateEmitter<SpeakStartedEventData>(this, nameof(this.SpeakStarted));
        }
        
        public void speak(string utterance)
        {
            dgnVoiceTxt.Speak(utterance);            
        }

        private void speechHasStarted()
        {
            this.SpeakStarted.Post(new SpeakStartedEventData(), DateTime.Now);
        }

        private void speechIsDone() {
            this.SpeakCompleted.Post(new SpeakCompletedEventData(), DateTime.Now);
        }

        public void Start(Action onCompleted, ReplayDescriptor descriptor)
        {
            dgnVoiceTxt = new DgnVoiceTxt();
            dgnVoiceTxt.Register("Kiosk", "TTS");
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
