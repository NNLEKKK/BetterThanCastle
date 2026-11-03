using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Brix.NewUI.OverheadDisplay;
using UnityEngine;
using UnityEngine.UI;

namespace BetterThanCastle
{
	// Local visual settings for the overhead name tag of units (vanilla NameTag in OverheadDisplay).
	// Plate = name text + its background, Icon = mode icon (NameTag.Badge incl. its Icon child).
	// Defaults (1,1) are a strict no-op. Settings file: <config>/unit-plates.txt.
	public static class BtcUnitPlates
	{
		private const string File_ = "unit-plates.txt";
		private const float Eps = 0.0001f;

		public static float PlateScale = 1f;
		public static float IconScale = 1f;

		private static bool loaded;
		private static bool anyApplied;
		private static bool dumped;

		private sealed class State
		{
			public NameTag tag;
			public Transform tagT;
			public Vector3 tagOrig;     // vanilla local scale of the tag root (re-captured if vanilla changes it)
			public Vector3 tagLast;     // what we wrote last
			public Image fill;          // black background = Image on the tag root
			public bool fillOrig;
			public Transform[] hide;    // name, context, arrow: hidden when the plate scale is 0
			public Vector3[] hideOrig;
			public Transform badge;
			public Vector3 badgeOrig;
			public bool applied;
		}

		private static readonly Dictionary<int, State> states = new Dictionary<int, State>();
		private static readonly List<int> dead = new List<int>();
		private static FieldInfo fName, fContext, fFill, fBadge, fContent;
		private static bool refl;

		public static bool AnyApplied { get { return anyApplied; } }
		public static bool IsDefaultFast { get { return loaded && IsDefault; } }

		public static bool IsDefault
		{
			get { return Mathf.Approximately(PlateScale, 1f) && Mathf.Approximately(IconScale, 1f); }
		}

		public static void Load()
		{
			if (loaded) return;
			loaded = true;
			PlateScale = Mathf.Clamp(RF("plate.scale", 1f), 0f, 2f);
			IconScale = Mathf.Clamp(RF("icon.scale", 1f), 0f, 2f);
		}

