using UnityEngine;
using System;

namespace HitOrMiss
{
    public static class TriggerEncoder
    {
        // ====================================================================
        // Task 1 — PPS / vibrotactile
        // ====================================================================

        public enum Task1TrialType
        {
            VisualOnly            = 1,
            VibrotactileOnly      = 2,
            Both                  = 3,
        }

        public enum Task1Width
        {
            Narrow = 1,
            Wide   = 2,
        }

        public enum Task1Speed
        {
            Slow = 1,
            Fast = 2,
        }

        public enum Task1Distance
        {
            D1   = 0,
            D2   = 1,
            D3   = 2,
            D4   = 3,
            D5   = 4,
            D6   = 5,
            D7   = 6,
        }

        public static int EncodeTask1(
            string distance,
            Task1TrialType type,
            Task1Width width,
            Task1Speed speed)
        {
            int w = (int)width - 1;   // 0..1
            int s = (int)speed - 1;   // 0..1

            if (type == Task1TrialType.VisualOnly)
            {
                // 1..4
                return w * 2 + s + 1;
            }

            int t = type == Task1TrialType.VibrotactileOnly ? 0 : 1;  // 0..1

            int d = distance switch
            {
                "D1" => 0,
                "D2" => 1,
                "D3" => 2,
                "D4" => 3,
                "D5" => 4,
                "D6" => 5,
                "D7" => 6,
                _    => throw new ArgumentException($"Invalid distance for non-VisualOnly trial: {distance}")
            };

            // 5..60
            return (((t * 7 + d) * 2 + w) * 2 + s) + 5;
        }

        // ====================================================================
        // Task 2 — Hit-or-Miss
        // ====================================================================

        public static int EncodeTask2(
            TrialCategory trajectory,
            SpeedLevel speed,
            TransitionStatus transition)
        {
            int trajectoryIndex = trajectory.ToTriggerDigit() - 1;  // 0..3
            int speedIndex      = speed.ToTriggerDigit()      - 1;  // 0..1
            int transitionIndex = transition.ToTriggerDigit();       // 0..2

            return 1 + trajectoryIndex * 6 + speedIndex * 3 + transitionIndex;
        }
    }
}
