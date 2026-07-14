using System;
using UnityEngine;

namespace HitOrMiss.Pps
{
    /// <summary>
    /// ScriptableObject container for all PPS protocol parameters.
    /// Edit in the Inspector (one asset per study) — no recompilation needed.
    ///
    /// NOTE: the C# initialisers below are only defaults for a NEWLY created asset.
    /// An existing .asset already has its values serialised, so editing this file
    /// will not change it. Change values in the Inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "PpsTask", menuName = "Parkinson/HitOrMiss/PPS Task Asset")]
    public class PpsTaskAsset : ScriptableObject
    {
        [SerializeField] string m_TaskName = "PPS Looming Task";

        [Header("Protocol")]
        [Tooltip("Number of experimental blocks")]
        [SerializeField] int m_BlockCount = 3;


        [Tooltip("Total trials per block. Derived: forced to VT + V + T.")]
        [SerializeField] int m_TrialsPerBlock = 154;

        [Header("Trial counts per block")]

        [Tooltip("Visuotactile trials per block. Must divide evenly by " +
                 "(distances x speeds x active widths) or PpsTrialGenerator will throw. " +
                 "Default 70 = 5 reps x 7 distances x 2 speeds, width factor OFF.")]
        [SerializeField, Min(0)] int m_VtTrialsPerBlock = 70;

        [Tooltip("Visual-only (catch) trials per block. Must divide evenly by " +
                 "(speeds x active widths). Default 28 = 14 reps x 2 speeds, width factor OFF.")]
        [SerializeField, Min(0)] int m_VisualOnlyTrialsPerBlock = 28;

        [Tooltip("Tactile-only (baseline) trials per block. Must divide evenly by " +
                 "(distances x speeds). T trials NEVER cross width: nothing is rendered, so " +
                 "width is meaningless and crossing it would only halve the trials per timing " +
                 "cell. Default 56 = 4 reps x 7 distances x 2 speeds.")]
        [SerializeField, Min(0)] int m_TactileOnlyTrialsPerBlock = 56;

        [Header("Width factor")]
        [Tooltip("Width (narrow vs wide LED separation) changes the lateral extent of the looming " +
                 "pair. It only affects the VISUAL stimulus, so it applies to VT and V trials and " +
                 "never to T trials.\n\n" +
                 "OFF (recommended): every trial uses DefaultWidth. Width is not a factor.\n\n" +
                 "ON: doubles the VT and V cell count, which HALVES the trials per cell at the same " +
                 "block size. The pilot ran with width crossed and only 9 VT trials per " +
                 "(distance x speed x width) cell, below every study in the literature. Enable this " +
                 "only if width is preregistered AND the trial budget accounts for it.")]
        [SerializeField] bool m_UseWidthFactor = false;

        [Tooltip("Width used on every trial when UseWidthFactor is off.")]
        [SerializeField] PpsWidth m_DefaultWidth = PpsWidth.Narrow;

        [Header("Loom timing")]
        [Tooltip("Seconds for the SCORED loom only. The warm-up is additional and is derived from " +
                 "WarmupDistanceMeters at the same velocity, so it is NOT included here. ")]
        [SerializeField] float m_FastDurationSeconds = 1.5f;
        [SerializeField] float m_SlowDurationSeconds = 4.0f;

        [Header("Motion curve (shared by visual loom and tactile-only timing)")]
        [Tooltip("Normalized loom progress t in [0,1] -> curved progress.\n\n" +
                 "MUST BE LINEAR. Two reasons:\n" +
                 "  1. A non-linear curve makes the loom's instantaneous velocity vary across the " +
                 "distance stages, confounding distance with velocity WITHIN a trial. Every PPS " +
                 "study in the literature uses constant velocity.\n" +
                 "  2. With EaseInOut (smoothstep) the velocity at the first scored stage is exactly " +
                 "ZERO, so the warm-up glides in at constant speed, the lights stop dead at D7, then " +
                 "accelerate away. That is a stronger cue than having no warm-up at all.\n\n" +
                 "Right-click this asset in the Project window -> 'Set Motion Curve to Linear'.")]
        [SerializeField] AnimationCurve m_MotionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Header("Inter-trial interval")]
        [Tooltip("ITI is drawn uniformly from [min, max] before every trial. The jitter is what " +
                 "breaks between-trial temporal anticipation, and it is why the fast/slow duration " +
                 "padding is no longer needed. A fixed ITI lets the participant learn the rhythm.")]
        [SerializeField, Min(0f)] float m_ItiMinSeconds = 1.2f;
        [SerializeField, Min(0f)] float m_ItiMaxSeconds = 1.8f;

