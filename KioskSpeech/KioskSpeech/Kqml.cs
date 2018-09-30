using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NU.Kiosk.SharedObject;
using log4net;
using System.Reflection;

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
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

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
                _log.Debug($"Sending: {kqml.ToString()}");
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

            var registermsg = $"(register :sender {this.name} :receiver facilitator :content (\"socket://{this.facilitatorIp}:{this.localPort}\" nil nil {this.localPort}))";
            //Console.WriteLine($"IP and port: '{this.facilitatorIp}, {facilitatorPort}, {this.localPort}'; Register Message: '{registermsg}'");
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
            _log.Info($"***Ready***\n");
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
            _log.Info($"Facilitator says: '{data}' - length {data.Length}");
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
                            _log.Error($"Error: {kqml.ToString()}");
                            break;
                        case "ask_all":
                        case "ask_one":
                        case "advertise":
                        case "untell":
                        case "subscribe":
                        default:
                            _log.Error($"Unknown KQML Performative: {kqml.performative}");
                            break;
                    }
                }
            } else
            {
                _log.Info($"Facilitator message ignored: {data}");
            }            
        }

        // performative handlers

        private void handlePing(KQMLMessage msg, AbstractSimpleSocket socket)
        {
            socket.Send($"(update :sender {this.name} :receiver facilitator :in-reply-to {msg.reply_with} :content (:agent psi :uptime 12h :state guess))");
        }

        private void handleTell(KQMLMessage msg, AbstractSimpleSocket socket)
        {
            receivedMsgs.Add(msg); // is this really necessary?
            _log.Debug($"Received tell message: {msg.ToString()}");
            //socket.Send(KQMLMessage.createTell(this.name, msg.sender, this.nextMsgId(), msg.reply_with, ":ok").ToString()); 
            //this.Out.Post(msg.content.ToString(), DateTime.Now); // utilize... later
        }

        private void handleAchieve(KQMLMessage msg, AbstractSimpleSocket socket)
        {
            _log.Debug($"handleAchieve picked up a message: {msg.ToString()}");

            receivedMsgs.Add(msg); // is this really necessary? --> change to logging in the future

            if (msg.content is string || msg.content is KQMLMessage)
            {
                KQMLMessage msg_content;
                if (msg.content is string)
                {
                    msg_content = (new KQMLMessageParser()).parse((string)msg.content);
                }
                else {
                    msg_content = (KQMLMessage)msg.content;
                }

                // msg_content should be in the form of "(TheSet (psikiShowMap ...) (psikiSayText "...") ...)"
                foreach (object o in msg_content.unaffiliated_obj_and_strings)
                {
                    KQMLMessage individual_message = (KQMLMessage)o;
                    List<object> object_list = individual_message.unaffiliated_obj_and_strings;
                    List<string> string_list = new List<string>();
                    for (int j = 0, size = object_list.Count; j < size; j++)
                    {
                        var strr = object_list[j].ToString().Trim('"');
                        string_list.Add(strr);
                    }

                    var a = new NU.Kiosk.SharedObject.Action(individual_message.performative, string_list.ToArray<string>());
                    this.Out.Post(a, DateTime.Now);
                    socket.Send(KQMLMessage.createTell(this.name, msg.sender, this.nextMsgId(), msg.reply_with, ":ok").ToString());
                }
            }
            else {
                _log.Error($"Cannot handleAchieve: {msg.content} is of type {msg.content.GetType()}");
            }
        }
    }

}
