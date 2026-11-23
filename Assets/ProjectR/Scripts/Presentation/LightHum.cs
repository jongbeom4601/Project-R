using UnityEngine;

namespace ProjectR
{
    // Loops a fixture hum while its light is on, and pops once when the light cuts out.
    [RequireComponent(typeof(AudioSource))]
    public sealed class LightHum : MonoBehaviour
    {
        [SerializeField] private Light source;
        [SerializeField] private AudioClip offSound;
        [SerializeField, Range(0f, 1f)] private float offVolume = .6f;

        private AudioSource hum;
        private bool wasOn;

        private bool IsOn => source != null && source.isActiveAndEnabled;

        private void Awake() => hum = GetComponent<AudioSource>();

        private void OnEnable()
        {
            wasOn = IsOn;
            SyncHum();
        }

        private void LateUpdate()
        {
            bool on = IsOn;
            if (wasOn && !on && offSound != null)
                AudioSource.PlayClipAtPoint(offSound, transform.position, offVolume * GameSettings.EffectsVolume);

            wasOn = on;
            SyncHum();
        }

        private void SyncHum()
        {
            if (hum.clip == null)
                return;

            if (wasOn && !hum.isPlaying)
                hum.Play();
            else if (!wasOn && hum.isPlaying)
                hum.Stop();
        }
    }
}
