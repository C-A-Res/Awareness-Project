using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NU.Kiosk.SharedObject;

namespace NU.Kqml
{
    using Microsoft.Psi;
    using Microsoft.Psi.Components;

    public class SocketEchoer
    {
        private readonly string facilitatorIp;
        private readonly int facilitatorPort;
        private readonly int localPort;
        private readonly string name = "echoer";
        private bool ready = false;
        private int messageCounter = 0;

        private SimpleSocketServer listener;
        private SimpleSocket facilitator = null;

        public SocketEchoer(string facilitatorIp = "127.0.0.1", int facilitatorPort = 9000, int localPort = 6000)
        {
            this.facilitatorIp = facilitatorIp;
            this.facilitatorPort = facilitatorPort;
            this.localPort = localPort;

            // start listening
            this.listener = new SimpleSocketServer(this.localPort);
            this.listener.OnMessage = this.ProcessMessageFromUpstream; // push the data downstream
            this.listener.StartListening();

            facilitator = new SimpleSocket(this.facilitatorIp, facilitatorPort);
            facilitator.OnMessage = this.ProcessMessageFromUpstream;

            this.ready = true;

            // register with facilitator
            //facilitator = new SimpleSocket(this.facilitatorIp, facilitatorPort);
            //facilitator.OnMessage = this.ProcessMessageFromUpstream;
            //facilitator.Connect();
            //var registermsg = $"(register :sender {this.name} :receiver facilitator :content (\"socket://127.0.0.1:{this.localPort}\" nil nil {this.localPort}))";
            //facilitator.Send(registermsg);
            //facilitator.Close();
        }

        private void ProcessMessageFromUpstream(string data, AbstractSimpleSocket socket)
        {
            KQMLMessage msg = (new KQMLMessageParser()).parse(data);
            socket.Send(KQMLMessage.createTell(msg.receiver, msg.sender, msg.reply_with, "echoer", ":ok").ToString());

            if (msg.performative != "tell" && msg.performative != "register")
            {
                Console.WriteLine($"[Echoer] Recieved data: {data}.");// ; Parsed content: {msg.content}");
                string temp = msg.receiver;
                msg.receiver = msg.sender;
                msg.sender = temp;

                facilitator.Connect();
                var outbound_msg = msg.ToString();
                //Console.WriteLine($"[Echoer] Outbound message: {outbound_msg}");
                facilitator.Send(outbound_msg);
                facilitator.Close();
            }

        }

        private String nextMsgId()
        {
            return $"echoer-id{this.messageCounter++}";
        }
    }

    public class SocketStringConsumer : ConsumerProducer<string, NU.Kiosk.SharedObject.Action>, Microsoft.Psi.Components.IStartable
    {
        private readonly int localPort;
        private readonly string facilitatorIp;
        private readonly int facilitatorPort;
        private readonly string default_achieve_destination;
        private readonly Pipeline pipeline;

        private bool ready = false;

        private SimpleSocketServer listener;
        private SimpleSocket facilitator;
        private string name = "psi";
        private int messageCounter = 0;

        private List<KQMLMessage> receivedMsgs = new List<KQMLMessage>();

        public SocketStringConsumer(Pipeline pipeline, string fIP, int fP, int localP, string default_achieve_destination = "interaction-manager")
            : base(pipeline)
        {
            this.pipeline = pipeline;
            this.localPort = localP;
            this.facilitatorIp = fIP;
            this.facilitatorPort = fP;
            this.default_achieve_destination = default_achieve_destination;
        }

        protected override void Receive(string message, Envelope e)
        {
            //Console.WriteLine($"[SocketStringConsumer] Consuming: {message}");
            if (ready && message.Length > 5)
            {
                var kqml = KQMLMessage.createAchieve(name, this.default_achieve_destination, nextMsgId(), null, $"(task :action (processKioskUtterance \"{message}\"))");
                //facilitator = new SimpleSocket(this.facilitatorIp, facilitatorPort);
                //facilitator.OnMessage = this.ProcessMessageFromUpstream;
                facilitator.Connect();
                Console.WriteLine($"[SocketStringConsumer] Sending: {kqml.ToString()}");
                facilitator.Send(kqml.ToString());
                //facilitator.Close();
            }
        }

