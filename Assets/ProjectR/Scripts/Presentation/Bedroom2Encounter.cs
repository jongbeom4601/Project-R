using System.Collections;
using UnityEngine;

namespace ProjectR
{
    // The last scare, in bedroom 2 of Apartment 08:
    // 1. The door locks behind the player; trying it puts Shadowlop in the room.
    // 2. When the player turns and sees it: a stinger, then a blackout.
    // 3. The lights return without Shadowlop, and the door opens again.
    // 4. Opening it, Shadowlop lunges at the camera from beyond the door, the picture cuts to black and the demo ends.
    public sealed class Bedroom2Encounter : MonoBehaviour
    {
        private enum Step { Waiting, Lurking, Blackout, Gone, Lunging, Ended }

        [Header("Scene")]
        [SerializeField, Tooltip("Bedroom-2 door; it locks behind the player.")] private DoorController door;
        [SerializeField, Tooltip("Shadowlop placed in the room, switched off in the scene.")] private GameObject shadowlop;
        [SerializeField] private Camera view;
        [SerializeField] private PlayerController player;
        [SerializeField] private HorrorEffects effects;
        [SerializeField] private GameDirector director;

        [Header("Sound")]
        [SerializeField, Tooltip("2D source for the stingers.")] private AudioSource stingerSource;
        [SerializeField] private AudioClip seenStinger;
        [SerializeField, Range(0f, 1f)] private float seenVolume = 1f;
        [SerializeField] private AudioClip lungeStinger;
        [SerializeField, Range(0f, 1f)] private float lungeVolume = 1f;

        [Header("Seeing it")]
        [SerializeField, Range(.2f, 1f), Tooltip("Share of the screen, around its centre, that counts as seeing Shadowlop.")]
        private float viewFraction = .65f;
        [SerializeField, Min(0f)] private float blackoutDelay = .3f;
        [SerializeField, Min(0f), Tooltip("The lights blink once before going out: off for this long...")]
        private float flickerOff = .08f;
        [SerializeField, Min(0f), Tooltip("...then back on for this long, then the blackout.")]
        private float flickerOn = .12f;
        [SerializeField, Min(0f)] private float blackoutTime = 1.75f;

        [Header("Lunge")]
        [SerializeField, Tooltip("Plays while Shadowlop runs at the camera (the CHASE clip).")]
        private RuntimeAnimatorController lungeController;
        [SerializeField, Range(.1f, 1f), Tooltip("How far the door must have swung open (0-1) before the lunge starts, so the panel is out of its way.")]
        private float doorClearOpenness = .6f;
        [SerializeField, Min(0f), Tooltip("Metres beyond the door where the lunge starts.")] private float lungeStart = 1.8f;
        [SerializeField, Min(.05f)] private float lungeTime = .3f;
        [SerializeField, Min(.1f), Tooltip("How close in front of the camera its face ends up.")] private float lungeStop = .3f;
        [SerializeField, Range(.5f, 1f), Tooltip("Height of the face within the body, from the feet (0) to the top (1). The lunge aims this point at the camera.")]
        private float faceHeight = .65f; // .65 puts the open mouth in the middle of the picture
        [SerializeField, Min(0f), Tooltip("Silent black before the demo ending appears.")] private float endingSilence = 1f;

        private Step step;
        private Renderer[] renderers;
        private Collider[] ownColliders;
        private readonly RaycastHit[] hits = new RaycastHit[16];

        private void Awake()
        {
            renderers = shadowlop.GetComponentsInChildren<Renderer>(true);
            ownColliders = shadowlop.GetComponentsInChildren<Collider>(true);
        }

        private void OnEnable()
        {
            door.LockedTried += OnLockedTried;
            door.OpenStarted += OnOpenStarted;
        }

        private void OnDisable()
        {
            door.LockedTried -= OnLockedTried;
            door.OpenStarted -= OnOpenStarted;
        }

        private void OnLockedTried(DoorController tried)
        {
            if (step != Step.Waiting || !tried.IsPassageLocked)
                return;
            shadowlop.SetActive(true);
            step = Step.Lurking;
        }

        private void Update()
        {
            if (step == Step.Lurking && Time.timeScale > 0f && IsSeen())
                StartCoroutine(SeenThenBlackout());
        }

