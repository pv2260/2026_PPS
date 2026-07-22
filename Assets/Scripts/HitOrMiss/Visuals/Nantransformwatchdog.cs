using UnityEngine;

namespace HitOrMiss
{
    /// <summary>
    /// DIAGNOSTIC ONLY — remove before participant sessions.
    ///
    /// Scans every active transform each frame. The first time any position,
    /// scale, or rotation component is NaN/Infinity, logs ONE red line naming
    /// the object (full hierarchy path) with its offending values, and pauses
    /// the editor at that exact frame so the scene can be inspected in the
    /// state that produced the "Invalid AABB / transform is corrupt" flood.
    ///
    /// Usage: create an empty GameObject in Task2Scene, add this component,
    /// press Play, run a block, wait for the pause. The culprit is named in
    /// the Console. Delete the GameObject when done.
    /// </summary>
    public class NanTransformWatchdog : MonoBehaviour
    {
        [Tooltip("Pause the editor on first detection so the corrupt state can be inspected.")]
        [SerializeField] bool m_BreakOnDetect = true;

        [Tooltip("Keep reporting new offenders after the first (one line each). Off = first offender only.")]
        [SerializeField] bool m_ReportSubsequent = true;

        int m_Reported;

        void LateUpdate()
        {
            if (m_Reported > 0 && !m_ReportSubsequent) return;

            // GetRootGameObjects allocates; fine for a diagnostic.
            var scene = gameObject.scene;
            var roots = scene.GetRootGameObjects();
            foreach (var root in roots)
                Scan(root.transform);
        }

        void Scan(Transform t)
        {
            Vector3 p = t.position;
            Vector3 s = t.lossyScale;
            Quaternion r = t.rotation;

            bool bad =
                !Finite(p.x) || !Finite(p.y) || !Finite(p.z) ||
                !Finite(s.x) || !Finite(s.y) || !Finite(s.z) ||
                !Finite(r.x) || !Finite(r.y) || !Finite(r.z) || !Finite(r.w);

            if (bad)
            {
                m_Reported++;
                Debug.LogError(
                    $"[NanTransformWatchdog] CORRUPT TRANSFORM #{m_Reported} at frame {Time.frameCount}, t={Time.time:F3}s\n" +
                    $"  object: {Path(t)}\n" +
                    $"  position={p}  lossyScale={s}  rotation=({r.x}, {r.y}, {r.z}, {r.w})",
                    t.gameObject);

                if (m_BreakOnDetect)
                    Debug.Break();

                // Don't descend into a corrupt branch — children inherit the
                // corruption and would just add noise.
                return;
            }

            for (int i = 0; i < t.childCount; i++)
                Scan(t.GetChild(i));
        }

        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        static string Path(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }
    }
}