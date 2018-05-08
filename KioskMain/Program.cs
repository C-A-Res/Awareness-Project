

namespace NU.Kiosk
{
    using System;
    using System.Diagnostics;
    using System.Windows;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Psi;
    using Microsoft.Psi.Imaging;
    using Microsoft.Psi.Media;
    using Microsoft.Psi.Audio;
    using Microsoft.Psi.Data;
    using Microsoft.Psi.Speech;
    using Microsoft.Psi.Visualization.Client;
    using Microsoft.Psi.Visualization.Common;


    public static class OpenCV
    {
        /// <summary>
        /// Helper to wrap a Psi Image into an ImageBuffer suitable for passing to our C++ interop layer
        /// </summary>
        /// <param name="source">Image to wrap</param>
        /// <returns>A Psi image wrapped as an ImageBuffer</returns>
        public static ImageBuffer ToImageBuffer(this Shared<Image> source)
        {
            return new ImageBuffer(source.Resource.Width, source.Resource.Height, source.Resource.ImageData, source.Resource.Stride);
        }

        /// <summary>
        /// Here we define an Psi extension. This extension will take a stream of images (source)
        /// and create a new stream of converted images.
        /// </summary>
        /// <param name="source">Our source producer (source stream of image samples)</param>
        /// <param name="f">A wapper face classifier object (null means use the default)</param>
        /// <param name="framecount">A integer to control the frame number</param>
        /// <param name="deliveryPolicy">Our delivery policy (null means use the default)</param>
        /// <returns>The new stream of converted images.</returns>
        public static IProducer<Shared<Image>> ToGrayViaOpenCV(this IProducer<Shared<Image>> source, FaceCasClassifier f = null, DeliveryPolicy deliveryPolicy = null)
        {
            // Process informs the pipeline that we want to call our lambda ("(srcImage, env, e) =>{...}") with each image
            // from the stream.
            return source.Process<Shared<Image>, Shared<Image>>(
                (srcImage, env, e) =>
                {
                    // Our lambda here is called with each image sample from our stream and calls OpenCV to convert
                    // the image into a grayscale image. We then post the resulting gray scale image to our event queu
                    // so that the Psi pipeline will send it to the next component.

                    // Have Psi allocate a new image. We will convert the current image ('srcImage') into this new image.
                    using (var destImage = ImagePool.GetOrCreate(srcImage.Resource.Width, srcImage.Resource.Height, PixelFormat.Gray_8bpp))
                    {
                        // Call into our OpenCV wrapper to convert the source image ('srcImage') into the newly created image ('destImage')
                        // Note: since srcImage & destImage are Shared<> we need to access the Microsoft.Psi.Imaging.Image data via the Resource member
                        OpenCVMethods.ToGray(srcImage.ToImageBuffer(), destImage.ToImageBuffer(), f, ref Program.DisNose, ref Program.DisLipMiddle, ref Program.DisLipRight, ref Program.DisLipLeft, ref Program.HasFace);

                        // Debug.WriteLine(MainWindow.MouthOpen);
                        e.Post(srcImage, env.OriginatingTime);
                    }
                }, deliveryPolicy);
        }
    }


    class Program
    {
        private static string AppName = "Kiosk";

        public static double DisNose;
        public static double DisLipMiddle;
        public static double DisLipRight;
        public static double DisLipLeft;
        public static int HasFace;
        public static List<bool> PastMouthStatus = new List<bool>();

        static void Main(string[] args)
        {
            DisNose = 0.0;
            DisLipMiddle = 0.0;
            DisLipRight = 0.0;
            DisLipLeft = 0.0;
            HasFace = 0;
            bool exit = false;

            Console.WriteLine("Starting");
            Console.WriteLine();
            while (!exit)
            {
                Console.WriteLine("================================================================================");
                Console.WriteLine("                               Kiosk Awareness sample");
                Console.WriteLine("================================================================================");
                Console.WriteLine("1) Start listening and looking. ");
                Console.WriteLine("Q) QUIT");
                Console.Write("Enter selection: ");
                ConsoleKey key = Console.ReadKey().Key;
                Console.WriteLine();
                exit = false;

                if (key == ConsoleKey.D1)
                {
                    StartListeningAndLooking(args);
                }
                else
                {
                    exit = true;
                }

            }
        }

