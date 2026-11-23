using UnityEngine;

namespace ProjectR
{
    public sealed class BedroomDoorScare : MonoBehaviour
    {
        private enum Phase { Watching, Closing, Finished }

        [SerializeField] private DoorController door;
        [SerializeField] private HorrorEventManager progression;
        [SerializeField] private AudioSource slamSound;
        [SerializeField, Min(1f)] private float startingAngle = 24f;
        [SerializeField, Min(0f)] private float slamDistance = 4f;
        [SerializeField, Min(1f)] private float closingSpeed = 330f;

        private Phase phase;

        public bool HasSlammed => phase == Phase.Finished;

        private void Start() => door.HoldAjar(startingAngle);

        private void Update()
        {
            if (HasSlammed || Time.deltaTime <= 0f)
                return;

            if (phase == Phase.Closing)
            {
                if (!door.IsClosed)
                    return;

                slamSound.Play();
                phase = Phase.Finished;
                return;
            }

            CharacterController player = progression.PlayerBody;
            Vector3 approach = player.bounds.center - door.transform.position;
            approach.y = 0f;
            if (Vector3.Dot(approach, door.transform.forward) >= 0f || approach.sqrMagnitude > slamDistance * slamDistance)
                return;

            phase = Phase.Closing;
            door.TryClose(player, closingSpeed);
        }
    }
}