        private IEnumerator SeenThenBlackout()
        {
            step = Step.Blackout;
            stingerSource.PlayCue(seenStinger, seenVolume);
            yield return new WaitForSeconds(blackoutDelay);
            // One blink, Shadowlop still standing there, then darkness.
            effects.BeginBlackout();
            yield return new WaitForSeconds(flickerOff);
            effects.EndBlackout();
            yield return new WaitForSeconds(flickerOn);
            effects.BeginBlackout();
            shadowlop.SetActive(false);
            yield return new WaitForSeconds(blackoutTime);
            effects.EndBlackout();
            door.ReleasePassageLock();
            step = Step.Gone;
        }

        private void OnOpenStarted(DoorController opened)
        {
            if (step == Step.Gone)
                StartCoroutine(Lunge());
        }

        private IEnumerator Lunge()
        {
            step = Step.Lunging;
            player.SetInputBlocked(true);

            // Let the panel swing out of the doorway first, so the rush is seen through the opening.
            while (door.Openness < doorClearOpenness && !door.IsClosed)
                yield return null;

            // Beyond the door, on the far side from the player, facing them.
            Vector3 away = door.transform.position - view.transform.position;
            away.y = 0f;
            away.Normalize();
            Transform body = shadowlop.transform;
            Vector3 start = door.transform.position + away * lungeStart;
            start.y = door.transform.position.y;
            body.SetPositionAndRotation(start, Quaternion.LookRotation(-away, Vector3.up));
            if (lungeController != null && shadowlop.TryGetComponent(out Animator animator))
                animator.runtimeAnimatorController = lungeController;
            shadowlop.SetActive(true);
            stingerSource.PlayCue(lungeStinger, lungeVolume);
            yield return null; // let the pose and bounds settle

            // The last pose is always the arrival in front of the lens, and it is drawn once before the cut,
            // however long the frames are.
            for (float t = 0f; ; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / lungeTime);
                // Aim the face, not the middle of the body, at the lens. The running pose moves it, so measure each frame.
                Vector3 faceOffset = FacePoint() - body.position;
                Vector3 target = view.transform.position + view.transform.forward * lungeStop - faceOffset;
                body.position = Vector3.Lerp(start, target, k * k); // speeds up as it comes
                Vector3 facing = view.transform.position - body.position;
                facing.y = 0f;
                if (facing.sqrMagnitude > .0001f)
                    body.rotation = Quaternion.LookRotation(facing, Vector3.up);
                yield return null;
                if (k >= 1f)
                    break;
            }

            // Cut to black at the moment it fills the screen; the cut takes the sound with it, so the hold is silent.
            stingerSource.Stop();
            door.StopSounds();
            effects.BeginBlackout();
            shadowlop.SetActive(false);
            step = Step.Ended;
            director.EndFromBlackout(endingSilence);
        }

        private Vector3 FacePoint()
        {
            Bounds b = BodyBounds();
            return new Vector3(b.center.x, b.min.y + b.size.y * faceHeight, b.center.z);
        }

        private Bounds BodyBounds()
        {
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers)
                bounds.Encapsulate(r.bounds);
            return bounds;
        }

        // Seen: part of the body sits in the middle of the picture with nothing solid between it and the camera.
        private bool IsSeen()
        {
            Bounds bounds = BodyBounds();
            Vector3 eye = view.transform.position;
            float margin = (1f - viewFraction) * .5f;
            foreach (Vector3 point in new[] { bounds.center, bounds.center + Vector3.up * bounds.extents.y * .6f })
            {
                Vector3 vp = view.WorldToViewportPoint(point);
                if (vp.z <= 0f || vp.x < margin || vp.x > 1f - margin || vp.y < margin || vp.y > 1f - margin)
                    continue;
                if (!Blocked(eye, point))
                    return true;
            }
            return false;
        }

        private bool Blocked(Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            int count = Physics.RaycastNonAlloc(from, dir.normalized, hits, dir.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider c = hits[i].collider;
                if (System.Array.IndexOf(ownColliders, c) >= 0 || c.transform.IsChildOf(player.transform))
                    continue;
                return true;
            }
            return false;
        }
    }
}
