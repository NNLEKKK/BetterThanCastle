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

	internal static void Refresh()
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
			BtcReplayUI.sHead.normal.textColor = BtcMenu.AccentA(0.95f);
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
		BtcReplayUI.Fill(rect, on ? BtcMenu.AccentA(0.08f) : new Color(1f, 1f, 1f, 0.03f));
		float num = (float)fs * 1.1f;
		Rect r = new Rect((float)fs, rect.y + (rowH - num) * 0.5f, num, num);
		BtcReplayUI.Fill(r, new Color(1f, 1f, 1f, 0.5f));
		BtcReplayUI.Fill(new Rect(r.x + 1f, r.y + 1f, r.width - 2f, r.height - 2f), new Color(0.08f, 0.08f, 0.1f, 1f));
		if (on)
		{
			BtcReplayUI.Fill(new Rect(r.x + 3f, r.y + 3f, r.width - 6f, r.height - 6f), BtcReplayUI.Green);
		}
		return BtcSfx.Btn(rect, label, BtcReplayUI.sRow);
	}

	internal static bool Button(Rect r, string text, Color c)
	{
		BtcReplayUI.Fill(r, new Color(1f, 1f, 1f, 0.07f));
		Color contentColor = GUI.contentColor;
		GUI.contentColor = c;
		bool result = BtcSfx.Btn(r, text, BtcReplayUI.sBtn);
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
		if (BtcReplayLibrary.SettingsOpen) num = BtcReplaySettings.Draw(e, num, w, fs);
		num += rowH * 0.3f;
		num = BtcReplayLibrary.Draw(e, num, w, rowH, fs);
		BtcReplayUI.ContentH = num + rowH * 0.5f;
		return false;
	}

	internal static void OpenFolder()
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

	internal static string Size(long b)
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
			BtcReplayLoadUI.EnsureHost();
		}
		else if (BtcReplayUI.Hidden || !BtcReplayLayout.Visible(BtcReplayLayout.Bar))
		{
			// Replay panel hidden (button or F8): only a faint corner button brings it back; the game UI is untouched.
			float sb = (float)num * 1.9f;
			Rect rb = new Rect((float)num * 0.5f, (float)Screen.height - sb - (float)num * 0.5f, sb, sb);
			if (BtcReplayUI.Hidden && BtcReplayIcons.Button(rb, BtcReplayIcons.Monitor, false, BtcReplayUI.T("Show UI (F8)", "Показать UI (F8)")))
			{
				BtcReplayUI.ToggleHidden();
			}
			BtcReplayIcons.DrawTip(num);
		}
		else
		{
			float num4 = (float)Screen.width;
			float hs = BtcReplayLayout.BarScale;
			float apmH = (BtcConfig.ReplayApmMode.Value != 0 && BtcApm.Has) ? (float)num * 1.7f * hs * BtcReplayLayout.ApmHeight : 0f;
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
			Rect waveR = new Rect(r2.x, rect.y + 2f, r2.width, apmH > 0f ? Mathf.Max(0f, r2.center.y - 6f - rect.y) : 0f);   // down to the timeline line
			if (apmH > 0f) rect2 = Rect.MinMaxRect(rect2.x, waveR.y, rect2.xMax, rect2.yMax);   // the wave is part of the seek area
			float num6 = Mathf.Max(0.001f, BtcReplay.PlayTotal);
			float num7 = BtcReplay.Seeking ? BtcReplay.SeekTarget : BtcReplay.PlayClock;
			Event current = Event.current;
			BtcReplayGif.HandleTrack(rect2, r2, num6);
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
					BtcSfx.Jump(); BtcReplay.SeekTo(num7);
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
			BtcReplayTrack.Draw(r2, num8, BtcReplay.Seeking, BtcReplayUI.seekDrag || rect2.Contains(current.mousePosition), num6, num);
			float autoLift = BtcAutoMarks.DrawLane(BtcReplayTrack.Line(r2), num2 * BtcReplayLayout.MarkScale, num6, rect);
			BtcReplayGif.DrawRange(r2, num6); if (apmH > 0f) BtcApm.Draw(waveR, num6, (float)num);
			BtcTimer.DrawTimeline(BtcReplayTrack.Line(r2), num2 * BtcReplayLayout.MarkScale, num6, new Rect(rect.x, rect.y - autoLift, rect.width, rect.height));
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
				BtcReplayUI.Fill(new Rect(num10 - 0.5f, r2.center.y - 11f, 1f, 22f), new Color(1f, 1f, 1f, 0.5f));
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
			if (BtcSfx.Btn(nameR, "", BtcReplayUI.sBtn)) BtcReplayUI.povOpen = !BtcReplayUI.povOpen;
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
			if (BtcReplayIcons.Button(new Rect(cx, iy, ib, ib), BtcReplayIcons.Layout, false, BtcReplayUI.T("UI editor (F7)", "UI редактор (F7)"))) ResourcePackManager.OpenUiEditor(true); cx += ib + gap; BtcReplayGif.DrawButton(ref cx, iy, ib, gap);
			if (BtcReplayIcons.Plain(new Rect((float)Screen.width - (float)num * 0.5f - ib, iy, ib, ib), BtcReplayIcons.Stop, BtcReplayUI.Red, BtcReplayUI.T("Exit replay", "Выйти из реплея"))) BtcReplay.StopReplay();
			BtcReplayLayout.End();
			BtcReplayInput.Draw((float)num, rect.y - autoLift - 4f); BtcReplayGif.DrawPanel(rect, autoLift, (float)num);
			BtcReplayIcons.DrawTip(num);
			BtcReplayUI.DrawPovList(nameR, num);
		}
		BtcReplayLayout.DrawWindow(num);
		BtcReplayLoadUI.EnsureHost();
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
			BtcReplayUI.Fill(row, cur ? BtcMenu.AccentA(0.16f) : new Color(1f, 1f, 1f, row.Contains(e.mousePosition) ? 0.14f : 0.04f));
			Color old = GUI.contentColor;
			if (i > 0) { Color fc = players[i - 1].faction.FactionColor; GUI.contentColor = new Color(fc.r, fc.g, fc.b, 1f); }
			GUI.Label(new Rect(row.x + (float)fs * 0.6f, row.y, row.width - (float)fs * 0.6f, rowH), i == 0 ? BtcReplayUI.T("Spectator", "Спектатор") : players[i - 1].name, BtcReplayUI.sBarLabel);
			GUI.contentColor = old;
			if (BtcSfx.Btn(row, "", BtcReplayUI.sBtn))
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

	internal static List<BtcReplay.Data> list = new List<BtcReplay.Data>();

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

	internal static Color Green { get { return BtcMenu.Accent; } }

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
