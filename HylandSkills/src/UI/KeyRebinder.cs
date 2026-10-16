using System;
using HylandSkills.Core;
using UnityEngine;

namespace HylandSkills.UI
{
    /// <summary>Waits for the next key press and assigns it to an active slot.</summary>
    public static class KeyRebinder
    {
        public static bool IsListening { get; private set; }
        public static int Slot { get; private set; } = -1;
        static Action onDone;
        static int startFrame;

        static readonly KeyCode[] candidates = BuildCandidates();

        static KeyCode[] BuildCandidates()
        {
            var all = (KeyCode[])Enum.GetValues(typeof(KeyCode));
            return Array.FindAll(all, k =>
                k != KeyCode.None && k != KeyCode.Escape && k != KeyCode.Mouse0 && k != KeyCode.Mouse1 &&
                (int)k < (int)KeyCode.JoystickButton0);
        }

        public static void Listen(int slot, Action done)
        {
            Slot = slot;
            onDone = done;
            IsListening = true;
            startFrame = Time.frameCount;
            done?.Invoke();
        }

        public static void Cancel()
        {
            if (!IsListening) return;
            IsListening = false;
            Slot = -1;
            var cb = onDone;
            onDone = null;
            cb?.Invoke();
        }

        /// <summary>Polled every frame from the mod's update loop.</summary>
        public static void Update()
        {
            if (!IsListening || Time.frameCount == startFrame) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { Cancel(); return; }
            foreach (var k in candidates)
            {
                if (!Input.GetKeyDown(k)) continue;
                // A key can only drive one slot: swap with whichever slot already uses it.
                for (int i = 0; i < Prefs.SlotKeys.Length; i++)
                    if (i != Slot && Prefs.SlotKeys[i].Value == k) Prefs.SlotKeys[i].Value = Prefs.SlotKeys[Slot].Value;
                Prefs.SlotKeys[Slot].Value = k;
                Prefs.Save();
                IsListening = false;
                Slot = -1;
                var cb = onDone;
                onDone = null;
                cb?.Invoke();
                State.Skills.NotifyChanged();
                Hud.Relayout();
                return;
            }
        }
    }
}
