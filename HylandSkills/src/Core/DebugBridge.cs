using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HylandSkills.Actives;
using HylandSkills.State;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppScheduleOne.PlayerScripts;
using MelonLoader.Utils;
using UnityEngine;

namespace HylandSkills.Core
{
    /// <summary>
    /// Test bridge, only active with DebugLogging = true. Reads commands from UserData/HylandSkills.bridge.txt
    /// (the file is consumed), runs them on the main thread and appends results to UserData/HylandSkills.bridge.out.
    /// Commands: echo, wait &lt;sec&gt;, waitmin &lt;game min&gt;, con &lt;console command&gt;, rank &lt;id&gt; &lt;n&gt;, ranks [clear],
    /// eval &lt;expr&gt;, set &lt;expr&gt; = &lt;literal&gt;, each &lt;Type&gt; &lt;path&gt; | &lt;path&gt;..., save.
    /// Expressions: Type.Static.Member, find:Type[#i].Member, all:Type.Member, player.Member, Method(args), List[i].
    /// </summary>
    public static class DebugBridge
    {
        static string InPath => Path.Combine(MelonEnvironment.UserDataDirectory, "HylandSkills.bridge.txt");
        static string OutPath => Path.Combine(MelonEnvironment.UserDataDirectory, "HylandSkills.bridge.out");

        static readonly Queue<string> queue = new Queue<string>();
        static readonly Dictionary<string, Type> typeCache = new Dictionary<string, Type>();
        static float poll;
        static float waitUntil;
        static int waitUntilMinute = -1;

        public static void Update()
        {
            if (Prefs.DebugLogging == null || !Prefs.DebugLogging.Value) return;
            poll -= Time.unscaledDeltaTime;
            if (poll <= 0f)
            {
                poll = 0.25f;
                TryRead();
            }
            while (queue.Count > 0)
            {
                if (Time.unscaledTime < waitUntil) return;
                if (waitUntilMinute >= 0 && ActiveManager.GameNow < waitUntilMinute) return;
                waitUntilMinute = -1;
                string line = queue.Dequeue().Trim();
                try { Run(line); }
                catch (Exception e) { Out($"ERR {line}: {e.GetBaseException().GetType().Name}: {e.GetBaseException().Message}"); }
            }
        }

        static void TryRead()
        {
            try
            {
                if (!File.Exists(InPath)) return;
                var lines = File.ReadAllLines(InPath);
                File.Delete(InPath);
                foreach (var l in lines) queue.Enqueue(l);
            }
            catch { /* file still being written; retry next poll */ }
        }

        public static void Out(string msg)
        {
            Mod.Log.Msg("[bridge] " + msg);
            try { File.AppendAllText(OutPath, $"[{DateTime.Now:HH:mm:ss} g{ActiveManager.GameNow}] {msg}\n"); } catch { }
        }

