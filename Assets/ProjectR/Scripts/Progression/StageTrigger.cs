using UnityEngine;

namespace ProjectR
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class StageTrigger : MonoBehaviour
    {
        public enum Milestone
        {
            LivingRoom, NormalCorridor, ExtendedCorridor = 4,
            StrangeRoom, RepeatExit, DarkCorridorMidpoint, DarkCorridorEnd, Exterior
        }

        [SerializeField] private HorrorEventManager progression;
        [SerializeField] private Milestone milestone;

        private BoxCollider volume;

        private void Awake() => volume = GetComponent<BoxCollider>();

        private void OnTriggerStay(Collider other)
        {
            // Count the player's center in the zone, not the leading edge of the capsule.
            if (other == progression.PlayerBody && volume.bounds.Contains(other.bounds.center))
                progression.Reach(milestone);
        }
    }
}
