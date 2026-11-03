using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Brix.Network;
using UnityEngine;

namespace BetterThanCastle
{
public static class BtcGameplayUI
{
	private static string T(string en, string ru)
	{
		return BtcGameplay.T(en, ru);
	}

	public static void Draw(Event e, float w, float rowH, int fs, Color old)
	{
		BtcGameplayUI.EnsureStyles(fs);
		BtcGameplay.Tick();
		bool flag = e.rawType == EventType.MouseUp;
		bool flag2 = flag;
		if (flag2)
		{
			BtcGameplayUI._dragId = 0;
		}
		float num = 0f;
		bool isHost = BtcGameplay.IsHost;
		bool inSession = BtcGameplay.InSession;
		bool flag3 = !BtcGameplay.Enabled;
		bool flag4 = flag3;
		if (flag4)
		{
			BtcGameplayUI.Note(ref num, w, fs, BtcGameplayUI.T("Turn on BetterConnection first: Gameplay Settings only work for BetterConnection lobbies.", "Сначала включите BetterConnection: настройки геймплея работают только в лобби BetterConnection."));
			BtcGameplayUI.ContentH = num + rowH;
		}
		else
		{
			bool flag5 = isHost;
			bool flag6 = flag5;
			string text;
			if (flag6)
			{
				text = (BtcGameplay.CanEdit ? BtcGameplayUI.T("You are the host: changes apply to everybody at once.", "Вы хост: изменения сразу применяются у всех.") : BtcGameplayUI.T("You are the host. The game has started: rules are locked.", "Вы хост. Игра началась: правила заблокированы."));
			}
			else
			{
				bool flag7 = inSession;
				bool flag8 = flag7;
				if (flag8)
				{
					text = (BtcGameplayUI._draft ? BtcGameplayUI.T("Request mode: set what you want and send it to the host.", "Режим запроса: выберите, что хотите, и отправьте хосту.") : BtcGameplayUI.T("You are a client: these are the host's rules (read-only).", "Вы клиент: это правила хоста (только просмотр)."));
				}
				else
				{
					text = BtcGameplayUI.T("Host a lobby (or join one) to use these settings. Presets can be edited anywhere.", "Создайте лобби (или зайдите в чужое), чтобы менять настройки. Пресеты можно править где угодно.");
				}
			}
			BtcGameplayUI.Note(ref num, w, fs, text);
			string[] array = new string[]
			{
				BtcGameplayUI.T("Balance", "Баланс"),
				BtcGameplayUI.T("General", "Общее"),
				BtcGameplayUI.T("Handicap", "Фора"),
				BtcGameplayUI.T("Presets", "Пресеты"),
				BtcGameplayUI.T("Map", "Карта")
			};
			float num2 = (w - (float)fs * 2f) / (float)array.Length;
			for (int i = 0; i < array.Length; i++)
			{
				Rect r = new Rect((float)fs + num2 * (float)i, num + rowH * 0.05f, num2 - 4f, rowH * 0.85f);
				bool flag9 = BtcGameplayUI.Button(e, r, array[i], (i == BtcGameplayUI._subTab) ? BtcGameplayUI.Accent : Color.white, old, true);
				bool flag10 = flag9;
				if (flag10)
				{
					BtcGameplayUI._subTab = i;
				}
			}
			num += rowH;
			float[] shown = isHost ? BtcGameplay.Active : (BtcGameplayUI._draft ? BtcGameplay.Proposal : BtcGameplay.Active);
			bool interact = (isHost && BtcGameplay.CanEdit) || (!isHost && inSession && BtcGameplayUI._draft);
			bool flag11 = isHost && BtcGameplay.Pending.Count > 0;
			bool flag12 = flag11;
			if (flag12)
			{
				BtcGameplayUI.Note(ref num, w, fs, BtcGameplayUI.T("Requests from players are waiting at the bottom of this tab.", "Запросы игроков ждут внизу этой вкладки."));
			}
			int subTab = BtcGameplayUI._subTab;
			bool flag13 = subTab == 0;
			bool flag14 = flag13;
			if (flag14)
			{
				BtcGameplayUI.DrawBalance(e, ref num, w, rowH, fs, old, shown, interact, isHost, inSession);
			}
			else
			{
				bool flag15 = subTab == 1;
				bool flag16 = flag15;
				if (flag16)
				{
					BtcGameplayUI.DrawGeneral(e, ref num, w, rowH, fs, old, shown, interact);
				}
				else
				{
					bool flag17 = subTab == 2;
					bool flag18 = flag17;
					if (flag18)
					{
						BtcHandicap.Draw(e, ref num, w, rowH, fs, old);
					}
					else
					{
						bool flag19 = subTab == 4;
						bool flag20 = flag19;
						if (flag20)
						{
							try { BtcMapScale.Draw(e, ref num, w, rowH, fs, old); }
							catch (Exception ex) { BtcGameplayUI.LogDrawOnce("MapScale.Draw", ex); }
							try { BtcSymmetry.Draw(e, ref num, w, rowH, fs, old); }
							catch (Exception ex) { BtcGameplayUI.LogDrawOnce("Symmetry.Draw", ex); }
						}
						else
						{
							BtcGameplayUI.DrawPresets(e, ref num, w, rowH, fs, old, shown, isHost, inSession);
						}
					}
				}
			}
			BtcGameplayUI.DrawPlates(e, ref num, w, rowH, fs, old, isHost, inSession);
			BtcGameplayUI.ContentH = num + rowH * 0.4f;
		}
	}

