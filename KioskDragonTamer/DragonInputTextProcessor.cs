using Microsoft.Psi;
using Microsoft.Psi.Audio;
using Microsoft.Psi.Components;
using Microsoft.Psi.Speech;
using NU.Kiosk.Speech;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NU.Kiosk.Speech
{
    class DragonInputTextProcessor : IDisposable
    {
        private const string destination_pipe_name = "dragon_synthesizer_pipe";

        NamedPipeClientStream pipeClient;
        StreamString ss;

        public DragonInputTextProcessor()
        {
        }

        public void Dispose()
        {
            Console.WriteLine("[DragonInputTextProcessor] Dispose");

            pipeClient.Close();
            pipeClient.Dispose();
        }
        
        public void Initialize()
        {
            pipeClient = new NamedPipeClientStream(".", destination_pipe_name, PipeDirection.Out, PipeOptions.None, TokenImpersonationLevel.Impersonation);

            Console.WriteLine("[DragonInputTextPreProcessor] Connecting to server... ");
            pipeClient.Connect();
            Console.WriteLine("[DragonInputTextPreProcessor] PreProcessor Connected!");

            ss = new StreamString(pipeClient);
        }

        public void Send(string message)
        {
            string updated_text = message;
            ss.WriteString(message);
        }
    }
}