        [Header("Response window")]
        [Tooltip("Hard ceiling on the RT window, measured from the vibration. The window opens at " +
                 "trial start (so anticipations and false alarms are still caught) and closes on the " +
                 "first press or after this interval, whichever comes first. Presses after it are " +
                 "pacing presses, never reaction times. Petrizzo et al. (2024) excluded RTs above " +
                 "1000 ms as inattention.")]
        [SerializeField, Min(0.1f)] float m_ResponseWindowSeconds = 1.0f;

        [Tooltip("Refractory gap between the RT window closing and the advance gate opening. Stops " +
                 "a late detection press being silently consumed as the advance press.")]
        [SerializeField, Min(0f)] float m_AdvanceGateDelaySeconds = 0.3f;

        [Tooltip("Safety timeout on the advance gate. Prevents the unbounded hang that produced " +
                 "71-second 'reaction times' in the pilot.")]
        [SerializeField, Min(1f)] float m_AdvanceGateTimeoutSeconds = 15f;

        [Tooltip("If true, EVERY trial waits for a press to advance (fully self-paced). If false, " +
                 "only missed trials do: the pace stays fixed while the participant is engaged, and " +
                 "control is handed back only when they lose the thread. False is recommended.")]
        [SerializeField] bool m_GateEveryTrial = false;

        [Header("Spatial layout (all values in METERS, from the body anchor)")]

        [Tooltip("Distance forward from the body anchor to the fixation crosshair (meters).")]
        [SerializeField, Min(0.01f)] float m_CrosshairDistance = 6.0f;

        [Tooltip("Vertical offset of the crosshair above the body anchor (meters). Typically eye level.")]
        [SerializeField, Min(0f)] float m_CrosshairHeight = 0.9f;

        [Tooltip("Fallback shoulder width in meters. Used as the narrow LED separation when no " +
                 "participant-specific value is provided.")]
        [SerializeField, Min(0.01f)] float m_DefaultShoulderWidthMeters = 0.40f;

        [Tooltip("Extra meters added to the narrow separation for the WIDE condition. Only used " +
                 "when UseWidthFactor is on, or when DefaultWidth is Wide.")]
        [SerializeField, Min(0f)] float m_WideOffsetMeters = 0.30f;

        [Tooltip("Vertical offset of the side LEDs relative to the body anchor.")]
        [SerializeField] float m_LedHeight = 0.9f;

        [Header("Distance stages")]

        [Tooltip("Farthest SCORED point. Serino et al. (2015) sampled 5-197 cm in VR; " +
                 "Petrizzo et al. (2024) used 0.25-2.25 m and found the VR PPS boundary at ~1.2 m.")]
        [SerializeField, Min(0.01f)] float m_LoomStartDistance = 2.25f;

        [Tooltip("Nearest SCORED point (D1).")]
        [SerializeField, Min(0.01f)] float m_LoomEndDistance = 0.25f;

        [Tooltip("Number of equally spaced distance stages, including start and end.\n\n" +
                 "The stages in use are always the N NEAREST labels: 7 gives D7..D1, 6 gives D6..D1, " +
                 "5 gives D5..D1. The farthest label in use always sits at LoomStartDistance and D1 " +
                 "always sits at LoomEndDistance, so lowering this widens the spacing rather than " +
                 "truncating the range.")]
        [SerializeField, Min(2)] int m_DistanceStageCount = 7;

        [Header("Loom warm-up (pre-D7 visibility)")]

        [Tooltip("Distance in meters at which the lights first APPEAR, before gliding in to the " +
                 "farthest scored stage.\n\n" +
                 "MUST be greater than LoomStartDistance, or the warm-up is silently skipped and the " +
                 "LEDs pop into existence at D7.\n\n" +
                 "The warm-up DURATION is not set here: it is derived so the glide runs at the loom's " +
                 "own velocity, which is what makes D7 imperceptible. It is therefore speed-dependent " +
                 "(longer on slow trials). Tactile-only trials wait the same interval blind, so their " +
                 "vibration lands at the same elapsed time as the matched VT trial.")]
        [SerializeField, Min(0.01f)] float m_WarmupDistanceMeters = 3.0f;

