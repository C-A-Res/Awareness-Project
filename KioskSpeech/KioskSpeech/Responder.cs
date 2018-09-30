﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Speech.Synthesis;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using log4net;
using Microsoft.Psi;
using Microsoft.Psi.Components;
using NU.Kiosk.SharedObject;

namespace NU.Kiosk.Speech
{
    public class Responder
    {
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Pipeline pipeline;

        private int repeatCount = 0;

        public Responder(Pipeline pipeline)
        {
            this.pipeline = pipeline;

            // Dialog connections
            this.UserInput = pipeline.CreateReceiver<Utterance>(this, ReceiveUserInput, nameof(this.UserInput));
            this.TextResponse = pipeline.CreateEmitter<string>(this, nameof(this.TextResponse));

            // KQML connections
            this.KQMLResponse = pipeline.CreateReceiver<NU.Kiosk.SharedObject.Action>(this, ReceiveKQMLResponse, nameof(this.KQMLResponse));
            this.KQMLRequest = pipeline.CreateEmitter<string>(this, nameof(this.KQMLRequest));

            // UI connection
            this.ActionResponse = pipeline.CreateEmitter<NU.Kiosk.SharedObject.Action>(this, nameof(this.ActionResponse));
        }

        // Dialog
        public Receiver<Utterance> UserInput { get; private set; }
        public Emitter<string> TextResponse { get; private set; }

        // UI
        public Emitter<NU.Kiosk.SharedObject.Action> ActionResponse { get; private set; } 

        // KQML
        public Receiver<NU.Kiosk.SharedObject.Action> KQMLResponse { get; private set; }
        public Emitter<string> KQMLRequest { get; private set; }

        private void ReceiveUserInput(Utterance arg1, Envelope arg2)
        {
            _log.Debug($"[ReceiverUserInput] Utterance received: {arg1.Text}");
            if (!generateAutoResponse(arg1, arg2))
            {
                KQMLRequest.Post(arg1.Text, arg2.OriginatingTime);
            }
        }

        private void ReceiveKQMLResponse(NU.Kiosk.SharedObject.Action arg1, Envelope arg2)
        {
            // just forward it
            _log.Debug($"[ReceiveKQMLResponse] Forwarding KQML Response with action {arg1}");
            ActionResponse.Post(arg1, DateTime.Now);
            if (arg1.Name == "psikiSayText")
            {
                //TextResponse.Post((string)arg1.Args[0], DateTime.Now);
            }
        }

        private bool generateAutoResponse(Utterance arg1, Envelope arg2)
        {
            var text = arg1.Text;
            var confidence = arg1.Confidence;
            _log.Debug($"[generateAutoResponse] Received utterance ({text}) has confidence {confidence}");
            if (confidence < 0.3)
            {
                if (repeatCount <= 1)
                {
                    sendResponse("Could you please repeat that?");
                }
                else if (repeatCount <= 3)
                {
                    sendResponse("Please try to rephrase.");
                }
                else
                {
                    sendResponse("Please try again.");
                }
                repeatCount++;
                return true;
            }
            else
            {
                repeatCount = 0;
                var lower = text.ToLower();
                switch (lower)
                {
                    case "hi":
                    case "hello":
                    case "greetings":
                    case "good morning":
                    case "sup":
                        sendResponse("Hello");
                        return true;
                    case "what can you do?":
                    case "what do you do?":
                    case "how can you help?":
                    case "help":
                    case "help me":
                        generateHelpResponse(arg2);
                        return true;
                    case "what time is it?":
                    case "what's the time?":
                        var time = DateTime.Now.ToString("h:mm tt");
                        sendResponse($"It is {time}");
                        return true;
                    case "":
                    case "okay":
                    case "hm":
                    case "um":
                    case "ah":
                    case "cool":
                    case "huh?":
                    case "wow!":
                    case "huck you":
                    case "bye":
                    case "bye bye":
                        _log.Debug($"[generateAutoResponse] Discarding message: {text}");
                        return true;
                    default:
                        if (lower.Contains("bathroom") || lower.Contains("restroom") || lower.Contains("men's room") || lower.Contains("women's room"))
                        {
                            sendResponse("The bathroom is in the southeast corner of the floor.");
                            ActionResponse.Post(new SharedObject.Action("psikiShowMap", "Bathroom", "bathroom"), DateTime.Now);
                            return true;
                        } else if (lower.Contains("office hours"))
                        {
                            sendResponse("Office hours are displayed below.");
                            ActionResponse.Post(new SharedObject.Action("psikiShowCalendar", "today"), DateTime.Now);
                            return true;
                        }
                        break;
                }
            
            }
            return false;
        }

        private void generateHelpResponse(Envelope arg2)
        {
            sendResponse("I can answer questions about where someone's office is and how to contact a professor.");
        }

        private void sendResponse(string response)
        {
            _log.Debug($"[sendResponse] Responding with {response}");
            ActionResponse.Post(new SharedObject.Action("psikiSayText", response), DateTime.Now);
        }
        
    }
}