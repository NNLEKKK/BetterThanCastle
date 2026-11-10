using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Brix.Game.AI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BetterThanCastle
{
public static class BtcHotkeys
{
	public static bool Enabled
	{
		get
		{
			BtcHotkeys.EnsureLoaded();
			return BtcHotkeys._enabled;
		}
	}

	public static void SetEnabled(bool on)
	{
		BtcHotkeys.EnsureLoaded();
		bool flag = BtcHotkeys._enabled == on;
		bool flag2 = !flag;
		bool flag3 = flag2;
		if (flag3)
		{
			if (!BtcUndo.Suspended)
			{
				bool was = BtcHotkeys._enabled;
				BtcUndo.Push("hotkeys", BtcReplayUI.T("Advanced Hotkeys switch", "Выключатель Advanced Hotkeys"), delegate { BtcHotkeys.SetEnabled(was); });
			}
			BtcHotkeys._enabled = on;
			BtcHotkeys._blockFrame = -1;
			bool flag4 = !on;
			bool flag5 = flag4;
			bool flag6 = flag5;
			if (flag6)
			{
				BtcHotkeys.CancelCapture();
			}
			BtcHotkeys.SaveNow();
		}
	}

	public static bool MenuOpen
	{
		get
		{
			return BtcHotkeys._menuOpen;
		}
		set
		{
			bool changed = BtcHotkeys._menuOpen != value;
			BtcHotkeys._menuOpen = value;
			if (changed) { if (value) BtcSfx.Open(); else BtcSfx.Close(); }
			BtcHotkeys._blockFrame = -1;
			bool flag = !value && BtcHotkeys._capId != null;
			bool flag2 = flag;
			bool flag3 = flag2;
			if (flag3)
			{
				BtcHotkeys.CancelCapture();
			}
		}
	}

	public static bool Capturing
	{
		get
		{
			return BtcHotkeys._capId != null;
		}
	}

	public static string CaptureId
	{
		get
		{
			return BtcHotkeys._capId;
		}
	}

	public static bool EscBlocked
	{
		get
		{
			return BtcHotkeys._capId != null || Time.frameCount - BtcHotkeys._capEndFrame <= 1;
		}
	}

	public static IList<BtcHotkeys.Def> Defs
	{
		get
		{
			BtcHotkeys.EnsureLoaded();
			return BtcHotkeys._defs;
		}
	}

	public static IList<string> Groups
	{
		get
		{
			BtcHotkeys.EnsureLoaded();
			return BtcHotkeys._groups;
		}
	}

	public static KeyCode[] GetKeys(string name, KeyCode[] fallback)
	{
		BtcHotkeys.EnsureLoaded();
		KeyCode[] array;
		if (BtcHotkeys._keyCache.TryGetValue(name, out array))
		{
			return array;
		}
		KeyCode[][] alts = null;
		string value;
		if (!(BtcHotkeys._raw.TryGetValue(name, out value) && BtcHotkeys.TryParseAlts(value, out alts)))
		{
			BtcHotkeys.Def def;
			if (!(BtcHotkeys._defById.TryGetValue(name, out def) && BtcHotkeys.TryParseAlts(def.def, out alts)))
			{
				alts = null;
			}
		}
		if (alts == null)
		{
			array = fallback ?? new KeyCode[0];
		}
		else
		{
			array = (alts.Length > 0) ? alts[0] : new KeyCode[0];
			if (alts.Length > 1)
			{
				if (BtcHotkeys._alts.Count > 1024)
				{
					BtcHotkeys._alts.Clear();
				}
				BtcHotkeys._alts[array] = alts;
			}
		}
		BtcHotkeys._keyCache[name] = array;
		return array;
	}

	/// <summary>All non-empty alternatives of a bind (a combo each), primary first. One bind can fire from any of them.</summary>
	public static KeyCode[][] GetAlts(string id)
	{
		KeyCode[] primary = BtcHotkeys.GetKeys(id, null);
		KeyCode[][] result;
		if (BtcHotkeys._alts.TryGetValue(primary, out result))
		{
			return result;
		}
		return (primary.Length == 0) ? new KeyCode[0][] : new KeyCode[][] { primary };
	}

	private sealed class RefEq : IEqualityComparer<KeyCode[]>
	{
		public bool Equals(KeyCode[] a, KeyCode[] b)
		{
			return object.ReferenceEquals(a, b);
		}

		public int GetHashCode(KeyCode[] a)
		{
			return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(a);
		}
	}

	private static readonly Dictionary<KeyCode[], KeyCode[][]> _alts = new Dictionary<KeyCode[], KeyCode[][]>(new BtcHotkeys.RefEq());

	private static bool TryParseAlts(string value, out KeyCode[][] alts)
	{
		string[] parts = (value ?? string.Empty).Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
		List<KeyCode[]> list = new List<KeyCode[]>();
		for (int i = 0; i < parts.Length; i++)
		{
			KeyCode[] combo;
			if (!BtcHotkeys.TryParseCombo(parts[i], out combo))
			{
				alts = null;
				return false;
			}
			if (combo.Length > 0)
			{
				list.Add(combo);
			}
		}
		alts = list.ToArray();
		return true;
	}

	public static KeyCode[] GetJobKeys(Occupation.Job job, KeyCode[] fallback)
	{
		return BtcHotkeys.GetKeys("job_" + job.ToString(), fallback);
	}

	public static float GetFloat(string name, float fallback)
	{
		BtcHotkeys.EnsureLoaded();
		float num = 0f;
		string s;
		bool flag = BtcHotkeys._raw.TryGetValue(name, out s) && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out num);
		bool flag2 = flag;
		bool flag3 = flag2;
		float result;
		if (flag3)
		{
			result = num;
		}
		else
		{
			BtcHotkeys.Def def;
			bool flag4 = BtcHotkeys._defById.TryGetValue(name, out def) && float.TryParse(def.def, NumberStyles.Float, CultureInfo.InvariantCulture, out num);
			bool flag5 = flag4;
			bool flag6 = flag5;
			if (flag6)
			{
				result = num;
			}
			else
			{
				result = fallback;
			}
		}
		return result;
	}

	public static string GetString(string name, string fallback)
	{
		BtcHotkeys.EnsureLoaded();
		string text;
		bool flag = BtcHotkeys._raw.TryGetValue(name, out text) && !string.IsNullOrEmpty(text);
		bool flag2 = flag;
		bool flag3 = flag2;
		string result;
		if (flag3)
		{
			result = text;
		}
		else
		{
			BtcHotkeys.Def def;
			bool flag4 = BtcHotkeys._defById.TryGetValue(name, out def) && !string.IsNullOrEmpty(def.def);
			bool flag5 = flag4;
			bool flag6 = flag5;
			if (flag6)
			{
				result = def.def;
			}
			else
			{
				result = fallback;
			}
		}
		return result;
	}

	public static bool IsSuppressed()
	{
		return BtcHotkeys.IsSuppressed(null);
	}

	public static bool IsSuppressed(KeyCode[] combo)
	{
		KeyCode[][] alts;
		if (combo != null && BtcHotkeys._alts.TryGetValue(combo, out alts))
		{
			for (int i = 0; i < alts.Length; i++)
			{
				if (!BtcHotkeys.IsSuppressedOne(alts[i]))
				{
					return false;
				}
			}
			return true;
		}
		return BtcHotkeys.IsSuppressedOne(combo);
	}

	private static bool IsSuppressedOne(KeyCode[] combo)
	{
		BtcHotkeys.EnsureLoaded();
		bool flag = BtcHotkeys.GlobalBlocked();
		bool flag2 = flag;
		bool flag3 = flag2;
		bool result;
		if (flag3)
		{
			result = true;
		}
		else
		{
			bool flag4 = BtcHotkeys.MouseBlockedByUi(combo);
			bool flag5 = flag4;
			if (flag5)
			{
				return true;
			}
			foreach (KeyCode key in BtcHotkeys.GuardKeys())
			{
				bool key2 = Input.GetKey(key);
				bool flag6 = key2;
				bool flag7 = flag6;
				if (flag7)
				{
					bool flag8 = !BtcHotkeys.ContainsKey(combo, key);
					bool flag9 = flag8;
					bool flag10 = flag9;
					if (flag10)
					{
						return true;
					}
				}
			}
			result = false;
		}
		return result;
	}

	public static bool ComboPressed(KeyCode[] keys)
	{
		KeyCode[][] alts;
		if (keys != null && BtcHotkeys._alts.TryGetValue(keys, out alts))
		{
			for (int i = 0; i < alts.Length; i++)
			{
				if (BtcHotkeys.ComboPressedOne(alts[i]))
				{
					return true;
				}
			}
			return false;
		}
		return BtcHotkeys.ComboPressedOne(keys);
	}

	private static bool ComboPressedOne(KeyCode[] keys)
	{
		bool flag = keys == null || keys.Length == 0;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool result;
		if (flag3)
		{
			result = false;
		}
		else
		{
			bool flag4 = BtcHotkeys.IsSuppressed(keys);
			bool flag5 = flag4;
			bool flag6 = flag5;
			if (flag6)
			{
				result = false;
			}
			else
			{
				for (int i = 0; i < keys.Length - 1; i++)
				{
					bool flag7 = !Input.GetKey(keys[i]);
					bool flag8 = flag7;
					bool flag9 = flag8;
					if (flag9)
					{
						return false;
					}
				}
				result = (Input.GetKeyDown(keys[keys.Length - 1]) && !BtcHotkeys.ExtraModifiersHeld(keys));
				if (result) BtcApm.NoteEffect();
			}
		}
		return result;
	}

	private static readonly KeyCode[] _noKeys = new KeyCode[0];

	// True while the user-bound "no pie menu" key (Safety group) is held. Unbound = always false.
	public static bool PieMenuBlocked()
	{
		KeyCode[] keys = BtcHotkeys.GetKeys("NoPieMenuIfHolding", BtcHotkeys._noKeys);
		return keys != null && keys.Length > 0 && BtcHotkeys.AllHeld(keys);
	}

	public static bool AllHeld(KeyCode[] keys)
	{
		KeyCode[][] alts;
		if (BtcHotkeys._alts.TryGetValue(keys, out alts))
		{
			for (int a = 0; a < alts.Length; a++)
			{
				if (BtcHotkeys.AllHeldOne(alts[a]))
				{
					return true;
				}
			}
			return false;
		}
		return BtcHotkeys.AllHeldOne(keys);
	}

	private static bool AllHeldOne(KeyCode[] keys)
	{
		for (int i = 0; i < keys.Length; i++)
		{
			bool flag = !Input.GetKey(keys[i]);
			bool flag2 = flag;
			bool flag3 = flag2;
			if (flag3)
			{
				return false;
			}
		}
		return true;
	}

	public static bool ContainsKey(KeyCode[] combo, KeyCode key)
	{
		KeyCode[][] alts;
		if (combo != null && BtcHotkeys._alts.TryGetValue(combo, out alts))
		{
			for (int i = 0; i < alts.Length; i++)
			{
				if (BtcHotkeys.ContainsKeyOne(alts[i], key))
				{
					return true;
				}
			}
			return false;
		}
		return BtcHotkeys.ContainsKeyOne(combo, key);
	}

	private static bool ContainsKeyOne(KeyCode[] combo, KeyCode key)
	{
		bool flag = combo == null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool result;
		if (flag3)
		{
			result = false;
		}
		else
		{
			for (int i = 0; i < combo.Length; i++)
			{
				bool flag4 = combo[i] == key;
				bool flag5 = flag4;
				bool flag6 = flag5;
				if (flag6)
				{
					return true;
				}
			}
			result = false;
		}
		return result;
	}

	public static bool ExtraModifiersHeld(KeyCode[] combo)
	{
		for (int i = 0; i < BtcHotkeys.ModKeys.Length; i++)
		{
			KeyCode key = BtcHotkeys.ModKeys[i];
			bool flag = Input.GetKey(key) && !BtcHotkeys.ContainsKey(combo, key);
			bool flag2 = flag;
			bool flag3 = flag2;
			if (flag3)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>The key that went down this frame (any keyboard / mouse key, modifiers included), or None. Used by the menu key finder.</summary>
	public static KeyCode AnyKeyDown()
	{
		KeyCode[] keys = BtcHotkeys.CaptureKeys();
		for (int i = 0; i < keys.Length; i++)
		{
			if (Input.GetKeyDown(keys[i]))
			{
				return keys[i];
			}
		}
		for (int j = 0; j < BtcHotkeys.ModKeys.Length; j++)
		{
			if (Input.GetKeyDown(BtcHotkeys.ModKeys[j]))
			{
				return BtcHotkeys.ModKeys[j];
			}
		}
		return KeyCode.None;
	}

	/// <summary>Ids of the Key binds that have the key in any of their combos.</summary>
	public static List<string> IdsUsingKey(KeyCode key)
	{
		BtcHotkeys.EnsureLoaded();
		List<string> list = new List<string>();
		for (int i = 0; i < BtcHotkeys._defs.Count; i++)
		{
			BtcHotkeys.Def def = BtcHotkeys._defs[i];
			if (def.kind != BtcHotkeys.Kind.Key)
			{
				continue;
			}
			KeyCode[][] alts = BtcHotkeys.GetAlts(def.id);
			for (int a = 0; a < alts.Length; a++)
			{
				if (BtcHotkeys.ContainsKeyOne(alts[a], key))
				{
					list.Add(def.id);
					break;
				}
			}
		}
		return list;
	}

	/// <summary>Every setting as "id=value" lines (a bind profile).</summary>
	public static List<string> ExportLines()
	{
		BtcHotkeys.EnsureLoaded();
		List<string> list = new List<string>();
		for (int i = 0; i < BtcHotkeys._defs.Count; i++)
		{
			list.Add(BtcHotkeys._defs[i].id + "=" + BtcHotkeys.Effective(BtcHotkeys._defs[i].id));
		}
		return list;
	}

	/// <summary>Applies "id=value" lines; unknown ids are ignored. Returns the number of settings set.</summary>
	public static int ImportLines(IList<string> lines)
	{
		BtcHotkeys.EnsureLoaded();
		BtcHotkeys.RecordAll(BtcReplayUI.T("Profile applied", "Применение профиля"));
		int n = 0;
		for (int i = 0; i < lines.Count; i++)
		{
			string t = lines[i].Trim();
			int eq = t.IndexOf('=');
			if (t.Length == 0 || t[0] == '#' || eq <= 0)
			{
				continue;
			}
			string id = t.Substring(0, eq).Trim();
			if (BtcHotkeys._defById.ContainsKey(id))
			{
				BtcHotkeys._raw[id] = t.Substring(eq + 1).Trim();
				n++;
			}
		}
		BtcHotkeys.AfterBulkChange();
		return n;
	}

	public static BtcHotkeys.Def GetDef(string id)
	{
		BtcHotkeys.EnsureLoaded();
		BtcHotkeys.Def result;
		BtcHotkeys._defById.TryGetValue(id, out result);
		return result;
	}

	public static string Effective(string id)
	{
		BtcHotkeys.EnsureLoaded();
		string text;
		bool flag = BtcHotkeys._raw.TryGetValue(id, out text);
		bool flag2 = flag;
		bool flag3 = flag2;
		string result;
		if (flag3)
		{
			result = text;
		}
		else
		{
			BtcHotkeys.Def def;
			result = (BtcHotkeys._defById.TryGetValue(id, out def) ? def.def : string.Empty);
		}
		return result;
	}

	public static bool IsDefault(string id)
	{
		BtcHotkeys.Def def = BtcHotkeys.GetDef(id);
		return def == null || string.Equals(BtcHotkeys.Effective(id).Trim(), def.def.Trim(), StringComparison.OrdinalIgnoreCase);
	}

	public static void SetKeys(string id, KeyCode[] keys)
	{
		BtcHotkeys.EnsureLoaded();
		BtcHotkeys.Def def = BtcHotkeys.GetDef(id);
		if (def != null && def.any)
		{
			BtcHotkeys.SetRaw(id, BtcHotkeys.Join(keys, ","));
			return;
		}
		BtcHotkeys.ReplaceAlt(id, 0, keys);
	}

	private static string JoinAlts(IList<KeyCode[]> alts)
	{
		StringBuilder sb = new StringBuilder();
		for (int i = 0; i < alts.Count; i++)
		{
			if (alts[i] == null || alts[i].Length == 0)
			{
				continue;
			}
			if (sb.Length > 0)
			{
				sb.Append('|');
			}
			sb.Append(BtcHotkeys.Join(alts[i], "+"));
		}
		return sb.ToString();
	}

	/// <summary>Replaces alternative idx of a bind; idx outside the list appends a new alternative (the "+" button). An identical combo is not added twice.</summary>
	public static void ReplaceAlt(string id, int idx, KeyCode[] combo)
	{
		BtcHotkeys.EnsureLoaded();
		List<KeyCode[]> list = new List<KeyCode[]>(BtcHotkeys.GetAlts(id));
		string norm = BtcHotkeys.ComboNorm(combo);
		for (int i = 0; i < list.Count; i++)
		{
			if (i != idx && BtcHotkeys.ComboNorm(list[i]) == norm)
			{
				return;
			}
		}
		if (idx >= 0 && idx < list.Count)
		{
			list[idx] = combo;
		}
		else
		{
			list.Add(combo);
		}
		BtcHotkeys.SetRaw(id, BtcHotkeys.JoinAlts(list));
	}

	public static void RemoveAlt(string id, int idx)
	{
		BtcHotkeys.EnsureLoaded();
		List<KeyCode[]> list = new List<KeyCode[]>(BtcHotkeys.GetAlts(id));
		if (idx >= 0 && idx < list.Count)
		{
			list.RemoveAt(idx);
			BtcHotkeys.SetRaw(id, BtcHotkeys.JoinAlts(list));
		}
	}

	public static void Unbind(string id)
	{
		BtcHotkeys.EnsureLoaded();
		BtcHotkeys.SetRaw(id, string.Empty);
	}

	public static void ResetBind(string id)
	{
		BtcHotkeys.EnsureLoaded();
		BtcHotkeys.Def def = BtcHotkeys.GetDef(id);
		bool flag = def != null;
		bool flag2 = flag;
		bool flag3 = flag2;
		if (flag3)
		{
			BtcHotkeys.SetRaw(id, def.def);
		}
	}

	public static void SetFloat(string id, float value)
	{
		BtcHotkeys.EnsureLoaded();
		BtcHotkeys.RecordOne(id);
		BtcHotkeys._raw[id] = value.ToString("0.###", CultureInfo.InvariantCulture);
		BtcHotkeys._keyCache.Remove(id);
		BtcHotkeys._guardCache = null;
		BtcHotkeys._dirty = true;
		BtcHotkeys._dirtyAt = Time.unscaledTime;
	}

	public static void ToggleMode(string id)
	{
		BtcHotkeys.EnsureLoaded();
		string @string = BtcHotkeys.GetString(id, "hold");
		BtcHotkeys.SetRaw(id, @string.Equals("toggle", StringComparison.OrdinalIgnoreCase) ? "hold" : "toggle");
	}

	public static bool GetBool(string id, bool fallback)
	{
		string s = BtcHotkeys.GetString(id, fallback ? "1" : "0");
		return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase) || s.Equals("on", StringComparison.OrdinalIgnoreCase);
	}

	public static void ToggleBool(string id)
	{
		BtcHotkeys.EnsureLoaded();
		BtcHotkeys.SetRaw(id, BtcHotkeys.GetBool(id, false) ? "0" : "1");
	}

	public static void ClearAll()
	{
		BtcHotkeys.EnsureLoaded();
		BtcHotkeys.RecordAll(BtcReplayUI.T("Unbind all functions", "Снятие всех биндов"));
		for (int i = 0; i < BtcHotkeys._defs.Count; i++)
		{
			BtcHotkeys.Def def = BtcHotkeys._defs[i];
			bool flag = def.kind == BtcHotkeys.Kind.Key && def.id != "DisableIfHolding";
			bool flag2 = flag;
			bool flag3 = flag2;
			if (flag3)
			{
				BtcHotkeys._raw[def.id] = string.Empty;
			}
		}
		BtcHotkeys.AfterBulkChange();
	}

	public static void ResetAll()
	{
		BtcHotkeys.EnsureLoaded();
		BtcHotkeys.RecordAll(BtcReplayUI.T("Reset all binds", "Сброс всех биндов"));
		for (int i = 0; i < BtcHotkeys._defs.Count; i++)
		{
			BtcHotkeys.Def def = BtcHotkeys._defs[i];
			BtcHotkeys._raw[def.id] = def.def;
		}
		BtcHotkeys.AfterBulkChange();
	}

	public static bool IsConflict(string id)
	{
		BtcHotkeys.EnsureLoaded();
		bool flag = BtcHotkeys._conflictVersion != BtcHotkeys._version;
		bool flag2 = flag;
		bool flag3 = flag2;
		if (flag3)
		{
			BtcHotkeys.RebuildConflicts();
		}
		return BtcHotkeys._conflicts.Contains(id);
	}

	public static string ComboText(string id)
	{
		BtcHotkeys.Def def = BtcHotkeys.GetDef(id);
		KeyCode[] keys = BtcHotkeys.GetKeys(id, null);
		bool flag = keys.Length == 0;
		bool flag2 = flag;
		bool flag3 = flag2;
		string result;
		if (flag3)
		{
			result = BtcLang.Tr("(not set)");
		}
		else if (BtcHotkeys.GetAlts(id).Length > 1)
		{
			KeyCode[][] alts = BtcHotkeys.GetAlts(id);
			string[] parts = new string[alts.Length];
			for (int a = 0; a < alts.Length; a++)
			{
				parts[a] = BtcHotkeys.ComboLabel(alts[a], false);
			}
			result = string.Join(" | ", parts);
		}
		else
		{
			string[] array = new string[keys.Length];
			for (int i = 0; i < keys.Length; i++)
			{
				array[i] = BtcHotkeys.KeyName(keys[i]);
			}
			result = string.Join((def != null && def.any) ? " / " : " + ", array);
		}
		return result;
	}

	public static string ComboLabel(KeyCode[] keys, bool any)
	{
		if (keys == null || keys.Length == 0)
		{
			return string.Empty;
		}
		string[] array = new string[keys.Length];
		for (int i = 0; i < keys.Length; i++)
		{
			array[i] = BtcHotkeys.KeyName(keys[i]);
		}
		return string.Join(any ? " / " : " + ", array);
	}

	public static string KeyName(KeyCode k)
	{
		KeyCode keyCode = k;
		string result;
		switch (keyCode)
		{
		case KeyCode.RightShift:
			result = "RShift";
			break;
		case KeyCode.LeftShift:
			result = "Shift";
			break;
		case KeyCode.RightControl:
			result = "RCtrl";
			break;
		case KeyCode.LeftControl:
			result = "Ctrl";
			break;
		case KeyCode.RightAlt:
			result = "RAlt";
			break;
		case KeyCode.LeftAlt:
			result = "Alt";
			break;
		default:
		{
			bool flag = keyCode != KeyCode.Mouse0;
			if (flag)
			{
				bool flag2 = keyCode != KeyCode.Mouse1;
				if (flag2)
				{
					string text = k.ToString();
					bool flag3 = text.StartsWith("Alpha");
					bool flag4 = flag3;
					bool flag5 = flag4;
					if (flag5)
					{
						result = text.Substring(5);
					}
					else
					{
						bool flag6 = text.StartsWith("Keypad");
						bool flag7 = flag6;
						bool flag8 = flag7;
						if (flag8)
						{
							result = "Num " + text.Substring(6);
						}
						else
						{
							result = text;
						}
					}
				}
				else
				{
					result = "RMB";
				}
			}
			else
			{
				result = "LMB";
			}
			break;
		}
		}
		return result;
	}

	public static void BeginCapture(string id)
	{
		BtcHotkeys.BeginCapture(id, 0);
	}

	/// <summary>alt = index of the alternative to replace, or -1 to add a new one.</summary>
	public static void BeginCapture(string id, int alt)
	{
		BtcHotkeys.EnsureLoaded();
		BtcHotkeys._capId = id;
		BtcHotkeys._capAlt = alt;
		BtcHotkeys._capStartFrame = Time.frameCount;
		BtcHotkeys._capMods.Clear();
		BtcHotkeys._blockFrame = -1;
	}

	public static void CancelCapture()
	{
		bool flag = BtcHotkeys._capId != null;
		bool flag2 = flag;
		bool flag3 = flag2;
		if (flag3)
		{
			BtcHotkeys._capId = null;
			BtcHotkeys._capEndFrame = Time.frameCount;
			BtcHotkeys._capMods.Clear();
			BtcHotkeys._blockFrame = -1;
		}
	}

	public static void Tick()
	{
		bool flag = BtcHotkeys._dirty && Time.unscaledTime - BtcHotkeys._dirtyAt > 0.5f;
		bool flag2 = flag;
		bool flag3 = flag2;
		if (flag3)
		{
			BtcHotkeys.SaveNow();
		}
		BtcHotkeys.PollCapture();
		BtcHotkeys.UpdateClickSwallow();
	}

	private static void PollCapture()
	{
		bool flag = BtcHotkeys._capId == null || Time.frameCount == BtcHotkeys._capStartFrame;
		bool flag2 = !flag;
		bool flag3 = flag2;
		if (flag3)
		{
			bool keyDown = Input.GetKeyDown(KeyCode.Escape);
			if (keyDown) BtcSfx.Back();
			bool flag4 = keyDown;
			bool flag5 = flag4;
			if (flag5)
			{
				BtcHotkeys.CancelCapture();
			}
			else
			{
				for (int i = 0; i < BtcHotkeys.ModKeys.Length; i++)
				{
					KeyCode keyCode = BtcHotkeys.ModKeys[i];
					bool flag6 = Input.GetKeyDown(keyCode) && !BtcHotkeys._capMods.Contains(keyCode);
					bool flag7 = flag6;
					bool flag8 = flag7;
					if (flag8)
					{
						BtcHotkeys._capMods.Add(keyCode);
					}
				}
				KeyCode[] array = BtcHotkeys.CaptureKeys();
				for (int j = 0; j < array.Length; j++)
				{
					bool keyDown2 = Input.GetKeyDown(array[j]);
					bool flag9 = keyDown2;
					bool flag10 = flag9;
					if (flag10)
					{
						List<KeyCode> list = new List<KeyCode>();
						for (int k = 0; k < BtcHotkeys._capMods.Count; k++)
						{
							bool key = Input.GetKey(BtcHotkeys._capMods[k]);
							bool flag11 = key;
							bool flag12 = flag11;
							if (flag12)
							{
								list.Add(BtcHotkeys._capMods[k]);
							}
						}
						list.Add(array[j]);
						BtcHotkeys.FinishCapture(list.ToArray());
						return;
					}
				}
				bool flag13 = BtcHotkeys._capMods.Count > 0;
				bool flag14 = flag13;
				bool flag15 = flag14;
				if (flag15)
				{
					bool flag16 = false;
					for (int l = 0; l < BtcHotkeys._capMods.Count; l++)
					{
						bool key2 = Input.GetKey(BtcHotkeys._capMods[l]);
						bool flag17 = key2;
						bool flag18 = flag17;
						if (flag18)
						{
							flag16 = true;
						}
					}
					bool flag19 = !flag16;
					bool flag20 = flag19;
					bool flag21 = flag20;
					if (flag21)
					{
						BtcHotkeys.FinishCapture(BtcHotkeys._capMods.ToArray());
					}
				}
			}
		}
	}

	private static void FinishCapture(KeyCode[] combo)
	{
		string capId = BtcHotkeys._capId;
		int capAlt = BtcHotkeys._capAlt;
		BtcHotkeys.CancelCapture();
		bool flag = combo != null && combo.Length != 0 && BtcHotkeys.IsMouseClickKey(combo[combo.Length - 1]);
		bool flag2 = flag;
		if (flag2)
		{
			BtcHotkeys._swallowClick = true;
			BtcHotkeys._swallowReleaseFrame = -1;
		}
		bool flag3 = capId != null && combo != null && combo.Length != 0;
		bool flag4 = flag3;
		bool flag5 = flag4;
		if (flag5)
		{
			if (capId.StartsWith("v:") && BtcHotkeys.ExternalCapture != null)
			{
				BtcSfx.Ok();
				BtcHotkeys.ExternalCapture(capId.Substring(2), capAlt, combo);
				return;
			}
			BtcSfx.Ok();
			BtcHotkeys.Def cd = BtcHotkeys.GetDef(capId);
			if (cd != null && cd.any)
			{
				BtcHotkeys.SetKeys(capId, combo);
			}
			else
			{
				BtcHotkeys.ReplaceAlt(capId, capAlt, combo);
			}
		}
	}

	/// <summary>Capture ids "v:action" are vanilla binds: the menu tab handles them (action, alternative index, combo).</summary>
	public static Action<string, int, KeyCode[]> ExternalCapture;

	/// <summary>Changes on every bind edit (the menu rebuilds its caches).</summary>
	public static int Version
	{
		get
		{
			return BtcHotkeys._version;
		}
	}

	public static string ComboKey(KeyCode[] combo)
	{
		return BtcHotkeys.ComboNorm(combo);
	}

	public static int CaptureAlt
	{
		get
		{
			return BtcHotkeys._capAlt;
		}
	}

	private static int _capAlt;

	private static KeyCode[] CaptureKeys()
	{
		bool flag = BtcHotkeys._captureKeys == null;
		bool flag2 = flag;
		bool flag3 = flag2;
		if (flag3)
		{
			List<KeyCode> list = new List<KeyCode>();
			HashSet<int> hashSet = new HashSet<int>();
			Array values = Enum.GetValues(typeof(KeyCode));
			for (int i = 0; i < values.Length; i++)
			{
				KeyCode keyCode = (KeyCode)values.GetValue(i);
				bool flag4 = keyCode == KeyCode.None || keyCode == KeyCode.Escape;
				bool flag5 = !flag4;
				bool flag6 = flag5;
				if (flag6)
				{
					bool flag7 = Array.IndexOf<KeyCode>(BtcHotkeys.ModKeys, keyCode) >= 0;
					bool flag8 = !flag7;
					bool flag9 = flag8;
					if (flag9)
					{
						bool flag10 = keyCode.ToString().StartsWith("Joystick");
						bool flag11 = !flag10;
						bool flag12 = flag11;
						if (flag12)
						{
							bool flag13 = hashSet.Add((int)keyCode);
							bool flag14 = flag13;
							bool flag15 = flag14;
							if (flag15)
							{
								list.Add(keyCode);
							}
						}
					}
				}
			}
			BtcHotkeys._captureKeys = list.ToArray();
		}
		return BtcHotkeys._captureKeys;
	}

	private static void EnsureLoaded()
	{
		bool loaded = BtcHotkeys._loaded;
		bool flag = !loaded;
		bool flag2 = flag;
		if (flag2)
		{
			BtcHotkeys._loaded = true;
			BtcHotkeys.BuildCatalog();
			bool flag3 = false;
			try
			{
				BtcPaths.Init();
				flag3 = File.Exists(BtcHotkeys.FilePath);
				bool flag4 = flag3;
				bool flag5 = flag4;
				bool flag6 = flag5;
				if (flag6)
				{
					string[] array = File.ReadAllLines(BtcHotkeys.FilePath);
					for (int i = 0; i < array.Length; i++)
					{
						string text = array[i].Trim();
						bool flag7 = text.Length == 0 || text.StartsWith("#") || text.StartsWith("//");
						bool flag8 = !flag7;
						bool flag9 = flag8;
						if (flag9)
						{
							int num = text.IndexOf('=');
							bool flag10 = num <= 0;
							bool flag11 = !flag10;
							bool flag12 = flag11;
							if (flag12)
							{
								string text2 = text.Substring(0, num).Trim();
								string text3 = text.Substring(num + 1).Trim();
								bool flag13 = text2.Equals("Enabled", StringComparison.OrdinalIgnoreCase);
								bool flag14 = flag13;
								bool flag15 = flag14;
								if (flag15)
								{
									BtcHotkeys._enabled = (text3 != "0" && !text3.Equals("false", StringComparison.OrdinalIgnoreCase));
								}
								else
								{
									BtcHotkeys._raw[text2] = text3;
								}
							}
						}
					}
				}
			}
			catch (Exception arg)
			{
				Debug.LogWarning("[BtcHotkeys] Failed to read the config, using defaults. " + arg);
			}
			bool flag16 = !flag3;
			bool flag17 = flag16;
			bool flag18 = flag17;
			if (flag18)
			{
				BtcHotkeys.SaveNow();
			}
		}
	}

	private static string FilePath
	{
		get
		{
			return BtcPaths.Config("hotkeys.txt");
		}
	}

	private static void SaveNow()
	{
		BtcHotkeys._dirty = false;
		try
		{
			BtcPaths.Init();
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("# ==== Better Than Castle: Advanced Hotkeys ====\r\n");
			stringBuilder.Append("# Managed from the mod menu (PageDown -> Advanced Hotkeys). Can also be edited by hand while the game is closed.\r\n");
			stringBuilder.Append("# Формат: имя=Клавиша1+Клавиша2 (последняя клавиша = момент нажатия, предыдущие = держать).\r\n");
			stringBuilder.Append("# Имена клавиш = UnityEngine.KeyCode (LeftShift, LeftControl, A-Z, Alpha0-9, F1-F12, Mouse0 = ЛКМ, Mouse1 = ПКМ, Mouse2 и т.д.). Пустое значение = бинд отключён.\r\n");
			stringBuilder.Append("# Комбо срабатывает только если зажаты РОВНО указанные модификаторы (Ctrl/Shift/Alt/CapsLock).\r\n\r\n");
			stringBuilder.Append("# 1 = хоткеи включены, 0 = все хоткеи выключены\r\n");
			stringBuilder.Append("Enabled=").Append(BtcHotkeys._enabled ? "1" : "0").Append("\r\n\r\n");
			string b = null;
			HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			hashSet.Add("Enabled");
			for (int i = 0; i < BtcHotkeys._defs.Count; i++)
			{
				BtcHotkeys.Def def = BtcHotkeys._defs[i];
				bool flag = def.group != b;
				bool flag2 = flag;
				bool flag3 = flag2;
				if (flag3)
				{
					b = def.group;
					stringBuilder.Append("\r\n# ---- ").Append(def.group).Append(" ----\r\n");
				}
				stringBuilder.Append(def.id).Append('=').Append(BtcHotkeys.Effective(def.id)).Append("\r\n");
				hashSet.Add(def.id);
			}
			bool flag4 = false;
			foreach (KeyValuePair<string, string> keyValuePair in BtcHotkeys._raw)
			{
				bool flag5 = hashSet.Contains(keyValuePair.Key);
				bool flag6 = !flag5;
				bool flag7 = flag6;
				if (flag7)
				{
					bool flag8 = !flag4;
					bool flag9 = flag8;
					bool flag10 = flag9;
					if (flag10)
					{
						flag4 = true;
						stringBuilder.Append("\r\n# ---- other ----\r\n");
					}
					stringBuilder.Append(keyValuePair.Key).Append('=').Append(keyValuePair.Value).Append("\r\n");
				}
			}
			File.WriteAllText(BtcHotkeys.FilePath, stringBuilder.ToString(), new UTF8Encoding(false));
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[BtcHotkeys] Failed to save the config: " + ex.Message);
		}
	}

	private static void RecordOne(string id)
	{
		if (BtcUndo.Suspended)
		{
			return;
		}
		string old = BtcHotkeys.Effective(id);
		BtcHotkeys.Def d = BtcHotkeys.GetDef(id);
		BtcUndo.Push("hotkeys", BtcReplayUI.T("Bind: ", "Бинд: ") + (d != null ? d.Title : id), delegate
		{
			BtcHotkeys.SetRaw(id, old);
		}, id);
	}

	private static void RecordAll(string label)
	{
		if (BtcUndo.Suspended)
		{
			return;
		}
		Dictionary<string, string> snap = new Dictionary<string, string>(BtcHotkeys._raw, StringComparer.OrdinalIgnoreCase);
		BtcUndo.Push("hotkeys", label, delegate
		{
			BtcHotkeys._raw.Clear();
			foreach (KeyValuePair<string, string> kv in snap)
			{
				BtcHotkeys._raw[kv.Key] = kv.Value;
			}
			BtcHotkeys.AfterBulkChange();
		});
	}

	private static void SetRaw(string id, string value)
	{
		BtcHotkeys.RecordOne(id);
		BtcHotkeys._raw[id] = value;
		BtcHotkeys._keyCache.Remove(id);
		BtcHotkeys._guardCache = null;
		BtcHotkeys._version++;
		BtcHotkeys.SaveNow();
	}

	private static void AfterBulkChange()
	{
		BtcHotkeys._keyCache.Clear();
		BtcHotkeys._alts.Clear();
		BtcHotkeys._guardCache = null;
		BtcHotkeys._version++;
		BtcHotkeys.SaveNow();
	}

	private static bool GlobalBlocked()
	{
		bool flag = BtcHotkeys._blockFrame == Time.frameCount;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool result;
		if (flag3)
		{
			result = BtcHotkeys._blockVal;
		}
		else
		{
			BtcHotkeys._blockFrame = Time.frameCount;
			bool flag4 = !BtcHotkeys._enabled || BtcHotkeys._menuOpen || BtcHotkeys._capId != null || Time.frameCount - BtcHotkeys._capEndFrame <= 1;
			bool flag5 = !flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			if (flag7)
			{
				try
				{
					ChatBoxDriver instance = ChatBoxDriver.Instance;
					bool flag8 = instance != null && instance.IsFocused;
					bool flag9 = flag8;
					bool flag10 = flag9;
					if (flag10)
					{
						flag4 = true;
					}
				}
				catch (Exception)
				{
				}
			}
			BtcHotkeys._blockVal = flag4;
			result = flag4;
		}
		return result;
	}

	private static KeyCode[] GuardKeys()
	{
		bool flag = BtcHotkeys._guardCache == null;
		bool flag2 = flag;
		bool flag3 = flag2;
		if (flag3)
		{
			List<KeyCode> list = new List<KeyCode>();
			string text = BtcHotkeys.Effective("DisableIfHolding");
			string[] array = text.Split(new char[]
			{
				',',
				'+'
			}, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < array.Length; i++)
			{
				KeyCode item;
				bool flag4 = BtcHotkeys.TryParseKey(array[i].Trim(), out item);
				bool flag5 = flag4;
				bool flag6 = flag5;
				if (flag6)
				{
					list.Add(item);
				}
			}
			BtcHotkeys._guardCache = list.ToArray();
		}
		return BtcHotkeys._guardCache;
	}

	private static string Join(KeyCode[] keys, string sep)
	{
		bool flag = keys == null || keys.Length == 0;
		bool flag2 = flag;
		bool flag3 = flag2;
		string result;
		if (flag3)
		{
			result = string.Empty;
		}
		else
		{
			string[] array = new string[keys.Length];
			for (int i = 0; i < keys.Length; i++)
			{
				array[i] = keys[i].ToString();
			}
			result = string.Join(sep, array);
		}
		return result;
	}

	private static bool TryParseCombo(string value, out KeyCode[] keys)
	{
		string[] array = (value ?? string.Empty).Split(new char[]
		{
			'+',
			','
		}, StringSplitOptions.RemoveEmptyEntries);
		List<KeyCode> list = new List<KeyCode>();
		for (int i = 0; i < array.Length; i++)
		{
			KeyCode item;
			bool flag = !BtcHotkeys.TryParseKey(array[i].Trim(), out item);
			bool flag2 = flag;
			bool flag3 = flag2;
			if (flag3)
			{
				keys = null;
				return false;
			}
			list.Add(item);
		}
		keys = list.ToArray();
		return true;
	}

	private static bool TryParseKey(string token, out KeyCode kc)
	{
		string text = BtcHotkeys.ResolveModifierAlias(token.Trim());
		kc = KeyCode.None;
		bool flag = text.Length == 0 || char.IsDigit(text[0]);
		bool flag2 = flag;
		bool flag3 = flag2;
		bool result;
		if (flag3)
		{
			result = false;
		}
		else
		{
			try
			{
				kc = (KeyCode)Enum.Parse(typeof(KeyCode), text, true);
				result = true;
			}
			catch (Exception)
			{
				kc = KeyCode.None;
				result = false;
			}
		}
		return result;
	}

	private static string ResolveModifierAlias(string token)
	{
		string a = token.ToLowerInvariant();
		bool flag = a == "shift";
		bool flag2 = flag;
		bool flag3 = flag2;
		string result;
		if (flag3)
		{
			result = "LeftShift";
		}
		else
		{
			bool flag4 = a == "ctrl" || a == "control";
			bool flag5 = flag4;
			bool flag6 = flag5;
			if (flag6)
			{
				result = "LeftControl";
			}
			else
			{
				bool flag7 = a == "alt";
				bool flag8 = flag7;
				bool flag9 = flag8;
				if (flag9)
				{
					result = "LeftAlt";
				}
				else
				{
					result = token;
				}
			}
		}
		return result;
	}

	private static string ComboNorm(KeyCode[] k)
	{
		bool flag = k == null || k.Length == 0;
		bool flag2 = flag;
		bool flag3 = flag2;
		string result;
		if (flag3)
		{
			result = string.Empty;
		}
		else
		{
			bool flag4 = k.Length == 1;
			bool flag5 = flag4;
			bool flag6 = flag5;
			if (flag6)
			{
				result = k[0].ToString();
			}
			else
			{
				List<int> list = new List<int>();
				for (int i = 0; i < k.Length - 1; i++)
				{
					list.Add((int)k[i]);
				}
				list.Sort();
				StringBuilder stringBuilder = new StringBuilder();
				for (int j = 0; j < list.Count; j++)
				{
					stringBuilder.Append(list[j]).Append('+');
				}
				stringBuilder.Append('|').Append(k[k.Length - 1].ToString());
				result = stringBuilder.ToString();
			}
		}
		return result;
	}

	private static void RebuildConflicts()
	{
		BtcHotkeys._conflictVersion = BtcHotkeys._version;
		BtcHotkeys._conflicts.Clear();
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		for (int i = 0; i < BtcHotkeys._defs.Count; i++)
		{
			BtcHotkeys.Def def = BtcHotkeys._defs[i];
			bool flag = def.kind != BtcHotkeys.Kind.Key || def.any;
			bool flag2 = !flag;
			bool flag3 = flag2;
			if (flag3)
			{
				KeyCode[][] alts = BtcHotkeys.GetAlts(def.id);
				for (int a = 0; a < alts.Length; a++)
				{
					string text = BtcHotkeys.ComboNorm(alts[a]);
					if (text.Length == 0)
					{
						continue;
					}
					string item;
					if (dictionary.TryGetValue(text, out item))
					{
						if (item != def.id)
						{
							BtcHotkeys._conflicts.Add(item);
							BtcHotkeys._conflicts.Add(def.id);
						}
					}
					else
					{
						dictionary[text] = def.id;
					}
				}
			}
		}
	}

	private static void BuildCatalog()
	{
		BtcHotkeys._defs = new List<BtcHotkeys.Def>();
		BtcHotkeys._groups = new List<string>();
		BtcHotkeys._defById = new Dictionary<string, BtcHotkeys.Def>(StringComparer.OrdinalIgnoreCase);
		BtcHotkeys.AddKey("Safety", "DisableIfHolding", "While one of these keys is held, all hotkeys are off", 0, "LeftControl", true);
		BtcHotkeys.AddKey("Safety", "NoPieMenuIfHolding", "While this key is held, holding RMB does not open the pie menu", 0, "", false);
		string group = "Camera: remember and return";
		BtcHotkeys.AddKey(group, "camera_bookmark_cycle", "Remember / return (camera and selection)", 0, "Mouse4", false);
		BtcHotkeys.AddKey(group, "camera_bookmark_record", "Remember only (no jump)", 0, "LeftControl+Mouse4", false);
		group = "Camera: slots";
		for (int i = 1; i <= 5; i++)
		{
			BtcHotkeys.AddKey(group, "camera_slot_save_" + i, "Save slot", i, "LeftAlt+Alpha" + i, false);
		}
		for (int j = 1; j <= 5; j++)
		{
			BtcHotkeys.AddKey(group, "camera_slot_goto_" + j, "Go to slot", j, "Alpha" + j, false);
		}
		BtcHotkeys.AddKey(group, "camera_slot_next", "Next occupied slot", 0, "LeftShift+Alpha1", false);
		BtcHotkeys.AddKey(group, "camera_slot_prev", "Previous occupied slot", 0, string.Empty, false);
		group = "Camera: fast modes";
		for (int k = 1; k <= 3; k++)
		{
			BtcHotkeys._curSub = "Fast mode " + k;
			BtcHotkeys.AddKey(group, "camera_fast_move_" + k, "Key", 0, (k == 1) ? "Mouse2" : string.Empty, false);
			BtcHotkeys.AddMode(group, "camera_fast_move_" + k + "_mode", "Mode (hold / toggle)", (k == 1) ? "toggle" : "hold");
			BtcHotkeys.AddFloat(group, "camera_fast_move_" + k + "_translation_multiplier", "Move speed multiplier", (k == 1) ? "3" : "2.5", 0.5f, 10f, 0.1f, "0.0", "x");
			BtcHotkeys.AddFloat(group, "camera_fast_move_" + k + "_zoom_multiplier", "Zoom speed multiplier", (k == 1) ? "1.5" : "2.5", 0.5f, 10f, 0.1f, "0.0", "x");
			BtcHotkeys.AddFloat(group, "camera_fast_move_" + k + "_orbit_multiplier", "Orbit speed multiplier", (k == 1) ? "1.5" : "2.5", 0.5f, 10f, 0.1f, "0.0", "x");
		}
		BtcHotkeys._curSub = null;
		group = "View";
		BtcHotkeys.AddKey("Mod menu", "menu_toggle", "Open / close the mod menu", 0, "PageDown", false);
		BtcHotkeys.AddKey("Mod menu", "ui_editor", "Open / close the UI editor (customise the whole mod interface)", 0, "F7", false);
		BtcHotkeys.AddKey(group, "visual_compare", "Hold to see the vanilla look (colored HP and overlays off), release to get yours back", 0, string.Empty, false);
		BtcHotkeys.AddKey(group, "xray_toggle", "Toggle X-Ray view", 0, "LeftShift+T", false);
		BtcHotkeys.AddKey(group, "camera_focus_selection", "Jump camera to the selection", 0, "Home", false);
		BtcHotkeys.AddKey(group, "overlay_toggle", "Show / hide the groups panel", 0, "O", false);
		group = "Control groups";
		string[] array = new string[]
		{
			"LeftShift+LeftControl+Z",
			"LeftShift+LeftControl+X",
			"LeftShift+LeftControl+F",
			"LeftShift+LeftControl+R",
			string.Empty
		};
		string[] array2 = new string[]
		{
			"Z",
			"X",
			"F",
			"R",
			string.Empty
		};
		for (int l = 1; l <= 5; l++)
		{
			BtcHotkeys.AddKey(group, "group_save_" + l, "Save group", l, array[l - 1], false);
		}
		for (int m = 1; m <= 5; m++)
		{
			BtcHotkeys.AddKey(group, "group_select_" + m, "Select group", m, array2[m - 1], false);
		}
		BtcHotkeys.AddKey(group, "group_save_first_empty", "Save selection into the first free group", 0, "LeftControl+LeftShift+E", false);
		BtcHotkeys.AddKey(group, "group_clear_all", "Clear all groups", 0, "LeftShift+LeftControl+T", false);
		BtcHotkeys.AddKey(group, "group_cycle", "Cycle through occupied groups", 0, "Space", false);
		BtcHotkeys.AddKey(group, "group_cycle_nocam", "Cycle through occupied groups (no camera jump)", 0, "LeftShift+Space", false);
		BtcHotkeys.AddKey(group, "group_auto_assign", "Auto-assign units to free groups by proximity", 0, "B", false);
		BtcHotkeys.AddFloat(group, "group_auto_cluster_radius", "Auto-group cluster radius (blocks)", "6", 1f, 30f, 0.5f, "0.0", string.Empty);
		group = "Orders";
		BtcHotkeys.AddBool(group, "order_any_distance", "Give orders from any camera distance (vanilla ignores clicks when the camera is zoomed out too far)", true);
		BtcHotkeys.AddKey("Timer", "timer_mark", "Place a replay timeline mark (10 s back, shown in your colour)", 0, string.Empty, false);
		BtcReplayKeys.AddKeys();
		BtcHotkeys.AddKey(group, "dig_voxel", "Dig the block under the cursor (selected units)", 0, "G", false);
		BtcHotkeys.AddKey(group, "break_object", "Break object under the cursor (selected units)", 0, string.Empty, false);
		BtcHotkeys.AddKey(group, "focus_mark", "Artificer focus mark: mark the enemy under the cursor for Sentinel Wards (selected Artificer)", 0, string.Empty, false);
		BtcHotkeys.AddKey(group, "shoot_at", "Shoot at the block or object under the cursor (selected archers, not Artificers)", 0, string.Empty, false);
		BtcHotkeys.AddKey(group, "drop_luggage", "Drop the carried object (barrel etc.) at the cursor (selected units)", 0, string.Empty, false);
		BtcHotkeys.AddKey(group, "go_detonate", "Go detonate the barrel under the cursor (selected units)", 0, string.Empty, false);
		BtcHotkeys.AddKey(group, "waypoint_add", "Add a waypoint at the cursor (selected units walk the queue in order)", 0, "V", false);
		BtcHotkeys.AddFloat(group, "waypoint_radius", "Waypoint arrival radius (blocks)", "2", 0.5f, 10f, 0.5f, "0.0", string.Empty);
		BtcHotkeys.AddFloat(group, "waypoint_stuck", "Waypoint: skip it when the unit stands still for (sec, 0 = never)", "2", 0f, 6f, 0.5f, "0.0", string.Empty);
		BtcHotkeys.AddFloat(group, "waypoint_lead", "Waypoint look-ahead (sec, go to the next point early so units do not stop)", "0.35", 0f, 1.5f, 0.05f, "0.00", string.Empty);
		BtcHotkeys.AddKey(group, "line_move", "Hold and sweep over blocks: each selected unit goes to its own block (line formation)", 0, string.Empty, false);
		BtcHotkeys.AddMode(group, "line_move_mode", "Line-move: mode (hold / toggle)", "hold");
		BtcHotkeys.AddFloat(group, "line_move_nearest", "Line-move: pick the nearest free unit for each block (0 = selection order, 1 = nearest)", "1", 0f, 1f, 1f, "0", string.Empty);
		BtcHotkeys.AddKey(group, "line_move_diagonal", "Hold and sweep over blocks: like line-move, but units go only on diagonals (like a chess bishop)", 0, string.Empty, false);
		group = "Match statistics";
		BtcHotkeys.AddKey(group, "match_stats_panel", "Open / close the match statistics panel (graphs and table; Esc closes)", 0, "LeftShift+F7", false);
		group = "Select all units of a profession";
		BtcHotkeys.AddKey(group, "job_Builder", "Select all Builders", 0, string.Empty, false);
		BtcHotkeys.AddKey(group, "job_Halberdier", "Select all Halberdiers", 0, string.Empty, false);
		BtcHotkeys.AddKey(group, "job_Knight", "Select all Knights", 0, string.Empty, false);
		BtcHotkeys.AddKey(group, "job_Archer", "Select all Archers", 0, string.Empty, false);
		BtcHotkeys.AddKey(group, "job_Arbalist", "Select all Arbalists", 0, string.Empty, false);
		BtcHotkeys.AddKey(group, "job_Alchemist", "Select all Alchemists", 0, string.Empty, false);
		BtcHotkeys.AddKey(group, "job_Artificer", "Select all Artificers", 0, string.Empty, false);
	}

	private static BtcHotkeys.Def NewDef(string group, string id, string label, BtcHotkeys.Kind kind, string def)
	{
		BtcHotkeys.Def def2 = new BtcHotkeys.Def();
		def2.group = group;
		def2.id = id;
		def2.label = label;
		def2.kind = kind;
		def2.def = def;
		def2.sub = BtcHotkeys._curSub;
		bool flag = !BtcHotkeys._groups.Contains(group);
		bool flag2 = flag;
		bool flag3 = flag2;
		if (flag3)
		{
			BtcHotkeys._groups.Add(group);
		}
		BtcHotkeys._defs.Add(def2);
		BtcHotkeys._defById[id] = def2;
		return def2;
	}

	internal static void AddKey(string group, string id, string label, int num, string def, bool any)
	{
		BtcHotkeys.Def def2 = BtcHotkeys.NewDef(group, id, label, BtcHotkeys.Kind.Key, def);
		def2.num = num;
		def2.any = any;
	}

	private static void AddMode(string group, string id, string label, string def)
	{
		BtcHotkeys.NewDef(group, id, label, BtcHotkeys.Kind.Mode, def);
	}

	private static void AddBool(string group, string id, string label, bool def)
	{
		BtcHotkeys.NewDef(group, id, label, BtcHotkeys.Kind.Bool, def ? "1" : "0");
	}

	private static void AddFloat(string group, string id, string label, string def, float min, float max, float step, string format, string prefix)
	{
		BtcHotkeys.Def def2 = BtcHotkeys.NewDef(group, id, label, BtcHotkeys.Kind.Float, def);
		def2.min = min;
		def2.max = max;
		def2.step = step;
		def2.format = format;
		def2.prefix = prefix;
	}

	public static bool IsMouseClickKey(KeyCode k)
	{
		return k == KeyCode.Mouse0 || k == KeyCode.Mouse1;
	}

	public static bool PointerOverUi()
	{
		try
		{
			EventSystem current = EventSystem.current;
			return current != null && current.IsPointerOverGameObject();
		}
		catch (Exception)
		{
		}
		return false;
	}

	public static bool MouseBlockedByUi(KeyCode[] combo)
	{
		bool flag = combo == null || combo.Length == 0;
		bool flag2 = flag;
		bool result;
		if (flag2)
		{
			result = false;
		}
		else
		{
			bool flag3 = false;
			for (int i = 0; i < combo.Length; i++)
			{
				bool flag4 = BtcHotkeys.IsMouseClickKey(combo[i]);
				bool flag5 = flag4;
				if (flag5)
				{
					flag3 = true;
					break;
				}
			}
			result = (flag3 && BtcHotkeys.PointerOverUi());
		}
		return result;
	}

	private static void UpdateClickSwallow()
	{
		bool flag = !BtcHotkeys._swallowClick;
		bool flag2 = !flag;
		if (flag2)
		{
			bool flag3 = Input.GetKey(KeyCode.Mouse0) || Input.GetKey(KeyCode.Mouse1);
			bool flag4 = flag3;
			if (flag4)
			{
				BtcHotkeys._swallowReleaseFrame = -1;
			}
			else
			{
				bool flag5 = BtcHotkeys._swallowReleaseFrame < 0;
				bool flag6 = flag5;
				if (flag6)
				{
					BtcHotkeys._swallowReleaseFrame = Time.frameCount;
				}
				else
				{
					bool flag7 = Time.frameCount > BtcHotkeys._swallowReleaseFrame;
					bool flag8 = flag7;
					if (flag8)
					{
						BtcHotkeys._swallowClick = false;
					}
				}
			}
		}
	}

	public static bool UiClickSwallowed
	{
		get
		{
			return BtcHotkeys._swallowClick;
		}
	}

	public const string GuardId = "DisableIfHolding";

	private static bool _loaded;

	private static bool _enabled = true;

	private static bool _menuOpen;

	private static bool _dirty;

	private static float _dirtyAt;

	private static int _version;

	private static int _conflictVersion = -1;

	private static int _blockFrame = -1;

	private static bool _blockVal;

	private static string _capId;

	private static int _capStartFrame;

	private static int _capEndFrame = -100;

	private static KeyCode[] _guardCache;

	private static KeyCode[] _captureKeys;

	private static List<BtcHotkeys.Def> _defs;

	private static List<string> _groups;

	private static Dictionary<string, BtcHotkeys.Def> _defById;

	private static readonly Dictionary<string, string> _raw = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	private static readonly Dictionary<string, KeyCode[]> _keyCache = new Dictionary<string, KeyCode[]>(StringComparer.OrdinalIgnoreCase);

	private static readonly HashSet<string> _conflicts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private static readonly List<KeyCode> _capMods = new List<KeyCode>();

	private static readonly KeyCode[] ModKeys = new KeyCode[]
	{
		KeyCode.LeftControl,
		KeyCode.RightControl,
		KeyCode.LeftShift,
		KeyCode.RightShift,
		KeyCode.LeftAlt,
		KeyCode.RightAlt,
		KeyCode.CapsLock
	};

	private static string _curSub;

	private static bool _swallowClick;

	private static int _swallowReleaseFrame = -1;

	public enum Kind
	{
		Key,
		Float,
		Mode,
		Bool
	}

	public class Def
	{
		public string Title
		{
			get
			{
				string text = BtcLang.Tr(this.label);
				return (this.num > 0) ? (text + " " + this.num.ToString()) : text;
			}
		}

		public string id;

		public string group;

		public string label;

		public int num;

		public BtcHotkeys.Kind kind;

		public string def;

		public float min;

		public float max;

		public float step;

		public string format;

		public string prefix;

		public bool any;

		public string sub;
	}
}
}