        [Header("Phase durations")]
        [SerializeField] float m_RestDurationSeconds = 30f;

        [Header("Phase content (Localization keys)")]
        [SerializeField] string m_InstructionsKey = "pps.instructions";
        [SerializeField] string m_PracticeIntroKey = "pps.practice_intro";
        [SerializeField] string m_BlockIntroKey = "pps.block_intro";
        [SerializeField] string m_OutroKey = "pps.outro";

        [Header("Trial generator")]
        [SerializeField] TrialOrder m_OrderingStrategy = TrialOrder.Shuffled;

        [Tooltip("-1 = time-seeded (non-reproducible). Any other value = reproducible seed.")]
        [SerializeField] int m_RngSeed = -1;

        [Header("Vibration")]
        [SerializeField, Min(1)] private int m_VibrationDurationMs = 100;

        public int VibrationDurationMs => m_VibrationDurationMs;

        // Compatibility value only.
        // The external vibration app controls real intensity.
        public float VibrationIntensity => 1f;

        // ------------------------------------------------------------------
        // Stage ordering
        // ------------------------------------------------------------------

        /// <summary>All seven labels, farthest first. Never reorder.</summary>
        static readonly DistanceStage[] k_AllStages =
        {
            DistanceStage.D7,
            DistanceStage.D6,
            DistanceStage.D5,
            DistanceStage.D4,
            DistanceStage.D3,
            DistanceStage.D2,
            DistanceStage.D1
        };

        /// <summary>
        /// The stages actually sampled, farthest first. With DistanceStageCount = N this is
        /// the N NEAREST labels: 7 -> D7..D1, 6 -> D6..D1, 5 -> D5..D1.
        ///
        /// The slicing matters. The old StageIndex hard-coded D7=0 ... D1=6 and then clamped
        /// to (N-1). At N=6 that mapped BOTH D2 and D1 to index 5, so two stages collapsed
        /// onto the same distance AND the same firing time, silently and with no error.
        /// </summary>
        public DistanceStage[] ActiveStages
        {
            get
            {
                int n = Mathf.Clamp(m_DistanceStageCount, 2, k_AllStages.Length);
                var result = new DistanceStage[n];
                Array.Copy(k_AllStages, k_AllStages.Length - n, result, 0, n);
                return result;
            }
        }

        public bool IsStageActive(DistanceStage stage)
            => Array.IndexOf(ActiveStages, stage) >= 0;

        // ------------------------------------------------------------------
        // Width factor
        // ------------------------------------------------------------------

        public bool UseWidthFactor => m_UseWidthFactor;
        public PpsWidth DefaultWidth => m_DefaultWidth;

        /// <summary>
        /// The widths the generator crosses for VISUAL trials (VT and V). One entry when
        /// the factor is off. Tactile-only trials always use DefaultWidth, because no LEDs
        /// are rendered and SeparationFor() is never called on them.
        /// </summary>
        public PpsWidth[] ActiveWidths =>
            m_UseWidthFactor
                ? new[] { PpsWidth.Narrow, PpsWidth.Wide }
                : new[] { m_DefaultWidth };

        // ------------------------------------------------------------------
        // Public getters
        // ------------------------------------------------------------------

        public string TaskName => m_TaskName;
        public int BlockCount => m_BlockCount;

        public int TrialsPerBlock => m_TrialsPerBlock;
        public int VtTrialsPerBlock => m_VtTrialsPerBlock;
        public int VisualOnlyTrialsPerBlock => m_VisualOnlyTrialsPerBlock;
        public int TactileOnlyTrialsPerBlock => m_TactileOnlyTrialsPerBlock;

        public float FastDurationSeconds => m_FastDurationSeconds;
        public float SlowDurationSeconds => m_SlowDurationSeconds;

        public float ItiMinSeconds => m_ItiMinSeconds;
        public float ItiMaxSeconds => Mathf.Max(m_ItiMaxSeconds, m_ItiMinSeconds);

        public float ResponseWindowSeconds     => m_ResponseWindowSeconds;
        public float AdvanceGateDelaySeconds   => m_AdvanceGateDelaySeconds;
        public float AdvanceGateTimeoutSeconds => m_AdvanceGateTimeoutSeconds;
        public bool  GateEveryTrial            => m_GateEveryTrial;

        public AnimationCurve MotionCurve => m_MotionCurve;

