using System;
using System.Collections.Generic;
using Il2CppScheduleOne.Audio;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.UI.Phone.Delivery;
using Il2CppScheduleOne.UI.Phone.Messages;
using UnityEngine;
using UnityEngine.UI;

namespace HylandSkills.UI
{
    /// <summary>
    /// UI sounds borrowed from the game: the native button hover/click on every button, and clips from other
    /// native screens for app events (rank-up blip when learning, the new-mix jingle for a mastery, the message
    /// "sent" sound when equipping, the ATM "complete" sound when paying a reset). They play through one 2D source
    /// routed to the game's own mixer group, so they follow the player's volume settings.
    /// </summary>
    public static class Sfx
    {
        static ButtonSound template;
        static AudioSource source;
        static readonly Dictionary<string, (AudioClip clip, float volume)> clips = new Dictionary<string, (AudioClip, float)>();

        static ButtonSound Template
        {
            get
            {
                if (template != null) return template;
                try
                {
                    foreach (var b in Resources.FindObjectsOfTypeAll<ButtonSound>())
                    {
                        if (b != null && b._clickClip != null && b._audioSource != null) { template = b; break; }
                    }
                }
                catch (Exception e) { Mod.Debug("ButtonSound lookup: " + e.Message); }
                return template;
            }
        }

        /// <summary>Gives a uGUI button the native hover/click sounds.</summary>
        public static void Attach(Button button)
        {
            var t = Template;
            if (t == null || button == null || button.GetComponent<ButtonSound>() != null) return;
            try
            {
                var bs = button.gameObject.AddComponent<ButtonSound>();
                bs._clickClip = t._clickClip;
                bs._clickVolume = t._clickVolume;
                bs._hoverClip = t._hoverClip;
                bs._hoverVolume = t._hoverVolume;
                bs._audioSource = t._audioSource;
            }
            catch (Exception e) { Mod.Debug("ButtonSound attach: " + e.Message); }
        }

        static AudioSource Source()
        {
            if (source != null) return source;
            var go = new GameObject("HylandSkillsSfx");
            UnityEngine.Object.DontDestroyOnLoad(go);
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            try
            {
                var native = Template?._audioSource?._audioSource;
                if (native != null) source.outputAudioMixerGroup = native.outputAudioMixerGroup;
            }
            catch { }
            return source;
        }

        /// <summary>Finds (once) the clip of a native AudioSourceController on some game screen.</summary>
        static (AudioClip clip, float volume) Native<T>(string key, Func<T, AudioSourceController> pick) where T : UnityEngine.Object
        {
            if (clips.TryGetValue(key, out var hit) && hit.clip != null) return hit;
            (AudioClip, float) found = (null, 0f);
            try
            {
                foreach (var o in Resources.FindObjectsOfTypeAll<T>())
                {
                    var c = o != null ? pick(o) : null;
                    if (c == null) continue;
                    // screens that never opened yet (rank-up) have not run Awake: read the AudioSource directly
                    var src = c._audioSource != null ? c._audioSource : c.GetComponent<AudioSource>();
                    var clip = src != null ? src.clip : null;
                    if (clip == null) continue;
                    // their own screens play them quietly under other sounds; here they are the only sound, so
                    // they get the same level as the native button click
                    found = (clip, 1f);
                    break;
                }
            }
            catch (Exception e) { Mod.Debug($"Sound {key}: {e.Message}"); }
            clips[key] = found;
            Mod.Debug($"Sound {key}: {(found.Item1 != null ? found.Item1.name : "not found")}");
            return found;
        }

        static void PlayClip(AudioClip clip, float volume, float pitch = 1f)
        {
            if (clip == null) return;
            try
            {
                var s = Source();
                s.pitch = pitch;
                s.PlayOneShot(clip, volume);
            }
            catch { }
        }

        /// <summary>The native button click, re-pitched.</summary>
        static void Click(float pitch, float volumeScale)
        {
            var t = Template;
            if (t == null || t._clickClip == null) return;
            PlayClip(t._clickClip, Mathf.Max(0.05f, t._clickVolume) * volumeScale, pitch);
        }

        static void NativeOr(string key, (AudioClip clip, float volume) n, float scale, float pitch, float fallbackPitch)
        {
            if (n.clip != null) PlayClip(n.clip, n.volume * scale, pitch);
            else Click(fallbackPitch, 1.1f);
        }

        /// <summary>Learning or upgrading a skill: the rank-up blip, a little higher for each rank.</summary>
        public static void Learn(int rank = 1) =>
            NativeOr("blip", Native<RankUpCanvas>("blip", r => r.BlipSound), 0.9f, 1f + 0.06f * Mathf.Clamp(rank - 1, 0, 4), 1.25f);

        /// <summary>Choosing a Major Mastery: the new-mix discovery jingle.</summary>
        public static void Mastery()
        {
            var mix = Native<NewMixScreen>("newmix", m => m.sound);
            if (mix.clip != null) PlayClip(mix.clip, mix.volume * 0.8f);
            else NativeOr("rankup", Native<RankUpCanvas>("rankup", r => r.SoundEffect), 0.6f, 1f, 1.4f);
        }

        /// <summary>A tree reaches 24/24: the rank-up chime, softer.</summary>
        public static void TreeComplete() =>
            NativeOr("rankclick", Native<RankUpCanvas>("rankclick", r => r.ClickSound), 0.9f, 1.15f, 1.35f);

        /// <summary>An active skill goes into a slot: the Messages "sent" sound.</summary>
        public static void Equip() =>
            NativeOr("sent", Native<MessagesApp>("sent", m => m.MessageSentSound), 0.8f, 1f, 1.15f);

        public static void Unequip() => Click(0.85f, 1f);

        /// <summary>A paid skill reset: the ATM "transaction complete" sound.</summary>
        public static void Respec()
        {
            var atm = Native<ATMInterface>("atm", a => a.completeSound);
            if (atm.clip != null) PlayClip(atm.clip, atm.volume);
            else NativeOr("order", Native<DeliveryApp>("order", d => d.OrderSubmittedSound), 0.9f, 1f, 1f);
        }

        public static void Error() => Click(0.7f, 0.9f);
        public static void Activate() => Click(1.1f, 1f);
    }
}
