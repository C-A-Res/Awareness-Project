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

        private Microsoft.Kinect.KinectSensor kinectSensor = null;

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

            this.Audio = pipeline.CreateEmitter<AudioBuffer>(this, nameof(this.Audio));

        }

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

            this.kinectSensor = Microsoft.Kinect.KinectSensor.KinectSensors[0];

            this.kinectSensor.Start();

            // Start streaming audio!
            this.audioStream = this.kinectSensor.AudioSource.Start();

            // Use a separate thread for capturing audio because audio stream read operations
            // will block, and we don't want to block main UI thread.
            this.reading = true;
            this.readingThread = new Thread(AudioReadingThread);
            this.readingThread.Start();

        }

        /// <summary>
        /// IStartable called by the pipeline when KinectSensor is activated in the pipeline
        /// </summary>
        /// <param name="onCompleted">Unused</param>
        /// <param name="descriptor">Parameter Unused</param>
        void IStartable.Start(Action onCompleted, ReplayDescriptor descriptor)
        {
            this.StartKinect();
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