        public void Start(System.Action onCompleted, ReplayDescriptor descriptor)
        {
            // start listening
            this.listener = new SimpleSocketServer(this.localPort);
            this.listener.OnMessage = this.ProcessMessageFromUpstream; // push the data downstream
            this.listener.StartListening();

            // register with facilitator
            facilitator = new SimpleSocket(this.facilitatorIp, facilitatorPort);
            facilitator.OnMessage = this.ProcessMessageFromUpstream;
            facilitator.Connect();

            var registermsg = $"(register :sender {this.name} :receiver facilitator :content (\"socket://127.0.0.1:{this.localPort}\" nil nil {this.localPort}))";
            facilitator.Send(registermsg);
            facilitator.Close(); 

            // advertise - skipped
            //string fn = "test";
            //string msgid = nextMsgId();
            //var admsg = $"(advertise :sender {this.name} :receiver facilitator :reply-with {msgid} " +
            //    $":content (ask-all :receiver {this.name} :in-reply-to {msgid} :content {fn}))";
            //facilitator.Connect();
            //facilitator.Send(admsg);
            //facilitator.Close();

            this.ready = true;
            Console.WriteLine("[SocketStringConsumer] ***Ready***\n");
        }

        public void Stop()
        {
            listener.Close();
        }

        private String nextMsgId()
        {
            return $"psi-id{this.messageCounter++}";
        }

        private void ProcessMessageFromUpstream(string data, AbstractSimpleSocket socket)
        {
            // push this into Out
            Console.WriteLine($"[SocketStringConsumer] Facilitator says: {data} - length {data.Length}");
            if (data.Length > 3)
            {
                KQMLMessage kqml = (new KQMLMessageParser()).parse(data);
                if (kqml != null && ready)
                {
                    switch (kqml.performative)
                    {
                        case "ping":
                        case "common-lisp-user::ping":
                            handlePing(kqml, socket);
                            break;
                        case "achieve":
                        case "common-lisp-user::achieve":
                            handleAchieve(kqml, socket);
                            break;
                        case "tell":
                        case "common-lisp-user::tell":
                            handleTell(kqml, socket);
                            break;
                        case "error":
                            Console.WriteLine($"[SocketStringConsumer] Error: {kqml.ToString()}");
                            break;
                        case "ask_all":
                        case "ask_one":
                        case "advertise":
                        case "untell":
                        case "subscribe":
                        default:
                            Console.WriteLine($"[SocketStringConsumer] Unknown KQML Performative: {kqml.performative}");
                            break;
                    }
                }
            } else
            {
                Console.WriteLine($"[SocketStringConsumer] Facilitator message ignored: {data}");
            }            
        }

        // performative handlers

        private void handlePing(KQMLMessage msg, AbstractSimpleSocket socket)
        {
            Console.WriteLine("[SocketStringConsumer] pinged");
            socket.Send($"(update :sender {this.name} :receiver facilitator :in-reply-to {msg.reply_with} :content (:agent psi :uptime 12h :state guess))");
        }

        private void handleTell(KQMLMessage msg, AbstractSimpleSocket socket)
        {
            receivedMsgs.Add(msg); // is this really necessary?
            Console.WriteLine($"[SocketStringConsumer] Received tell message: {msg.ToString()}");
            //socket.Send(KQMLMessage.createTell(this.name, msg.sender, this.nextMsgId(), msg.reply_with, ":ok").ToString()); 
            //this.Out.Post(msg.content.ToString(), DateTime.Now); // utilize... later
        }

        private void handleAchieve(KQMLMessage msg, AbstractSimpleSocket socket)
        {
            Console.WriteLine($"[SocketStringConsumer] handleAchieve picked up a message: {msg.ToString()}");

            receivedMsgs.Add(msg); // is this really necessary? --> change to logging in the future
            List<object> object_list = ((KQMLMessage)msg.content).unaffiliated_obj_and_strings;
            if (object_list.Count <= 1)
            {
                Console.WriteLine($"[SocketStringConsumer] handleAchieve picked up a malformed message: {msg.ToString()}");
            }
            else
            {
                List<string> string_list = new List<string>();
                for (int i = 1, size = object_list.Count; i < size; i++)
                {
                    var str = object_list[i].ToString().Trim('"');
                    string_list.Add(str);
                }

                var a = new NU.Kiosk.SharedObject.Action((string)object_list[0], string_list);
                this.Out.Post(a, DateTime.Now);

                //this.Out.Post(msg.content.ToString().Trim('"'), DateTime.Now);
                socket.Send(KQMLMessage.createTell(this.name, msg.sender, this.nextMsgId(), msg.reply_with, ":ok").ToString());
            }
        }
    }

}
