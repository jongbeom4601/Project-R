using System.Collections;
using UnityEngine;

namespace ProjectR
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class LivingRoomScare : MonoBehaviour
    {
        [SerializeField] private Light lightToDim;
        [SerializeField, Range(0f, 1f)] private float dimIntensity = .4f;
        [SerializeField, Min(0f)] private float dimDuration;
        [SerializeField] private AudioSource distantCue;
        [SerializeField] private AudioSource nearCue;
        [SerializeField] private AudioClip openingSound;
        [SerializeField] private AudioClip followUpSound;
        [SerializeField, Range(0f, 1f)] private float openingVolume = .45f;
        [SerializeField, Range(0f, 1f)] private float followUpVolume = .65f;
        [SerializeField, Min(0)] private int followUpCount;

        private BoxCollider volume;
        private bool played;

        private void Awake() => volume = GetComponent<BoxCollider>();

        private void OnTriggerStay(Collider other)
        {
            if (played || !other.TryGetComponent(out PlayerController _) || !volume.bounds.Contains(other.bounds.center))
                return;

            played = true;
            StartCoroutine(Play());
        }

        private IEnumerator Play()
        {
            distantCue.PlayCue(openingSound, openingVolume);
            if (dimDuration > 0f)
            {
                float original = lightToDim.intensity;
                lightToDim.intensity = original * dimIntensity;
                yield return new WaitForSeconds(dimDuration);
                lightToDim.intensity = original;
            }

            for (int i = 0; i < followUpCount; i++)
            {
                yield return new WaitForSeconds(.37f);
                nearCue.PlayCue(followUpSound, followUpVolume);
            }

            if (followUpCount > 0)
            {
                yield return new WaitForSeconds(followUpSound != null ? followUpSound.length : 0f);
                nearCue.Stop();
            }
        }
    }
}