        public float DefaultShoulderWidthMeters => m_DefaultShoulderWidthMeters;
        public float WideOffsetMeters => m_WideOffsetMeters;

        public float CrosshairDistance => m_CrosshairDistance;
        public float CrosshairHeight => m_CrosshairHeight;

        public float NarrowSeparation => m_DefaultShoulderWidthMeters;
        public float WideSeparation => m_DefaultShoulderWidthMeters + m_WideOffsetMeters;

        public float LedHeight => m_LedHeight;

        public float LoomStartDistance => m_LoomStartDistance;
        public float LoomEndDistance => m_LoomEndDistance;
        public int DistanceStageCount => m_DistanceStageCount;

        public float WarmupDistanceMeters => m_WarmupDistanceMeters;


        public float RestDurationSeconds => m_RestDurationSeconds;

        public string InstructionsKey => m_InstructionsKey;
        public string PracticeIntroKey => m_PracticeIntroKey;
        public string BlockIntroKey => m_BlockIntroKey;
        public string OutroKey => m_OutroKey;

        public TrialOrder OrderingStrategy => m_OrderingStrategy;
        public int? RngSeed => m_RngSeed < 0 ? null : m_RngSeed;

        // ------------------------------------------------------------------
        // Warm-up
        // ------------------------------------------------------------------

        /// <summary>
        /// The warm-up runs only when the lights start FARTHER than the first scored stage.
        /// If WarmupDistanceMeters is not greater than LoomStartDistance the glide is
        /// skipped and the LEDs appear abruptly at D7. This is what happened when
        /// LoomStartDistance was set to 20 m while the warm-up sat at 4 m.
        /// </summary>
        public bool WarmupActive => m_WarmupDistanceMeters > m_LoomStartDistance;

        /// <summary>
        /// Seconds of warm-up before the first stage callback, at the given speed.
        ///
        /// SINGLE SOURCE OF TRUTH. LoomingPairController.RunLoom calls this for the visual
        /// glide, and PpsTaskManager calls it for the tactile-only blind wait. If the two
        /// ever computed it independently they could drift apart and T would silently stop
        /// being delay-matched to VT.
        ///
        /// Speed-dependent by design: the glide runs at the loom's own velocity, so there
        /// is no velocity discontinuity at D7. Slower trials get a longer warm-up.
        /// </summary>
        public float WarmupLeadSeconds(PpsSpeed speed)
        {
            if (!WarmupActive) return 0f;

            float scoredTravel = m_LoomStartDistance - m_LoomEndDistance;
            float velocity     = scoredTravel / Mathf.Max(0.0001f, DurationFor(speed));
            float extra        = m_WarmupDistanceMeters - m_LoomStartDistance;

            return velocity > 0f ? extra / velocity : 0f;
        }

        /// <summary>Total trial length from trial start, at the given speed.</summary>
        public float TotalTrialSeconds(PpsSpeed speed)
            => WarmupLeadSeconds(speed) + DurationFor(speed);

        // ------------------------------------------------------------------
        // Motion curve
        // ------------------------------------------------------------------

        /// <summary>True when Evaluate(t) == t across the range, i.e. constant velocity.</summary>
        public bool IsMotionCurveLinear()
        {
            if (m_MotionCurve == null) return false;

            for (int i = 0; i <= 20; i++)
            {
                float t = i / 20f;
                if (Mathf.Abs(m_MotionCurve.Evaluate(t) - t) > 0.01f)
                    return false;
            }
            return true;
        }

#if UNITY_EDITOR
        [ContextMenu("Set Motion Curve to Linear")]
        void SetMotionCurveLinear()
        {
            m_MotionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[PpsTaskAsset] '{name}' MotionCurve set to Linear (0,0) -> (1,1).", this);
        }

