using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Connector.Utilities;
using Newtonsoft.Json;
using SlackathonMTL.Model;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SlackathonMTL
{
    [BotAuthentication]
    public class MessagesController : ApiController
    {
        /// <summary>
        /// POST: api/Messages
        /// Receive a message from a user and reply to it
        /// </summary>
        public async Task<Message> Post([FromBody]Message message)
        {
            if (message.Type == "Message")
            {
                CheckForNewUser(message);

                CheckForBroadcastAnswer(message);
                
                InterpretorResult result = await MessageInterpretor.InterpretMessage(message.Text);
                return FindExpert(message.Text, message);
            }
            else
            {
                return HandleSystemMessage(message);
            }
        }

        private Message HandleSystemMessage(Message message)
        {
            if (message.Type == "Ping")
            {
                Message reply = message.CreateReplyMessage();
                reply.Type = "Ping";
                return reply;
            }
            else if (message.Type == "DeleteUserData")
            {
                // Implement user deletion here
                // If we handle user deletion, return a real message
            }
            else if (message.Type == "BotAddedToConversation")
            {
            }
            else if (message.Type == "BotRemovedFromConversation")
            {
            }
            else if (message.Type == "UserAddedToConversation")
            {
            }
            else if (message.Type == "UserRemovedFromConversation")
            {
            }
            else if (message.Type == "EndOfConversation")
            {
            }

            return null;
        }

        private void CheckForNewUser(Message message)
        {
            Person person = Person.GetAll().FirstOrDefault(p => p.Id == message.From.Id);
            if (person == null)
            {
                person = new Person
                {
                    Id = message.From.Id,
                    Username = message.From.Name
                };
                Person.Add(person);
                Person.Save();
            }
            else if (message.From.Name != person.Username)
            {
                person.Username = message.From.Name;
                Person.Update(person);
                Person.Save();
            }
        }

        private void CheckForBroadcastAnswer(Message message)
        {
            if (!message.Text.Contains("@"))
                return;

            string sub = message.Text.Substring(message.Text.IndexOf("@"));
            string username = string.Empty;
            if (sub.Contains(" "))
            {
                username = sub.Substring(1, sub.IndexOf(" ")-1);
            }
            else
            {
                username = sub.Substring(1);
            }

            Person asker = Person.GetAll().FirstOrDefault(p => p.Username == username);
            if (asker == null || asker.Id == message.From.Id)
                return;

            List<Broadcast> currentBroadcasts = Broadcast.GetAll();
            
            foreach (Broadcast broadcast in currentBroadcasts)
            {
                if (broadcast.Status != BroadcastStatus.WaitingForAnswer ||
                    broadcast.Asker.Id != asker.Id)
                    continue;

                broadcast.Status = BroadcastStatus.WaitingForAnswer;

                var connector = new ConnectorClient();
                string answerer = "@" + message.From.Name;
                Message answerAck = message.CreateReplyMessage(answerer + " has responded to your question. Is it a good answer?");
                message.To = broadcast.Asker;
                connector.Messages.SendMessage(message);
                break;
            }
        }

        private Message BroadcastMessage(string subjectName, Message message)
        {
            Broadcast.Add(subjectName, message.From);
            Message ack = message.CreateReplyMessage("broadcast done");
            return ack;
        }

        private Message FindExpert(string subjectName, Message message)
        {
            subjectName = subjectName.ToLower();
            Subject subject = Subject.GetAll().FirstOrDefault(p => p.Name == subjectName);
            if (subject == null)
            {
                return BroadcastMessage(subjectName, message);
            }

            List<Person> persons = Person.GetAll();

            List<KeyValuePair<Person, int>> potentialExperts = new List<KeyValuePair<Person, int>>();

            foreach (var person in persons)
            {
                int points = Matrix.GetPoints(person, subject);
                if (points > 0)
                {
                    potentialExperts.Add(new KeyValuePair<Person, int>(person, points));
                }
            }
            potentialExperts.Sort((p1, p2) => p2.Value - p1.Value);

            StringBuilder response = new StringBuilder();
            if (potentialExperts.Count == 0)
            {
                // No expert
            }
            else
            {
                for (int i = 0; i < potentialExperts.Count && i < 3; ++i)
                {
                    response.Append(string.Format("@{0} ", potentialExperts[i].Key.Username));
                }
            }
            return message.CreateReplyMessage(response.ToString(), "en");
        }
    }
}