using UnityEngine;

namespace ProjectR
{
    public sealed class DoorController : MonoBehaviour
    {
        private enum State { Closed, Opening, Open, Closing }

        [SerializeField] private Transform pivot;
        [SerializeField] private BoxCollider panel;
        [SerializeField] private BoxCollider interactionTarget;
        [SerializeField] private bool startsOpen;
        [SerializeField] private bool opensAwayFromPlayer;
        [SerializeField, Tooltip("Lock this door after the player passes through. Assign a return barrier covering the opening.")]
        private bool lockAfterPassage;
        [SerializeField] private bool lockedFromBothSides;
        [SerializeField] private bool lockedFromExitSide;
        [SerializeField, Min(0f)] private float passageClearanceOverride;
        [SerializeField] private BoxCollider returnBarrier;
        [SerializeField] private Transform rattleVisual;
        [SerializeField] private AudioSource lockedSound;
        [SerializeField, Tooltip("Plays open and close sounds. Leave empty to use the Locked Sound source.")]
        private AudioSource doorAudio;
        [SerializeField] private DoorSounds sounds;
        [SerializeField] private HorrorEventManager progression;
        [SerializeField] private int unlockStage;
        [SerializeField] private bool startsStage;
        [SerializeField, Range(1f, 120f)] private float openAngle = 90f;
        [SerializeField, Min(1f)] private float degreesPerSecond = 150f;
        [SerializeField, Min(0f)] private float openingSpeedOverride;
        [Header("Locked rattle")]
        [SerializeField, Range(0f, 5f), Tooltip("How far the panel jolts against the latch, in degrees.")]
        private float rattleAngle = 2.6f;
        [SerializeField, Tooltip("Seconds from the key press to each jolt. Match the hits in the locked sound clip.")]
        private float[] rattleJolts = { 0f };

        private State state;
        private float angle;
        private float openingAngle;
        private Quaternion closedRotation;
        private CharacterController player;
        private float closingSpeed;
        private float panelSweepRadius;
        private int passageDirection;
        private bool passageLocked;
        private Vector3 rattleRestPosition;
        private float rattleTime;
        private float rattleDuration;
        private float rattleSign = 1f;

        private const float JoltAttack = .035f;   // seconds to reach the latch
        private const float JoltDecay = .07f;     // seconds to settle back
        // Scripted closes faster than this are slams; their scripts play the slam instead of the latch.
        private const float SlamSpeedFactor = 1.5f;

        private AudioSource DoorAudio => doorAudio != null ? doorAudio : lockedSound;
        private float OpeningSpeed => openingSpeedOverride > 0f ? openingSpeedOverride : degreesPerSecond;

        public bool IsClosed => state == State.Closed;
        public bool HasOpened { get; private set; }
        public float Openness => Mathf.Abs(angle) / openAngle;
        public bool IsPassageLocked => passageLocked;
        // Raised when the player presses E on this door while it is locked for them.
        public event System.Action<DoorController> LockedTried;
        internal bool PassageAvailable { private get; set; } = true;
        public bool CanInteractFrom(CharacterController interactor) =>
            IsLockedFor(interactor) || state == State.Open || (state == State.Closed && CanOpen());

        public bool IsLockedFor(CharacterController interactor) => !PassageAvailable || passageLocked || lockedFromBothSides ||
            (progression != null && progression.IsWaitingForUnlockingDoor(this)) ||
            (lockedFromExitSide && state == State.Closed && SideOfDoor(interactor) >= 0f);

        private void Awake()
        {
            closedRotation = pivot.localRotation;
            openingAngle = openAngle;
            panelSweepRadius = MeasurePanelSweep();
            if (rattleVisual != null)
                rattleRestPosition = rattleVisual.localPosition;

            if (startsOpen)
            {
                angle = openAngle;
                pivot.localRotation = closedRotation * Quaternion.Euler(0f, angle, 0f);
                state = State.Open;
                HasOpened = true;
            }
        }

        private void Start()
        {
            if (!startsOpen)
                return;

            var startingPlayer = progression != null ? progression.PlayerBody : FindFirstObjectByType<PlayerController>()?.GetComponent<CharacterController>();
            if (startingPlayer != null)
                TrackPassage(startingPlayer);
        }

        public bool IsInteractionTarget(Collider candidate) => candidate == interactionTarget;