	private static void ValueRow(Event e, ref float y, float w, float rowH, int fs, Color old, int i, float[] shown, bool interact)
	{
		BtcGameplay.Def def = BtcGameplay.Defs[i];
		float num = shown[i];
		bool flag = Mathf.Abs(num - def.Val) > 0.0001f || (def.Abs && shown[def.ModeIdx] > 0.5f);
		bool flag2 = !BtcGameplay.IsHost && BtcGameplayUI._draft && Mathf.Abs(num - BtcGameplay.Active[i]) > 0.0001f;
		Rect rect = new Rect(0f, y, w, rowH);
		y += rowH;
		BtcGameplayUI.RowBackground(e, rect, old);
		Color color = flag2 ? new Color(1f, 0.65f, 0.2f, 1f) : (flag ? BtcGameplayUI.Changed : Color.white);
		bool flag3 = !interact;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		bool flag9 = flag8;
		bool flag10 = flag9;
		if (flag10)
		{
			color.a = 0.8f;
		}
		bool isBool = def.IsBool;
		bool flag11 = isBool;
		bool flag12 = flag11;
		bool flag13 = flag12;
		bool flag14 = flag13;
		bool flag15 = flag14;
		bool flag16 = flag15;
		bool flag17 = flag16;
		if (flag17)
		{
			bool flag18 = e.type == EventType.Repaint;
			bool flag19 = flag18;
			bool flag20 = flag19;
			bool flag21 = flag20;
			bool flag22 = flag21;
			bool flag23 = flag22;
			bool flag24 = flag23;
			bool flag25 = flag24;
			if (flag25)
			{
				float num2 = (float)fs * 1.1f;
				Rect position = new Rect((float)fs, rect.y + (rowH - num2) * 0.5f, num2, num2);
				GUI.color = new Color(1f, 1f, 1f, 0.7f);
				GUI.DrawTexture(position, BtcGameplayUI.White());
				GUI.color = new Color(0.06f, 0.06f, 0.08f, 1f);
				GUI.DrawTexture(new Rect(position.x + 2f, position.y + 2f, position.width - 4f, position.height - 4f), BtcGameplayUI.White());
				bool flag26 = num > 0.5f;
				bool flag27 = flag26;
				bool flag28 = flag27;
				bool flag29 = flag28;
				bool flag30 = flag29;
				bool flag31 = flag30;
				bool flag32 = flag31;
				bool flag33 = flag32;
				if (flag33)
				{
					GUI.color = BtcGameplayUI.Accent;
					GUI.DrawTexture(new Rect(position.x + 4f, position.y + 4f, position.width - 8f, position.height - 8f), BtcGameplayUI.White());
				}
				GUI.color = old;
			}
			BtcGameplayUI.DrawLabel(new Rect((float)fs * 2.4f, rect.y, w * 0.8f, rowH), BtcGameplayUI.T(def.En, def.Ru), color);
			bool flag34 = GUI.Button(rect, GUIContent.none, GUIStyle.none) && interact && !BtcHotkeys.UiClickSwallowed;
			bool flag35 = flag34;
			bool flag36 = flag35;
			bool flag37 = flag36;
			bool flag38 = flag37;
			bool flag39 = flag38;
			bool flag40 = flag39;
			bool flag41 = flag40;
			if (flag41)
			{
				BtcGameplay.Set(i, (num > 0.5f) ? 0f : 1f);
			}
			BtcGameplayUI.Hint(ref y, w, fs, def);
		}
		else
		{
			bool flag42 = def.Abs && shown[def.ModeIdx] > 0.5f;
			int num3 = flag42 ? def.NumIdx : i;
			BtcGameplay.Def def2 = BtcGameplay.Defs[num3];
			float num4 = shown[num3];
			string text = BtcGameplayUI.T(def.En, def.Ru);
			bool flag43 = flag42;
			bool flag44 = flag43;
			bool flag45 = flag44;
			bool flag46 = flag45;
			bool flag47 = flag46;
			bool flag48 = flag47;
			bool flag49 = flag48;
			if (flag49)
			{
				text = text + " [" + BtcGameplayUI.T(def.UnitEn, def.UnitRu) + "]";
			}
			BtcGameplayUI.DrawLabel(new Rect((float)fs, rect.y, w * 0.38f - (float)fs, rowH), text, flag42 ? BtcGameplayUI.Accent : color);
			Rect hit = new Rect(w * 0.4f, rect.y, w * (def.Abs ? 0.25f : 0.34f), rowH);
			float num5 = BtcGameplayUI.SliderCore(e, hit, num3 + 1, num4, def2.Min, def2.Max, def2.Step, old, interact);
			bool abs = def.Abs;
			bool flag50 = abs;
			bool flag51 = flag50;
			bool flag52 = flag51;
			bool flag53 = flag52;
			bool flag54 = flag53;
			bool flag55 = flag54;
			if (flag55)
			{
				Rect r = new Rect(w * 0.66f, rect.y + rowH * 0.12f, w * 0.07f, rowH * 0.76f);
				bool flag56 = BtcGameplayUI.Button(e, r, flag42 ? "№" : "×", flag42 ? BtcGameplayUI.Accent : Color.white, old);
				bool flag57 = flag56 && interact;
				bool flag58 = flag57;
				bool flag59 = flag58;
				bool flag60 = flag59;
				bool flag61 = flag60;
				bool flag62 = flag61;
				bool flag63 = flag62;
				if (flag63)
				{
					BtcGameplay.Set(def.ModeIdx, flag42 ? 0f : 1f);
					bool flag64 = !flag42;
					bool flag65 = flag64;
					bool flag66 = flag65;
					bool flag67 = flag66;
					bool flag68 = flag67;
					bool flag69 = flag68;
					bool flag70 = flag69;
					if (flag70)
					{
						BtcGameplay.Set(def.NumIdx, BtcGameplay.SeenNum(i));
					}
					return;
				}
			}
			Rect r2 = new Rect(w * 0.76f, rect.y + rowH * 0.12f, w * 0.13f, rowH * 0.76f);
			float num6 = num5;
			num5 = BtcGameplayUI.NumField(e, r2, num3, num5, def2, interact);
			bool flag71 = interact && Mathf.Abs(num5 - num6) > 1E-05f && def2.Group != "ore" && (num5 < def2.Min - 1E-05f || num5 > def2.Max + 1E-05f);
			bool flag72 = flag71 && PlayerPrefs.GetInt("btc.gs.nowarn", 0) != 1;
			bool flag73 = flag72;
			bool flag74 = flag73;
			bool flag75 = flag74;
			bool flag76 = flag75;
			if (flag76)
			{
				BtcGameplayUI._warnIdx = num3;
				BtcGameplayUI._warnVal = num5;
				num5 = num4;
			}
			bool flag77 = interact && (flag || flag42);
			bool flag78 = flag77;
			bool flag79 = flag78;
			bool flag80 = flag79;
			bool flag81 = flag80;
			bool flag82 = flag81;
			bool flag83 = flag82;
			bool flag84 = flag83;
			if (flag84)
			{
				Rect r3 = new Rect(w * 0.9f, rect.y + rowH * 0.12f, w * 0.08f, rowH * 0.76f);
				bool flag85 = BtcGameplayUI.Button(e, r3, BtcGameplayUI.T("def", "сбр"), Color.white, old);
				bool flag86 = flag85;
				bool flag87 = flag86;
				bool flag88 = flag87;
				bool flag89 = flag88;
				bool flag90 = flag89;
				bool flag91 = flag90;
				bool flag92 = flag91;
				if (flag92)
				{
					bool abs2 = def.Abs;
					bool flag93 = abs2;
					bool flag94 = flag93;
					bool flag95 = flag94;
					bool flag96 = flag95;
					bool flag97 = flag96;
					bool flag98 = flag97;
					if (flag98)
					{
						BtcGameplay.Set(def.ModeIdx, 0f);
						BtcGameplay.Set(def.NumIdx, def.NumDefault);
					}
					float num7 = flag42 ? num4 : def.Val;
					bool flag99 = !flag42;
					bool flag100 = flag99;
					bool flag101 = flag100;
					bool flag102 = flag101;
					bool flag103 = flag102;
					bool flag104 = flag103;
					bool flag105 = flag104;
					if (flag105)
					{
						BtcGameplay.Set(i, def.Val);
					}
					return;
				}
			}
			bool flag106 = interact && Mathf.Abs(num5 - num4) > 1E-05f;
			bool flag107 = flag106;
			bool flag108 = flag107;
			bool flag109 = flag108;
			bool flag110 = flag109;
			bool flag111 = flag110;
			bool flag112 = flag111;
			bool flag113 = flag112;
			if (flag113)
			{
				BtcGameplay.Set(num3, num5);
			}
			BtcGameplayUI.DrawWarn(e, ref y, w, rowH, fs, old, num3, def2, interact);
			BtcGameplayUI.Hint(ref y, w, fs, def);
		}
	}

