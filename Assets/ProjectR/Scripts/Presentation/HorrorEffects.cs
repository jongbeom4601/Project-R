using System.Collections;
using UnityEngine;

namespace ProjectR
{
    public sealed class HorrorEffects : MonoBehaviour
    {
        [SerializeField] private HorrorEventManager progression;
        [SerializeField] private DoorController corridorExitBehindPlayer;
        [SerializeField] private DoorController frontDoor;
        [SerializeField] private AudioSource indoorAmbience;
        [SerializeField] private AudioSource exteriorAmbience;
        [SerializeField] private AudioSource houseCue;
        [SerializeField] private AudioSource roomCue;
        [SerializeField] private AudioSource corridorCue;
        [SerializeField] private AudioSource slamSource;
        [SerializeField] private AudioClip objectMovement;
        [SerializeField] private AudioClip footstep;
        [SerializeField] private AudioClip doorSlam;
        [SerializeField] private AudioSource silenceCue;
        [SerializeField] private AudioClip tinnitus;
        [SerializeField, Range(0f, 1f)] private float tinnitusVolume = .5f;
        [SerializeField] private Light[] sceneLights;
        [SerializeField] private Light flickerLight;
        [SerializeField] private GameObject shadowCaster;

        private Coroutine climax;
        private Coroutine hallCue;
        private bool[] lightStates;
        private Color ambientColor;
        private float reflectionIntensity;
        private Camera blackoutView;
        private Color backgroundColor;
        private int cullingMask;
        private CameraClearFlags clearFlags;
        private bool fadingOutdoors;

        public bool IsBlackout => lightStates != null;

        private void OnEnable()
        {
            GameSettings.MarkDriven(exteriorAmbience);
            progression.StageActivated += StageActivated;
            progression.ClimaxRequested += StartClimax;
            progression.StageCompleted += StageCompleted;
        }

        private void Start()
        {
            if (indoorAmbience.clip != null)
                indoorAmbience.Play();
        }

        private void OnDisable()
        {
            progression.StageActivated -= StageActivated;
            progression.ClimaxRequested -= StartClimax;
            progression.StageCompleted -= StageCompleted;
            StopAllCoroutines();
            hallCue = null;
            climax = null;
            foreach (AudioSource source in new[] { indoorAmbience, exteriorAmbience, houseCue, roomCue, corridorCue, slamSource, silenceCue })
                if (source != null)
                    source.Stop();
            RestoreLights();
            if (shadowCaster != null)
                shadowCaster.SetActive(false);
        }

        private void Update()
        {
            if (Time.timeScale == 0f || !exteriorAmbience.isActiveAndEnabled)
                return;

            if (!exteriorAmbience.isPlaying && !fadingOutdoors && exteriorAmbience.clip != null)
                exteriorAmbience.Play();

            Vector3 fromThreshold = progression.PlayerBody.bounds.center - frontDoor.transform.position;
            bool outdoors = Vector3.Dot(fromThreshold, frontDoor.transform.forward) > 0f;
            float outside = fadingOutdoors ? 0f : (outdoors ? 1f : frontDoor.Openness) * .52f * GameSettings.AmbienceVolume;
            exteriorAmbience.volume = Mathf.MoveTowards(exteriorAmbience.volume, outside, Time.deltaTime * .85f);
        }

        private void StageActivated(int stage)
        {
            if (stage == 2)
                hallCue = StartCoroutine(GuideToHall());
            else if (stage == 4)
                PlayCue(roomCue, objectMovement, .28f);
        }

        private IEnumerator GuideToHall()
        {
            yield return new WaitForSeconds(.32f);
            for (int i = 0; i < 3; i++)
            {
                PlayCue(houseCue, footstep, .52f);
                yield return new WaitForSeconds(.39f);
            }
            hallCue = null;
        }

        private void StartClimax()
        {
            if (climax == null)
                climax = StartCoroutine(PlayClimax());
        }

        private IEnumerator PlayClimax()
        {
            if (hallCue != null)
            {
                StopCoroutine(hallCue);
                hallCue = null;
            }
            houseCue.Stop();
            roomCue.Stop();
            PlayCue(corridorCue, footstep, .74f);
            yield return new WaitForSeconds(.42f);
            PlayCue(corridorCue, footstep, 1f);
            yield return new WaitForSeconds(.30f);
            corridorExitBehindPlayer.TryClose(progression.PlayerBody, 390f);
            PlayCue(slamSource, doorSlam, 1f);

            bool flickerWasEnabled = flickerLight.enabled;
            flickerLight.enabled = false;
            yield return new WaitForSeconds(.13f);
            flickerLight.enabled = true;
            shadowCaster.SetActive(true);
            yield return new WaitForSeconds(.22f);
            shadowCaster.SetActive(false);
            flickerLight.enabled = false;
            yield return new WaitForSeconds(.12f);
            flickerLight.enabled = flickerWasEnabled;

            indoorAmbience.Stop();
            corridorCue.Stop();
            slamSource.Stop();
            Blackout();
            PlayCue(silenceCue, tinnitus, tinnitusVolume);
            yield return new WaitForSeconds(1.5f);
            RestoreLights();
            yield return new WaitForSeconds(.65f);
            climax = null;
            progression.FinishClimax();
        }

        private void Blackout()
        {
            lightStates = new bool[sceneLights.Length];
            for (int i = 0; i < sceneLights.Length; i++)
            {
                lightStates[i] = sceneLights[i].enabled;
                sceneLights[i].enabled = false;
            }
            ambientColor = RenderSettings.ambientLight;
            reflectionIntensity = RenderSettings.reflectionIntensity;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.reflectionIntensity = 0f;
            blackoutView = progression.PlayerBody.GetComponentInChildren<Camera>();
            backgroundColor = blackoutView.backgroundColor;
            cullingMask = blackoutView.cullingMask;
            clearFlags = blackoutView.clearFlags;
            // The camera clears to the night sky, so the blackout must clear to black instead.
            blackoutView.clearFlags = CameraClearFlags.SolidColor;
            blackoutView.backgroundColor = Color.black;
            blackoutView.cullingMask = 0;
        }

        private void RestoreLights()
        {
            if (lightStates == null)
                return;

            for (int i = 0; i < sceneLights.Length; i++)
                if (sceneLights[i] != null)
                    sceneLights[i].enabled = lightStates[i];
            RenderSettings.ambientLight = ambientColor;
            RenderSettings.reflectionIntensity = reflectionIntensity;
            if (blackoutView != null)
            {
                blackoutView.backgroundColor = backgroundColor;
                blackoutView.cullingMask = cullingMask;
                blackoutView.clearFlags = clearFlags;
            }
            lightStates = null;
        }

        // Cues stay silent until a clip is assigned.
        private static void PlayCue(AudioSource source, AudioClip clip, float volume)
        {
            if (source != null && clip != null)
                source.PlayOneShot(clip, volume);
        }

        private void StageCompleted(int stage)
        {
            if (stage == 6)
                fadingOutdoors = true;
        }
    }
}
