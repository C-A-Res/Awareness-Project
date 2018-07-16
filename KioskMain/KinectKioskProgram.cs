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
        static TimeSpan _100ms = TimeSpan.FromSeconds(0.1);

        public static void Main(string[] args)
        {
            bool detected = false;

            Console.WriteLine("Starting Kinect-based Kiosk.  Verify that Kinect is setup before continuing");

            using (Pipeline pipeline = Pipeline.Create())
            {
                Microsoft.Psi.Kinect.v1.KinectSensor kinectSensor = new Microsoft.Psi.Kinect.v1.KinectSensor(pipeline);

                Microsoft.Psi.Kinect.v1.SkeletonFaceTracker faceTracker = new Microsoft.Psi.Kinect.v1.SkeletonFaceTracker(pipeline, kinectSensor.kinectSensor);

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
                mouthOpen.Do(x => Console.Write($"{x} "));

                var speechDetector = new SystemVoiceActivityDetector(pipeline);
                kinectSensor.Audio.PipeTo(speechDetector);

                var mouthAndSpeechDetector = speechDetector.Join(mouthOpen, _100ms).Select((t, e) => t.Item1 && t.Item2);

                var recognizer = NU.Kiosk.Speech.Program.CreateSpeechRecognizer(pipeline);

                kinectSensor.Audio.Join(mouthAndSpeechDetector).Where(x => x.Item2).Select(x => x.Item1).PipeTo(recognizer);

                var finalResults = recognizer.Out.Where(result => result.IsFinal);

                finalResults.Do(result =>
                {
                    var ssrResult = result as SpeechRecognitionResult;
                    Console.WriteLine($"{ssrResult.Text} (confidence: {ssrResult.Confidence})");
                });

                // Run the pipeline
                pipeline.RunAsync();

                Console.WriteLine("Press any key to exit...");
                Console.ReadKey(true);

            }
        }
    }
}
