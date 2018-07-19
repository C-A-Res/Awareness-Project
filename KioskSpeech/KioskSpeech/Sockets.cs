using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NU.Kqml
{
    static class SocketUtil
    {
        public static IPEndPoint getInterNetworkEndPoint(string hostNameOrAddress, int port)
        {
            IPHostEntry ipHostInfo = Dns.GetHostEntry(hostNameOrAddress);
            IPAddress ipAddress = null;
            foreach (IPAddress ipa in ipHostInfo.AddressList)
            {
                Console.WriteLine(ipa.ToString());
                Console.WriteLine(ipa.AddressFamily);
                if (ipa.AddressFamily == AddressFamily.InterNetwork)
                {
                    ipAddress = ipa;
                    break;
                }
            }
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, port);
            //Console.WriteLine("IPHostEntry:" + ipHostInfo.ToString());
            //Console.WriteLine("IPAddress:" + ipAddress.ToString());
            //Console.WriteLine("AddressFamily:" + ipAddress.AddressFamily);
            //Console.WriteLine("IsLoopback:" + IPAddress.IsLoopback(ipAddress));
            //Console.WriteLine("IPEndPoint:" + localEndPoint.ToString());

            return localEndPoint;
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

                //Console.WriteLine("Socket connected to {0}",
                //    sender.RemoteEndPoint.ToString());

            }
            catch (ArgumentNullException ane)
            {
                Console.WriteLine("ArgumentNullException : {0}", ane.ToString());
            }
            catch (SocketException se)
            {
                Console.WriteLine("SocketException : {0}", se.ToString());
            }
            catch (Exception e)
            {
                Console.WriteLine("Unexpected exception : {0}", e.ToString());
            }
        }

        public override void Send(string data)
        {
            //Console.WriteLine("Sending " + data);

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
                    //Console.WriteLine($"All {bytesSent} bytes sent");
                }
                else
                {
                    Console.WriteLine("WARNING: Message not completely sent");
                }

                // Receive the response from the remote device.  
                int bytesRec = sender.Receive(bytes);
                string resp = Encoding.ASCII.GetString(bytes, 0, bytesRec);
                //Console.WriteLine("Facilitator says = {0}", resp);
                OnMessage(resp, this);
            }
            catch (ArgumentNullException ane)
            {
                Console.WriteLine("ArgumentNullException : {0}", ane.ToString());
            }
            catch (SocketException se)
            {
                if (se.SocketErrorCode == SocketError.ConnectionAborted)
                {
                    Console.WriteLine("Socket closed by Facilitator");
                }
                else
                {
                    Console.WriteLine("SocketException : {0}", se.ToString());
                    Console.WriteLine("SocketError : {0}", se.SocketErrorCode);
                }
                
            }
            catch (Exception e)
            {
                Console.WriteLine("Unexpected exception : {0}", e.ToString());
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
                Console.WriteLine("SocketException : {0}", se.ToString());
            }
            catch (Exception e)
            {
                Console.WriteLine("Unexpected exception : {0}", e.ToString());
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

                    //Console.WriteLine("Waiting for a connection on " + localEndPoint.ToString() + ":" + this.port);
                    listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);
                    
                    allDone.WaitOne();
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            //Console.WriteLine("\nPress ENTER to continue...");
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
                Console.WriteLine("Listening socket disposed.");
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
                    //Console.WriteLine("Read {0} bytes from socket. \n Data : {1}",
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

            if (handler != null)
            {
                // Begin sending the data to the remote device.  
                handler.BeginSend(byteData, 0, byteData.Length, 0,
                    new AsyncCallback(SendCallback), handler);
            } else
            {
                Console.WriteLine("ERROR: Not able to reply with {0}", data);
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
                //Console.WriteLine("Sent {0} bytes to client.", bytesSent);

                handler.Shutdown(SocketShutdown.Both);
                handler.Close();

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }


        public void Close()
        {
            running = false;
            if (listener != null) listener.Close();
        }
    }


}
