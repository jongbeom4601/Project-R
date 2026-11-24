using System.Collections.Generic;
using UnityEngine;

namespace ProjectR
{
    [RequireComponent(typeof(HorrorEffects))]
    public sealed class ApartmentLightFlicker : MonoBehaviour
    {
        [SerializeField] private Light[] flickeringLights = System.Array.Empty<Light>();
        [SerializeField] private Light[] unpoweredLights = System.Array.Empty<Light>();
        [SerializeField] private Vector2 timeBetweenFaults = new(3f, 7.5f);
        [SerializeField] private Vector2 outageDuration = new(.14f, .42f);
        [SerializeField] private Vector2 recoveryDuration = new(.25f, .68f);
        [SerializeField] private AudioClip faultSound;
        [SerializeField, Range(0f, 1f)] private float faultVolume = .35f;

        private readonly List<Fixture> flickering = new();
        private readonly List<Fixture> unpowered = new();

        private HorrorEffects effects;

        private void Awake() => effects = GetComponent<HorrorEffects>();

        private void OnEnable()
        {
            foreach (Light light in flickeringLights)
            {
                if (light != null && light.enabled)
                    flickering.Add(new Fixture(light) { Remaining = SampleDuration(timeBetweenFaults) });
            }

            foreach (Light light in unpoweredLights)
            {
                if (light == null)
                    continue;

                var fixture = new Fixture(light);
                fixture.SetPower(false);
                unpowered.Add(fixture);
            }
        }

        private void Update()
        {
            if (effects.IsBlackout || Time.deltaTime <= 0f)
                return;

            AdvanceFlicker(Time.deltaTime);
        }

        private void AdvanceFlicker(float deltaTime)
        {
            foreach (Fixture fixture in flickering)
            {
                fixture.Remaining -= deltaTime;
                if (fixture.Remaining > 0f)
                    continue;

                if (fixture.Powered)
                {
                    if (fixture.RemainingOutages == 0)
                    {
                        fixture.RemainingOutages = Random.Range(1, 4);
                        // One pop per burst, so a quick run of outages does not machine-gun.
                        if (faultSound != null)
                        {
                            // Kept under this always-active object, so a fixture in a switched-off apartment pops as before.
                            if (fixture.Pop == null)
                                fixture.Pop = AudioCue.CreatePointSource(transform, "Light Fault Pop");
                            fixture.Pop.PlayAt(faultSound, fixture.Position, faultVolume * GameSettings.EffectsVolume);
                        }
                    }

                    fixture.RemainingOutages--;
                    fixture.SetPower(false);
                    fixture.Remaining = SampleDuration(outageDuration);
                }
                else
                {
                    fixture.SetPower(true);
                    fixture.Remaining = SampleDuration(fixture.RemainingOutages > 0 ? recoveryDuration : timeBetweenFaults);
                }
            }
        }

        private static float SampleDuration(Vector2 range)
        {
            float minimum = Mathf.Max(.1f, range.x);
            return Random.Range(minimum, Mathf.Max(minimum, range.y));
        }

        private void OnDisable()
        {
            foreach (Fixture fixture in flickering)
            {
                fixture.SetPower(true);
                if (fixture.Pop != null)
                    Destroy(fixture.Pop.gameObject);
            }
            foreach (Fixture fixture in unpowered)
                fixture.SetPower(true);
            flickering.Clear();
            unpowered.Clear();
        }

        private sealed class Fixture
        {
            private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
            private readonly Light light;
            private readonly bool wasEnabled;
            private readonly List<(Renderer Renderer, int Slot, MaterialPropertyBlock Original, MaterialPropertyBlock Unlit)> emission = new();

            public bool Powered { get; private set; } = true;
            public Vector3 Position => light != null ? light.transform.position : Vector3.zero;
            public float Remaining { get; set; }
            public int RemainingOutages { get; set; }
            public AudioSource Pop { get; set; }

            public Fixture(Light light)
            {
                this.light = light;
                wasEnabled = light.enabled;

                Transform visuals = light.transform;
                if (!light.TryGetComponent<Renderer>(out _) && visuals.parent != null && visuals.parent.TryGetComponent<Renderer>(out _))
                    visuals = visuals.parent;

                foreach (Renderer renderer in visuals.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int slot = 0; slot < materials.Length; slot++)
                    {
                        Material material = materials[slot];
                        if (material == null || !material.HasProperty(EmissionColor) || !material.IsKeywordEnabled("_EMISSION"))
                            continue;

                        var original = new MaterialPropertyBlock();
                        renderer.GetPropertyBlock(original, slot);
                        var unlit = new MaterialPropertyBlock();
                        if (original.isEmpty)
                            renderer.GetPropertyBlock(unlit);
                        else
                            renderer.GetPropertyBlock(unlit, slot);
                        unlit.SetColor(EmissionColor, Color.black);
                        emission.Add((renderer, slot, original.isEmpty ? null : original, unlit));
                    }
                }
            }

            public void SetPower(bool on)
            {
                Powered = on;
                if (light != null)
                    light.enabled = on && wasEnabled;

                foreach (var (renderer, slot, original, unlit) in emission)
                    if (renderer != null)
                        renderer.SetPropertyBlock(on ? original : unlit, slot);
            }
        }
    }
}
