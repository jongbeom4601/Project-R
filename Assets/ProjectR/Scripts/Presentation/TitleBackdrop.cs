using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ProjectR
{
    // Live background for the title screen: a camera inside one apartment (06 by default) looking down the
    // hallway toward the front door, with its lamps faltering on unscaled time while the game is paused.
    // The apartment is only switched on for the title and returned to the route's state when play starts.
    public sealed class TitleBackdrop : MonoBehaviour
    {
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private ApartmentRoute route;
        [SerializeField] private Camera playerCamera;
        [SerializeField, Min(0)] private int spaceIndex = 6;
        [SerializeField, Tooltip("Camera position in the apartment's own space.")]
        private Vector3 localPosition = new(13.0f, 1.55f, -4.35f);
        [SerializeField, Tooltip("Camera rotation in the apartment's own space.")]
        private Vector3 localEuler = new(4f, 264f, 0f);
        [SerializeField, Range(30f, 90f)] private float fieldOfView = 62f;
        [SerializeField, Tooltip("Lamps (by object name inside the apartment) that falter on the title screen.")]
        private string[] lampNames = { "Lamp_Ceiling_Apt_03", "Lamp_Ceiling_Apt_03 (1)" };
        [SerializeField] private Vector2 steadyTime = new(1.5f, 5f);
        [SerializeField] private Vector2 outageTime = new(.05f, .22f);
        [SerializeField, Range(0f, 1f), Tooltip("Chance that a fault ends in a long blackout instead of a short stutter.")]
        private float longOutageChance = .2f;

        private readonly List<Lamp> lamps = new();
        private Camera view;
        private GameObject space;
        private bool spaceWasActive;
        private bool running;

        public void Begin()
        {
            if (running || route == null)
                return;
            space = route.Space(spaceIndex);
            if (space == null)
                return;

            running = true;
            spaceWasActive = space.activeSelf;
            space.SetActive(true);

            if (view == null)
            {
                view = new GameObject("Title Camera").AddComponent<Camera>();
                view.transform.SetParent(transform, false);
                view.nearClipPlane = .05f;
                view.clearFlags = CameraClearFlags.Skybox;
                view.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true; // brightness setting
            }
            view.fieldOfView = fieldOfView;
            view.depth = (playerCamera != null ? playerCamera.depth : 0f) + 1f;
            view.transform.SetPositionAndRotation(space.transform.TransformPoint(localPosition),
                space.transform.rotation * Quaternion.Euler(localEuler));
            view.enabled = true;
            if (playerCamera != null)
                playerCamera.enabled = false;

            lamps.Clear();
            foreach (Transform t in space.GetComponentsInChildren<Transform>(true))
                if (System.Array.IndexOf(lampNames, t.name) >= 0)
                {
                    Light light = t.GetComponentInChildren<Light>(true);
                    if (light != null && light.enabled)
                        lamps.Add(new Lamp(light, t.GetComponentsInChildren<Renderer>(true), Random.Range(.4f, steadyTime.y)));
                }
        }

        public void End()
        {
            if (!running)
                return;
            running = false;
            foreach (Lamp lamp in lamps)
                lamp.SetPower(true);
            lamps.Clear();

            if (view != null)
                view.enabled = false;
            if (playerCamera != null)
                playerCamera.enabled = true;
            if (space != null)
            {
                // The room was lit oddly while it faltered; let it capture its reflection again when the player gets there.
                foreach (RoomReflectionProbe probe in space.GetComponentsInChildren<RoomReflectionProbe>(true))
                    probe.ForgetCapture();
                space.SetActive(spaceWasActive);
            }
        }

        private void Update()
        {
            if (!running)
                return;
            float dt = Time.unscaledDeltaTime; // the game is paused under the title
            foreach (Lamp lamp in lamps)
            {
                lamp.Timer -= dt;
                if (lamp.Timer > 0f)
                    continue;

                if (lamp.Powered)
                {
                    if (lamp.Stutters == 0)
                        lamp.Stutters = Random.Range(1, 5);
                    lamp.Stutters--;
                    lamp.SetPower(false);
                    bool longOut = lamp.Stutters == 0 && Random.value < longOutageChance;
                    lamp.Timer = longOut ? Random.Range(.6f, 1.4f) : Random.Range(outageTime.x, outageTime.y);
                }
                else
                {
                    lamp.SetPower(true);
                    lamp.Timer = lamp.Stutters > 0 ? Random.Range(.04f, .16f) : Random.Range(steadyTime.x, steadyTime.y);
                }
            }
        }

        private void OnDisable() => End();

        private sealed class Lamp
        {
            private readonly Light light;
            private readonly List<(Renderer renderer, int slot)> glow = new();
            private readonly MaterialPropertyBlock unlit = new();

            public float Timer;
            public int Stutters;
            public bool Powered { get; private set; } = true;

            public Lamp(Light light, Renderer[] renderers, float firstFault)
            {
                this.light = light;
                Timer = firstFault;
                unlit.SetColor(EmissionColor, Color.black);
                foreach (Renderer r in renderers)
                {
                    Material[] materials = r.sharedMaterials;
                    for (int slot = 0; slot < materials.Length; slot++)
                        if (materials[slot] != null && materials[slot].HasProperty(EmissionColor) && materials[slot].IsKeywordEnabled("_EMISSION"))
                            glow.Add((r, slot));
                }
            }

            public void SetPower(bool on)
            {
                Powered = on;
                if (light != null)
                    light.enabled = on;
                foreach (var (renderer, slot) in glow)
                    if (renderer != null)
                        renderer.SetPropertyBlock(on ? null : unlit, slot);
            }
        }
    }
}