        static void Run(string line)
        {
            if (line.Length == 0 || line.StartsWith("#")) return;
            int sp = line.IndexOf(' ');
            string cmd = sp < 0 ? line : line.Substring(0, sp);
            string rest = sp < 0 ? "" : line.Substring(sp + 1).Trim();
            switch (cmd)
            {
                case "echo": Out(rest); break;
                case "wait": waitUntil = Time.unscaledTime + float.Parse(rest, CultureInfo.InvariantCulture); break;
                case "waitmin": waitUntilMinute = ActiveManager.GameNow + int.Parse(rest); break;
                case "con":
                    Il2CppScheduleOne.Console.SubmitCommand(rest);
                    Out("con " + rest);
                    break;
                case "rank":
                {
                    var p = rest.Split(' ');
                    int n = int.Parse(p[1]);
                    if (n <= 0) Skills.Data.Ranks.Remove(p[0]); else Skills.Data.Ranks[p[0]] = n;
                    Skills.NotifyChanged();
                    Out($"rank {p[0]} = {n}");
                    break;
                }
                case "ranks":
                    if (rest == "clear") { Skills.Data.Ranks.Clear(); Skills.NotifyChanged(); }
                    Out("ranks: " + string.Join(", ", Skills.Data.Ranks.Select(kv => kv.Key + "=" + kv.Value)) +
                        " | synergies: " + string.Join(", ", Data.SkillDb.Synergies.Where(s => Skills.SynergyActive(s.Id)).Select(s => s.Id)));
                    break;
                case "eval": Eval(rest); break;
                case "set":
                {
                    int eq = rest.IndexOf(" = ", StringComparison.Ordinal);
                    Set(rest.Substring(0, eq).Trim(), rest.Substring(eq + 3).Trim());
                    break;
                }
                case "each":
                {
                    int s2 = rest.IndexOf(' ');
                    string type = rest.Substring(0, s2);
                    var paths = rest.Substring(s2 + 1).Split('|').Select(x => x.Trim()).ToArray();
                    var objs = FindAll(ResolveType(type));
                    Out($"each {type}: {objs.Count}");
                    for (int i = 0; i < objs.Count; i++)
                    {
                        var sb = new StringBuilder($"  #{i}");
                        foreach (var p in paths)
                        {
                            string v;
                            try { v = Format(Walk(objs[i], Split(p))); } catch (Exception e) { v = "ERR " + e.GetBaseException().Message; }
                            sb.Append($" | {p}={v}");
                        }
                        Out(sb.ToString());
                    }
                    break;
                }
                case "learn":
                {
                    var d = Data.SkillDb.Get(rest);
                    Out($"learn {rest}: {(d == null ? "unknown" : Skills.CanLearn(d) + " -> " + Skills.TryLearn(d))} (rank {Skills.Rank(rest)}, points {Skills.AvailablePoints})");
                    break;
                }
                case "equip":
                {
                    var p = rest.Split(' ');
                    ActiveManager.Equip(int.Parse(p[0]), p[1]);
                    Out($"equip {p[1]} in {p[0]}: cooldown {ActiveManager.CooldownLeft(p[1]):0.#}");
                    break;
                }
                case "activate":
                    Out($"activate {rest}: {ActiveManager.TryActivate(rest)} running {ActiveManager.IsRunning(rest)} left {ActiveManager.RunningLeft(rest):0.#} cooldown {ActiveManager.CooldownLeft(rest):0.#}");
                    break;
                case "ready":
                    Skills.Data.GameCooldownUntil.Remove(rest);
                    Skills.Data.RealCooldownLeft.Remove(rest);
                    Out("ready " + rest);
                    break;
                case "save":
                    Out("save requested: " + S1API.Internal.Abstraction.Saveable.RequestGameSave());
                    break;
                default: Out("unknown command: " + line); break;
            }
        }

        // ---------------------------------------------------------------- expressions
        static void Eval(string expr)
        {
            var segs = Split(expr);
            string root = segs[0];
            if (root.StartsWith("all:"))
            {
                var objs = FindAll(ResolveType(root.Substring(4)));
                Out($"{expr}: {objs.Count} objects");
                for (int i = 0; i < objs.Count; i++)
                {
                    string v;
                    try { v = Format(Walk(objs[i], segs.Skip(1).ToList())); } catch (Exception e) { v = "ERR " + e.GetBaseException().Message; }
                    Out($"  #{i} = {v}");
                }
                return;
            }
            Out($"{expr} = {Format(Resolve(segs))}");
        }

        static void Set(string expr, string literal)
        {
            var segs = Split(expr);
            object target;
            Type t;
            if (segs.Count == 2 && !IsRootObject(segs[0])) { target = null; t = ResolveType(segs[0]); }
            else
            {
                target = Resolve(segs.Take(segs.Count - 1).ToList());
                target = Downcast(target);
                t = target.GetType();
            }
            string name = segs[segs.Count - 1];
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;
            var prop = t.GetProperty(name, flags);
            if (prop != null) { prop.SetValue(target, ConvertArg(Arg(literal), prop.PropertyType)); }
            else
            {
                var f = t.GetField(name, flags) ?? throw new MissingMemberException(t.Name, name);
                f.SetValue(target, ConvertArg(Arg(literal), f.FieldType));
            }
            Out($"set {expr} = {literal}");
        }

        static bool IsRootObject(string seg) => seg.StartsWith("[") || seg.StartsWith("find:") || seg.StartsWith("res:") || seg.StartsWith("new:") || seg.StartsWith("list:") || seg == "player";

        static object Resolve(List<string> segs) => Resolve(segs, out _);

        static object Resolve(List<string> segs, out Type staticType)
        {
            staticType = null;
            string root = segs[0];
            object cur;
            int start = 1;
            if (root == "player") cur = Player.Local;
            else if (root.StartsWith("find:") || root.StartsWith("res:"))
            {
                bool res = root.StartsWith("res:");
                string tn = root.Substring(res ? 4 : 5);
                int idx = 0;
                string byName = null;
                int h = tn.IndexOf('#');
                int at = tn.IndexOf('@');
                if (h >= 0) { idx = int.Parse(tn.Substring(h + 1)); tn = tn.Substring(0, h); }
                else if (at >= 0) { byName = tn.Substring(at + 1); tn = tn.Substring(0, at); }
                var all = FindAll(ResolveType(tn), res);
                if (byName != null) cur = all.FirstOrDefault(o => o is UnityEngine.Object uo && uo.name == byName);
                else cur = idx < all.Count ? all[idx] : null;
            }
            else if (root.StartsWith("new:")) cur = Construct(root.Substring(4));
            else if (root.StartsWith("list:")) cur = BuildList(root.Substring(5));
            else
            {
                // [Full.Namespace.Type] lets a root name contain dots
                staticType = ResolveType(root.StartsWith("[") && root.EndsWith("]") ? root.Substring(1, root.Length - 2) : root);
                if (segs.Count == 1) return staticType;
                cur = Member(null, staticType, segs[1]);
                start = 2;
            }
            return Walk(cur, segs.Skip(start).ToList());
        }

        static object Walk(object cur, List<string> segs)
        {
            foreach (var s in segs)
            {
                if (cur == null) throw new NullReferenceException("null before ." + s);
                cur = Member(cur, cur.GetType(), s);
            }
            return cur;
        }

        static object Member(object obj, Type t, string seg)
        {
            // indexer suffix: Name[3]
            int br = seg.IndexOf('[');
            if (br > 0 && seg.EndsWith("]") && seg.IndexOf('(') < 0)
            {
                var coll = Member(obj, t, seg.Substring(0, br));
                int i = int.Parse(seg.Substring(br + 1, seg.Length - br - 2));
                return Index(coll, i);
            }
            var flags = BindingFlags.Public | BindingFlags.NonPublic | (obj == null ? BindingFlags.Static : BindingFlags.Instance) | BindingFlags.FlattenHierarchy;
            int par = seg.IndexOf('(');
            if (par > 0)
            {
                string name = seg.Substring(0, par);
                var args = SplitArgs(seg.Substring(par + 1, seg.Length - par - 2)).Select(Arg).ToArray();
                var m = t.GetMethods(flags).Where(x => x.Name == name && x.GetParameters().Length == args.Length)
                    .OrderByDescending(x => Score(x.GetParameters(), args)).FirstOrDefault();
                if (m == null && obj != null) { var d = Downcast(obj); if (d != obj) return Member(d, d.GetType(), seg); }
                if (m == null) throw new MissingMethodException(t.Name, name);
                var ps = m.GetParameters();
                return m.Invoke(obj, args.Select((a, i) => ConvertArg(a, ps[i].ParameterType)).ToArray());
            }
            var prop = t.GetProperty(seg, flags);
            if (prop != null && prop.GetIndexParameters().Length == 0) return prop.GetValue(obj);
            var f = t.GetField(seg, flags);
            if (f != null) return f.GetValue(obj);
            if (obj != null)
            {
                var d = Downcast(obj);
                if (d != obj) return Member(d, d.GetType(), seg);
            }
            throw new MissingMemberException(t.Name, seg);
        }

        static object Index(object coll, int i)
        {
            if (coll is IList l) return l[i];
            var t = coll.GetType();
            var getItem = t.GetMethod("get_Item", new[] { typeof(int) });
            if (getItem != null) return getItem.Invoke(coll, new object[] { i });
            throw new InvalidOperationException("not indexable: " + t.Name);
        }

        /// <summary>Casts an interop object to its most derived managed wrapper (Employee → Botanist).</summary>
        static object Downcast(object obj)
        {
            if (obj is not Il2CppObjectBase b) return obj;
            try
            {
                var il2 = b.Cast<Il2CppSystem.Object>().GetIl2CppType();
                string full = il2.FullName.Replace('+', '.');
                Type t = null;
                foreach (var cand in new[] { full, "Il2Cpp" + full, "Il2Cpp." + full })
                {
                    t = FindType(cand, exactFull: true);
                    if (t != null) break;
                }
                if (t == null || t == obj.GetType()) return obj;
                var m = typeof(Il2CppObjectBase).GetMethod("TryCast").MakeGenericMethod(t);
                return m.Invoke(b, null) ?? obj;
            }
            catch { return obj; }
        }

