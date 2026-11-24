using UnityEngine;

namespace ProjectR
{
    // Shows hidden objects the first time the player tries a door that has locked behind them.
    // Only the listed objects change; the room and the route stay as they are.
    public sealed class RevealOnLockedDoor : MonoBehaviour
    {
        [SerializeField, Tooltip("Door the player tries from inside after it has locked behind them.")]
        private DoorController door;
        [SerializeField, Tooltip("Objects that appear. Keep them switched off in the scene.")]
        private GameObject[] revealed = System.Array.Empty<GameObject>();
        [SerializeField, Tooltip("Reveal only once the door has locked behind the player (they are inside).")]
        private bool onlyAfterPassage = true;

        private bool done;

        private void OnEnable()
        {
            if (door != null)
                door.LockedTried += OnLockedTried;
        }

        private void OnDisable()
        {
            if (door != null)
                door.LockedTried -= OnLockedTried;
        }

        private void OnLockedTried(DoorController tried)
        {
            if (done || (onlyAfterPassage && !tried.IsPassageLocked))
                return;

            done = true;
            foreach (GameObject target in revealed)
                if (target != null)
                    target.SetActive(true);
        }
    }
}
