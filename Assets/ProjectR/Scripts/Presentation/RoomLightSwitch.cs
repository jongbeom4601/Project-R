using System.Collections.Generic;
using UnityEngine;

namespace ProjectR
{
    // A room that starts dark and lights up the first time the player steps inside.
    // While it is dark no light can leak out of it, whatever state its door is in.
    public sealed class RoomLightSwitch : MonoBehaviour
    {
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private CharacterController player;
        [SerializeField] private Light[] lights = System.Array.Empty<Light>();
        [SerializeField, Tooltip("Lamp meshes whose glow turns off with the lights. Empty: the lamp this object hangs from.")]
        private Renderer[] lampGlow = System.Array.Empty<Renderer>();
        [SerializeField, Tooltip("Captured again once the room is lit, so reflections show the lit room.")]
        private RoomReflectionProbe reflection;
        [SerializeField, Tooltip("Room box centre, in world axes, relative to this object.")]
        private Vector3 roomCenterOffset;
        [SerializeField, Tooltip("Room box size in world axes.")]
        private Vector3 roomSize = new(3f, 2.8f, 4f);

        private readonly List<(Renderer renderer, int slot)> glowSlots = new();
        private MaterialPropertyBlock unlit;
        private bool lit;

        private void Awake()
        {
            unlit = new MaterialPropertyBlock();
            unlit.SetColor(EmissionColor, Color.black);
            if (lampGlow.Length == 0 && transform.parent != null)
                lampGlow = transform.parent.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in lampGlow)
            {
                if (r == null)
                    continue;
                Material[] materials = r.sharedMaterials;
                for (int slot = 0; slot < materials.Length; slot++)
                    if (materials[slot] != null && materials[slot].HasProperty(EmissionColor))
                        glowSlots.Add((r, slot));
            }
            SetLit(false);
        }

        private void Update()
        {
            if (lit || player == null)
                return;

            var room = new Bounds(transform.position + roomCenterOffset, roomSize);
            if (room.Contains(player.bounds.center))
                SetLit(true);
        }

        private void SetLit(bool on)
        {
            lit = on;
            foreach (Light light in lights)
                if (light != null)
                    light.enabled = on;
            foreach (var (renderer, slot) in glowSlots)
                renderer.SetPropertyBlock(on ? null : unlit, slot);
            if (on && reflection != null)
                reflection.Recapture();
        }
    }
}
