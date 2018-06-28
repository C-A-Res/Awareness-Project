namespace PsiKinectv1
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using Microsoft.Psi;
    using Microsoft.Kinect;
    using Microsoft.Kinect.Toolkit.FaceTracking;

    class Program : IProducer<Boolean>
    {
        Pipeline pipeline;

        private const uint MaxMissedFrames = 100;

        private readonly Dictionary<int, SkeletonFaceTracker> trackedSkeletons = new Dictionary<int, SkeletonFaceTracker>();

        private byte[] colorImage;

        private ColorImageFormat colorImageFormat = ColorImageFormat.Undefined;

        private short[] depthImage;

        private DepthImageFormat depthImageFormat = DepthImageFormat.Undefined;

        private bool disposed;

        private Skeleton[] skeletonData;

        public KinectSensor kinect;

        public Emitter<bool> Out { get; private set; }

        static void Main(string[] args)
        {
            Console.WriteLine("Looking for faces...");
            using (var p = Pipeline.Create())
            {
                Program prog = new Program(p);
                prog.start();
                prog.Out.Do(t => Console.WriteLine(t));
                p.Run();
            }
        }

        public Program(Pipeline p)
        {
            this.pipeline = p;

            this.Out = p.CreateEmitter<bool>(this, nameof(this.Out));
        }

        public void start()
        {
            if (KinectSensor.KinectSensors.Count > 0)
            {
                this.kinect = KinectSensor.KinectSensors[0];
                if (kinect.Status == KinectStatus.Connected)
                {
                    kinect.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
                    kinect.DepthStream.Enable(DepthImageFormat.Resolution320x240Fps30);
                    kinect.DepthStream.Range = DepthRange.Near;
                    kinect.SkeletonStream.EnableTrackingInNearRange = true;
                    kinect.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                    kinect.SkeletonStream.Enable();

                    kinect.AllFramesReady += Kinect_AllFramesReady;
                    

                    kinect.Start();
                }
            }
        }

        private void Kinect_AllFramesReady(object sender, AllFramesReadyEventArgs allFramesReadyEventArgs)
        {
            ColorImageFrame colorImageFrame = null;
            DepthImageFrame depthImageFrame = null;
            SkeletonFrame skeletonFrame = null;

            try
            {
                colorImageFrame = allFramesReadyEventArgs.OpenColorImageFrame();
                depthImageFrame = allFramesReadyEventArgs.OpenDepthImageFrame();
                skeletonFrame = allFramesReadyEventArgs.OpenSkeletonFrame();

                if (colorImageFrame == null || depthImageFrame == null || skeletonFrame == null)
                {
                    return;
                }

                // Check for image format changes.  The FaceTracker doesn't
                // deal with that so we need to reset.
                if (this.depthImageFormat != depthImageFrame.Format)
                {
                    this.ResetFaceTracking();
                    this.depthImage = null;
                    this.depthImageFormat = depthImageFrame.Format;
                }

                if (this.colorImageFormat != colorImageFrame.Format)
                {
                    this.ResetFaceTracking();
                    this.colorImage = null;
                    this.colorImageFormat = colorImageFrame.Format;
                }

                // Create any buffers to store copies of the data we work with
                if (this.depthImage == null)
                {
                    this.depthImage = new short[depthImageFrame.PixelDataLength];
                }

                if (this.colorImage == null)
                {
                    this.colorImage = new byte[colorImageFrame.PixelDataLength];
                }

                // Get the skeleton information
                if (this.skeletonData == null || this.skeletonData.Length != skeletonFrame.SkeletonArrayLength)
                {
                    this.skeletonData = new Skeleton[skeletonFrame.SkeletonArrayLength];
                }

                colorImageFrame.CopyPixelDataTo(this.colorImage);
                depthImageFrame.CopyPixelDataTo(this.depthImage);
                skeletonFrame.CopySkeletonDataTo(this.skeletonData);

                // Update the list of trackers and the trackers with the current frame information
                foreach (Skeleton skeleton in this.skeletonData)
                {
                    if (skeleton.TrackingState == SkeletonTrackingState.Tracked
                        || skeleton.TrackingState == SkeletonTrackingState.PositionOnly)
                    {
                        // We want keep a record of any skeleton, tracked or untracked.
                        if (!this.trackedSkeletons.ContainsKey(skeleton.TrackingId))
                        {
                            this.trackedSkeletons.Add(skeleton.TrackingId, new SkeletonFaceTracker());
                        }

                        // Give each tracker the upated frame.
                        SkeletonFaceTracker skeletonFaceTracker;
                        if (this.trackedSkeletons.TryGetValue(skeleton.TrackingId, out skeletonFaceTracker))
                        {
                            skeletonFaceTracker.OnFrameReady(this.kinect, colorImageFormat, colorImage, depthImageFormat, depthImage, skeleton);
                            skeletonFaceTracker.LastTrackedFrame = skeletonFrame.FrameNumber;
                            if (skeletonFaceTracker.lastFaceTrackSucceeded)
                            {
                                if (skeletonFaceTracker.LastTrackedFrame % 10 == 0)
                                    Out.Post(true, pipeline.GetCurrentTime());
                            }
                            else
                            {
                                if (skeletonFaceTracker.LastTrackedFrame % 10 == 0)
                                {
                                    Out.Post(false, pipeline.GetCurrentTime());
                                }
                                
                            }
                        }
                    }
                }

                this.RemoveOldTrackers(skeletonFrame.FrameNumber);

                //this.InvalidateVisual();
            }
            finally
            {
                if (colorImageFrame != null)
                {
                    colorImageFrame.Dispose();
                }

                if (depthImageFrame != null)
                {
                    depthImageFrame.Dispose();
                }

                if (skeletonFrame != null)
                {
                    skeletonFrame.Dispose();
                }
            }
        }

        private void ResetFaceTracking()
        {
            foreach (int trackingId in new List<int>(this.trackedSkeletons.Keys))
            {
                this.RemoveTracker(trackingId);
            }
        }

        private void RemoveOldTrackers(int currentFrameNumber)
        {
            var trackersToRemove = new List<int>();

            foreach (var tracker in this.trackedSkeletons)
            {
                uint missedFrames = (uint)currentFrameNumber - (uint)tracker.Value.LastTrackedFrame;
                if (missedFrames > MaxMissedFrames)
                {
                    // There have been too many frames since we last saw this skeleton
                    trackersToRemove.Add(tracker.Key);
                }
            }

            foreach (int trackingId in trackersToRemove)
            {
                this.RemoveTracker(trackingId);
            }
        }

        private void RemoveTracker(int trackingId)
        {
            this.trackedSkeletons[trackingId].Dispose();
            this.trackedSkeletons.Remove(trackingId);
        }


        private class SkeletonFaceTracker : IDisposable
        {
            private static FaceTriangle[] faceTriangles;

            private EnumIndexableCollection<FeaturePoint, PointF> facePoints;

            private FaceTracker faceTracker;

            public bool lastFaceTrackSucceeded;

            private SkeletonTrackingState skeletonTrackingState;

            public int LastTrackedFrame { get; set; }

            public void Dispose()
            {
                if (this.faceTracker != null)
                {
                    this.faceTracker.Dispose();
                    this.faceTracker = null;
                }
            }

            //public void DrawFaceModel(DrawingContext drawingContext)
            //{
            //    if (!this.lastFaceTrackSucceeded || this.skeletonTrackingState != SkeletonTrackingState.Tracked)
            //    {
            //        return;
            //    }

            //    var faceModelPts = new List<Point>();
            //    var faceModel = new List<FaceModelTriangle>();

            //    for (int i = 0; i < this.facePoints.Count; i++)
            //    {
            //        faceModelPts.Add(new Point(this.facePoints[i].X + 0.5f, this.facePoints[i].Y + 0.5f));
            //    }

            //    foreach (var t in faceTriangles)
            //    {
            //        var triangle = new FaceModelTriangle();
            //        triangle.P1 = faceModelPts[t.First];
            //        triangle.P2 = faceModelPts[t.Second];
            //        triangle.P3 = faceModelPts[t.Third];
            //        faceModel.Add(triangle);
            //    }

            //    var faceModelGroup = new GeometryGroup();
            //    for (int i = 0; i < faceModel.Count; i++)
            //    {
            //        var faceTriangle = new GeometryGroup();
            //        faceTriangle.Children.Add(new LineGeometry(faceModel[i].P1, faceModel[i].P2));
            //        faceTriangle.Children.Add(new LineGeometry(faceModel[i].P2, faceModel[i].P3));
            //        faceTriangle.Children.Add(new LineGeometry(faceModel[i].P3, faceModel[i].P1));
            //        faceModelGroup.Children.Add(faceTriangle);
            //    }

            //    drawingContext.DrawGeometry(Brushes.LightYellow, new Pen(Brushes.LightYellow, 1.0), faceModelGroup);
            //}

            /// <summary>
            /// Updates the face tracking information for this skeleton
            /// </summary>
            internal void OnFrameReady(KinectSensor kinectSensor, ColorImageFormat colorImageFormat, byte[] colorImage, DepthImageFormat depthImageFormat, short[] depthImage, Skeleton skeletonOfInterest)
            {
                this.skeletonTrackingState = skeletonOfInterest.TrackingState;

                if (this.skeletonTrackingState != SkeletonTrackingState.Tracked)
                {
                    // nothing to do with an untracked skeleton.
                    return;
                }

                if (this.faceTracker == null)
                {
                    try
                    {
                        this.faceTracker = new FaceTracker(kinectSensor);
                    }
                    catch (InvalidOperationException)
                    {
                        // During some shutdown scenarios the FaceTracker
                        // is unable to be instantiated.  Catch that exception
                        // and don't track a face.
                        Console.WriteLine("AllFramesReady - creating a new FaceTracker threw an InvalidOperationException");
                        this.faceTracker = null;
                    }
                }

                if (this.faceTracker != null)
                {
                    FaceTrackFrame frame = this.faceTracker.Track(
                        colorImageFormat, colorImage, depthImageFormat, depthImage, skeletonOfInterest);

                    this.lastFaceTrackSucceeded = frame.TrackSuccessful;
                    if (this.lastFaceTrackSucceeded)
                    {
                        if (faceTriangles == null)
                        {
                            // only need to get this once.  It doesn't change.
                            faceTriangles = frame.GetTriangles();
                        }

                        this.facePoints = frame.GetProjected3DShape();
                    }
                }
            }

            private struct FaceModelTriangle
            {
                public Point P1;
                public Point P2;
                public Point P3;
            }
        }
    }
}
