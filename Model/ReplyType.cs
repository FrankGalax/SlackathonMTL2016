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
        ExpertiseFound,
        NoExpertiseFound,
        AcceptedAnswer,
        UnknownPerson, // Person not in DB
        UnknownSubject, // Subject not in DB
        PointCount, // How many points does a person have for a given subject
        BroadcastMessage, // Help call (broadcasted)
        BroadcastSentMessage, // Broadcast was sent
        IsGoodAnswerQuestion, // Is the given answer appropriate
        HasAnsweredMessage, // Person has answered message
        RefusedAnswerReply, // The person has rejected the answer
        AnswererFeedbackPositive, // The message sent to the person who answers the question (his answer was accepted)
        AnswererFeedbackNegative // The message sent to the person who answers the question (his answer was refused)
        // TODO: WhatWasYourQuestion 
    }
}