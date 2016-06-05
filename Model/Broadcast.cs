using Microsoft.Bot.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SlackathonMTL.Model
{
    public class Broadcast
    {
        public string SubjectName;
        public ChannelAccount Asker;
        public BroadcastStatus Status;
        public Queue<BroadcastAnswer> Answers;

        private static List<Broadcast> m_currentBroadcasts;

        static Broadcast()
        {
            m_currentBroadcasts = new List<Broadcast>();
        }

        public static void Add(string subjectName, ChannelAccount asker)
        {
            m_currentBroadcasts.Add(new Broadcast {
                SubjectName = subjectName,
                Asker = asker,
                Status = BroadcastStatus.WaitingForAnswer,
                Answers = new Queue<BroadcastAnswer>()
            });
        }

        public static void Remove(Broadcast broadcast)
        {
            if (!m_currentBroadcasts.Contains(broadcast))
                return;

            m_currentBroadcasts.Remove(broadcast);
        }

        public static List<Broadcast> GetAll()
        {
            return m_currentBroadcasts;
        }
    }
}