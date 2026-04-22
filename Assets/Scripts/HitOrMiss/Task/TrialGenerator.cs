using System.Collections.Generic;
using UnityEngine;

namespace HitOrMiss
{
    /// <summary>
    /// Generates 80 trial definitions per block with parametric variation.
    /// 20 per category: Hit, NearHit, NearMiss, Miss.
    /// Each trial has unique approach angle and curve direction with small random jitter.
    /// Trials are shuffled with a no-consecutive-same-category constraint.
    /// 
    /// Added on 22.04.26 by pame
    /// Trials now also receive a speed condition (Slow / Fast).
    /// Speed is assigned AFTER category shuffling so speed becomes an
    /// independent factor on top of category.
    /// </summary>
    /// </summary>
    public static class TrialGenerator
    {
        // Category offset ranges (meters from body center)
        static readonly Vector2 HitRange = new(0f, 0f);            // On body (0 offset)
        static readonly Vector2 NearHitRange = new(0f, 0.10f);     // 0-10 cm
        static readonly Vector2 NearMissRange = new(0.10f, 0.25f); // 10-25 cm
        static readonly Vector2 MissRange = new(0.30f, 0.45f);     // 30-45 cm

        const int TrialsPerCategory = 20;
        const float DefaultSpawnDistance = 7f; 
        const float DefaultVanishDistance = 1f;
        /*
        const float DefaultSpeed = 2.5f;
        */

        // Added by pam 
        // Two possible speeds for the task.
        // Units in Unity are meters/second.
        // 65 cm/s = 0.65 m/s
        // 85 cm/s = 0.85 m/s
        const float DefaultSlowSpeed = 0.65f;
        const float DefaultFastSpeed = 0.85f;
        const float DefaultBallDiameter = 0.175f;
        const float DefaultCurvatureMagnitude = 0.40f; // 30-50 cm displacement from straight line


        // Added by pam 
        // GenerateBlock now accepts two speeds:
        // - slowSpeed
        // - fastSpeed
        // We first generate all trials using slowSpeed as a placeholder,
        // then assign each trial its actual speed condition afterward.


          /*public static TrialDefinition[] GenerateBlock(int blockIndex, float spawnDistance = DefaultSpawnDistance,
            float vanishDistance = DefaultVanishDistance, float speed = DefaultSpeed, float ballDiameter = DefaultBallDiameter)*/
        public static TrialDefinition[] GenerateBlock(int blockIndex, float spawnDistance = DefaultSpawnDistance,
        float vanishDistance = DefaultVanishDistance, float slowSpeed = DefaultSlowSpeed, float fastSpeed = DefaultFastSpeed,
        float ballDiameter = DefaultBallDiameter)   
        {
            var trials = new List<TrialDefinition>();

/*
            trials.AddRange(GenerateCategory(blockIndex, TrialCategory.Hit, HitRange, SemanticCommand.Hit, spawnDistance, vanishDistance, speed, ballDiameter));
            trials.AddRange(GenerateCategory(blockIndex, TrialCategory.NearHit, NearHitRange, SemanticCommand.Hit, spawnDistance, vanishDistance, speed, ballDiameter));
            trials.AddRange(GenerateCategory(blockIndex, TrialCategory.NearMiss, NearMissRange, SemanticCommand.Miss, spawnDistance, vanishDistance, speed, ballDiameter));
            trials.AddRange(GenerateCategory(blockIndex, TrialCategory.Miss, MissRange, SemanticCommand.Miss, spawnDistance, vanishDistance, speed, ballDiameter));
*/

            // Added by pam 
            // Use slowSpeed as a temporary placeholder during generation.
            // Actual slow/fast assignment happens below in AssignSpeedConditions().
            trials.AddRange(GenerateCategory(blockIndex, TrialCategory.Hit, HitRange, SemanticCommand.Hit, spawnDistance, vanishDistance, slowSpeed, ballDiameter));
            trials.AddRange(GenerateCategory(blockIndex, TrialCategory.NearHit, NearHitRange, SemanticCommand.Hit, spawnDistance, vanishDistance, slowSpeed, ballDiameter));
            trials.AddRange(GenerateCategory(blockIndex, TrialCategory.NearMiss, NearMissRange, SemanticCommand.Miss, spawnDistance, vanishDistance, slowSpeed, ballDiameter));
            trials.AddRange(GenerateCategory(blockIndex, TrialCategory.Miss, MissRange, SemanticCommand.Miss, spawnDistance, vanishDistance, slowSpeed, ballDiameter));

            ShuffleNoConsecutive(trials);

            // Added by pam 
            // After shuffling categories, assign speed conditions chunk-wise.
            // This keeps speed orthogonal to category.
            AssignSpeedConditions(trials, slowSpeed, fastSpeed);  
            AssignTimings(trials);

            return trials.ToArray();
        }

