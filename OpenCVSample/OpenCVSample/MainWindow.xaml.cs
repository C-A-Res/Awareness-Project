// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

#pragma warning disable SA1649 // SA1649FileNameMustMatchTypeName

namespace NU.Kiosk
{
    using System;
    using System.Diagnostics;
    using System.Windows;
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
        public static IProducer<Shared<Image>> ToGrayViaOpenCV(this IProducer<Shared<Image>> source,  FaceCasClassifier f = null, int framecount = 0, DeliveryPolicy deliveryPolicy = null)
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
                        OpenCVMethods.ToGray(srcImage.ToImageBuffer(), destImage.ToImageBuffer(), f, ref MainWindow.DisNose, ref MainWindow.DisLipMiddle, ref MainWindow.DisLipRight, ref MainWindow.DisLipLeft, ref MainWindow.HasFace);

                        // Debug.WriteLine(MainWindow.MouthOpen);
                        e.Post(destImage, env.OriginatingTime);
                    }
                }, deliveryPolicy);
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Define our Psi Pipeline object
        
        public static int FrameCount;
        public static double DisNose;
        public static double DisLipMiddle;
        public static double DisLipRight;
        public static double DisLipLeft;
        public static int HasFace;

        private Pipeline pipeline;
        private static string AppName = "Kiosk";

        public MainWindow()
        {
            this.InitializeComponent();
            this.DispImage = new DisplayImage();
            this.DataContext = this;
            this.DoConvert = true;
            this.Closing += this.MainWindow_Closing;
            DisNose = 0.0;
            DisLipMiddle = 0.0;
            DisLipRight = 0.0;
            DisLipLeft = 0.0;
            HasFace = 0;
            FrameCount = 0;

            // Setup our Psi pipeline
            this.SetupPsi();
        }

        // Define a property that exposes our DisplayImage so that WPF can access it.
        public DisplayImage DispImage { get; set; }

        public bool DoConvert { get; set; }

        /// <summary>
        /// SetupPsi() is called at application startup. It is responsible for
        /// building and starting the Psi pipeline
        /// </summary>
        public void SetupPsi()
        {
            Console.WriteLine("================================================================================");
            Console.WriteLine("                               Kiosk Awareness sample");
            Console.WriteLine("================================================================================");
            Console.WriteLine();

            this.pipeline = Pipeline.Create();
            
                // Next register an event handler to catch pipeline errors
                this.pipeline.PipelineCompletionEvent += this.PipelineCompletionEvent;

                /*
               // bool usingKqml = false;
               string facilitatorIP = null;
               int facilitatorPort = -1;
               int localPort = -1;


               if (arguments.Length > 0)
               {
                   if (arguments.Length < 3)
                   {
                       Console.WriteLine("Usage for running with a facilitator: \nKioskMain facilitatorIP facilitatorPort localPort");
                       return;
                   }

                   // usingKqml = true;

                   facilitatorIP = arguments[0];
                   facilitatorPort = int.Parse(arguments[1]);
                   localPort = int.Parse(arguments[2]);
               }
               */

                // bool showLiveVisualization = false;
                string inputLogPath = null;
                // string outputLogPath = null;

                DateTime startTime = DateTime.Now;

                IProducer<AudioBuffer> audioInput = SetupAudioInput(this.pipeline, inputLogPath, ref startTime);

                // Create our webcam
                MediaCapture webcam = new MediaCapture(this.pipeline, 320, 240, 10);
                Debug.WriteLine("Open webcam");

                FaceCasClassifier f = new FaceCasClassifier();

                Debug.WriteLine("Load classifier");
                Debug.WriteLine(f);

                var mouthOpenAsBool = webcam.Out.ToGrayViaOpenCV(f, FrameCount).Select(
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
                    Console.WriteLine(Math.Abs(DisLipMiddle) + " " + Math.Abs(DisLipRight) + " " + Math.Abs(DisLipLeft) + " " + (Math.Abs(DisNose) / (4 * Math.Abs(DisLipMiddle))) + " " + mouthOpen);
                    this.DispImage.UpdateImage(img);
                        return mouthOpen;
                    });

                var mouthAndSpeech = audioInput.Pair(mouthOpenAsBool).Where(t => true).Select(t =>
                {
                    return t.Item1;
                }
                );

                SystemSpeechRecognizer recognizer = SetupSpeechRecognizer(this.pipeline);

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

                // Finally start the pipeline running
                try
                {
                    this.pipeline.RunAsync();
                }
                catch (AggregateException exp)
                {
                    MessageBox.Show("Error! " + exp.InnerException.Message);
                }

            
        }

        /// <summary>
        /// PipelineCompletionEvent is called when the pipeline finishes running
        /// </summary>
        /// <param name="sender">Object that sent this event</param>
        /// <param name="e">Pipeline event arguments. Primarily used to get any pipeline errors back.</param>
        private void PipelineCompletionEvent(object sender, PipelineCompletionEventArgs e)
        {
            if (e.Errors.Count > 0)
            {
                MessageBox.Show("Error! " + e.Errors[0].Message);
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
                        new GrammarInfo() { Name = AppName, FileName = "SampleGrammar.grxml" }
                }
                });
        }

        private static IProducer<AudioBuffer> SetupAudioInput(Pipeline pipeline, string inputLogPath, ref DateTime startTime)
        {
            IProducer<AudioBuffer> audioInput = null;
            if (inputLogPath != null)
            {
                // Open the MicrophoneAudio stream from the last saved log
                var store = Store.Open(pipeline, AppName, inputLogPath);
                audioInput = store.OpenStream<AudioBuffer>($"{AppName}.MicrophoneAudio");

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

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // this.DoConvert = !this.DoConvert;
            // this.ConvertButton.Content = this.DoConvert ? "ToRGB" : "ToGray";
        }

        /// <summary>
        /// Called when main window is closed
        /// </summary>
        /// <param name="sender">window that we are closing</param>
        /// <param name="e">args for closing event</param>
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Dispose of the pipeline to shut it down and exit clean
            this.pipeline?.Dispose();
            this.pipeline = null;
        }
    }
}

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName
