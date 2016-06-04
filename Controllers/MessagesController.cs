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

                InterpretorResult result = await MessageInterpretor.InterpretMessage(message.Text);
                float prob = 0f;
                IntentType intentType = IntentType.None;

                foreach (Intent intent in result.intents)
                {
                    if (intent.score > prob)
                    {
                        intentType = intent.GetIntentType();
                        prob = intent.score;
                    }
                }

                switch (intentType)
                {
                    case IntentType.None:
                        return message.CreateReplyMessage(Reply.GetReply(ReplyType.None).Text, "en");
                        break;
                    case IntentType.FindAnExpert:
                        return FindExpert(message.Text, message);
                        break;
                    case IntentType.FindExpertise:
                    case IntentType.FindExpertiseForSubject:
                        return message.CreateReplyMessage(intentType.ToString(), "en");
                        break;
                }
            }
            return HandleSystemMessage(message);
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
            
        }

        private Message BroadcastMessage(Subject subject, Message message)
        {
            Broadcast.Add(subject, message.From);
            Message ack = message.CreateReplyMessage("broadcast done");
            return ack;
        }

        private Message FindExpert(string subjectName, Message message)
        {
            subjectName = subjectName.ToLower();
            Subject subject = Subject.GetAll().FirstOrDefault(p => p.Name == subjectName);
            if (subject == null)
            {
                subject = new Subject
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = subjectName
                };
                Subject.Add(subject);
                Subject.Save();
                return BroadcastMessage(subject, message);
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