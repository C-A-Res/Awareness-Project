using System;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Synthesis;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Psi;
using Microsoft.Psi.Components;

namespace NU.Kiosk.Speech
{
    public class DialogManager : ConsumerProducer<string, string>
    {
        private readonly Pipeline pipeline;

        private DialogState state;
        public enum DialogState
        {
            Sleeping,
            Listening,
            Thinking,
            Speaking
        }

        private bool isDetectingFace;
        private bool face = false;

        public DialogManager(Pipeline pipeline, bool isDetectingFace = true) : base(pipeline)
        {
            this.UserInput = pipeline.CreateReceiver<Utterance>(this, ReceiveUserInput, nameof(this.UserInput));
            this.CompInput = pipeline.CreateReceiver<string>(this, ReceiveCompInput, nameof(this.CompInput));
            this.SpeechSynthesizerState = pipeline.CreateReceiver<SynthesizerState>(this, ReceiveSynthesizerState, nameof(this.SpeechSynthesizerState));
            this.FaceDetected = pipeline.CreateReceiver<bool>(this, ReceiveFaceDetected, nameof(this.FaceDetected));

            this.isDetectingFace = isDetectingFace;

            this.UserOutput = pipeline.CreateEmitter<Utterance>(this, nameof(this.UserOutput));
            this.CompOutput = pipeline.CreateEmitter<string>(this, nameof(this.CompOutput));
            this.StateChanged = pipeline.CreateEmitter<DialogState>(this, nameof(this.StateChanged));

            state = DialogState.Listening;

            InitTimer();
        }

        public Receiver<Utterance> UserInput { get; private set; }
        public Receiver<string> CompInput { get; private set; }
        public Receiver<SynthesizerState> SpeechSynthesizerState { get; private set; }
        public Receiver<bool> FaceDetected { get; private set; }

        public Emitter<Utterance> UserOutput { get; private set; }
        public Emitter<string> CompOutput { get; private set; }
        public Emitter<DialogState> StateChanged { get; private set; }



        private void ReceiveUserInput(Utterance arg1, Envelope arg2)
        {
            if (state == DialogState.Listening)
            {
                Console.Write('x');
                UserOutput.Post(arg1, arg2.OriginatingTime);
                state = DialogState.Thinking;
                StartTimer();
            } else
            {
                // log it, then ignore it
            }
        }

        private void ReceiveCompInput(string arg1, Envelope arg2)
        {
            handleCompInput(arg1, arg2.OriginatingTime);
        }

        private void handleCompInput(string text, DateTime origTime)
        {
            if (state == DialogState.Thinking)
            {
                state = DialogState.Speaking;
                CompOutput.Post(text, origTime);                
            }
            StopTimer();
        }

        private void ReceiveSynthesizerState(SynthesizerState arg1, Envelope arg2)
        {
            if (state == DialogState.Speaking && arg1 == SynthesizerState.Ready)
            {
                state = face || !isDetectingFace ? DialogState.Listening : DialogState.Sleeping;
            }
        }

        private void ReceiveFaceDetected(bool arg1, Envelope arg2)
        {
            face = arg1;
            if (!isDetectingFace || state == DialogState.Sleeping)
            {
                state = DialogState.Listening;
            }
        }


        // Timer for handling timeout of no response
        System.Timers.Timer timer = new System.Timers.Timer(1);

        private void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            StopTimer();
            Console.WriteLine("Time's up");
            handleCompInput("Sorry, does not compute.", DateTime.Now);
        }

        private void InitTimer()
        {
            timer = new System.Timers.Timer(10000);
            timer.Elapsed += this.OnTimedEvent;
        }

        private void StartTimer()
        {
            timer.Enabled = true;
            Console.WriteLine("[Merger: StartTimer] Timer Enabled.");
        }

        private void StopTimer()
        {
            timer.Enabled = false;
            Console.WriteLine("[Merger: StopTimer] Timer Disabled");
        }

    }
}
