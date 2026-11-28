using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Brix.Engine;
using UnityEngine;

namespace BetterThanCastle
{
	// Local HUD customisation of the vanilla screen-space UI (minimap, unit panel, chat, top/bottom bars...): per-panel scale, opacity,
	// hidden flag and position offset, driven by the UI editor (elements "ui:<panel key>", BtcUiEdit). Panels are discovered at runtime
	// (the HUD is built by Lua, names are not fixed): walk root screen-space canvases, descend through full-stretch containers, every
	// non-stretched RectTransform below them is a "panel". Defaults are a strict no-op. The old file <config>/interface.txt (scale/alpha
	// from the Visuals tab) is read once by the editor migration (LegacyValues). Env BTC_UIDUMP=1 logs the canvas hierarchy.
	public static class BtcInterface
	{
		private const string File_ = "interface.txt";

		/// <summary>Slider callback used by the legacy DrawSettings of several features (rendered by BtcVisualRows.SliderCb).</summary>
		public delegate float SliderFn(string label, int id, float v, float mn, float mx, float st, string fmt, string suf);

		private sealed class Panel
		{
			public string key;
			public string label;
			public RectTransform t;
			public Canvas canvas;
			public Vector3 origScale, lastScale;
			public Vector2 origPos, lastPos;
			public float curScale = 1f;
			public Vector2 curOff;
			public CanvasGroup cg;
			public bool addedCg;
			public float origAlpha;
			public bool origRaycast;
			public bool origInteract;
			public bool applied;
			public bool seen;
		}

		/// <summary>Editor values: scale, alpha, offset x px, offset y px (down), hidden (1/0).</summary>
		private static readonly Dictionary<string, float[]> settings = new Dictionary<string, float[]>();
		private static readonly Dictionary<string, Panel> panels = new Dictionary<string, Panel>();
		private static readonly List<Panel> list = new List<Panel>();
		private static readonly List<string> removeKeys = new List<string>();
		private static readonly StringBuilder sb = new StringBuilder();
		private static readonly Vector3[] corners = new Vector3[4];
		private static bool anyNonDefault;
		private static float nextDiscover;
		private static float uiOpenUntil;
		private static string lastLog = string.Empty;

		public static bool Active { get { return anyNonDefault; } }

		/// <summary>Bumped whenever the discovered panel list changes (the UI editor rebuilds its element list).</summary>
		internal static int Version;

		/// <summary>Old Visuals-tab values (key -> {scale, alpha}) from interface.txt, for the one-time UI editor migration.</summary>
		internal static Dictionary<string, float[]> LegacyValues()
		{
			Dictionary<string, float[]> d = new Dictionary<string, float[]>();
			try
			{
				string p = BtcPaths.Config(File_);
				if (!File.Exists(p)) return d;
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
					if (!isScale && !k.EndsWith(".alpha")) continue;
					string key = k.Substring(0, k.Length - 6);
					float[] s;
					if (!d.TryGetValue(key, out s)) { s = new float[] { 1f, 1f }; d[key] = s; }
					if (isScale) s[0] = Mathf.Clamp(v, 0f, 2f); else s[1] = Mathf.Clamp01(v);
				}
			}
			catch (Exception e) { BtcLog.Error("Interface", "legacy load: " + e.Message); }
			return d;
		}

		/// <summary>UI editor pushes the values of one panel (called from BtcUiEdit.Apply).</summary>
		internal static void Set(string key, bool en, float scale, float alpha, float dx, float dy)
		{
			float[] s;
			if (!settings.TryGetValue(key, out s)) { s = new float[5]; settings[key] = s; }
			s[0] = scale; s[1] = alpha; s[2] = dx; s[3] = dy; s[4] = en ? 0f : 1f;
		}

		internal static void ClearSettings() { settings.Clear(); }

		internal static void Recompute()
		{
			bool any = false;
			foreach (KeyValuePair<string, float[]> kv in settings) if (!IsDef(kv.Value)) { any = true; break; }
			anyNonDefault = any;
		}

		private static bool IsDef(float[] s)
		{
			return Mathf.Abs(s[0] - 1f) < 0.001f && Mathf.Abs(s[1] - 1f) < 0.001f && Mathf.Abs(s[2]) < 0.5f && Mathf.Abs(s[3]) < 0.5f && s[4] < 0.5f;
		}

