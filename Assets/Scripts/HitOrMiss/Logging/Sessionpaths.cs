using System.IO;
using UnityEngine;

namespace HitOrMiss
{
    /// <summary>
    /// SINGLE SOURCE OF TRUTH for where session data lives. Both the writer
    /// (EegMarkerEmitter, and through it TaskLogger) and the reader
    /// (HitMissNetworkServer's Sessions library) call this, so they can never
    /// disagree about the folder again.
    ///
    /// Editor / desktop: {project}/Logger — e.g.
    ///   C:\Users\VR_Users\Documents\GitHub\2026_PPS\Logger
    /// Standalone build (Quest): Application.persistentDataPath/Logs, because
    /// a headset cannot write into the PC's project folder.
    ///
    /// NOTE: the editor root sits inside a git repository. Participant data
    /// must NOT be committed — add "Logger/" to .gitignore.
    /// </summary>
    public static class SessionPaths
    {
        public static string Root
        {
            get
            {
#if UNITY_EDITOR || UNITY_STANDALONE
                return Path.Combine(Directory.GetCurrentDirectory(), "Logger");
#else
                return Path.Combine(Application.persistentDataPath, "Logs");
#endif
            }
        }
    }
}