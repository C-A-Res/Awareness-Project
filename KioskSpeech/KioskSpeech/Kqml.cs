using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NU.Kqml
{
    using Microsoft.Psi;
    using Microsoft.Psi.Components;

    //public class SocketEchoer
    //{
    //    private readonly string facilitatorIp;
    //    private readonly int facilitatorPort;
    //    private readonly int localPort;
    //    private readonly string name = "echoer";

    //    private SimpleSocketServer listener;
    //    private SimpleSocket facilitator;

    //    public SocketEchoer(string facilitatorIp = "127.0.0.1", int facilitatorPort = 9000, int localPort = 6000)
    //    {
    //        this.facilitatorIp = facilitatorIp;
    //        this.facilitatorPort = facilitatorPort;
    //        this.localPort = localPort;

    //        // start listening
    //        this.listener = new SimpleSocketServer(this.localPort);
    //        this.listener.OnMessage = this.ProcessMessageFromUpstream; // push the data downstream
    //        this.listener.StartListening();

    //        // register with facilitator
    //        facilitator = new SimpleSocket(this.facilitatorIp, facilitatorPort);
    //        facilitator.OnMessage = this.ProcessMessageFromUpstream;
    //        facilitator.Connect();
    //        //var registermsg = $"(register :sender {this.name} :receiver facilitator :content (\"socket://192.168.56.1:{this.localPort}\" nil nil {this.localPort}))";
    //        var registermsg = $"(register :sender {this.name} :receiver facilitator :content (\"socket://127.0.0.1:{this.localPort}\" nil nil {this.localPort}))";
    //        //Console.Write($"{registermsg}");
    //        facilitator.Send(registermsg);
    //        facilitator.Close();
    //    }

    //    private void ProcessMessageFromUpstream(string data, AbstractSimpleSocket socket)
    //    {

    //        KQMLMessage msg = KQMLMessage.parseMessage(data);

    //        if (msg.sender != "facilitator")
    //        {
    //            Console.WriteLine($"[Echoer] Recieved {data}");
    //            string temp = msg.receiver;
    //            msg.receiver = msg.sender;
    //            msg.sender = temp;

    //            facilitator = new SimpleSocket(this.facilitatorIp, facilitatorPort);
    //            facilitator.OnMessage = this.ProcessMessageFromUpstream;
    //            facilitator.Connect();
    //            var outbound_msg = msg.ToString();
    //            Console.Write($"Echoer outbound message: {outbound_msg}");
    //            facilitator.Send(outbound_msg);
    //            facilitator.Close();
    //        }
    //        else if (msg.performative == "ping")
    //        {
    //            Console.WriteLine($"[Echoer] Ping");
    //            socket.Send($"(ping :sender {this.name} :receiver facilitator :in-reply-to {msg.reply_with})");
    //        }
    //        else
    //        {
    //            Console.WriteLine($"[Echoer] Message received from facilitator: {msg.performative}");
    //        }
    //    }
    //}

    public class SocketStringConsumer : ConsumerProducer<string, string>, Microsoft.Psi.Components.IStartable
    {
        //private readonly Pipeline pipeline;
        private readonly int localPort;
        private readonly string facilitatorIp;
        private readonly int facilitatorPort;
        private readonly Pipeline pipeline;

        private bool ready = false;

        private SimpleSocketServer listener;
        private SimpleSocket facilitator;
        private string name = "psi";
        private int messageCounter = 0;

        private List<KQMLMessage> receivedMsgs = new List<KQMLMessage>();

        public SocketStringConsumer(Pipeline pipeline, string fIP, int fP, int localP)
            : base(pipeline)
        {
            this.pipeline = pipeline;

            this.localPort = localP;
            this.facilitatorIp = fIP;
            this.facilitatorPort = fP;
            //this.In = pipeline.CreateReceiver<string>(this, ReceiveString, "SocketReceiver");
        }

        protected override void Receive(string message, Envelope e)
        {
            //Console.WriteLine($"[SocketStringConsumer] Consuming: {message}");
            if (ready && message.Length > 5)
            {
                //var kqml = KQMLMessage.createAchieve(name, "interaction-manager", nextMsgId(), null, $"(processUserUtterance HandMadeEEs-Library \"{message}\")");
                var kqml = KQMLMessage.createAchieve(name, "interaction-manager", nextMsgId(), null, $"(processKioskUtterance \"{message}\")"); // FIXME send to self for testing
                facilitator.Connect();
                Console.WriteLine($"[SocketStringConsumer] Sending: {kqml.ToString()}");
                facilitator.Send(kqml.ToString());
            }
        }

        public void Start(Action onCompleted, ReplayDescriptor descriptor)
        {
            // start listening
            this.listener = new SimpleSocketServer(this.localPort);
            this.listener.OnMessage = this.ProcessMessageFromUpstream; // push the data downstream
            this.listener.StartListening();

            // register with facilitator
            facilitator = new SimpleSocket(this.facilitatorIp, facilitatorPort);
            facilitator.OnMessage = this.ProcessMessageFromUpstream;
            facilitator.Connect();
            //var registermsg = $"(register :sender {this.name} :receiver facilitator :content (\"socket://192.168.56.1:{this.localPort}\" nil nil {this.localPort}))";
            var registermsg = $"(register :sender {this.name} :receiver facilitator :content (\"socket://127.0.0.1:{this.localPort}\" nil nil {this.localPort}))";
            //Console.Write($"{registermsg}");
            facilitator.Send(registermsg);
            facilitator.Close();

            // advertise - this is not right yet
            //string fn = "test";
            //string msgid = nextMsgId();
            //var admsg = $"(advertise :sender {this.name} :receiver facilitator :reply-with {msgid} " +
            //    $":content (ask-all :receiver {this.name} :in-reply-to {msgid} :content {fn}))";
            //facilitator.Connect();
            //facilitator.Send(admsg);
            //facilitator.Close();

            this.ready = true;
            Console.WriteLine("\n***Ready***\n");
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
            Console.WriteLine("[SocketStringConsumer] Facilitator says: " + data);
            KQMLMessage kqml = KQMLMessage.parseMessage(data);
            //if (kqml.performative != "ping")
            //{
            
            //}            
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
        }

        // performative handlers

        private void handlePing(KQMLMessage msg, AbstractSimpleSocket socket)
        {
            socket.Send($"(update :sender {this.name} :receiver facilitator :in-reply-to {msg.reply_with} :content (:agent psi :uptime 12h :state guess)");
        }

        private void handleTell(KQMLMessage msg, AbstractSimpleSocket socket)
        {
            receivedMsgs.Add(msg);
            Console.Write($"Received tell message: {msg.ToString()}");
            //socket.Send(KQMLMessage.createTell(this.name, msg.sender, this.nextMsgId(), msg.reply_with, ":ok").ToString());
            //this.Out.Post(msg.content.ToString(), DateTime.Now);
        }

        private void handleAchieve(KQMLMessage msg, AbstractSimpleSocket socket)
        {
            receivedMsgs.Add(msg);
            this.Out.Post(msg.content.ToString(), DateTime.Now);
            socket.Send(KQMLMessage.createTell(this.name, msg.sender, this.nextMsgId(), msg.reply_with, ":ok").ToString());
        }
    }

    class KQMLMessage
    {
        public string performative { get; private set; }
        public string sender { get; set; }
        public string receiver;
        public string reply_with;
        public string reply_to;
        public object content { get; private set; }
        Dictionary<string, object> other = new Dictionary<string, object>();

        private string[] standard_keys = new string[] { ":sender", ":receiver", ":reply-with", ":in-reply-to", ":content" };

        private KQMLMessage(string perf, string send, string rec, string with, string to, object cont)
        {
            this.performative = perf;
            this.sender = send;
            this.receiver = rec;
            this.reply_with = with;
            this.reply_to = to;
            this.content = cont;
        }

        private KQMLMessage(string perf, Dictionary<string, object> dict)
        {
            this.performative = perf;
            object temp = null;
            if (dict.TryGetValue(":sender", out temp))
            {
                this.sender = (string)temp;
            }
            if (dict.TryGetValue(":receiver", out temp))
            {
                this.receiver = (string)temp;
            }
            if (dict.TryGetValue(":reply-with", out temp))
            {
                this.reply_with = (string)temp;
            }
            if (dict.TryGetValue(":in-reply-to", out temp))
            {
                this.reply_to = (string)temp;
            }
            if (dict.TryGetValue(":content", out temp))
            {
                this.content = temp;
            }
            foreach (KeyValuePair<string, object> entry in dict)
            {
                if (!standard_keys.Contains(entry.Key))
                {
                    other.Add(entry.Key, entry.Value);
                }
            }
        }

        public static KQMLMessage createAchieve(string sender, string receiver, string reply_with, string reply_to, KQMLMessage message)
        {
            return new KQMLMessage("achieve", sender, receiver, reply_with, reply_to, message);
        }

        public static KQMLMessage createAchieve(string sender, string receiver, string reply_with, string reply_to, string message)
        {
            return new KQMLMessage("achieve", sender, receiver, reply_with, reply_to, message);
        }

        public static KQMLMessage createTask(string sender, string receiver, string reply_with, string reply_to, string action)
        {
            return new KQMLMessage("task", sender, receiver, reply_with, reply_to, action);
        }

        public static KQMLMessage createTell(string sender, string receiver, string reply_with, string reply_to, string tell)
        {
            return new KQMLMessage("tell", sender, receiver, reply_with, reply_to, tell);
        }

        public static KQMLMessage createInsert(string sender, string receiver, string reply_with, string reply_to, string fact)
        {
            return new KQMLMessage("insert", sender, receiver, reply_with, reply_to, fact);
        }

        public static KQMLMessage parseMessage(string message)
        {
            return parseMessage(new StringReader(message));
        }

        public static KQMLMessage parseMessage(StringReader sr)
        {
            if (sr.Read() == '(')
            {
                char next;
                string perf = null;
                if (sr.Peek() != ':')
                {
                    StringBuilder sb = new StringBuilder();
                    next = (char)sr.Read();
                    while (next != ' ')
                    {
                        sb.Append(next);
                        next = (char)sr.Read();
                    }
                    perf = sb.ToString();
                }

                Dictionary<string, object> dict = new Dictionary<string, object>();
                next = (char)sr.Peek();
                while (next != ')')
                {
                    //if (next == ':')
                    //{
                        string key = parseKey(sr);
                        if (key != null)
                        {
                            object value = parseValue(sr, false);
                            dict.Add(key, value);
                        }
                    //} else
                    //{
                    //    object value = parseValue(sr, false);
                        // now put this value somewhere
                    //}
                    next = (char)sr.Peek();
                }

                return new KQMLMessage(perf, dict);
            }
            else
            {
                return null;
            }
        }

        private static string parseKey(StringReader sr)
        {
            char next = (char)sr.Read();
            if (next == ':')
            {
                StringBuilder sb = new StringBuilder();
                while (next != ' ')
                {
                    sb.Append(next);
                    next = (char)sr.Read();
                }
                return sb.ToString();
            }
            else
            {
                return null;
            }
        }

        private static object parseValue(StringReader sr, bool isQuoted)
        {
            char next = (char)sr.Peek();
            if (next != '(' && !isQuoted)
            {
                StringBuilder sb = new StringBuilder();
                while (next != ' ' || isQuoted)
                {
                    if (next == '"')
                    {
                        next = (char)sr.Read();
                        sb.Append(next);
                        next = (char)sr.Peek();
                        isQuoted = !isQuoted;
                        if (isQuoted)
                        {
                            continue;
                        }
                        else
                        {
                            break;
                        }
                    }
                    else if (next == ')' && !isQuoted)
                    {
                        break;
                    }
                    next = (char)sr.Read();
                    sb.Append(next);
                    next = (char)sr.Peek();
                }
                return sb.ToString();
            }
            else
            {
                return parseMessage(sr);
            }
        }


        public override string ToString()
        {
            StringBuilder sb = new StringBuilder("(");
            if (this.performative != null)
            {
                sb.Append(performative).Append(' ');
            }
            if (this.sender != null)
            {
                sb.Append(":sender ").Append(sender).Append(' ');
            }
            if (this.receiver != null)
            {
                sb.Append(":receiver ").Append(receiver).Append(' ');
            }
            if (this.reply_to != null)
            {
                sb.Append(":in-reply-to ").Append(reply_to).Append(' ');
            }
            if (this.reply_with != null)
            {
                sb.Append(":reply-with ").Append(reply_with).Append(' ');
            }
            if (this.content != null)
            {
                sb.Append(":content ").Append(content).Append(' ');
            }
            foreach (KeyValuePair<string, object> entry in other)
            {
                if (!standard_keys.Contains(entry.Key))
                {
                    if (entry.Value != null)
                    {
                        sb.Append(entry.Key).Append(' ').Append(entry.Value.ToString()).Append(' ');
                    }
                }
            }
            return sb.Append(")").ToString();
        }
    }


}
