﻿using System;
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
        static Dictionary<string, ChannelAccount> accountsForId = new Dictionary<string, ChannelAccount>();
        /// <summary>
        /// POST: api/Messages
        /// Receive a message from a user and reply to it
        /// </summary>
        public async Task<Message> Post([FromBody]Message message)
        {
            if (message.Type == "Message")
            {
                CheckForNewUser(message);

                if (CheckForBroadcastAnswer(message)) return null;

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
                    case IntentType.BroadcastAnswerAccept:
                        entity1 = result.entities[0];
                        if (entity1 != null && entity1.type == "Person")
                        {
                            return BroadcastAccept(entity1.GetEntityName(), message);
                        }
                        break;

                    case IntentType.BroadcastAnswerDenied:
                        entity1 = result.entities[0];
                        if (entity1 != null && entity1.type == "Person")
                        {
                            return BroadcastDenied(message);
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
                accountsForId[message.From.Id] = message.From;
            }
            else if (message.Type == "UserRemovedFromConversation")
            {
                if (accountsForId.ContainsKey(message.From.Id))
                {
                    accountsForId.Remove(message.From.Id);
                }
            }
            else if (message.Type == "EndOfConversation")
            {
            }

            return null;
        }

        private void CheckForNewUser(Message message)
        {
            if (!message.From.IsBot.Value)
            {
                accountsForId[message.From.Id] = message.From;
            }
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


        private Message BroadcastAccept(string userName, Message message)
        {
            Person person = Person.GetAll().FirstOrDefault(p => p.Username == userName);
            if (person == null)
            {
                return message.CreateReplyMessage("unkown person", "en");
            }

            Broadcast broadcast = Broadcast.GetAll().FirstOrDefault(b => b.Asker.Id == message.From.Id && b.Status == BroadcastStatus.WaitingForApproval);
            if (broadcast == null)
            {
                return message.CreateReplyMessage("you have no open questions", "en");
            }

            Subject subject = Subject.GetAll().FirstOrDefault(s => s.Name == broadcast.SubjectName);
            if (subject == null)
            {
                subject = new Subject { Id = Guid.NewGuid().ToString(), Name = broadcast.SubjectName };
                Subject.Add(subject);
                Subject.Save();
            }

            Matrix.SetPoints(person, subject, Matrix.GetPoints(person, subject) + 1);
            Matrix.Save();

            Broadcast.Remove(broadcast);

            return message.CreateReplyMessage("duly noted");
        }

        private Message BroadcastDenied(Message message)
        {
            Broadcast broadcast = Broadcast.GetAll().FirstOrDefault(b => b.Asker.Id == message.From.Id && b.Status == BroadcastStatus.WaitingForApproval);
            if (broadcast == null)
            {
                return message.CreateReplyMessage("you have no open questions", "en");
            }
            broadcast.Status = BroadcastStatus.WaitingForAnswer;

            return message.CreateReplyMessage("the search goes on");
        }

        private bool CheckForBroadcastAnswer(Message message)
        {
            if (!message.Text.Contains("@"))
                return false;

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
                return false;

            List<Broadcast> currentBroadcasts = Broadcast.GetAll();
            
            foreach (Broadcast broadcast in currentBroadcasts)
            {
                if (broadcast.Status != BroadcastStatus.WaitingForAnswer ||
                    broadcast.Asker.Id != asker.Id)
                    continue;

                broadcast.Status = BroadcastStatus.WaitingForApproval;

                var connector = new ConnectorClient();
                string answerer = "@" + message.From.Name;

                Message answerAck = message.CreateReplyMessage(answerer + " has responded to your question. Is it a good answer?" + message.Text);

                Message replyMessage = new Message();
                replyMessage.From = answerAck.From;
                replyMessage.Text = answerAck.Text;
                replyMessage.Language = "en";
                replyMessage.To = broadcast.Asker;
                connector.Messages.SendMessage(replyMessage);

                return true;
            }
            return false;
        }

        private Message BroadcastMessage(string subjectName, string broadcastText, Message message)
        {
            Broadcast.Add(subjectName, message.From);

            Message ack = message.CreateReplyMessage($"broadcast done");

            var connector = new ConnectorClient();

            foreach (string user in accountsForId.Keys)
            {
                if (accountsForId[user].Id == message.From.Id) continue;

                Message broadcastMessage = new Message();
                broadcastMessage.From = ack.From;
                broadcastMessage.Text = broadcastText;
                broadcastMessage.Language = "en";
                broadcastMessage.To = accountsForId[user];
                connector.Messages.SendMessage(broadcastMessage);
            }


            return ack;
        }

        private Message FindExpert(string subjectName, Message message)
        {
            subjectName = subjectName.ToLower();
            Subject subject = Subject.GetAll().FirstOrDefault(p => p.Name == subjectName);
            if (subject == null)
            {
                return BroadcastMessage(subjectName, $"@{Person.GetAll().FirstOrDefault(p => p.Id == message.From.Id).Username} needs help with {subjectName}", message);
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