		/// <summary>Keeps discovery running while the UI editor shows the panels.</summary>
		internal static void Touch() { uiOpenUntil = Time.realtimeSinceStartup + 2f; }

		internal static int Count { get { return list.Count; } }
		internal static string KeyAt(int i) { return list[i].key; }
		internal static string LabelAt(int i) { return list[i].label; }

		// world-following markers, tooltips and menus that open on demand are not HUD panels one would place
		private static readonly string[] Skip = { "Compass", "OverheadDisplays", "LineHighlights", "ToolTipBuilder", "WorkshopUpdate", "MapSaverMenu", "EditorModeMessage" };

		private static readonly string[][] Names =
		{
			new[] { "chatbox", "Chat", "Чат" }, new[] { "scoreboard", "Scoreboard", "Табло счёта" }, new[] { "centermessage", "Centre messages", "Сообщения по центру" },
			new[] { "taskbar", "Task bar", "Панель задач" }, new[] { "minimap", "Minimap", "Миникарта" }, new[] { "mini map", "Minimap", "Миникарта" },
		};

		/// <summary>Readable name of a panel (EN or RU), the object path when it is not a known one.</summary>
		internal static string NameOf(int i, bool ru)
		{
			string low = list[i].key.ToLowerInvariant();
			for (int k = 0; k < Names.Length; k++) if (low.Contains(Names[k][0])) return ru ? Names[k][2] : Names[k][1];
			return list[i].label;
		}

		internal static Vector2 PivotAt(int i)
		{
			RectTransform t = list[i].t;
			return t != null ? new Vector2(t.pivot.x, 1f - t.pivot.y) : new Vector2(0.5f, 0.5f);
		}

		/// <summary>Screen rect (y down) of a panel as vanilla lays it out, i.e. without our scale and offset.</summary>
		internal static bool NaturalRect(string key, out Rect r)
		{
			r = new Rect();
			Panel p;
			if (!panels.TryGetValue(key, out p) || p.t == null) return false;
			Camera cam = p.canvas != null && p.canvas.renderMode != RenderMode.ScreenSpaceOverlay ? p.canvas.worldCamera : null;
			p.t.GetWorldCorners(corners);
			float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
			for (int i = 0; i < 4; i++)
			{
				Vector2 sp = RectTransformUtility.WorldToScreenPoint(cam, corners[i]);
				float yy = Screen.height - sp.y;
				if (sp.x < x0) x0 = sp.x; if (sp.x > x1) x1 = sp.x;
				if (yy < y0) y0 = yy; if (yy > y1) y1 = yy;
			}
			Rect cur = new Rect(x0, y0, x1 - x0, y1 - y0);
			float s = Mathf.Max(0.01f, p.curScale);
			float px = p.t.pivot.x, py = 1f - p.t.pivot.y;
			float cx = cur.x + cur.width * px - p.curOff.x, cy = cur.y + cur.height * py - p.curOff.y;
			float w = cur.width / s, h = cur.height / s;
			r = new Rect(cx - w * px, cy - h * py, w, h);
			return true;
		}

		// ---- runtime ----
		public static void Tick()
		{
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
			Version++;
		}