		private static float RF(string k, float d)
		{
			float v;
			return float.TryParse(BtcPaths.ReadKey(File_, k, d.ToString(CultureInfo.InvariantCulture)), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : d;
		}

		public static void Save()
		{
			try
			{
				string[] lines = new string[]
				{
					"# Better Than Castle: unit name plates (local visual). plate.scale/icon.scale 0..2; 1/1 = vanilla",
					"plate.scale=" + PlateScale.ToString("0.###", CultureInfo.InvariantCulture),
					"icon.scale=" + IconScale.ToString("0.###", CultureInfo.InvariantCulture)
				};
				File.WriteAllLines(BtcPaths.Config(File_), lines);
			}
			catch (Exception e)
			{
				BtcLog.Error("UnitPlates", "save: " + e.Message);
			}
		}

		// Called from the Overlays tab (slider(label,id,value,min,max,step,format,suffix)).
		public delegate float SliderFn(string label, int id, float v, float mn, float mx, float st, string fmt, string suf);

		public static void DrawSettings(SliderFn slider)
		{
			Load();
			bool ru = BtcLang.Russian;
			bool chg = false;
			float a = slider(ru ? "Масштаб плашки имени" : "Name plate scale", 9501, PlateScale, 0f, 2f, 0.05f, "0.00", string.Empty);
			if (Mathf.Abs(a - PlateScale) > 0.0005f) { PlateScale = a; chg = true; }
			float b = slider(ru ? "Масштаб иконки" : "Icon scale", 9502, IconScale, 0f, 2f, 0.05f, "0.00", string.Empty);
			if (Mathf.Abs(b - IconScale) > 0.0005f) { IconScale = b; chg = true; }
			if (chg) Save();
		}

		// Postfix of OverheadDisplayDriver.BrixUpdate (Bricktron drivers only).
		public static void Apply(OverheadDisplay od)
		{
			if (!loaded) Load();
			bool def = IsDefault;
			if (def && !anyApplied) return;
			if (od == null) return;
			if (!od.NameTag.IsSpawned) return;
			NameTag tag = od.NameTag.Get;
			if (tag == null) return;
			int id = tag.GetInstanceID();
			State st;
			if (!states.TryGetValue(id, out st))
			{
				if (def) return;
				st = Build(tag);
				if (st == null) return;
				states[id] = st;
			}
			if (def)
			{
				Restore(st);
				return;
			}
			Write(st);
		}

		// Restores every cached tag when settings went back to default, and drops dead entries.
		public static void Tick()
		{
			if (states.Count == 0) { anyApplied = false; return; }
			bool def = IsDefault;
			dead.Clear();
			bool any = false;
			foreach (KeyValuePair<int, State> kv in states)
			{
				State s = kv.Value;
				if (s.tag == null) { dead.Add(kv.Key); continue; }
				if (def && s.applied) Restore(s);
				if (s.applied) any = true;
			}
			for (int i = 0; i < dead.Count; i++) states.Remove(dead[i]);
			anyApplied = any;
		}

		private static void Write(State s)
		{
			if (s.tagT == null) return;
			// vanilla rewrote the tag scale since our last write: take it as the new vanilla value
			if (s.applied && s.tagT.localScale != s.tagLast) s.tagOrig = s.tagT.localScale;
			bool zero = PlateScale < 0.02f;
			float p = zero ? 1f : PlateScale;
			float ic = IconScale < 0.02f ? Eps : IconScale / p;
			Vector3 ts3 = s.tagOrig * p;
			s.tagT.localScale = ts3;
			s.tagLast = ts3;
			if (s.fill != null) s.fill.enabled = !zero && s.fillOrig;
			for (int i = 0; i < s.hide.Length; i++)
				if (s.hide[i] != null) s.hide[i].localScale = zero ? s.hideOrig[i] * Eps : s.hideOrig[i];
			if (s.badge != null) s.badge.localScale = s.badgeOrig * ic;
			s.applied = true;
			anyApplied = true;
		}

		private static void Restore(State s)
		{
			if (s.tagT != null && s.tagT.localScale == s.tagLast) s.tagT.localScale = s.tagOrig;
			if (s.fill != null) s.fill.enabled = s.fillOrig;
			for (int i = 0; i < s.hide.Length; i++)
				if (s.hide[i] != null) s.hide[i].localScale = s.hideOrig[i];
			if (s.badge != null) s.badge.localScale = s.badgeOrig;
			s.applied = false;
		}

		private static T Fld<T>(FieldInfo f, object o) where T : class
		{
			return f == null ? null : f.GetValue(o) as T;
		}

		private static State Build(NameTag tag)
		{
			try
			{
				if (!refl)
				{
					refl = true;
					BindingFlags f = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
					Type t = typeof(NameTag);
					fName = t.GetField("Name", f);
					fContext = t.GetField("Context", f);
					fFill = t.GetField("FillImage", f);
					fBadge = t.GetField("Badge", f);
					fContent = t.GetField("content", f);
				}
				Text name = Fld<Text>(fName, tag);
				Text ctx = Fld<Text>(fContext, tag);
				Image fill = Fld<Image>(fFill, tag);
				Image badge = Fld<Image>(fBadge, tag);
				RectTransform content = Fld<RectTransform>(fContent, tag);
				if (!dumped)
				{
					dumped = true;
					Dump(tag, name, ctx, fill, badge, content);
				}
				State s = new State();
				s.tag = tag;
				s.tagT = tag.transform;
				s.tagOrig = s.tagT.localScale;
				s.tagLast = s.tagOrig;
				if (fill == null) fill = tag.GetComponent<Image>();
				s.fill = fill;
				s.fillOrig = fill != null && fill.enabled;
				List<Transform> hide = new List<Transform>(3);
				if (name != null) hide.Add(name.transform);
				if (ctx != null) hide.Add(ctx.transform);
				Transform arrow = s.tagT.Find("SmallArrow");
				if (arrow != null) hide.Add(arrow);
				s.hide = hide.ToArray();
				s.hideOrig = new Vector3[s.hide.Length];
				for (int i = 0; i < s.hide.Length; i++) s.hideOrig[i] = s.hide[i].localScale;
				s.badge = badge != null ? badge.transform : null;
				s.badgeOrig = s.badge != null ? s.badge.localScale : Vector3.one;
				return s;
			}
			catch (Exception e)
			{
				BtcLog.Error("UnitPlates", "build: " + e);
				return null;
			}
		}

		private static void Dump(NameTag tag, Text name, Text ctx, Image fill, Image badge, RectTransform content)
		{
			try
			{
				System.Text.StringBuilder sb = new System.Text.StringBuilder();
				sb.Append("NameTag hierarchy: name=").Append(name != null ? name.name : "null");
				sb.Append(" badge=").Append(badge != null ? badge.name : "null");
				sb.Append(" fill=").Append(fill != null ? fill.name : "null");
				sb.Append(" content=").Append(content != null ? content.name : "null").Append(" | ");
				Graphic[] g = tag.GetComponentsInChildren<Graphic>(true);
				for (int i = 0; i < g.Length; i++)
				{
					Transform t = g[i].transform;
					string path = t.name;
					for (Transform p = t.parent; p != null && p != tag.transform.parent; p = p.parent) path = p.name + "/" + path;
					sb.Append(path).Append('[').Append(g[i].GetType().Name).Append("] ");
				}
				BtcLog.Info("UnitPlates", sb.ToString());
			}
			catch (Exception e)
			{
				BtcLog.Warn("UnitPlates", "dump: " + e.Message);
			}
		}
	}
}
