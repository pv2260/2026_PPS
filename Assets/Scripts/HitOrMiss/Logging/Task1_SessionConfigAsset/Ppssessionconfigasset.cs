using UnityEngine;

namespace HitOrMiss.Pps
{
    /// <summary>
    /// The single per-session entry point for a PPS (Task 1) run. One asset per
    /// participant/session. References the shared PpsTaskAsset protocol and holds
    /// the identity, subject data, and the use-panel switch.
    ///
    /// Authority map (identical to the Task 2 SessionConfig):
    ///   - protocol  -> the referenced PpsTaskAsset (shared, reusable).
    ///   - identity  -> ParticipantId / SessionNumber here (panel-off runs).
    ///   - subject   -> SubjectInfo here (panel-off runs).
    ///   - the switch (UseClinicalPanel) decides whether this asset drives the
    ///     run or the clinical panel supplies identity/subject live instead.
    ///
    /// SessionSubjectInfo lives in the HitOrMiss namespace and is reused here.
    /// </summary>
    [CreateAssetMenu(fileName = "PpsSessionConfig", menuName = "Parkinson/HitOrMiss/PPS Session Config")]
    public class PpsSessionConfigAsset : ScriptableObject
    {
        [Header("Protocol (shared, reusable)")]
        [Tooltip("The PPS protocol to run. Study-wide, not per-participant. Many PpsSessionConfig " +
                 "assets can point at the same protocol asset.")]
        [SerializeField] PpsTaskAsset m_TaskAsset;

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

        public PpsTaskAsset TaskAsset => m_TaskAsset;
        public bool UseClinicalPanel => m_UseClinicalPanel;
        public string ParticipantId => m_ParticipantId;
        public int SessionNumber => m_SessionNumber;

        /// <summary>Session label for the marker emitter, e.g. "S1".</summary>
        public string SessionLabel => $"S{m_SessionNumber}";

        /// <summary>
        /// Assembles the full SessionMetadata for a panel-free run: identity and
        /// subject from this asset, protocol snapshot from the referenced PPS
        /// asset (for setup.json). Nothing here is read back to drive the run;
        /// the protocol authority remains the PpsTaskAsset.
        /// </summary>
        public SessionMetadata BuildMetadata()
        {
            var md = SessionMetadata.CreateDefault(m_ParticipantId);

            md.participantId = m_ParticipantId;
            md.sessionNumber = m_SessionNumber;
            md.sessionId = SessionLabel;

            m_SubjectInfo.ApplyTo(ref md);

            if (m_TaskAsset != null)
                md.PopulateFromPpsTaskAsset(m_TaskAsset);

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
                Debug.LogWarning($"[PpsSessionConfigAsset] '{name}' has no PpsTaskAsset assigned.", this);
        }
    }
}