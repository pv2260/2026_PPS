using System;
using UnityEngine;

namespace HitOrMiss.Cybersickness
{
    public enum CyberBugCondition
    {
        Miss,
        ContactSplash
    }

    public enum CyberYesNoResponse
    {
        None,
        Yes,
        No
    }

    [Serializable]
    public struct CyberBugTrialDefinition
    {
        public string trialId;

        public int blockIndex;
        public int trialIndex;

        public CyberBugCondition condition;

        public float startDistance;
        public float speed;

        public float lateralOffset;
        public float contactDistance;
        public float passBehindDistance;

        public bool willTouch;
        public bool willSplash;

        public CyberYesNoResponse expectedResponse;
    }

    [Serializable]
    public struct CyberBugTrialResult
    {
        public string participantId;

        public string trialId;
        public int blockIndex;
        public int trialIndex;

        public CyberBugCondition condition;

        public float startDistance;
        public float speed;
        public float lateralOffset;

        public bool willTouch;
        public bool willSplash;

        public CyberYesNoResponse expectedResponse;
        public CyberYesNoResponse participantResponse;

        public bool responded;
        public bool correct;

        public double trialStartTime;
        public double responseTime;
        public double outcomeTime;

        public double reactionTimeMs;
    }
}