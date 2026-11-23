using System;
using UnityEngine;

namespace ProjectR
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-5)]
    public sealed class CameraSway : MonoBehaviour
    {
        [Header("General")]
        [SerializeField, Tooltip("이동 및 접지 상태를 읽을 플레이어의 CharacterController.")]
        private CharacterController body;
        [SerializeField, Range(0f, 1f), Tooltip("전체 흔들림 강도. 권장 0.3~1. 0은 화면 흔들림 끄기.")]
        private float masterIntensity = 1f;
        [SerializeField, Min(.01f), Tooltip("상태 전환에 쓰는 감쇠 시간(초). 권장 0.1~0.3.")]
        private float blendTime = .18f;

        [Header("Idle Breathing")]
        [SerializeField, Min(.1f), Tooltip("호흡 한 번의 길이(초). 권장 3~4.")]
        private float breathPeriod = 3.5f;
        [SerializeField, Min(0f), Tooltip("호흡 시 상하 이동 진폭(유닛). 권장 0.010~0.015.")]
        private float breathHeight = .012f;
        [SerializeField, Min(0f), Tooltip("호흡 시 위아래 시선 기울기(도). 권장 0.2~0.3.")]
        private float breathPitch = .24f;

        [Header("Hand Tremor")]
        [SerializeField, Min(0f), Tooltip("Perlin 노이즈의 샘플 진행 속도. 권장 0.5~1.5.")]
        private float tremorRate = .9f;
        [SerializeField, Tooltip("축별 미세 이동 진폭(유닛). 권장 각 축 0~0.003.")]
        private Vector3 tremorPosition = new(.002f, .0015f, .001f);
        [SerializeField, Tooltip("축별 미세 회전 진폭(도). 권장 X/Y 0~0.10, Z 0~0.18.")]
        private Vector3 tremorRotation = new(.055f, .035f, .12f);
        [SerializeField, Range(0f, 1f), Tooltip("현재 공포·긴장 수치. 0은 평상시, 1은 최대 긴장.")]
        private float tension;
        [SerializeField, Min(0f), Tooltip("긴장 수치가 1일 때 미세 흔들림 증가량. 권장 0.15~0.5. 0.35는 35% 증가.")]
        private float tensionTremorBoost = .35f;

        [Header("Walking")]
        [SerializeField, Min(.01f), Tooltip("1배 진폭 및 발걸음 빈도의 기준 속도(유닛/초). 현재 프로젝트는 1.")]
        private float referenceSpeed = 1f;
        [SerializeField, Min(0f), Tooltip("아주 작은 움직임을 걸음에서 제외하는 속도(유닛/초). 권장 0.02~0.1.")]
        private float minimumWalkSpeed = .05f;
        [SerializeField, Min(.01f), Tooltip("기준 속도에서 초당 발걸음 수. 권장 1.2~1.8.")]
        private float stepsPerSecond = 1.4f;
        [SerializeField, Min(0f), Tooltip("기준 속도에서 상하 출렁임 진폭(유닛). 권장 0.01~0.025.")]
        private float walkHeight = .018f;
        [SerializeField, Min(0f), Tooltip("기준 속도에서 좌우 이동 진폭(유닛). 권장 0.005~0.015.")]
        private float walkSway = .009f;
        [SerializeField, Min(0f), Tooltip("기준 속도에서 좌우 기울기(도). 권장 0.5~1.0.")]
        private float walkRoll = .65f;

        [Header("Footstep Impact")]
        [SerializeField, Tooltip("발이 닿을 때 카메라를 짧게 더 내린다. 기본값은 꺼짐.")]
        private bool enableFootstepImpact;
        [SerializeField, Min(0f), Tooltip("기준 속도에서 발 디딤 때 추가로 내려가는 거리(유닛). 권장 0.003~0.01.")]
        private float impactDepth = .006f;
        [SerializeField, Min(.01f), Tooltip("발 디딤 충격이 사라지기까지 걸리는 시간(초). 권장 0.05~0.12.")]
        private float impactDuration = .075f;

        [Header("Comfort Limits")]
        [SerializeField, Min(0f), Tooltip("합산한 위치 오프셋의 최대 크기(유닛). 권장 0.025~0.06.")]
        private float maxPositionOffset = .045f;
        [SerializeField, Min(0f), Tooltip("합산한 회전 오프셋의 최대 크기(도). 권장 0.8~1.5.")]
        private float maxRotationOffset = 1.2f;

        private PlayerController player;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private float breathPhase;
        private float noiseTime;
        private float walkAmount;
        private double walkPhase = .5d;
        private float impactAge = float.PositiveInfinity;

        // The contact event still fires when the visual intensity is zero.
        public event Action OnFootstep;

        public void SetMasterIntensity(float value) => masterIntensity = Mathf.Clamp01(value);

        public void SetTension(float value) => tension = Mathf.Clamp01(value);

        private void Awake()
        {
            if (body != null)
                player = body.GetComponent<PlayerController>();
        }

        private void OnEnable()
        {
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
        }

        private void OnDisable()
        {
            RestorePose();
        }

        private void LateUpdate()
        {
            if (masterIntensity <= 0f)
                RestorePose();

            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            breathPhase = Mathf.Repeat(breathPhase + dt / breathPeriod, 1f);
            noiseTime += dt * tremorRate;

            bool grounded = body != null && body.enabled && body.isGrounded;
            float speed = 0f;
            if (grounded && (player == null || player.AcceptsInput))
            {
                Vector3 velocity = body.velocity;
                speed = new Vector2(velocity.x, velocity.z).magnitude;
            }

            float speedRatio = speed > minimumWalkSpeed ? speed / referenceSpeed : 0f;
            float blend = 1f - Mathf.Exp(-dt / blendTime);
            walkAmount = grounded ? Mathf.Lerp(walkAmount, speedRatio, blend) : 0f;
            int contacts = AdvanceWalk(dt, speedRatio * stepsPerSecond, grounded);

            if (masterIntensity > 0f)
                ApplyOffsets();

            for (int i = 0; i < contacts; i++)
                OnFootstep?.Invoke();
        }

        private int AdvanceWalk(float dt, float stepRate, bool grounded)
        {
            if (!grounded)
            {
                impactAge = float.PositiveInfinity;
                return 0;
            }

            impactAge += dt;
            if (stepRate <= 0f)
                return 0; // Keep the phase where it was when the player stopped.

            double nextPhase = walkPhase + (double)stepRate * dt;
            // Each integer phase is a vertical trough. Counting crossings also handles a long frame.
            double lastContact = Math.Floor(nextPhase);
            int contacts = (int)(lastContact - Math.Floor(walkPhase));
            if (contacts > 0)
                impactAge = (float)((nextPhase - lastContact) / stepRate);

            walkPhase = nextPhase % 2d;
            return contacts;
        }

        private void ApplyOffsets()
        {
            var (breathPosition, breathRotation) = CalculateBreathing();
            var (tremorOffset, tremorEuler) = CalculateTremor();
            var (walkPosition, walkRotation) = CalculateWalk();
            float idleWeight = 1f - Mathf.Clamp01(walkAmount);
            Vector3 position = breathPosition * idleWeight + tremorOffset + (walkPosition + CalculateImpact()) * walkAmount;
            Vector3 rotation = breathRotation * idleWeight + tremorEuler + walkRotation * walkAmount;

            transform.localPosition = restPosition + Vector3.ClampMagnitude(position, maxPositionOffset) * masterIntensity;
            transform.localRotation = restRotation * Quaternion.Euler(Vector3.ClampMagnitude(rotation, maxRotationOffset) * masterIntensity);
        }

        private (Vector3 Position, Vector3 Rotation) CalculateBreathing()
        {
            float breath = Mathf.Sin(breathPhase * 2f * Mathf.PI);
            return (Vector3.up * (breathHeight * breath), Vector3.left * (breathPitch * breath));
        }

        private (Vector3 Position, Vector3 Rotation) CalculateTremor()
        {
            // Distinct noise samples prevent all axes from drifting in sync.
            Vector3 positionNoise = new(SignedNoise(11.7f), SignedNoise(23.4f), SignedNoise(37.1f));
            Vector3 rotationNoise = new(SignedNoise(48.9f), SignedNoise(63.2f), SignedNoise(79.5f));
            float gain = 1f + tension * tensionTremorBoost;
            return (Vector3.Scale(tremorPosition, positionNoise) * gain, Vector3.Scale(tremorRotation, rotationNoise) * gain);
        }

        private float SignedNoise(float channel) => Mathf.PerlinNoise(noiseTime + channel, channel) * 2f - 1f;

        private (Vector3 Position, Vector3 Rotation) CalculateWalk()
        {
            float angle = (float)walkPhase * Mathf.PI;
            float sway = Mathf.Cos(angle);
            float rise = -Mathf.Cos(2f * angle);
            return (new Vector3(sway * walkSway, rise * walkHeight, 0f), new Vector3(0f, 0f, -sway * walkRoll));
        }

        private Vector3 CalculateImpact()
        {
            if (!enableFootstepImpact || impactAge >= impactDuration)
                return Vector3.zero;

            float pulse = Mathf.Sin(Mathf.PI * (impactAge / impactDuration));
            return Vector3.down * (pulse * impactDepth);
        }

        private void RestorePose()
        {
            transform.localPosition = restPosition;
            transform.localRotation = restRotation;
        }
    }
}
