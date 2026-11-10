using System;
using UnityEngine;

namespace BetterThanCastle
{
	// Shared look for all BTC menu tabs (matches the Resource Packs menu: dark panel, green accent, flat rows).
	internal static class BtcUiStyle
	{
		public static Color Accent { get { return BtcMenu.Accent; } }
		public static readonly Color Panel = new Color(0.06f, 0.06f, 0.08f, 0.93f);
		public static readonly Color Text = new Color(1f, 1f, 1f, 0.85f);
		public static readonly Color Dim = new Color(1f, 1f, 1f, 0.55f);
		public static readonly Color Line = new Color(1f, 1f, 1f, 0.12f);
		public static readonly Color Hover = new Color(1f, 1f, 1f, 0.08f);

		private static Texture2D _white;
		private sealed class S
		{
			public GUIStyle button, tab, tabActive, toggle, header, note, label, field, area, sliderBg, thumb, checkRow;
		}

		private static readonly System.Collections.Generic.Dictionary<int, S> _cache = new System.Collections.Generic.Dictionary<int, S>();

		public static Texture2D White
		{
			get
			{
				if (_white == null)
				{
					_white = Make(1, 1, (x, y) => Color.white);
				}
				return _white;
			}
		}

		private static Texture2D Make(int w, int h, Func<int, int, Color> f)
		{
			Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
			Color[] c = new Color[w * h];
			for (int y = 0; y < h; y++)
			{
				for (int x = 0; x < w; x++)
				{
					c[y * w + x] = f(x, y);
				}
			}
			t.SetPixels(c);
			t.Apply();
			t.hideFlags = HideFlags.HideAndDontSave;
			t.wrapMode = TextureWrapMode.Clamp;
			return t;
		}

		private static Texture2D Solid(Color c)
		{
			return Make(1, 1, (x, y) => c);
		}

		private static void Kill(Texture2D t)
		{
			if (t != null)
			{
				UnityEngine.Object.Destroy(t);
			}
		}

		private static Texture2D Box(int s, bool on)
		{
			// s x s box, plus 2 transparent columns (stretched part of the toggle background).
			return Make(s + 2, s, (x, y) =>
			{
				if (x >= s)
				{
					return new Color(0f, 0f, 0f, 0f);
				}
				if (x < 1 || y < 1 || x >= s - 1 || y >= s - 1)
				{
					return new Color(1f, 1f, 1f, 0.7f);
				}
				if (on && x >= 3 && y >= 3 && x < s - 3 && y < s - 3)
				{
					return Accent;
				}
				return new Color(0.06f, 0.06f, 0.08f, 1f);
			});
		}

		private static void Flat(GUIStyle st, Texture2D n, Texture2D h, Texture2D a, Color tn, Color th)
		{
			st.normal.background = n;
			st.hover.background = h;
			st.active.background = a;
			st.focused.background = n;
			// "on" states are used by GUI.SelectionGrid/Toolbar for the selected entry; the skin default is black
			st.onNormal.background = a;
			st.onHover.background = a;
			st.onActive.background = a;
			st.onFocused.background = a;
			st.onNormal.textColor = th;
			st.onHover.textColor = th;
			st.onActive.textColor = th;
			st.onFocused.textColor = th;
			st.normal.textColor = tn;
			st.hover.textColor = th;
			st.active.textColor = th;
			st.focused.textColor = tn;
		}

