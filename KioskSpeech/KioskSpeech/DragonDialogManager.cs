using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Security.Principal;
using System.Speech.Synthesis;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Psi;
using Microsoft.Psi.Components;

namespace NU.Kiosk.Speech
{
    public class DragonDialogManager : ConsumerProducer<string, string>, IStartable, IDisposable
    {
        private readonly Pipeline pipeline;
        private const string synth_state_listener_pipe_name = "dragon_synthesizer_state_pipe";
        private const string destination_pipe_name = "dragon_synthesizer_pipe";
        private const string destination_start_stop_pipe_name = "dragon_start_stop_pipe";
        //private const string listener_pipe_name = "dragon_processed_text_pipe";

        private PipeListener synth_listener;
        private PipeSender synth_sender;
        private PipeSender start_stop_sender;

        private DialogState state;
        public enum DialogState
        {
            Sleeping,
            Listening,
            Thinking,
            Speaking
        }

        private bool face = false;

        public DragonDialogManager(Pipeline pipeline) : base(pipeline)
        {
            this.UserInput = pipeline.CreateReceiver<Utterance>(this, ReceiveUserInput, nameof(this.UserInput));
            this.CompInput = pipeline.CreateReceiver<string>(this, ReceiveCompInput, nameof(this.CompInput));
            //this.SpeechSynthesizerState = pipeline.CreateReceiver<SynthesizerState>(this, ReceiveSynthesizerState, nameof(this.SpeechSynthesizerState));
            this.FaceDetected = pipeline.CreateReceiver<bool>(this, ReceiveFaceDetected, nameof(this.FaceDetected));


            this.UserOutput = pipeline.CreateEmitter<Utterance>(this, nameof(this.UserOutput));
            this.CompOutput = pipeline.CreateEmitter<string>(this, nameof(this.CompOutput));
            this.StateChanged = pipeline.CreateEmitter<DialogState>(this, nameof(this.StateChanged));

            this.state = DialogState.Sleeping;

            // response
            synth_sender = new PipeSender(destination_pipe_name);

            // start-stop
            start_stop_sender = new PipeSender(destination_start_stop_pipe_name);

            // synth listener
            synth_listener = new PipeListener(ReceiveSynthesizerState, synth_state_listener_pipe_name);

            InitTimer();
        }

        public void Start(System.Action onCompleted, ReplayDescriptor descriptor)
        {
            new Task(() => synth_sender.Initialize()).Start();
            new Task(() => start_stop_sender.Initialize()).Start();
            new Task(() => synth_listener.Initialize()).Start();
        }

        public void Stop()
        {
            // do nothing, leave for dispose
        }

        public Receiver<Utterance> UserInput { get; private set; }
        public Receiver<string> CompInput { get; private set; }
        //public Receiver<SynthesizerState> SpeechSynthesizerState { get; private set; }
        public Receiver<bool> FaceDetected { get; private set; }

        public Emitter<Utterance> UserOutput { get; private set; }
        public Emitter<string> CompOutput { get; private set; }
        public Emitter<DialogState> StateChanged { get; private set; }

        public void Dispose()
        {
            Console.WriteLine("[DragonDialogManager] Dispose");

            // synth listener
            synth_listener.Dispose();

            // sender then stops
            synth_sender.Dispose();

            // start-stop stops
            start_stop_sender.Dispose();
        }

        public void SendToDragon(string message)
        {
            Console.WriteLine($"[DragonDialogManager] sending message to synthesizer: {message}");
            synth_sender.Send(message);
        }

        public void TellDragonToStartOrStopListening(bool start)
        {
            var startOrNot = start ? "start" : "stop";
            Console.WriteLine($"[DragonDialogManager] telling dragon to {startOrNot} listening");
            start_stop_sender.Send(start ? "1" : "0");
        }

        private void ReceiveUserInput(Utterance arg1, Envelope arg2)
        {

            if (state == DialogState.Listening)
            {
                // for recognition, the setNotAccepting is automatic
                Console.Write('x');
                UserOutput.Post(arg1, arg2.OriginatingTime);
                state = DialogState.Thinking;
                StartTimer();
            }
            else
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
                SendToDragon(text);
                CompOutput.Post(text, origTime);
                state = DialogState.Speaking;
            }
            // Stop timer here prevents the "Sorry I don't know" from coming up. 
            // when speak finishes, dragon will automatically start listening again. 
            StopTimer();
        }

        private void ReceiveSynthesizerState(string arg1)
        {
            if (state == DialogState.Speaking && arg1.Equals("Done"))
            {
                if (face)
                {
                    state = DialogState.Listening;
                    TellDragonToStartOrStopListening(true);
                    //dragon_recognizer.setAccepting();
                } else
                {
                    state = DialogState.Sleeping;
                    TellDragonToStartOrStopListening(false);
                    //dragon_recognizer.setNotAccepting();
                }
            }
        }

        private void ReceiveFaceDetected(bool arg1, Envelope arg2)
        {
            face = arg1;
            if (face && state == DialogState.Sleeping)
            {
                state = DialogState.Listening;
                TellDragonToStartOrStopListening(true);
                //dragon_recognizer.setAccepting();
            } else if (!face && state == DialogState.Listening)
            {
                state = DialogState.Sleeping;
                TellDragonToStartOrStopListening(false);
            }
        }


        // Timer for handling timeout of no response
        System.Timers.Timer timer = new System.Timers.Timer(1);

        private void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            StopTimer();
            Console.WriteLine("[DragonDialogManager] Time's up.");
            handleCompInput("Sorry, does not compute.", DateTime.Now);
            TellDragonToStartOrStopListening(true);
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
