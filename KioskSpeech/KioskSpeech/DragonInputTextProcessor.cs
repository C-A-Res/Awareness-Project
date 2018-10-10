using Microsoft.Psi;
using Microsoft.Psi.Audio;
using Microsoft.Psi.Components;
using Microsoft.Psi.Speech;
using NU.Kiosk.Speech;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NU.Kiosk.SharedObject;

namespace NU.Kiosk.Speech
{
    public class DragonInputTextProcessor : IProducer<Utterance>, IStartable, IDisposable
    {
        private const string listener_pipe_name = "dragon_processed_text_pipe";
        private PipeListener listener;

        public Receiver<string> UiInput { get; private set; }
        public Emitter<Utterance> Out { get; private set; }

        private bool isUsing;

        public DragonInputTextProcessor(Pipeline pipeline)
        {
            this.Out = pipeline.CreateEmitter<Utterance>(this, nameof(DragonInputTextProcessor));
            listener = new PipeListener(PassOnToPipeline, listener_pipe_name);
            this.UiInput = pipeline.CreateReceiver<string>(this, ReceiveUiInput, nameof(this.UiInput));
        }

        public void Dispose()
        {
            Console.WriteLine("[DragonInputTextProcessor] Dispose");
            listener.Dispose();
        }

        private void ReceiveUiInput(string arg1, Envelope arg2)
        {
            if (isUsing)
            {
                Out.Post(new Utterance(arg1, 1.0, StringResultSource.ui), arg2.OriginatingTime);
            }
        }

        public void PassOnToPipeline(string message)
        {
            if (isUsing)
            {
                Out.Post(new Utterance(message, 1.0, StringResultSource.speech), DateTime.Now);
            }            
        }

        public void Start(System.Action onCompleted, ReplayDescriptor descriptor)
        {
            listener.Initialize();
            isUsing = true;
        }

        public void Stop()
        {
            isUsing = false;
        }
    }
}
