using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;

namespace HylandSkills
{
    /// <summary>
    /// Safe access to IL2CPP static fields. Writing a <c>const</c> (literal) field through interop corrupts memory and
    /// kills the game, so every write is checked against the field's metadata first.
    /// </summary>
    public static class Il2CppStatics
    {
        const int FieldAttributeLiteral = 0x40;
        static readonly Dictionary<string, bool> writable = new Dictionary<string, bool>();

        public static bool IsWritable<T>(string field) => IsWritable(typeof(T), field);

        public static bool IsWritable(Type type, string field)
        {
            string key = type.FullName + "::" + field;
            if (writable.TryGetValue(key, out var ok)) return ok;
            ok = false;
            try
            {
                IntPtr cls = Il2CppClassPointerStore.GetNativeClassPointer(type);
                IntPtr f = IL2CPP.GetIl2CppField(cls, field);
                if (f != IntPtr.Zero)
                {
                    int flags = IL2CPP.il2cpp_field_get_flags(f);
                    ok = (flags & FieldAttributeLiteral) == 0;
                }
            }
            catch (Exception e)
            {
                Mod.Log.Warning($"Static check {key} failed: {e.Message}");
            }
            writable[key] = ok;
            if (!ok) Mod.Debug($"{key} is a constant; it will not be written.");
            return ok;
        }

        /// <summary>Runs <paramref name="write"/> only when the field is a real static (not a compiled constant).</summary>
        public static bool TrySet<T>(string field, Action write)
        {
            if (!IsWritable<T>(field)) return false;
            write();
            return true;
        }
    }
}
