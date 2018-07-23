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

        // Component that serves as connection to Python
        //static WebSocketStringConsumer python = null;
        static SocketStringConsumer python = null;

        private static int ReloadMessageIDCurrent = 0;

        public static void Main(string[] args)
        {
            string facilitatorIP = args[0];
            int facilitatorPort = int.Parse(args[1]);
            int localPort = int.Parse(args[2]);

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

                // Create System.Speech recognizer component
                var recognizer = CreateSpeechRecognizer(pipeline);

                // Subscribe the recognizer to the input audio
                audioInput.PipeTo(recognizer);

                // Partial and final speech recognition results are posted on the same stream. Here
                // we use Psi's Where() operator to filter out only the final recognition results.
                var finalResults = recognizer.Out.Where(result => result.IsFinal);

                // Print the recognized text of the final recognition result to the console.
                finalResults.Do(result =>
                {
                    var ssrResult = result as SpeechRecognitionResult;
                    if (ssrResult.Text.IndexOf("What") >= 0 || ssrResult.Text.IndexOf("When") >= 0 || ssrResult.Text.IndexOf("Where") >= 0 || ssrResult.Text.IndexOf("Who") >= 0 || ssrResult.Text.IndexOf("Can") >= 0)
                    {
                        Console.WriteLine($"{ssrResult.Text}? (confidence: {ssrResult.Confidence})");
                    }
                    else if (isCommand(ssrResult.Text))
                    {
                        SystemSpeechRecognizer r = (SystemSpeechRecognizer)recognizer;
                        processCommand(ref r, ssrResult.Text);
                    }
                    else
                    {
                        Console.WriteLine($"{ssrResult.Text} (confidence: {ssrResult.Confidence})");
                    }
                });

                SystemSpeechSynthesizer speechSynth = CreateSpeechSynthesizer(pipeline);

                var text = finalResults.Select(result => result.Text);
                text.Do(x => Console.WriteLine(x));
                text.PipeTo(speechSynth);
                speechSynth.SpeakCompleted.Do(x => Console.WriteLine("."));


                //if (facilitatorIP != "none")
                //{
                //    python = new SocketStringConsumer(pipeline, facilitatorIP, facilitatorPort, localPort);

                //    var text = finalResults.Select(result =>
                //    {
                //        var ssrResult = result as SpeechRecognitionResult;
                //        return ssrResult.Text;
                //    });

                //    text.PipeTo(python.In);
                //}


                // Create a data store to log the data to if necessary. A data store is necessary
                // only if output logging or live visualization are enabled.
                var dataStore = CreateDataStore(pipeline, outputLogPath, showLiveVisualization);

                // For disk logging or live visualization only
                if (dataStore != null)
                {
                    // Log the microphone audio and recognition results
                    audioInput.Write($"{Program.AppName}.MicrophoneAudio", dataStore);
                    finalResults.Write($"{Program.AppName}.FinalRecognitionResults", dataStore);
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
                     new GrammarInfo() { Name = Program.AppName, FileName = @"Resources\CuratedGrammar.grxml" }
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
            Console.WriteLine($"Pipeline execution completed with {e.Errors.Count} errors");

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
                        var gw = new GrammarWriter();
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
