using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NU.Kqml
{
    static class SocketUtil
    {
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static IPEndPoint getInterNetworkEndPoint(string hostNameOrAddress, int port)
        {
            _log.Debug($"getInterNetworkEndPoint, hostNameOrAddress: '{hostNameOrAddress}'");
            IPHostEntry ipHostInfo = Dns.GetHostEntry(hostNameOrAddress);
            IPAddress ipAddress = null;
            IPEndPoint localEndPoint = null;

            foreach (IPAddress ipa in ipHostInfo.AddressList)
            {
                _log.Debug($"getInterNetworkEndPoint, ipa: '{ipa.ToString()}'; ipa.AddressFamily: '{ipa.AddressFamily}'");
                if (ipa.AddressFamily == AddressFamily.InterNetwork)
                {
                    ipAddress = ipa;
                    localEndPoint = new IPEndPoint(ipAddress, port);
                    return localEndPoint;
                }

            }
            IPEndPoint end = new IPEndPoint(IPAddress.Parse(hostNameOrAddress), port);
            _log.Debug($"getInterNetworkEndPoint, end: '{end}'");
            //IPEndPoint localEndPoint = new IPEndPoint(ipAddress, port);

            //_log.Info("IPHostEntry:" + ipHostInfo.ToString());
            //_log.Info("IPAddress:" + ipAddress.ToString());
            //_log.Info("AddressFamily:" + ipAddress.AddressFamily);
            //_log.Info("IsLoopback:" + IPAddress.IsLoopback(ipAddress));
            //_log.Info("IPEndPoint:" + localEndPoint.ToString());

            return end;
        }
    }

    abstract class AbstractSimpleSocket
    {
        public delegate void onmessage(string msg, AbstractSimpleSocket handler);

        public onmessage OnMessage
        {
            get;
            set;
        }

        public abstract void Send(string data);
    }

    // see https://docs.microsoft.com/en-us/dotnet/framework/network-programming/synchronous-client-socket-example
    class SimpleSocket : AbstractSimpleSocket
    {
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Socket sender;
        private IPEndPoint remoteEP;

        public SimpleSocket(string ipAddress, int port)
        {
            // Establish the remote endpoint for the socket.  
            // This example uses port 11000 on the local computer.  
            remoteEP = SocketUtil.getInterNetworkEndPoint(ipAddress, port);

            
        }

        public void Connect()
        {
            // Connect the socket to the remote endpoint. Catch any errors.  
            try
            {
                if (sender == null) {
                    // Create a TCP/IP  socket.  
                    sender = new Socket(AddressFamily.InterNetwork,
                        SocketType.Stream, ProtocolType.Tcp);
                }
                if (!sender.Connected)
                {
                    sender.Connect(remoteEP);
                }

                //_log.Info("Socket connected to {0}",
                //    sender.RemoteEndPoint.ToString());

            }
            catch (ArgumentNullException ane)
            {
                _log.Error($"Connect: ArgumentNullException : {ane.ToString()}");
            }
            catch (SocketException se)
            {
                _log.Error($"Connect: SocketException : {se.ToString()}");
            }
            catch (Exception e)
            {
                _log.Error($"Connect: Unexpected exception : {e.ToString()}");
            }
        }

        public override void Send(string data)
        {
            //_log.Info("Sending " + data);

            // Data buffer for incoming data.  
            byte[] bytes = new byte[1024];

            try
            {
                // Encode the data string into a byte array.  
                byte[] msg = Encoding.ASCII.GetBytes(data);

                // Send the data through the socket.  
                int bytesSent = sender.Send(msg);
                if (bytesSent == msg.Length)
                {
                    //_log.Info($"All {bytesSent} bytes sent");
                }
                else
                {
                    _log.Warn("Send: WARNING: Message not completely sent");
                }

                // Receive the response from the remote device.  
                int bytesRec = sender.Receive(bytes);
                string resp = Encoding.ASCII.GetString(bytes, 0, bytesRec);
                _log.Debug($"Send: in response, facilitator said {resp}");
                OnMessage(resp, this);
            }
            catch (ArgumentNullException ane)
            {
                _log.Info($"Send: ArgumentNullException : {ane.ToString()}");
            }
            catch (SocketException se)
            {
                if (se.SocketErrorCode == SocketError.ConnectionAborted)
                {
                    _log.Error("Send: Socket closed by Facilitator");
                }
                else
                {
                    _log.Error($"Send: SocketException : {se.ToString()}");
                    _log.Error($"Send: SocketError : {se.SocketErrorCode}");
                }
                
            }
            catch (Exception e)
            {
                _log.Error($"Send: Unexpected exception : {e.ToString()}");
            }
            Close();
        }

        public void Close()
        {
            try
            {
                // Release the socket.  
                if (sender != null)
                {
                    sender.Shutdown(SocketShutdown.Both);
                    sender.Close();
                }
                sender = null;
            }
            catch (SocketException se)
            {
                _log.Error($"Close: SocketException : {se.ToString()}");
            }
            catch (Exception e)
            {
                _log.Error($"Close: Unexpected exception : {e.ToString()}");
            }
        }
    }

    class StateObject
    {
        // Client  socket.  
        public Socket workSocket = null;
        // Size of receive buffer.  
        public const int BufferSize = 1024;
        // Receive buffer.  
        public byte[] buffer = new byte[BufferSize];
        // Received data string.  
        public StringBuilder sb = new StringBuilder();
    }

    // see https://docs.microsoft.com/en-us/dotnet/framework/network-programming/synchronous-server-socket-example
    class SimpleSocketServer : AbstractSimpleSocket
    {
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // Incoming data from the client.  
        public static string data = null;

        private int port = 9001;
        private Socket listener = null;
        private Socket handler = null;

        private Boolean running = true;        

        public SimpleSocketServer(int port)
        {
            this.port = port;
        }

        public static ManualResetEvent allDone = new ManualResetEvent(false);

        public void StartListening()
        {
            Thread myThread;
            myThread = new Thread(new ThreadStart(StartListening_thread));
            myThread.Start();
        }

        public void StartListening_thread()
        {
            // Data buffer for incoming data.  
            byte[] bytes = new Byte[1024];

            // Establish the local endpoint for the socket.  
            // Dns.GetHostName returns the name of the   
            // host running the application.  
            IPEndPoint localEndPoint = SocketUtil.getInterNetworkEndPoint(Dns.GetHostName(), port);

            // Create a TCP/IP socket.  
            listener = new Socket(AddressFamily.InterNetwork,
                SocketType.Stream, ProtocolType.Tcp);

            // Bind the socket to the local endpoint and   
            // listen for incoming connections.  
            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(10);

                // Start listening for connections.  
                while (running)
                {
                    // Set the event to nonsignaled state.
                    allDone.Reset();

                    //_log.Info("Waiting for a connection on " + localEndPoint.ToString() + ":" + this.port);
                    listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);
                    
                    allDone.WaitOne();
                }

            }
            catch (Exception e)
            {
                _log.Error($"StartListening_thread: {e.ToString()}");
            }

            //_log.Info("\nPress ENTER to continue...");
            //Console.Read();

        }

        public void AcceptCallback(IAsyncResult ar)
        {
            // Signal the main thread to continue
            allDone.Set();

            // Get the socket that handles the client request.
            Socket listener = (Socket)ar.AsyncState;
            try
            {
                Socket handler = listener.EndAccept(ar);

                // Create the state object.
                StateObject state = new StateObject();
                state.workSocket = handler;
                handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);

            } catch (ObjectDisposedException e)
            {
                _log.Error($"Listening socket disposed");
            }
        }

        public void ReadCallback(IAsyncResult ar)
        {
            String content = String.Empty;

            // Retrieve the state object and the handler socket  
            // from the asynchronous state object.  
            StateObject state = (StateObject)ar.AsyncState;
            handler = state.workSocket;

            if (handler != null)
            {
                // Read data from the client socket.   
                int bytesRead = handler.EndReceive(ar);

                if (bytesRead > 0)
                {
                    // There  might be more data, so store the data received so far.  
                    state.sb.Append(Encoding.ASCII.GetString(
                        state.buffer, 0, bytesRead));

                    content = state.sb.ToString();
                    //_log.Info("Read {0} bytes from socket. \n Data : {1}",
                    //    content.Length, content);

                    
                    if (handler.Available > 0)
                    {
                        handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                        new AsyncCallback(ReadCallback), state);
                    } else
                    {
                        OnMessage(content, this);
                    }

                }

            }
        }

        public override void Send(String data)
        {
            // Convert the string data to byte data using ASCII encoding.  
            byte[] byteData = Encoding.ASCII.GetBytes(data);
            //Console.Write($"ByteData: ");
            //for (int i = 0, l = byteData.Length; i < l; i++)
            //{
            //    Console.Write($"-{byteData[i]}- ");
            //}
            //_log.Info();

            if (handler != null)
            {
                try
                {
                    // Begin sending the data to the remote device.  
                    handler.BeginSend(byteData, 0, byteData.Length, 0,
                        new AsyncCallback(SendCallback), handler);
                } catch (Exception e)
                {
                    _log.Error($"Send: error during send '{data}'; trace: {e.StackTrace}");
                }
            } else
            {
                _log.Error($"Send: ERROR: Not able to reply with {data}");
            }
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.  
                Socket handler = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.  
                int bytesSent = handler.EndSend(ar);
                //_log.Info("Sent {0} bytes to client.", bytesSent);

                handler.Shutdown(SocketShutdown.Both);
                handler.Close();

            }
            catch (Exception e)
            {
                _log.Error($"SendCallback: {e.ToString()}");
            }
        }


        public void Close()
        {
            running = false;
            if (listener != null) listener.Close();
        }
    }


}
