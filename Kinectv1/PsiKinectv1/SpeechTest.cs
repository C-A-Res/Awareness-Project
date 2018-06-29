namespace PsiKinectv1
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using Microsoft.Psi;
    using Microsoft.Psi.Audio;
    using Microsoft.Psi.Speech;
    using Microsoft.Kinect;
    using Microsoft.Kinect.Toolkit.FaceTracking;

    class SpeechTest
    {

        static void Main(string[] args)
        {
            Console.WriteLine("Starting speech test...");
            using (var p = Pipeline.Create())
            {
                Microsoft.Psi.Kinect.v1.KinectSensor kinectSensor = new Microsoft.Psi.Kinect.v1.KinectSensor(p);


                // Create System.Speech recognizer component
                var recognizer = new SystemSpeechRecognizer(
                    p,
                    new SystemSpeechRecognizerConfiguration()
                    {
                        Language = "en-US",
                        Grammars = new GrammarInfo[]
                        {
                            new GrammarInfo() { Name = "Kinect v1 Speech Test", FileName = "SampleGrammar.grxml" }
                        }
                    });
                //pipeline);

                kinectSensor.Audio.PipeTo(recognizer);

                var finalResults = recognizer.Out.Where(result => result.IsFinal);

                // Print the recognized text of the final recognition result to the console.
                finalResults.Do(result =>
                {
                    var ssrResult = result as SpeechRecognitionResult;
                    Console.WriteLine($"{ssrResult.Text} (confidence: {ssrResult.Confidence})");
                });

                p.Run();
            }
        }

    }
}