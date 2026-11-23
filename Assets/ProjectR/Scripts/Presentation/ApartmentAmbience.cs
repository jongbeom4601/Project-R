using UnityEngine;

namespace ProjectR
{
    // Background sound by the apartment the player is in:
    // fades a tension drone in and out, and fades the indoor hum away from a set apartment onward.
    // The climax cuts the drone at once so the blackout lands in silence.
    [RequireComponent(typeof(AudioSource))]
    public sealed class ApartmentAmbience : MonoBehaviour
    {
        [SerializeField] private ApartmentRoute route;
        [SerializeField] private HorrorEventManager progression;
        [SerializeField] private HorrorEffects effects;
        [Tooltip("Index = route space: 0 starting bedroom, 1-8 apartments.")]
        [SerializeField] private float[] droneVolumeBySpace = { 0f, 0f, 0f, 0f, .12f, .3f, .5f, 0f, 0f };
        [SerializeField, Min(.01f)] private float fadePerSecond = .2f;
        [SerializeField] private AudioSource indoorHum;
        [SerializeField, Min(0), Tooltip("Route space from which the indoor hum fades out (7 = apartment 07).")]
        private int humOffFromSpace = 7;

        private AudioSource drone;
        private float humVolume;
        private bool silenced;

        private void Awake()
        {
            drone = GetComponent<AudioSource>();
            drone.volume = 0f;
            GameSettings.MarkDriven(drone);
            if (indoorHum != null)
            {
                humVolume = indoorHum.volume;
                GameSettings.MarkDriven(indoorHum);
            }
        }

        private void OnEnable() => progression.ClimaxRequested += Silence;

        private void OnDisable()
        {
            progression.ClimaxRequested -= Silence;
            drone.Stop();
        }

        private void Silence() => silenced = true;

        private void Update()
        {
            int space = route.CurrentSpace;
            UpdateHum(space);
            UpdateDrone(space);
        }

        private void UpdateHum(int space)
        {
            if (indoorHum == null)
                return;

            float target = space >= humOffFromSpace ? 0f : humVolume * GameSettings.AmbienceVolume;
            indoorHum.volume = Mathf.MoveTowards(indoorHum.volume, target, fadePerSecond * Time.unscaledDeltaTime);
        }

        private void UpdateDrone(int space)
        {
            if (drone.clip == null)
                return;

            if (silenced || effects.IsBlackout)
            {
                drone.volume = 0f;
            }
            else
            {
                float target = (space >= 0 && space < droneVolumeBySpace.Length ? droneVolumeBySpace[space] : 0f) * GameSettings.AmbienceVolume;
                drone.volume = Mathf.MoveTowards(drone.volume, target, fadePerSecond * Time.unscaledDeltaTime);
            }

            if (drone.volume > 0f && !drone.isPlaying)
                drone.Play();
            else if (drone.volume <= 0f && drone.isPlaying)
                drone.Stop();
        }
    }
}
