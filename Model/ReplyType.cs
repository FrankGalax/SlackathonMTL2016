using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SlackathonMTL.Model
{
    public enum ReplyType
    {
        None,
        Greetings,
        QuestionSubjectFound,
        ManyExpertsFound,
        FewExpertsFound,
        NoExpertsFound,
        ManyExpertiseFound,
        FewExpertiseFound,
        NoExpertiseFound,
        AcceptedAnswer,
        Reference
    }
}