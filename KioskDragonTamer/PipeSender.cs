using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace NU.Kiosk
{
    class PipeSender
    {
        private string destination_pipe_name;
        NamedPipeClientStream pipeClient;
        StreamString ss;
        bool ready;

        public PipeSender(string dest_pipe_name)
        {
            this.destination_pipe_name = dest_pipe_name;
            ready = false;
        }

        public void Dispose()
        {
            Console.WriteLine($"[PipeSender.{destination_pipe_name}] Dispose");

            pipeClient.Close();
            pipeClient.Dispose();
        }
        
        public void Initialize()
        {
            pipeClient = new NamedPipeClientStream(".", destination_pipe_name, PipeDirection.Out, PipeOptions.None, TokenImpersonationLevel.Impersonation);

            Console.WriteLine($"[PipeSender.{destination_pipe_name}] Connecting to server... ");
            pipeClient.Connect();
            Console.WriteLine($"[PipeSender.{destination_pipe_name}] Connected!");

            ss = new StreamString(pipeClient);
            ready = true;
        }

        public void Reconnect()
        {
            ready = false;
            pipeClient = new NamedPipeClientStream(".", destination_pipe_name, PipeDirection.Out, PipeOptions.None, TokenImpersonationLevel.Impersonation);
            Console.WriteLine($"[PipeSender.{destination_pipe_name}] Reconnecting...");
            pipeClient.Connect();
            Console.WriteLine($"[PipeSender.{destination_pipe_name}] Connected!");

            ss = new StreamString(pipeClient);
            ready = true;
        }

        public void Send(string message)
        {
            if (ready)
            {
                ss.WriteString(message);
            }            
        }
    }
}
