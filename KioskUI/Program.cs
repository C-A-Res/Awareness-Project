using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Psi;
using Microsoft.Psi.Components;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace KioskUI
{
    public class KioskUIServer : WebSocketBehavior
    {
        private string nextMsg = "{\"user\" : \"companion\", \"content\" :  \"This is the first\"}";

        protected override void OnMessage (MessageEventArgs e)
        {
            var input = e.Data;

            var msg = nextMsg;
            nextMsg = "{\"user\" : \"other\", \"content\" :  \"I'm here\"}";

            Send(msg);
        }

        
    }

    public class KioskUI : IStartable
    {

        private readonly Pipeline pipeline;

        public KioskUI(Pipeline pipeline)
        {
            this.pipeline = pipeline;

            this.Updating = pipeline.CreateEmitter<bool>(this, nameof(this.Updating));
        }

        /// <summary>
        /// Gets the list of skeletons from the Kinect
        /// </summary>
        public Emitter<bool> Updating { get; private set; }

        public void Start(Action onCompleted, ReplayDescriptor descriptor)
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }
    }

    public class Program
    {
        static void Main(string[] args)
        {
            var wssv = new WebSocketServer("ws://127.0.0.1:9791");
            wssv.AddWebSocketService<KioskUIServer>("/dialog");
            wssv.Start();
            Console.ReadKey(true);
            wssv.Stop();
        }
    }
}