        /// <summary>
        /// Prints the full design: stage distances, firing times, reps per cell, catch
        /// rate, and estimated session length. Run this after every Inspector change.
        /// </summary>
        [ContextMenu("Log Trial Structure")]
        void LogTrialStructure()
        {
            var stages = ActiveStages;
            var widths = ActiveWidths;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[PpsTaskAsset] '{name}'");
            sb.AppendLine($"  width factor: {(m_UseWidthFactor ? "ON (narrow + wide)" : $"OFF (all {m_DefaultWidth})")}");
            sb.AppendLine($"  stages: {stages.Length} | speeds: 2 | widths: {widths.Length}");

            foreach (var s in stages)
                sb.AppendLine(
                    $"    {s}: {DistanceForStage(s):F3} m | " +
                    $"fast fires {WarmupLeadSeconds(PpsSpeed.Fast) + TimeToReachStage(PpsSpeed.Fast, s):F2} s | " +
                    $"slow fires {WarmupLeadSeconds(PpsSpeed.Slow) + TimeToReachStage(PpsSpeed.Slow, s):F2} s");

            int vtCells = stages.Length * 2 * widths.Length;
            int tCells  = stages.Length * 2;
            int vCells  = 2 * widths.Length;

            sb.AppendLine($"  VT: {m_VtTrialsPerBlock}/block / {vtCells} cells = " +
                          $"{(vtCells > 0 ? m_VtTrialsPerBlock / (float)vtCells : 0):F2} reps/cell/block " +
                          $"({(vtCells > 0 ? m_BlockCount * m_VtTrialsPerBlock / (float)vtCells : 0):F0} over {m_BlockCount} blocks)");
            sb.AppendLine($"  T:  {m_TactileOnlyTrialsPerBlock}/block / {tCells} cells = " +
                          $"{(tCells > 0 ? m_TactileOnlyTrialsPerBlock / (float)tCells : 0):F2} reps/cell/block " +
                          $"({(tCells > 0 ? m_BlockCount * m_TactileOnlyTrialsPerBlock / (float)tCells : 0):F0} over {m_BlockCount} blocks)");
            sb.AppendLine($"  V:  {m_VisualOnlyTrialsPerBlock}/block / {vCells} cells");
            sb.AppendLine($"  catch rate: {(m_TrialsPerBlock > 0 ? 100f * m_VisualOnlyTrialsPerBlock / m_TrialsPerBlock : 0):F1}%");
            sb.AppendLine($"  trial length: fast {TotalTrialSeconds(PpsSpeed.Fast):F2} s | slow {TotalTrialSeconds(PpsSpeed.Slow):F2} s");

            float meanIti   = 0.5f * (ItiMinSeconds + ItiMaxSeconds);
            float meanTrial = 0.5f * (TotalTrialSeconds(PpsSpeed.Fast) + TotalTrialSeconds(PpsSpeed.Slow));
            float minutes   = m_BlockCount * m_TrialsPerBlock * (meanTrial + meanIti) / 60f;
            sb.AppendLine($"  estimated task time: {minutes:F1} min " +
                          $"({m_BlockCount} x {m_TrialsPerBlock} = {m_BlockCount * m_TrialsPerBlock} trials, excl. breaks)");

            Debug.Log(sb.ToString(), this);
        }
#endif

        // ------------------------------------------------------------------
        // Distance stages
        // ------------------------------------------------------------------

        public float DistanceD7 => DistanceForStage(DistanceStage.D7);
        public float DistanceD6 => DistanceForStage(DistanceStage.D6);
        public float DistanceD5 => DistanceForStage(DistanceStage.D5);
        public float DistanceD4 => DistanceForStage(DistanceStage.D4);
        public float DistanceD3 => DistanceForStage(DistanceStage.D3);
        public float DistanceD2 => DistanceForStage(DistanceStage.D2);
        public float DistanceD1 => DistanceForStage(DistanceStage.D1);

        public float DurationFor(PpsSpeed speed)
        {
            return speed == PpsSpeed.Fast ? m_FastDurationSeconds : m_SlowDurationSeconds;
        }

        public float SeparationFor(PpsWidth width)
        {
            return SeparationFor(width, 0f);
        }

        public float SeparationFor(PpsWidth width, float participantShoulderWidthMeters)
        {
            float shoulder = participantShoulderWidthMeters > 0f
                ? participantShoulderWidthMeters
                : m_DefaultShoulderWidthMeters;

            float narrow = Mathf.Max(shoulder, m_DefaultShoulderWidthMeters);

            return width == PpsWidth.Wide
                ? narrow + m_WideOffsetMeters
                : narrow;
        }

        public PpsTrialDefinition[] GenerateBlock(int blockIndex)
        {
            return PpsTrialGenerator.Generate(this, blockIndex);
        }

        /// <summary>
        /// Runtime-only clone that can be mutated for the active session without touching
        /// the on-disk ScriptableObject. The caller (PpsAppController) destroys the clone
        /// at session end.
        /// </summary>
        public PpsTaskAsset CreateSessionClone()
        {
            return Instantiate(this);
        }

