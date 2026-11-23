using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectR
{
    [RequireComponent(typeof(CharacterController), typeof(PlayerInput))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField, Tooltip("Eye-level pivot rotated by mouse look.")]
        private Transform view;
        [SerializeField, Min(0f)] private float moveSpeed = 1f;
        [SerializeField, Min(0f)] private float mouseSensitivity = 0.12f;

        private CharacterController body;
        private InputAction move;
        private InputAction look;
        private Vector2 mouseDelta;
        private float pitch;
        private float fallSpeed;
        private bool inputBlocked;

        public bool AcceptsInput => !inputBlocked && (Application.isBatchMode || Cursor.lockState == CursorLockMode.Locked);

        private void Start()
        {
            body = GetComponent<CharacterController>();
            InputActionAsset actions = GetComponent<PlayerInput>().actions;
            move = actions.FindAction("Gameplay/Move", true);
            look = actions.FindAction("Gameplay/Look", true);
            look.performed += ReadMouseDelta;
            CaptureCursor();
        }

        private void Update()
        {
            if (inputBlocked)
                return;

            if (!AcceptsInput && Application.isFocused && Mouse.current?.leftButton.wasPressedThisFrame == true)
                CaptureCursor();

            Vector2 movement = Vector2.zero;
            if (AcceptsInput)
            {
                movement = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
                Vector2 lookRotation = mouseDelta * mouseSensitivity;
                transform.Rotate(0f, lookRotation.x, 0f);
                pitch = Mathf.Clamp(pitch - lookRotation.y, -85f, 85f);
                view.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
            mouseDelta = Vector2.zero;

            fallSpeed = body.isGrounded ? -2f : fallSpeed + Physics.gravity.y * Time.deltaTime;
            Vector3 velocity = (transform.right * movement.x + transform.forward * movement.y) * moveSpeed;
            velocity.y = fallSpeed;
            body.Move(velocity * Time.deltaTime);
        }

        private void OnDisable()
        {
            ReleaseCursor();
        }

        public void SetInputBlocked(bool blocked)
        {
            inputBlocked = blocked;
            mouseDelta = Vector2.zero;
            if (blocked)
                ReleaseCursor();
            else
                CaptureCursor();
        }

        private void OnDestroy()
        {
            if (look != null)
                look.performed -= ReadMouseDelta;
        }

        private void ReadMouseDelta(InputAction.CallbackContext context)
        {
            if (AcceptsInput)
                mouseDelta = context.ReadValue<Vector2>();
        }

        private static void ReleaseCursor()
        {
            if (!Application.isBatchMode)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private static void CaptureCursor()
        {
            if (!Application.isBatchMode)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}