        public Vector3 HandlePosition(Vector3 viewerPosition)
        {
            Vector3 handle = panel.center + new Vector3(panel.size.x * .35f, -panel.size.y * .015f, 0f);
            float facing = Mathf.Sign(panel.transform.InverseTransformPoint(viewerPosition).z - panel.center.z);
            handle.z += facing * (panel.size.z * .5f + .04f);
            return panel.transform.TransformPoint(handle);
        }

        private bool CanOpen() => progression == null || progression.CanOpen(unlockStage);

        public void TryToggle(CharacterController interactor)
        {
            if (IsLockedFor(interactor))
            {
                Rattle(interactor);
                if (progression != null)
                    progression.OnLockedDoorInteracted(this);
                LockedTried?.Invoke(this);
                return;
            }

            if (state == State.Closed)
            {
                if (!CanOpen())
                    return;

                TrackPassage(interactor);
                openingAngle = OpeningAngleFor(passageDirection);
                state = State.Opening;
                if (sounds != null)
                    sounds.PlayOpen(DoorAudio, OpeningSpeed);
                if (startsStage && progression != null)
                    progression.BeginDoorStage(unlockStage);
            }
            else
            {
                TryClose(interactor);
            }
        }

        public void TryClose(CharacterController interactor, float speed = 0f)
        {
            if (state != State.Open)
                return;

            state = State.Closing;
            closingSpeed = speed > 0f ? speed : degreesPerSecond;
            player = interactor;
        }

        internal void HoldAjar(float degrees)
        {
            if (state != State.Closed)
                return;

            angle = Mathf.Sign(OpeningAngleFor(1)) * Mathf.Clamp(degrees, 1f, openAngle);
            pivot.localRotation = closedRotation * Quaternion.Euler(0f, angle, 0f);
            state = State.Open;
        }

        private void Update()
        {
            UpdateRattle();
            if (Time.deltaTime > 0f && state != State.Closed)
            {
                float clearance = passageClearanceOverride > 0f ? passageClearanceOverride : panelSweepRadius + .05f;
                if (state == State.Open && HasClearedDoorway(clearance))
                    TryClose(player);
            }

            if (state != State.Opening && state != State.Closing)
                return;

            float target = state == State.Opening ? openingAngle : 0f;
            float speed = state == State.Closing ? closingSpeed : OpeningSpeed;
            float end = Mathf.MoveTowards(angle, target, speed * Time.deltaTime);
            // Check intermediate poses too, so a slow frame cannot sweep through the player.
            while (angle != end)
            {
                float next = Mathf.MoveTowards(angle, end, 3f);
                Quaternion nextRotation = closedRotation * Quaternion.Euler(0f, next, 0f);
                if (WouldHitPlayer(nextRotation))
                    return;

                angle = next;
                pivot.localRotation = nextRotation;
            }

            if (angle != target)
                return;

            if (state == State.Closing && sounds != null && closingSpeed <= degreesPerSecond * SlamSpeedFactor)
                sounds.PlayClose(DoorAudio);

            state = state == State.Opening ? State.Open : State.Closed;
            HasOpened |= state == State.Open;
            if (state == State.Closed)
                LockIfPassedThrough();
        }

        // A one-way door locks only once it has shut with the player on the far side.
        // While it stands open the player may step in and out freely, and closing it from the near side keeps it usable.
        private void LockIfPassedThrough()
        {
            if (!lockAfterPassage || passageLocked || returnBarrier == null || !HasClearedDoorway(returnBarrier.bounds))
                return;
            passageLocked = true;
            returnBarrier.isTrigger = false;
        }

        private void TrackPassage(CharacterController interactor)
        {
            player = interactor;
            passageDirection = SideOfDoor(player) < 0f ? 1 : -1;
        }

        private float SideOfDoor(CharacterController body) => Vector3.Dot(body.bounds.center - pivot.position, transform.forward);

        private float OpeningAngleFor(int direction) => opensAwayFromPlayer && direction > 0 ? -openAngle : openAngle;

