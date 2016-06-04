using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;

namespace SlackathonMTL.Model
{
    public class Reply
    {
        private static Dictionary<ReplyType, List<Reply>> m_replies;
        private static string m_path;
        private static Random m_generator;
        public string Text { get; private set; }

        static Reply()
        {
            string appPath = HttpRuntime.AppDomainAppPath;
            m_path = Path.Combine(appPath, "Data", "replies.json");
            m_replies = new Dictionary<ReplyType, List<Reply>>();
            m_generator = new Random();
        }

        public static void Load()
        {
            m_replies.Clear();
            string json = System.IO.File.ReadAllText(m_path);

            JObject jObject = JObject.Parse(json);
            Dictionary<String, List<String>> dict = JsonConvert.DeserializeObject<Dictionary<String, List<String>>>(jObject.ToString());

            foreach (String key in dict.Keys)
            {
                ReplyType rt = (ReplyType)Enum.Parse(typeof(ReplyType), key);
                List<Reply> replies = new List<Reply>();
                foreach (String reply in dict[key])
                {
                    replies.Add(new Reply{ Text = reply});
                }

                m_replies.Add(rt, replies);
            }
        }

        public static Reply GetReply(ReplyType rt)
        {
            List<Reply> possibleReplies = m_replies[rt];
            int rand = m_generator.Next(0, possibleReplies.Count); 
            return possibleReplies[rand];
        }
    }
}