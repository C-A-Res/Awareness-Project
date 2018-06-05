using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NU.Kqml
{
    using Microsoft.Psi;

    public class SocketStringConsumer : IConsumer<string>, Microsoft.Psi.Components.IStartable
    {
        //private readonly Pipeline pipeline;
        private readonly int localPort;
        private readonly string facilitatorIp;
        private readonly int facilitatorPort;

        private bool ready = false;

        private SimpleSocketServer listener;
        private SimpleSocket facilitator;
        private string name = "psi";
        private int messageCounter = 0;

        private List<KQMLMessage> receivedMsgs = new List<KQMLMessage>();

        public SocketStringConsumer(Microsoft.Psi.Pipeline pipeline, string fIP, int fP, int localP)
        {
            this.localPort = localP;
            this.facilitatorIp = fIP;
            this.facilitatorPort = fP;
            this.In = pipeline.CreateReceiver<string>(this, ReceiveString, "SocketReceiver");
        }

        //public Receiver<string> In => ((IConsumer<string>)receiver).In;
        public Receiver<string> In { get; private set; }

        private void ReceiveString(string message, Envelope e)
        {
            //Console.WriteLine($"Consuming {message}");
            if (ready && message.Length > 5)
            {
                var kqml = KQMLMessage.createAchieve(name, "interaction-manager", nextMsgId(), null, $"(processUserUtterance HandMadeEEs-Library \"{message}\")");
                facilitator.Connect();
                facilitator.Send(kqml.ToString());
            }
        }

        public void Start(Action onCompleted, ReplayDescriptor descriptor)
        {
            // register with facilitator
            facilitator = new SimpleSocket(this.facilitatorIp, facilitatorPort);
            facilitator.OnMessage = this.handleRemoteMessage;
            facilitator.Connect();
            var registermsg = $"(register :sender {this.name} :receiver facilitator :content (\"socket://192.168.56.1:{this.localPort}\" nil nil {this.localPort}))";
            facilitator.Send(registermsg);

            // advertise - this is not right yet
            string fn = "test";
            string msgid = nextMsgId();
            var admsg = $"(advertise :sender {this.name} :receiver facilitator :reply-with {msgid} " +
                $":content (ask-all :receiver {this.name} :in-reply-to {msgid} :content {fn}))";
            facilitator.Connect();
            facilitator.Send(admsg);
            facilitator.Close();

            this.ready = true;
            Console.WriteLine("\n***Ready***\n");

            // start listening
            this.listener = new SimpleSocketServer(this.localPort);
            this.listener.OnMessage = this.handleRemoteMessage;
            this.listener.StartListening();

        }

        public void Stop()
        {
            listener.Close();
        }

        private String nextMsgId()
        {
            return $"psi-id{this.messageCounter++}";
        }

        private void handleRemoteMessage(string data, AbstractSimpleSocket socket)
        {
            Console.WriteLine($"\nFacilitator says: {data}\n");
            KQMLMessage kqml = KQMLMessage.parseMessage(data);
            //Console.WriteLine("Facilitator says: " + kqml.ToString());
            if (kqml != null)
            {
                if (kqml.performative == "ping")
                {
                    handlePing(kqml, socket);
                }
                else if (kqml.performative == "achieve")
                {

                }
                else if (kqml.performative == "ask_all")
                {

                }
                else if (kqml.performative == "ask_one")
                {

                }
                else if (kqml.performative == "advertise")
                {

                }
                else if (kqml.performative == "tell")
                {
                    handleTell(kqml, socket);
                }
                else if (kqml.performative == "untell")
                {

                }
                else if (kqml.performative == "advertise")
                {

                }
                else if (kqml.performative == "subscribe")
                {

                }
            }
        }

        // performative handlers

        private void handlePing(KQMLMessage msg, AbstractSimpleSocket socket)
        {
            socket.Send($"(ping :sender psi :receiver facilitator :in-reply-to {msg.reply_with})");
        }

        private void handleTell(KQMLMessage msg, AbstractSimpleSocket socket)
        {
            receivedMsgs.Add(msg);
            socket.Send(KQMLMessage.createTell(this.name, msg.sender, this.nextMsgId(), msg.reply_with, ":ok").ToString());
        }
    }

    class KQMLMessage
    {
        public string performative { get; private set; }
        public string sender { get; private set; }
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
                    string key = parseKey(sr);
                    if (key != null)
                    {
                        object value = parseValue(sr);
                        dict.Add(key, value);
                    }
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

        private static object parseValue(StringReader sr)
        {
            char next = (char)sr.Peek();
            if (next != '(')
            {
                StringBuilder sb = new StringBuilder();
                while (next != ' ')
                {
                    if (next == ')')
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
