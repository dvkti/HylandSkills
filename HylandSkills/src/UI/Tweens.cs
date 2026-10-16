using System;
using System.Collections.Generic;
using UnityEngine;

namespace HylandSkills.UI
{
    /// <summary>
    /// Tiny tween runner for the app's UI animations, ticked from <see cref="SkillsApp.Tick"/> with unscaled time
    /// (the phone works while the game is paused). A new tween with the same key replaces the running one.
    /// </summary>
    public static class Tweens
    {
        sealed class Tween
        {
            public object Key;
            public float Delay, Duration, Time;
            public Action<float> Step;
            public Action Done;
        }

        static readonly List<Tween> running = new List<Tween>();

        public static void Run(object key, float duration, Action<float> step, float delay = 0f, Action done = null)
        {
            if (key != null) running.RemoveAll(t => Equals(t.Key, key));
            var tw = new Tween { Key = key, Delay = delay, Duration = Mathf.Max(0.01f, duration), Step = step, Done = done };
            running.Add(tw);
            if (delay <= 0f) Safe(tw, 0f);
        }

        public static void Update(float dt)
        {
            for (int i = running.Count - 1; i >= 0; i--)
            {
                if (i >= running.Count) continue;
                var tw = running[i];
                if (tw.Delay > 0f) { tw.Delay -= dt; continue; }
                tw.Time += dt;
                float k = Mathf.Clamp01(tw.Time / tw.Duration);
                if (!Safe(tw, k) || k >= 1f)
                {
                    running.Remove(tw);
                    if (k >= 1f) try { tw.Done?.Invoke(); } catch { }
                }
            }
        }

        public static void Clear() => running.Clear();

        public static bool IsRunning(object key) => running.Exists(t => Equals(t.Key, key));

        static bool Safe(Tween tw, float k)
        {
            try { tw.Step(k); return true; }
            catch { return false; }
        }

        // ---------------- easing
        public static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);
        public static float EaseInOut(float t) => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;

        /// <summary>0 → peak → 0 bump with a small settle, for "pop" feedback.</summary>
        public static float Punch(float t) => Mathf.Sin(t * Mathf.PI) * (1f - t * 0.35f);

        /// <summary>Scale pop on a RectTransform around its resting scale.</summary>
        public static void Pop(RectTransform rt, float amount = 0.12f, float duration = 0.22f, float delay = 0f, float rest = 1f)
        {
            if (rt == null) return;
            Run(rt.GetInstanceID(), duration, k =>
            {
                if (rt == null) return;
                rt.localScale = Vector3.one * (rest + amount * Punch(k));
            }, delay, () => { if (rt != null) rt.localScale = Vector3.one * rest; });
        }
    }
}