        static List<object> FindAll(Type t, bool resources = false)
        {
            var res = new List<object>();
            var arr = resources ? Resources.FindObjectsOfTypeAll(Il2CppType.From(t)) : UnityEngine.Object.FindObjectsOfType(Il2CppType.From(t));
            var cast = typeof(Il2CppObjectBase).GetMethod("TryCast").MakeGenericMethod(t);
            foreach (var o in arr)
            {
                var c = cast.Invoke(o, null);
                if (c != null) res.Add(c);
            }
            return res;
        }

        static Type ResolveType(string name)
        {
            name = name.Replace(':', '.'); // UnityEngine:Color, since dots split expressions
            var t = FindType(name, exactFull: true) ?? FindType(name, exactFull: false);
            return t ?? throw new TypeLoadException("type not found: " + name);
        }

        static Type FindType(string name, bool exactFull)
        {
            string key = (exactFull ? "F:" : "N:") + name;
            if (typeCache.TryGetValue(key, out var hit)) return hit;
            Type best = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types.Where(x => x != null).ToArray(); } catch { continue; }
                foreach (var t in types)
                {
                    string full = t.FullName?.Replace('+', '.');
                    bool match = exactFull ? full == name : (t.Name == name || (full != null && full.EndsWith("." + name)));
                    if (!match) continue;
                    if (best == null || (t.Namespace ?? "").StartsWith("Il2CppScheduleOne")) best = t;
                }
            }
            typeCache[key] = best;
            return best;
        }

        // ---------------------------------------------------------------- parsing and formatting
        static List<string> Split(string expr)
        {
            var res = new List<string>();
            var sb = new StringBuilder();
            int depth = 0;
            bool quote = false;
            foreach (char c in expr)
            {
                if (c == '"') quote = !quote;
                if (!quote && (c == '(' || c == '[')) depth++;
                if (!quote && (c == ')' || c == ']')) depth--;
                if (c == '.' && depth == 0 && !quote && !(sb.Length > 0 && char.IsDigit(sb[sb.Length - 1]) && sb.ToString().All(char.IsDigit)))
                {
                    res.Add(sb.ToString());
                    sb.Clear();
                    continue;
                }
                sb.Append(c);
            }
            if (sb.Length > 0) res.Add(sb.ToString());
            return res;
        }

        static List<string> SplitArgs(string s)
        {
            var res = new List<string>();
            if (string.IsNullOrWhiteSpace(s)) return res;
            var sb = new StringBuilder();
            bool quote = false;
            int depth = 0;
            foreach (char c in s)
            {
                if (c == '"') quote = !quote;
                if (!quote && (c == '(' || c == '[')) depth++;
                if (!quote && (c == ')' || c == ']')) depth--;
                if (c == ',' && !quote && depth == 0) { res.Add(sb.ToString().Trim()); sb.Clear(); continue; }
                sb.Append(c);
            }
            res.Add(sb.ToString().Trim());
            return res;
        }

        /// <summary>A call argument: a literal, or an expression (find:/res:/new:/player/Type.Member).</summary>
        static object Arg(string s)
        {
            if (s.Length > 0 && !s.StartsWith("\"") && (IsRootObject(Split(s)[0]) || (char.IsLetter(s[0]) && s.Contains('.'))))
                return Resolve(Split(s));
            return Literal(s);
        }

        /// <summary>Overload ranking: exact type matches first, then convertible primitives.</summary>
        static int Score(ParameterInfo[] ps, object[] args)
        {
            int score = 0;
            for (int i = 0; i < ps.Length; i++)
            {
                var a = args[i];
                var t = ps[i].ParameterType;
                if (a == null) score += t.IsValueType ? -100 : 1;
                else if (t.IsInstanceOfType(a)) score += 3;
                else if (a is IConvertible && (t.IsPrimitive || t.IsEnum || t == typeof(string))) score += 2;
                else if (a is Il2CppObjectBase && typeof(Il2CppObjectBase).IsAssignableFrom(t)) score += 1;
                else score -= 100;
            }
            return score;
        }

        /// <summary>list:Type(a, b, ...) builds an Il2CppSystem List&lt;Type&gt;.</summary>
        static object BuildList(string spec)
        {
            int par = spec.IndexOf('(');
            var t = ResolveType(spec.Substring(0, par));
            var lt = typeof(Il2CppSystem.Collections.Generic.List<>).MakeGenericType(t);
            var list = Activator.CreateInstance(lt);
            var add = lt.GetMethod("Add");
            foreach (var a in SplitArgs(spec.Substring(par + 1, spec.Length - par - 2)).Select(Arg))
                add.Invoke(list, new[] { ConvertArg(a, t) });
            return list;
        }

        static object Construct(string spec)
        {
            int par = spec.IndexOf('(');
            var t = ResolveType(spec.Substring(0, par));
            var args = SplitArgs(spec.Substring(par + 1, spec.Length - par - 2)).Select(Arg).ToArray();
            var ctor = t.GetConstructors().Where(c => c.GetParameters().Length == args.Length && c.GetParameters().All(p => p.ParameterType != typeof(IntPtr)))
                           .OrderByDescending(c => Score(c.GetParameters(), args)).FirstOrDefault()
                       ?? throw new MissingMethodException(t.Name, ".ctor/" + args.Length);
            var ps = ctor.GetParameters();
            return ctor.Invoke(args.Select((a, i) => ConvertArg(a, ps[i].ParameterType)).ToArray());
        }

        static object Literal(string s)
        {
            if (s == "null") return null;
            if (s == "true") return true;
            if (s == "false") return false;
            if (s.Length >= 2 && s.StartsWith("\"") && s.EndsWith("\"")) return s.Substring(1, s.Length - 2);
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) return i;
            if (float.TryParse(s.TrimEnd('f'), NumberStyles.Float, CultureInfo.InvariantCulture, out var f)) return f;
            return s;
        }

        static object ConvertArg(object a, Type t)
        {
            if (t.IsByRef) t = t.GetElementType(); // out/ref parameters: pass the value, the result is discarded
            if (a == null) return null;
            if (t.IsInstanceOfType(a)) return a;
            if (a is Il2CppObjectBase ob && typeof(Il2CppObjectBase).IsAssignableFrom(t))
                return typeof(Il2CppObjectBase).GetMethod("TryCast").MakeGenericMethod(t).Invoke(ob, null);
            if (t.IsEnum) return a is string s ? Enum.Parse(t, s) : Enum.ToObject(t, a);
            if (t == typeof(string)) return a.ToString();
            return Convert.ChangeType(a, t, CultureInfo.InvariantCulture);
        }

        static string Format(object v, int depth = 0)
        {
            switch (v)
            {
                case null: return "null";
                case string s: return "\"" + s + "\"";
                case float f: return f.ToString("0.####", CultureInfo.InvariantCulture);
                case double d: return d.ToString("0.####", CultureInfo.InvariantCulture);
                case Type ty: return "type " + ty.FullName;
                case UnityEngine.Object uo: return $"{v.GetType().Name}('{(uo != null ? uo.name : "destroyed")}')";
            }
            var t = v.GetType();
            if (t.IsPrimitive || t.IsEnum || t.IsValueType) return v.ToString();
            if (v is Il2CppScheduleOne.ItemFramework.ItemInstance item)
            {
                var q = item.TryCast<Il2CppScheduleOne.ItemFramework.QualityItemInstance>();
                return $"Item({item.ID} x{item.Quantity}{(q != null ? " " + q.Quality : "")})";
            }
            if (depth > 0) return t.Name;
            var count = t.GetProperty("Count");
            var getItem = t.GetMethod("get_Item", new[] { typeof(int) });
            if (count != null && getItem != null)
            {
                int n = Convert.ToInt32(count.GetValue(v));
                var items = new List<string>();
                for (int i = 0; i < Math.Min(n, 12); i++) items.Add(Format(getItem.Invoke(v, new object[] { i }), depth + 1));
                return $"{t.Name}[{n}] {{{string.Join(", ", items)}}}";
            }
            if (v is IEnumerable e)
            {
                var items = new List<string>();
                foreach (var x in e) { if (items.Count >= 12) break; items.Add(Format(x, depth + 1)); }
                return $"{t.Name} {{{string.Join(", ", items)}}}";
            }
            return t.Name + ": " + v;
        }
    }
}
