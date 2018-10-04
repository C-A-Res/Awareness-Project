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

        public async Task<string> nextIntercampusShuttle()
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
                        var finalres2 = (Newtonsoft.Json.Linq.JValue)entry.GetValue("arrival_at");
                        var winner2 = (System.DateTime)finalres.Value;
                        return winner2;
                    }
                }
                else
                {
                    _log.Info($"[computeIntercampusShuttleTime] Cannot parse response: {restr}");
                    return DateTime.MaxValue;
                }
            }
            catch (Exception e)
            {
                _log.Info($"[computeIntercampusShuttleTime] exception thrown");
                return DateTime.MaxValue;
            }

        }
    }
}
