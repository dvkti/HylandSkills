using System;
using HylandSkills.Actives;
using HylandSkills.State;
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Experimental;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Skating;
using Il2CppScheduleOne.Tools;
using UnityEngine;

namespace HylandSkills.Effects
{
    /// <summary>MOBILITY: foot speed, double jump, skateboard tuning, Sprint Burst and Board Boost.</summary>
    public class MobilityModule : EffectModule
    {
        const string MoveLabel = "HylandSkills.Mobility";

        static readonly TrackedBase skate = new TrackedBase();
        static float appliedMove = 1f;
        static float refresh;
        static int airJumpsUsed;
        static float smokeUntil;
        static Skateboard board;

        public override void Reset()
        {
            skate.Clear();
            appliedMove = 1f;
            board = null;
            Movement()?.MoveSpeedMultiplierStack?.Remove(MoveLabel);
        }

        static PlayerMovement Movement() =>
            PlayerSingleton<PlayerMovement>.InstanceExists ? PlayerSingleton<PlayerMovement>.Instance : null;

        /// <summary>Smoke &amp; Mirrors combo: a short speed burst right after Vanish.</summary>
        public static void StartSmoke(float seconds) => smokeUntil = Time.time + seconds;

        public override void Update(float dt)
        {
            var pm = Movement();
            if (pm == null || Player.Local == null) return;
            DoubleJump(pm);

            refresh -= dt;
            if (refresh > 0) return;
            refresh = 0.2f;
            ApplyMove(pm);
            ApplySkate();
        }

        // ---------------------------------------------------------------- foot speed
        static bool CarryingProduct()
        {
            try
            {
                var inv = PlayerSingleton<PlayerInventory>.Instance;
                if (inv == null) return false;
                foreach (var slot in inv.hotbarSlots)
                    if (slot?.ItemInstance != null && slot.ItemInstance.TryCast<ProductItemInstance>() != null) return true;
            }
            catch { }
            return false;
        }

        static void ApplyMove(PlayerMovement pm)
        {
            float m = 1f + Skills.Pct("fleet_foot");
            if (pm.IsCrouched && Skills.Has("cat_burglar")) m *= 1f + Skills.Pct("cat_burglar");
            if (ActiveManager.IsRunning("sprint_burst")) m *= 1f + Data.SkillDb.Get("sprint_burst").Value / 100f;
            if (Time.time < smokeUntil) m *= 1.25f;
            if (Skills.Has("courier") && CarryingProduct()) m *= 1f + Skills.Pct("courier");
            if (Mathf.Approximately(m, appliedMove)) return;
            var stack = pm.MoveSpeedMultiplierStack;
            if (stack == null) return;
            stack.Remove(MoveLabel);
            if (!Mathf.Approximately(m, 1f)) stack.Add(new FloatStack.StackEntry(MoveLabel, m, FloatStack.EStackMode.Multiplicative, 51));
            appliedMove = m;
        }

        // ---------------------------------------------------------------- double jump
        static void DoubleJump(PlayerMovement pm)
        {
            if (pm.IsGrounded) { airJumpsUsed = 0; return; }
            if (!Skills.Has("double_jump") || GameInput.IsTyping || Player.Local.IsSkating || Player.Local.IsInVehicle) return;
            int maxJumps = Skills.Has("parkour") ? 2 : 1;
            if (airJumpsUsed >= maxJumps || pm.TimeAirborne < 0.12f) return;
            if (!GameInput.GetButtonDown(GameInput.ButtonCode.Jump)) return;

            float height = 1f;
            if (Skills.Has("parkour")) height *= 1.2f;
            if (Skills.Combo("free_runner") && ActiveManager.IsRunning("sprint_burst"))
            {
                height *= 1.5f;
                ActiveManager.ExtendRealWindow("sprint_burst", 1f);
            }
            float v = PlayerMovement.JumpForce * Mathf.Max(0.5f, PlayerMovement.JumpMultiplier) * height;
            float before = pm.movementY;
            pm.movementY = Mathf.Max(pm.movementY, v);
            airJumpsUsed++;
            if (Mod.Tracing) Mod.Debug($"[trace] Double jump #{airJumpsUsed}: movementY {before:0.##} -> {pm.movementY:0.##} (JumpForce {PlayerMovement.JumpForce}, mult {PlayerMovement.JumpMultiplier}, airborne {pm.TimeAirborne:0.##}s)");
        }

        // ---------------------------------------------------------------- skateboard
        static Skateboard CurrentBoard()
        {
            if (Player.Local == null || !Player.Local.IsSkating) return null;
            if (board != null && board.Rider == Player.Local) return board;
            board = null;
            foreach (var b in UnityEngine.Object.FindObjectsOfType<Skateboard>())
                if (b != null && b.Rider == Player.Local) { board = b; break; }
            return board;
        }

        static void Tune(SkateboardSettings s, string field, float current, Func<float, float> f, Action<float> write)
        {
            float v = skate.Scale(new IntPtr(s.Pointer.ToInt64() ^ field.GetHashCode()), current, f);
            if (!Mathf.Approximately(v, current)) write(v);
        }

        static void ApplySkate()
        {
            var b = CurrentBoard();
            var s = b != null ? b.CurentSettings : null;
            if (s == null) return;
            float top = 1f + Skills.Pct("board_feel") + Skills.Pct("street_surfer");
            float push = 1f + Skills.Pct("push_power");
            float jump = 1f + Skills.Pct("kickflip");
            // Air Control steadies the board: weaker lean force in the air, stronger pull back to level
            float air = Skills.Has("air_control") ? 1f - Skills.Pct("air_control") : 1f;
            float level = Skills.Has("air_control") ? 1f + Skills.Pct("air_control") : 1f;
            float turn = Skills.Has("air_control") ? 1f - Skills.Pct2("air_control") : 1f;
            float forward = Skills.Combo("big_air") ? 1.3f : 1f;
            if (Mod.Tracing && Time.frameCount % 300 == 0) Mod.Debug($"[trace] Skate: top {s.TopSpeed_Kmh:0.#} push {s.PushForceMultiplier:0.##} jump {s.JumpForce:0.##}");
            Tune(s, "top", s.TopSpeed_Kmh, x => x * top, v => s.TopSpeed_Kmh = v);
            Tune(s, "push", s.PushForceMultiplier, x => x * push, v => s.PushForceMultiplier = v);
            Tune(s, "jump", s.JumpForce, x => x * jump, v => s.JumpForce = v);
            Tune(s, "air", s.AirMovementForce, x => x * air, v => s.AirMovementForce = v);
            Tune(s, "level", s.RotationClampForce, x => x * level, v => s.RotationClampForce = v);
            Tune(s, "turn", s.TurnForce, x => x * turn, v => s.TurnForce = v);
            Tune(s, "fwd", s.JumpForwardBoost, x => x * forward, v => s.JumpForwardBoost = v);
            float delay = Skills.Has("street_surfer") ? 0.5f : 1f;
            Tune(s, "delay", s.PushDelay, x => x * delay, v => s.PushDelay = v);
        }

        /// <summary>Board Boost: instant forward kick while riding.</summary>
        public static bool Boost(float kmh)
        {
            var b = CurrentBoard();
            if (b == null || b.Rb == null) return false;
            var fwd = b.transform.forward;
            fwd.y = 0;
            b.Rb.AddForce(fwd.normalized * (kmh / 3.6f), ForceMode.VelocityChange);
            return true;
        }

        public static bool IsSkating => Player.Local != null && Player.Local.IsSkating;
    }
}
