using UnityEngine;

namespace ProjectR
{
    // Shadowless fill that lights the ceiling above a downward spot.
    // Follows the source light so flicker, dimming, colour changes and blackouts stay in sync.
    [RequireComponent(typeof(Light))]
    public sealed class FillLight : MonoBehaviour
    {
        [SerializeField] private Light source;
        [SerializeField, Range(0f, 1f)] private float intensityRatio = .3f;

        private Light fill;

        private void Awake() => fill = GetComponent<Light>();

        private void LateUpdate()
        {
            bool on = source != null && source.isActiveAndEnabled;
            fill.enabled = on;
            if (!on)
                return;

            fill.color = source.color;
            fill.intensity = source.intensity * intensityRatio;
        }
    }
}
