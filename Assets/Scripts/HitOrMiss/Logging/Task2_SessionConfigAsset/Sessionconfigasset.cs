using UnityEngine;

namespace HitOrMiss
{
    /// <summary>
    /// The single per-session entry point for a Hit-or-Miss run. One asset per
    /// participant/session. It references the shared protocol asset and holds
    /// the identity, subject data, and the use-panel switch.
    ///
    /// Authority map:
    ///   - protocol  -> the referenced TrajectoryTaskAsset (shared, reusable).
    ///   - identity  -> ParticipantId / SessionNumber here (panel-off runs).
    ///   - subject   -> SubjectInfo here (panel-off runs).
    ///   - the switch (UseClinicalPanel) decides whether this asset drives the
    ///     run, or the clinical panel supplies identity/subject live instead.
    ///
    /// The controller reads this, assembles a SessionMetadata via BuildMetadata,
    /// and pushes the identity into the EEG marker emitter.
    /// </summary>
    [CreateAssetMenu(fileName = "SessionConfig", menuName = "Parkinson/HitOrMiss/Session Config")]
    public class SessionConfigAsset : ScriptableObject
    {
        [Header("Protocol (shared, reusable)")]
        [Tooltip("The task protocol to run. This stays study-wide and is not per-participant. " +
                 "Many SessionConfig assets can point at the same protocol asset.")]
        [SerializeField] TrajectoryTaskAsset m_TaskAsset;

        [Header("Mode")]
        [Tooltip("If true, the clinical panel supplies identity and subject data live, and the " +
                 "Identity / Subject fields below are ignored. If false, this asset drives a " +
                 "panel-free run using the fields below.")]
        [SerializeField] bool m_UseClinicalPanel = false;

        [Header("Identity (used when Use Clinical Panel is false)")]
        [Tooltip("Participant id. Drives the EEG marker stream and the CSV / setup.json filenames.")]
        [SerializeField] string m_ParticipantId = "P000";

        [Tooltip("Session number (1, 2, ...).")]
        [SerializeField, Min(1)] int m_SessionNumber = 1;

        [Header("Subject (used when Use Clinical Panel is false)")]
        [SerializeField] SessionSubjectInfo m_SubjectInfo = new SessionSubjectInfo
        {
            shoulderWidthCm = 42f,
            subjectGroup = "healthy",
            dominantHand = DominantHand.Unspecified,
            sessionType = SessionType.HealthySession1,
            dbsStatus = DbsStatus.Na,
            language = "english",
        };

        public TrajectoryTaskAsset TaskAsset => m_TaskAsset;
        public bool UseClinicalPanel => m_UseClinicalPanel;
        public string ParticipantId => m_ParticipantId;
        public int SessionNumber => m_SessionNumber;

        /// <summary>
        /// Session label for the marker emitter, e.g. "S1". The controller uses
        /// this when pushing identity into the emitter.
        /// </summary>
        public string SessionLabel => $"S{m_SessionNumber}";

        /// <summary>
        /// Assembles the full SessionMetadata for a panel-free run: identity and
        /// subject from this asset, protocol snapshot from the referenced task
        /// asset (for setup.json). Nothing here is read back to drive the run;
        /// the protocol authority remains the task asset.
        /// </summary>
        public SessionMetadata BuildMetadata()
        {
            var md = SessionMetadata.CreateDefault(m_ParticipantId);

            md.participantId = m_ParticipantId;
            md.sessionNumber = m_SessionNumber;
            md.sessionId = SessionLabel;

            m_SubjectInfo.ApplyTo(ref md);

            if (m_TaskAsset != null)
                md.PopulateFromTaskAsset(m_TaskAsset);

            return md;
        }

        [ContextMenu("Subject info: fill with defaults")]
        void FillSubjectInfoWithDefaults()
        {
            m_SubjectInfo = SessionSubjectInfo.CreateDefault();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        void OnValidate()
        {
            if (m_SessionNumber < 1) m_SessionNumber = 1;

            if (m_TaskAsset == null)
                Debug.LogWarning($"[SessionConfigAsset] '{name}' has no protocol asset assigned.", this);
        }
    }

    /// <summary>
    /// The subject-and-condition fields entered for a panel-free run. Holds ONLY
    /// per-participant data. It has no participant id, session, or protocol
    /// fields by design, so those each have exactly one home elsewhere
    /// (the identity fields above, and the TrajectoryTaskAsset).
    /// </summary>
    [System.Serializable]
    public struct SessionSubjectInfo
    {
        [Header("Subject")]
        public int ageYears;
        public DominantHand dominantHand;
        public float heightCm;

        [Tooltip("Participant shoulder width in cm. This is the ONLY subject field " +
                 "that changes the task itself: it scales the offset bands via " +
                 "(shoulderWidthCm / TrajectoryTaskAsset.ReferenceShoulderWidthCm). " +
                 "Everything else here is recorded into setup.json but does not alter the run.")]
        public float shoulderWidthCm;

        [Tooltip("\"patient\" or \"healthy\".")]
        public string subjectGroup;
        public bool hasDbs;

        [Header("Session condition")]
        public SessionType sessionType;
        public DbsStatus dbsStatus;

        [Tooltip("\"english\" or \"french\".")]
        public string language;

        [Header("Equipment")]
        public bool eegEnabled;
        public bool emgEnabled;
        public bool heartRateBandEnabled;
        public bool eyeTrackingEnabled;

        [Header("Clinician")]
        public string clinicianInitials;
        [TextArea] public string clinicianNotes;

        public static SessionSubjectInfo CreateDefault()
        {
            return new SessionSubjectInfo
            {
                ageYears = 0,
                dominantHand = DominantHand.Unspecified,
                heightCm = 0f,
                shoulderWidthCm = 42f,
                subjectGroup = "healthy",
                hasDbs = false,

                sessionType = SessionType.HealthySession1,
                dbsStatus = DbsStatus.Na,
                language = "english",

                eegEnabled = false,
                emgEnabled = false,
                heartRateBandEnabled = false,
                eyeTrackingEnabled = false,

                clinicianInitials = string.Empty,
                clinicianNotes = string.Empty,
            };
        }

        /// <summary>
        /// Copies the subject fields onto an existing SessionMetadata. Leaves
        /// participant id, session, and protocol fields untouched.
        /// </summary>
        public void ApplyTo(ref SessionMetadata md)
        {
            md.ageYears = ageYears;
            md.dominantHand = dominantHand;
            md.heightCm = heightCm;
            md.shoulderWidthCm = shoulderWidthCm;
            md.subjectGroup = subjectGroup;
            md.hasDbs = hasDbs;

            md.sessionType = sessionType;
            md.dbsStatus = dbsStatus;
            md.language = language;

            md.eegEnabled = eegEnabled;
            md.emgEnabled = emgEnabled;
            md.heartRateBandEnabled = heartRateBandEnabled;
            md.eyeTrackingEnabled = eyeTrackingEnabled;

            md.clinicianInitials = clinicianInitials;
            md.clinicianNotes = clinicianNotes;
        }
    }
}