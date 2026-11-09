using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace BetterThanCastle
{
public static class BtcReplayUI
{
	public static string T(string en, string ru)
	{
		return BtcLang.Russian ? ru : en;
	}

	public static void OnMenuOpen()
	{
		BtcReplayUI.listLoaded = false;
		BtcReplayUI.confirmPath = null;
	}

	private static void Refresh()
	{
		BtcReplayUI.list = BtcReplay.ListReplays();
		BtcReplayUI.listLoaded = true;
	}

	internal static void Ensure(int fs)
	{
		bool flag = BtcReplayUI.white == null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			BtcReplayUI.white = new Texture2D(1, 1, TextureFormat.RGBA32, false);
			BtcReplayUI.white.SetPixel(0, 0, Color.white);
			BtcReplayUI.white.Apply();
			BtcReplayUI.white.hideFlags = HideFlags.HideAndDontSave;
		}
		bool flag9 = BtcReplayUI.sRow != null && BtcReplayUI.styleFs == fs;
		bool flag10 = !flag9;
		bool flag11 = flag10;
		bool flag12 = flag11;
		bool flag13 = flag12;
		bool flag14 = flag13;
		bool flag15 = flag14;
		bool flag16 = flag15;
		if (flag16)
		{
			BtcReplayUI.styleFs = fs;
			BtcReplayUI.sRow = new GUIStyle(GUI.skin.button);
			BtcReplayUI.sRow.alignment = TextAnchor.MiddleLeft;
			BtcReplayUI.sRow.fontSize = fs;
			BtcReplayUI.sRow.padding = new RectOffset(Mathf.RoundToInt((float)fs * 2.8f), fs, 0, 0);
			BtcReplayUI.sRow.border = new RectOffset(0, 0, 0, 0);
			BtcReplayUI.sRow.margin = new RectOffset(0, 0, 0, 0);
			BtcReplayUI.SetColors(BtcReplayUI.sRow, new Color(1f, 1f, 1f, 0.85f), Color.white);
			BtcReplayUI.sLabel = new GUIStyle(GUI.skin.label);
			BtcReplayUI.sLabel.alignment = TextAnchor.MiddleLeft;
			BtcReplayUI.sLabel.fontSize = fs;
			BtcReplayUI.sLabel.padding = new RectOffset(0, 0, 0, 0);
			BtcReplayUI.sLabel.clipping = TextClipping.Clip;
			BtcReplayUI.sLabel.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
			BtcReplayUI.sNote = new GUIStyle(BtcReplayUI.sLabel);
			BtcReplayUI.sNote.fontSize = Mathf.Max(10, Mathf.RoundToInt((float)fs * 0.85f));
			BtcReplayUI.sNote.wordWrap = true;
			BtcReplayUI.sNote.alignment = TextAnchor.UpperLeft;
			BtcReplayUI.sNote.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
			BtcReplayUI.sHead = new GUIStyle(BtcReplayUI.sLabel);
			BtcReplayUI.sHead.fontStyle = FontStyle.Bold;
			BtcReplayUI.sHead.normal.textColor = new Color(0.55f, 0.85f, 0.55f, 0.95f);
			BtcReplayUI.sBtn = BtcUiStyle.Button(fs);
			BtcReplayUI.sVal = new GUIStyle(BtcReplayUI.sLabel);
			BtcReplayUI.sVal.alignment = TextAnchor.MiddleCenter;
			BtcReplayUI.MakeBarStyles(fs);
		}
	}

	private static void SetColors(GUIStyle st, Color normal, Color hover)
	{
		st.normal.textColor = normal;
		st.hover.textColor = hover;
		st.active.textColor = hover;
		st.focused.textColor = hover;
		st.normal.background = null;
		st.hover.background = null;
		st.active.background = null;
		st.focused.background = null;
	}

	internal static void Fill(Rect r, Color c)
	{
		bool flag = Event.current.type != EventType.Repaint;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			Color color = GUI.color;
			GUI.color = c;
			GUI.DrawTexture(r, BtcReplayUI.white);
			GUI.color = color;
		}
	}

	private static float Note(float y, float w, int fs, string text)
	{
		float num = BtcReplayUI.sNote.CalcHeight(new GUIContent(text), w - (float)fs * 2f);
		GUI.Label(new Rect((float)fs, y + 2f, w - (float)fs * 2f, num), text, BtcReplayUI.sNote);
		return y + num + 6f;
	}

	private static bool CheckRow(Event e, ref float y, float w, float rowH, int fs, string label, bool on)
	{
		Rect rect = new Rect(0f, y, w, rowH);
		y += rowH;
		BtcReplayUI.Fill(rect, on ? new Color(0.55f, 0.85f, 0.55f, 0.08f) : new Color(1f, 1f, 1f, 0.03f));
		float num = (float)fs * 1.1f;
		Rect r = new Rect((float)fs, rect.y + (rowH - num) * 0.5f, num, num);
		BtcReplayUI.Fill(r, new Color(1f, 1f, 1f, 0.5f));
		BtcReplayUI.Fill(new Rect(r.x + 1f, r.y + 1f, r.width - 2f, r.height - 2f), new Color(0.08f, 0.08f, 0.1f, 1f));
		if (on)
		{
			BtcReplayUI.Fill(new Rect(r.x + 3f, r.y + 3f, r.width - 6f, r.height - 6f), BtcReplayUI.Green);
		}
		return GUI.Button(rect, label, BtcReplayUI.sRow);
	}

	internal static bool Button(Rect r, string text, Color c)
	{
		BtcReplayUI.Fill(r, new Color(1f, 1f, 1f, 0.07f));
		Color contentColor = GUI.contentColor;
		GUI.contentColor = c;
		bool result = GUI.Button(r, text, BtcReplayUI.sBtn);
		GUI.contentColor = contentColor;
		return result;
	}

	/// <summary>Timeline readout: advanced timer time when the replay has one, else the replay clock.</summary>
	internal static string TClock(float sec)
	{
		return BtcTimer.ClockAt(sec) ?? BtcReplayUI.Clock(sec);
	}

	public static string Clock(float sec)
	{
		int num = Mathf.Max(0, Mathf.RoundToInt(sec));
		bool flag = num >= 3600;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		string result;
		if (flag8)
		{
			result = string.Concat(new string[]
			{
				(num / 3600).ToString(),
				":",
				(num / 60 % 60).ToString("00"),
				":",
				(num % 60).ToString("00")
			});
		}
		else
		{
			result = (num / 60).ToString() + ":" + (num % 60).ToString("00");
		}
		return result;
	}

	public static bool Draw(Event e, float w, float rowH, int fs, Color old)
	{
		BtcReplayUI.Ensure(fs);
		BtcReplayShare.Poll();
		bool flag = !BtcReplayUI.listLoaded;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			BtcReplayUI.Refresh();
		}
		float num = 0f;
		num = BtcReplayUI.Note(num, w, fs, BtcReplayUI.T("Records the full network stream the host sends to clients (everything a spectator sees: spawns, movement, animations) into a compressed file. During the game recording only copies packets into a buffer, compression and disk writes run on a background thread. Replays are stored in BetterThanCastle/Replays.", "Записывает полный сетевой поток, который хост отправляет клиентам (всё, что видит зритель: спавн, движение, анимации), в сжатый файл. Во время игры запись лишь копирует пакеты в буфер, сжатие и запись на диск идут в фоновом потоке. Реплеи лежат в BetterThanCastle/Replays."));
		bool flag9 = BtcReplayUI.CheckRow(e, ref num, w, rowH, fs, BtcReplayUI.T("Record replays automatically at match start (when you host)", "Автозапись реплея при старте матча (когда вы хост)"), BtcReplay.GetRecordEnabled());
		bool flag10 = flag9;
		bool flag11 = flag10;
		bool flag12 = flag11;
		bool flag13 = flag12;
		bool flag14 = flag13;
		bool flag15 = flag14;
		bool flag16 = flag15;
		if (flag16)
		{
			BtcReplay.SetRecordEnabled(!BtcReplay.GetRecordEnabled());
		}
		bool flag17 = BtcReplayUI.CheckRow(e, ref num, w, rowH, fs, BtcReplayUI.T("Also record singleplayer games", "Записывать и одиночные игры"), BtcReplay.GetRecordSingleplayer());
		bool flag18 = flag17;
		bool flag19 = flag18;
		bool flag20 = flag19;
		bool flag21 = flag20;
		bool flag22 = flag21;
		bool flag23 = flag22;
		bool flag24 = flag23;
		if (flag24)
		{
			BtcReplay.SetRecordSingleplayer(!BtcReplay.GetRecordSingleplayer());
		}
		bool flag25 = BtcReplayUI.CheckRow(e, ref num, w, rowH, fs, BtcReplayUI.T("Show REC indicator with timer and size", "Показывать индикатор REC с таймером и размером"), BtcReplay.GetShowIndicator());
		bool flag26 = flag25;
		bool flag27 = flag26;
		bool flag28 = flag27;
		bool flag29 = flag28;
		bool flag30 = flag29;
		bool flag31 = flag30;
		bool flag32 = flag31;
		if (flag32)
		{
			BtcReplay.SetShowIndicator(!BtcReplay.GetShowIndicator());
		}
		if (BtcReplayUI.CheckRow(e, ref num, w, rowH, fs, BtcReplayUI.T("Auto-skip pauses and host freezes when playing a replay", "Автоматически перематывать паузы и фризы хоста в реплее"), BtcReplayKeys.SkipPauses))
		{
			BtcReplayKeys.SkipPauses = !BtcReplayKeys.SkipPauses;
		}
		if (BtcReplayUI.CheckRow(e, ref num, w, rowH, fs, BtcReplayUI.T("Download the replay from the host after a match (when you are a client)", "Загружать реплей у хоста после матча (когда вы клиент)"), BtcConfig.ReplayDownloadFromHost.Value))
		{
			BtcConfig.ReplayDownloadFromHost.Value = !BtcConfig.ReplayDownloadFromHost.Value;
		}
		if (BtcReplayUI.CheckRow(e, ref num, w, rowH, fs, BtcReplayUI.T("Send the replay to all clients after a match (when you host)", "Отправлять реплей всем клиентам после матча (когда вы хост)"), BtcConfig.ReplayAutoShare.Value))
		{
			BtcConfig.ReplayAutoShare.Value = !BtcConfig.ReplayAutoShare.Value;
		}
		bool flag49 = BtcReplayUI.CheckRow(e, ref num, w, rowH, fs, BtcReplayBoost.UiFastNature(), BtcReplayBoost.GetFastNature());
		bool flag50 = flag49;
		bool flag51 = flag50;
		if (flag51)
		{
			BtcReplayBoost.SetFastNature(!BtcReplayBoost.GetFastNature());
		}
		bool flag52 = BtcReplayUI.CheckRow(e, ref num, w, rowH, fs, BtcReplayBoost.UiFastWaits(), BtcReplayBoost.GetFastWaits());
		bool flag53 = flag52;
		bool flag54 = flag53;
		if (flag54)
		{
			BtcReplayBoost.SetFastWaits(!BtcReplayBoost.GetFastWaits());
		}
		int num4 = BtcReplayUI.StepRow(num, w, rowH, fs, BtcReplayBoost.UiBudget(), BtcReplayBoost.GetBudgetMs().ToString() + " ms");
		num += rowH;
		bool flag55 = num4 != 0;
		bool flag56 = flag55;
		bool flag57 = flag56;
		if (flag57)
		{
			BtcReplayBoost.StepBudget(num4);
		}
		num = BtcReplayUI.Note(num, w, fs, BtcReplayBoost.UiNote());
		Color red = new Color(1f, 1f, 1f, 0.7f);
		bool flag58 = BtcReplay.Mode == BtcReplay.State.Recording;
		bool flag59 = flag58;
		bool flag60 = flag59;
		bool flag61 = flag60;
		bool flag62 = flag61;
		bool flag63 = flag62;
		bool flag64 = flag63;
		bool flag65 = flag64;
		string text;
		if (flag65)
		{
			text = string.Concat(new object[]
			{
				"REC  ",
				BtcReplayUI.Clock(BtcReplay.RecSeconds),
				"  -  ",
				BtcReplayUI.Size(BtcReplay.RecBytes),
				"  -  ",
				BtcReplay.RecKeyframes,
				" ",
				BtcReplayUI.T("keyframes", "ключ. кадров"),
				"  -  ",
				BtcReplay.RecFile
			});
			red = BtcReplayUI.Red;
		}
		else
		{
			text = BtcReplayUI.T("Not recording.", "Запись не идёт.");
		}
		Color contentColor = GUI.contentColor;
		GUI.contentColor = red;
		GUI.Label(new Rect((float)fs, num, w - (float)fs * 2f, rowH), text, BtcReplayUI.sLabel);
		GUI.contentColor = contentColor;
		num += rowH;
		bool flag66 = !string.IsNullOrEmpty(BtcReplay.Message) && Time.unscaledTime < BtcReplay.MessageUntil;
		bool flag67 = flag66;
		bool flag68 = flag67;
		bool flag69 = flag68;
		bool flag70 = flag69;
		bool flag71 = flag70;
		bool flag72 = flag71;
		bool flag73 = flag72;
		if (flag73)
		{
			num = BtcReplayUI.Note(num, w, fs, BtcReplay.Message);
		}
		num += rowH * 0.3f;
		GUI.Label(new Rect((float)fs, num, w * 0.5f, rowH), string.Concat(new object[]
		{
			BtcReplayUI.T("Saved replays", "Сохранённые реплеи"),
			" (",
			BtcReplayUI.list.Count,
			")"
		}), BtcReplayUI.sHead);
		float num5 = (float)fs * 8f;
		bool flag74 = BtcReplayUI.Button(new Rect(w - num5 * 2f - (float)fs * 1.5f, num + rowH * 0.1f, num5, rowH * 0.8f), BtcReplayUI.T("Refresh", "Обновить"), Color.white);
		bool flag75 = flag74;
		bool flag76 = flag75;
		bool flag77 = flag76;
		bool flag78 = flag77;
		bool flag79 = flag78;
		bool flag80 = flag79;
		bool flag81 = flag80;
		if (flag81)
		{
			BtcReplayUI.Refresh();
		}
		bool flag82 = BtcReplayUI.Button(new Rect(w - num5 - (float)fs, num + rowH * 0.1f, num5, rowH * 0.8f), BtcReplayUI.T("Open folder", "Открыть папку"), Color.white);
		bool flag83 = flag82;
		bool flag84 = flag83;
		bool flag85 = flag84;
		bool flag86 = flag85;
		bool flag87 = flag86;
		bool flag88 = flag87;
		bool flag89 = flag88;
		if (flag89)
		{
			BtcReplayUI.OpenFolder();
		}
		num += rowH;
		num = BtcReplayUI.Note(num, w, fs, BtcReplayUI.T("Press Play to watch a replay as a spectator (free camera, whole map). Start it from the main menu, outside of a game. Replays recorded by older versions have no world snapshot and cannot be played.", "Нажмите «Играть», чтобы смотреть реплей как зритель (свободная камера, вся карта). Запускайте из главного меню, вне игры. Реплеи, записанные старыми версиями, без снимка мира и не проигрываются."));
		bool flag90 = BtcReplay.Mode == BtcReplay.State.Playing;
		bool flag91 = flag90;
		bool flag92 = flag91;
		bool flag93 = flag92;
		bool flag94 = flag93;
		bool flag95 = flag94;
		bool flag96 = flag95;
		bool flag97 = flag96;
		if (flag97)
		{
			num = BtcReplayUI.Note(num, w, fs, string.IsNullOrEmpty(BtcReplay.PlayStatus) ? BtcReplayUI.T("Replay is running.", "Реплей идёт.") : BtcReplay.PlayStatus);
		}
		bool flag98 = BtcReplayUI.list.Count == 0;
		bool flag99 = flag98;
		bool flag100 = flag99;
		bool flag101 = flag100;
		bool flag102 = flag101;
		bool flag103 = flag102;
		bool flag104 = flag103;
		bool flag105 = flag104;
		if (flag105)
		{
			num = BtcReplayUI.Note(num, w, fs, BtcReplayUI.T("No replays yet. Start a match and the replay appears here when it ends.", "Пока нет реплеев. Начните матч, реплей появится здесь, когда он закончится."));
		}
		float num6 = (float)fs * 6f;
		for (int i = 0; i < BtcReplayUI.list.Count; i++)
		{
			BtcReplay.Data data = BtcReplayUI.list[i];
			Rect r = new Rect(0f, num, w, rowH);
			num += rowH;
			BtcReplayUI.Fill(r, (i % 2 == 0) ? new Color(1f, 1f, 1f, 0.03f) : new Color(1f, 1f, 1f, 0f));
			bool flag106 = data.Error != null;
			bool flag107 = flag106;
			bool flag108 = flag107;
			bool flag109 = flag108;
			bool flag110 = flag109;
			bool flag111 = flag110;
			bool flag112 = flag111;
			bool flag113 = flag112;
			string text2;
			if (flag113)
			{
				text2 = data.FileName + "   " + data.Error;
			}
			else
			{
				text2 = string.Concat(new object[]
				{
					(data.Ticks > 0L) ? new DateTime(data.Ticks).ToString("yyyy-MM-dd HH:mm") : data.FileName,
					"   ",
					string.IsNullOrEmpty(data.MapName) ? "?" : data.MapName,
					"   ",
					data.Multiplayer ? "MP" : "SP",
					"   ",
					data.Players.Count,
					" ",
					BtcReplayUI.T("pl.", "игр."),
					"   ",
					data.Complete ? BtcReplayUI.Clock(data.Duration) : BtcReplayUI.T("incomplete", "не завершён"),
					"   ",
					BtcReplayUI.Size(data.FileBytes)
				});
			}
			bool flag114 = data.Error == null && data.Complete && data.SnapBytes > 0;
			GUI.Label(new Rect((float)fs, r.y, w - num6 * 4.5f - (float)fs * 5.5f, rowH), text2, BtcReplayUI.sLabel);
			bool flag115 = false;
			bool flag116 = data.Error == null;
			bool flag117 = flag116;
			bool flag118 = flag117;
			bool flag119 = flag118;
			bool flag120 = flag119;
			bool flag121 = flag120;
			bool flag122 = flag121;
			if (flag122)
			{
				Rect r2 = new Rect(w - num6 * 2f - (float)fs * 2f, r.y + rowH * 0.1f, num6, rowH * 0.8f);
				flag115 = BtcReplayUI.Button(r2, flag114 ? BtcReplayUI.T("Play", "Играть") : BtcReplayUI.T("No snapshot", "Нет снимка"), flag114 ? BtcReplayUI.Green : new Color(1f, 1f, 1f, 0.35f));
			}
			bool flag123 = data.Error == null && data.Complete;
			int num7 = flag123 ? BtcReplayShare.State(data.FilePath) : 0;
			bool flag124 = flag123;
			bool flag125 = flag124;
			bool flag126 = flag125;
			if (flag126)
			{
				Rect r3 = new Rect(w - num6 * 3.25f - (float)fs * 3f, r.y + rowH * 0.1f, num6 * 1.25f, rowH * 0.8f);
				bool flag127 = num7 == 1;
				bool flag128 = flag127;
				bool flag129 = flag128;
				if (flag129)
				{
					BtcReplayUI.Button(r3, BtcReplayShare.UiUploading(), new Color(1f, 1f, 1f, 0.5f));
				}
				else
				{
					bool flag130 = num7 == 2;
					bool flag131 = flag130;
					bool flag132 = flag131;
					if (flag132)
					{
						BtcReplayUI.Button(r3, BtcReplayShare.UiCreate(), new Color(1f, 1f, 1f, 0.35f));
					}
					else
					{
						bool flag133 = BtcReplayUI.Button(r3, (num7 == 3) ? BtcReplayShare.UiRetry() : BtcReplayShare.UiCreate(), Color.white);
						bool flag134 = flag133;
						bool flag135 = flag134;
						if (flag135)
						{
							BtcReplayShare.Create(data.FilePath);
						}
					}
				}
			}
			bool flag136 = data.Error == null;
			bool flag137 = flag136;
			if (flag137)
			{
				Rect r4 = new Rect(w - num6 * 4.5f - (float)fs * 4f, r.y + rowH * 0.1f, num6 * 1.25f, rowH * 0.8f);
				bool flag138 = BtcReplayShare.FileCopied(data.FilePath);
				bool flag139 = BtcReplayUI.Button(r4, BtcReplayShare.UiCopyFile(flag138), flag138 ? BtcReplayUI.Green : Color.white);
				bool flag140 = flag139;
				bool flag141 = flag140;
				if (flag141)
				{
					BtcReplayShare.CopyFile(data.FilePath);
				}
			}
			bool flag142 = flag115;
			bool flag143 = flag142;
			bool flag144 = flag143;
			bool flag145 = flag144;
			bool flag146 = flag145;
			bool flag147 = flag146;
			bool flag148 = flag147;
			if (flag148)
			{
				bool flag149 = flag114;
				bool flag150 = flag149;
				bool flag151 = flag150;
				bool flag152 = flag151;
				bool flag153 = flag152;
				bool flag154 = flag153;
				bool flag155 = flag154;
				if (flag155)
				{
					BtcReplay.StartReplay(data);
				}
				else
				{
					BtcReplay.Say(data.Complete ? BtcReplayUI.T("This replay has no world snapshot (recorded by an older version).", "В этом реплее нет снимка мира (записан старой версией).") : BtcReplayUI.T("The replay is incomplete.", "Реплей не завершён."));
				}
			}
			Rect r5 = new Rect(w - num6 - (float)fs, r.y + rowH * 0.1f, num6, rowH * 0.8f);
			bool flag156 = BtcReplayUI.confirmPath == data.FilePath && Time.unscaledTime < BtcReplayUI.confirmUntil;
			bool flag157 = BtcReplayUI.Button(r5, flag156 ? BtcReplayUI.T("Sure?", "Точно?") : BtcReplayUI.T("Delete", "Удалить"), BtcReplayUI.Red);
			bool flag158 = flag157;
			bool flag159 = flag158;
			bool flag160 = flag159;
			bool flag161 = flag160;
			bool flag162 = flag161;
			bool flag163 = flag162;
			bool flag164 = flag163;
			if (flag164)
			{
				bool flag165 = flag156;
				bool flag166 = flag165;
				bool flag167 = flag166;
				bool flag168 = flag167;
				bool flag169 = flag168;
				bool flag170 = flag169;
				bool flag171 = flag170;
				bool flag172 = flag171;
				if (flag172)
				{
					bool flag173 = BtcReplay.Delete(data.FilePath);
					bool flag174 = flag173;
					bool flag175 = flag174;
					bool flag176 = flag175;
					bool flag177 = flag176;
					bool flag178 = flag177;
					bool flag179 = flag178;
					bool flag180 = flag179;
					if (flag180)
					{
						BtcReplayUI.confirmPath = null;
						BtcReplayUI.Refresh();
						BtcReplayUI.ContentH = num + rowH;
						return false;
					}
					BtcReplay.Say(BtcReplayUI.T("Cannot delete this file.", "Не удалось удалить файл."));
				}
				else
				{
					BtcReplayUI.confirmPath = data.FilePath;
					BtcReplayUI.confirmUntil = Time.unscaledTime + 3f;
				}
			}
			bool flag181 = num7 == 2 || num7 == 3;
			bool flag182 = flag181;
			bool flag183 = flag182;
			if (flag183)
			{
				float num8 = rowH * 0.9f;
				Rect r6 = new Rect(0f, num, w, num8);
				num += num8;
				BtcReplayUI.Fill(r6, (i % 2 == 0) ? new Color(1f, 1f, 1f, 0.03f) : new Color(1f, 1f, 1f, 0f));
				bool flag184 = num7 == 2;
				bool flag185 = flag184;
				bool flag186 = flag185;
				if (flag186)
				{
					string text3 = BtcReplayShare.Url(data.FilePath) + (BtcReplayShare.IsTemp(data.FilePath) ? BtcReplayShare.UiTempNote() : string.Empty);
					GUI.Label(new Rect((float)fs * 2f, r6.y, w - num6 * 3.25f - (float)fs * 5.5f, num8), text3, BtcReplayUI.sLabel);
					bool flag187 = BtcReplayShare.Copied(data.FilePath);
					bool flag188 = BtcReplayUI.Button(new Rect(w - num6 * 3.25f - (float)fs * 3f, r6.y + num8 * 0.1f, num6 * 1.25f, num8 * 0.8f), BtcReplayShare.UiCopy(flag187), flag187 ? BtcReplayUI.Green : Color.white);
					bool flag189 = flag188;
					bool flag190 = flag189;
					if (flag190)
					{
						BtcReplayShare.Copy(data.FilePath);
					}
				}
				else
				{
					GUI.Label(new Rect((float)fs * 2f, r6.y, w - (float)fs * 4f, num8), BtcReplayShare.UiFailed(BtcReplayShare.Error(data.FilePath)), BtcReplayUI.sLabel);
				}
			}
		}
		BtcReplayUI.ContentH = num + rowH * 0.5f;
		return false;
	}

	private static void OpenFolder()
	{
		try
		{
			Directory.CreateDirectory(BtcReplay.ReplaysDir);
			Process.Start(BtcReplay.ReplaysDir);
		}
		catch (Exception)
		{
			try
			{
				Application.OpenURL("file://" + BtcReplay.ReplaysDir);
			}
			catch (Exception)
			{
			}
		}
	}

	public static void OnGUI()
	{
		bool flag = BtcReplay.Mode == BtcReplay.State.Playing;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		if (flag7)
		{
			BtcReplayUI.DrawPlayer();
		}
		else
		{
			BtcReplayUI.Hidden = false;
			BtcReplayMinimap.Reset();
			bool flag8 = BtcReplay.Mode != BtcReplay.State.Recording || !BtcReplay.GetShowIndicator();
			bool flag9 = !flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			if (flag15)
			{
				int num = Mathf.Max(14, Screen.height / 38);
				BtcReplayUI.Ensure(num);
				float unscaledTime = Time.unscaledTime;
				bool flag16 = unscaledTime >= BtcReplayUI.indNext || BtcReplayUI.indText == null;
				bool flag17 = flag16;
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				if (flag23)
				{
					BtcReplayUI.indNext = unscaledTime + 1f;
					BtcReplayUI.indText = "REC " + BtcReplayUI.Clock(BtcReplay.RecSeconds) + "  " + BtcReplayUI.Size(BtcReplay.RecBytes);
				}
				GUI.depth = -900;
				Rect position = new Rect((float)num * 0.6f, (float)num * 0.4f, (float)num * 14f, (float)num * 1.4f);
				Color contentColor = GUI.contentColor;
				GUI.contentColor = new Color(1f, 0.4f, 0.4f, 0.75f);
				GUI.Label(position, BtcReplayUI.indText, BtcReplayUI.sLabel);
				GUI.contentColor = contentColor;
			}
		}
	}

	/// <summary>Replay player panel hidden (Hide UI button / F8); reset when playback ends.</summary>
	public static bool Hidden;

	public static void ToggleHidden()
	{
		BtcReplayUI.Hidden = !BtcReplayUI.Hidden;
	}

	static BtcReplayUI()
	{
		BtcReplayUI.Green = new Color(0.55f, 0.85f, 0.55f, 1f);
		BtcReplayUI.Red = new Color(1f, 0.45f, 0.4f, 1f);
	}

	private static int StepRow(float y, float w, float rowH, int fs, string label, string value)
	{
		Rect r = new Rect(0f, y, w, rowH);
		BtcReplayUI.Fill(r, new Color(1f, 1f, 1f, 0.03f));
		GUI.Label(new Rect((float)fs, y, w - (float)fs * 12f, rowH), label, BtcReplayUI.sLabel);
		float num = (float)fs * 2.2f;
		float num2 = (float)fs * 5f;
		float num3 = w - (float)fs - num * 2f - num2;
		int result = 0;
		bool flag = BtcReplayUI.Button(new Rect(num3, y + rowH * 0.1f, num, rowH * 0.8f), "-", Color.white);
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			result = -1;
		}
		GUI.Label(new Rect(num3 + num, y, num2, rowH), value, BtcReplayUI.sVal);
		bool flag9 = BtcReplayUI.Button(new Rect(num3 + num + num2, y + rowH * 0.1f, num, rowH * 0.8f), "+", Color.white);
		bool flag10 = flag9;
		bool flag11 = flag10;
		bool flag12 = flag11;
		bool flag13 = flag12;
		bool flag14 = flag13;
		bool flag15 = flag14;
		bool flag16 = flag15;
		if (flag16)
		{
			result = 1;
		}
		return result;
	}

	private static string Size(long b)
	{
		bool flag = b < 1048576L;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		string result;
		if (flag8)
		{
			result = (b / 1024L).ToString() + " KB";
		}
		else
		{
			result = ((double)b / 1048576.0).ToString("F1") + " MB";
		}
		return result;
	}

	private static void DrawPlayer()
	{
		int num = Mathf.Max(14, Screen.height / 38);
		BtcReplayUI.Ensure(num);
		GUI.depth = -950;
		BtcReplayMinimap.Tick();
		BtcApm.Warm();
		BtcPovRec.DrawCursors((float)num);
		BtcReplayLayout.DrawHandles(num);
		BtcUnitDots.Draw((float)num);
		float num2 = (float)num * 2f;
		float num3 = (float)num * 3f;
		bool loading = BtcReplay.Loading;
		bool flag = loading;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		if (flag6)
		{
			Rect r = new Rect((float)Screen.width * 0.5f - (float)num * 12f, (float)Screen.height * 0.5f - num2, (float)num * 24f, num2 * 2f);
			BtcReplayUI.Fill(r, new Color(0f, 0f, 0f, 0.6f));
			GUI.Label(new Rect(r.x + (float)num, r.y, r.width - (float)num * 2f, num2), BtcReplayUI.T("REPLAY", "РЕПЛЕЙ") + ": " + BtcReplay.PlayStatus, BtcReplayUI.sLabel);
			bool flag7 = BtcReplayUI.Button(new Rect(r.x + (float)num, r.y + num2, (float)num * 6f, num2 * 0.9f), BtcReplayUI.T("Cancel", "Отмена"), BtcReplayUI.Red);
			bool flag8 = flag7;
			bool flag9 = flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			if (flag13)
			{
				BtcReplay.StopReplay();
			}
		}
		else if (BtcReplayUI.Hidden || !BtcReplayLayout.Visible(BtcReplayLayout.Bar))
		{
			// Replay panel hidden (button or F8): only a faint corner button brings it back; the game UI is untouched.
			Rect rb = new Rect((float)num * 0.5f, (float)Screen.height - num2 * 1.2f, (float)num * 8f, num2);
			if (BtcReplayUI.Hidden && BtcReplayUI.Button(rb, BtcReplayUI.T("Show UI (F8)", "Показать UI (F8)"), new Color(1f, 1f, 1f, 0.35f)))
			{
				BtcReplayUI.ToggleHidden();
			}
		}
		else
		{
			float num4 = (float)Screen.width;
			float hs = BtcReplayLayout.BarScale;
			float apmH = (BtcConfig.ReplayApmMode.Value != 0 && BtcApm.Has) ? (float)num * 1.7f * hs : 0f;
			float barH = (float)num * 4.5f * hs + apmH;
			Rect rect = new Rect(0f, (float)Screen.height - barH, num4, barH);
			BtcReplayLayout.Begin(BtcReplayLayout.Bar, rect);
			BtcReplayUI.Fill(rect, new Color(0f, 0f, 0f, 0.7f));
			float by = rect.y + apmH;
			float num5 = rect.x + (float)num * 0.5f;
			float y = by + (float)num * 2.4f * hs;
			float height = (float)num * 1.9f * hs;
			Rect r2 = new Rect(rect.x + (float)num * 0.5f, by + (float)num * 0.9f * hs, num4 - (float)num, (float)num * 0.8f * hs);
			Rect rect2 = new Rect(r2.x, by + (float)num * 0.2f * hs, r2.width, (float)num * 1.9f * hs);
			Rect waveR = new Rect(r2.x, rect.y + 2f, r2.width, apmH > 0f ? Mathf.Max(0f, r2.y - rect.y - 3f) : 0f);   // down to the timeline line
			if (apmH > 0f) rect2 = Rect.MinMaxRect(rect2.x, waveR.y, rect2.xMax, rect2.yMax);   // the wave is part of the seek area
			float num6 = Mathf.Max(0.001f, BtcReplay.PlayTotal);
			float num7 = BtcReplay.Seeking ? BtcReplay.SeekTarget : BtcReplay.PlayClock;
			Event current = Event.current;
			bool flag14 = current.type == EventType.MouseDown && current.button == 0 && rect2.Contains(current.mousePosition);
			bool flag15 = flag14;
			bool flag16 = flag15;
			bool flag17 = flag16;
			bool flag18 = flag17;
			bool flag19 = flag18;
			if (flag19)
			{
				BtcReplayUI.seekDrag = true;
				current.Use();
			}
			bool flag20 = BtcReplayUI.seekDrag;
			bool flag21 = flag20;
			bool flag22 = flag21;
			bool flag23 = flag22;
			bool flag24 = flag23;
			bool flag25 = flag24;
			if (flag25)
			{
				BtcReplayUI.seekFrac = Mathf.Clamp01((current.mousePosition.x - r2.x) / r2.width);
				num7 = BtcReplayUI.seekFrac * num6;
				bool flag26 = current.type == EventType.MouseUp;
				bool flag27 = flag26;
				bool flag28 = flag27;
				bool flag29 = flag28;
				bool flag30 = flag29;
				bool flag31 = flag30;
				if (flag31)
				{
					BtcReplayUI.seekDrag = false;
					BtcReplay.SeekTo(num7);
					current.Use();
				}
				else
				{
					bool flag32 = current.type == EventType.MouseDrag;
					bool flag33 = flag32;
					bool flag34 = flag33;
					bool flag35 = flag34;
					bool flag36 = flag35;
					bool flag37 = flag36;
					if (flag37)
					{
						current.Use();
					}
				}
			}
			float num8 = Mathf.Clamp01(num7 / num6);
			BtcReplayUI.Fill(r2, new Color(1f, 1f, 1f, 0.18f));
			BtcReplayUI.Fill(new Rect(r2.x, r2.y, r2.width * num8, r2.height), BtcReplay.Seeking ? new Color(1f, 0.75f, 0.2f, 0.95f) : BtcReplayUI.Green);
			BtcReplayUI.Fill(new Rect(r2.x + r2.width * num8 - 2f, r2.y - num2 * 0.1f, 4f, r2.height + num2 * 0.2f), Color.white);
			float autoLift = BtcAutoMarks.DrawLane(r2, num2 * BtcReplayLayout.MarkScale, num6, rect);
			if (apmH > 0f) BtcApm.Draw(waveR, num6, (float)num);
			BtcTimer.DrawTimeline(r2,num2 * BtcReplayLayout.MarkScale, num6, new Rect(rect.x, rect.y - autoLift, rect.width, rect.height));
			bool flag38 = BtcReplayUI.seekDrag || rect2.Contains(current.mousePosition);
			bool flag39 = flag38;
			bool flag40 = flag39;
			bool flag41 = flag40;
			bool flag42 = flag41;
			bool flag43 = flag42;
			if (flag43)
			{
				float num9 = BtcReplayUI.seekDrag ? BtcReplayUI.seekFrac : Mathf.Clamp01((current.mousePosition.x - r2.x) / r2.width);
				float num10 = r2.x + r2.width * num9;
				BtcReplayUI.Fill(new Rect(num10 - 1f, r2.y - num2 * 0.05f, 2f, r2.height + num2 * 0.1f), new Color(1f, 1f, 1f, 0.6f));
				bool flag44 = BtcReplayUI.sTip == null;
				bool flag45 = flag44;
				bool flag46 = flag45;
				bool flag47 = flag46;
				bool flag48 = flag47;
				if (flag48)
				{
					BtcReplayUI.sTip = new GUIStyle(BtcReplayUI.sLabel);
					BtcReplayUI.sTip.alignment = TextAnchor.MiddleCenter;
				}
				BtcReplayUI.sTip.fontSize = BtcReplayUI.sLabel.fontSize;
				float num11 = (float)num * 4.6f;
				float num12 = num2 * 0.9f;
				Rect rect3 = new Rect(Mathf.Clamp(num10 - num11 * 0.5f, rect.x, rect.x + rect.width - num11), rect.y - autoLift - num12 - 4f, num11, num12);
				BtcReplayUI.Fill(rect3, new Color(0f, 0f, 0f, 0.85f));
				GUI.Label(rect3, BtcReplayUI.TClock(num9 * num6), BtcReplayUI.sTip);
					if (apmH > 0f) BtcApm.DrawHover(waveR, num9, num10, rect3.y - 4f, (float)num);
				if (BtcAutoMarks.AltDown)
				{
					BtcAutoMarks.DrawPreview(num9 * num6, num10, rect3.y - 4f, num2 * BtcReplayLayout.MarkScale);
				}
			}
			string text = BtcReplayUI.TClock(num7) + " / " + BtcReplayUI.TClock(BtcReplay.PlayTotal);
			bool seeking = BtcReplay.Seeking;
			bool flag49 = seeking;
			bool flag50 = flag49;
			bool flag51 = flag50;
			bool flag52 = flag51;
			bool flag53 = flag52;
			if (flag53)
			{
				text += " >>";
			}
			bool finished = BtcReplay.Finished;
			bool flag54 = finished;
			bool flag55 = flag54;
			bool flag56 = flag55;
			bool flag57 = flag56;
			bool flag58 = flag57;
			bool flag59 = flag58;
			if (flag59)
			{
				text = text + "  " + BtcReplayUI.T("(end)", "(конец)");
			}
			float u = (float)num * Mathf.Min(1f, ((float)Screen.width - (float)num) / ((float)num * 62f));
			float gap = u * 0.25f;
			float ib = Mathf.Min(height, u * 2f);
			float iy = y + (height - ib) * 0.5f;
			float cx = num5;
			Color wc = new Color(1f, 1f, 1f, 0.9f);
			GUI.Label(new Rect(cx, y, u * 8.5f, height), text, BtcReplayUI.sBarLabel);
			cx += u * 8.5f;
			if (BtcReplayIcons.Plain(new Rect(cx, iy, ib, ib), BtcReplay.PlayPaused ? BtcReplayIcons.Play : BtcReplayIcons.Pause, wc, BtcReplayUI.T("Pause / play (Space)", "Пауза / плей (Пробел)"))) BtcReplay.TogglePause();
			cx += ib + gap * 2f;
			if (BtcReplayIcons.Plain(new Rect(cx, iy, ib, ib), BtcReplayIcons.Slow, wc, BtcReplayUI.T("Slower (Z)", "Медленнее (Z)"))) BtcReplay.SpeedStep(-1);
			cx += ib;
			GUI.Label(new Rect(cx, y, u * 3.5f, height), "x" + BtcReplay.PlaySpeed.ToString("0.##"), BtcReplayUI.sBarVal);
			cx += u * 3.5f;
			if (BtcReplayIcons.Plain(new Rect(cx, iy, ib, ib), BtcReplayIcons.Fast, wc, BtcReplayUI.T("Faster (X)", "Быстрее (X)"))) BtcReplay.SpeedStep(1);
			cx += ib + gap * 2f;
			if (BtcReplayIcons.Plain(new Rect(cx, iy, ib, ib), BtcReplayIcons.Back10, wc, BtcReplayUI.T("Back 10 s (C)", "Назад 10 с (C)"))) BtcReplay.SeekTo(BtcReplay.PlayClock - 10f);
			cx += ib + gap;
			if (BtcReplayIcons.Plain(new Rect(cx, iy, ib, ib), BtcReplayIcons.Fwd10, wc, BtcReplayUI.T("Forward 10 s (V)", "Вперёд 10 с (V)"))) BtcReplay.SeekTo((BtcReplay.Seeking ? BtcReplay.SeekTarget : BtcReplay.PlayClock) + 10f);
			cx += ib + u;
			// POV / spectator switch (Shift+A / Shift+D); click the nick for the list
			if (BtcReplayIcons.Plain(new Rect(cx, iy, ib, ib), BtcReplayIcons.Prev, wc, BtcReplayUI.T("Previous (Shift+A)", "Предыдущий (Shift+A)"))) BtcPov.Cycle(-1);
			cx += ib + gap;
			string povName = BtcPov.Active ? BtcStr.Plain(BtcPov.PovName) : BtcReplayUI.T("Spectator", "Спектатор");
			if (povName != null && povName.Length > 12) povName = povName.Substring(0, 11) + "..";
			Color oldCc = GUI.contentColor;
			if (BtcPov.Active && BtcPov.PovFaction != null) { Color fc = BtcPov.PovFaction.FactionColor; GUI.contentColor = new Color(fc.r, fc.g, fc.b, 1f); }
			Rect nameR = new Rect(cx, y, u * 8f, height);
			BtcReplayUI.Fill(nameR, new Color(1f, 1f, 1f, nameR.Contains(current.mousePosition) || BtcReplayUI.povOpen ? 0.12f : 0.05f));
			GUI.Label(new Rect(cx + u * 0.4f, y, u * 7.6f, height), povName, BtcReplayUI.sBarLabel);
			GUI.contentColor = oldCc;
			if (GUI.Button(nameR, "", BtcReplayUI.sBtn)) BtcReplayUI.povOpen = !BtcReplayUI.povOpen;
			cx += u * 8f + gap;
			if (BtcReplayIcons.Plain(new Rect(cx, iy, ib, ib), BtcReplayIcons.Next, wc, BtcReplayUI.T("Next (Shift+D)", "Следующий (Shift+D)"))) BtcPov.Cycle(1);
			cx += ib + u * 1.5f;
			// icon toggles
			if (BtcReplayIcons.Button(new Rect(cx, iy, ib, ib), BtcReplayIcons.Monitor, false, BtcReplayUI.T("Hide UI (F8)", "Скрыть UI (F8)"))) BtcReplayUI.ToggleHidden();
			cx += ib + gap;
			bool povCam = BtcConfig.PovFollowCam.Value;
			if (BtcReplayIcons.Button(new Rect(cx, iy, ib, ib), BtcReplayIcons.Camera, povCam, BtcReplayUI.T("Follow the player camera", "Камера игрока"))) BtcConfig.PovFollowCam.Value = !povCam;
			cx += ib + gap;
			bool povCur = BtcConfig.PovShowCursor.Value;
			if (BtcReplayIcons.Button(new Rect(cx, iy, ib, ib), BtcReplayIcons.Cursor, povCur, BtcReplayUI.T("Player cursors", "Курсоры игроков"))) BtcConfig.PovShowCursor.Value = !povCur;
			cx += ib + gap;
			bool povInfo = BtcConfig.ReplayInfo.Value;
			if (BtcReplayIcons.Button(new Rect(cx, iy, ib, ib), BtcReplayIcons.Blueprint, povInfo, BtcReplayUI.T("Blueprints + tasks", "Чертежи + задачи"))) BtcConfig.ReplayInfo.Value = !povInfo;
			cx += ib + gap;
			bool povCone = BtcConfig.PovShowCone.Value;
			if (BtcReplayIcons.Button(new Rect(cx, iy, ib, ib), BtcReplayIcons.View, povCone, BtcReplayUI.T("View cones", "Конусы обзора"))) BtcConfig.PovShowCone.Value = !povCone;
			cx += ib + gap;
			int keysMode = BtcConfig.ReplayKeysMode.Value;
			if (BtcReplayIcons.Button(new Rect(cx, iy, ib, ib), BtcReplayIcons.Keys, keysMode != 0, BtcReplayUI.T("Player keys: ", "Нажатия игрока: ") + (keysMode == 0 ? BtcReplayUI.T("hidden", "скрыто") : (keysMode == 1 ? BtcReplayUI.T("feed", "лента") : BtcReplayUI.T("keyboard", "клавиатура"))))) BtcConfig.ReplayKeysMode.Value = (keysMode + 1) % 3;
			cx += ib + gap;
			int apmMode = BtcConfig.ReplayApmMode.Value;
			if (BtcReplayIcons.Button(new Rect(cx, iy, ib, ib), BtcReplayIcons.Wave, apmMode != 0, BtcReplayUI.T("Activity graph: ", "График активности: ") + (apmMode == 0 ? BtcReplayUI.T("hidden", "скрыт") : (apmMode == 1 ? "APM" : "EPM")))) BtcConfig.ReplayApmMode.Value = (apmMode + 1) % 3;
			cx += ib + gap;
			bool hideMm = BtcConfig.ReplayHideMinimap.Value;
			if (BtcReplayIcons.Button(new Rect(cx, iy, ib, ib), BtcReplayIcons.Map, hideMm, BtcReplayUI.T("Hide minimap", "Скрыть миникарту"))) BtcConfig.ReplayHideMinimap.Value = !hideMm;
			cx += ib + gap;
			if (BtcReplayIcons.Button(new Rect(cx, iy, ib, ib), BtcReplayIcons.Layout, BtcReplayLayout.Edit, BtcReplayUI.T("UI settings (F7)", "Настройки UI (F7)"))) BtcReplayLayout.ToggleEdit();
			if (BtcReplayIcons.Plain(new Rect((float)Screen.width - (float)num * 0.5f - ib, iy, ib, ib), BtcReplayIcons.Stop, BtcReplayUI.Red, BtcReplayUI.T("Exit replay", "Выйти из реплея"))) BtcReplay.StopReplay();
			BtcReplayLayout.End();
			BtcReplayInput.Draw((float)num, rect.y - autoLift - 4f);
			BtcReplayIcons.DrawTip(num);
			BtcReplayUI.DrawPovList(nameR, num);
		}
		BtcReplayLayout.DrawWindow(num);
	}

	internal static GUIStyle sBarLabel, sBarVal;

	private static Font barFont;

	private static bool barFontTried;

	/// <summary>Replay bar text: a geometric OS font (Bahnschrift, Win10+) that matches the blocky icons; falls back to the default GUI font.</summary>
	private static void MakeBarStyles(int fs)
	{
		if (!BtcReplayUI.barFontTried)
		{
			BtcReplayUI.barFontTried = true;
			try { BtcReplayUI.barFont = Font.CreateDynamicFontFromOSFont(new string[] { "Bahnschrift SemiBold", "Bahnschrift", "Segoe UI Semibold", "Arial" }, 16); }
			catch (Exception ex) { BtcLog.Warn("ReplayUI", "bar font: " + ex.Message); }
		}
		BtcReplayUI.sBarLabel = new GUIStyle(BtcReplayUI.sLabel);
		BtcReplayUI.sBarVal = new GUIStyle(BtcReplayUI.sVal);
		if (BtcReplayUI.barFont != null)
		{
			BtcReplayUI.sBarLabel.font = BtcReplayUI.barFont;
			BtcReplayUI.sBarVal.font = BtcReplayUI.barFont;
		}
		BtcReplayUI.sBarLabel.normal.textColor = new Color(1f, 1f, 1f, 0.92f);
		BtcReplayUI.sBarVal.normal.textColor = new Color(1f, 1f, 1f, 0.92f);
	}

	internal static bool povOpen;

	/// <summary>Player list above the nick button (click the nick): Spectator + every player, click to switch.</summary>
	private static void DrawPovList(Rect anchor, int fs)
	{
		if (!BtcReplayUI.povOpen) return;
		Event e = Event.current;
		List<BtcPov.PlayerInfo> players = BtcPov.Players();
		float rowH = (float)fs * 1.7f;
		float w = Mathf.Max(anchor.width, (float)fs * 12f);
		float h = rowH * (players.Count + 1) + 4f;
		Rect box = new Rect(Mathf.Clamp(anchor.x, 2f, (float)Screen.width - w - 2f), anchor.y - h - 6f, w, h);
		if (e.type == EventType.MouseDown && !box.Contains(e.mousePosition) && !anchor.Contains(e.mousePosition)) { BtcReplayUI.povOpen = false; return; }
		BtcReplayUI.Fill(box, new Color(0.04f, 0.04f, 0.06f, 0.95f));
		for (int i = 0; i <= players.Count; i++)
		{
			Rect row = new Rect(box.x + 2f, box.y + 2f + rowH * i, w - 4f, rowH);
			bool cur = i == 0 ? !BtcPov.Active : (players[i - 1].faction == BtcPov.PovFaction);
			BtcReplayUI.Fill(row, cur ? new Color(0.5f, 1f, 0.55f, 0.16f) : new Color(1f, 1f, 1f, row.Contains(e.mousePosition) ? 0.14f : 0.04f));
			Color old = GUI.contentColor;
			if (i > 0) { Color fc = players[i - 1].faction.FactionColor; GUI.contentColor = new Color(fc.r, fc.g, fc.b, 1f); }
			GUI.Label(new Rect(row.x + (float)fs * 0.6f, row.y, row.width - (float)fs * 0.6f, rowH), i == 0 ? BtcReplayUI.T("Spectator", "Спектатор") : players[i - 1].name, BtcReplayUI.sBarLabel);
			GUI.contentColor = old;
			if (GUI.Button(row, "", BtcReplayUI.sBtn))
			{
				if (i == 0) BtcPov.Reset(); else BtcPov.SetPov(players[i - 1].faction, players[i - 1].name);
				BtcReplayUI.povOpen = false;
				return;
			}
		}
		if (e.type == EventType.MouseDown && box.Contains(e.mousePosition)) e.Use();
	}

	public static bool TabActive;

	public static float ContentH;

	private static List<BtcReplay.Data> list = new List<BtcReplay.Data>();

	private static bool listLoaded;

	private static string confirmPath;

	private static float confirmUntil;

	private static Texture2D white;

	private static GUIStyle sRow;

	internal static GUIStyle sLabel;

	private static GUIStyle sNote;

	internal static GUIStyle sHead;

	internal static GUIStyle sBtn;

	private static int styleFs;

	internal static readonly Color Green;

	internal static readonly Color Red;

	internal static GUIStyle sVal;

	private static string indText;

	private static float indNext;

	private static readonly int[] KeySteps = new int[]
	{
		0,
		1,
		2,
		3,
		5,
		10,
		15,
		20,
		30,
		60
	};

	private static bool seekDrag;

	private static float seekFrac;

	private static GUIStyle sTip;
}
}
