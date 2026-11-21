using System.Linq;
using DV.Utils;
using RealisticBrakes.Core;
using RealisticBrakes.Sim;
using UnityEngine;
using UnityEngine.Audio;

namespace RealisticBrakes.Controls
{
 internal sealed class EjectorSound : MonoBehaviour
    {
        private const string ClipName = "SteamRelease_01";
        private const float SmallVolume = 0.25f;
        private const float LargeVolume = 0.1f;
        private const float SmallPitch = 1.15f;
        private const float LargePitch = 0.9f;
        private const float FadeTime = 0.6f;

        // The chimney, from the engine's middle: up from the rail and back from the front coupler, m (inferred).
        private const float ChimneyHeight = 4.2f;
        private const float ChimneyBack = 2.5f;

        // Exponential backoff when the clip isn't loaded yet, s (1 -> 2 -> 4).
        private const float InitialLookBackoff = 1f;
        private const float MaxLookBackoff = 4f;

        private static AudioClip cachedClip;
        private static float nextClipLook;
        private static float clipLookBackoff = InitialLookBackoff;

        private TrainCar car;
        private AudioSource source;
        private bool searchedOnce;

 public static void Attach(TrainCar car)
        {
            if (car == null || car.GetComponent<EjectorSound>() != null)
                return;
            car.gameObject.AddComponent<EjectorSound>().car = car;
        }

        private void Update()
        {
            var target = 0f;
            var pitch = SmallPitch;
            if (Main.Active && car != null && car.brakeSystem != null && BrakeSets.TryGet(car.brakeSystem, out var air)
                && air.Brake.VacuumEngine is VacuumEngine engine && engine.Spec.SteamBrake)
            {
                var supply = Mathf.Clamp01((float)engine.Supply);
                var small = engine.PumpOn ? SmallVolume : 0f;
                var large = engine.CutIn && engine.Position == VacuumValvePosition.Release ? LargeVolume : 0f;
                target = (small + large) * supply * Mathf.Clamp(Main.Settings.EjectorVolume, 0, 200) / 100f;
                if (large > 0f)
                    pitch = LargePitch;
            }
            if (source == null)
            {
                if (target <= 0f)
                    return;
                source = Make();
                if (source == null)
                    return;
            }
            source.volume = Mathf.MoveTowards(source.volume, target, Time.deltaTime / FadeTime);
            source.pitch = Mathf.MoveTowards(source.pitch, pitch, Time.deltaTime);
            if (source.volume > 0f && !source.isPlaying)
                source.Play();
            else if (source.volume <= 0f && source.isPlaying)
                source.Stop();
        }

        private AudioClip FindClip()
        {
            if (cachedClip != null)
                return cachedClip;
            // A newly active engine gets one immediate look rather than waiting out an earlier engine's backoff.
            if (searchedOnce && Time.time < nextClipLook)
                return null;
            searchedOnce = true;
            cachedClip = Resources.FindObjectsOfTypeAll<AudioClip>().FirstOrDefault(c => c != null && c.name == ClipName);
            if (cachedClip != null)
            {
                clipLookBackoff = InitialLookBackoff;
                nextClipLook = 0f;
                return cachedClip;
            }
            nextClipLook = Time.time + clipLookBackoff;
            clipLookBackoff = Mathf.Min(clipLookBackoff * 2f, MaxLookBackoff);
            return null;
        }

        private AudioSource Make()
        {
            var clip = FindClip();
            if (clip == null)
                return null;
            var place = new GameObject("RealisticBrakes Ejector");
            place.transform.SetParent(car.transform, false);
            var half = car.InterCouplerDistance > 1f ? car.InterCouplerDistance / 2f : 6f;
            place.transform.localPosition = new Vector3(0f, ChimneyHeight, half - ChimneyBack);
            var made = place.AddComponent<AudioSource>();
            made.clip = clip;
            made.loop = true;
            made.playOnAwake = false;
            made.spatialBlend = 1f;
            made.rolloffMode = AudioRolloffMode.Logarithmic;
            made.minDistance = 2f;
            made.maxDistance = 120f;
            made.dopplerLevel = 0f;
            made.volume = 0f;
            made.pitch = SmallPitch;
            made.outputAudioMixerGroup = OutdoorsGroup();
            return made;
        }

        // The game's group for an engine's outside sounds, its steam sounds among them (the S282's air pump, blower, and
        // dynamo, jobs 2882 to 2889).
        private const string GroupName = "Outdoors";

        private static AudioMixerGroup outdoors;

        private static AudioMixerGroup OutdoorsGroup()
        {
            if (outdoors != null)
                return outdoors;
            var mix = SingletonBehaviour<AudioManager>.Instance != null ? SingletonBehaviour<AudioManager>.Instance.mix : null;
            outdoors = mix != null ? mix.FindMatchingGroups(GroupName).FirstOrDefault(g => g != null && g.name == GroupName) : null;
            if (outdoors == null)
                Main.Mod?.Logger.Warning($"The game's {GroupName} sound group wasn't found; the vacuum ejectors play outside the game's mixer");
            return outdoors;
        }
    }
}
