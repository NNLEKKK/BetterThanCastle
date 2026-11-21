using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Brix.Engine;
using UnityEngine;

namespace BetterThanCastle
{
	// Local HUD customisation (Overlays > Interface): per-panel scale (0..2, 0 = hidden) and opacity (0..1) of the
	// in-game screen-space UI. Panels are discovered at runtime (the HUD is built by Lua, names are not fixed):
	// walk root screen-space canvases, descend through full-stretch containers, every non-stretched RectTransform
	// below them is a "panel". Defaults (1, 1) are a strict no-op. Settings file: <config>/interface.txt.
	// Env BTC_UIDUMP=1 logs the canvas hierarchy (discovery aid).
	public static class BtcInterface
	{
		private const string File_ = "interface.txt";
		public static readonly string[] TabNames = { "Units", "Minimap", "Bottom bar", "Top bar", "Other" };
		public static int Tab;

		private sealed class Panel
		{
			public string key;
			public string label;
			public int tab;
			public Transform t;
			public Vector3 origScale;
			public Vector3 lastScale;
			public CanvasGroup cg;
			public bool addedCg;
			public float origAlpha;
			public bool origRaycast;
			public bool origInteract;
			public bool applied;
			public bool seen;
		}

		private static readonly Dictionary<string, float[]> settings = new Dictionary<string, float[]>(); // key -> {scale, alpha}
		private static readonly Dictionary<string, Panel> panels = new Dictionary<string, Panel>();
		private static readonly List<Panel> list = new List<Panel>();
		private static readonly List<string> removeKeys = new List<string>();
		private static readonly StringBuilder sb = new StringBuilder();
		private static bool loaded;
		private static bool anyNonDefault;
		private static float nextDiscover;
		private static float uiOpenUntil;
		private static string lastLog = string.Empty;

		public static bool Active { get { return anyNonDefault; } }

