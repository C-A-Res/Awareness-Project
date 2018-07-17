namespace PsiKinectv1
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using Microsoft.Psi;
    using Microsoft.Psi.Imaging;
    using Microsoft.Kinect;
    using Microsoft.Kinect.Toolkit.FaceTracking;
    using PsiKinectv1;

    class FaceTest
    {

        static void Main(string[] args)
        {
            Console.WriteLine("Looking for faces...");
            using (var p = Pipeline.Create())
            {
                Microsoft.Psi.Kinect.v1.KinectSensor kinectSensor = new Microsoft.Psi.Kinect.v1.KinectSensor(p);

                Microsoft.Psi.Kinect.v1.SkeletonFaceTracker faceTracker = new Microsoft.Psi.Kinect.v1.SkeletonFaceTracker(p, kinectSensor);

                var joinedFrames = kinectSensor.ColorImage.Join(kinectSensor.DepthImage).Join(kinectSensor.Skeletons);

                joinedFrames.PipeTo(faceTracker);

                faceTracker.FaceDetected.Do(x => Console.WriteLine("Face: " + x));

                p.Run();
            }
        }

    }
}
