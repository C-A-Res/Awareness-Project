using log4net;
using Microsoft.Psi;
using Microsoft.Psi.Audio;
using Microsoft.Psi.Components;
using Microsoft.Psi.Speech;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NU.Kiosk.Speech
{
    public class GrammarRecognizerWrapper : ConsumerProducer<AudioBuffer, IStreamingSpeechRecognitionResult>
    {
        /**
         * This class wraps around the SystemSpeechRecognizer, so that we can prevent it from
         * listening to itself while the speech synthesizer is delivering its output. 
         * 
         * Without this class, "Hello" and other short utterance will be captured and send 
         * to the downstream for processing. The result typically arrives right after DialogManager 
         * changes its state to listen, causing a self-feeding loop. 
         * 
         * A more elegant solution would involve manipulating the Timestamp of different components,
         * which is yet to be developed.
         */
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        SystemSpeechRecognizer recognizer;

        private const string AppName = "PsiSpeechSample__new";
        private const string BaseGrammarLocation = @"Resources\BaseGrammar.grxml";
        private const bool InitialDataAcceptance = true;

        private bool isAccepting;
        private int ReloadMessageIDCurrent = 0;
        private bool isAllowingGrammarReload;

        private Emitter<AudioBuffer> internalAudioTransfer;
        private Receiver<IStreamingSpeechRecognitionResult> internalResultTransfer;

        public Receiver<bool> isAcceptingData;


        public GrammarRecognizerWrapper(Pipeline pipeline, bool isAllowingGrammarReload = false) : base(pipeline)
        {
            this.recognizer = new SystemSpeechRecognizer(
                pipeline,
                new SystemSpeechRecognizerConfiguration()
                {
                    Language = "en-US",
                    Grammars = new GrammarInfo[]
                    {
                        new GrammarInfo() { Name = AppName, FileName = @BaseGrammarLocation}
                    }
                });
            
            this.isAcceptingData = pipeline.CreateReceiver<bool>(this, ReceiveStateChange, nameof(this.isAcceptingData));
            this.internalAudioTransfer = pipeline.CreateEmitter<AudioBuffer>(this, nameof(this.internalAudioTransfer));
            this.internalResultTransfer = pipeline.CreateReceiver<IStreamingSpeechRecognitionResult>(this, TransferOutput, nameof(this.internalAudioTransfer));

            this.internalAudioTransfer.PipeTo(this.recognizer.In);
            this.recognizer.Out.PipeTo(internalResultTransfer);

            this.isAccepting = InitialDataAcceptance;
            this.isAllowingGrammarReload = isAllowingGrammarReload;
        }

        protected override void Receive(AudioBuffer audio, Envelope e)
        {
            if (isAccepting)
            {
                this.internalAudioTransfer.Post(audio, DateTime.Now);
            }
        }

        private void ReceiveStateChange(bool arg1, Envelope arg2)
        {
            this.isAccepting = arg1;
        }

        private void TransferOutput(IStreamingSpeechRecognitionResult arg1, Envelope arg2)
        {
            this.Out.Post(arg1, DateTime.Now);
        }

        public void ReloadGrammar()
        {
            if (isAllowingGrammarReload)
            {
                this.isAccepting = false;

                var gw = new Kiosk.AllXMLGrammarWriter(@BaseGrammarLocation);
                gw.ReadFileAndConvert();
                string updatedGrammar = gw.GetResultString();

                DateTime post_time = new DateTime();

                Message<System.Collections.Generic.IEnumerable<String>> updateRequest =
                    new Message<System.Collections.Generic.IEnumerable<String>>(
                        new String[] {
                        updatedGrammar
                        }, post_time, post_time, 9876, ReloadMessageIDCurrent++);
                recognizer.SetGrammars(updateRequest);
                gw.WriteToFile();

                this.isAccepting = true;
            }
            else
            {
                _log.Info($"Grammar is called but feature is disabled");
            }            
        }
    }
}
