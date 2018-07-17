namespace Microsoft.Psi.Kinect.v1
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Kinect;
    using Microsoft.Kinect.Toolkit.FaceTracking;
    using Microsoft.Psi;
    using Microsoft.Psi.Components;
    using Microsoft.Psi.Imaging;

    public class SkeletonFaceTracker : ConsumerProducer<(Shared<Image>,Shared<Image>,List<Skeleton>),bool>, IStartable, IDisposable
    {
        private Microsoft.Kinect.KinectSensor kinectSensor = null;

        private bool disposed = false;

        private readonly Pipeline pipeline;

        private FaceTracker faceTracker;

        public bool lastFaceTrackSucceeded;

        private SkeletonTrackingState skeletonTrackingState;

        public int LastTrackedFrame { get; set; }

        private readonly Dictionary<int, SkeletonFaceTracker> trackedSkeletons = new Dictionary<int, SkeletonFaceTracker>();

        public SkeletonFaceTracker(Pipeline pipeline, Microsoft.Kinect.KinectSensor kinectSensor) 
            : base(pipeline)
        {
            this.pipeline = pipeline;

            this.kinectSensor = kinectSensor;

            this.FaceDetected = pipeline.CreateEmitter<bool>(this, nameof(this.FaceDetected));

            try
            {
                this.faceTracker = new FaceTracker(kinectSensor);
            }
            catch (InvalidOperationException)
            {
                // During some shutdown scenarios the FaceTracker
                // is unable to be instantiated.  Catch that exception
                // and don't track a face.
                Console.WriteLine("SkeletonFaceTracker - creating a new FaceTracker threw an InvalidOperationException");
                this.faceTracker = null;
            }

        }

        public Emitter<bool> FaceDetected { get; private set; }

        public void Dispose()
        {
            if (!disposed)
            {
                if (this.faceTracker != null)
                {
                    this.faceTracker.Dispose();
                    this.faceTracker = null;
                }
                disposed = true;
            }
        }

        protected override void Receive((Shared<Image>, Shared<Image>, List<Skeleton>) frames, Envelope envelope)
        {
            // Update the list of trackers and the trackers with the current frame information
            foreach (Skeleton skeleton in frames.Item3)
            {
                if (skeleton.TrackingState == SkeletonTrackingState.Tracked
                    || skeleton.TrackingState == SkeletonTrackingState.PositionOnly)
                {
                    // We want keep a record of any skeleton, tracked or untracked.
                    if (!this.trackedSkeletons.ContainsKey(skeleton.TrackingId))
                    {
                        this.trackedSkeletons.Add(skeleton.TrackingId, this);
                    }

                    // Give each tracker the upated frame.
                    SkeletonFaceTracker skeletonFaceTracker;
                    if (this.trackedSkeletons.TryGetValue(skeleton.TrackingId, out skeletonFaceTracker))
                    {
                        byte[] colorImage = new byte[frames.Item1.Resource.Height * frames.Item1.Resource.Width * PixelFormatHelper.GetBytesPerPixel(frames.Item1.Resource.PixelFormat)];
                        frames.Item1.Resource.CopyTo(colorImage);

                        short[] depthImage = new short[frames.Item2.Resource.Height * frames.Item2.Resource.Width * PixelFormatHelper.GetBytesPerPixel(frames.Item2.Resource.PixelFormat) / 2];
                        byte[] depthImageB = new byte[frames.Item2.Resource.Height * frames.Item2.Resource.Width * PixelFormatHelper.GetBytesPerPixel(frames.Item2.Resource.PixelFormat)];
                        frames.Item2.Resource.CopyTo(depthImageB);
                        // gotta be a better way to do this
                        for (int i = 0; i < depthImageB.Length; i+=2)
                        {
                            depthImage[i/2] = BitConverter.ToInt16(depthImageB, i);
                        }

                        skeletonFaceTracker.OnFrameReady(kinectSensor.ColorStream.Format, colorImage, kinectSensor.DepthStream.Format, depthImage, skeleton);
                        // this isn't exactly LastTrackedFrame, will need to change this or get rid of it
                        skeletonFaceTracker.LastTrackedFrame = envelope.SequenceId; // skeletonFrame.FrameNumber;
                        if (skeletonFaceTracker.lastFaceTrackSucceeded)
                        {
                            //if (skeletonFaceTracker.LastTrackedFrame % 10 == 0)
                                //Console.WriteLine(true);
                            this.FaceDetected.Post(true, envelope.OriginatingTime);
                        }
                        else
                        {
                            //Console.WriteLine(false);
                            this.FaceDetected.Post(false, envelope.OriginatingTime);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Updates the face tracking information for this skeleton
        /// </summary>
        private void OnFrameReady(ColorImageFormat colorImageFormat, byte[] colorImage, DepthImageFormat depthImageFormat, short[] depthImage, Skeleton skeletonOfInterest)
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
                    this.faceTracker = new FaceTracker(this.kinectSensor);
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
                //if (this.lastFaceTrackSucceeded)
                //{
                //    if (faceTriangles == null)
                //    {
                //        // only need to get this once.  It doesn't change.
                //        faceTriangles = frame.GetTriangles();
                //    }

                //    this.facePoints = frame.GetProjected3DShape();
                //}
            }
        }

        public void Start(Action onCompleted, ReplayDescriptor descriptor)
        {
            //throw new NotImplementedException();
        }

        public void Stop()
        {
            //throw new NotImplementedException();
        }
    }
}
