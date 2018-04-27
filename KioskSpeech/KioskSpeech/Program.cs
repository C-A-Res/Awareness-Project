// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="Microsoft Corporation">
//   Copyright (C) Microsoft Corporation.  All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Microsoft.Psi.Samples.SpeechSample
{
    using System;
    using System.IO;
    using System.Linq;
    using Microsoft.Psi.Audio;
    using Microsoft.Psi.CognitiveServices.Speech;
    using Microsoft.Psi.Data;
    using Microsoft.Psi.MicrosoftSpeech;
    using Microsoft.Psi.Speech;
    using Microsoft.Psi.Visualization.Client;
    using System.Speech.Recognition;
    
    public static class Program
    {
        private const string AppName = "PsiSpeechSample";

        private const string LogPath = @"..\..\..\Videos\" + Program.AppName;

        // Component that serves as connection to Python
        //static WebSocketStringConsumer python = null;
        static SocketStringConsumer python = null;

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

            //while (!exit)
            //{
            //    Console.WriteLine("================================================================================");
            //    Console.WriteLine("                               Psi Speech Sample");
            //    Console.WriteLine("================================================================================");
            //    Console.WriteLine("1) Speech-To-Text using System.Speech recognizer (using SampleGrammar.grxml)");
            //    Console.WriteLine("2) Speech-To-Text using Bing speech recognizer");
            //    Console.WriteLine("3) Speech-To-Text using Microsoft speech recognizer (using SampleGrammar.grxml)");
            //    Console.WriteLine("4) Toggle audio source (currently {0})", inputLogPath == null ? "LIVE" : $"from logged data {inputLogPath}");
            //    Console.WriteLine("5) Toggle logging to disk (currently {0})", outputLogPath == null ? "OFF" : $"logging to {outputLogPath}");
            //    Console.WriteLine("6) Toggle live visualization (currently {0})", showLiveVisualization ? "ON" : "OFF");
            //    Console.WriteLine("Q) QUIT");
            //    Console.Write("Enter selection: ");
            //    ConsoleKey key = Console.ReadKey().Key;
            //    Console.WriteLine();

            //    exit = false;
            //    switch (key)
            //    {
            //        case ConsoleKey.D1:
            //            // Demonstrate the use of the SystemSpeechRecognizer component
            //            RunSystemSpeech(outputLogPath, inputLogPath, showLiveVisualization);
            //            break;

            //        case ConsoleKey.D2:
            //            // Bing speech service requires a valid subscription key
            //            if (GetSubscriptionKey())
            //            {
            //                // Demonstrate the use of the BingSpeechRecognizer component
            //                RunBingSpeech(outputLogPath, inputLogPath, showLiveVisualization);
            //            }

            //            break;

            //        case ConsoleKey.D3:
            //            // Demonstrate the use of the MicrosoftSpeechRecognizer component
            //            RunMicrosoftSpeech(outputLogPath, inputLogPath, showLiveVisualization);
            //            break;

            //        case ConsoleKey.D4:
            //            // Toggle between using live audio and logged audio as input
            //            inputLogPath = inputLogPath == null ? Program.LogPath : null;
            //            break;

            //        case ConsoleKey.D5:
            //            // Toggle output logging
            //            outputLogPath = outputLogPath == null ? Program.LogPath : null;
            //            break;

            //        case ConsoleKey.D6:
            //            // Toggle live visualization in PsiStudio
            //            showLiveVisualization = !showLiveVisualization;
            //            break;

            //        case ConsoleKey.Q:
            //            exit = true;
            //            // close WebSocket
            //            if (python != null) python.Stop();
            //            break;

            //        default:
            //            exit = false;
            //            break;
            //    }
            //}
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
                var recognizer = new SystemSpeechRecognizer(
                    //pipeline,
                    //new SystemSpeechRecognizerConfiguration()
                    //{
                    //    Language = "en-US",
                    //    Grammars = new DictationGrammar();
                    //    //Grammars = new GrammarInfo[]
                    //    //{
                    //    //    new GrammarInfo() { Name = Program.AppName, FileName = "SampleGrammar.grxml" }
                    //    //}
                    //});
                    pipeline);

                
                // Subscribe the recognizer to the input audio
                audioInput.PipeTo(recognizer);

                // Partial and final speech recognition results are posted on the same stream. Here
                // we use Psi's Where() operator to filter out only the final recognition results.
                var finalResults = recognizer.Out.Where(result => result.IsFinal);

                // Print the recognized text of the final recognition result to the console.
                finalResults.Do(result =>
                {
                    var ssrResult = result as SystemSpeechRecognitionResult;
                    Console.WriteLine($"{ssrResult.Text} (confidence: {ssrResult.Confidence})");
                });

                // Also send data across web socket.
                //finalResults.Do(result =>
                //{
                //    var ssrResult = result as SystemSpeechRecognitionResult;
                //    sendToSocket(ssrResult.Text);
                //});

                //python = new WebSocketStringConsumer(pipeline, 9001);
                python = new SocketStringConsumer(pipeline, facilitatorIP, facilitatorPort, localPort);
                
                var text = finalResults.Select(result =>
                {
                    var ssrResult = result as SystemSpeechRecognitionResult;
                    return ssrResult.Text;
                });

                text.PipeTo(python.In);

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

                // Ignore this block if live visualization is not enabled
                if (showLiveVisualization)
                {
                    // Create the visualization client
                    var visualizationClient = new VisualizationClient();

                    // Clear all data if the visualizer is already open
                    visualizationClient.ClearAll();

                    // Create the visualization client to visualize live data
                    visualizationClient.SetLiveMode(startTime);

                    // Plot the microphone audio stream in a new panel
                    visualizationClient.AddTimelinePanel();
                    audioInput.Show(visualizationClient);

                    // Plot the recognition results in a new panel
                    visualizationClient.AddTimelinePanel();
                    finalResults.Show(visualizationClient);
                }

                // Register an event handler to catch pipeline errors
                pipeline.PipelineCompletionEvent += PipelineCompletionEvent;

                // Run the pipeline
                pipeline.RunAsync();

                Console.WriteLine("Press any key to exit...");
                Console.ReadKey(true);
            }
        }
        
        /// <summary>
        /// Builds and runs a speech recognition pipeline using the .NET System.Speech recognizer and a set of fixed grammars.
        /// This sample requires that the Microsoft Speech Platform runtime and language pack be installed in order to run.
        /// See https://www.microsoft.com/en-us/download/details.aspx?id=27225
        /// and https://www.microsoft.com/en-us/download/details.aspx?id=27224
        /// </summary>
        /// <param name="outputLogPath">The path under which to write log data.</param>
        /// <param name="inputLogPath">The path from which to read audio input data.</param>
        /// <param name="showLiveVisualization">A flag indicating whether to display live data in PsiStudio as the pipeline is running.</param>
        public static void RunMicrosoftSpeech(string outputLogPath = null, string inputLogPath = null, bool showLiveVisualization = true)
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

                // Create a configuration object for the MicrosoftSpeechRecognizer component. This
                // allows the component-specific configuration parameters to be specified.
                var configuration = new MicrosoftSpeechRecognizerConfiguration()
                {
                    // The recognition language (the corresponding language pack must be pre-installed)
                    Language = "en-US",

                    // The MicrosoftSpeech recognizer uses CFG grammars that conform to the SRGS 1.0 specification
                    Grammars = new GrammarInfo[]
                    {
                        new GrammarInfo() { Name = Program.AppName, FileName = "SampleGrammar.grxml" }
                    }
                };

                try
                {
                    // Create MicrosoftSpeech recognizer component with the above configuration
                    var recognizer = new MicrosoftSpeechRecognizer(pipeline, configuration);

                    // Subscribe the recognizer to the input audio
                    audioInput.PipeTo(recognizer);

                    // Partial and final speech recognition results are posted on the same stream. Here
                    // we use Psi's Where() operator to filter out only the final recognition results.
                    var finalResults = recognizer.Out.Where(result => result.IsFinal);

                    // Print the recognized text of the final recognition result to the console.
                    finalResults.Do(result =>
                    {
                        var ssrResult = result as MicrosoftSpeechRecognitionResult;
                        Console.WriteLine($"{ssrResult.Text} (confidence: {ssrResult.Confidence})");
                    });

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

                    // Ignore this block if live visualization is not enabled
                    if (showLiveVisualization)
                    {
                        // Create the visualization client
                        var visualizationClient = new VisualizationClient();

                        // Clear all data if the visualizer is already open
                        visualizationClient.ClearAll();

                        // Create the visualization client to visualize live data
                        visualizationClient.SetLiveMode(startTime);

                        // Plot the microphone audio stream in a new panel
                        visualizationClient.AddTimelinePanel();
                        audioInput.Show(visualizationClient);

                        // Plot the recognition results in a new panel
                        visualizationClient.AddTimelinePanel();
                        finalResults.Show(visualizationClient);
                    }

                    // Register an event handler to catch pipeline errors
                    pipeline.PipelineCompletionEvent += PipelineCompletionEvent;

                    // Run the pipeline
                    pipeline.RunAsync();

                    // The file SampleGrammar.grxml defines a grammar to transcribe numbers
                    Console.WriteLine("Say any number between 0 and 100");
                }
                catch (FileNotFoundException error)
                {
                    if (error.FileName.Contains("Microsoft.Speech"))
                    {
                        Console.WriteLine(
                            "In order to use the MicrosoftSpeech recognizer, the Microsoft Speech Platform Runtime needs to be installed first. " +
                            "Download link available from https://www.microsoft.com/en-us/download/details.aspx?id=27225 " +
                            "(currently only x64 is supported).");
                    }
                }
                catch (ArgumentException error)
                {
                    if (error.ParamName == "culture")
                    {
                        Console.WriteLine(
                            "In order to use the MicrosoftSpeech recognizer, the appropriate runtime language pack ({0}) needs to be installed. " +
                            "Download link available from https://www.microsoft.com/en-us/download/details.aspx?id=27224", configuration.Language);
                    }
                }

                Console.WriteLine("Press any key to exit...");
                Console.ReadKey(true);
            }
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