        /// <summary>
        /// Applies overrides from the clinician form. Only call this on a
        /// CreateSessionClone() result so the on-disk asset stays clean.
        /// </summary>
        public void ApplyTask1SessionOverrides(SessionMetadata md)
        {
            if (md.task1NumberOfBlocks > 0)
                m_BlockCount = md.task1NumberOfBlocks;

            if (md.task1VtTrialsPerBlock > 0)
                m_VtTrialsPerBlock = md.task1VtTrialsPerBlock;
            if (md.task1VisualOnlyTrialsPerBlock > 0)
                m_VisualOnlyTrialsPerBlock = md.task1VisualOnlyTrialsPerBlock;
            if (md.task1TactileOnlyTrialsPerBlock > 0)
                m_TactileOnlyTrialsPerBlock = md.task1TactileOnlyTrialsPerBlock;

            m_TrialsPerBlock =
                m_VtTrialsPerBlock + m_VisualOnlyTrialsPerBlock + m_TactileOnlyTrialsPerBlock;

            if (md.task1BreakDurationSeconds > 0f)
                m_RestDurationSeconds = md.task1BreakDurationSeconds;

            if (md.task1WideOffsetCm > 0f)
                m_WideOffsetMeters = md.task1WideOffsetCm / 100f;
        }

        /// <summary>
        /// Normalized progress for a stage: 0 at the farthest ACTIVE stage, 1 at D1.
        ///
        /// The farthest active stage sits at progress 0, so TimeToReachStage returns
        /// exactly 0 for it. That is why tactile-only trials must add WarmupLeadSeconds:
        /// without it a T trial at the farthest stage fires on the same frame as trial start.
        /// </summary>
        public float ProgressForStage(DistanceStage stage)
        {
            int index = StageIndex(stage);
            int maxIndex = Mathf.Max(1, ActiveStages.Length - 1);

            return Mathf.Clamp01((float)index / maxIndex);
        }

        /// <summary>
        /// Distance in meters for a stage. Active stages are equally spaced between
        /// LoomStartDistance and LoomEndDistance, so spacing = (start - end) / (N - 1).
        /// </summary>
        public float DistanceForStage(DistanceStage stage)
        {
            float progress = ProgressForStage(stage);
            return Mathf.Lerp(m_LoomStartDistance, m_LoomEndDistance, progress);
        }

        /// <summary>
        /// Elapsed seconds FROM the farthest active stage at which the motion curve reaches
        /// the given stage. Does NOT include the warm-up: callers needing time from trial
        /// start must add WarmupLeadSeconds(speed).
        /// </summary>
        public float TimeToReachStage(PpsSpeed speed, DistanceStage stage)
        {
            float threshold = ProgressForStage(stage);

            float duration = DurationFor(speed);
            if (threshold <= 0f) return 0f;

            const int steps = 512;
            float prevT = 0f;
            float prevY = m_MotionCurve.Evaluate(0f);

            for (int i = 1; i <= steps; i++)
            {
                float t = (float)i / steps;
                float y = m_MotionCurve.Evaluate(t);

                if (y >= threshold)
                {
                    float frac = Mathf.Approximately(y, prevY)
                        ? 0f
                        : (threshold - prevY) / (y - prevY);

                    return Mathf.Lerp(prevT, t, frac) * duration;
                }

                prevT = t;
                prevY = y;
            }

            return duration;
        }

        /// <summary>
        /// Index within ActiveStages: 0 = farthest, N-1 = D1. Derived from the array rather
        /// than a hard-coded switch, which is what let D2 and D1 collide at N=6.
        /// </summary>
        int StageIndex(DistanceStage stage)
        {
            int i = Array.IndexOf(ActiveStages, stage);
            return i >= 0 ? i : 0;
        }

