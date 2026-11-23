using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectR
{
    // Player options, saved between sessions: master volume, ambience ("sound"), effects and brightness.
    // Looping sources count as ambience, one-shots as effects. Scripts that fade a source themselves
    // register it as driven and multiply their own target by AmbienceVolume.
    public static class GameSettings
    {
        private const string MasterKey = "ProjectR.MasterVolume";
        private const string AmbienceKey = "ProjectR.AmbienceVolume";
        private const string EffectsKey = "ProjectR.EffectsVolume";
        private const string BrightnessKey = "ProjectR.Brightness";

        public const float MinBrightness = -1.5f;
        public const float MaxBrightness = 1.5f;

        private static readonly Dictionary<AudioSource, float> baseVolumes = new();
        private static readonly HashSet<AudioSource> driven = new();
        private static ColorAdjustments exposure;
        private static bool loaded;

        public static float MasterVolume { get; private set; } = 1f;
        public static float AmbienceVolume { get; private set; } = 1f;
        public static float EffectsVolume { get; private set; } = 1f;
        public static float Brightness { get; private set; }

        public static void Load()
        {
            if (loaded)
                return;
            loaded = true;
            MasterVolume = PlayerPrefs.GetFloat(MasterKey, 1f);
            AmbienceVolume = PlayerPrefs.GetFloat(AmbienceKey, 1f);
            EffectsVolume = PlayerPrefs.GetFloat(EffectsKey, 1f);
            Brightness = PlayerPrefs.GetFloat(BrightnessKey, 0f);
        }

        public static void Save() => PlayerPrefs.Save();

        public static void SetMasterVolume(float value)
        {
            MasterVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MasterKey, MasterVolume);
            AudioListener.volume = MasterVolume;
        }

        public static void SetAmbienceVolume(float value)
        {
            AmbienceVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(AmbienceKey, AmbienceVolume);
            ApplySourceVolumes();
        }

        public static void SetEffectsVolume(float value)
        {
            EffectsVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(EffectsKey, EffectsVolume);
            ApplySourceVolumes();
        }

        public static void SetBrightness(float value)
        {
            Brightness = Mathf.Clamp(value, MinBrightness, MaxBrightness);
            PlayerPrefs.SetFloat(BrightnessKey, Brightness);
            ApplyBrightness();
        }

        // Scripts that set a source's volume every frame call this so the scene-wide scaling leaves it alone.
        public static void MarkDriven(AudioSource source)
        {
            if (source != null)
                driven.Add(source);
        }

        // Called once per scene load: remember every source's authored volume, then apply all options.
        public static void ApplyToScene()
        {
            Load();
            driven.RemoveWhere(s => s == null);
            baseVolumes.Clear();
            foreach (AudioSource source in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!driven.Contains(source))
                    baseVolumes[source] = source.volume;
            exposure = null;
            AudioListener.volume = MasterVolume;
            ApplySourceVolumes();
            ApplyBrightness();
        }

        private static void ApplySourceVolumes()
        {
            foreach (var pair in baseVolumes)
                if (pair.Key != null)
                    pair.Key.volume = pair.Value * (pair.Key.loop ? AmbienceVolume : EffectsVolume);
        }

        // Brightness is a global post-exposure override on top of the scene's own volumes.
        private static void ApplyBrightness()
        {
            if (exposure == null)
            {
                var go = new GameObject("Brightness Volume");
                var volume = go.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 1000f;
                volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
                exposure = volume.profile.Add<ColorAdjustments>(true);
            }
            exposure.postExposure.overrideState = true;
            exposure.postExposure.value = Brightness;
        }
    }
}
