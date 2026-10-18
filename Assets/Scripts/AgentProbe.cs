using UnityEngine;

namespace JPEGXL
{
    /// <summary>Fixture for docs/rider-mcp.md in UnityFramework: a MonoBehaviour that Rider's MCP tools are tested against.</summary>
    public class AgentProbe : MonoBehaviour
    {
        /// <summary>Public field read by <see cref="AgentProbeUser"/>.</summary>
        public int probeCount = 3;

        [SerializeField] private float probeScale = 1.5f;

        /// <summary>Returns a value derived from both fields.</summary>
        public float ComputeProbeValue(float factor)
        {
            return probeCount * probeScale * factor;
        }

        private void Start()
        {
            Debug.Log($"AgentProbe at {transform.position}: {ComputeProbeValue(2f)}");
        }
    }
}
