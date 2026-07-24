using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HitOrMiss.Pps
{
    /// <summary>
    /// Debug-only ruler for room verification.
    ///
    /// Draws ONE line along the DistanceLayout forward axis, starting at the
    /// layout origin, with a tick and label at every whole meter, and
    /// optionally a tick and label at each configured stage (D7 ... D1).
    ///
    /// By default the line is drawn at FOOT level so it can be compared
    /// directly against a tape measure laid on the floor. The stage marks are
    /// projected straight down onto that line, so a stage tick shows the
    /// horizontal distance to that stage, not its height.
    ///
    /// Everything is parented under the DistanceLayout, so the ruler sits in
    /// the same reference frame as the looming lights and follows the
    /// participant.
    ///
    /// Uses legacy TextMesh and LineRenderer, so no TMP font asset is needed.
    /// Remove or disable this component for data collection.
    /// </summary>
    public class DistanceMarkerLabels : MonoBehaviour
    {
        [Header("References")]

        [Tooltip("The DistanceLayout to measure along. If empty, searched on this GameObject.")]
        [SerializeField] private DistanceLayout m_Layout;

        [Header("Ruler height")]

        [Tooltip("Draw the ruler at floor level. Turn off to draw it at the height of the looming lights instead.")]
        [SerializeField] private bool m_PlaceAtFloorLevel = true;

        [Tooltip("Optional. Transform whose Y defines the floor, normally XR Origin. If empty, world Y = 0 is used.")]
        [SerializeField] private Transform m_FloorReference;

        [Tooltip("Extra vertical offset applied after the height mode above, in meters. A small positive value keeps the line off the floor surface.")]
        [SerializeField] private float m_HeightOffset = 0.01f;

        [Header("Ruler")]

        [Tooltip("Length of the ruler line in meters. If 0, it runs to the furthest stage rounded up to the next whole meter.")]
        [SerializeField] private float m_RulerLengthMeters = 0f;

        [SerializeField] private float m_LineWidth = 0.004f;

        [Header("Meter ticks")]

        [SerializeField] private bool m_ShowMeterTicks = true;

        [SerializeField] private Color m_MeterColor = Color.white;

        [Tooltip("Half-height of the meter tick crossbar, in meters.")]
        [SerializeField] private float m_MeterTickSize = 0.05f;

        [Header("Stage ticks (D7 ... D1)")]

        [Tooltip("Show a tick and label for each configured stage.")]
        [SerializeField] private bool m_ShowStageTicks = true;

        [SerializeField] private Color m_StageColor = Color.yellow;

        [Tooltip("Half-height of the stage tick crossbar, in meters.")]
        [SerializeField] private float m_StageTickSize = 0.10f;

        [Header("Reference distance")]

        [Tooltip("Add a second number to each stage label: the straight-line distance from the reference point below to the actual stage position, including its height. Useful when measuring from the body rather than along the floor.")]
        [SerializeField] private bool m_ShowReferenceDistance = false;

        [Tooltip("The point reference distances are measured from. Assign the ChestAnchor to measure from the body anchor.")]
        [SerializeField] private Transform m_ReferencePoint;

        [Header("Text")]

        [SerializeField] private float m_CharacterSize = 0.010f;

        [Tooltip("How far to the right of its tick each label sits, in meters.")]
        [SerializeField] private float m_LabelSideOffset = 0.03f;

        [Tooltip("Labels turn to face the headset every frame so they stay readable.")]
        [SerializeField] private bool m_BillboardToCamera = true;

        private readonly List<TextMesh> m_Labels = new List<TextMesh>();
        private Transform m_Root;
        private Transform m_CameraTransform;
        private Material m_LineMaterial;

        private IEnumerator Start()
        {
            if (m_Layout == null)
                m_Layout = GetComponent<DistanceLayout>();

            if (m_Layout == null)
            {
                Debug.LogError("[DistanceRuler] No DistanceLayout assigned or found.", this);
                yield break;
            }

            // Wait one frame so DistanceLayout.Awake has configured the stages.
            yield return null;

            Build();
        }

        private void LateUpdate()
        {
            if (!m_BillboardToCamera || m_Labels.Count == 0) return;

            if (m_CameraTransform == null && Camera.main != null)
                m_CameraTransform = Camera.main.transform;

            if (m_CameraTransform == null) return;

            foreach (TextMesh label in m_Labels)
            {
                if (label == null) continue;

                Vector3 toLabel = label.transform.position - m_CameraTransform.position;
                toLabel.y = 0f;

                if (toLabel.sqrMagnitude > 0.0001f)
                    label.transform.rotation = Quaternion.LookRotation(toLabel, Vector3.up);
            }
        }

        private void Build()
        {
            m_LineMaterial = new Material(Shader.Find("Sprites/Default"));

            GameObject rootObject = new GameObject("DistanceRuler");
            rootObject.transform.SetParent(m_Layout.transform, false);
            rootObject.transform.localPosition = Vector3.zero;
            rootObject.transform.localRotation = Quaternion.identity;
            m_Root = rootObject.transform;

            float height = RulerHeight() + m_HeightOffset;
            float length = RulerLength();

            DrawLine(
                "RulerLine",
                new Vector3(0f, height, 0f),
                new Vector3(0f, height, length),
                m_MeterColor
            );

            if (m_ShowMeterTicks)
            {
                for (int meter = 1; meter <= Mathf.FloorToInt(length + 0.001f); meter++)
                {
                    DrawTick(
                        "Meter_" + meter,
                        meter,
                        height,
                        m_MeterTickSize,
                        m_MeterColor,
                        meter + " m"
                    );
                }
            }

            if (m_ShowStageTicks)
            {
                AddStageTick("D7", m_Layout.D7, height);
                AddStageTick("D6", m_Layout.D6, height);
                AddStageTick("D5", m_Layout.D5, height);
                AddStageTick("D4", m_Layout.D4, height);
                AddStageTick("D3", m_Layout.D3, height);
                AddStageTick("D2", m_Layout.D2, height);
                AddStageTick("D1", m_Layout.D1, height);
            }

            Debug.Log(
                "[DistanceRuler] Built ruler of " + length.ToString("F2") +
                " m at local height " + height.ToString("F3") + " m " +
                (m_PlaceAtFloorLevel ? "(floor level)." : "(light level)."),
                this
            );
        }

        private void AddStageTick(string stageName, Transform stage, float height)
        {
            if (stage == null)
            {
                Debug.LogWarning("[DistanceRuler] Stage " + stageName + " is null, skipping.", this);
                return;
            }

            float z = stage.localPosition.z;

            string label = stageName + "  " + z.ToString("F3") + " m";

            if (m_ShowReferenceDistance && m_ReferencePoint != null)
            {
                float fromReference =
                    Vector3.Distance(m_ReferencePoint.position, stage.position);

                label += "   ref " + fromReference.ToString("F3") + " m";
            }

            DrawTick("Stage_" + stageName, z, height, m_StageTickSize, m_StageColor, label);
        }

        private void DrawTick(
            string name,
            float z,
            float height,
            float halfSize,
            Color color,
            string labelText
        )
        {
            DrawLine(
                name + "_Tick",
                new Vector3(0f, height - halfSize, z),
                new Vector3(0f, height + halfSize, z),
                color
            );

            GameObject labelObject = new GameObject(name + "_Label");
            labelObject.transform.SetParent(m_Root, false);
            labelObject.transform.localPosition =
                new Vector3(m_LabelSideOffset, height, z);
            labelObject.transform.localRotation = Quaternion.identity;

            TextMesh textMesh = labelObject.AddComponent<TextMesh>();
            textMesh.characterSize = m_CharacterSize;
            textMesh.fontSize = 64;
            textMesh.anchor = TextAnchor.MiddleLeft;
            textMesh.alignment = TextAlignment.Left;
            textMesh.color = color;
            textMesh.text = labelText;

            m_Labels.Add(textMesh);
        }

        private void DrawLine(string name, Vector3 localStart, Vector3 localEnd, Color color)
        {
            GameObject lineObject = new GameObject(name);
            lineObject.transform.SetParent(m_Root, false);
            lineObject.transform.localPosition = Vector3.zero;
            lineObject.transform.localRotation = Quaternion.identity;

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.SetPosition(0, localStart);
            line.SetPosition(1, localEnd);
            line.startWidth = m_LineWidth;
            line.endWidth = m_LineWidth;
            line.material = m_LineMaterial;
            line.startColor = color;
            line.endColor = color;
        }

        /// <summary>
        /// Local Y, in DistanceLayout space, that the ruler line sits at.
        /// </summary>
        private float RulerHeight()
        {
            if (!m_PlaceAtFloorLevel)
                return StageHeight();

            float floorWorldY = m_FloorReference != null
                ? m_FloorReference.position.y
                : 0f;

            Vector3 floorWorldPoint = new Vector3(
                m_Layout.transform.position.x,
                floorWorldY,
                m_Layout.transform.position.z
            );

            return m_Layout.transform.InverseTransformPoint(floorWorldPoint).y;
        }

        private float StageHeight()
        {
            foreach (Transform stage in Stages())
            {
                if (stage != null)
                    return stage.localPosition.y;
            }

            return 0f;
        }

        private float RulerLength()
        {
            if (m_RulerLengthMeters > 0f)
                return m_RulerLengthMeters;

            float furthest = 0f;

            foreach (Transform stage in Stages())
            {
                if (stage != null)
                    furthest = Mathf.Max(furthest, stage.localPosition.z);
            }

            return Mathf.Max(1f, Mathf.Ceil(furthest));
        }

        private Transform[] Stages()
        {
            return new[]
            {
                m_Layout.D1, m_Layout.D2, m_Layout.D3, m_Layout.D4,
                m_Layout.D5, m_Layout.D6, m_Layout.D7
            };
        }

        public void SetVisible(bool visible)
        {
            if (m_Root != null)
                m_Root.gameObject.SetActive(visible);
        }
    }
}