        private void Rattle(CharacterController interactor)
        {
            if (rattleTime > 0f)
                return;

            // The panel jumps toward the side it would open to, then the latch stops it.
            int direction = interactor != null && SideOfDoor(interactor) >= 0f ? -1 : 1;
            rattleSign = Mathf.Sign(OpeningAngleFor(direction));

            float lastJolt = 0f;
            foreach (float jolt in rattleJolts)
                lastJolt = Mathf.Max(lastJolt, jolt);
            rattleDuration = lastJolt + JoltAttack + JoltDecay * 4f;
            rattleTime = rattleDuration;

            // Sound and the first jolt start on the same frame as the key press.
            if (lockedSound != null && lockedSound.clip != null)
            {
                lockedSound.pitch = 1f;
                lockedSound.Play();
            }
            else if (sounds != null)
            {
                sounds.PlayLocked(DoorAudio); // doors without their own rattle use the shared one
            }
            ApplyRattle(0f);
        }

        private void UpdateRattle()
        {
            if (rattleTime <= 0f)
                return;

            rattleTime = Mathf.Max(0f, rattleTime - Time.deltaTime);
            ApplyRattle(rattleDuration - rattleTime);
            if (rattleTime <= 0f)
                ResetRattlePose();
        }

        // Each jolt is one knock: the panel snaps out against the latch and eases back, without wobbling.
        private void ApplyRattle(float elapsed)
        {
            float shake = 0f;
            foreach (float jolt in rattleJolts)
            {
                float t = elapsed - jolt;
                if (t < 0f)
                    continue;
                shake += t < JoltAttack
                    ? Mathf.Lerp(.4f, 1f, t / JoltAttack) // already moving on the key-press frame
                    : Mathf.Exp(-(t - JoltAttack) / JoltDecay);
            }
            shake = Mathf.Clamp01(shake);

            if (state == State.Closed)
                pivot.localRotation = closedRotation * Quaternion.Euler(0f, rattleSign * shake * rattleAngle, 0f);
            if (rattleVisual != null)
                rattleVisual.localPosition = rattleRestPosition + Vector3.forward * (shake * .01f);
        }

        private void ResetRattlePose()
        {
            if (state == State.Closed)
                pivot.localRotation = closedRotation * Quaternion.Euler(0f, angle, 0f);
            if (rattleVisual != null)
                rattleVisual.localPosition = rattleRestPosition;
        }

        private void OnDisable()
        {
            if (rattleTime > 0f)
                ResetRattlePose();
            rattleTime = 0f;
        }

        private bool HasClearedDoorway(Bounds barrier)
        {
            float farEdge = passageDirection * Vector3.Dot(barrier.center - pivot.position, transform.forward) +
                ExtentAlongDoorNormal(barrier);
            return HasClearedDoorway(farEdge + .02f);
        }

        private bool HasClearedDoorway(float clearance)
        {
            if (passageDirection == 0 || player == null || !player.enabled || !player.gameObject.activeInHierarchy)
                return false;

            Bounds body = player.bounds;
            Vector3 normal = transform.forward;
            float passedDistance = passageDirection * Vector3.Dot(body.center - pivot.position, normal);
            return passedDistance > ExtentAlongDoorNormal(body) + clearance;
        }

        private float ExtentAlongDoorNormal(Bounds bounds)
        {
            Vector3 normal = transform.forward;
            return Mathf.Abs(normal.x) * bounds.extents.x + Mathf.Abs(normal.y) * bounds.extents.y +
                Mathf.Abs(normal.z) * bounds.extents.z;
        }

        private float MeasurePanelSweep()
        {
            // The outer edge gives the maximum reach at any point of the closing arc.
            Bounds bounds = panel.bounds;
            Vector3 offset = bounds.center - pivot.position;
            return new Vector2(Mathf.Abs(offset.x) + bounds.extents.x, Mathf.Abs(offset.z) + bounds.extents.z).magnitude;
        }

        private bool WouldHitPlayer(Quaternion nextRotation)
        {
            if (player == null || !player.enabled || !player.gameObject.activeInHierarchy)
                return false;

            Quaternion nextWorldRotation = pivot.parent.rotation * nextRotation;
            Quaternion delta = nextWorldRotation * Quaternion.Inverse(pivot.rotation);
            Vector3 panelPosition = pivot.position + delta * (panel.transform.position - pivot.position);
            return Physics.ComputePenetration(panel, panelPosition, delta * panel.transform.rotation,
                player, player.transform.position, player.transform.rotation, out _, out _);
        }
    }
}
