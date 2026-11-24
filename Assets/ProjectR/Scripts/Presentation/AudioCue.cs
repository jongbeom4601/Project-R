using UnityEngine;

namespace ProjectR
{
    public static class AudioCue
    {
        // Plays a one-shot when both the source and the clip are set. Cues stay silent until a clip is assigned.
        public static void PlayCue(this AudioSource source, AudioClip clip, float volume)
        {
            if (source != null && clip != null)
                source.PlayOneShot(clip, volume);
        }

        // A reusable 3D source with the same settings AudioSource.PlayClipAtPoint uses, so a sound that repeats
        // from a fixed spot does not create and destroy an object each time. Callers apply the effects volume themselves.
        public static AudioSource CreatePointSource(Transform parent, string name)
        {
            var source = new GameObject(name).AddComponent<AudioSource>();
            source.transform.SetParent(parent, false);
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            GameSettings.MarkDriven(source);
            return source;
        }

        public static void PlayAt(this AudioSource source, AudioClip clip, Vector3 position, float volume)
        {
            if (source == null || clip == null)
                return;
            source.transform.position = position;
            source.PlayOneShot(clip, volume);
        }
    }
}