        void OnValidate()
        {
            if (m_BlockCount < 1) m_BlockCount = 1;

            if (m_VtTrialsPerBlock < 0) m_VtTrialsPerBlock = 0;
            if (m_VisualOnlyTrialsPerBlock < 0) m_VisualOnlyTrialsPerBlock = 0;
            if (m_TactileOnlyTrialsPerBlock < 0) m_TactileOnlyTrialsPerBlock = 0;

            m_TrialsPerBlock =
                m_VtTrialsPerBlock + m_VisualOnlyTrialsPerBlock + m_TactileOnlyTrialsPerBlock;

            if (m_TrialsPerBlock < 1)
            {
                m_VtTrialsPerBlock = 1;
                m_VisualOnlyTrialsPerBlock = 0;
                m_TactileOnlyTrialsPerBlock = 0;
                m_TrialsPerBlock = 1;
            }

            if (m_FastDurationSeconds <= 0f) m_FastDurationSeconds = 0.1f;
            if (m_SlowDurationSeconds <= 0f) m_SlowDurationSeconds = 0.1f;

            if (m_LoomStartDistance <= 0f) m_LoomStartDistance = 0.01f;
            if (m_LoomEndDistance <= 0f) m_LoomEndDistance = 0.01f;

            if (m_DistanceStageCount < 2) m_DistanceStageCount = 2;
            if (m_DistanceStageCount > 7) m_DistanceStageCount = 7;

            if (m_LoomStartDistance <= m_LoomEndDistance)
            {
                Debug.LogWarning(
                    $"[PpsTaskAsset] '{name}' loom distances out of order. Expected " +
                    $"LoomStartDistance > LoomEndDistance. Got start={m_LoomStartDistance}, " +
                    $"end={m_LoomEndDistance}.", this);
            }

            if (!WarmupActive)
            {
                Debug.LogWarning(
                    $"[PpsTaskAsset] '{name}' warm-up is DISABLED: WarmupDistanceMeters " +
                    $"({m_WarmupDistanceMeters:F2}m) must be GREATER than LoomStartDistance " +
                    $"({m_LoomStartDistance:F2}m). The LEDs will pop into existence at the first " +
                    $"scored stage, and tactile-only trials there will fire on the same frame as " +
                    $"trial start.", this);
            }

            if (!IsMotionCurveLinear())
            {
                Debug.LogWarning(
                    $"[PpsTaskAsset] '{name}' MotionCurve is NOT linear. The loom will change speed " +
                    $"across the distance stages, and with EaseInOut its velocity at the first " +
                    $"scored stage is exactly zero. Right-click this asset in the Project window " +
                    $"and choose 'Set Motion Curve to Linear'.", this);
            }

            // Divisibility. PpsTrialGenerator throws on these; warn here so the Inspector
            // tells you before you press Play.
            int stages = Mathf.Clamp(m_DistanceStageCount, 2, 7);
            int widths = m_UseWidthFactor ? 2 : 1;

            int vtCells = stages * 2 * widths;
            int tCells  = stages * 2;
            int vCells  = 2 * widths;

            if (m_VtTrialsPerBlock % vtCells != 0)
                Debug.LogWarning(
                    $"[PpsTaskAsset] '{name}': VT trials ({m_VtTrialsPerBlock}) do not divide " +
                    $"evenly by {stages} distances x 2 speeds x {widths} width(s) = {vtCells} cells. " +
                    $"The design would be unbalanced. Use a multiple of {vtCells}.", this);

            if (m_TactileOnlyTrialsPerBlock % tCells != 0)
                Debug.LogWarning(
                    $"[PpsTaskAsset] '{name}': T trials ({m_TactileOnlyTrialsPerBlock}) do not " +
                    $"divide evenly by {stages} distances x 2 speeds = {tCells} cells. " +
                    $"Use a multiple of {tCells}.", this);

            if (m_VisualOnlyTrialsPerBlock % vCells != 0)
                Debug.LogWarning(
                    $"[PpsTaskAsset] '{name}': V trials ({m_VisualOnlyTrialsPerBlock}) do not " +
                    $"divide evenly by 2 speeds x {widths} width(s) = {vCells} cells. " +
                    $"Use a multiple of {vCells}.", this);

            if (m_DefaultShoulderWidthMeters <= 0f) m_DefaultShoulderWidthMeters = 0.40f;

            if (m_CrosshairDistance <= 0f) m_CrosshairDistance = 0.1f;
            if (m_CrosshairHeight < 0f) m_CrosshairHeight = 0f;

            if (m_WideOffsetMeters < 0f) m_WideOffsetMeters = 0f;

            if (m_ItiMinSeconds < 0f) m_ItiMinSeconds = 0f;
            if (m_ItiMaxSeconds < m_ItiMinSeconds) m_ItiMaxSeconds = m_ItiMinSeconds;
        }
    }
}