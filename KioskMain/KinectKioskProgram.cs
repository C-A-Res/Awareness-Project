﻿namespace NU.Kiosk
{
    using System;
    using System.IO;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using Microsoft.Kinect;
    using Microsoft.Psi;
    using Microsoft.Psi.Kinect.v1;
    using Microsoft.Psi.Imaging;
    using Microsoft.Psi.Media;
    using Microsoft.Psi.Audio;
    using Microsoft.Psi.Data;
    using Microsoft.Psi.Speech;
    using Microsoft.Psi.Visualization.Client;
    using WebSocketSharp.Server;
    using System.Threading.Tasks;
    using System.Timers;

    public static class KinectKioskProgram
    {
        static string AppName = "Kiosk";

        static TimeSpan _100ms = TimeSpan.FromSeconds(0.1);
        static TimeSpan _300ms = TimeSpan.FromSeconds(0.1);
        static TimeSpan _500ms = TimeSpan.FromSeconds(2.5);

        static bool isAccepting = true;
        static System.Timers.Timer timer = new System.Timers.Timer(1);
        
        private static void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            isAccepting = true;
        }

        private static void InitTimer()
        {
            timer = new System.Timers.Timer(10000);
            timer.Elapsed += OnTimedEvent;
        }

        private static void StartTimer()
        {
            timer.Enabled = true;
            Console.WriteLine("[StopTimer] Timer Enabled.");
        }

        private static void StopTimer()
        {
            timer.Enabled = false;
            Console.WriteLine("[StopTimer] Timer Disabled");
        }
        
        public static void setAccepting()
        {
            Console.WriteLine("[Main: setAccepting] Yes! Accepting!");
            isAccepting = true;
            StopTimer();
        }

        public static void setNotAccepting()
        {
            Console.WriteLine("[Main: setAccepting] No! Not accepting!");
            isAccepting = false;
            StartTimer();
        }

        public static void Main(string[] args)
        {
            bool detected = false;
            bool usingKqml = true;
            string facilitatorIP = args[0]; 
            int facilitatorPort = int.Parse(args[1]); 
            int localPort = int.Parse(args[2]);
            InitTimer();

            Console.WriteLine("Starting Kinect-based Kiosk.  Verify that Kinect is setup before continuing");

            using (Pipeline pipeline = Pipeline.Create())
            {
                // Components
                Microsoft.Psi.Kinect.v1.KinectSensor kinectSensor = new Microsoft.Psi.Kinect.v1.KinectSensor(pipeline);

                Microsoft.Psi.Kinect.v1.SkeletonFaceTracker faceTracker = new Microsoft.Psi.Kinect.v1.SkeletonFaceTracker(pipeline, kinectSensor.kinectSensor);

                var speechDetector = new SystemVoiceActivityDetector(pipeline);

                var recognizer = Speech.Program.CreateSpeechRecognizer(pipeline);

                var merger = new Speech.SpeechMerger(pipeline);

                var synthesizer = Speech.Program.CreateSpeechSynthesizer(pipeline);

                NU.Kqml.SocketStringConsumer kqml = null;

                NU.Kqml.KioskInputTextPreProcessor preproc = new NU.Kqml.KioskInputTextPreProcessor(pipeline, (SystemSpeechRecognizer)recognizer);

                KioskUI.KioskUI ui = new KioskUI.KioskUI(pipeline);

                // Wiring together the components
                var joinedFrames = kinectSensor.ColorImage.Join(kinectSensor.DepthImage).Join(kinectSensor.Skeletons);

                joinedFrames.PipeTo(faceTracker);

                var mouthOpenAsFloat = faceTracker.FaceDetected.Select((bool x) =>
                {
                    if (!detected)
                    {
                        Console.WriteLine("Face found");
                        detected = true;
                    }
                    return x ? 1.0 : 0.0;
                });

                // Hold faceDetected to true for a while, even after face is gone
                var faceDetected = mouthOpenAsFloat.Hold(0.1, 0.05);
                faceDetected.PipeTo(ui.FaceDetected);

                // Send audio to recognizer if face is detected and ready to accept more input    
                //kinectSensor.Audio.Join(faceDetected, _300ms).Where(result => result.Item2 && isAccepting).Select(pair => {
                //    return pair.Item1;
                //}).PipeTo(recognizer);
                kinectSensor.Audio.Join(faceDetected, _300ms).Pair(synthesizer.StateChanged).Where(result => result.Item2 && result.Item3.State == System.Speech.Synthesis.SynthesizerState.Ready).Select(pair => {
                    return pair.Item1;
                }).PipeTo(recognizer);

                // Get final results of speech recognition
                var finalResults = recognizer.Out.Where(result => result.IsFinal);

                var recognitionResult = finalResults.Select(r =>  // Need to add a Where Item2, but skipping for now
                {
                    var ssrResult = r as IStreamingSpeechRecognitionResult;
                    Console.WriteLine($"{ssrResult.Text} (confidence: {ssrResult.Confidence})");
                    return ssrResult;
                });

                if (usingKqml)
                {
                    Console.WriteLine("Setting up connection to Companion");
                    int facilitatorPort_num = Convert.ToInt32(facilitatorPort);
                    int localPort_num = Convert.ToInt32(localPort);
                    Console.WriteLine("Your Companion IP address is: " + facilitatorIP);
                    Console.WriteLine("Your Companion port is: " + facilitatorPort);
                    Console.WriteLine("Your local port is: " + localPort);

                    // setup interface to Companion
                    kqml = new NU.Kqml.SocketStringConsumer(pipeline, facilitatorIP, facilitatorPort_num, localPort_num);

                    // Send user input to preprocess
                    recognitionResult.PipeTo(preproc.In);

                    // Set accepting flag based on preprocessor output
                    var non_trivial_result = preproc.Out.Where(x => {
                        if (x == null)
                        {
                            //setAccepting();
                            return false;
                        }
                        else
                        {
                            //setNotAccepting();
                            return true;
                        }
                    });

                    preproc.AutoResponse.PipeTo(merger.OtherIn);

                    // Send processed user input to Companion and UI
                    non_trivial_result.PipeTo(kqml.In);
                    non_trivial_result.PipeTo(ui.UserInput);
                    non_trivial_result.PipeTo(merger.LastOut);

                    // Get response from Companion and forward to UI and synthesizer
                    kqml.Out.Do(x => Console.WriteLine(x));
                    kqml.Out.PipeTo(merger.In);
                    merger.Out.PipeTo(ui.CompResponse);
                    merger.Out.PipeTo(synthesizer);

                    // When speaking complete, ready to accept more input
                    //synthesizer.SpeakCompleted.Delay(_500ms).Do(x => setAccepting());
                   
                }
                else
                {
                    Console.WriteLine("Status: Not using KQML");
                   
                    recognitionResult.PipeTo(preproc.In);

                    var non_trivial_result = preproc.Out.Where(x => {
                        if (x == null)
                        {
                            setAccepting();
                            return false;
                        }
                        else
                        {
                            setNotAccepting();
                            return true;
                        }
                    });
                    non_trivial_result.PipeTo(ui.UserInput);
                    var delayed = non_trivial_result.Select(result =>
                    {
                        Thread.Sleep(3000);
                        return result;
                    });
                    TimeSpan the_wait = TimeSpan.FromSeconds(13.0);
                    delayed.PipeTo(ui.CompResponse);
                    delayed.PipeTo(synthesizer);
                    synthesizer.SpeakCompleted.Do(x => setAccepting());

                }

                // Setup psi studio visualizations
                //SetupDataStore(pipeline, @"..\..\..\Videos\" + AppName, "", true, kinectSensor, faceTracker, finalResults);

                // Register an event handler to catch pipeline errors
                pipeline.PipelineCompletionEvent += PipelineCompletionEvent;

                // Run the pipeline
                pipeline.RunAsync();
                
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

        public static void SetupDataStore(Pipeline pipeline, string outputStorePath, string inputStorePath, bool showLive, 
            Microsoft.Psi.Kinect.v1.KinectSensor kinectSensor, SkeletonFaceTracker faceTracker, IProducer<IStreamingSpeechRecognitionResult> speechRecog)
        {
            string outputLogPath = null;

            if (outputStorePath != null && outputStorePath != "" )
            {
                if (!Directory.Exists(outputStorePath))
                {
                    Directory.CreateDirectory(outputStorePath);
                }
                outputLogPath = outputStorePath;
            }
            Console.WriteLine(outputLogPath == null);

            string inputLogPath = null;

            if (inputStorePath != null && inputStorePath != "" && Directory.Exists(inputStorePath))
            {
                inputLogPath = inputStorePath;
            }
            Console.WriteLine(inputLogPath == null);

            // Needed only for live visualization
            DateTime startTime = DateTime.Now;

            // Create a data store to log the data to if necessary. A data store is necessary
            // only if output logging or live visualization are enabled.
            Console.WriteLine(outputLogPath == null);
            var dataStore = CreateDataStore(pipeline, outputLogPath, showLive);
            Console.WriteLine(dataStore == null);
            Console.WriteLine("dataStore is empty");
            // For disk logging or live visualization only
            if (dataStore != null)
            {
                // Log the microphone audio and recognition results
                //kinectSensor.ColorImage.Write("Kiosk.KinectSensor.ColorImage", dataStore);
                kinectSensor.Audio.Write("Kiosk.KinectSensor.Audio", dataStore);
                //faceTracker.Write("Kiosk.FaceTracker", dataStore);
                speechRecog.Write($"Kiosk.FinalRecognitionResults", dataStore);

                Console.WriteLine("Stored the data here! ");
            }

            // Ignore this block if live visualization is not enabled
            if (showLive)
            {
                // Create the visualization client
                var visualizationClient = new VisualizationClient();

                // Clear all data if the visualizer is already open
                visualizationClient.ClearAll();

                // Create the visualization client to visualize live data
                visualizationClient.SetLiveMode(startTime);

                // Plot the video stream in a new panel
                //visualizationClient.AddXYPanel();
                //kinectSensor.ColorImage.Show(visualizationClient);

                // Plot the microphone audio stream in a new panel
                //visualizationClient.AddTimelinePanel();
                //kinectSensor.Audio.Show(visualizationClient);

                // Plot the recognition results in a new panel
                //visualizationClient.AddTimelinePanel();
                //faceTracker.Show(visualizationClient);

                // Plot the recognition results in a new panel
                //visualizationClient.AddTimelinePanel();
                //speechRecog.Show(visualizationClient);

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
            string dataStoreName = (outputLogPath != null) ? AppName :
                showLiveVisualization ? "Temp-" + DateTime.Now.ToString("yyyyMMdd-hhmmss") : null;

            // Create the store only if it is needed (logging to disk or live visualization).
            return (dataStoreName != null) ? Store.Create(pipeline, dataStoreName, outputLogPath) : null;
        }
    }
}