        public static void StartListeningAndLooking(string[] args)
        {
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


                bool showLiveVisualization = true;
                string inputLogPath = null;
                string outputLogPath = "D:/recordings";

                // Needed only for live visualization
                DateTime startTime = DateTime.Now;

                // Use either live audio from the microphone or audio from a previously saved log
                IProducer<AudioBuffer> audioInput = SetupAudioInput(pipeline, inputLogPath, ref startTime);

                // Create our webcam
                MediaCapture webcam = new MediaCapture(pipeline, 320, 240, 10);

                FaceCasClassifier f = new FaceCasClassifier();

                Console.WriteLine("Load classifier");
                Console.WriteLine(f);

                // Bind the webcam's output to our display image.
                // The "Do" operator is executed on each sample from the stream (webcam.Out), which are the images coming from the webcam
                var rawVideo = webcam.Out.EncodeJpeg(90, DeliveryPolicy.LatestMessage).Out;
                var processedVideo = webcam.Out.ToGrayViaOpenCV(f).EncodeJpeg(90, DeliveryPolicy.LatestMessage).Out;                
                var mouthOpenAsBool = processedVideo.Select(
                (img, e) =>
                {
                    // Debug.WriteLine(FrameCount % 10);
                    bool mouthOpen = false;
                    if ((Math.Abs(DisNose) / (4 * Math.Abs(DisLipMiddle))) < 3)
                    {
                        mouthOpen = true;
                    }
                    else
                    {
                        mouthOpen = false;
                    }

                    
                    PastMouthStatus.Add(mouthOpen);
                    if (!mouthOpen)
                    {
                        int openFrequencyIn2s = 0;
                        int nearestOpenDistance = -1;
                        for (var i = PastMouthStatus.Count - 1; i >= 0 && (i >= PastMouthStatus.Count - 10); i--)
                        {
                            if (PastMouthStatus[i])
                            {
                                openFrequencyIn2s++;
                                nearestOpenDistance = PastMouthStatus.Count - 1 - i;
                            }
                        }

                        if (1.0*openFrequencyIn2s / Math.Min(10, PastMouthStatus.Count) >= 0.3 && nearestOpenDistance > 0 && nearestOpenDistance <= 8)
                        {
                            mouthOpen = true;
                        }
                    }
                    // Console.WriteLine(Math.Abs(DisLipMiddle) + " " + Math.Abs(DisLipRight) + " " + Math.Abs(DisLipLeft) + " " + (Math.Abs(DisNose) / (4 * Math.Abs(DisLipMiddle))) + " " + mouthOpen);
                    //Console.WriteLine(mouthOpen);

                    
                    return mouthOpen;
                });

                var hasFaceAsBool = webcam.Out.ToGrayViaOpenCV(f).Select(
                (img, e) =>
                {
                    bool hasFaceboll = false;
                    if (HasFace == 1)
                    {
                        hasFaceboll = true;
                    }
                    else
                    {
                        hasFaceboll = false;
                    }
                    return hasFaceboll;
                });

                var mouthAndSpeech = audioInput.Pair(mouthOpenAsBool).Where(t => t.Item2).Select(t => {
                    return t.Item1;
                }
                );

                SystemSpeechRecognizer recognizer = SetupSpeechRecognizer(pipeline);

                // Subscribe the recognizer to the input audio
                mouthAndSpeech.PipeTo(recognizer);
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

                /*
                SocketStringConsumer kqml = null;
                if (usingKqml)
                {
                    kqml = new SocketStringConsumer(pipeline, facilitatorIP, facilitatorPort, localPort);

                    text.PipeTo(kqml.In)
                }
                */

                // Create a data store to log the data to if necessary. A data store is necessary
                // only if output logging or live visualization are enabled.
                var dataStore = CreateDataStore(pipeline, outputLogPath, showLiveVisualization);

                // For disk logging or live visualization only
                if (dataStore != null)
                {
                    // Log the microphone audio and recognition results
                    rawVideo.Write($"{Program.AppName}.WebCamRawVideo", dataStore);
                    processedVideo.Write($"{Program.AppName}.WebCamProcessedVideo", dataStore);
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

                    // Plot the video stream in a new panel
                    visualizationClient.AddTimelinePanel();
                    processedVideo.Show(visualizationClient);

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

               // if (kqml != null) kqml.Stop();
            }
        }

        private static SystemSpeechRecognizer SetupSpeechRecognizer(Pipeline pipeline)
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

        private static IProducer<AudioBuffer> SetupAudioInput(Pipeline pipeline, string inputLogPath, ref DateTime startTime)
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