        static List<TrialDefinition> GenerateCategory(int blockIndex, TrialCategory category,
            Vector2 offsetRange, SemanticCommand expected, float spawnDist, float vanishDist, float speed, float diameter)
        {
            var result = new List<TrialDefinition>(TrialsPerCategory);

            for (int i = 0; i < TrialsPerCategory; i++)
            {
                // Vary approach angle: spread across -30 to +30 degrees
                float approachAngle = Mathf.Lerp(-30f, 30f, (float)i / (TrialsPerCategory - 1));
                approachAngle += Random.Range(-3f, 3f); // small jitter

                // Alternate curve direction and add slight variation
                float curveDir = (i % 2 == 0) ? 1f : -1f;

                // Final lateral offset: random within category range, with random side
                float offsetMagnitude = Random.Range(offsetRange.x, Mathf.Max(offsetRange.x + 0.001f, offsetRange.y));
                float side = (Random.value > 0.5f) ? 1f : -1f;
                float finalOffset = offsetMagnitude * side;

                // Curvature magnitude with small variation (15-25 cm as per spec)
                float curvature = DefaultCurvatureMagnitude + Random.Range(-0.10f, 0.10f);

/*
                result.Add(new TrialDefinition
                {
                    trialId = $"B{blockIndex + 1}_T{result.Count + 1 + (int)category * TrialsPerCategory:D2}",
                    category = category,
                    approachAngleDeg = approachAngle,
                    curveDirection = curveDir,
                    curvatureMagnitude = curvature,
                    finalLateralOffset = finalOffset,
                    spawnDistance = spawnDist,
                    vanishDistance = vanishDist,
                    speed = speed,
                    ballDiameter = diameter,
                    expectedResponse = expected
                }); */


                // Added by pam 
                // Add a default speedCondition field.
                // This is only a temporary placeholder and will be overwritten later
                // by AssignSpeedConditions().

              result.Add(new TrialDefinition
                {
                    trialId = $"B{blockIndex + 1}_T{result.Count + 1 + (int)category * TrialsPerCategory:D2}", 
                    category = category,
                    approachAngleDeg = approachAngle,
                    curveDirection = curveDir,
                    curvatureMagnitude = curvature,
                    finalLateralOffset = finalOffset,
                    spawnDistance = spawnDist,
                    vanishDistance = vanishDist,
                    speedCondition = SpeedCondition.Slow,
                    speed = speed,
                    ballDiameter = diameter,
                    expectedResponse = expected
                });  

            }

            return result;
        }

