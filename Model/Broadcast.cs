﻿using Microsoft.Bot.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SlackathonMTL.Model
{
    public class Broadcast
    {
        public Subject Subject;
        public ChannelAccount Asker;
        public BroadcastStatus Status;

        private static List<Broadcast> m_currentBroadcasts;

        static Broadcast()
        {
            m_currentBroadcasts = new List<Broadcast>();
        }

        public static void Add(Subject subject, ChannelAccount asker)
        {
            if (m_currentBroadcasts.FirstOrDefault(p =>
                p.Asker.Id == asker.Id &&
                p.Subject.Id == subject.Id) != null)
                return;

            m_currentBroadcasts.Add(new Broadcast {
                Subject = subject,
                Asker = asker,
                Status = BroadcastStatus.WaitingForAnswer
            });
        }

        public static void Remove(Broadcast broadcast)
        {
            if (!m_currentBroadcasts.Contains(broadcast))
                return;

            m_currentBroadcasts.Remove(broadcast);
        }

        public List<Broadcast> GetAll()
        {
            return m_currentBroadcasts;
        }
    }
}