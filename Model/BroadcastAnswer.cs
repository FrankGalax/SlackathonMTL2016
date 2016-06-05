using Microsoft.Bot.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SlackathonMTL.Model
{
    public class BroadcastAnswer
    {
        public ChannelAccount Answerer { get; set; }
        public string MessageText { get; set; }
    }
}