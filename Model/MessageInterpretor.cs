using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

namespace SlackathonMTL.Model
{
    public class MessageInterpretor
    {
        private static String InterpertorURL = "https://api.projectoxford.ai/luis/v1/application?id=bad61257-ff71-41fd-a29a-6d23257632d1&subscription-key=0a27f907424944ea81aa8395f88815b8&q=";

        public static async Task<InterpretorResult> InterpretMessage(String message)
        {
            using (var client = new HttpClient())
            {
                String uri = InterpertorURL + HttpUtility.UrlEncode(message);
                HttpResponseMessage response = await client.GetAsync(uri);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var Data = JsonConvert.DeserializeObject<InterpretorResult>(jsonResponse);
                    return Data;
                }
            }
            return null;
        }

    }
}