// The project to combine video and audio together

#pragma warning disable SA1649 // SA1649FileNameMustMatchTypeName

namespace Microsoft.Psi.VideoAndAudio
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
                        OpenCVMethods.ToGray(srcImage.ToImageBuffer(), destImage.ToImageBuffer(), f, ref Program.DisNose, ref Program.DisLipMiddle, ref Program.DisLipRight, ref Program.DisLipLeft);

                        // Debug.WriteLine(MainWindow.MouthOpen);
                        e.Post(destImage, env.OriginatingTime);
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

        public static void Main(string[] args)
        {
            DisNose = 0.0;
            DisLipMiddle = 0.0;
            DisLipRight = 0.0;
            DisLipLeft = 0.0;
            bool exit = false;

            Console.WriteLine("Starting");
            Console.WriteLine();
            while ( !exit )
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
                /*
                switch (key)
                {
                    case ConsoleKey.D1:
                        StartListeningAndLooking();
                        break;

                    case ConsoleKey.Q:
                        exit = true;
                        break;

                    default:
                        exit = false;
                        break;
                }
                */
                if (key == ConsoleKey.D1)
                {
                    StartListeningAndLooking(args);
                }else
                {
                    exit = true;
                }
                
            }
            
            
        }

        public static void StartListeningAndLooking(string[] args)
        {
            using (Pipeline pipeline = Pipeline.Create())
            {
                pipeline.PipelineCompletionEvent += PipelineCompletionEvent;
                // bool usingKqml = false;
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
                    // usingKqml = true;

                    facilitatorIP = args[0];
                    facilitatorPort = int.Parse(args[1]);
                    localPort = int.Parse(args[2]);
                }

                // bool showLiveVisualization = false;
                string inputLogPath = null;
                // string outputLogPath = null;

                // Needed only for live visualization
                DateTime startTime = DateTime.Now;

                // Use either live audio from the microphone or audio from a previously saved log
                IProducer<AudioBuffer> audioInput = SetupAudioInput(pipeline, inputLogPath, ref startTime);
               
                MediaCapture webcam = new MediaCapture(pipeline, 640, 480, 10);
                Console.WriteLine("Open webcam");

                FaceCasClassifier f = new FaceCasClassifier();

                Console.WriteLine("Load classifier");
                Console.WriteLine(f);

                var mouthOpenAsBool = webcam.Out.ToGrayViaOpenCV(f).Select(
                (img, e) =>
                {
                    // Debug.WriteLine(FrameCount % 10);
                    bool mouthOpen = false;
                    if ((Math.Abs(DisNose) / (4 * Math.Abs(DisLipMiddle))) < 0.7)
                    {
                        mouthOpen = true;
                    }
                    else
                    {
                        mouthOpen = false;
                    }
                    // Console.WriteLine(Math.Abs(DisLipMiddle) + " " + Math.Abs(DisLipRight) + " " + Math.Abs(DisLipLeft) + " " + (Math.Abs(DisNose) / (4 * Math.Abs(DisLipMiddle))) + " " + mouthOpen);

                    return mouthOpen;
                });


                var mouthAndSpeech = audioInput.Pair(mouthOpenAsBool).Where(t => t.Item2).Select(t => {
                    return t.Item1;
                    }
                );

               SystemSpeechRecognizer recognizer = SetupSpeechRecognizer(pipeline);

                mouthAndSpeech.PipeTo(recognizer);

               var finalResults = recognizer.Out.Where(result => result.IsFinal);

               finalResults.Do(result =>
               {
                   var ssrResult = result as SpeechRecognitionResult;
                   Console.WriteLine($"{ssrResult.Text} (confidence: {ssrResult.Confidence})");
               });

               var text = finalResults.Select(result =>
               {
                   var ssrResult = result as SpeechRecognitionResult;
                   return ssrResult.Text;
               });



                try
                {
                    pipeline.RunAsync();
                }
                catch (AggregateException exp)
                {
                    Console.WriteLine("Error! " + exp.InnerException.Message);
                }

                Console.WriteLine("Press any key to exit...");
                Console.ReadKey(true);

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
                        new GrammarInfo() { Name = Program.AppName, FileName = "C:/psi/awareness/Awareness/ConsoleVideoAndAudio/SampleGrammar.grxml" }
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

    }
}

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName