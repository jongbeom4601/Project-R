using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectR
{
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        private const string InteractText = "상호작용";
        private const string ReturnLockedText = "돌아갈 수 없습니다";
        private const string LockedText = "잠겨 있습니다";

        [SerializeField] private Camera view;
        [SerializeField, Min(0f)] private float reach = 2.25f;

        private CharacterController body;
        private PlayerController controller;
        private InputAction interact;
        private bool interactionRequested;
        private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
        private DoorController focusedDoor;
        private DoorController deniedDoor;
        private float deniedUntil;
        private Font promptFont;
        private GUIStyle keyStyle;
        private GUIStyle messageStyle;
        private bool hasKoreanFont;

        private void Start()
        {
            body = GetComponent<CharacterController>();
            controller = GetComponent<PlayerController>();
            interact = GetComponent<PlayerInput>().actions.FindAction("Gameplay/Interact", true);
            interact.performed += RequestInteraction;
        }

        private void LateUpdate()
        {
            bool requested = interactionRequested;
            interactionRequested = false;
            focusedDoor = null;
            if (!controller.AcceptsInput || view.cullingMask == 0)
                return;

            Ray ray = view.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            DoorController target = FindDoor(ray);
            if (target == null || !target.CanInteractFrom(body))
                return;

            focusedDoor = target;
            if (requested)
            {
                if (target.IsLockedFor(body))
                {
                    deniedDoor = target;
                    deniedUntil = Time.time + 1.6f;
                }
                target.TryToggle(body);
            }
        }

        private DoorController FindDoor(Ray ray)
        {
            RaycastHit[] hits = hitBuffer;
            int count = Physics.RaycastNonAlloc(ray, hits, reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            if (count == hits.Length)
            {
                hits = Physics.RaycastAll(ray, reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
                count = hits.Length;
            }

            DoorController target = null;
            float closest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i];
                Collider collider = hit.collider;
                if (hit.distance >= closest || collider.transform.IsChildOf(transform))
                    continue;

                DoorController door = collider.GetComponentInParent<DoorController>();
                if (collider.isTrigger && (door == null || !door.IsInteractionTarget(collider)))
                    continue;

                closest = hit.distance;
                target = door;
            }

            return target;
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || focusedDoor == null || !focusedDoor.CanInteractFrom(body) ||
                !controller.AcceptsInput || view.cullingMask == 0)
                return;

            Vector3 anchor = view.WorldToScreenPoint(focusedDoor.HandlePosition(view.transform.position));
            if (anchor.z <= view.nearClipPlane)
                return;

            if (messageStyle == null)
                CreatePromptStyles();

            bool denied = focusedDoor == deniedDoor && Time.time < deniedUntil && focusedDoor.IsLockedFor(body);
            string message = hasKoreanFont ? InteractText : "Interact";
            if (denied)
                message = focusedDoor.IsPassageLocked
                    ? (hasKoreanFont ? ReturnLockedText : "No way back")
                    : (hasKoreanFont ? LockedText : "Locked");
            const float height = 30f;
            float width = Mathf.Ceil(messageStyle.CalcSize(new GUIContent(message)).x) + 46f;
            float left = Mathf.Clamp(anchor.x + 12f, 6f, Mathf.Max(6f, Screen.width - width - 6f));
            float top = Mathf.Clamp(Screen.height - anchor.y - height / 2f, 6f, Mathf.Max(6f, Screen.height - height - 6f));
            Color previous = GUI.color;
            GUI.color = new Color(.07f, .075f, .08f, .94f);
            GUI.DrawTexture(new Rect(left, top, width, height), Texture2D.whiteTexture);
            GUI.color = denied ? new Color(.78f, .48f, .39f) : new Color(.88f, .87f, .82f);
            GUI.DrawTexture(new Rect(left + 4f, top + 4f, 22f, 22f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(left + 4f, top + 4f, 22f, 22f), "E", keyStyle);
            GUI.Label(new Rect(left + 34f, top, width - 38f, height), message, messageStyle);
            GUI.color = previous;
        }

        private void CreatePromptStyles()
        {
            promptFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Noto Sans KR" }, 16);
            hasKoreanFont = promptFont != null && promptFont.HasCharacter('상') && promptFont.HasCharacter('잠');
            messageStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontSize = 15 };
            if (hasKoreanFont)
                messageStyle.font = promptFont;
            messageStyle.normal.textColor = Color.white;
            keyStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 14, fontStyle = FontStyle.Bold };
            keyStyle.normal.textColor = new Color(.10f, .10f, .11f);
        }

        private void OnDestroy()
        {
            if (interact != null)
                interact.performed -= RequestInteraction;
            if (promptFont != null)
                Destroy(promptFont);
        }

        private void RequestInteraction(InputAction.CallbackContext context)
        {
            interactionRequested = isActiveAndEnabled && controller.AcceptsInput;
        }
    }
}
