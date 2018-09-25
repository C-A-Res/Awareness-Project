using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NU.Kiosk.SharedObject
{
    public class KQMLMessageParser
    {
        enum State { init, obj_init, obj, obj_complete, performative_seek, performative, keyword, str, regular_token, segment_complete, terminal };
        Stack<State> stk = new Stack<State>();
        StringReader reader;

        public KQMLMessageParser()
        {
        }

        public KQMLMessage parse(string input)
        {
            this.stk = new Stack<State>();
            this.reader = new StringReader(input.Trim());
            Console.WriteLine($"[KQMLMessageParser] Parsing input: {input}");
            stk.Push(State.init);
            return parse(new Dictionary<string, object>());
        }

        private KQMLMessage parse(Dictionary<string, object> current_dict)
        {
            processChar(null);
            State current_state = stk.Peek();

            while (current_state != State.obj_init)
            {
                processChar(null);
                current_state = stk.Peek();
            }

            if (current_state == State.obj_init)
            {
                // first object
                reader.Read();
                stk.Pop();
                stk.Push(State.obj);
                stk.Push(State.performative_seek);
                current_state = stk.Peek();
            }

            string current_keyword = null;
            while (processSegment(current_dict, ref current_keyword))
            {
                // process
            }
            stk.Pop();
            return new KQMLMessage(current_dict);
        }
        
        private bool processSegment(Dictionary<string, object> current_dict, ref string current_keyword)
        {
            StringBuilder current_segment = new StringBuilder();

            State current_state = stk.Peek();

            while (current_state != State.segment_complete && current_state != State.obj_complete && current_state != State.obj_init && current_state != State.terminal)
            {
                processChar(current_segment);
                current_state = stk.Peek();
            }
            current_state = stk.Pop();
            switch (current_state)
            {
                case State.obj_init:
                    stk.Push(State.obj_init);
                    if (current_keyword == null)
                    {
                        object objs;
                        if (!current_dict.ContainsKey("unaffiliated-objects-or-strings"))
                        {
                            current_dict.Add("unaffiliated-objects-or-strings", new List<object>());
                        }
                        current_dict.TryGetValue("unaffiliated-objects-or-strings", out objs);
                        ((List<object>)objs).Add(parse(new Dictionary<string, object>()));
                    } else
                    {
                        current_dict.Add(current_keyword, parse(new Dictionary<string, object>()));
                        current_keyword = null;
                    }
                    return true;
                case State.terminal:
                    stk.Push(State.terminal);
                    goto case State.obj_complete;
                case State.obj_complete:
                    if (current_segment.Length > 0)
                    {
                        if (stk.Peek() == State.performative)
                        {
                            current_keyword = "performative";
                        }
                        else if (current_keyword == null)
                        {
                            object strings;
                            if (!current_dict.ContainsKey("unaffiliated-objects-or-strings"))
                            {
                                current_dict.Add("unaffiliated-objects-or-strings", new List<object>());
                            }
                            current_dict.TryGetValue("unaffiliated-objects-or-strings", out strings);
                            ((List<object>)strings).Add(current_segment.ToString());
                        }
                        else
                        {
                            current_dict.Add(current_keyword, current_segment.ToString());
                        }
                    }
                    // return control to parse without popping
                    return false;
                case State.segment_complete:
                    // check if it is a keyword
                    if (stk.Peek() == State.keyword)
                    {
                        if (current_keyword != null)
                        {
                            current_dict.Add(current_keyword, current_segment.ToString());
                            current_keyword = null;
                        }
                        else
                        {
                            current_keyword = current_segment.ToString();
                        }
                    }
                    else
                    {
                        if (stk.Peek() == State.performative)
                        {
                            current_dict.Add("performative", current_segment.ToString()); // this does not interfere with full tuple type
                        }
                        else if (current_keyword == null)
                        {
                            object strings;
                            if (!current_dict.ContainsKey("unaffiliated-objects-or-strings"))
                            {
                                current_dict.Add("unaffiliated-objects-or-strings", new List<object>());
                            }
                            current_dict.TryGetValue("unaffiliated-objects-or-strings", out strings);
                            ((List<object>)strings).Add(current_segment.ToString());
                        }
                        else
                        {
                            current_dict.Add(current_keyword, current_segment.ToString());
                            current_keyword = null;
                        }
                    }
                    current_segment.Clear();
                    stk.Pop();
                    return true;
                default:
                    Console.WriteLine($"[KQMLMessageParser] Incorrect message formulation. Current segment: {current_segment}; Remaining string: {reader.ReadToEnd()}");
                    throw new Exception($"[KQMLMessageParser] Incorrect message formulation. Current segment: {current_segment}; Remaining string: {reader.ReadToEnd()}");
            }

        }

        private void processChar(StringBuilder current_segment)
        {
            char c = (char)reader.Peek();
            if (c <= 0)
            {
                stk.Push(State.terminal);
                return;
            }
            switch (c)
            {
                case '(':
                    if (stk.Peek() == State.str)
                    {
                        current_segment.Append((char)reader.Read());
                    }
                    else
                    {
                        if (stk.Peek() == State.init)
                        {
                            stk.Pop();
                            stk.Push(State.obj_init);
                        }
                        else if (stk.Peek() != State.obj_init)
                        {
                            stk.Push(State.obj_init);
                        }
                        // then / otherwise directly return control
                        return;
                    }
                    break;
                case ')':
                    if (stk.Peek() == State.str)
                    {
                        current_segment.Append((char)reader.Read());
                    }
                    else
                    {
                        reader.Read();
                        stk.Push(State.obj_complete);
                    }
                    break;
                case '"':
                    current_segment.Append((char)reader.Read());
                    if (stk.Peek() == State.str)
                    {
                        stk.Push(State.segment_complete);
                    }
                    else
                    {
                        if (stk.Peek() == State.performative_seek)
                        {
                            stk.Pop();
                        }
                        stk.Push(State.str);
                    }
                    break;
                case ' ':
                case '\n':
                case '\t':
                    if (stk.Peek() == State.obj)
                    {
                        reader.Read();
                    }
                    else if (stk.Peek() == State.performative_seek)
                    {
                        reader.Read();
                    }
                    else if (stk.Peek() == State.str)
                    {
                        current_segment.Append((char)reader.Read());
                    }
                    else
                    {
                        reader.Read();
                        stk.Push(State.segment_complete);
                    }
                    break;
                case ':':
                    if (stk.Peek() == State.str || stk.Peek() == State.regular_token || stk.Peek() == State.performative || stk.Peek() == State.keyword)
                    {
                        current_segment.Append((char)reader.Read());
                    }
                    else if (stk.Peek() == State.performative_seek)
                    {
                        stk.Pop();
                        stk.Push(State.keyword);
                        current_segment.Append((char)reader.Read());
                    }
                    else if (stk.Peek() == State.obj)
                    {
                        current_segment.Append((char)reader.Read());
                        stk.Push(State.keyword);
                    }
                    else
                    {
                        Console.WriteLine($"[KQMLMessageParser] Incorrect message formulation. Current char: {(char)c}; Remaining string: {reader.ReadToEnd()}");
                        throw new Exception($"[KQMLMessageParser] Incorrect message formulation. Current char: {(char)c}; Remaining string: {reader.ReadToEnd()}");
                    }
                    break;
                default:
                    // all other characters
                    if (stk.Peek() == State.obj)
                    {
                        stk.Push(State.regular_token);
                    }
                    else if (stk.Peek() == State.performative_seek)
                    {
                        stk.Pop();
                        stk.Push(State.performative);
                    }
                    if (current_segment == null)
                    {
                        Console.WriteLine($"[KQMLMessageParser] Incorrect message formulation. Current char: {(char)c}; Remaining string: {reader.ReadToEnd()}");
                        throw new Exception($"[KQMLMessageParser] Incorrect message formulation. Current char: {(char)c}; Remaining string: {reader.ReadToEnd()}");
                    }
                    current_segment.Append((char)reader.Read());
                    break;
            }
        }
    }

    public class KQMLMessage
    {
        public string performative { get; private set; }
        public string sender { get; set; }
        public string receiver;
        public string reply_with;
        public string reply_to;
        public object content { get; private set; }
        public List<object> unaffiliated_obj_and_strings;
        Dictionary<string, object> other = new Dictionary<string, object>();

        private string[] standard_keys = new string[] { ":sender", ":receiver", ":reply-with", ":in-reply-to", ":content", "performative", "unaffiliated-objects-or-strings" };

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

        public KQMLMessage(Dictionary<string, object> dict)
        {
            //this.performative = perf;
            object temp = null;
            if (dict.TryGetValue("performative", out temp))
            {
                this.performative = (string)temp;
            }
            if (dict.TryGetValue("unaffiliated-objects-or-strings", out temp))
            {
                this.unaffiliated_obj_and_strings = (List<object>)temp;
            }
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

        /**
         * Replaced this one with KQMLMessageParser.parse(string), which supports nested KQML expression.
         */
        [Obsolete("KQMLMessage.parseMessage(string) is deprecated. Use KQMLMessageParser.parse(string) instead", true)]
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
            if (this.unaffiliated_obj_and_strings != null)
            {
                foreach (object s in this.unaffiliated_obj_and_strings)
                {
                    sb.Append(s.ToString()).Append(' ');
                }
            }
            return sb.Append(")").ToString();
        }
    }


}
