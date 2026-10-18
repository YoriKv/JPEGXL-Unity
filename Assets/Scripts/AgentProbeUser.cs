using UnityEngine;

namespace JPEGXL
{
    /// <summary>Fixture for docs/rider-mcp.md in UnityFramework: uses <see cref="AgentProbe"/>.</summary>
    public class AgentProbeUser : MonoBehaviour
    {
        [SerializeField] private AgentProbe probe;

        private void Update()
        {
            if (probe == null)
                return;
            var value = probe.ComputeProbeValue(Time.deltaTime);
            if (probe.probeCount > 0 && value > 100f)
                Debug.LogWarning("AgentProbe value is large");
        }
    }
}
