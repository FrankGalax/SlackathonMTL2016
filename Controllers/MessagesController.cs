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
        static Dictionary<string, ChannelAccount> accountsForId = new Dictionary<string, ChannelAccount>();
        static Object megaLock = new object();
        /// <summary>
        /// POST: api/Messages
        /// Receive a message from a user and reply to it
        /// </summary>
        public async Task<Message> Post([FromBody]Message message)
        {
            try
            {
                if (message.Type == "Message")
                {
                    lock(megaLock)
                    {
                        CheckForNewUser(message);

                        if (CheckForBroadcastAnswer(message) || CheckForBroadcastPendingQuestion(message)) return null;
                    }

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
                    lock (megaLock)
                    {
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
                            case IntentType.BroadcastAnswerAccepted:
                                return BroadcastAccept(message);

                            case IntentType.BroadcastAnswerDenied:
                                return BroadcastDenied(message);
                            case IntentType.Salutations:
                                return message.CreateReplyMessage(Reply.GetReply(ReplyType.Salutations).Text);
                            case IntentType.Thanks:
                                return message.CreateReplyMessage(Reply.GetReply(ReplyType.Thanks).Text);
                            case IntentType.Goodbyes:
                                return message.CreateReplyMessage(Reply.GetReply(ReplyType.Goodbyes).Text);
                        }
                    }
                }
                return HandleSystemMessage(message);
            }
            catch (Exception ex)
            {
                return message.CreateReplyMessage(ex.Message + " " + ex.StackTrace, "en");
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
                return message.CreateReplyMessage(Reply.GetReply(ReplyType.Greetings).Text);
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
                    Username = "@" + message.From.Name
                };
                Person.Add(person);
                Person.Save();
            }
            else if (message.From.Name != person.Username)
            {
                person.Username = "@" + message.From.Name;
                Person.Update(person);
                Person.Save();
            }
        }

        private Message BroadcastAccept(Message message)
        {
            Broadcast broadcast = Broadcast.GetAll().FirstOrDefault(b => 
                b.Asker.Id == message.From.Id && 
                b.Status == BroadcastStatus.WaitingForApproval &&
                b.Answers.Count > 0
            );
            if (broadcast == null)
            {
                return message.CreateReplyMessage(Reply.GetReply(ReplyType.None).Text, "en");
            }

            Subject subject = Subject.GetAll().FirstOrDefault(s => s.Name == broadcast.SubjectName);
            if (subject == null)
            {
                subject = new Subject { Id = Guid.NewGuid().ToString(), Name = broadcast.SubjectName };
                Subject.Add(subject);
                Subject.Save();
            }

            Person person = Person.GetAll().FirstOrDefault(p => p.Id == broadcast.Answers.First().Answerer.Id);
            if (person == null)
            {
                return message.CreateReplyMessage("Something went horibly wrong, put the laptop down and run away!", "en");
            }

            Matrix.SetPoints(person, subject, Matrix.GetPoints(person, subject) + 1);
            Matrix.Save();

            Broadcast.Remove(broadcast);

            ChannelAccount answererAccount = accountsForId.ContainsKey(person.Id) ? accountsForId[person.Id] : null;
            if (answererAccount != null)
            {
                var connector = new ConnectorClient();
                var ackMessage = new Message();
                ackMessage.From = message.To;
                ackMessage.To = answererAccount;
                ackMessage.Text = string.Format(Reply.GetReply(ReplyType.AnswererFeedbackPositive).Text, "@" + broadcast.Asker.Name);
                ackMessage.Language = "en";
                connector.Messages.SendMessage(ackMessage);
            }

            return message.CreateReplyMessage(Reply.GetReply(ReplyType.AcceptedAnswer).Text, "en");
        }

        private Message BroadcastDenied(Message message)
        {
            Broadcast broadcast = Broadcast.GetAll().FirstOrDefault(b => b.Asker.Id == message.From.Id && b.Status == BroadcastStatus.WaitingForApproval);
            if (broadcast == null)
            {
                return message.CreateReplyMessage("You have no open questions.", "en");
            }

            ChannelAccount answererAccount = broadcast.Answers.First().Answerer;
            var connector = new ConnectorClient();
            if (answererAccount != null)
            {
                var ackMessage = new Message();
                ackMessage.From = message.To;
                ackMessage.To = answererAccount;
                ackMessage.Text = string.Format(Reply.GetReply(ReplyType.AnswererFeedbackNegative).Text, "@" + broadcast.Asker.Name);
                ackMessage.Language = "en";
                connector.Messages.SendMessage(ackMessage);
            }

            broadcast.Answers.Dequeue();
            if (broadcast.Answers.Count == 0)
            {
                broadcast.Status = BroadcastStatus.WaitingForAnswer;
                return message.CreateReplyMessage(Reply.GetReply(ReplyType.RefusedAnswerReply).Text, "en");
            }
            BroadcastAnswer nextAnswer = broadcast.Answers.First();

            Message answerAck = message.CreateReplyMessage("@" + nextAnswer.Answerer.Name + ": ");

            Message replyMessage = new Message();

            replyMessage.From = answerAck.From;
            replyMessage.Text = answerAck.Text + nextAnswer.MessageText + Reply.GetReply(ReplyType.IsGoodAnswerQuestion).Text;
            replyMessage.Language = "en";
            replyMessage.To = broadcast.Asker;
            if (replyMessage.From.Id == null)
            {
                replyMessage.From = message.To;
            }
            return replyMessage;
        }

        private bool CheckForBroadcastAnswer(Message message)
        {
            if (!message.Text.Contains("@"))
                return false;

            string sub = message.Text.Substring(message.Text.IndexOf("@"));
            string username = string.Empty;
            if (sub.Contains(" "))
            {
                username = sub.Substring(0, sub.IndexOf(" "));
            }
            else
            {
                username = sub;
            }

            Person asker = Person.GetAll().FirstOrDefault(p => p.Username == username);
            if (asker == null || asker.Id == message.From.Id)
                return false;

            List<Broadcast> currentBroadcasts = Broadcast.GetAll();
            
            foreach (Broadcast broadcast in currentBroadcasts)
            {
                if (broadcast.Asker.Id != asker.Id)
                    continue;

                string[] split = message.Text.Split(' ');

                Regex rgx = new Regex(split.FirstOrDefault(s => s.StartsWith("@")) + " ");
                string answerString = rgx.Replace(message.Text, "", 1);

                if (broadcast.Status == BroadcastStatus.WaitingForApproval && broadcast.Answers.Count > 0)
                {
                    broadcast.Answers.Enqueue(new BroadcastAnswer { Answerer = message.From, MessageText = answerString });
                    return true;
                }

                broadcast.Status = BroadcastStatus.WaitingForApproval;
                
                broadcast.Answers.Enqueue(new BroadcastAnswer { Answerer = message.From, MessageText = answerString });
                var connector = new ConnectorClient();
                string answerer = "@" + message.From.Name;

                Message answerAck = message.CreateReplyMessage(answerer + ": ");

                Message replyMessage = new Message();
                replyMessage.From = answerAck.From;
                replyMessage.Language = "en";
                replyMessage.To = broadcast.Asker;
                replyMessage.Text = answerAck.Text + answerString + " - Is it a good answer?";

                connector.Messages.SendMessage(replyMessage);

                return true;
            }
            return false;
        }

        private bool CheckForBroadcastPendingQuestion(Message message)
        {
            Broadcast currentBroadcast = Broadcast.GetAll().FirstOrDefault(b => b.Status == BroadcastStatus.WaitingForQuestion && b.Asker.Id == message.From.Id);

            if (currentBroadcast == null) return false;

            Message ack = message.CreateReplyMessage(Reply.GetReply(ReplyType.BroadcastSentMessage).Text, "en");

            var connector = new ConnectorClient();

            string messageString = $"@{currentBroadcast.Asker.Name} has a question about {currentBroadcast.SubjectName} : \"{message.Text}\" You can answer him here using @{currentBroadcast.Asker.Name}";

            currentBroadcast.Status = BroadcastStatus.WaitingForAnswer;

            if (currentBroadcast.Recipients.Count <= 5 && currentBroadcast.Experts >= 0)
            { 
                SendQuestionToAnswerers(messageString, currentBroadcast.Recipients, message, currentBroadcast.Experts);
                ack.Text = Reply.GetReply(ReplyType.QuestionSubjectFound).Text;
                connector.Messages.SendMessage(ack);
                return true;
            }

            foreach (ChannelAccount channelAccoutn in currentBroadcast.Recipients)
            {
                Message broadcastMessage = new Message();
                broadcastMessage.From = ack.From;
                broadcastMessage.Text = messageString;
                broadcastMessage.Language = "en";
                broadcastMessage.To = channelAccoutn;
                connector.Messages.SendMessage(broadcastMessage);
            }

            connector.Messages.SendMessage(ack);
            ack.Text = Reply.GetReply(ReplyType.QuestionSubjectFound).Text;
            connector.Messages.SendMessage(ack);

            return true;
        }

        private Message BroadcastMessage(string subjectName, string broadcastText, Message message)
        {
            List<ChannelAccount> recipients = new List<ChannelAccount>();
            foreach (string user in accountsForId.Keys)
            {
                if (accountsForId[user].Id == message.From.Id) continue;

                recipients.Add(accountsForId[user]);
            }

            Broadcast.Add(subjectName, message.From, recipients, -1);

            Message ack = message.CreateReplyMessage($"And what was your question?");
            
            return ack;
        }

        private void SendQuestionToAnswerers(string messageText, List<ChannelAccount> potentialAnswerers, Message message, int experts)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("Question sent to ");
            for(int i = 0; i < potentialAnswerers.Count; i++)
            {
                if (i == 0)
                {
                }
                else if (i < potentialAnswerers.Count - 1)
                {
                    builder.Append(", ");
                }
                else
                {
                    builder.Append(" and ");
                }

                if (experts > 0)
                {
                    builder.Append(string.Format("@{0} (Expert)", potentialAnswerers[i].Name));
                    experts--;
                }
                else
                {
                    builder.Append(string.Format("@{0} (Casual)", potentialAnswerers[i].Name));
                }
            }

            Message ack = message.CreateReplyMessage(builder.ToString());
            var connector = new ConnectorClient();

            foreach (ChannelAccount account in potentialAnswerers)
            {
                Message questionMessage = new Message();
                questionMessage.From = ack.From;
                questionMessage.Text = messageText;
                questionMessage.Language = "en";
                questionMessage.To = account;
                connector.Messages.SendMessage(questionMessage);
            }

            connector.Messages.SendMessage(ack);
        }

        private Message FindExpert(string subjectName, Message message)
        {
            subjectName = subjectName.ToLower();
            Subject subject = Subject.GetAll().FirstOrDefault(p => p.Name == subjectName);
            string question = $"{Person.GetAll().FirstOrDefault(p => p.Id == message.From.Id).Username} needs help with {subjectName}";
            if (subject == null)
            {
                return BroadcastMessage(subjectName, question, message);
            }

            List<Person> persons = Person.GetAll().Where(p => p.Id != message.From.Id).ToList();

            List<KeyValuePair<Person, int>> potentialExperts = new List<KeyValuePair<Person, int>>();

            foreach (var person in persons)
            {
                int points = Matrix.GetPoints(person, subject);
                if (points > 5)
                {
                    potentialExperts.Add(new KeyValuePair<Person, int>(person, points));
                }
            }

            potentialExperts.Sort((p1, p2) => p2.Value - p1.Value);

            List<ChannelAccount> potentialAnswerers = new List<ChannelAccount>();
            List<string> experts = new List<string>();
            List<string> randoms = new List<string>();
            List<string> chosenIds = new List<string>();
            for (int i = 0; i < potentialExperts.Count && i < 3; ++i)
            {
                string chosenId = potentialExperts[i].Key.Id;
                if (accountsForId.ContainsKey(chosenId))
                {
                    chosenIds.Add(chosenId);
                    potentialAnswerers.Add(accountsForId[chosenId]);
                    experts.Add("@" + accountsForId[chosenId].Name);
                }
            }

            List<string> allIds = accountsForId.Keys.ToList();
            allIds.Remove(message.From.Id);
            foreach (string id in chosenIds)
                allIds.Remove(id);

            int n = allIds.Count;
            Random random = new Random();
            while (n > 1)
            {
                n--;
                int k = random.Next(n + 1);
                string value = allIds[k];
                allIds[k] = allIds[n];
                allIds[n] = value;
            }

            for (int i = 0; i < 5 - potentialAnswerers.Count && i < allIds.Count; ++i)
            {
                potentialAnswerers.Add(accountsForId[allIds[i]]);
                randoms.Add("@" + accountsForId[allIds[i]].Name);
            }

            if (potentialAnswerers.Count > 0)
            {
                Broadcast.Add(subjectName, message.From, potentialAnswerers, experts.Count);

                return message.CreateReplyMessage($"And what was your question?", "en");
            }
            else
            {
                return message.CreateReplyMessage(Reply.GetReply(ReplyType.NoExpertsFound).Text);
            }
        }

        private Message FindExpertise(string personName, Message message)
        {
            personName = personName.ToLower();
            Person person = Person.GetAll().FirstOrDefault(p => p.Username.ToLower() == personName);
            if (person == null)
            {
                return message.CreateReplyMessage(String.Format(Reply.GetReply(ReplyType.UnknownPerson).Text, personName), "en");
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
                response.Append(Reply.GetReply(ReplyType.NoExpertiseFound).Text);
            }
            else
            {
                for (int i = 0; i < potentialExpertise.Count && i < 3; ++i)
                {
                    if (i == 0)
                    {
                    }
                    else if (i < potentialExpertise.Count - 1)
                    {
                        response.Append(", ");
                    }
                    else
                    {
                        response.Append(" and ");
                    }

                    response.Append(string.Format($"{potentialExpertise[i].Key.Name}"));
                }
                response.Append(". " + Reply.GetReply(ReplyType.ExpertiseFound).Text);
            }

            return message.CreateReplyMessage(response.ToString(), "en");
        }

        private Message FindExpertiseForSubject(string personName, string subjectName, Message message)
        {
            personName = personName.ToLower();
            Person person = Person.GetAll().FirstOrDefault(p => p.Username.ToLower() == personName);
            if (person == null)
            {
                return message.CreateReplyMessage(String.Format(Reply.GetReply(ReplyType.UnknownPerson).Text, personName), "en");
            }
            Subject subject = Subject.GetAll().FirstOrDefault(p => p.Name == subjectName);
            if (subject == null)
            {
                return message.CreateReplyMessage(String.Format(Reply.GetReply(ReplyType.UnknownSubject).Text, subjectName), "en");
            }

            float points = Matrix.GetPoints(person, subject);

            StringBuilder response = new StringBuilder();
            response.Append(string.Format(Reply.GetReply(ReplyType.PointCount).Text, person.Username, points, subject.Name));
            return message.CreateReplyMessage(response.ToString(), "en");
        }
    }
}