﻿using System;
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
    public class DragonDialogManager : ConsumerProducer<string, string>
    {
        private readonly Pipeline pipeline;
        private DragonRecognizer dragon_recognizer;

        private DialogState state;
        public enum DialogState
        {
            Sleeping,
            Listening,
            Thinking,
            Speaking
        }

        private bool face = false;

        public DragonDialogManager(Pipeline pipeline, DragonRecognizer dragon = null) : base(pipeline)
        {
            this.UserInput = pipeline.CreateReceiver<Utterance>(this, ReceiveUserInput, nameof(this.UserInput));
            this.CompInput = pipeline.CreateReceiver<string>(this, ReceiveCompInput, nameof(this.CompInput));
            this.SpeechSynthesizerState = pipeline.CreateReceiver<SynthesizerState>(this, ReceiveSynthesizerState, nameof(this.SpeechSynthesizerState));
            this.FaceDetected = pipeline.CreateReceiver<bool>(this, ReceiveFaceDetected, nameof(this.FaceDetected));


            this.UserOutput = pipeline.CreateEmitter<Utterance>(this, nameof(this.UserOutput));
            this.CompOutput = pipeline.CreateEmitter<string>(this, nameof(this.CompOutput));
            this.StateChanged = pipeline.CreateEmitter<DialogState>(this, nameof(this.StateChanged));

            this.state = DialogState.Listening;
            this.dragon_recognizer = dragon;

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
                CompOutput.Post(text, origTime);
                state = DialogState.Speaking;
            }
            StopTimer();
        }

        private void ReceiveSynthesizerState(SynthesizerState arg1, Envelope arg2)
        {
            if (state == DialogState.Speaking && arg1 == SynthesizerState.Ready)
            {
                if (face)
                {
                    state = DialogState.Listening;
                    dragon_recognizer.setAccepting();
                } else
                {
                    state = DialogState.Sleeping;
                    dragon_recognizer.setNotAccepting();
                }
            }
        }

        private void ReceiveFaceDetected(bool arg1, Envelope arg2)
        {
            face = arg1;
            if (state == DialogState.Sleeping)
            {
                state = DialogState.Listening;
                if (dragon_recognizer != null)
                {
                    dragon_recognizer.setAccepting();
                }
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
            if (dragon_recognizer != null)
            {
                dragon_recognizer.setAccepting();
            }
        }

    }
}