		private static S Get(int fs)
		{
			S z;
			if (_cache.TryGetValue(fs, out z) && z.button != null && z.field.normal.background != null)
			{
				return z;
			}
			z = new S();
			_cache[fs] = z;
			Texture2D _btnN = Solid(new Color(1f, 1f, 1f, 0.07f));
			Texture2D _btnH = Solid(new Color(1f, 1f, 1f, 0.15f));
			Texture2D _btnA = Solid(BtcMenu.AccentA(0.3f));
			Texture2D _fieldBg = Solid(new Color(0.02f, 0.02f, 0.03f, 0.9f));
			int s = Mathf.Max(12, Mathf.RoundToInt(fs * 1.1f));
			Texture2D _boxOff = Box(s, false);
			Texture2D _boxOn = Box(s, true);

			z.button = new GUIStyle(GUI.skin.button);
			z.button.alignment = TextAnchor.MiddleCenter;
			z.button.fontSize = fs;
			z.button.border = new RectOffset(0, 0, 0, 0);
			z.button.margin = new RectOffset(0, 0, 0, 0);
			z.button.padding = new RectOffset(4, 4, 0, 0);
			z.button.richText = true;
			Flat(z.button, _btnN, _btnH, _btnA, Text, Color.white);

			z.tab = new GUIStyle(z.button);
			z.tab.padding = new RectOffset(2, 2, 0, 0);
			Flat(z.tab, null, null, null, Text, Color.white);
			z.tabActive = new GUIStyle(z.tab);
			z.tabActive.fontStyle = FontStyle.Bold;
			Flat(z.tabActive, null, null, null, Accent, Accent);

			z.checkRow = new GUIStyle(z.button);
			z.checkRow.alignment = TextAnchor.MiddleLeft;
			z.checkRow.padding = new RectOffset(Mathf.RoundToInt(fs * 2.2f), fs, 0, 0);
			Flat(z.checkRow, null, null, null, Text, Color.white);

			z.toggle = new GUIStyle(GUI.skin.toggle);
			z.toggle.fontSize = fs;
			z.toggle.alignment = TextAnchor.MiddleLeft;
			z.toggle.border = new RectOffset(s, 0, 0, 0);
			z.toggle.padding = new RectOffset(s + Mathf.RoundToInt(fs * 0.5f), 0, 0, 0);
			z.toggle.margin = new RectOffset(0, 0, 0, 0);
			z.toggle.overflow = new RectOffset(0, 0, 0, 0);
			z.toggle.richText = true;
			z.toggle.fixedHeight = s; // square box: the background must not stretch vertically with the row height
			z.toggle.normal.background = _boxOff;
			z.toggle.hover.background = _boxOff;
			z.toggle.active.background = _boxOff;
			z.toggle.focused.background = _boxOff;
			z.toggle.onNormal.background = _boxOn;
			z.toggle.onHover.background = _boxOn;
			z.toggle.onActive.background = _boxOn;
			z.toggle.onFocused.background = _boxOn;
			z.toggle.normal.textColor = Text;
			z.toggle.hover.textColor = Color.white;
			z.toggle.active.textColor = Color.white;
			z.toggle.focused.textColor = Text;
			z.toggle.onNormal.textColor = Text;
			z.toggle.onHover.textColor = Color.white;
			z.toggle.onActive.textColor = Color.white;
			z.toggle.onFocused.textColor = Text;

			z.label = new GUIStyle(GUI.skin.label);
			z.label.alignment = TextAnchor.MiddleLeft;
			z.label.fontSize = fs;
			z.label.padding = new RectOffset(0, 0, 0, 0);
			z.label.richText = true;
			z.label.normal.textColor = Text;

			z.header = new GUIStyle(z.label);
			z.header.fontStyle = FontStyle.Bold;
			z.header.normal.textColor = new Color(Accent.r, Accent.g, Accent.b, 0.95f);

			z.note = new GUIStyle(z.label);
			z.note.fontSize = Mathf.Max(10, Mathf.RoundToInt(fs * 0.85f));
			z.note.wordWrap = true;
			z.note.alignment = TextAnchor.UpperLeft;
			z.note.normal.textColor = Dim;

			z.field = new GUIStyle(GUI.skin.textField);
			z.field.fontSize = fs;
			z.field.alignment = TextAnchor.MiddleLeft;
			z.field.clipping = TextClipping.Clip;
			z.field.border = new RectOffset(0, 0, 0, 0);
			z.field.padding = new RectOffset(6, 6, 0, 0);
			z.field.normal.background = _fieldBg;
			z.field.hover.background = _fieldBg;
			z.field.focused.background = _fieldBg;
			z.field.active.background = _fieldBg;
			z.field.normal.textColor = Text;
			z.field.hover.textColor = Text;
			z.field.focused.textColor = Color.white;
			z.field.active.textColor = Color.white;

			z.area = new GUIStyle(z.field);
			z.area.wordWrap = true;
			z.area.alignment = TextAnchor.UpperLeft;

			z.sliderBg = new GUIStyle(GUI.skin.horizontalSlider);
			z.sliderBg.normal.background = Solid(new Color(1f, 1f, 1f, 0.22f));
			z.sliderBg.border = new RectOffset(0, 0, 0, 0);
			z.sliderBg.fixedHeight = 4f;
			z.thumb = new GUIStyle(GUI.skin.horizontalSliderThumb);
			z.thumb.normal.background = Solid(Color.white);
			z.thumb.hover.background = Solid(Accent);
			z.thumb.active.background = Solid(Accent);
			z.thumb.border = new RectOffset(0, 0, 0, 0);
			z.thumb.fixedWidth = Mathf.Max(6f, fs * 0.5f);
			z.thumb.fixedHeight = Mathf.Max(12f, fs);
			return z;
		}

		public static GUIStyle Button(int fs) { return Get(fs).button; }
		public static GUIStyle Tab(int fs) { return Get(fs).tab; }
		public static GUIStyle TabActive(int fs) { return Get(fs).tabActive; }
		public static GUIStyle Toggle(int fs) { return Get(fs).toggle; }
		public static GUIStyle Header(int fs) { return Get(fs).header; }
		public static GUIStyle Note(int fs) { return Get(fs).note; }
		public static GUIStyle Label(int fs) { return Get(fs).label; }
		public static GUIStyle Field(int fs) { return Get(fs).field; }
		public static GUIStyle Area(int fs) { return Get(fs).area; }
		public static GUIStyle SliderBg(int fs) { return Get(fs).sliderBg; }
		public static GUIStyle SliderThumb(int fs) { return Get(fs).thumb; }

		// Menu-look checkbox row: hover background + checkbox + label. Returns true on click.
		public static bool CheckRow(Rect row, bool on, string label, int fs)
		{
			S z = Get(fs);
			Event e = Event.current;
			if (e.type == EventType.Repaint)
			{
				Color old = GUI.color;
				if (row.Contains(e.mousePosition))
				{
					GUI.color = Hover;
					GUI.DrawTexture(row, White);
				}
				float s = fs * 1.1f;
				Rect box = new Rect(row.x + fs * 0.5f, row.y + (row.height - s) * 0.5f, s, s);
				GUI.color = new Color(1f, 1f, 1f, 0.7f);
				GUI.DrawTexture(box, White);
				GUI.color = new Color(0.06f, 0.06f, 0.08f, 1f);
				GUI.DrawTexture(new Rect(box.x + 2f, box.y + 2f, box.width - 4f, box.height - 4f), White);
				if (on)
				{
					GUI.color = Accent;
					GUI.DrawTexture(new Rect(box.x + 4f, box.y + 4f, box.width - 8f, box.height - 8f), White);
				}
				GUI.color = old;
			}
			return GUI.Button(row, label, z.checkRow);
		}

		public static void HeaderLabel(Rect r, string text, int fs)
		{
			S z = Get(fs);
			if (Event.current.type == EventType.Repaint)
			{
				Color old = GUI.color;
				GUI.color = Line;
				GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1f), White);
				GUI.color = old;
			}
			GUI.Label(r, text, z.header);
		}
	}
}
