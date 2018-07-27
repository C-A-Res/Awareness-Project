using Microsoft.Psi;
using Microsoft.Psi.Audio;
using Microsoft.Psi.Components;
using Microsoft.Psi.Speech;
using NU.Kqml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NU.Kiosk.Speech
{
    class trials_and_errors
    {
        public static void Main(string[] args)
        {
            //////KQMLMessage msg = KQMLMessage.parseMessage("(achieve :sender psi :receiver psi :reply-with psi-id0 :content (processKioskUtterance \"Where is Professor Forbus 's office\") )");
            ////KQMLMessage msg = KQMLMessageParser.parse("(achieve :sender psi :receiver psi :reply-with psi-id0 :content (processKioskUtterance \"Where is Professor Forbus 's office\") )");
            ////Console.WriteLine($"Message content: {msg.ToString()}");

            ////Console.WriteLine("Press any key to exit...");
            ////Console.ReadKey(true);

            string facilitatorIP = "127.0.0.1";
            int echoerPort = 6000;
            int speechPort = 8090;

            IProducer<AudioBuffer> audioInput = null;
            SocketStringConsumer python = null;
            SocketEchoer echoer = new SocketEchoer(facilitatorIP, speechPort, echoerPort);
            SpeechInputTextPreProcessor preproc = null;

            Console.WriteLine("Trials and errors!\n");

            using (Pipeline pipeline = Pipeline.Create())
            {
                audioInput = new AudioSource(pipeline, new AudioSourceConfiguration() { OutputFormat = WaveFormat.Create16kHz1Channel16BitPcm() });

                // Create System.Speech recognizer component
                var recognizer = CreateSpeechRecognizer(pipeline);

                // Subscribe the recognizer to the input audio
                audioInput.PipeTo(recognizer);

                // Partial and final speech recognition results are posted on the same stream. Here
                // we use Psi's Where() operator to filter out only the final recognition results.
                var finalResults = recognizer.Out.Where(result => result.IsFinal);

                python = new SocketStringConsumer(pipeline, facilitatorIP, echoerPort, speechPort, "echoer");
                preproc = new SpeechInputTextPreProcessor(pipeline);
                
                finalResults.Select(result => result.Text).PipeTo(preproc.In);
                preproc.Out.PipeTo(python.In);

                SystemSpeechSynthesizer speechSynth = CreateSpeechSynthesizer(pipeline);
                speechSynth.SpeakCompleted.Do(x =>
                {
                    Console.WriteLine(".");
                    preproc.setAccepting();
                });
                python.Out.PipeTo(speechSynth);

                // Register an event handler to catch pipeline errors
                pipeline.PipelineCompletionEvent += PipelineCompletionEvent;

                // Run the pipeline
                pipeline.RunAsync();

                Console.WriteLine("Press any key to exit...");
                Console.ReadKey(true);
                Console.WriteLine("Done!");
            }
        }

        public static ConsumerProducer<AudioBuffer, IStreamingSpeechRecognitionResult> CreateSpeechRecognizer(Pipeline pipeline)
        {
            var recognizer = new SystemSpeechRecognizer(
            pipeline,
            new SystemSpeechRecognizerConfiguration()
            {
                Language = "en-US",
                Grammars = new GrammarInfo[]
                {
                     new GrammarInfo() { Name = "TrialsAndErrors", FileName = @"Resources\CuratedGrammar.grxml" }
                }
            });
            return recognizer;
        }

        public static SystemSpeechSynthesizer CreateSpeechSynthesizer(Pipeline pipeline)
        {
            // testing out the speech synth
            return new SystemSpeechSynthesizer(
                pipeline,
                new SystemSpeechSynthesizerConfiguration()
                {
                    Voice = "Microsoft Zira Desktop",
                    UseDefaultAudioPlaybackDevice = true
                });
        }

        private static void PipelineCompletionEvent(object sender, PipelineCompletionEventArgs e)
        {
            Console.WriteLine("Pipeline execution completed with {0} errors", e.Errors.Count);

            // Prints all exceptions that were thrown by the pipeline
            if (e.Errors.Count > 0)
            {
                foreach (var error in e.Errors)
                {
                    Console.WriteLine(error);
                }
            }
        }
    }


}
