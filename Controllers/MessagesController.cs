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
                Entity entity1;
                Entity entity2;
                switch (intentType)
                {
                    case IntentType.None:
                        return message.CreateReplyMessage(Reply.GetReply(ReplyType.None).Text, "en");
                        
                    case IntentType.FindAnExpert:
                        entity1 = result.entities[0];
                        if (entity1 != null && entity1.type == "Subject")
                        {
                            return FindExpert(entity1.GetEntityName(), message);
                        }
                        break;
                    case IntentType.FindExpertise:
                        entity1 = result.entities[0];
                        if (entity1 != null && entity1.type == "Person")
                        {
                            return FindExpertise(entity1.GetEntityName(), message);
                        }
                        break;
                    case IntentType.FindExpertiseForSubject:
                        entity1 = result.entities[0];
                        entity2 = result.entities[1];
                        if (entity1 == null || entity2 == null) break;

                        if (entity1.type == "Person" && entity2.type == "Subject")
                        {
                            return FindExpertiseForSubject(entity1.GetEntityName(), entity2.GetEntityName(), message);
                        }
                        if (entity1.type == "Subject" && entity2.type == "Person")
                        {
                            return FindExpertiseForSubject(entity2.GetEntityName(), entity1.GetEntityName(), message);
                        }
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
                response.Append(Reply.GetReply(ReplyType.NoExpertsFound).Text);
            }
            else
            {
                for (int i = 0; i < potentialExperts.Count && i < 3; ++i)
                {
                    response.Append(string.Format($"{potentialExperts[i].Key.Username}\n"));
                }
            }
            return message.CreateReplyMessage(response.ToString(), "en");
        }

        private Message FindExpertise(string personName, Message message)
        {
            personName = personName.ToLower();
            Person person = Person.GetAll().FirstOrDefault(p => p.Username.ToLower() == personName);
            if (person == null)
            {
                return message.CreateReplyMessage("unkown person", "en");
            }

            List<Subject> subjects = Subject.GetAll();

            List<KeyValuePair<Subject, int>> potentialExpertise = new List<KeyValuePair<Subject, int>>();

            foreach (var subject in subjects)
            {
                int points = Matrix.GetPoints(person, subject);
                if (points > 0)
                {
                    potentialExpertise.Add(new KeyValuePair<Subject, int>(subject, points));
                }
            }
            potentialExpertise.Sort((p1, p2) => p2.Value - p1.Value);

            StringBuilder response = new StringBuilder();
            if (potentialExpertise.Count == 0)
            {
                response.Append(Reply.GetReply(ReplyType.NoExpertsFound).Text);
            }
            else
            {
                for (int i = 0; i < potentialExpertise.Count && i < 3; ++i)
                {
                    response.Append(string.Format($"{potentialExpertise[i].Key.Name}\n"));
                }
            }
            return message.CreateReplyMessage(response.ToString(), "en");
        }

        private Message FindExpertiseForSubject(string personName, string subjectName, Message message)
        {
            personName = personName.ToLower();
            Person person = Person.GetAll().FirstOrDefault(p => p.Username.ToLower() == personName);
            if (person == null)
            {
                return message.CreateReplyMessage("unkown person", "en");
            }
            Subject subject = Subject.GetAll().FirstOrDefault(p => p.Name == subjectName);
            if (subject == null)
            {
                return message.CreateReplyMessage("unkown subject", "en");
            }

            float points = Matrix.GetPoints(person, subject);

            StringBuilder response = new StringBuilder();
            response.Append(string.Format($"{person.Username} has {points} points for {subject.Name}"));
            return message.CreateReplyMessage(response.ToString(), "en");
        }
    }
}