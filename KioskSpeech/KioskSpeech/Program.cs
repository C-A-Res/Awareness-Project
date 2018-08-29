// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="Microsoft Corporation">
//   Copyright (C) Microsoft Corporation.  All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace NU.Kiosk.Speech
{
    using System;
    using System.IO;
    using System.Linq;
    using Microsoft.Psi;
    using Microsoft.Psi.Audio;
    using Microsoft.Psi.Components;
    using Microsoft.Psi.Data;
    using Microsoft.Psi.Speech;
    using Microsoft.Psi.Visualization.Client;
    using NU.Kqml;
    using System.Speech.Recognition;

    public static class Program
    {
        private const string AppName = "PsiSpeechSample";
        private const string LogPath = @"..\..\..\Videos\" + Program.AppName;
        private const bool isUsingDragon = true;
        private static int ReloadMessageIDCurrent = 0;
        
        // Component that serves as connection to Python
        static SocketStringConsumer python = null;

        public static void Main(string[] args)
        {
            //KQMLMessage mess = (new KQMLMessageParser()).parse(" (tell :reply-with id-interaction-manager235 :sender interaction-manager :receiver psi :in-reply-to psi-id0 :language fire :content (onAgenda interaction-manager (TaskFn 1)))");
            //Console.WriteLine(mess.ToString() + "\n");

            //Console.WriteLine("Press any key to exit...");
            //Console.ReadKey(true);

            string facilitatorIP = args[0];
            int facilitatorPort = int.Parse(args[1]);
            int localPort = int.Parse(args[2]);

            KioskInputTextPreProcessor.isUsingIsAccepting = true;

            // The root folder under which data will be logged. This may be set to null, which will create
            // a volatile data store to which data can be written for the purposes of live visualization.
            string outputLogPath = null;

            // The root folder from which previously logged audio data will be read as input. By default the
            // most recent session will be used. If set to null, live audio from the microphone will be used.
            string inputLogPath = null;

            // Flag to display live data in PsiStudio
            bool showLiveVisualization = false;

            // Flag to exit the application
            bool exit = false;

            RunSystemSpeech(outputLogPath, inputLogPath, showLiveVisualization, facilitatorIP, facilitatorPort, localPort);
            if (python != null) python.Stop();

        }

        /// <summary>
        /// Builds and runs a speech recognition pipeline using the .NET System.Speech recognizer and a set of fixed grammars.
        /// </summary>
        /// <param name="outputLogPath">The path under which to write log data.</param>
        /// <param name="inputLogPath">The path from which to read audio input data.</param>
        /// <param name="showLiveVisualization">A flag indicating whether to display live data in PsiStudio as the pipeline is running.</param>
        public static void RunSystemSpeech(string outputLogPath = null, string inputLogPath = null, bool showLiveVisualization = true,
            string facilitatorIP = "localhost", int facilitatorPort = 9000, int localPort = 8090)
        {
            // Create the pipeline object.
            using (Pipeline pipeline = Pipeline.Create())
            {
                // Needed only for live visualization
                DateTime startTime = DateTime.Now;

                // Use either live audio from the microphone or audio from a previously saved log
                IProducer<AudioBuffer> audioInput = null;
                if (inputLogPath != null)
                {
                    // Open the MicrophoneAudio stream from the last saved log
                    var store = Store.Open(pipeline, Program.AppName, inputLogPath);
                    audioInput = store.OpenStream<AudioBuffer>($"{Program.AppName}.MicrophoneAudio");

                    // Get the originating time of the start of the data in the store. We will use this
                    // to set the correct start time in the visualizer (if live visualization is on).
                    startTime = store.OriginatingTimeInterval.Left;
                }
                else
                {
                    // Create the AudioSource component to capture audio from the default device in 16 kHz 1-channel
                    // PCM format as required by both the voice activity detector and speech recognition components.
                    audioInput = new AudioSource(pipeline, new AudioSourceConfiguration() { OutputFormat = WaveFormat.Create16kHz1Channel16BitPcm() });
                }

                if (isUsingDragon)
                {
                    // Create System.Speecsh recognizer component
                    var recognizer = new DragonRecognizer(pipeline);
                    DragonSpeechSynthesizer speechSynth = new DragonSpeechSynthesizer(pipeline);
                    recognizer.Out.PipeTo(speechSynth);
                    speechSynth.SpeakCompleted.Do(x => {
                        Console.WriteLine($"[Program.cs] SpeakCompleted; set accepting.");
                        recognizer.setAccepting();
                    });
                    speechSynth.SpeakStarted.Do(x =>
                    {
                        Console.WriteLine($"[Program.cs] SpeakStarted...");
                    });
                } else
                {
                    // Create System.Speecsh recognizer component
                    var recognizer = CreateSpeechRecognizer(pipeline);

                    // Subscribe the recognizer to the input audio
                    audioInput.PipeTo(recognizer);

                    // Partial and final speech recognition results are posted on the same stream. Here
                    // we use Psi's Where() operator to filter out only the final recognition results.
                    var finalResults = recognizer.Out.Where(result => result.IsFinal);
                    finalResults.Do(x => Console.WriteLine(x));
                    KioskUI.KioskUI ui = new KioskUI.KioskUI(pipeline);
                    SystemSpeechSynthesizer speechSynth = CreateSpeechSynthesizer(pipeline);
                    KioskInputTextPreProcessor preproc = new NU.Kqml.KioskInputTextPreProcessor(pipeline, (SystemSpeechRecognizer)recognizer);

                    finalResults.PipeTo(preproc.In);
                    preproc.Out.Do(x => Console.WriteLine($"Processed: {x}"));

                    preproc.Out.PipeTo(ui.UserInput);
                    if (facilitatorIP != "none")
                    {
                        python = new SocketStringConsumer(pipeline, facilitatorIP, facilitatorPort, localPort);
                        preproc.Out.PipeTo(ui.UserInput);
                        python.Out.PipeTo(ui.CompResponse);
                        python.Out.PipeTo(speechSynth);
                    }
                    else
                    {
                        preproc.Out.PipeTo(ui.CompResponse);
                        preproc.Out.PipeTo(speechSynth);
                    }
                    speechSynth.SpeakCompleted.Do(x =>
                    {
                        Console.WriteLine($"[Program.cs] SpeakCompleted; set accepting!");
                        preproc.setAccepting();
                    });
                    speechSynth.SpeakStarted.Do(x =>
                    {
                        Console.WriteLine($"[Program.cs] SpeakStarted: '{x}'");
                    });
                    speechSynth.StateChanged.Do(x =>
                    {
                        Console.WriteLine($"[Program.cs] speechSynth state: '{x}'");
                    });
                    speechSynth.SpeakProgress.Do(x =>
                    {
                        Console.WriteLine($"[Program.cs] SpeakProgress: '{x}'");
                    });

                    // Create a data store to log the data to if necessary. A data store is necessary
                    // only if output logging or live visualization are enabled.
                    var dataStore = CreateDataStore(pipeline, outputLogPath, showLiveVisualization);

                    // For disk logging or live visualization only
                    if (dataStore != null)
                    {
                        // Log the microphone audio and recognition results
                        audioInput.Write($"{Program.AppName}.MicrophoneAudio", dataStore);
                        //finalResults.Write($"{Program.AppName}.FinalRecognitionResults", dataStore);
                    }
                }                

                // Register an event handler to catch pipeline errors
                pipeline.PipelineCompletionEvent += PipelineCompletionEvent;

                // Run the pipeline
                pipeline.RunAsync();

                Console.WriteLine("Press any key to exit...");
                Console.ReadKey(true);
            }
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

        public static ConsumerProducer<AudioBuffer, IStreamingSpeechRecognitionResult> CreateSpeechRecognizer(Pipeline pipeline)
        {
            var recognizer = new SystemSpeechRecognizer(
            pipeline,
            new SystemSpeechRecognizerConfiguration()
            {
                Language = "en-US",
                Grammars = new GrammarInfo[]
                {
                     new GrammarInfo() { Name = Program.AppName, FileName = @"Resources\BaseGrammar.grxml" }
                }
            });
            return recognizer;
        }

        /// <summary>
        /// Event handler for the PipelineCompletion event.
        /// </summary>
        /// <param name="sender">The sender which raised the event.</param>
        /// <param name="e">The pipeline completion event arguments.</param>
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

        private static bool isCommand(string input)
        {
            switch (input)
            {
                case "Reload grammars":
                    return true;
                default:
                    return false;
            }
        }

        private static void processCommand(ref SystemSpeechRecognizer recognizer, string input)
        {
            switch (input)
            {
                case "Reload grammars":
                    {
                        var gw = new AllXMLGrammarWriter();
                        gw.ReadFileAndConvert();
                        string updatedGrammar = gw.GetResultString();

                        DateTime post_time = new DateTime();
                        //String originalGrammar = File.ReadAllText("GeneratedGrammar.grxml");

                        Message<System.Collections.Generic.IEnumerable<String>> updateRequest =
                            new Message<System.Collections.Generic.IEnumerable<String>>(
                                new String[] {
                                    updatedGrammar
                                    //originalGrammar
                                }, post_time, post_time, 9876, ReloadMessageIDCurrent++);
                        recognizer.SetGrammars(updateRequest);
                        gw.WriteToFile();
                        break;
                    }
                default:
                    break;
            }
        }

        /// <summary>
        /// Create a data store to log stream data to. A data store may be persisted on disk (if outputLogPath is defined),
        /// or it may be an in-memory volatile store. The latter is only required if we are visualizing live data, and
        /// only if we are not already logging data to a persisted store.
        /// </summary>
        /// <param name="pipeline">The Psi pipeline associated with the store.</param>
        /// <param name="outputLogPath">The path to a folder in which a persistent store will be created.</param>
        /// <param name="showLiveVisualization">Whether or not live visualization is enabled.</param>
        /// <returns>The store Exporter object if a store was successfully created.</returns>
        private static Exporter CreateDataStore(Pipeline pipeline, string outputLogPath = null, bool showLiveVisualization = false)
        {
            // If this is a persisted store, use the application name as the store name. Otherwise, generate
            // a unique temporary name for the volatile store only if we are visualizing live data.
            string dataStoreName = (outputLogPath != null) ? Program.AppName :
                showLiveVisualization ? "Temp-" + DateTime.Now.ToString("yyyyMMdd-hhmmss") : null;

            // Create the store only if it is needed (logging to disk or live visualization).
            return (dataStoreName != null) ? Store.Create(pipeline, dataStoreName, outputLogPath) : null;
        }

    }
}