	private static void Hint(ref float y, float w, int fs, BtcGameplay.Def d)
	{
		string text = BtcGameplayUI.T(d.HintEn, d.HintRu);
		bool flag = !string.IsNullOrEmpty(text);
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			BtcGameplayUI.NoteRaw(ref y, w, fs, text);
		}
	}

	private static float NumField(Event e, Rect r, int i, float v, BtcGameplay.Def d, bool interact)
	{
		string text = "btcgs" + i;
		bool flag = GUI.GetNameOfFocusedControl() == text;
		bool flag2 = e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter);
		bool flag3 = !flag;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		bool flag9 = flag8;
		if (flag9)
		{
			bool flag10 = BtcGameplayUI._editing == i;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				BtcGameplayUI._editing = -1;
				string s = null;
				float num = 0f;
				bool flag17 = BtcGameplayUI.Buf.TryGetValue(i, out s) && BtcGameplayUI.TryParse(s, out num);
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				if (flag23)
				{
					v = num;
				}
			}
			bool flag24 = e.type == EventType.MouseDown && r.Contains(e.mousePosition);
			BtcGameplayUI.Buf[i] = ((d.HasMapDefault && v < 0f && !flag24) ? BtcGameplayUI.T("map", "карта") : BtcGameplayUI.NumStr(d, v));
			bool flag25 = flag24 && d.HasMapDefault && v < 0f;
			bool flag26 = flag25;
			bool flag27 = flag26;
			bool flag28 = flag27;
			bool flag29 = flag28;
			bool flag30 = flag29;
			bool flag31 = flag30;
			if (flag31)
			{
				BtcGameplayUI.Buf[i] = "-1";
			}
		}
		bool flag32 = !interact;
		bool flag33 = flag32;
		bool flag34 = flag33;
		bool flag35 = flag34;
		bool flag36 = flag35;
		bool flag37 = flag36;
		bool flag38 = flag37;
		float result;
		if (flag38)
		{
			GUI.Label(r, BtcGameplayUI.Buf[i], BtcGameplayUI._field);
			result = v;
		}
		else
		{
			GUI.SetNextControlName(text);
			string s2 = GUI.TextField(r, BtcGameplayUI.Buf[i], 10, BtcGameplayUI._field);
			bool flag39 = flag;
			bool flag40 = flag39;
			bool flag41 = flag40;
			bool flag42 = flag41;
			bool flag43 = flag42;
			bool flag44 = flag43;
			bool flag45 = flag44;
			if (flag45)
			{
				BtcGameplayUI._editing = i;
				BtcGameplayUI.Buf[i] = BtcGameplayUI.Filter(s2);
				bool flag46 = flag2;
				bool flag47 = flag46;
				bool flag48 = flag47;
				bool flag49 = flag48;
				bool flag50 = flag49;
				bool flag51 = flag50;
				bool flag52 = flag51;
				if (flag52)
				{
					float num2 = 0f;
					bool flag53 = BtcGameplayUI.TryParse(BtcGameplayUI.Buf[i], out num2);
					bool flag54 = flag53;
					bool flag55 = flag54;
					bool flag56 = flag55;
					bool flag57 = flag56;
					bool flag58 = flag57;
					bool flag59 = flag58;
					if (flag59)
					{
						v = num2;
					}
					BtcGameplayUI._editing = -1;
					GUI.FocusControl(string.Empty);
					e.Use();
				}
			}
			result = v;
		}
		return result;
	}

	private static string NumStr(BtcGameplay.Def d, float v)
	{
		return v.ToString((d.Step >= 1f) ? "0" : "0.##", CultureInfo.InvariantCulture);
	}

	private static string Filter(string s)
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (char c in s)
		{
			bool flag = (c >= '0' && c <= '9') || c == '.' || c == ',' || c == '-';
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				stringBuilder.Append(c);
			}
		}
		return stringBuilder.ToString();
	}

	private static bool TryParse(string s, out float v)
	{
		return float.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
	}

	private static void DrawPresets(Event e, ref float y, float w, float rowH, int fs, Color old, float[] shown, bool host, bool session)
	{
		BtcGameplayUI.Note(ref y, w, fs, BtcGameplayUI.T("Presets store every setting of this tab. Host: Load applies at once. Client: Propose sends it to the host as a request.", "Пресеты хранят все настройки вкладки. Хост: Load применяет сразу. Клиент: Propose отправляет хосту запросом."));
		Rect position = new Rect((float)fs, y + rowH * 0.1f, w * 0.5f, rowH * 0.8f);
		BtcGameplayUI._presetName = GUI.TextField(position, BtcGameplayUI._presetName, 24, BtcGameplayUI._field);
		Rect r = new Rect((float)fs + w * 0.5f + 6f, y + rowH * 0.1f, w * 0.3f, rowH * 0.8f);
		bool flag = BtcGameplayUI.Button(e, r, BtcGameplayUI.T("Save current", "Сохранить текущие"), BtcGameplayUI.Accent, old, true);
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			BtcGameplay.SavePreset(BtcGameplayUI._presetName, shown);
		}
		y += rowH;
		for (int i = 0; i < BtcGameplay.Presets.Count; i++)
		{
			BtcGameplay.Preset preset = BtcGameplay.Presets[i];
			Rect row = new Rect(0f, y, w, rowH);
			y += rowH;
			BtcGameplayUI.RowBackground(e, row, old);
			BtcGameplayUI.DrawLabel(new Rect((float)fs, row.y, w * 0.4f, rowH), preset.Name + (preset.BuiltIn ? "  *" : string.Empty), Color.white);
			float num = w * 0.45f;
			float num2 = w * 0.16f;
			if (host)
			{
				bool flag9 = BtcGameplayUI.Button(e, new Rect(num, row.y + rowH * 0.1f, num2, rowH * 0.8f), BtcGameplayUI.T("Load", "Загрузить"), BtcGameplay.CanEdit ? Color.white : BtcGameplayUI.Dim, old) && BtcGameplay.CanEdit;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					BtcGameplay.SetAll(BtcGameplay.Decode(preset.Data));
				}
			}
			else if (session)
			{
				bool flag17 = BtcGameplayUI.Button(e, new Rect(num, row.y + rowH * 0.1f, num2, rowH * 0.8f), BtcGameplayUI.T("To draft", "В черновик"), Color.white, old);
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				if (flag24)
				{
					BtcGameplay.Proposal = BtcGameplay.Decode(preset.Data);
					BtcGameplayUI._draft = true;
					BtcGameplayUI._subTab = 0;
				}
				bool flag25 = BtcGameplayUI.Button(e, new Rect(num + num2 + 4f, row.y + rowH * 0.1f, num2, rowH * 0.8f), BtcGameplayUI.T("Propose", "Предложить"), BtcGameplayUI.Accent, old);
				bool flag26 = flag25;
				bool flag27 = flag26;
				bool flag28 = flag27;
				bool flag29 = flag28;
				bool flag30 = flag29;
				bool flag31 = flag30;
				bool flag32 = flag31;
				if (flag32)
				{
					BtcGameplay.Proposal = BtcGameplay.Decode(preset.Data);
					BtcGameplay.SendRequest(BtcGameplay.Proposal);
				}
			}
			bool flag33 = !preset.BuiltIn && BtcGameplayUI.Button(e, new Rect(w - w * 0.12f - (float)fs * 0.5f, row.y + rowH * 0.1f, w * 0.12f, rowH * 0.8f), BtcGameplayUI.T("Delete", "Удалить"), BtcGameplayUI.Warn, old);
			bool flag34 = flag33;
			bool flag35 = flag34;
			bool flag36 = flag35;
			bool flag37 = flag36;
			bool flag38 = flag37;
			bool flag39 = flag38;
			bool flag40 = flag39;
			if (flag40)
			{
				BtcGameplay.DeletePreset(i);
				break;
			}
		}
	}

	private static void DrawPlates(Event e, ref float y, float w, float rowH, int fs, Color old, bool host, bool session)
	{
		if (host)
		{
			for (int i = BtcGameplay.Pending.Count - 1; i >= 0; i--)
			{
				BtcGameplay.PendingReq pendingReq = BtcGameplay.Pending[i];
				float[] active = BtcGameplay.Active;
				List<string> list = BtcGameplay.DiffLines(pendingReq.Vals);
				Rect position = new Rect((float)fs * 0.5f, y + 2f, w - (float)fs, rowH * 0.95f);
				y += rowH;
				bool flag = e.type == EventType.Repaint;
				bool flag2 = flag;
				bool flag3 = flag2;
				bool flag4 = flag3;
				bool flag5 = flag4;
				bool flag6 = flag5;
				bool flag7 = flag6;
				bool flag8 = flag7;
				if (flag8)
				{
					GUI.color = new Color(1f, 1f, 1f, 0.08f);
					GUI.DrawTexture(position, BtcGameplayUI.White());
					GUI.color = old;
				}
				BtcGameplayUI.DrawLabel(new Rect(position.x + (float)fs * 0.5f, position.y, position.width * 0.5f, position.height), string.Concat(new object[]
				{
					pendingReq.Name,
					": ",
					list.Count,
					" ",
					BtcGameplayUI.T("changes", "изменений")
				}), BtcGameplayUI.Changed);
				float num = position.width * 0.13f;
				bool flag9 = BtcGameplayUI.Button(e, new Rect(position.x + position.width * 0.52f, position.y + 3f, num, position.height - 6f), BtcGameplayUI.T("View", "Смотреть"), Color.white, old);
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					BtcGameplayUI._expanded = ((BtcGameplayUI._expanded == pendingReq) ? null : pendingReq);
				}
				bool flag17 = BtcGameplayUI.Button(e, new Rect(position.x + position.width * 0.52f + num + 4f, position.y + 3f, num, position.height - 6f), "OK", BtcGameplayUI.Accent, old, true);
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				if (flag24)
				{
					BtcGameplay.Resolve(pendingReq, true);
					break;
				}
				bool flag25 = BtcGameplayUI.Button(e, new Rect(position.x + position.width * 0.52f + num * 2f + 8f, position.y + 3f, num, position.height - 6f), "X", BtcGameplayUI.Warn, old, true);
				bool flag26 = flag25;
				bool flag27 = flag26;
				bool flag28 = flag27;
				bool flag29 = flag28;
				bool flag30 = flag29;
				bool flag31 = flag30;
				bool flag32 = flag31;
				if (flag32)
				{
					BtcGameplay.Resolve(pendingReq, false);
					break;
				}
				bool flag33 = BtcGameplayUI._expanded == pendingReq;
				bool flag34 = flag33;
				bool flag35 = flag34;
				bool flag36 = flag35;
				bool flag37 = flag36;
				bool flag38 = flag37;
				bool flag39 = flag38;
				bool flag40 = flag39;
				if (flag40)
				{
					int num2 = 0;
					while (num2 < list.Count && num2 < 24)
					{
						BtcGameplayUI.NoteRaw(ref y, w, fs, list[num2]);
						num2++;
					}
				}
			}
		}
		else
		{
			bool flag41 = session && BtcGameplay.MyReq > BtcGameplay.ReqState.None;
			bool flag42 = flag41;
			bool flag43 = flag42;
			bool flag44 = flag43;
			bool flag45 = flag44;
			bool flag46 = flag45;
			bool flag47 = flag46;
			bool flag48 = flag47;
			if (flag48)
			{
				Color c = Color.white;
				BtcGameplay.ReqState myReq = BtcGameplay.MyReq;
				bool flag49 = myReq != BtcGameplay.ReqState.Waiting;
				bool flag50 = flag49;
				bool flag51 = flag50;
				bool flag52 = flag51;
				bool flag53 = flag52;
				bool flag54 = flag53;
				bool flag55 = flag54;
				string text;
				if (flag55)
				{
					bool flag56 = myReq != BtcGameplay.ReqState.Approved;
					bool flag57 = flag56;
					bool flag58 = flag57;
					bool flag59 = flag58;
					bool flag60 = flag59;
					bool flag61 = flag60;
					bool flag62 = flag61;
					if (flag62)
					{
						text = BtcGameplayUI.T("The host declined your request.", "Хост отклонил ваш запрос.");
						c = BtcGameplayUI.Warn;
					}
					else
					{
						text = BtcGameplayUI.T("The host accepted your request.", "Хост принял ваш запрос.");
						c = BtcGameplayUI.Accent;
					}
				}
				else
				{
					text = BtcGameplayUI.T("Request sent, waiting for the host...", "Запрос отправлен, ждём хоста...");
				}
				BtcGameplayUI.DrawLabel(new Rect((float)fs, y, w - (float)fs * 2f, rowH), text, c);
				y += rowH;
			}
		}
	}

	private static readonly System.Collections.Generic.HashSet<string> _drawLogged = new System.Collections.Generic.HashSet<string>();

	private static void LogDrawOnce(string where, Exception ex)
	{
		if (_drawLogged.Add(where)) BtcLog.Error("Hook", "BtcGameplayUI Map tab " + where + ": " + ex);
	}

	public static void OnGUI()
	{
		Event current = Event.current;
		BtcLobby.Tick();
		BtcSpectate.OnGUI();
		bool flag = current.type == EventType.Repaint;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			BtcGameplay.Tick();
		}
		bool yellow = BtcGameplay.Warn != 0 && BtcGameplay.Enabled;
		bool red = BtcGameplay.EditorWarn && BtcGameplay.Enabled;
		if (!yellow && !red)
		{
			return;
		}
		int num = Mathf.Max(14, Screen.height / 38);
		BtcGameplayUI.EnsureStyles(num);
		GUI.depth = -1100;
		float top = (float)num * 0.5f;
		if (red)
		{
			bool ok;
			float h = BtcGameplayUI.DrawWarnBanner(current, num, top, new Color(0.45f, 0.06f, 0.06f, 0.97f), BtcGameplayUI.T("The host enabled the editor panel", "Хост включил эдитор-панель"), BtcGameplayUI.T("The editor panel is on in this game. This may cause unfair play.", "В этой игре включена эдитор-панель. Это может привести к нечестной игре."), false, out ok);
			top += h + (float)num * 0.3f;
			if (ok)
			{
				BtcGameplay.EditorWarn = false;
			}
		}
		if (yellow)
		{
			int count = BtcGameplay.DiffLines(BtcGameplay.WarnData ?? BtcGameplay.Active).Count;
			string text = (BtcGameplay.Warn == 1) ? BtcGameplayUI.T("This is not a vanilla game", "Это неванильная игра") : BtcGameplayUI.T("The host changed the rules", "Хост изменил правила");
			string text2 = (BtcGameplay.Warn == 1) ? BtcGameplayUI.T("The host plays with custom gameplay rules (" + count + " changes from vanilla). Damage, HP, speed or economy may differ from what you know.", "Хост играет с изменёнными правилами (" + count + " отличий от ванили). Урон, ХП, скорость или экономика могут отличаться от привычных.") : BtcGameplayUI.T("The host changed the gameplay rules while you are in the lobby (" + count + " changes from vanilla now).", "Хост изменил правила геймплея, пока вы в лобби (сейчас " + count + " отличий от ванили).");
			bool ok;
			BtcGameplayUI.DrawWarnBanner(current, num, top, new Color(0.30f, 0.24f, 0.02f, 0.97f), text, text2, true, out ok);
			if (ok)
			{
				BtcGameplay.Warn = 0;
				BtcGameplayUI._warnList = false;
			}
		}
	}

	// Compact non-blocking banner at the top of the screen. Returns its height. ok = OK pressed.
	private static float DrawWarnBanner(Event current, int num, float top, Color bg, string title, string text, bool canList, out bool ok)
	{
		ok = false;
		float lineH = (float)num * 1.6f;
		float width = Mathf.Min((float)Screen.width * 0.6f, (float)num * 40f);
		float inner = width - (float)num * 2f;
		float textH = BtcGameplayUI._note.CalcHeight(new GUIContent(text), inner);
		bool list = canList && BtcGameplayUI._warnList;
		float listH = list ? lineH * 5f : 0f;
		float height = lineH + textH + listH + lineH * 1.1f + (float)num * 0.6f;
		Rect position = new Rect(((float)Screen.width - width) * 0.5f, top, width, height);
		Color color = GUI.color;
		if (current.type == EventType.Repaint)
		{
			GUI.color = bg;
			GUI.DrawTexture(position, BtcGameplayUI.White());
			GUI.color = color;
		}
		GUI.Label(new Rect(position.x, position.y + (float)num * 0.2f, position.width, lineH), title, BtcGameplayUI._title);
		float y = position.y + lineH * 1.1f;
		GUI.Label(new Rect(position.x + (float)num, y, inner, textH), text, BtcGameplayUI._note);
		y += textH + (float)num * 0.2f;
		if (list)
		{
			List<string> lines = BtcGameplay.DiffLines(BtcGameplay.WarnData ?? BtcGameplay.Active);
			Rect view = new Rect(position.x + (float)num, y, inner, listH);
			Rect content = new Rect(0f, 0f, inner - (float)num, Mathf.Max(listH, (float)(lines.Count * num) * 1.3f));
			BtcGameplayUI._warnScroll = GUI.BeginScrollView(view, BtcGameplayUI._warnScroll, content);
			for (int i = 0; i < lines.Count; i++)
			{
				GUI.Label(new Rect(0f, (float)(i * num) * 1.3f, content.width, (float)num * 1.3f), lines[i], BtcGameplayUI._label);
			}
			GUI.EndScrollView();
			y += listH + (float)num * 0.2f;
		}
		float bw = canList ? (inner * 0.5f - 4f) : (inner * 0.4f);
		float by = position.yMax - lineH * 1.05f;
		if (canList)
		{
			Rect r = new Rect(position.x + (float)num, by, bw, lineH * 0.9f);
			if (BtcGameplayUI.Button(current, r, BtcGameplayUI._warnList ? BtcGameplayUI.T("Hide changes", "Скрыть изменения") : BtcGameplayUI.T("View changes", "Посмотреть изменения"), Color.white, color, true))
			{
				BtcGameplayUI._warnList = !BtcGameplayUI._warnList;
			}
		}
		Rect r2 = new Rect(position.xMax - (float)num - bw, by, bw, lineH * 0.9f);
		if (BtcGameplayUI.Button(current, r2, "OK", BtcGameplayUI.Accent, color, true))
		{
			ok = true;
		}
		return height;
	}

	private static bool GroupHeader(Event e, ref float y, float w, float rowH, int fs, Color old, string title, bool open, int changed)
	{
		Rect rect = new Rect(0f, y, w, rowH);
		y += rowH;
		BtcGameplayUI.RowBackground(e, rect, old);
		string text = (open ? "v  " : ">  ") + title + ((changed > 0) ? ("   (" + changed + ")") : string.Empty);
		GUI.Label(rect, text, BtcGameplayUI._sub);
		return GUI.Button(rect, GUIContent.none, GUIStyle.none) && !BtcHotkeys.UiClickSwallowed;
	}

	private static void DrawLabel(Rect r, string text, Color c)
	{
		Color textColor = BtcGameplayUI._label.normal.textColor;
		BtcGameplayUI._label.normal.textColor = c;
		GUI.Label(r, text, BtcGameplayUI._label);
		BtcGameplayUI._label.normal.textColor = textColor;
	}

	private static void Note(ref float y, float w, int fs, string text)
	{
		BtcGameplayUI.NoteRaw(ref y, w, fs, text);
	}

	private static void NoteRaw(ref float y, float w, int fs, string text)
	{
		float num = BtcGameplayUI._note.CalcHeight(new GUIContent(text), w - (float)fs * 2f);
		GUI.Label(new Rect((float)fs, y + 2f, w - (float)fs * 2f, num), text, BtcGameplayUI._note);
		y += num + (float)fs * 0.5f;
	}

	private static bool Button(Event e, Rect r, string text, Color color, Color old)
	{
		return BtcGameplayUI.Button(e, r, text, color, old, true);
	}

	private static bool Button(Event e, Rect r, string text, Color color, Color old, bool wide)
	{
		bool flag = e.type == EventType.Repaint;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			GUI.color = (r.Contains(e.mousePosition) ? new Color(1f, 1f, 1f, 0.16f) : new Color(1f, 1f, 1f, 0.07f));
			GUI.DrawTexture(r, BtcGameplayUI.White());
			GUI.color = old;
		}
		GUIStyle guistyle = wide ? BtcGameplayUI._btn : BtcGameplayUI._small;
		Color textColor = guistyle.normal.textColor;
		guistyle.normal.textColor = color;
		bool flag9 = GUI.Button(r, text, guistyle);
		bool flag10 = flag9 && BtcHotkeys.UiClickSwallowed;
		bool flag11 = flag10;
		bool flag12 = flag11;
		bool flag13 = flag12;
		bool flag14 = flag13;
		bool flag15 = flag14;
		bool flag16 = flag15;
		bool flag17 = flag16;
		if (flag17)
		{
			flag9 = false;
		}
		guistyle.normal.textColor = textColor;
		return flag9;
	}

	private static void RowBackground(Event e, Rect row, Color old)
	{
		bool flag = e.type == EventType.Repaint && row.Contains(e.mousePosition);
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			GUI.color = new Color(1f, 1f, 1f, 0.05f);
			GUI.DrawTexture(row, BtcGameplayUI.White());
			GUI.color = old;
		}
	}

	private static Texture2D White()
	{
		bool flag = BtcGameplayUI._white == null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			BtcGameplayUI._white = new Texture2D(1, 1, TextureFormat.RGBA32, false);
			BtcGameplayUI._white.SetPixel(0, 0, Color.white);
			BtcGameplayUI._white.Apply();
			BtcGameplayUI._white.hideFlags = HideFlags.HideAndDontSave;
		}
		return BtcGameplayUI._white;
	}

	private static float SliderCore(Event e, Rect hit, int id, float value, float min, float max, float step, Color old, bool interact)
	{
		if (interact)
		{
			bool flag = e.type == EventType.MouseDown && e.button == 0 && hit.Contains(e.mousePosition);
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				BtcGameplayUI._dragId = id;
				GUI.FocusControl(string.Empty);
			}
			bool flag9 = BtcGameplayUI._dragId == id && e.button == 0 && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag);
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				float num = Mathf.Clamp01((e.mousePosition.x - hit.x) / hit.width);
				e.Use();
				float num2 = min + num * (max - min);
				bool flag17 = step > 0f;
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				if (flag24)
				{
					num2 = min + Mathf.Round((num2 - min) / step) * step;
				}
				value = Mathf.Clamp(num2, min, max);
			}
		}
		bool flag25 = e.type == EventType.Repaint;
		bool flag26 = flag25;
		bool flag27 = flag26;
		bool flag28 = flag27;
		bool flag29 = flag28;
		bool flag30 = flag29;
		bool flag31 = flag30;
		bool flag32 = flag31;
		if (flag32)
		{
			float num3 = Mathf.Clamp01((value - min) / (max - min));
			Rect position = new Rect(hit.x, hit.y + hit.height * 0.5f - 2f, hit.width, 4f);
			GUI.color = new Color(1f, 1f, 1f, 0.22f);
			GUI.DrawTexture(position, BtcGameplayUI.White());
			GUI.color = (interact ? BtcGameplayUI.Accent : new Color(BtcGameplayUI.Accent.r, BtcGameplayUI.Accent.g, BtcGameplayUI.Accent.b, 0.4f));
			GUI.DrawTexture(new Rect(position.x, position.y, position.width * num3, position.height), BtcGameplayUI.White());
			GUI.color = (interact ? Color.white : BtcGameplayUI.Dim);
			float num4 = hit.height * 0.5f;
			GUI.DrawTexture(new Rect(position.x + position.width * num3 - num4 * 0.25f, hit.y + hit.height * 0.5f - num4 * 0.5f, num4 * 0.5f, num4), BtcGameplayUI.White());
			GUI.color = old;
		}
		return value;
	}

	private static void EnsureStyles(int fs)
	{
		bool flag = BtcGameplayUI._label != null && BtcGameplayUI._styleFs == fs;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			BtcGameplayUI._styleFs = fs;
			BtcGameplayUI._label = BtcGameplayUI.Make(GUI.skin.label, fs, TextAnchor.MiddleLeft, FontStyle.Normal, Color.white);
			BtcGameplayUI._note = BtcGameplayUI.Make(GUI.skin.label, Mathf.Max(10, (int)((float)fs * 0.8f)), TextAnchor.UpperLeft, FontStyle.Normal, new Color(1f, 1f, 1f, 0.55f));
			BtcGameplayUI._note.wordWrap = true;
			BtcGameplayUI._sub = BtcGameplayUI.Make(GUI.skin.label, fs, TextAnchor.MiddleLeft, FontStyle.Bold, BtcGameplayUI.Accent);
			BtcGameplayUI._sub.padding = new RectOffset(fs, 0, 0, 0);
			BtcGameplayUI._btn = BtcGameplayUI.Make(GUI.skin.label, Mathf.Max(10, (int)((float)fs * 0.9f)), TextAnchor.MiddleCenter, FontStyle.Normal, Color.white);
			BtcGameplayUI._small = BtcGameplayUI.Make(GUI.skin.label, Mathf.Max(10, (int)((float)fs * 0.75f)), TextAnchor.MiddleCenter, FontStyle.Normal, Color.white);
			BtcGameplayUI._title = BtcGameplayUI.Make(GUI.skin.label, (int)((float)fs * 1.2f), TextAnchor.MiddleCenter, FontStyle.Bold, new Color(1f, 0.85f, 0.4f, 1f));
			BtcGameplayUI._field = BtcGameplayUI.Make(GUI.skin.textField, Mathf.Max(10, (int)((float)fs * 0.85f)), TextAnchor.MiddleCenter, FontStyle.Normal, Color.white);
		}
	}

	private static GUIStyle Make(GUIStyle basis, int size, TextAnchor anchor, FontStyle fontStyle, Color color)
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
			}
		};
	}

	static BtcGameplayUI()
	{
		BtcGameplayUI._focusName = string.Empty;
		BtcGameplayUI.Buf = new Dictionary<int, string>();
		BtcGameplayUI.Open = new HashSet<string>
		{
			"dmg",
			"eco"
		};
		BtcGameplayUI._presetName = "My preset";
		BtcGameplayUI.Accent = new Color(0.55f, 0.85f, 0.55f, 1f);
		BtcGameplayUI.Warn = new Color(1f, 0.45f, 0.45f, 1f);
		BtcGameplayUI.Changed = new Color(1f, 0.85f, 0.4f, 1f);
		BtcGameplayUI.Dim = new Color(1f, 1f, 1f, 0.35f);
	}

	private static void DrawWarn(Event e, ref float y, float w, float rowH, int fs, Color old, int idx, BtcGameplay.Def d, bool interact)
	{
		bool flag = BtcGameplayUI._warnIdx != idx;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		if (flag5)
		{
			string text = BtcGameplayUI.NumStr(d, BtcGameplayUI._warnVal);
			string text2 = BtcGameplayUI.NumStr(d, d.Min) + " .. " + BtcGameplayUI.NumStr(d, d.Max);
			BtcGameplayUI.NoteRaw(ref y, w, fs, BtcGameplayUI.T(string.Concat(new string[]
			{
				"Warning: ",
				text,
				" is outside the normal range (",
				text2,
				"). Values like this can break the balance or the game. Apply anyway?"
			}), string.Concat(new string[]
			{
				"Внимание: ",
				text,
				" вне обычного диапазона (",
				text2,
				"). Такие значения могут сломать баланс или игру. Всё равно применить?"
			})));
			Rect row = new Rect(0f, y, w, rowH);
			y += rowH;
			BtcGameplayUI.RowBackground(e, row, old);
			Rect r = new Rect(w * 0.03f, row.y + rowH * 0.12f, w * 0.26f, rowH * 0.76f);
			Rect r2 = new Rect(w * 0.31f, row.y + rowH * 0.12f, w * 0.18f, rowH * 0.76f);
			Rect r3 = new Rect(w * 0.51f, row.y + rowH * 0.12f, w * 0.46f, rowH * 0.76f);
			bool flag6 = BtcGameplayUI.Button(e, r, BtcGameplayUI.T("Apply", "Применить"), new Color(1f, 0.65f, 0.2f, 1f), old);
			bool flag7 = BtcGameplayUI.Button(e, r2, BtcGameplayUI.T("Cancel", "Отмена"), Color.white, old);
			bool flag8 = BtcGameplayUI.Button(e, r3, (BtcGameplayUI._warnNever ? "[x] " : "[ ] ") + BtcGameplayUI.T("don't show again", "больше не показывать"), Color.white, old);
			bool flag9 = flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			if (flag13)
			{
				BtcGameplayUI._warnNever = !BtcGameplayUI._warnNever;
			}
			bool flag14 = flag6 && interact;
			bool flag15 = flag14;
			bool flag16 = flag15;
			bool flag17 = flag16;
			bool flag18 = flag17;
			if (flag18)
			{
				bool warnNever = BtcGameplayUI._warnNever;
				bool flag19 = warnNever;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				if (flag22)
				{
					PlayerPrefs.SetInt("btc.gs.nowarn", 1);
					PlayerPrefs.Save();
				}
				BtcGameplay.Set(idx, BtcGameplayUI._warnVal);
				BtcGameplayUI._warnIdx = -1;
			}
			else
			{
				bool flag23 = flag7;
				bool flag24 = flag23;
				bool flag25 = flag24;
				bool flag26 = flag25;
				bool flag27 = flag26;
				if (flag27)
				{
					BtcGameplayUI._warnIdx = -1;
				}
			}
		}
	}

	private static void DrawRequestBar(Event e, ref float y, float w, float rowH, int fs, Color old)
	{
		bool flag = !BtcGameplay.IsHost && BtcGameplay.InSession;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			float num = (w - (float)fs * 2f) * 0.5f;
			Rect r = new Rect((float)fs, y + rowH * 0.1f, num - 4f, rowH * 0.8f);
			Rect r2 = new Rect((float)fs + num + 4f, y + rowH * 0.1f, num - 4f, rowH * 0.8f);
			bool flag9 = BtcGameplayUI.Button(e, r, BtcGameplayUI._draft ? BtcGameplayUI.T("Back to host rules", "Назад к правилам хоста") : BtcGameplayUI.T("Make a request", "Составить запрос"), Color.white, old, true);
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				BtcGameplayUI._draft = !BtcGameplayUI._draft;
				bool draft = BtcGameplayUI._draft;
				bool flag17 = draft;
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				if (flag23)
				{
					BtcGameplay.Proposal = (float[])BtcGameplay.Active.Clone();
				}
			}
			bool draft2 = BtcGameplayUI._draft;
			bool flag24 = draft2;
			bool flag25 = flag24;
			bool flag26 = flag25;
			bool flag27 = flag26;
			bool flag28 = flag27;
			bool flag29 = flag28;
			bool flag30 = flag29;
			if (flag30)
			{
				bool flag31 = BtcGameplay.Encode(BtcGameplay.Proposal) != BtcGameplay.Encode(BtcGameplay.Active);
				bool flag32 = BtcGameplayUI.Button(e, r2, BtcGameplayUI.T("Send request to host", "Отправить запрос хосту"), flag31 ? BtcGameplayUI.Accent : BtcGameplayUI.Dim, old, true) && flag31;
				bool flag33 = flag32;
				bool flag34 = flag33;
				bool flag35 = flag34;
				bool flag36 = flag35;
				bool flag37 = flag36;
				bool flag38 = flag37;
				bool flag39 = flag38;
				if (flag39)
				{
					BtcGameplay.SendRequest(BtcGameplay.Proposal);
				}
			}
			y += rowH;
		}
	}

	private static void DrawGeneral(Event e, ref float y, float w, float rowH, int fs, Color old, float[] shown, bool interact)
	{
		bool flag = !BtcGameplayUI._genInit;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		if (flag7)
		{
			BtcGameplayUI._genInit = true;
			BtcGameplayUI.Open.Add("gen");
		}
		BtcGameplayUI.Note(ref y, w, fs, BtcGameplayUI.T("Players, colours, teams. The host sets the slots and the new colours here. Everybody picks colour, team A-H or Spectator in the lobby (vanilla panel and the small BTC panel). A spectator spawns without a base and crystals and just watches.", "Игроки, цвета, команды. Хост задаёт слоты и новые цвета здесь. Цвет, команду A-H или наблюдателя каждый выбирает в лобби (ванильная панель и маленькая панель BTC). Наблюдатель появляется без базы и кристаллов и просто смотрит."));
		string[] array = new string[]
		{
			"gen",
			"gcol",
			"path",
			"mov",
		};
		string[] array2 = new string[]
		{
			"Players",
			"New colours (slots 5-8)",
			"Pathfinding (search budgets)",
			"Movement",
		};
		string[] array3 = new string[]
		{
			"Игроки",
			"Новые цвета (слоты 5-8)",
			"Поиск путей (бюджеты поиска)",
			"Движение",
		};
		for (int i = 0; i < array.Length; i++)
		{
			int num = 0;
			for (int j = 0; j < BtcGameplay.Defs.Count; j++)
			{
				bool flag8 = BtcGameplay.Defs[j].Group == array[i] && BtcGameplay.IsDev(j, shown[j]);
				bool flag9 = flag8;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				if (flag14)
				{
					num++;
				}
			}
			bool flag15 = BtcGameplayUI.Open.Contains(array[i]);
			bool flag16 = BtcGameplayUI.GroupHeader(e, ref y, w, rowH, fs, old, BtcGameplayUI.T(array2[i], array3[i]), flag15, num);
			bool flag17 = flag16;
			bool flag18 = flag17;
			bool flag19 = flag18;
			bool flag20 = flag19;
			bool flag21 = flag20;
			bool flag22 = flag21;
			if (flag22)
			{
				bool flag23 = flag15;
				bool flag24 = flag23;
				bool flag25 = flag24;
				bool flag26 = flag25;
				bool flag27 = flag26;
				bool flag28 = flag27;
				bool flag29 = flag28;
				if (flag29)
				{
					BtcGameplayUI.Open.Remove(array[i]);
				}
				else
				{
					BtcGameplayUI.Open.Add(array[i]);
				}
			}
			bool flag30 = flag15;
			bool flag31 = flag30;
			bool flag32 = flag31;
			bool flag33 = flag32;
			bool flag34 = flag33;
			bool flag35 = flag34;
			bool flag36 = flag35;
			if (flag36)
			{
				bool flag37 = array[i] == "gcol";
				bool flag38 = flag37;
				bool flag39 = flag38;
				bool flag40 = flag39;
				bool flag41 = flag40;
				bool flag42 = flag41;
				bool flag43 = flag42;
				if (flag43)
				{
					BtcGameplayUI.DrawColorStrip(e, ref y, w, rowH, fs, old, shown, interact);
				}
				for (int k = 0; k < BtcGameplay.Defs.Count; k++)
				{
					BtcGameplay.Def def = BtcGameplay.Defs[k];
					bool flag44 = def.Group == array[i] && !def.Hidden && (def.Adv == 0 || BtcQol.AdvShown(shown)) && BtcSimplePhys.Vis(def, shown);
					bool flag45 = flag44;
					bool flag46 = flag45;
					bool flag47 = flag46;
					bool flag48 = flag47;
					bool flag49 = flag48;
					bool flag50 = flag49;
					if (flag50)
					{
						BtcGameplayUI.ValueRow(e, ref y, w, rowH, fs, old, k, shown, interact);
					}
				}
			}
		}
		BtcGameplayUI.Note(ref y, w, fs, BtcGameplayUI.T("A colour is a real start only if the map has that many 'Player Starting Point' markers. All 8 colours and Spectator are always listed.", "Цвет - настоящий старт, только если на карте столько же маркеров 'Player Starting Point'. Все 8 цветов и наблюдатель в списке всегда."));
		BtcGameplayUI.DrawRequestBar(e, ref y, w, rowH, fs, old);
	}

	private static void DrawColorStrip(Event e, ref float y, float w, float rowH, int fs, Color old, float[] shown, bool interact)
	{
		Rect rect = new Rect(0f, y, w, rowH * 1.1f);
		y += rowH * 1.1f;
		float num = Mathf.Min(rowH * 0.8f, (w - (float)fs * 2f) / 14f);
		float num2 = (float)fs;
		bool flag = e.type == EventType.Repaint;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			for (int i = 0; i < 8; i++)
			{
				Color color = (i < 4) ? BtcLobby.VanillaPal[i] : BtcLobby.ExtraColorFrom(shown, i - 4);
				Rect position = new Rect(num2 + (float)i * (num + 4f), rect.y + (rect.height - num) * 0.5f, num, num);
				GUI.color = new Color(1f, 1f, 1f, 0.7f);
				GUI.DrawTexture(position, BtcGameplayUI.White());
				GUI.color = color;
				GUI.DrawTexture(new Rect(position.x + 2f, position.y + 2f, position.width - 4f, position.height - 4f), BtcGameplayUI.White());
			}
			GUI.color = old;
		}
		float num3 = num2 + 8f * (num + 4f) + (float)fs;
		Rect r = new Rect(num3, rect.y + rect.height * 0.1f, w - num3 - (float)fs, rect.height * 0.8f);
		bool flag5 = BtcGameplayUI.Button(e, r, BtcGameplayUI.T("Reset new colours", "Сбросить новые цвета"), interact ? Color.white : BtcGameplayUI.Dim, old, true) && interact;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		bool flag9 = flag8;
		if (flag9)
		{
			for (int j = 0; j < 12; j++)
			{
				int num4 = BtcGameplay.Idx(string.Concat(new object[]
				{
					"gcol.",
					j / 3,
					".",
					j % 3
				}));
				bool flag10 = num4 >= 0;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				if (flag13)
				{
					BtcGameplay.Set(num4, BtcGameplay.Defs[num4].Val);
				}
			}
		}
	}

	private static void DrawBalance(Event e, ref float y, float w, float rowH, int fs, Color old, float[] shown, bool interact, bool isHost, bool inSession)
	{
		string[] array = new string[]
		{
			"dmg",
			"hp",
			"spd",
			"eco",
			"vis",
			"rng",
			"ore"
		};
		string[] array2 = new string[]
		{
			"Damage",
			"HP & stamina",
			"Speed",
			"Economy (energy, bricktrons)",
			"Fog of war: vision",
			"Shooting range",
			"Deep layer: stone and ore (new maps)"
		};
		string[] array3 = new string[]
		{
			"Урон",
			"ХП и стамина",
			"Скорость",
			"Экономика (энергия, бриктроны)",
			"Туман войны: обзор",
			"Дальность стрельбы",
			"Глубокий слой: камень и руда (новые карты)"
		};
		bool flag = BtcGameplay.Edit != null && shown[BtcGameplay.Idx("dmg.adv")] > 0.5f;
		for (int i = 0; i < array.Length; i++)
		{
			int num = 0;
			for (int j = 0; j < BtcGameplay.Defs.Count; j++)
			{
				BtcGameplay.Def def = BtcGameplay.Defs[j];
				bool flag2 = def.Group == array[i] && !def.Hidden && (Mathf.Abs(shown[j] - def.Val) > 0.0001f || (def.Abs && def.ModeIdx >= 0 && shown[def.ModeIdx] > 0.5f));
				bool flag3 = flag2;
				if (flag3)
				{
					num++;
				}
			}
			bool flag4 = BtcGameplayUI.Open.Contains(array[i]);
			bool flag5 = BtcGameplayUI.GroupHeader(e, ref y, w, rowH, fs, old, BtcGameplayUI.T(array2[i], array3[i]), flag4, num);
			bool flag6 = flag5;
			if (flag6)
			{
				bool flag7 = flag4;
				bool flag8 = flag7;
				if (flag8)
				{
					BtcGameplayUI.Open.Remove(array[i]);
				}
				else
				{
					BtcGameplayUI.Open.Add(array[i]);
				}
			}
			bool flag9 = flag4;
			bool flag10 = flag9;
			if (flag10)
			{
				for (int k = 0; k < BtcGameplay.Defs.Count; k++)
				{
					BtcGameplay.Def def2 = BtcGameplay.Defs[k];
					bool flag11 = def2.Group == array[i] && !def2.Hidden && (def2.Adv == 0 || (def2.Adv == 1 && !flag) || (def2.Adv == 2 && flag));
					bool flag12 = flag11;
					if (flag12)
					{
						BtcGameplayUI.ValueRow(e, ref y, w, rowH, fs, old, k, shown, interact);
					}
				}
			}
		}
		BtcRecipes.Draw(e, ref y, w, rowH, fs, old, interact, isHost, inSession);
		BtcGameplayUI.Note(ref y, w, fs, BtcGameplayUI.T("Planned: bomb count, respawn times, build/craft/mine speed.", "В планах: число бомб, время респавна, скорость стройки/крафта/добычи."));
		BtcGameplayUI.DrawRequestBar(e, ref y, w, rowH, fs, old);
	}

	public static bool TabActive;

	public static float ContentH = 400f;

	private const int SubBalance = 0;

	private const int SubGeneral = 1;

	private const int SubHandicap = 2;

	private const int SubPresets = 3;

	private static int _subTab;

	private static bool _draft;

	private static int _dragId;

	private static string _focusName;

	private static readonly Dictionary<int, string> Buf;

	private static readonly HashSet<string> Open;

	private static string _presetName;

	private static BtcGameplay.PendingReq _expanded;

	private static bool _warnList;

	private static Vector2 _warnScroll;

	private static readonly Color Accent;

	private static readonly Color Warn;

	private static readonly Color Changed;

	private static readonly Color Dim;

	private static GUIStyle _label;

	private static GUIStyle _note;

	private static GUIStyle _sub;

	private static GUIStyle _small;

	private static GUIStyle _btn;

	private static GUIStyle _field;

	private static GUIStyle _title;

	private static int _styleFs;

	private static Texture2D _white;

	private static int _editing = -1;

	private static int _warnIdx = -1;

	private static float _warnVal;

	private static bool _warnNever;

	private static bool _genInit;
}
}
