using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.IO;
using System.Net.Http.Headers;

using Newtonsoft.Json;
using log4net;
using System.Reflection;

namespace NU.Kiosk.Speech
{
    public class InternetQueryHandler
    {
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        HttpClient client = new HttpClient();

        public InternetQueryHandler()
        {
            client = new HttpClient();
        }

        private async Task<string> nextIntercampusShuttle()
        {
            using (var requestMessage = new HttpRequestMessage(
                HttpMethod.Get, "https://transloc-api-1-2.p.mashape.com/arrival-estimates.json?agencies=665&callback=call&routes=8005040&stops=8204728"))
            {

                requestMessage.Headers.Add("X-Mashape-Key", "qWtrTWouCJmshGtAbD488TpzjpAep1h6Bb2jsnNz76giHO5JbL");
                requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                HttpResponseMessage response = await client.SendAsync(requestMessage);
                using (var reader = new StreamReader(await response.Content.ReadAsStreamAsync()))
                {
                    // Write the output.
                    return await reader.ReadToEndAsync();
                }
            }
        }

        public DateTime getNextIntercampusShuttleTime()
        {
            try
            {
                var restr = nextIntercampusShuttle().GetAwaiter().GetResult();
                var reader = JsonConvert.DeserializeObject(restr);

                if (reader is Newtonsoft.Json.Linq.JObject)
                {
                    var res = (Newtonsoft.Json.Linq.JObject)reader;
                    var val = (Newtonsoft.Json.Linq.JArray)res.GetValue("data");

                    if (val.Count == 0)
                    {
                        return DateTime.MinValue;
                    }

                    var arr = (Newtonsoft.Json.Linq.JObject)val.First();
                    var arrivals = (Newtonsoft.Json.Linq.JArray)arr.GetValue("arrivals");
                    var entry = (Newtonsoft.Json.Linq.JObject)arrivals.First();
                    var finalres = (Newtonsoft.Json.Linq.JValue)entry.GetValue("arrival_at");
                    var winner = (System.DateTime)finalres.Value;

                    if ((winner - DateTime.Now).Minutes >= 5)
                    {
                        return winner;
                    }
                    else
                    {
                        if (arrivals.Count == 1) return winner;
                        var entry2 = (Newtonsoft.Json.Linq.JObject)arrivals.ElementAt(1);
                        var finalres2 = (Newtonsoft.Json.Linq.JValue)entry2.GetValue("arrival_at");
                        var winner2 = (System.DateTime)finalres2.Value;
                        return winner2;
                    }
                }
                else
                {
                    _log.Info($"[getNextIntercampusShuttleTime] Cannot parse response: {restr}");
                    return DateTime.MaxValue;
                }
            }
            catch (Exception e)
            {
                _log.Info($"[getNextIntercampusShuttleTime] exception thrown");
                return DateTime.MaxValue;
            }

        }

        private async Task<string> nextCTA201Bus()
        {
            using (var requestMessage = new HttpRequestMessage(
                HttpMethod.Get, "http://www.ctabustracker.com/bustime/api/v2/getpredictions?key=35vUE2WmAVFQXsb33NDXWyXz7&rt=201&stpid=18357&format=json"))
            {
                HttpResponseMessage response = await client.SendAsync(requestMessage);
                using (var reader = new StreamReader(await response.Content.ReadAsStreamAsync()))
                {
                    // Write the output.
                    return await reader.ReadToEndAsync();
                }
            }
        }

        public DateTime getNextCTA201BusTime()
        {
            try
            {
                var restr = nextCTA201Bus().GetAwaiter().GetResult();
                var reader = JsonConvert.DeserializeObject(restr);

                if (reader is Newtonsoft.Json.Linq.JObject)
                {
                    var res = (Newtonsoft.Json.Linq.JObject)reader;
                    var bustime_res = (Newtonsoft.Json.Linq.JObject)res.GetValue("bustime-response");
                    var pred = (Newtonsoft.Json.Linq.JArray)bustime_res.GetValue("prd");

                    if (pred.Count == 0)
                    {
                        return DateTime.MinValue;
                    }

                    var one_shuttle = (Newtonsoft.Json.Linq.JObject)pred.First();
                    var arrival = (Newtonsoft.Json.Linq.JValue)one_shuttle.GetValue("prdtm");
                    var unformated_time = (System.String)arrival.Value;
                    var winner = formatCTATimeString(unformated_time);

                    Console.WriteLine($"type: {unformated_time.GetType()}; val: {unformated_time}");

                    if ((winner - DateTime.Now).Minutes >= 5)
                    {
                        return winner;
                    }
                    else
                    {
                        if (pred.Count == 1) return winner;
                        var one_shuttle2 = (Newtonsoft.Json.Linq.JObject)pred.ElementAt(1);
                        var arrival2 = (Newtonsoft.Json.Linq.JValue)one_shuttle2.GetValue("prdtm");
                        var unformated_time2 = (System.String)arrival2.Value;
                        var winner2 = formatCTATimeString(unformated_time2);
                        return winner2;
                    }
                }
                else
                {
                    _log.Info($"[getNextCTA201BusTime] Cannot parse response: {restr}");
                    return DateTime.MaxValue;
                }
            }
            catch (Exception e)
            {
                _log.Info($"[getNextCTA201BusTime] exception thrown");
                return DateTime.MaxValue;
            }

        }

        private static DateTime formatCTATimeString(System.String str)
        {
            int hour24 = int.Parse(str.Substring(9, 2));
            int hour12 = hour24 > 12 ? hour24 - 12 : hour24;
            string ampm = hour24 >= 12 ? "pm" : "am";
            var minute = str.Substring(12, 2);
            var timeString = string.Format($"{str.Substring(0, 4)}-{str.Substring(4, 2)}-{str.Substring(6, 2)} {hour12}:{minute}{ampm}");
            return DateTime.Parse(timeString);
        }
    }
}