		public static void Load()
		{
			if (loaded) return;
			loaded = true;
			try
			{
				string p = BtcPaths.Config(File_);
				if (!File.Exists(p)) return;
				string[] lines = File.ReadAllLines(p);
				for (int i = 0; i < lines.Length; i++)
				{
					string l = lines[i];
					if (l.Length == 0 || l[0] == '#') continue;
					int eq = l.LastIndexOf('=');
					if (eq <= 0) continue;
					string k = l.Substring(0, eq);
					float v;
					if (!float.TryParse(l.Substring(eq + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) continue;
					bool isScale = k.EndsWith(".scale");
					bool isAlpha = k.EndsWith(".alpha");
					if (!isScale && !isAlpha) continue;
					string key = k.Substring(0, k.Length - 6);
					float[] s = Get(key);
					if (isScale) s[0] = Mathf.Clamp(v, 0f, 2f); else s[1] = Mathf.Clamp01(v);
				}
				Recompute();
			}
			catch (Exception e)
			{
				BtcLog.Error("Interface", "load: " + e.Message);
			}
		}

		private static float[] Get(string key)
		{
			float[] s;
			if (!settings.TryGetValue(key, out s)) { s = new float[] { 1f, 1f }; settings[key] = s; }
			return s;
		}

		private static bool IsDef(float[] s)
		{
			return Mathf.Approximately(s[0], 1f) && Mathf.Approximately(s[1], 1f);
		}

		private static void Recompute()
		{
			bool any = false;
			foreach (KeyValuePair<string, float[]> kv in settings) if (!IsDef(kv.Value)) { any = true; break; }
			anyNonDefault = any;
		}

		private static void Save()
		{
			try
			{
				List<string> lines = new List<string>();
				lines.Add("# Better Than Castle: HUD panels (local visual). <panel>.scale 0..2 (0 = hidden), <panel>.alpha 0..1; 1/1 = vanilla");
				foreach (KeyValuePair<string, float[]> kv in settings)
				{
					if (IsDef(kv.Value)) continue;
					lines.Add(kv.Key + ".scale=" + kv.Value[0].ToString("0.###", CultureInfo.InvariantCulture));
					lines.Add(kv.Key + ".alpha=" + kv.Value[1].ToString("0.###", CultureInfo.InvariantCulture));
				}
				File.WriteAllLines(BtcPaths.Config(File_), lines.ToArray());
			}
			catch (Exception e)
			{
				BtcLog.Error("Interface", "save: " + e.Message);
			}
		}

		// ---- UI (called from the Overlays tab, IMGUI thread; no per-call allocations beyond label strings) ----
		public delegate float SliderFn(string label, int id, float v, float mn, float mx, float st, string fmt, string suf);

		// Draws the sliders of one sub-tab (tab 0 = unit plates is drawn by BtcUnitPlates). Returns false when the tab has no panels.
		public static bool DrawTab(SliderFn slider, int tab)
		{
			Load();
			uiOpenUntil = Time.realtimeSinceStartup + 2f;
			bool ru = BtcLang.Russian;
			bool chg = false;
			bool any = false;
			for (int i = 0; i < list.Count; i++)
			{
				Panel p = list[i];
				if (p.tab != tab || p.t == null) continue;
				any = true;
				float[] s = Get(p.key);
				float a = slider(p.label + (ru ? " - масштаб" : " - scale"), 9600 + i * 2, s[0], 0f, 2f, 0.05f, "0.00", string.Empty);
				if (Mathf.Abs(a - s[0]) > 0.0005f) { s[0] = a; chg = true; }
				float b = slider(p.label + (ru ? " - прозрачность" : " - opacity"), 9601 + i * 2, s[1], 0f, 1f, 0.05f, "0.00", string.Empty);
				if (Mathf.Abs(b - s[1]) > 0.0005f) { s[1] = b; chg = true; }
			}
			if (chg) { Recompute(); Save(); }
			return any;
		}

		// ---- runtime ----
		public static void Tick()
		{
			if (!loaded) Load();
			float now = Time.realtimeSinceStartup;
			if (dumpEnv) DumpTick(now);
			bool uiOpen = now < uiOpenUntil;
			if (!anyNonDefault && !uiOpen)
			{
				if (list.Count > 0) ClearAll();
				return;
			}
			if (now >= nextDiscover)
			{
				nextDiscover = now + (uiOpen ? 1f : 2f);
				try { Discover(); }
				catch (Exception e) { BtcLog.Error("Interface", "discover: " + e); nextDiscover = now + 30f; }
			}
			for (int i = 0; i < list.Count; i++) Apply(list[i]);
		}

		private static void ClearAll()
		{
			for (int i = 0; i < list.Count; i++) Restore(list[i]);
			list.Clear();
			panels.Clear();
		}

		private static void Discover()
		{
			bool inGame = false;
			try { inGame = Architecte.commence; } catch (Exception) { }
			for (int i = 0; i < list.Count; i++) list[i].seen = false;
			if (inGame)
			{
				Canvas[] cs = UnityEngine.Object.FindObjectsOfType<Canvas>();
				for (int i = 0; i < cs.Length; i++)
				{
					Canvas c = cs[i];
					if (c == null || c.renderMode == RenderMode.WorldSpace || c.rootCanvas != c) continue;
					Collect(c.transform, string.Empty, 0);
				}
			}
			// drop vanished panels (restore is moot when the object is gone)
			removeKeys.Clear();
			for (int i = 0; i < list.Count; i++)
				if (!list[i].seen || list[i].t == null) removeKeys.Add(list[i].key);
			if (removeKeys.Count > 0)
			{
				for (int i = 0; i < removeKeys.Count; i++)
				{
					Panel p;
					if (panels.TryGetValue(removeKeys[i], out p)) { if (p.t != null) Restore(p); panels.Remove(removeKeys[i]); list.Remove(p); }
				}
			}
			list.Sort(CompareByKey);
			sb.Length = 0;
			for (int i = 0; i < list.Count; i++) sb.Append(list[i].tab).Append(':').Append(list[i].key).Append(" | ");
			string cur = sb.ToString();
			if (cur != lastLog) { lastLog = cur; BtcLog.Info("Interface", "panels: " + cur); }
		}

		private static int CompareByKey(Panel a, Panel b)
		{
			return string.CompareOrdinal(a.key, b.key);
		}

		private static void Collect(Transform t, string path, int depth)
		{
			for (int i = 0; i < t.childCount; i++)
			{
				Transform c = t.GetChild(i);
				RectTransform r = c as RectTransform;
				if (r == null || !c.gameObject.activeInHierarchy) continue;
				bool stretch = r.anchorMin.x < 0.01f && r.anchorMin.y < 0.01f && r.anchorMax.x > 0.99f && r.anchorMax.y > 0.99f;
				if (stretch)
				{
					if (depth < 3) Collect(c, path + c.name + "/", depth + 1);
					continue;
				}
				Rect rc = r.rect;
				if (rc.width < 4f && rc.height < 4f) continue;
				Add(r, path + c.name);
			}
		}

		private static void Add(RectTransform r, string key)
		{
			key = key.Replace('=', '-').Replace('\n', ' ').Replace('\r', ' ');
			Panel p;
			if (panels.TryGetValue(key, out p))
			{
				if (p.t == null || p.t.gameObject != r.gameObject) // re-created under the same name: re-bind
				{
					p.t = r;
					p.origScale = r.localScale; p.lastScale = r.localScale;
					p.cg = null; p.addedCg = false; p.applied = false;
				}
				p.seen = true;
				return;
			}
			p = new Panel();
			p.key = key;
			p.t = r;
			p.origScale = r.localScale;
			p.lastScale = r.localScale;
			p.seen = true;
			string lab = key.Replace("Menu: ", string.Empty);
			p.label = lab.Length > 34 ? lab.Substring(lab.Length - 34) : lab;
			string low = key.ToLowerInvariant();
			float cy = (r.anchorMin.y + r.anchorMax.y) * 0.5f;
			if (low.Contains("minimap") || low.Contains("mini map")) p.tab = 1;
			else if (cy < 0.25f) p.tab = 2;
			else if (cy > 0.75f) p.tab = 3;
			else p.tab = 4;
			panels[key] = p;
			list.Add(p);
		}

		private static void Apply(Panel p)
		{
			if (p.t == null) return;
			float[] s;
			if (!settings.TryGetValue(p.key, out s) || IsDef(s))
			{
				if (p.applied) Restore(p);
				return;
			}
			// vanilla (layout) rewrote the scale since our last write: take it as the new vanilla value
			if (p.applied && p.t.localScale != p.lastScale) p.origScale = p.t.localScale;
			bool hidden = s[0] < 0.02f || s[1] < 0.01f;
			Vector3 want = hidden ? p.origScale : p.origScale * s[0];
			if (p.t.localScale != want) p.t.localScale = want;
			p.lastScale = want;
			if (p.cg == null)
			{
				p.cg = p.t.GetComponent<CanvasGroup>();
				if (p.cg == null) { p.cg = p.t.gameObject.AddComponent<CanvasGroup>(); p.addedCg = true; }
				p.origAlpha = p.cg.alpha; p.origRaycast = p.cg.blocksRaycasts; p.origInteract = p.cg.interactable;
			}
			float a = hidden ? 0f : s[1] * p.origAlpha;
			if (p.cg.alpha != a) p.cg.alpha = a;
			bool rc = !hidden && p.origRaycast;
			if (p.cg.blocksRaycasts != rc) p.cg.blocksRaycasts = rc;
			bool ia = !hidden && p.origInteract;
			if (p.cg.interactable != ia) p.cg.interactable = ia;
			p.applied = true;
		}

		private static void Restore(Panel p)
		{
			if (p.t != null && p.applied)
			{
				if (p.t.localScale == p.lastScale) p.t.localScale = p.origScale;
				if (p.cg != null)
				{
					if (p.addedCg) UnityEngine.Object.Destroy(p.cg);
					else { p.cg.alpha = p.origAlpha; p.cg.blocksRaycasts = p.origRaycast; p.cg.interactable = p.origInteract; }
				}
			}
			p.cg = null; p.addedCg = false; p.applied = false;
		}

		// ---- discovery aid (env BTC_UIDUMP) ----
		private static readonly bool dumpEnv = Environment.GetEnvironmentVariable("BTC_UIDUMP") != null;
		private static float nextDump = 30f;
		private static int dumps;

		private static void DumpTick(float now)
		{
			if (dumps >= 12 || now < nextDump) return;
			nextDump = now + 30f;
			dumps++;
			try
			{
				Canvas[] cs = UnityEngine.Object.FindObjectsOfType<Canvas>();
				for (int i = 0; i < cs.Length; i++)
				{
					if (cs[i].rootCanvas != cs[i]) continue;
					StringBuilder b = new StringBuilder();
					Walk(cs[i].transform, 0, b);
					BtcLog.Info("UIDump", "#" + dumps + " canvas " + cs[i].name + " mode=" + cs[i].renderMode + " ord=" + cs[i].sortingOrder + " :: " + b);
				}
			}
			catch (Exception e) { BtcLog.Error("UIDump", e.ToString()); }
		}

		private static void Walk(Transform t, int d, StringBuilder b)
		{
			if (d > 3) return;
			for (int i = 0; i < t.childCount; i++)
			{
				Transform c = t.GetChild(i);
				RectTransform r = c as RectTransform;
				b.Append(d).Append(':').Append(c.name).Append(c.gameObject.activeSelf ? "" : "(off)");
				if (r != null) b.Append('[').Append(r.anchorMin.x.ToString("0.##")).Append(',').Append(r.anchorMin.y.ToString("0.##")).Append('-').Append(r.anchorMax.x.ToString("0.##")).Append(',').Append(r.anchorMax.y.ToString("0.##")).Append(" sz").Append((int)r.rect.width).Append('x').Append((int)r.rect.height).Append(']');
				b.Append(" | ");
				Walk(c, d + 1, b);
			}
		}
	}
}
