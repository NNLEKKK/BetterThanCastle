#if DEV
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace BetterThanCastle.Dev
{
    /// <summary>DEV: "voxfields" logs the field layout of the voxel engine and the objects it holds (2 levels): type, value or
    /// collection size. Used to judge whether terrain keyframes can snapshot/restore the engine layers directly.</summary>
    internal static class BtcDevVoxFields
    {
        private const BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        internal static void Run()
        {
            object eng = Brix.Engine.Voxel.Engine;
            if (eng == null) { BtcLog.Info("VoxFields", "engine null"); return; }
            HashSet<object> seen = new HashSet<object>();
            Dump(eng, "eng", 0, seen);
        }

        private static void Dump(object o, string path, int depth, HashSet<object> seen)
        {
            if (o == null || depth > 3 || seen.Contains(o)) return;
            seen.Add(o);
            Type t = o.GetType();
            StringBuilder sb = new StringBuilder();
            sb.Append(path).Append(" : ").Append(t.FullName).Append(" {");
            List<KeyValuePair<string, object>> kids = new List<KeyValuePair<string, object>>();
            for (Type tt = t; tt != null && tt != typeof(object); tt = tt.BaseType)
            {
                foreach (FieldInfo f in tt.GetFields(All | BindingFlags.DeclaredOnly))
                {
                    object v = null;
                    try { v = f.GetValue(o); } catch (Exception) { }
                    sb.Append(' ').Append(f.Name).Append(':').Append(Short(f.FieldType)).Append('=').Append(Val(v)).Append(';');
                    if (v != null && !f.FieldType.IsPrimitive && !(v is string) && !f.FieldType.IsEnum && !(v is Delegate)
                        && (f.FieldType.Namespace == null || !f.FieldType.Namespace.StartsWith("UnityEngine")))
                        kids.Add(new KeyValuePair<string, object>(path + "." + f.Name, v));
                }
            }
            sb.Append(" }");
            BtcLog.Info("VoxFields", sb.Length > 1800 ? sb.ToString(0, 1800) + "..." : sb.ToString());
            foreach (KeyValuePair<string, object> kv in kids)
            {
                if (kv.Value is Array) continue;
                if (kv.Value is ICollection) continue;
                if (kv.Value.GetType().IsValueType && kv.Value.GetType().GetFields(All).Length < 4) continue;
                Dump(kv.Value, kv.Key, depth + 1, seen);
            }
        }

        private static string Short(Type t)
        {
            if (!t.IsGenericType) return t.Name;
            StringBuilder sb = new StringBuilder(t.Name.Substring(0, t.Name.IndexOf('`'))).Append('<');
            Type[] a = t.GetGenericArguments();
            for (int i = 0; i < a.Length; i++) sb.Append(i > 0 ? "," : "").Append(Short(a[i]));
            return sb.Append('>').ToString();
        }

        private static string Val(object v)
        {
            if (v == null) return "null";
            Array a = v as Array;
            if (a != null) return "[" + a.Length + "]";
            ICollection c = v as ICollection;
            if (c != null) return "#" + c.Count;
            if (v is string || v.GetType().IsPrimitive || v.GetType().IsEnum) return v.ToString();
            return "obj";
        }
    }
}
#endif
