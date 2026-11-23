using UnityEngine;

namespace ProjectR
{
    // One shared set of door sounds, so every door is tuned from a single asset.
    [CreateAssetMenu(menuName = "Project R/Door Sounds")]
    public sealed class DoorSounds : ScriptableObject
    {
        [SerializeField] private AudioClip open;
        [SerializeField, Tooltip("Used when a door opens slower than Slow Below Speed, such as the final front door.")]
        private AudioClip slowOpen;
        [SerializeField] private AudioClip close;
        [SerializeField, Tooltip("Rattle for locked doors that have no clip of their own.")]
        private AudioClip locked;
        [SerializeField, Range(0f, 1f)] private float lockedVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float openVolume = .7f;
        [SerializeField, Range(0f, 1f)] private float closeVolume = .8f;
        [SerializeField, Min(1f)] private float slowBelowSpeed = 100f;
        [SerializeField] private Vector2 pitchRange = new(.95f, 1.05f);

        internal void PlayOpen(AudioSource source, float degreesPerSecond)
        {
            AudioClip clip = degreesPerSecond < slowBelowSpeed && slowOpen != null ? slowOpen : open;
            Play(source, clip, openVolume);
        }

        internal void PlayClose(AudioSource source) => Play(source, close, closeVolume);

        // Unpitched so the jolt in the clip lines up with the door's jolt.
        internal void PlayLocked(AudioSource source)
        {
            if (source == null || locked == null)
                return;
            source.pitch = 1f;
            source.PlayOneShot(locked, lockedVolume);
        }

        private void Play(AudioSource source, AudioClip clip, float volume)
        {
            if (source == null || clip == null)
                return;

            source.pitch = Random.Range(pitchRange.x, pitchRange.y);
            source.PlayOneShot(clip, volume);
        }
    }
}
