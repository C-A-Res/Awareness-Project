namespace NU.Kiosk
{
    using System;
    using System.IO;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Kinect;
    using Microsoft.Psi;
    using Microsoft.Psi.Kinect.v1;
    using Microsoft.Psi.Imaging;
    using Microsoft.Psi.Media;
    using Microsoft.Psi.Audio;
    using Microsoft.Psi.Data;
    using Microsoft.Psi.Speech;
    using Microsoft.Psi.Visualization.Client;
    
    public static class KinectKioskProgram
    {
        static string AppName = "Kiosk";

        static TimeSpan _100ms = TimeSpan.FromSeconds(0.1);
        static TimeSpan _500ms = TimeSpan.FromSeconds(0.5);

        public static void Main(string[] args)
        {
            bool detected = false;
            bool usingKqml = true;
            // string facilitatorIP = "10.105.237.105";
            string facilitatorIP = "127.0.0.1";
            int facilitatorPort = 9000;
            int localPort = 8090;


            Console.WriteLine("Starting Kinect-based Kiosk.  Verify that Kinect is setup before continuing");

            using (Pipeline pipeline = Pipeline.Create())
            {
                // Components
                Microsoft.Psi.Kinect.v1.KinectSensor kinectSensor = new Microsoft.Psi.Kinect.v1.KinectSensor(pipeline);

                Microsoft.Psi.Kinect.v1.SkeletonFaceTracker faceTracker = new Microsoft.Psi.Kinect.v1.SkeletonFaceTracker(pipeline, kinectSensor.kinectSensor);

                var speechDetector = new SystemVoiceActivityDetector(pipeline);

                var recognizer = Speech.Program.CreateSpeechRecognizer(pipeline);

                var synthesizer = Speech.Program.CreateSpeechSynthesizer(pipeline);

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

                var mouthOpen = mouthOpenAsFloat.Hold(0.1);
                // mouthOpen.Do(x => Console.Write($"{x} "));

                // Not using speech detector for now
                //kinectSensor.Audio.PipeTo(speechDetector);
                //var mouthAndSpeechDetector = speechDetector.Join(mouthOpen, _100ms).Select((t, e) => t.Item1 && t.Item2);

                kinectSensor.Audio.Join(mouthOpen, _500ms).Where(result => result.Item2).Select(pair => {
                    return pair.Item1;
                    }
                ).PipeTo(recognizer);
                //kinectSensor.Audio.PipeTo(recognizer);

                var finalResults = recognizer.Out.Where(result => result.IsFinal);

                /*
                finalResults.Join(mouthOpen, _500ms).Do(pair =>
                {
                    var ssrResult = pair.Item1 as SpeechRecognitionResult;
                    Console.WriteLine($"{ssrResult.Text} (confidence: {ssrResult.Confidence}) (mouthOpen: {pair.Item2})");
                });
                */

                
                var text = finalResults.Join(mouthOpen, _500ms).Select(pair =>  // Need to add a Where Item2, but skipping for now
                {
                    var ssrResult = pair.Item1 as SpeechRecognitionResult;
                    Console.WriteLine($"{ssrResult.Text} (confidence: {ssrResult.Confidence}) (mouthOpen: {pair.Item2})");
                    return ssrResult.Text;
                });

                NU.Kqml.SocketStringConsumer kqml = null;
                if (usingKqml)
                {
                    Console.WriteLine("Setting up connection to Companion");
                    int facilitatorPort_num = Convert.ToInt32(facilitatorPort);
                    int localPort_num = Convert.ToInt32(localPort);
                    Console.WriteLine("Your Companion IP address is: " + facilitatorIP);
                    Console.WriteLine("Your Companion port is: " + facilitatorPort);
                    Console.WriteLine("Your local port is: " + localPort);

                    kqml = new NU.Kqml.SocketStringConsumer(pipeline, facilitatorIP, facilitatorPort_num, localPort_num);

                    text.PipeTo(kqml.In);
                    kqml.Out.Do(x => Console.WriteLine(x));
                    kqml.Out.PipeTo(synthesizer);
                } else
                {
                    text.PipeTo(synthesizer);
                    //synthesizer.SpeakStarted;
                    //synthesizer.SpeakCompleted;
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
