using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SlackathonMTL.Model
{
    public enum IntentType
    {
        None,
        FindAnExpert,
        FindExpertise,
        FindExpertiseForSubject,
        BroadcastAnswerAccept,
        BroadcastAnswerDenied
    }

    public class InterpretorResult
    {
        public string query { get; set; }
        public Intent[] intents { get; set; }
        public Entity[] entities { get; set; }
    }

    public class Intent
    {
        public string intent { get; set; }
        public float score { get; set; }
        public Action[] actions { get; set; }
        public IntentType GetIntentType()
        {
            return (IntentType)Enum.Parse(typeof(IntentType), intent);
        }
    }

    public class Action
    {
        public bool triggered { get; set; }
        public string name { get; set; }
        public object[] parameters { get; set; }
    }

    public class Entity
    {
        public string entity { get; set; }
        public string type { get; set; }
        public int startIndex { get; set; }
        public int endIndex { get; set; }
        public float score { get; set; }
        public string GetEntityName() { return entity.Replace(" ", ""); }
    }

}