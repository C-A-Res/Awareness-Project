using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NU.Kiosk
{
    class PipeListener
    {
        private string listener_pipe_name; 

        private const int NumThreads = 2;
        private NamedPipeServerStream pipeServer;
        private StreamString ss;
        private Thread listenerThread;

        private Action<string> string_handler;

        private IPipeUtilUsers parent;

        public PipeListener(Action<string> action_on_result, string listener_pipe_name, IPipeUtilUsers parent)
        {
            string_handler += action_on_result;
            this.listener_pipe_name = listener_pipe_name;
            this.parent = parent;
        }

        public void Dispose()
        {
            Console.WriteLine($"[PipeListener.{listener_pipe_name}] Dispose");
            if (listenerThread != null)
            {
                listenerThread.Abort();
            }           

            if (pipeServer.CanWrite)
            {
                pipeServer.Disconnect();
            }
            pipeServer.Dispose();
        }

        public void Initialize()
        {
            if (string_handler == null)
            {
                throw new Exception($"[PipeListener.{listener_pipe_name}] string_handler is not set");
            }

            pipeServer = new NamedPipeServerStream(listener_pipe_name, PipeDirection.In, NumThreads);
            Console.WriteLine($"[PipeListener.{listener_pipe_name}] Waiting for connection... ");
            pipeServer.WaitForConnection();
            Console.WriteLine($"[PipeListener.{listener_pipe_name}] Connected!");

            ss = new StreamString(pipeServer);
            listenerThread = new Thread(StartListening);
            listenerThread.Start();
        }

        public void Reconnect()
        {
            parent.ReconnectSenders();
            pipeServer = new NamedPipeServerStream(listener_pipe_name, PipeDirection.In, NumThreads);
            Console.WriteLine($"[PipeListener.{listener_pipe_name}] Waiting for connection... ");
            pipeServer.WaitForConnection();
            Console.WriteLine($"[PipeListener.{listener_pipe_name}] Connected!");
            ss = new StreamString(pipeServer);
        }

        private void StartListening(object o)
        {
            while (true)
            {
                while (true)
                {
                    var res = ss.ReadString();
                    if (res == null)
                    {
                        Console.WriteLine($"[PipeListener.{listener_pipe_name}] has stopped listening");
                        break;
                    }
                    Console.WriteLine($"[PipeListener.{listener_pipe_name}] received: '{res}'");
                    string_handler(res);
                }
                Reconnect();
            }
        }

    }
}