        /// <summary>
        /// Shuffle trials so no two consecutive trials share the same category.
        /// Uses a greedy swap approach.
        /// </summary>
        static void ShuffleNoConsecutive(List<TrialDefinition> trials)
        {
            // First: Fisher-Yates shuffle
            for (int i = trials.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (trials[i], trials[j]) = (trials[j], trials[i]);
            }

            // Then: fix consecutive same-category pairs
            int maxPasses = 100;
            for (int pass = 0; pass < maxPasses; pass++)
            {
                bool foundConflict = false;
                for (int i = 1; i < trials.Count; i++)
                {
                    if (trials[i].category == trials[i - 1].category)
                    {
                        foundConflict = true;
                        // Find a swap candidate later in the list
                        bool swapped = false;
                        for (int k = i + 1; k < trials.Count; k++)
                        {
                            bool safeHere = trials[k].category != trials[i - 1].category;
                            bool safeThere = (k + 1 >= trials.Count || trials[i].category != trials[k + 1].category)
                                          && (k - 1 < 0 || trials[i].category != trials[k - 1].category);

                            if (safeHere && safeThere)
                            {
                                (trials[i], trials[k]) = (trials[k], trials[i]);
                                swapped = true;
                                break;
                            }
                        }
                        if (!swapped)
                        {
                            // Fallback: just swap with a random non-adjacent position
                            int r = Random.Range(Mathf.Min(i + 2, trials.Count - 1), trials.Count);
                            (trials[i], trials[r]) = (trials[r], trials[i]);
                        }
                    }
                }
                if (!foundConflict) break;
            }
        }
        

        /// Added by pam
        /// Assign a speed condition and corresponding numeric speed to each trial.
        /// This is done after category shuffle so speed varies independently from category.
        /// </summary>
static void AssignSpeedConditions(List<TrialDefinition> trials, float slowSpeed, float fastSpeed)
{
    var speedSequence = GenerateBalancedSpeedSequence(trials.Count);

    for (int i = 0; i < trials.Count; i++)
    {
        var t = trials[i];
        t.speedCondition = speedSequence[i];
        t.speed = (t.speedCondition == SpeedCondition.Fast) ? fastSpeed : slowSpeed;
        trials[i] = t;
    }
}

        static List<SpeedCondition> GenerateBalancedSpeedSequence(int totalTrials)
        {
            var sequence = new List<SpeedCondition>(totalTrials);

            if (totalTrials % 10 != 0)
            {
                Debug.LogWarning("Total trials is not a multiple of 10. Speed chunking will be approximate.");
            }

            int chunkCount = totalTrials / 10;

            var chunkTemplates = new List<(int slowCount, int fastCount)>
            {
                (7, 3),
                (3, 7),
                (6, 4),
                (4, 6),
                (7, 3),
                (3, 7),
                (6, 4),
                (4, 6)
            };

            while (chunkTemplates.Count > chunkCount)
                chunkTemplates.RemoveAt(chunkTemplates.Count - 1);

            for (int i = chunkTemplates.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (chunkTemplates[i], chunkTemplates[j]) = (chunkTemplates[j], chunkTemplates[i]);
            }

            foreach (var chunk in chunkTemplates)
            {
                bool slowFirst = Random.value > 0.5f;
                AddSpeedChunk(sequence, chunk.slowCount, chunk.fastCount, slowFirst);
            }

            return sequence;
        }

        static void AddSpeedChunk(List<SpeedCondition> sequence, int slowCount, int fastCount, bool slowFirst)
        {
            if (slowFirst)
            {
                for (int i = 0; i < slowCount; i++)
                    sequence.Add(SpeedCondition.Slow);

                for (int i = 0; i < fastCount; i++)
                    sequence.Add(SpeedCondition.Fast);
            }
            else
            {
                for (int i = 0; i < fastCount; i++)
                    sequence.Add(SpeedCondition.Fast);

                for (int i = 0; i < slowCount; i++)
                    sequence.Add(SpeedCondition.Slow);
            }
        }

        /// <summary>
        /// Assign block-relative start times with ~4s inter-trial interval + small jitter.
        /// </summary>
        static void AssignTimings(List<TrialDefinition> trials)
        {
            float time = 0f;
            for (int i = 0; i < trials.Count; i++)
            {
                var t = trials[i];
                t.trialId = $"{t.trialId.Split('_')[0]}_T{i + 1:D2}";
                time += (i == 0) ? 0f : 4f + Random.Range(-0.3f, 0.3f); // ~4s ITI with jitter
                trials[i] = t;
            }
        }
    }
}