		private static void Discover()
		{
			bool inGame = false;
			try { inGame = Architecte.commence; } catch (Exception) { }
			for (int i = 0; i < list.Count; i++) list[i].seen = false;
			int before = list.Count;
			bool added = false;
			if (inGame)
			{
				Canvas[] cs = UnityEngine.Object.FindObjectsOfType<Canvas>();
				for (int i = 0; i < cs.Length; i++)
				{
					Canvas c = cs[i];
					if (c == null || c.renderMode == RenderMode.WorldSpace || c.rootCanvas != c) continue;
					added |= Collect(c, c.transform, string.Empty, 0);
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
			if (added || removeKeys.Count > 0 || before != list.Count) Version++;
			list.Sort(CompareByKey);
			sb.Length = 0;
			for (int i = 0; i < list.Count; i++) sb.Append(list[i].key).Append(" | ");
			string cur = sb.ToString();
			if (cur != lastLog) { lastLog = cur; BtcLog.Info("Interface", "panels: " + cur); }
		}

		private static int CompareByKey(Panel a, Panel b)
		{
			return string.CompareOrdinal(a.key, b.key);
		}

		private static bool Collect(Canvas canvas, Transform t, string path, int depth)
		{
			bool added = false;
			for (int i = 0; i < t.childCount; i++)
			{
				Transform c = t.GetChild(i);
				RectTransform r = c as RectTransform;
				if (r == null || !c.gameObject.activeInHierarchy) continue;
				bool stretch = r.anchorMin.x < 0.01f && r.anchorMin.y < 0.01f && r.anchorMax.x > 0.99f && r.anchorMax.y > 0.99f;
				if (stretch)
				{
					if (depth < 3) added |= Collect(canvas, c, path + c.name + "/", depth + 1);
					continue;
				}
				Rect rc = r.rect;
				if (rc.width < 4f && rc.height < 4f) continue;
				if (BtcHudSlots.Owns(c)) continue;   // C# game menu regions have their own editor elements
				added |= Add(canvas, r, path + c.name);
			}
			return added;
		}

		private static bool Add(Canvas canvas, RectTransform r, string key)
		{
			key = key.Replace('=', '-').Replace('|', '-').Replace('\n', ' ').Replace('\r', ' ');
			for (int i = 0; i < Skip.Length; i++) if (key.IndexOf(Skip[i], StringComparison.Ordinal) >= 0) return false;
			Panel p;
			if (panels.TryGetValue(key, out p))
			{
				if (p.t == null || p.t.gameObject != r.gameObject) // re-created under the same name: re-bind
				{
					p.t = r; p.canvas = canvas;
					p.origScale = r.localScale; p.lastScale = r.localScale;
					p.origPos = r.anchoredPosition; p.lastPos = r.anchoredPosition;
					p.curScale = 1f; p.curOff = Vector2.zero;
					p.cg = null; p.addedCg = false; p.applied = false;
				}
				p.seen = true;
				return false;
			}
			p = new Panel();
			p.key = key;
			p.t = r;
			p.canvas = canvas;
			p.origScale = r.localScale; p.lastScale = r.localScale;
			p.origPos = r.anchoredPosition; p.lastPos = r.anchoredPosition;
			p.seen = true;
			string lab = key.Replace("Menu: ", string.Empty);
			p.label = lab.Length > 34 ? lab.Substring(lab.Length - 34) : lab;
			panels[key] = p;
			list.Add(p);
			return true;
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
			// vanilla (layout) rewrote the scale / position since our last write: take it as the new vanilla value
			if (p.applied && p.t.localScale != p.lastScale) p.origScale = p.t.localScale;
			if (p.applied && p.t.anchoredPosition != p.lastPos) p.origPos = p.t.anchoredPosition;
			bool hidden = s[4] > 0.5f || s[0] < 0.02f || s[1] < 0.01f;
			float k = 1f;
			if (p.t.parent != null) k = Mathf.Max(0.0001f, p.t.parent.lossyScale.x);
			if (p.canvas != null && p.canvas.renderMode != RenderMode.ScreenSpaceOverlay) k = Mathf.Max(0.0001f, p.canvas.scaleFactor);
			Vector3 want = hidden ? p.origScale : p.origScale * s[0];
			if (p.t.localScale != want) p.t.localScale = want;
			p.lastScale = want;
			p.curScale = hidden ? 1f : s[0];
			Vector2 off = hidden ? Vector2.zero : new Vector2(s[2], s[3]);
			Vector2 wantPos = p.origPos + new Vector2(off.x, -off.y) / k;
			if (p.t.anchoredPosition != wantPos) p.t.anchoredPosition = wantPos;
			p.lastPos = wantPos;
			p.curOff = off;
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
				if (p.t.anchoredPosition == p.lastPos) p.t.anchoredPosition = p.origPos;
				if (p.cg != null)
				{
					if (p.addedCg) UnityEngine.Object.Destroy(p.cg);
					else { p.cg.alpha = p.origAlpha; p.cg.blocksRaycasts = p.origRaycast; p.cg.interactable = p.origInteract; }
				}
			}
			p.cg = null; p.addedCg = false; p.applied = false;
			p.curScale = 1f; p.curOff = Vector2.zero;
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
