using UnityEngine;

namespace ProjectR
{
    // Plays a step each time the player covers one stride on the ground.
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerFootsteps : MonoBehaviour
    {
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip[] walkSteps = System.Array.Empty<AudioClip>();
        [SerializeField, Min(.1f), Tooltip("Metres between steps while walking.")] private float walkStride = 1.35f;
        [SerializeField, Range(0f, 1f)] private float walkVolume = .45f;
        [SerializeField] private Vector2 pitchRange = new(.93f, 1.07f);

        private const float MinMovingSpeed = .3f;
        private const float MaxStepPerFrame = 1f; // larger jumps are teleports, not walking

        private CharacterController body;
        private Vector3 lastPosition;
        private float travelled;
        private int lastClip = -1;

        private void Awake() => body = GetComponent<CharacterController>();

        private void OnEnable()
        {
            lastPosition = transform.position;
            travelled = 0f;
        }

        private void LateUpdate()
        {
            Vector3 delta = transform.position - lastPosition;
            lastPosition = transform.position;
            delta.y = 0f;
            float distance = delta.magnitude;
            if (Time.deltaTime <= 0f || distance > MaxStepPerFrame || !body.isGrounded)
                return;

            if (distance / Time.deltaTime < MinMovingSpeed)
            {
                // Standing still: the first step after starting comes quickly.
                travelled = walkStride * .6f;
                return;
            }

            travelled += distance;
            if (travelled < walkStride)
                return;

            travelled -= walkStride;
            PlayStep();
        }

        private void PlayStep()
        {
            if (source == null || walkSteps.Length == 0)
                return;

            // Never repeat the same clip twice in a row.
            int index = Random.Range(0, walkSteps.Length);
            if (walkSteps.Length > 1 && index == lastClip)
                index = (index + 1) % walkSteps.Length;
            lastClip = index;

            if (walkSteps[index] == null)
                return;
            source.pitch = Random.Range(pitchRange.x, pitchRange.y);
            source.PlayOneShot(walkSteps[index], walkVolume * Random.Range(.85f, 1f));
        }
    }
}
