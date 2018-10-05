using System;
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

        private InternetQueryHandler internetHandler;

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

            // Internet Query Handler
            internetHandler = new NU.Kiosk.Speech.InternetQueryHandler();
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
            
            if (confidence < 0.3)
            {
                _log.Debug($"[generateAutoResponse] Received unintelligible utterance with confidence {confidence}");
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
                _log.Debug($"[generateAutoResponse] Received utterance ({text}) has confidence {confidence}");
                repeatCount = 0;
                var lower = text.ToLower();
                switch (lower)
                {
                    case "(Unintelligible)":
                        _log.Debug($"[generateAutoResponse] Unintelligible voice input");
                        sendResponse("Sorry! Could you repeat that?");
                        break;
                    case "hi":
                    case "hello":
                    case "greetings":
                    case "good morning":
                    case "sup":
                    case "'sup":
                    case "what's up?":
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
                    case "what time is it now?":
                    case "what's the time?":
                        //var time = DateTime.Now.ToString("h:mm tt");
                        sendResponse($"It is {DateTime.Now.ToShortTimeString()}");
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
                    case "what is the meaning of life?":
                    case "what is the meaning of life":
                        sendResponse("101010");
                        return true;
                    case "aravindan chris kris ian":
                        sendResponse("Wrong care court near");
                        return true;
                    case "show me the map.":
                    case "show me the map":
                        sendResponse("Sorry! I can't do that yet. Please click the 'Show Map' button below.");
                        //ActionResponse.Post(new SharedObject.Action("psikiShowMap", "", ""), DateTime.Now);
                        return true;
                    case "who are you?":
                        sendResponse("I am PsiKi, the intern receptionist of Computer Science.");
                        return true;
                    //case "it's raining outside.":
                    //    sendResponse("I've; see-een, thingss, you people wouldn't belee-eeve. Attack ships on fye-err off, shoulder of-oh ryen. I watched C beams; glitter in the dark near the Tannhäuser Gate. All those, moments; will be lost; in time; like; tears; in rain. Time to watch Blade Runner again.");
                    //    return true;
                    default:
                        if (lower.Contains("bathroom") || lower.Contains("restroom") || lower.Contains("men's room") || lower.Contains("lady's room") || lower.Contains("washroom"))
                        {
                            sendResponse("The bathrooms are in the southeast corner of the floor.");
                            ActionResponse.Post(new SharedObject.Action("psikiShowMap", "Bathroom", "bathroom"), DateTime.Now);
                            return true;
                        } else if (lower.Contains("seminar room"))
                        {
                            sendResponse("The Seminar Room is toward the east side of the floor.");
                            ActionResponse.Post(new SharedObject.Action("psikiShowMap", "Seminar", "seminar"), DateTime.Now);
                            return true;                            
                        } else if (lower.Contains("office hour"))
                        {
                            sendResponse("Office hours are displayed below.");
                            ActionResponse.Post(new SharedObject.Action("psikiShowCalendar", "today"), DateTime.Now);
                            return true;
                        } else if (lower.Contains("printer"))
                        {
                            sendResponse("The printers' location is displayed below.");
                            ActionResponse.Post(new SharedObject.Action("psikiShowMap", "EastPrinters", "printers"), DateTime.Now);
                            return true;
                        } else if (lower.Contains("kitchen"))
                        {
                            sendResponse("The kitchen is just around the corner.");
                            ActionResponse.Post(new SharedObject.Action("psikiShowMap", "Kitchen", "kitchen"), DateTime.Now);
                            return true;
                        } else if (lower.Contains("intercampus") && !lower.Contains("northbound"))
                        {
                            var nextArrival = internetHandler.getNextIntercampusShuttleTime();
                            respondWithShuttleTime(nextArrival);
                            return true;
                        } else if (lower.Contains("201") && !lower.Contains("northbound") && !lower.Contains("westbound"))
                        {
                            var nextArrival = internetHandler.getNextCTA201BusTime();
                            respondWithShuttleTime(nextArrival);
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
        
        private void respondWithShuttleTime(DateTime nextArrival)
        {
            var mins = (nextArrival - DateTime.Now).Minutes;
            if (nextArrival == DateTime.MaxValue)
            {
                sendResponse("Sorry, I'm having trouble accessing the real-time data.");
            }
            else if (nextArrival == DateTime.MinValue)
            {
                sendResponse("There isn't one coming any time soon.");
            }
            else if (mins < 1)
            {
                sendResponse($"It is arriving in a minute.");
            }
            else if (mins < 3)
            {
                sendResponse($"It is arriving in {mins} minutes. However, it usually takes 5 minutes to walk over.");
            }
            else if (mins < 5)
            {
                sendResponse($"Arriving in {(nextArrival - DateTime.Now).Minutes} minutes, at {nextArrival.ToShortTimeString()}. You should hurry.");
            }
            else
            {
                sendResponse($"Arriving in {(nextArrival - DateTime.Now).Minutes} minutes, at {nextArrival.ToShortTimeString()}.");
            }
        }
    }
}