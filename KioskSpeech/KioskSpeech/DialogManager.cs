using System;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Synthesis;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Psi;
using Microsoft.Psi.Components;
using NU.Kiosk.SharedObject;

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

        private bool face = false;
        private bool sessionActive = false;

        public DialogManager(Pipeline pipeline) : base(pipeline)
        {
            this.UserInput = pipeline.CreateReceiver<Utterance>(this, ReceiveUserInput, nameof(this.UserInput));
            this.CompInput = pipeline.CreateReceiver<SharedObject.Action>(this, ReceiveCompInput, nameof(this.CompInput));
            this.SpeechSynthesizerState = pipeline.CreateReceiver<SynthesizerState>(this, ReceiveSynthesizerState, nameof(this.SpeechSynthesizerState));
            this.FaceDetected = pipeline.CreateReceiver<bool>(this, ReceiveFaceDetected, nameof(this.FaceDetected));
            this.WakeUp = pipeline.CreateReceiver<bool>(this, ReceiveWakeUp, nameof(this.WakeUp));
            
            this.UserOutput = pipeline.CreateEmitter<Utterance>(this, nameof(this.UserOutput));
            this.TextOutput = pipeline.CreateEmitter<string>(this, nameof(this.TextOutput));
            this.ActionOutput = pipeline.CreateEmitter<SharedObject.Action>(this, nameof(this.ActionOutput));
            this.StateChanged = pipeline.CreateEmitter<string>(this, nameof(this.StateChanged));

            InitResponseTimer();
            InitSessionTimer();
        }

        public Receiver<Utterance> UserInput { get; private set; }
        public Receiver<SharedObject.Action> CompInput { get; private set; }
        public Receiver<SynthesizerState> SpeechSynthesizerState { get; private set; }
        public Receiver<bool> FaceDetected { get; private set; }
        public Receiver<bool> WakeUp { get; private set; }

        public Emitter<Utterance> UserOutput { get; private set; }
        public Emitter<string> TextOutput { get; private set; }
        public Emitter<SharedObject.Action> ActionOutput { get; private set; }
        public Emitter<string> StateChanged { get; private set; }



        private void ReceiveUserInput(Utterance arg1, Envelope arg2)
        {
            Console.Write(arg1.Text);
            if (state == DialogState.Listening)
            {
                Console.Write("-processing");
                UserOutput.Post(arg1, arg2.OriginatingTime);
                updateState(DialogState.Thinking, arg2.OriginatingTime);
                continueSession();
                StartResponseTimer();
            } else
            {
                // log it, then ignore it
                Console.Write("-ignoring");
            }
        }

        private void ReceiveCompInput(SharedObject.Action arg1, Envelope arg2)
        {
            handleCompInput(arg1, arg2.OriginatingTime);
        }

        private void handleCompInput(SharedObject.Action action, DateTime origTime)
        {
            if (state == DialogState.Thinking)
            {
                // sayText action handled specially
                if (action.Name == "psikiSayText")
                {
                    TextOutput.Post((string)action.Args[0], origTime);
                }
                // pass it through (even sayText)
                ActionOutput.Post(action, origTime);
                // update state
                updateState(DialogState.Speaking, origTime);
            }
            StopResponseTimer();
        }

        private void ReceiveSynthesizerState(SynthesizerState arg1, Envelope arg2)
        {
            if (state == DialogState.Speaking && arg1 == SynthesizerState.Ready)
            {
                if (sessionActive) updateState(DialogState.Listening, arg2.OriginatingTime); else updateState(DialogState.Sleeping, arg2.OriginatingTime);
            }
        }

        private void ReceiveFaceDetected(bool arg1, Envelope arg2)
        {
            face = arg1;
            if (face)
            {
                startSession();
                if (state == DialogState.Sleeping)
                {
                    updateState(DialogState.Listening, arg2.OriginatingTime);
                }
            } else
            {
                endSession();
            }

        }

        private void ReceiveWakeUp(bool arg1, Envelope arg2)
        {
            startSession();
            if (state == DialogState.Sleeping)
            {
                updateState(DialogState.Listening, arg2.OriginatingTime);
            }
        }

        private void updateState(DialogState newState, DateTime dt)
        {
            state = newState;
            // override dt for now
            dt = DateTime.Now;

            switch (newState)
            {
                case DialogState.Sleeping:
                    StateChanged.Post("sleeping", dt);
                    break;
                case DialogState.Listening:
                    StateChanged.Post("listening", dt);
                    break;
                case DialogState.Thinking:
                    StateChanged.Post("thinking", dt);
                    break;
                case DialogState.Speaking:
                    StateChanged.Post("speaking", dt);
                    break;
            }
        }

        private void startSession()
        {
            sessionActive = true;
            StartSessionTimer();
        }

        private void continueSession()
        {
            RestartSessionTimer();
        }

        private void endSession()
        {
            sessionActive = false;
            updateState(DialogState.Sleeping, DateTime.Now);
            StopSessionTimer();
        }

        // Timer for handling timeout of no response
        System.Timers.Timer timer = new System.Timers.Timer(1);

        private void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            StopResponseTimer();
            Console.WriteLine("Time's up");
            handleCompInput(new SharedObject.Action("psikiSayText", "Sorry, does not compute."), DateTime.Now);
        }

        private void InitResponseTimer()
        {
            timer = new System.Timers.Timer(10000);
            timer.Elapsed += this.OnTimedEvent;
        }

        private void StartResponseTimer()
        {
            //timer.Enabled = true;
            timer.Start();
            Console.WriteLine("[Merger: StartTimer] Timer Enabled.");
        }

        private void StopResponseTimer()
        {
            //timer.Enabled = false;
            timer.Stop();
            Console.WriteLine("[Merger: StopTimer] Timer Disabled");
        }



        // Timer for handling timeout of session
        System.Timers.Timer s_timer = new System.Timers.Timer(1);

        private void OnSessionTimedEvent(object source, ElapsedEventArgs e)
        {
            endSession();
        }

        private void InitSessionTimer()
        {
            s_timer = new System.Timers.Timer(100000);
            s_timer.Elapsed += this.OnSessionTimedEvent;
        }

        private void RestartSessionTimer()
        {
            s_timer.Stop();
            s_timer.Start();
        }

        private void StartSessionTimer()
        {
            //timer.Enabled = true;
            s_timer.Start();
        }

        private void StopSessionTimer()
        {
            //timer.Enabled = false;
            s_timer.Stop();
        }


    }
}
