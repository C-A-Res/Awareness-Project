namespace Microsoft.Psi.Kinect.v1
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Linq;
    using Microsoft.Kinect;
    using Microsoft.Psi;
    using Microsoft.Psi.Audio;
    using Microsoft.Psi.Components;
    using Microsoft.Psi.Imaging;

    public class KinectSensor :IStartable, IDisposable //, IKinectSensor
    {
        private static WaveFormat audioFormat = WaveFormat.Create16kHz1ChannelIeeeFloat();
        private readonly Pipeline pipeline;


        /// <summary>
        /// Number of milliseconds between each read of audio data from the stream.
        /// Faster polling (few tens of ms) ensures a smoother audio stream visualization.
        /// </summary>
        private const int AudioPollingInterval = 50;

        /// <summary>
        /// Number of samples captured from Kinect audio stream each millisecond.
        /// </summary>
        private const int SamplesPerMillisecond = 16;

        /// <summary>
        /// Number of bytes in each Kinect audio stream sample.
        /// </summary>
        private const int BytesPerSample = 2;

        // TODO go back to making this private
        public Microsoft.Kinect.KinectSensor kinectSensor = null;


        private byte[] colorImage;

        public ColorImageFormat colorImageFormat = ColorImageFormat.Undefined;

        private short[] depthImage;

        public DepthImageFormat depthImageFormat = DepthImageFormat.Undefined;

        private Skeleton[] skeletonData;

        /// <summary>
        /// Buffer used to hold audio data read from audio stream.
        /// </summary>
        private readonly byte[] audioBuffer = new byte[AudioPollingInterval * SamplesPerMillisecond * BytesPerSample];

        /// <summary>
        /// Stream of audio being captured by Kinect sensor.
        /// </summary>
        private Stream audioStream;

        /// <summary>
        /// <code>true</code> if audio is currently being read from Kinect stream, <code>false</code> otherwise.
        /// </summary>
        private bool reading;

        /// <summary>
        /// Thread that is reading audio from Kinect stream.
        /// </summary>
        private Thread readingThread;


        private bool disposed = false;

        public KinectSensor(Pipeline pipeline)
        {
            this.pipeline = pipeline;

            this.Skeletons = pipeline.CreateEmitter<List<Skeleton>>(this, nameof(this.Skeletons));
            this.ColorImage = pipeline.CreateEmitter<Shared<Image>>(this, nameof(this.ColorImage));
            this.DepthImage = pipeline.CreateEmitter<Shared<Image>>(this, nameof(this.DepthImage));

            this.Audio = pipeline.CreateEmitter<AudioBuffer>(this, nameof(this.Audio));

            this.StartKinect();
        }

        /// <summary>
        /// Gets the list of skeletons from the Kinect
        /// </summary>
        public Emitter<List<Skeleton>> Skeletons { get; private set; }

        /// <summary>
        /// Gets the current image from the color camera
        /// </summary>
        public Emitter<Shared<Image>> ColorImage { get; private set; }

        /// <summary>
        /// Gets the current image from the depth camera
        /// </summary>
        public Emitter<Shared<Image>> DepthImage { get; private set; }

        /// <summary>
        /// Gets the emitter that returns the Kinect's audio samples
        /// </summary>
        public Emitter<AudioBuffer> Audio { get; private set; }


        private void StartKinect()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(nameof(KinectSensor));
            }


            if (Microsoft.Kinect.KinectSensor.KinectSensors.Count > 0)
            {
                this.kinectSensor = Microsoft.Kinect.KinectSensor.KinectSensors[0];

                if (kinectSensor.Status == KinectStatus.Connected)
                {
                    //TODO use configurations to determine what to enable

                    kinectSensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
                    kinectSensor.DepthStream.Enable(DepthImageFormat.Resolution320x240Fps30);
                    kinectSensor.DepthStream.Range = DepthRange.Near;
                    kinectSensor.SkeletonStream.EnableTrackingInNearRange = true;
                    kinectSensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                    kinectSensor.SkeletonStream.Enable();

                    kinectSensor.AllFramesReady += Kinect_AllFramesReady;


                    kinectSensor.Start();


                    // Start streaming audio!
                    //this.audioStream = this.kinectSensor.AudioSource.Start();

                    // Use a separate thread for capturing audio because audio stream read operations
                    // will block, and we don't want to block main UI thread.
                    this.reading = true;
                    this.readingThread = new Thread(AudioReadingThread);
                    //this.readingThread.Start();

                }
            }
        }

        /// <summary>
        /// IStartable called by the pipeline when KinectSensor is activated in the pipeline
        /// </summary>
        /// <param name="onCompleted">Unused</param>
        /// <param name="descriptor">Parameter Unused</param>
        void IStartable.Start(Action onCompleted, ReplayDescriptor descriptor)
        {
            //this.StartKinect();

            // Start streaming audio!
            this.audioStream = this.kinectSensor.AudioSource.Start();

            this.readingThread.Start();
        }

        /// <summary>
        /// Called by the pipeline to stop the sensor
        /// </summary>
        void IStartable.Stop()
        {
            this.kinectSensor?.Stop();
        }

        /// <summary>
        /// Called to release the sensor
        /// </summary>
        public void Dispose()
        {
            if (!this.disposed)
            {
                this.kinectSensor?.Stop();
                this.disposed = true;
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
                    this.depthImage = null;
                    this.depthImageFormat = depthImageFrame.Format;
                }

                if (this.colorImageFormat != colorImageFrame.Format)
                {
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


                // TODO look into using the Timestamp on each frame
                var time = pipeline.GetCurrentTime();

                var sharedColorImage = ImagePool.GetOrCreate(colorImageFrame.Width, colorImageFrame.Height, Imaging.PixelFormat.BGRX_32bpp);
                var sharedDepthImage = ImagePool.GetOrCreate(depthImageFrame.Width, depthImageFrame.Height, Imaging.PixelFormat.Gray_16bpp);

                colorImageFrame.CopyPixelDataTo(sharedColorImage.Resource.ImageData, (colorImageFrame.Width * colorImageFrame.Height * 4));
                this.ColorImage.Post(sharedColorImage, time);

                //depthImageFrame.CopyPixelDataTo(sharedDepthImage.Resource.ImageData, (depthImageFrame.Width * depthImageFrame.Height * 2));
                depthImageFrame.CopyPixelDataTo(sharedDepthImage.Resource.ImageData, depthImageFrame.PixelDataLength);
                this.DepthImage.Post(sharedDepthImage, time);


                skeletonFrame.CopySkeletonDataTo(this.skeletonData);
                this.Skeletons.Post(this.skeletonData.ToList(), time);

            }
            catch
            {
                // TODO catch a cold
            }
        }

        /// <summary>
        /// Handles polling audio stream and updating visualization every tick.
        /// </summary>
        private void AudioReadingThread()
        {
            while (this.reading)
            {
                int readCount = audioStream.Read(audioBuffer, 0, audioBuffer.Length);

                // Compute originating time from the relative time reported by Kinect.
                // Scratch that idea for now, not sure if v1 can do that.
                // Let's just use the pipline time
                var originatingTime = this.pipeline.GetCurrentTime();

                // Worried about this, since I think this post the audio buffer by reference
                this.Audio.Post(new AudioBuffer(this.audioBuffer, KinectSensor.audioFormat), originatingTime);
                
            }
        }
    }
    
}
