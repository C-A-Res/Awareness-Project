using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NU.Kiosk
{
    using Microsoft.Psi;
    using Microsoft.Psi.Audio;
    using Microsoft.Psi.Data;
    using Microsoft.Psi.Speech;
    using Microsoft.Psi.Visualization.Client;
    using NU.Kqml;

    class Program
    {
        private static string AppName = "Kiosk";

        static void Main(string[] args)
        {
            Console.WriteLine("Starting");

            using (Pipeline pipeline = Pipeline.Create())
            {
                bool usingKqml = false;
                string facilitatorIP = null;
                int facilitatorPort = -1;
                int localPort = -1;
                if (args.Length > 0)
                {
                    if (args.Length < 3)
                    {
                        Console.WriteLine("Usage for running with a facilitator: \nKioskMain facilitatorIP facilitatorPort localPort");
                        return;
                    }
                    usingKqml = true;

                    facilitatorIP = args[0];
                    facilitatorPort = int.Parse(args[1]);
                    localPort = int.Parse(args[2]);
                }

                bool showLiveVisualization = false;
                string inputLogPath = null;
                string outputLogPath = null;

                // Needed only for live visualization
                DateTime startTime = DateTime.Now;

                // Use either live audio from the microphone or audio from a previously saved log
                IProducer<AudioBuffer> audioInput = setupAudioInput(pipeline, inputLogPath, ref startTime);

                // Create our webcam
                MediaCapture webcam = new MediaCapture(this.pipeline, 640, 480, 10);

                FaceCasClassifier f = new FaceCasClassifier();

                Console.WriteLine("Load classifier");
                Console.WriteLine(f);

                // Bind the webcam's output to our display image.
                // The "Do" operator is executed on each sample from the stream (webcam.Out), which are the images coming from the webcam
                webcam.Out.ToGrayViaOpenCV(f).Do(
                    (img, e) =>
                    {
                        string mouthOpen = "Close";
                        if ((Math.Abs(DisNose) / (4 * Math.Abs(DisLipMiddle))) < 1)
                        {
                            mouthOpen = "Open";
                        }
                        else
                        {
                            mouthOpen = "Close";
                        }
                        Console.WriteLine(Math.Abs(DisLipMiddle) + " " + Math.Abs(DisLipRight) + " " + Math.Abs(DisLipLeft) + " " + (Math.Abs(DisNose) / (4 * Math.Abs(DisLipMiddle))) + " " + mouthOpen);
                        return mouthOpen;
                    });

                IProducer<bool> mouthSignal = null;
                var mouthAndSpeech = mouthSignal.Join(audioInput);

                SystemSpeechRecognizer recognizer = setupSpeechRecognizer(pipeline);

                // Subscribe the recognizer to the input audio
                mouthAndSpeech.Where(x => x.Item1).Select(y => {
                    return y.Item2;
                }).PipeTo(recognizer);
                //audioInput.PipeTo(recognizer);

                // Partial and final speech recognition results are posted on the same stream. Here
                // we use Psi's Where() operator to filter out only the final recognition results.
                var finalResults = recognizer.Out.Where(result => result.IsFinal);

                // Print the recognized text of the final recognition result to the console.
                finalResults.Do(result =>
                {
                    var ssrResult = result as SpeechRecognitionResult;
                    Console.WriteLine($"{ssrResult.Text} (confidence: {ssrResult.Confidence})");
                });

                // Get just the text from the Speech Recognizer.  We may want to add another filter to only get text if confidence > 0.8
                var text = finalResults.Select(result =>
                {
                    var ssrResult = result as SpeechRecognitionResult;
                    return ssrResult.Text;
                });

                // Setup KQML connection to Companion
                SocketStringConsumer kqml = null;
                if (usingKqml)
                {
                    kqml = new SocketStringConsumer(pipeline, facilitatorIP, facilitatorPort, localPort);
                    
                    text.PipeTo(kqml.In)
                }

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

                if (kqml != null) kqml.Stop();
            }
        }

        private static SystemSpeechRecognizer setupSpeechRecognizer(Pipeline pipeline)
        {
            // Create System.Speech recognizer component
            return new SystemSpeechRecognizer(
                pipeline,
                new SystemSpeechRecognizerConfiguration()
                {
                    Language = "en-US",

                    Grammars = new GrammarInfo[]
                {
                        new GrammarInfo() { Name = Program.AppName, FileName = "SampleGrammar.grxml" }
                }
                });
        }

        private static IProducer<AudioBuffer> setupAudioInput(Pipeline pipeline, string inputLogPath, ref DateTime startTime)
        {
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

            return audioInput;
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
