using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace BetterThanCastle
{
public static class BtcHotkeysUI
{
	public static void Draw(Event e, float w, float rowH, int fs, Color old)
	{
		BtcHotkeysUI.EnsureStyles(fs);
		bool flag = e.rawType == EventType.MouseUp;
		bool flag2 = flag;
		if (flag2)
		{
			BtcHotkeysUI._dragId = 0;
		}
		IList<BtcHotkeys.Def> defs = BtcHotkeys.Defs;
		bool flag3 = !BtcHotkeysUI._groupsInit;
		bool flag4 = flag3;
		if (flag4)
		{
			BtcHotkeysUI._groupsInit = true;
			IList<string> groups = BtcHotkeys.Groups;
			for (int i = 0; i < groups.Count; i++)
			{
				BtcHotkeysUI.Collapsed.Add(groups[i]);
			}
		}
		float num = 0f;
		bool enabled = BtcHotkeys.Enabled;
		bool flag5 = BtcHotkeysUI.CheckRow(e, ref num, w, rowH, fs, old, "Advanced Hotkeys (master switch)", enabled);
		bool flag6 = flag5;
		if (flag6)
		{
			BtcHotkeys.SetEnabled(!enabled);
		}
		BtcHotkeysUI.Note(ref num, w, fs, "Off: every function of this tab is disabled. Hotkeys never fire while this menu is open or the chat is focused.");
		BtcHotkeysUI.Note(ref num, w, fs, "Click a key field, then press the new key. Hold Ctrl / Shift / Alt first for a combo (the last key is the moment of the press). Press only a modifier to bind the modifier itself. Esc cancels.");
		BtcHotkeysUI.Note(ref num, w, fs, "LMB / RMB can be bound too (press the mouse button while a field is waiting). A bare click fires everywhere outside the game interface, so for LMB / RMB it is better to hold Ctrl / Shift / Alt.");
		BtcHotkeysUI.Note(ref num, w, fs, "Colors: yellow = changed from default, red = conflict (same keys as another function), orange = waiting for a key. Binds apply at once, but hotkeys are muted while this menu is open - close it to test.");
		float num2 = (w - (float)fs * 2f) * 0.5f;
		Rect r = new Rect((float)fs, num + rowH * 0.1f, num2 - 4f, rowH * 0.8f);
		Rect r2 = new Rect((float)fs + num2 + 4f, num + rowH * 0.1f, num2 - 4f, rowH * 0.8f);
		bool flag7 = BtcHotkeysUI._confirm == "clear" && Time.unscaledTime < BtcHotkeysUI._confirmUntil;
		bool flag8 = BtcHotkeysUI._confirm == "reset" && Time.unscaledTime < BtcHotkeysUI._confirmUntil;
		bool flag9 = BtcHotkeysUI.Button(e, r, flag7 ? "Click again to confirm" : "Unbind all functions", flag7 ? BtcHotkeysUI.Warn : Color.white, old);
		bool flag10 = flag9;
		if (flag10)
		{
			bool flag11 = flag7;
			bool flag12 = flag11;
			if (flag12)
			{
				BtcHotkeys.ClearAll();
				BtcHotkeysUI._confirm = null;
			}
			else
			{
				BtcHotkeysUI._confirm = "clear";
				BtcHotkeysUI._confirmUntil = Time.unscaledTime + 3f;
			}
		}
		bool flag13 = BtcHotkeysUI.Button(e, r2, flag8 ? "Click again to confirm" : "Reset everything to defaults", flag8 ? BtcHotkeysUI.Warn : Color.white, old);
		bool flag14 = flag13;
		if (flag14)
		{
			bool flag15 = flag8;
			bool flag16 = flag15;
			if (flag16)
			{
				BtcHotkeys.ResetAll();
				BtcHotkeysUI._confirm = null;
			}
			else
			{
				BtcHotkeysUI._confirm = "reset";
				BtcHotkeysUI._confirmUntil = Time.unscaledTime + 3f;
			}
		}
		num += rowH;
		int num3;
		for (int j = 0; j < defs.Count; j = num3)
		{
			string group = defs[j].group;
			num3 = j;
			int num4 = 0;
			int num5 = 0;
			while (num3 < defs.Count && defs[num3].group == group)
			{
				bool flag17 = defs[num3].kind == BtcHotkeys.Kind.Key;
				bool flag18 = flag17;
				if (flag18)
				{
					num5++;
					bool flag19 = BtcHotkeys.IsConflict(defs[num3].id);
					bool flag20 = flag19;
					if (flag20)
					{
						num4++;
					}
				}
				num3++;
			}
			bool flag21 = !BtcHotkeysUI.Collapsed.Contains(group);
			bool flag22 = BtcHotkeysUI.GroupHeader(e, ref num, w, rowH, fs, old, group, flag21, num4);
			bool flag23 = flag22;
			if (flag23)
			{
				bool flag24 = flag21;
				bool flag25 = flag24;
				if (flag25)
				{
					BtcHotkeysUI.Collapsed.Add(group);
				}
				else
				{
					BtcHotkeysUI.Collapsed.Remove(group);
				}
			}
			bool flag26 = flag21;
			bool flag27 = flag26;
			if (flag27)
			{
				string text = null;
				for (int k = j; k < num3; k++)
				{
					bool flag28 = !string.IsNullOrEmpty(defs[k].sub) && defs[k].sub != text;
					bool flag29 = flag28;
					if (flag29)
					{
						text = defs[k].sub;
						GUI.Label(new Rect((float)fs * 1.6f, num + rowH * 0.1f, w - (float)fs * 3f, rowH * 0.9f), BtcLang.Tr(text), BtcHotkeysUI._sub);
						num += rowH;
					}
					BtcHotkeysUI.DrawDef(e, ref num, w, rowH, fs, old, defs[k]);
				}
			}
		}
		BtcHotkeysUI.ContentH = num + rowH * 0.3f;
	}

	private static void DrawDef(Event e, ref float y, float w, float rowH, int fs, Color old, BtcHotkeys.Def d)
	{
		Rect row = new Rect(0f, y, w, rowH);
		y += rowH;
		BtcHotkeysUI.RowBackground(e, row, old);
		bool flag = d.kind == BtcHotkeys.Kind.Key && BtcHotkeys.IsConflict(d.id);
		bool flag2 = BtcHotkeys.IsDefault(d.id);
		GUI.Label(new Rect((float)fs * 1.6f, row.y, w * 0.47f - (float)fs * 1.6f, rowH), d.Title, BtcHotkeysUI._label);
		float x = w * 0.49f;
		float width = w * 0.29f;
		Rect r = new Rect(w * 0.79f, row.y + rowH * 0.12f, w * 0.09f, rowH * 0.76f);
		Rect r2 = new Rect(w * 0.885f, row.y + rowH * 0.12f, w * 0.09f, rowH * 0.76f);
		bool flag3 = d.kind == BtcHotkeys.Kind.Key;
		bool flag4 = flag3;
		if (flag4)
		{
			Rect r3 = new Rect(x, row.y + rowH * 0.1f, width, rowH * 0.8f);
			bool flag5 = BtcHotkeys.Capturing && BtcHotkeys.CaptureId == d.id;
			string text = flag5 ? BtcLang.Tr("Press keys... (Esc = cancel)") : BtcHotkeys.ComboText(d.id);
			Color color = flag5 ? BtcHotkeysUI.Listening : (flag ? BtcHotkeysUI.Warn : (flag2 ? Color.white : BtcHotkeysUI.Changed));
			bool flag6 = BtcHotkeysUI.Button(e, r3, text, color, old, true);
			bool flag7 = flag6;
			if (flag7)
			{
				bool flag8 = flag5;
				bool flag9 = flag8;
				if (flag9)
				{
					BtcHotkeys.CancelCapture();
				}
				else
				{
					BtcHotkeys.BeginCapture(d.id);
				}
			}
			bool flag10 = BtcHotkeys.GetKeys(d.id, null).Length == 0;
			bool flag11 = BtcHotkeysUI.Button(e, r, "Reset", flag2 ? new Color(1f, 1f, 1f, 0.3f) : Color.white, old) && !flag2;
			bool flag12 = flag11;
			if (flag12)
			{
				BtcHotkeys.ResetBind(d.id);
			}
			bool flag13 = BtcHotkeysUI.Button(e, r2, "Clear", flag10 ? new Color(1f, 1f, 1f, 0.3f) : Color.white, old) && !flag10;
			bool flag14 = flag13;
			if (flag14)
			{
				BtcHotkeys.Unbind(d.id);
			}
			bool flag15 = flag && e.type == EventType.Repaint;
			bool flag16 = flag15;
			if (flag16)
			{
				GUI.color = BtcHotkeysUI.Warn;
				GUI.DrawTexture(new Rect(row.x, row.y + rowH * 0.2f, 3f, rowH * 0.6f), BtcHotkeysUI.White());
				GUI.color = old;
			}
		}
		else if (d.kind == BtcHotkeys.Kind.Bool)
		{
			bool on = BtcHotkeys.GetBool(d.id, true);
			Rect box = new Rect(x, row.y, width, rowH);
			if (e.type == EventType.Repaint)
			{
				float sz = (float)fs * 1.1f;
				Rect position = new Rect(x, row.y + (rowH - sz) * 0.5f, sz, sz);
				GUI.color = new Color(1f, 1f, 1f, 0.7f);
				GUI.DrawTexture(position, BtcHotkeysUI.White());
				GUI.color = new Color(0.06f, 0.06f, 0.08f, 1f);
				GUI.DrawTexture(new Rect(position.x + 2f, position.y + 2f, position.width - 4f, position.height - 4f), BtcHotkeysUI.White());
				if (on)
				{
					GUI.color = BtcHotkeysUI.Accent;
					GUI.DrawTexture(new Rect(position.x + 4f, position.y + 4f, position.width - 8f, position.height - 8f), BtcHotkeysUI.White());
				}
				GUI.color = flag2 ? old : BtcHotkeysUI.Changed;
				GUI.Label(new Rect(x + sz + (float)fs * 0.5f, row.y, width - sz - (float)fs * 0.5f, rowH), BtcLang.Tr(on ? "On" : "Off"), BtcHotkeysUI._label);
				GUI.color = old;
			}
			if (GUI.Button(box, GUIContent.none, GUIStyle.none))
			{
				BtcHotkeys.ToggleBool(d.id);
			}
			if (BtcHotkeysUI.Button(e, r, "Reset", flag2 ? new Color(1f, 1f, 1f, 0.3f) : Color.white, old) && !flag2)
			{
				BtcHotkeys.ResetBind(d.id);
			}
		}
		else
		{
			bool flag17 = d.kind == BtcHotkeys.Kind.Mode;
			bool flag18 = flag17;
			if (flag18)
			{
				Rect r4 = new Rect(x, row.y + rowH * 0.1f, width, rowH * 0.8f);
				bool flag19 = BtcHotkeys.GetString(d.id, "hold").Equals("toggle", StringComparison.OrdinalIgnoreCase);
				bool flag20 = BtcHotkeysUI.Button(e, r4, BtcLang.Tr(flag19 ? "Toggle (press to switch on / off)" : "Hold (active while held)"), flag2 ? Color.white : BtcHotkeysUI.Changed, old, true);
				bool flag21 = flag20;
				if (flag21)
				{
					BtcHotkeys.ToggleMode(d.id);
				}
				bool flag22 = BtcHotkeysUI.Button(e, r, "Reset", flag2 ? new Color(1f, 1f, 1f, 0.3f) : Color.white, old) && !flag2;
				bool flag23 = flag22;
				if (flag23)
				{
					BtcHotkeys.ResetBind(d.id);
				}
			}
			else
			{
				float num = BtcHotkeys.GetFloat(d.id, 1f);
				Rect hit = new Rect(x, row.y, w * 0.22f, rowH);
				float num2 = BtcHotkeysUI.SliderCore(e, hit, d.id.GetHashCode() | 1, num, d.min, d.max, d.step, old);
				bool flag24 = Mathf.Abs(num2 - num) > 0.0001f;
				bool flag25 = flag24;
				if (flag25)
				{
					BtcHotkeys.SetFloat(d.id, num2);
					num = num2;
				}
				GUI.Label(new Rect(hit.xMax + (float)fs * 0.3f, row.y, w * 0.78f - hit.xMax - (float)fs * 0.3f, rowH), d.prefix + num.ToString(d.format, CultureInfo.InvariantCulture), BtcHotkeysUI._valueL);
				bool flag26 = BtcHotkeysUI.Button(e, r, "Reset", flag2 ? new Color(1f, 1f, 1f, 0.3f) : Color.white, old) && !flag2;
				bool flag27 = flag26;
				if (flag27)
				{
					BtcHotkeys.ResetBind(d.id);
				}
			}
		}
	}

	private static bool GroupHeader(Event e, ref float y, float w, float rowH, int fs, Color old, string title, bool open, int conflicts)
	{
		Rect rect = new Rect(0f, y, w, rowH);
		y += rowH;
		BtcHotkeysUI.RowBackground(e, rect, old);
		bool flag = e.type == EventType.Repaint;
		bool flag2 = flag;
		if (flag2)
		{
			GUI.color = new Color(1f, 1f, 1f, 0.12f);
			GUI.DrawTexture(new Rect((float)fs, rect.y + rowH * 0.1f, w - (float)fs * 2f, 1f), BtcHotkeysUI.White());
			GUI.color = old;
		}
		string text = (open ? "v  " : ">  ") + BtcLang.Tr(title);
		bool flag3 = conflicts > 0;
		bool flag4 = flag3;
		if (flag4)
		{
			text = text + "   (" + BtcLang.Tr("conflict") + ")";
		}
		GUIStyle header = BtcHotkeysUI._header;
		Color textColor = header.normal.textColor;
		header.normal.textColor = ((conflicts > 0) ? BtcHotkeysUI.Warn : textColor);
		bool result = GUI.Button(rect, text, header);
		header.normal.textColor = textColor;
		return result;
	}

	private static bool CheckRow(Event e, ref float y, float w, float rowH, int fs, Color old, string label, bool on)
	{
		Rect rect = new Rect(0f, y, w, rowH);
		y += rowH;
		BtcHotkeysUI.RowBackground(e, rect, old);
		bool flag = e.type == EventType.Repaint;
		bool flag2 = flag;
		if (flag2)
		{
			float num = (float)fs * 1.1f;
			Rect position = new Rect((float)fs, rect.y + (rowH - num) * 0.5f, num, num);
			GUI.color = new Color(1f, 1f, 1f, 0.7f);
			GUI.DrawTexture(position, BtcHotkeysUI.White());
			GUI.color = new Color(0.06f, 0.06f, 0.08f, 1f);
			GUI.DrawTexture(new Rect(position.x + 2f, position.y + 2f, position.width - 4f, position.height - 4f), BtcHotkeysUI.White());
			if (on)
			{
				GUI.color = BtcHotkeysUI.Accent;
				GUI.DrawTexture(new Rect(position.x + 4f, position.y + 4f, position.width - 8f, position.height - 8f), BtcHotkeysUI.White());
			}
			GUI.color = old;
		}
		GUI.Label(new Rect((float)fs * 2.4f, rect.y, w - (float)fs * 2.4f, rowH), BtcLang.Tr(label), BtcHotkeysUI._label);
		return GUI.Button(rect, GUIContent.none, GUIStyle.none);
	}

	private static void Note(ref float y, float w, int fs, string text)
	{
		text = BtcLang.Tr(text);
		float num = BtcHotkeysUI._note.CalcHeight(new GUIContent(text), w - (float)fs * 2f);
		GUI.Label(new Rect((float)fs, y + 2f, w - (float)fs * 2f, num), text, BtcHotkeysUI._note);
		y += num + (float)fs * 0.5f;
	}

	private static bool Button(Event e, Rect r, string text, Color color, Color old)
	{
		return BtcHotkeysUI.Button(e, r, text, color, old, false);
	}

	private static bool Button(Event e, Rect r, string text, Color color, Color old, bool wide)
	{
		bool flag = e.type == EventType.Repaint;
		bool flag2 = flag;
		if (flag2)
		{
			GUI.color = (r.Contains(e.mousePosition) ? new Color(1f, 1f, 1f, 0.16f) : new Color(1f, 1f, 1f, 0.07f));
			GUI.DrawTexture(r, BtcHotkeysUI.White());
			GUI.color = old;
		}
		GUIStyle guistyle = wide ? BtcHotkeysUI._btn : BtcHotkeysUI._small;
		Color textColor = guistyle.normal.textColor;
		guistyle.normal.textColor = color;
		bool flag3 = GUI.Button(r, wide ? text : BtcLang.Tr(text), guistyle);
		bool flag4 = flag3 && BtcHotkeys.UiClickSwallowed;
		if (flag4)
		{
			flag3 = false;
		}
		guistyle.normal.textColor = textColor;
		return flag3;
	}

	private static void RowBackground(Event e, Rect row, Color old)
	{
		bool flag = e.type == EventType.Repaint && row.Contains(e.mousePosition);
		bool flag2 = flag;
		if (flag2)
		{
			GUI.color = new Color(1f, 1f, 1f, 0.05f);
			GUI.DrawTexture(row, BtcHotkeysUI.White());
			GUI.color = old;
		}
	}

	private static Texture2D White()
	{
		bool flag = BtcHotkeysUI._white == null;
		bool flag2 = flag;
		if (flag2)
		{
			BtcHotkeysUI._white = new Texture2D(1, 1, TextureFormat.RGBA32, false);
			BtcHotkeysUI._white.SetPixel(0, 0, Color.white);
			BtcHotkeysUI._white.Apply();
			BtcHotkeysUI._white.hideFlags = HideFlags.HideAndDontSave;
		}
		return BtcHotkeysUI._white;
	}

	private static void EnsureStyles(int fs)
	{
		bool flag = BtcHotkeysUI._label != null && BtcHotkeysUI._styleFs == fs;
		bool flag2 = !flag;
		if (flag2)
		{
			BtcHotkeysUI._styleFs = fs;
			BtcHotkeysUI._label = BtcHotkeysUI.MakeStyle(GUI.skin.label, fs, TextAnchor.MiddleLeft, FontStyle.Normal, Color.white);
			BtcHotkeysUI._note = BtcHotkeysUI.MakeStyle(GUI.skin.label, Mathf.Max(10, (int)((float)fs * 0.8f)), TextAnchor.UpperLeft, FontStyle.Normal, new Color(1f, 1f, 1f, 0.55f));
			BtcHotkeysUI._note.wordWrap = true;
			BtcHotkeysUI._header = BtcHotkeysUI.MakeStyle(GUI.skin.label, fs, TextAnchor.MiddleLeft, FontStyle.Bold, BtcUiStyle.Accent);
			BtcHotkeysUI._header.padding = new RectOffset(fs, 0, 0, 0);
			BtcHotkeysUI._valueL = BtcHotkeysUI.MakeStyle(GUI.skin.label, fs, TextAnchor.MiddleLeft, FontStyle.Normal, Color.white);
			BtcHotkeysUI._value = BtcHotkeysUI.MakeStyle(GUI.skin.label, fs, TextAnchor.MiddleRight, FontStyle.Normal, Color.white);
			BtcHotkeysUI._btn = BtcHotkeysUI.MakeStyle(GUI.skin.label, Mathf.Max(10, (int)((float)fs * 0.9f)), TextAnchor.MiddleCenter, FontStyle.Normal, Color.white);
			BtcHotkeysUI._sub = BtcHotkeysUI.MakeStyle(GUI.skin.label, fs, TextAnchor.MiddleLeft, FontStyle.Bold, new Color(0.55f, 0.85f, 0.55f, 1f));
			BtcHotkeysUI._small = BtcHotkeysUI.MakeStyle(GUI.skin.label, Mathf.Max(10, (int)((float)fs * 0.75f)), TextAnchor.MiddleCenter, FontStyle.Normal, Color.white);
		}
	}

	private static GUIStyle MakeStyle(GUIStyle basis, int size, TextAnchor anchor, FontStyle fontStyle, Color color)
	{
		return new GUIStyle(basis)
		{
			fontSize = size,
			alignment = anchor,
			fontStyle = fontStyle,
			normal =
			{
				textColor = color
			},
			hover =
			{
				textColor = color
			},
			active =
			{
				textColor = color
			},
			focused =
			{
				textColor = color
			},
			padding = new RectOffset(4, 4, 0, 0),
			wordWrap = false,
			clipping = TextClipping.Clip
		};
	}

	private static float SliderCore(Event e, Rect hit, int id, float value, float min, float max, float step, Color old)
	{
		bool flag = e.type == EventType.MouseDown && e.button == 0 && hit.Contains(e.mousePosition);
		bool flag2 = flag;
		if (flag2)
		{
			BtcHotkeysUI._dragId = id;
		}
		bool flag3 = BtcHotkeysUI._dragId == id && e.button == 0 && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag);
		bool flag4 = flag3;
		if (flag4)
		{
			float num = Mathf.Clamp01((e.mousePosition.x - hit.x) / hit.width);
			e.Use();
			float num2 = min + num * (max - min);
			bool flag5 = step > 0f;
			bool flag6 = flag5;
			if (flag6)
			{
				num2 = Mathf.Round(num2 / step) * step;
			}
			value = Mathf.Clamp(num2, min, max);
		}
		bool flag7 = e.type == EventType.Repaint;
		bool flag8 = flag7;
		if (flag8)
		{
			float num3 = Mathf.Clamp01((value - min) / (max - min));
			Rect position = new Rect(hit.x, hit.y + hit.height * 0.5f - 2f, hit.width, 4f);
			GUI.color = new Color(1f, 1f, 1f, 0.22f);
			GUI.DrawTexture(position, BtcHotkeysUI.White());
			GUI.color = BtcHotkeysUI.Accent;
			GUI.DrawTexture(new Rect(position.x, position.y, position.width * num3, position.height), BtcHotkeysUI.White());
			GUI.color = Color.white;
			float num4 = hit.height * 0.5f;
			GUI.DrawTexture(new Rect(position.x + position.width * num3 - num4 * 0.25f, hit.y + hit.height * 0.5f - num4 * 0.5f, num4 * 0.5f, num4), BtcHotkeysUI.White());
			GUI.color = old;
		}
		return value;
	}

	public static bool TabActive;

	public static float ContentH = 400f;

	private static readonly Color Accent = new Color(0.55f, 0.85f, 0.55f, 1f);

	private static readonly Color Warn = new Color(1f, 0.45f, 0.45f, 1f);

	private static readonly Color Changed = new Color(1f, 0.85f, 0.4f, 1f);

	private static readonly Color Listening = new Color(1f, 0.65f, 0.2f, 1f);

	private static Texture2D _white;

	private static int _styleFs = -1;

	private static GUIStyle _label;

	private static GUIStyle _note;

	private static GUIStyle _header;

	private static GUIStyle _value;

	private static GUIStyle _btn;

	private static GUIStyle _small;

	private static bool _groupsInit;

	private static readonly HashSet<string> Collapsed = new HashSet<string>();

	private static string _confirm;

	private static float _confirmUntil;

	private static GUIStyle _valueL;

	private static GUIStyle _sub;

	private static int _dragId;
}
}
