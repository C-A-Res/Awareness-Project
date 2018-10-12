namespace NU.Kiosk
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
    using NU.Kqml;
    using WebSocketSharp.Server;
    using System.Threading.Tasks;
    using System.Timers;
    using log4net;

    using NU.Kiosk.SharedObject;
    using log4net;
    using log4net.Config;
    using System.Reflection;
    using Newtonsoft.Json;

    public static class KinectKioskProgram
    {
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        static string AppName = "Kiosk";

        static TimeSpan _100ms = TimeSpan.FromSeconds(0.1);
        static TimeSpan _300ms = TimeSpan.FromSeconds(0.3);
        static TimeSpan _500ms = TimeSpan.FromSeconds(0.5);
        static TimeSpan _2500ms = TimeSpan.FromSeconds(2.5);

        public static void Main(string[] args)
        {
            // Do not change these values if you want to run under a different config
            // Use command line arguments, that's why they are there!
            bool usingDragon = false;
            bool usingKqml = false;
            bool usingKinect = true;

            string facilitatorIP = "";
            int facilitatorPort = 0;
            int localPort = 0;

            int i = 0;
            while (i < args.Length)
            {
                var arg0 = args[i];
                if (arg0 == "-companion")
                {
                    usingKqml = true;
                    facilitatorIP = args[i + 1];
                    facilitatorPort = int.Parse(args[i + 2]);
                    localPort = int.Parse(args[i + 3]);
                } else if (arg0 == "-nokinect" || arg0 == "-nok")
                {
                    usingKinect = false;
                } else if (arg0 == "-dragon")
                {
                    usingDragon = true;
                }
                i++;
            }

            //// uncomment these to debug speech recognition
            //usingKqml = false;
            //usingKinect = false;

            XmlConfigurator.Configure();

            if (usingDragon)
            {
                dragon(facilitatorIP, facilitatorPort, localPort, usingKqml, usingKinect); 
            } else
            {
                not_dragon(facilitatorIP, facilitatorPort, localPort, usingKqml, usingKinect);
            }
        }

        private static void dragon(string facilitatorIP, int facilitatorPort, int localPort, bool usingKqml, bool usingKinect)
        {
            bool detected = false;
            bool quit = false;
            using (Pipeline pipeline = Pipeline.Create())
            {
                _log.Info("Using Dragon.");

                #region Component declarations
                var dialog = new Speech.DialogManager(pipeline, true);//new Speech.DragonDialogManager(pipeline);
                SocketStringConsumer kqml = null;
                var preproc = new Speech.DragonInputTextProcessor(pipeline);
                var responder = new Speech.Responder(pipeline);
                KioskUI.KioskUI ui = new KioskUI.KioskUI(pipeline, usingKinect);
                Speech.IntervalBooleanProducer intervalboolean;

                Microsoft.Psi.Kinect.v1.KinectSensor kinectSensor = null;
                Microsoft.Psi.Kinect.v1.SkeletonFaceTracker faceTracker = null;
                #endregion


                #region Wiring together the components

                // send wakeup signal to dialog
                ui.Wake.PipeTo(dialog.WakeUp);

                // update UI with dialog state
                dialog.StateChanged.PipeTo(ui.DialogStateChanged);

                // update Recognizer with dialog state
                //dialog.StateChanged.Select(state => state == "listening").PipeTo(recognizer.isAcceptingData);

                // Combine all the kinect image outputs
                // This might be doable in a single join
                if (usingKinect)
                {
                    _log.Info("Starting Kinect-based Kiosk.  Verify that Kinect is setup before continuing");
                    kinectSensor = new Microsoft.Psi.Kinect.v1.KinectSensor(pipeline);
                    faceTracker = new Microsoft.Psi.Kinect.v1.SkeletonFaceTracker(pipeline, kinectSensor.kinectSensor);
                    var joinedFrames = kinectSensor.ColorImage.Join(kinectSensor.DepthImage).Join(kinectSensor.Skeletons);
                    joinedFrames.PipeTo(faceTracker);
                    var mouthOpenAsFloat = faceTracker.FaceDetected.Select((bool x) =>
                    {
                        if (!detected)
                        {
                            _log.Info("Started looking for a face...");
                            detected = true;
                        }
                        return x ? 1.0 : 0.0;
                    });

                    // Hold faceDetected to true for a while, even after face is gone
                    var faceDetected = mouthOpenAsFloat.Hold(0.1, 0.05);
                    faceDetected.PipeTo(dialog.FaceDetected);
                    faceDetected.PipeTo(ui.FaceDetected);
                } else
                {
                    _log.Info("Using push-to-talk: Press the spacebar to open the mic, and any key to close the mic, and q to quit");
                    var keys = Generators.Sequence(pipeline, Keys(), TimeSpan.FromMilliseconds(10));
                    var pushToTalk = keys.Select(k =>
                    {
                        if (k == ConsoleKey.Spacebar)
                        {
                            return true;
                        }
                        if (k == ConsoleKey.Q)
                        {
                            quit = true;
                            return false;
                        }
                        return false;
                    }).Repeat(Generators.Timer(pipeline, TimeSpan.FromMilliseconds(100)));

                    var mouthOpenAsFloat = pushToTalk.Out.Select((bool x) =>
                    {
                        if (!detected)
                        {
                            _log.Info("Started looking for a face...");
                            detected = true;
                        }
                        return x ? 1.0 : 0.0;
                    });

                    // Hold faceDetected to true for a while, even after face is gone
                    var faceDetected = mouthOpenAsFloat.Hold(0.1, 0.01);
                    faceDetected.PipeTo(dialog.FaceDetected);
                    faceDetected.PipeTo(ui.FaceDetected);
                }

                //done; recognitionResult.PipeTo(preproc.In);
                ui.TouchInput.PipeTo(preproc.UiInput);

                // Send processed user input to Companion and UI
                preproc.PipeTo(dialog.UserInput);
                dialog.UserOutput.Select(u => { return u.Text; }).PipeTo(ui.UserInput);

                // Get response from Companion and forward to UI and synthesizer
                dialog.ActionOutput.PipeTo(ui.ActionCommand);
                //dialog.CompOutput.PipeTo(ui.CompResponse);
                //done; already handled in handleCompInput. dialog.TextOutput.PipeTo(synthesizer);

                dialog.UserOutput.PipeTo(responder.UserInput);
                responder.ActionResponse.PipeTo(dialog.CompInput);

                if (usingKqml)
                {
                    _log.Info("Setting up connection to Companion");
                    int facilitatorPort_num = Convert.ToInt32(facilitatorPort);
                    int localPort_num = Convert.ToInt32(localPort);
                    _log.Info("Your Companion IP address is: " + facilitatorIP);
                    _log.Info("Your Companion port is: " + facilitatorPort);
                    _log.Info("Your local port is: " + localPort);

                    // setup interface to Companion
                    kqml = new SocketStringConsumer(pipeline, facilitatorIP, facilitatorPort_num, localPort_num);

                    responder.KQMLRequest.PipeTo(kqml);
                    kqml.PipeTo(responder.KQMLResponse);
                }
                else
                {
                    // echo
                    responder.KQMLRequest.Select(x => new NU.Kiosk.SharedObject.Action("psikbSayText", x)).PipeTo(responder.KQMLResponse);
                }

                #endregion

                // Setup psi studio visualizations
                //SetupDataStore(pipeline, @"..\..\..\Videos\" + AppName, "", true, kinectSensor, faceTracker, finalResults);

                // Register an event handler to catch pipeline errors
                pipeline.PipelineCompletionEvent += PipelineCompletionEvent;

                // Run the pipeline
                pipeline.RunAsync();

                while (!quit)
                {
                    Thread.Sleep(100);
                }

                //_log.Info($"Press any key to exit...");
                //Console.ReadKey(true);

            }
        }

        private static void not_dragon(string facilitatorIP, int facilitatorPort, int localPort, bool usingKqml, bool usingKinect)
        {
            bool detected = false;
            bool quit = false;
            using (Pipeline pipeline = Pipeline.Create())
            {
                _log.Info("Not using Dragon.");

                #region Component declarations
                NU.Kiosk.Speech.GrammarRecognizerWrapper recognizer = new NU.Kiosk.Speech.GrammarRecognizerWrapper(pipeline);

                var synthesizer = Speech.Program.CreateSpeechSynthesizer(pipeline);

                var dialog = new Speech.DialogManager(pipeline);

                SocketStringConsumer kqml = null;

                KioskInputTextPreProcessor preproc = new KioskInputTextPreProcessor(pipeline, recognizer);

                var responder = new Speech.Responder(pipeline);

                KioskUI.KioskUI ui = new KioskUI.KioskUI(pipeline);

                Speech.IntervalBooleanProducer intervalboolean;

                #endregion


                #region Wiring together the components

                // send wakeup signal to dialog
                ui.Wake.PipeTo(dialog.WakeUp);

                // update UI with dialog state
                dialog.StateChanged.PipeTo(ui.DialogStateChanged);

                // update Recognizer with dialog state
                dialog.StateChanged.Select(state => state == "listening").PipeTo(recognizer.isAcceptingData);

                // Combine all the kinect image outputs
                // This might be doable in a single join
                if (usingKinect)
                {
                    _log.Info("Starting Kinect-based Kiosk.  Verify that Kinect is setup before continuing");

                    Microsoft.Psi.Kinect.v1.KinectSensor kinectSensor = new Microsoft.Psi.Kinect.v1.KinectSensor(pipeline);

                    Microsoft.Psi.Kinect.v1.SkeletonFaceTracker faceTracker = new Microsoft.Psi.Kinect.v1.SkeletonFaceTracker(pipeline, kinectSensor.kinectSensor);

                    var joinedFrames = kinectSensor.ColorImage.Join(kinectSensor.DepthImage).Join(kinectSensor.Skeletons);

                    joinedFrames.PipeTo(faceTracker);
                    //joinedFrames.Do(x => Console.Write('.'));

                    var mouthOpenAsFloat = faceTracker.FaceDetected.Select((bool x) =>
                    {
                        if (!detected)
                        {
                            _log.Info("Started looking for a face...");
                            detected = true;
                        }
                        return x ? 1.0 : 0.0;
                    });

                    // Hold faceDetected to true for a while, even after face is gone
                    var faceDetected = mouthOpenAsFloat.Hold(0.1, 0.01);
                    faceDetected.PipeTo(dialog.FaceDetected);
                    faceDetected.PipeTo(ui.FaceDetected);
                    //faceDetected.Do(x => { if (x) { Console.Write(1); } else { Console.Write(0); } });

                    // Send audio to recognizer if face is detected and ready to accept more input    
                    kinectSensor.Audio.Join(faceDetected, _300ms).Where(result => result.Item2).Select(pair => {
                        return pair.Item1;
                    }).PipeTo(recognizer);
                }
                else
                {
                    _log.Info("Using push-to-talk: Press the spacebar to open the mic, and any key to close the mic, and q to quit");   
                    var keys = Generators.Sequence(pipeline, Keys(), TimeSpan.FromMilliseconds(10));
                    var pushToTalk = keys.Select(k =>
                    {
                        if (k == ConsoleKey.Spacebar)
                        {
                            return true;
                        }
                        if (k == ConsoleKey.Q)
                        {
                            quit = true;
                            return false;
                        }
                        return false;
                    }).Repeat(Generators.Timer(pipeline, TimeSpan.FromMilliseconds(100)));
                    
                    var mouthOpenAsFloat = pushToTalk.Out.Select((bool x) =>
                    {
                        if (!detected)
                        {
                            _log.Info("Started looking for a face...");
                            detected = true;
                        }
                        return x ? 1.0 : 0.0;
                    });

                    // Hold faceDetected to true for a while, even after face is gone
                    var faceDetected = mouthOpenAsFloat.Hold(0.1, 0.01);
                    faceDetected.PipeTo(dialog.FaceDetected);
                    faceDetected.PipeTo(ui.FaceDetected);
                    
                    // Create the AudioSource component to capture audio from the default device in 16 kHz 1-channel
                    // PCM format as required by both the voice activity detector and speech recognition components.
                    var audioInput = new AudioSource(pipeline, new AudioSourceConfiguration() { OutputFormat = WaveFormat.Create16kHz1Channel16BitPcm() });
                    audioInput.PipeTo(recognizer);
                }


                // Get final results of speech recognition
                var finalResults = recognizer.Out.Where(result => result.IsFinal);

                var recognitionResult = finalResults.Select(r =>  // Need to add a Where Item2, but skipping for now
                {
                    var ssrResult = r as IStreamingSpeechRecognitionResult;
                    _log.Info($"{ssrResult.Text} (confidence: {ssrResult.Confidence})");
                    return ssrResult;
                });


                // Send user input to preprocess
                recognitionResult.PipeTo(preproc.In);
                ui.TouchInput.PipeTo(preproc.UiInput);

                // Send processed user input to Companion and UI
                preproc.PipeTo(dialog.UserInput);
                dialog.UserOutput.Select(u => { return u.Text; }).PipeTo(ui.UserInput);

                // Get response from Companion and forward to UI and synthesizer
                dialog.ActionOutput.PipeTo(ui.ActionCommand);
                dialog.TextOutput.PipeTo(synthesizer);

                dialog.UserOutput.PipeTo(responder.UserInput);
                responder.ActionResponse.PipeTo(dialog.CompInput);

                synthesizer.StateChanged.Select(x =>
                {
                    SystemSpeechSynthesizer.StateChangedEventData data = x;
                    return data.State;
                }).PipeTo(dialog.SpeechSynthesizerState);

                if (usingKqml)
                {
                    _log.Info("Setting up connection to Companion");
                    int facilitatorPort_num = Convert.ToInt32(facilitatorPort);
                    int localPort_num = Convert.ToInt32(localPort);
                    _log.Info("Your Companion IP address is: " + facilitatorIP);
                    _log.Info("Your Companion port is: " + facilitatorPort);
                    _log.Info("Your local port is: " + localPort);

                    // setup interface to Companion
                    kqml = new SocketStringConsumer(pipeline, facilitatorIP, facilitatorPort_num, localPort_num);

                    responder.KQMLRequest.PipeTo(kqml);
                    kqml.PipeTo(responder.KQMLResponse);
                }
                else
                {
                    // echo
                    responder.KQMLRequest.Select(x => new NU.Kiosk.SharedObject.Action("psikiSayText", x)).PipeTo(responder.KQMLResponse);
                }

                #endregion

                // Setup psi studio visualizations
                //SetupDataStore(pipeline, @"..\..\..\Videos\" + AppName, "", true, kinectSensor, faceTracker, finalResults);

                // Register an event handler to catch pipeline errors
                pipeline.PipelineCompletionEvent += PipelineCompletionEvent;

                // Run the pipeline
                pipeline.RunAsync();

                while (!quit)
                {
                    Thread.Sleep(100);
                }
                //Console.WriteLine("Press any key to exit...");
                //Console.ReadKey(true);

            }
        }

        static IEnumerable<ConsoleKey> Keys()
        {
            while (true)
            {
                var key = Console.ReadKey(true).Key;
                yield return key;
                if (key == ConsoleKey.Q)
                {
                    yield break;
                }
            }
        }

        /// <summary>
        /// Event handler for the PipelineCompletion event.
        /// </summary>
        /// <param name="sender">The sender which raised the event.</param>
        /// <param name="e">The pipeline completion event arguments.</param>
        private static void PipelineCompletionEvent(object sender, PipelineCompletionEventArgs e)
        {
            _log.Info($"Pipeline execution completed with {e.Errors.Count} errors");

            // Prints all exceptions that were thrown by the pipeline
            if (e.Errors.Count > 0)
            {
                foreach (var error in e.Errors)
                {
                    _log.Info($"error: {error}");
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
            _log.Info($"dataStore is empty");
            // For disk logging or live visualization only
            if (dataStore != null)
            {
                // Log the microphone audio and recognition results
                //kinectSensor.ColorImage.Write("Kiosk.KinectSensor.ColorImage", dataStore);
                kinectSensor.Audio.Write("Kiosk.KinectSensor.Audio", dataStore);
                //faceTracker.Write("Kiosk.FaceTracker", dataStore);
                speechRecog.Write($"Kiosk.FinalRecognitionResults", dataStore);

                _log.Info($"Stored the data here! ");
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
