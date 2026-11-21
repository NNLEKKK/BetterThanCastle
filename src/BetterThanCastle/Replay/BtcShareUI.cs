using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

namespace BetterThanCastle
{
public static class BtcShareUI
{
	internal static string T(string en, string ru)
	{
		return BtcLang.Russian ? ru : en;
	}

	public static void OnMenuOpen()
	{
		BtcShareUI.listLoaded = false;
		BtcShareUI.confirmKey = null;
	}

	internal static void Refresh()
	{
		BtcShareUI.listLoaded = true;
		List<BtcShareUI.Entry> list = new List<BtcShareUI.Entry>();
		for (int i = 0; i < BtcShareUI.RootPaths.Length; i++)
		{
			string text = BtcShareUI.RootPaths[i];
			bool flag = !Directory.Exists(text);
			if (!flag)
			{
				bool isMap = i >= 3;
				string origin = (i % 3 == 0) ? string.Empty : ((i % 3 == 1) ? "UGC" : "Downloads");
				Dictionary<string, BtcShareUI.Entry> dictionary = new Dictionary<string, BtcShareUI.Entry>();
				try
				{
					string[] directories = Directory.GetDirectories(text);
					for (int j = 0; j < directories.Length; j++)
					{
						string fileName = Path.GetFileName(directories[j]);
						bool flag2 = fileName.EndsWith(".btcs_unpack", StringComparison.Ordinal);
						if (!flag2)
						{
							BtcShareUI.Entry entry = new BtcShareUI.Entry();
							entry.name = fileName;
							entry.folder = directories[j];
							entry.btcs = Path.Combine(text, fileName + ".btcs");
							entry.origin = origin;
							entry.isMap = isMap;
							entry.hasFolder = true;
							dictionary[fileName] = entry;
							list.Add(entry);
						}
					}
					string[] files = Directory.GetFiles(text, "*.btcs");
					for (int k = 0; k < files.Length; k++)
					{
						string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(files[k]);
						BtcShareUI.Entry entry2;
						bool flag3 = !dictionary.TryGetValue(fileNameWithoutExtension, out entry2);
						if (flag3)
						{
							entry2 = new BtcShareUI.Entry();
							entry2.name = fileNameWithoutExtension;
							entry2.folder = Path.Combine(text, fileNameWithoutExtension);
							entry2.btcs = files[k];
							entry2.origin = origin;
							entry2.isMap = isMap;
							list.Add(entry2);
						}
						BtcShareUI.ReloadBtcs(entry2);
					}
				}
				catch (Exception)
				{
				}
			}
		}
		list.Sort(delegate(BtcShareUI.Entry a, BtcShareUI.Entry b)
		{
			bool flag4 = a.isMap != b.isMap;
			int result;
			if (flag4)
			{
				result = (a.isMap ? 1 : -1);
			}
			else
			{
				result = string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
			}
			return result;
		});
		BtcShareUI.list = list;
	}

	internal static void ReloadBtcs(BtcShareUI.Entry en)
	{
		en.hasBtcs = false;
		en.err = null;
		en.btcsBytes = 0L;
		try
		{
			bool flag = !File.Exists(en.btcs);
			if (!flag)
			{
				en.hasBtcs = true;
				en.btcsBytes = new FileInfo(en.btcs).Length;
				BtcSaveFormat.Info info;
				string err;
				bool flag2 = !BtcSaveFormat.ReadInfo(en.btcs, out info, out err);
				if (flag2)
				{
					en.err = err;
				}
			}
		}
		catch (Exception ex)
		{
			en.err = ex.Message;
		}
	}

	internal static void Say(string m)
	{
		BtcShareUI.msg = m;
		BtcShareUI.msgUntil = Time.unscaledTime + 8f;
	}

	internal static void StartPack(BtcShareUI.Entry en)
	{
		bool flag = en.job == 1;
		if (!flag)
		{
			BtcSaveFormat.LoadSettings();
			en.job = 1;
			en.jobKind = "pack";
			en.jobMsg = null;
			string folder = en.folder;
			string outFile = en.btcs;
			bool isMap = en.isMap;
			new Thread(delegate()
			{
				string jobMsg = null;
				bool flag2 = false;
				try
				{
					flag2 = BtcSaveFormat.Pack(folder, outFile, isMap, out jobMsg);
				}
				catch (Exception ex)
				{
					jobMsg = ex.Message;
				}
				en.jobMsg = jobMsg;
				en.job = (flag2 ? 2 : 3);
			})
			{
				IsBackground = true
			}.Start();
		}
	}

	internal static void StartNormal(BtcShareUI.Entry en)
	{
		bool flag = en.job == 1;
		if (!flag)
		{
			en.job = 1;
			en.jobKind = "normal";
			en.jobMsg = null;
			string file = en.btcs;
			new Thread(delegate()
			{
				string jobMsg = null;
				bool flag2 = false;
				try
				{
					string text;
					flag2 = BtcSaveFormat.ToNormal(file, out text, out jobMsg);
				}
				catch (Exception ex)
				{
					jobMsg = ex.Message;
				}
				en.jobMsg = jobMsg;
				en.job = (flag2 ? 2 : 3);
			})
			{
				IsBackground = true
			}.Start();
		}
	}

	internal static void PollJobs()
	{
		for (int i = 0; i < BtcShareUI.list.Count; i++)
		{
			BtcShareUI.Entry entry = BtcShareUI.list[i];
			int job = entry.job;
			bool flag = job != 2 && job != 3;
			if (!flag)
			{
				string jobKind = entry.jobKind;
				entry.job = 0;
				bool flag2 = job == 2;
				if (flag2)
				{
					bool flag3 = jobKind == "pack";
					if (flag3)
					{
						BtcShareUI.ReloadBtcs(entry);
						BtcShareUI.Say(string.Concat(new string[]
						{
							BtcShareUI.T("Packed: ", "Сжато: "),
							entry.name,
							".btcs (",
							BtcShareUI.Size(entry.btcsBytes),
							")"
						}));
					}
					else
					{
						entry.hasFolder = true;
						BtcShareUI.Say(BtcShareUI.T("Converted to a normal " + (entry.isMap ? "map: " : "save: "), entry.isMap ? "Превращено в обычную карту: " : "Превращено в обычный сейв: ") + entry.name);
					}
				}
				else
				{
					BtcShareUI.Say(BtcShareUI.T("Failed: ", "Ошибка: ") + entry.name + "   " + entry.jobMsg);
				}
			}
		}
	}

	internal static void PollImport()
	{
		bool flag = BtcShareUI.dl == null || !BtcShareUI.dl.isDone;
		if (!flag)
		{
			WWW www = BtcShareUI.dl;
			BtcShareUI.dl = null;
			bool flag2 = !string.IsNullOrEmpty(www.error);
			if (flag2)
			{
				BtcShareUI.Say(BtcShareUI.T("Download failed: ", "Не удалось скачать: ") + www.error);
			}
			else
			{
				BtcShareUI.Say(BtcShareUI.ImportBytes(www.bytes));
				BtcShareUI.Refresh();
			}
		}
	}

	internal static string SafeName(string n)
	{
		bool flag = string.IsNullOrEmpty(n);
		string result;
		if (flag)
		{
			result = "imported";
		}
		else
		{
			char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
			for (int i = 0; i < invalidFileNameChars.Length; i++)
			{
				n = n.Replace(invalidFileNameChars[i], '_');
			}
			n = n.Trim();
			result = ((n.Length == 0) ? "imported" : n);
		}
		return result;
	}

	internal static string Unique(string dir, string baseName, string ext)
	{
		string text = Path.Combine(dir, baseName + ext);
		int num = 1;
		while (File.Exists(text))
		{
			text = Path.Combine(dir, baseName + "_" + num.ToString() + ext);
			num++;
		}
		return text;
	}

	internal static string ImportBytes(byte[] b)
	{
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		string text = null;
		string text2 = Path.Combine(Path.GetTempPath(), "btc_import_" + DateTime.Now.Ticks.ToString());
		try
		{
			bool flag = b == null || b.Length < 5;
			if (flag)
			{
				return BtcShareUI.T("Nothing downloaded.", "Ничего не скачалось.");
			}
			Directory.CreateDirectory(text2);
			bool flag2 = b[0] == 80 && b[1] == 75;
			if (flag2)
			{
				string text3 = Path.Combine(text2, "in.zip");
				File.WriteAllBytes(text3, b);
				string text4 = Path.Combine(text2, "x");
				Directory.CreateDirectory(text4);
				int[] progress = new int[]
				{
					10
				};
				lzip.decompress_File(text3, text4, progress, null, null, null);
				string[] files = Directory.GetFiles(text4, "*.*", SearchOption.AllDirectories);
				for (int i = 0; i < files.Length; i++)
				{
					BtcShareUI.PlaceFile(files[i], ref num, ref num2, ref num3, ref text);
				}
			}
			else
			{
				string text5 = Path.Combine(text2, "raw.bin");
				File.WriteAllBytes(text5, b);
				BtcShareUI.PlaceFile(text5, ref num, ref num2, ref num3, ref text);
			}
		}
		catch (Exception ex)
		{
			return BtcShareUI.T("Import failed: ", "Ошибка импорта: ") + ex.Message;
		}
		finally
		{
			try
			{
				bool flag3 = Directory.Exists(text2);
				if (flag3)
				{
					Directory.Delete(text2, true);
				}
			}
			catch (Exception)
			{
			}
		}
		bool flag4 = num3 > 0;
		if (flag4)
		{
			BtcReplayUI.OnMenuOpen();
		}
		bool flag5 = num + num2 + num3 == 0;
		string result;
		if (flag5)
		{
			bool flag6 = text != null;
			if (flag6)
			{
				result = BtcShareUI.T("Import failed: ", "Ошибка импорта: ") + text;
			}
			else
			{
				result = BtcShareUI.T("Unknown file. A direct link to a .btcs / .btcr file (or a zip with it) is needed.", "Файл не распознан. Нужна прямая ссылка на .btcs / .btcr (или zip с ним).");
			}
		}
		else
		{
			string text6 = BtcShareUI.T("Imported: ", "Импортировано: ");
			bool flag7 = num > 0;
			if (flag7)
			{
				text6 = text6 + num.ToString() + " " + BtcShareUI.T("save(s) ", "сейв(ов) ");
			}
			bool flag8 = num2 > 0;
			if (flag8)
			{
				text6 = text6 + num2.ToString() + " " + BtcShareUI.T("map(s) ", "карт ");
			}
			bool flag9 = num3 > 0;
			if (flag9)
			{
				text6 = text6 + num3.ToString() + " " + BtcShareUI.T("replay(s)", "реплей(ев)");
			}
			bool flag10 = text != null;
			if (flag10)
			{
				text6 = text6 + "   " + text;
			}
			result = text6;
		}
		return result;
	}

	internal static void PlaceFile(string file, ref int saves, ref int maps, ref int replays, ref string firstErr)
	{
		byte[] array = new byte[4];
		int num;
		using (FileStream fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
		{
			num = fileStream.Read(array, 0, 4);
		}
		bool flag = num < 4;
		if (!flag)
		{
			bool flag2 = array[0] == 66 && array[1] == 84 && array[2] == 67 && array[3] == 83;
			if (flag2)
			{
				BtcSaveFormat.Info info;
				string text;
				bool flag3 = !BtcSaveFormat.ReadInfo(file, out info, out text);
				if (flag3)
				{
					bool flag4 = firstErr == null;
					if (flag4)
					{
						firstErr = text;
					}
				}
				else
				{
					string text2 = info.isMap ? "Info/UGC/Maps" : "Info/UGC/Saves";
					Directory.CreateDirectory(text2);
					File.Copy(file, BtcShareUI.Unique(text2, BtcShareUI.SafeName(info.name), ".btcs"));
					bool isMap = info.isMap;
					if (isMap)
					{
						maps++;
					}
					else
					{
						saves++;
					}
				}
			}
			else
			{
				bool flag5 = array[0] == 66 && array[1] == 84 && array[2] == 67 && array[3] == 82;
				if (flag5)
				{
					string replaysDir = BtcReplay.ReplaysDir;
					Directory.CreateDirectory(replaysDir);
					File.Copy(file, BtcShareUI.Unique(replaysDir, BtcShareUI.SafeName((Path.GetFileNameWithoutExtension(file) == "raw") ? "imported_replay" : Path.GetFileNameWithoutExtension(file)), ".btcr"));
					replays++;
				}
			}
		}
	}

	internal static void Ensure(int fs)
	{
		bool flag = BtcShareUI.white == null;
		if (flag)
		{
			BtcShareUI.white = new Texture2D(1, 1, TextureFormat.RGBA32, false);
			BtcShareUI.white.SetPixel(0, 0, Color.white);
			BtcShareUI.white.Apply();
			BtcShareUI.white.hideFlags = HideFlags.HideAndDontSave;
		}
		bool flag2 = BtcShareUI.sRow != null && BtcShareUI.styleFs == fs;
		if (!flag2)
		{
			BtcShareUI.styleFs = fs;
			BtcShareUI.sRow = new GUIStyle(GUI.skin.button);
			BtcShareUI.sRow.alignment = TextAnchor.MiddleLeft;
			BtcShareUI.sRow.fontSize = fs;
			BtcShareUI.sRow.padding = new RectOffset(Mathf.RoundToInt((float)fs * 2.8f), fs, 0, 0);
			BtcShareUI.sRow.border = new RectOffset(0, 0, 0, 0);
			BtcShareUI.sRow.margin = new RectOffset(0, 0, 0, 0);
			BtcShareUI.SetColors(BtcShareUI.sRow, new Color(1f, 1f, 1f, 0.85f), Color.white);
			BtcShareUI.sLabel = new GUIStyle(GUI.skin.label);
			BtcShareUI.sLabel.alignment = TextAnchor.MiddleLeft;
			BtcShareUI.sLabel.fontSize = fs;
			BtcShareUI.sLabel.padding = new RectOffset(0, 0, 0, 0);
			BtcShareUI.sLabel.clipping = TextClipping.Clip;
			BtcShareUI.sLabel.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
			BtcShareUI.sNote = new GUIStyle(BtcShareUI.sLabel);
			BtcShareUI.sNote.fontSize = Mathf.Max(10, Mathf.RoundToInt((float)fs * 0.85f));
			BtcShareUI.sNote.wordWrap = true;
			BtcShareUI.sNote.alignment = TextAnchor.UpperLeft;
			BtcShareUI.sNote.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
			BtcShareUI.sHead = new GUIStyle(BtcShareUI.sLabel);
			BtcShareUI.sHead.fontStyle = FontStyle.Bold;
			BtcShareUI.sHead.normal.textColor = BtcMenu.AccentA(0.95f);
			BtcShareUI.sBtn = BtcUiStyle.Button(fs);
			BtcShareUI.sVal = new GUIStyle(BtcShareUI.sLabel);
			BtcShareUI.sVal.alignment = TextAnchor.MiddleCenter;
			BtcShareUI.sField = BtcUiStyle.Field(fs);
		}
	}

	internal static void SetColors(GUIStyle st, Color normal, Color hover)
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
		bool flag = Event.current.type == EventType.Repaint;
		if (flag)
		{
			Color color = GUI.color;
			GUI.color = c;
			GUI.DrawTexture(r, BtcShareUI.white);
			GUI.color = color;
		}
	}

	internal static float Note(float y, float w, int fs, string text)
	{
		float num = BtcShareUI.sNote.CalcHeight(new GUIContent(text), w - (float)fs * 2f);
		GUI.Label(new Rect((float)fs, y + 2f, w - (float)fs * 2f, num), text, BtcShareUI.sNote);
		return y + num + 6f;
	}

	internal static bool CheckRow(ref float y, float w, float rowH, int fs, string label, bool on)
	{
		Rect rect = new Rect(0f, y, w, rowH);
		y += rowH;
		BtcShareUI.Fill(rect, on ? BtcMenu.AccentA(0.08f) : new Color(1f, 1f, 1f, 0.03f));
		float num = (float)fs * 1.1f;
		Rect r = new Rect((float)fs, rect.y + (rowH - num) * 0.5f, num, num);
		BtcShareUI.Fill(r, new Color(1f, 1f, 1f, 0.5f));
		BtcShareUI.Fill(new Rect(r.x + 1f, r.y + 1f, r.width - 2f, r.height - 2f), new Color(0.08f, 0.08f, 0.1f, 1f));
		if (on)
		{
			BtcShareUI.Fill(new Rect(r.x + 3f, r.y + 3f, r.width - 6f, r.height - 6f), BtcShareUI.Green);
		}
		return GUI.Button(rect, label, BtcShareUI.sRow);
	}

	internal static bool Button(Rect r, string text, Color c)
	{
		BtcShareUI.Fill(r, new Color(1f, 1f, 1f, 0.07f));
		Color contentColor = GUI.contentColor;
		GUI.contentColor = c;
		bool result = GUI.Button(r, text, BtcShareUI.sBtn);
		GUI.contentColor = contentColor;
		return result;
	}

	internal static int Slider(Rect r, int val, int min, int max)
	{
		int controlID = GUIUtility.GetControlID(FocusType.Passive);
		Event current = Event.current;
		float num = r.height * 0.5f;
		float num2 = r.x + num;
		float num3 = Mathf.Max(1f, r.width - num * 2f);
		EventType typeForControl = current.GetTypeForControl(controlID);
		bool flag = typeForControl == EventType.MouseDown && current.button == 0 && r.Contains(current.mousePosition);
		if (flag)
		{
			GUIUtility.hotControl = controlID;
			val = min + Mathf.RoundToInt(Mathf.Clamp01((current.mousePosition.x - num2) / num3) * (float)(max - min));
			current.Use();
		}
		else
		{
			bool flag2 = typeForControl == EventType.MouseDrag && GUIUtility.hotControl == controlID;
			if (flag2)
			{
				val = min + Mathf.RoundToInt(Mathf.Clamp01((current.mousePosition.x - num2) / num3) * (float)(max - min));
				current.Use();
			}
			else
			{
				bool flag3 = typeForControl == EventType.MouseUp && GUIUtility.hotControl == controlID;
				if (flag3)
				{
					GUIUtility.hotControl = 0;
					current.Use();
				}
			}
		}
		bool flag4 = current.type == EventType.Repaint;
		if (flag4)
		{
			float num4 = r.y + r.height * 0.5f;
			BtcShareUI.Fill(new Rect(num2, num4 - 2f, num3, 4f), new Color(1f, 1f, 1f, 0.2f));
			float num5 = (max > min) ? ((float)(val - min) / (float)(max - min)) : 0f;
			BtcShareUI.Fill(new Rect(num2, num4 - 2f, num3 * num5, 4f), BtcMenu.AccentA(0.8f));
			float num6 = r.height * 0.6f;
			BtcShareUI.Fill(new Rect(num2 + num3 * num5 - num6 * 0.5f, num4 - num6 * 0.5f, num6, num6), BtcShareUI.Green);
		}
		return Mathf.Clamp(val, min, max);
	}

	internal static string Size(long b)
	{
		bool flag = b < 1048576L;
		string result;
		if (flag)
		{
			result = (b / 1024L).ToString() + " KB";
		}
		else
		{
			result = ((double)b / 1048576.0).ToString("F1") + " MB";
		}
		return result;
	}

	public static void Draw(Event e, float w, float rowH, int fs, Color old)
	{
		BtcShareUI.Ensure(fs);
		BtcSaveFormat.LoadSettings();
		BtcReplayShare.Poll();
		BtcShareUI.PollJobs();
		BtcShareUI.PollImport();
		bool flag = !BtcShareUI.listLoaded;
		if (flag)
		{
			BtcShareUI.Refresh();
		}
		float num = 0f;
		num = BtcShareUI.Note(num, w, fs, BtcShareUI.T("Saves and maps as single compact .btcs files: copy the file, make a download link, or turn it back into a normal save. The .btcs lies next to the original folder. Heavy work runs in the background.", "Сейвы и карты в виде одного компактного файла .btcs: скопировать файл, создать ссылку или превратить обратно в обычный сейв. Файл .btcs лежит рядом с исходной папкой. Тяжёлая работа идёт в фоне."));
		bool flag2 = BtcShareUI.CheckRow(ref num, w, rowH, fs, BtcShareUI.T("Store thumbnail inside .btcs (off = read it from the normal folder)", "Хранить тамбнейл внутри .btcs (выкл. = читать из обычной папки)"), BtcSaveFormat.IncludeThumb);
		if (flag2)
		{
			BtcSaveFormat.IncludeThumb = !BtcSaveFormat.IncludeThumb;
			BtcSaveFormat.SaveSettings();
		}
		bool includeThumb = BtcSaveFormat.IncludeThumb;
		if (includeThumb)
		{
			Rect r = new Rect(0f, num, w, rowH);
			BtcShareUI.Fill(r, new Color(1f, 1f, 1f, 0.03f));
			GUI.Label(new Rect((float)fs, num, w * 0.38f, rowH), BtcShareUI.T("Thumbnail downscale", "Уменьшение тамбнейла"), BtcShareUI.sLabel);
			int num2 = Mathf.Clamp(BtcSaveFormat.ThumbFactor, 1, 15);
			int num3 = BtcShareUI.Slider(new Rect(w * 0.4f, num + rowH * 0.1f, w * 0.42f, rowH * 0.8f), num2, 1, 15);
			GUI.Label(new Rect(w * 0.84f, num, w * 0.14f, rowH), (num3 == 1) ? BtcShareUI.T("original", "оригинал") : (num3.ToString() + "x"), BtcShareUI.sVal);
			num += rowH;
			bool flag3 = num3 != num2;
			if (flag3)
			{
				BtcSaveFormat.ThumbFactor = num3;
				BtcShareUI.settingsDirty = true;
			}
			bool flag4 = BtcShareUI.settingsDirty && Event.current.type == EventType.MouseUp;
			if (flag4)
			{
				BtcShareUI.settingsDirty = false;
				BtcSaveFormat.SaveSettings();
			}
		}
		num = BtcShareUI.Note(num, w, fs, BtcShareUI.T("Heightmap.png always stays in full quality, the map is built from it. Only the thumbnail is reduced (1x = original size, 15x = about 15 times smaller side).", "Heightmap.png всегда остаётся в полном качестве, по нему строится карта. Уменьшается только тамбнейл (1x = оригинал, 15x = сторона примерно в 15 раз меньше)."));
		num += rowH * 0.2f;
		GUI.Label(new Rect((float)fs, num, w * 0.6f, rowH), BtcShareUI.T("Import by link (.btcs, .btcr or zip)", "Импорт по ссылке (.btcs, .btcr или zip)"), BtcShareUI.sHead);
		num += rowH;
		float num4 = (float)fs * 7f;
		Rect position = new Rect((float)fs, num + rowH * 0.1f, w - (float)fs * 3f - num4 * 2f, rowH * 0.8f);
		BtcShareUI.importUrl = GUI.TextField(position, BtcShareUI.importUrl, 2048, BtcShareUI.sField);
		bool flag5 = BtcShareUI.Button(new Rect(position.xMax + (float)fs * 0.5f, position.y, num4, position.height), BtcShareUI.T("Paste", "Вставить"), Color.white);
		if (flag5)
		{
			string systemCopyBuffer = GUIUtility.systemCopyBuffer;
			bool flag6 = !string.IsNullOrEmpty(systemCopyBuffer);
			if (flag6)
			{
				BtcShareUI.importUrl = systemCopyBuffer.Trim();
			}
		}
		bool flag7 = BtcShareUI.dl != null;
		bool flag8 = BtcShareUI.Button(new Rect(position.xMax + (float)fs + num4, position.y, num4, position.height), flag7 ? BtcShareUI.T("Loading...", "Загрузка...") : BtcShareUI.T("Import", "Импорт"), flag7 ? new Color(1f, 1f, 1f, 0.4f) : BtcShareUI.Green) && !flag7;
		if (flag8)
		{
			string text = (BtcShareUI.importUrl ?? string.Empty).Trim();
			bool flag9 = text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || text.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
			if (flag9)
			{
				BtcShareUI.dl = new WWW(text);
				BtcShareUI.Say(BtcShareUI.T("Downloading...", "Скачиваю..."));
			}
			else
			{
				BtcShareUI.Say(BtcShareUI.T("Paste a link starting with https://", "Вставьте ссылку, начинающуюся с https://"));
			}
		}
		num += rowH;
		num = BtcShareUI.Note(num, w, fs, BtcShareUI.T("Needs a direct file link (catbox links from the Create link button work). Replays can be imported the same way.", "Нужна прямая ссылка на файл (подходят ссылки catbox, которые даёт кнопка «Создать ссылку»). Реплеи импортируются так же."));
		bool flag10 = !string.IsNullOrEmpty(BtcShareUI.msg) && Time.unscaledTime < BtcShareUI.msgUntil;
		if (flag10)
		{
			num = BtcShareUI.Note(num, w, fs, BtcShareUI.msg);
		}
		num += rowH * 0.3f;
		GUI.Label(new Rect((float)fs, num, w * 0.5f, rowH), BtcShareUI.T("Saves and maps", "Сейвы и карты") + " (" + BtcShareUI.list.Count.ToString() + ")", BtcShareUI.sHead);
		bool flag11 = BtcShareUI.Button(new Rect(w - num4 - (float)fs, num + rowH * 0.1f, num4, rowH * 0.8f), BtcShareUI.T("Refresh", "Обновить"), Color.white);
		if (flag11)
		{
			BtcShareUI.Refresh();
		}
		num += rowH;
		bool flag12 = BtcShareUI.list.Count == 0;
		if (flag12)
		{
			num = BtcShareUI.Note(num, w, fs, BtcShareUI.T("No saves or maps found in the Info folder.", "В папке Info не найдено сейвов и карт."));
		}
		float num5 = (float)fs * 7.5f;
		for (int i = 0; i < BtcShareUI.list.Count; i++)
		{
			BtcShareUI.Entry entry = BtcShareUI.list[i];
			Rect r2 = new Rect(0f, num, w, rowH);
			num += rowH;
			BtcShareUI.Fill(r2, (i % 2 == 0) ? new Color(1f, 1f, 1f, 0.03f) : new Color(1f, 1f, 1f, 0f));
			bool flag13 = entry.job == 1;
			bool flag14 = entry.hasBtcs && entry.err == null;
			string str = entry.isMap ? BtcShareUI.T("Map", "Карта") : BtcShareUI.T("Save", "Сейв");
			string text2 = entry.name + "   " + str + ((entry.origin.Length > 0) ? (" " + entry.origin) : string.Empty);
			Color red = Color.white;
			bool flag15 = flag13;
			if (flag15)
			{
				text2 = text2 + "   " + ((entry.jobKind == "pack") ? BtcShareUI.T("packing...", "сжимаю...") : BtcShareUI.T("converting...", "превращаю..."));
			}
			else
			{
				bool flag16 = entry.err != null;
				if (flag16)
				{
					text2 = text2 + "   .btcs: " + entry.err;
					red = BtcShareUI.Red;
				}
				else
				{
					bool hasBtcs = entry.hasBtcs;
					if (hasBtcs)
					{
						text2 = text2 + "   .btcs " + BtcShareUI.Size(entry.btcsBytes);
					}
				}
			}
			Color contentColor = GUI.contentColor;
			GUI.contentColor = red;
			GUI.Label(new Rect((float)fs, r2.y, w - num5 * 4f - (float)fs * 6f, rowH), text2, BtcShareUI.sLabel);
			GUI.contentColor = contentColor;
			float num6 = w - (float)fs;
			float y = r2.y + rowH * 0.1f;
			float height = rowH * 0.8f;
			num6 -= num5;
			bool flag17 = flag14 && !flag13;
			if (flag17)
			{
				string b = "n:" + entry.btcs;
				bool flag18 = BtcShareUI.confirmKey == b && Time.unscaledTime < BtcShareUI.confirmUntil;
				string text3 = flag18 ? BtcShareUI.T("Overwrite?", "Перезаписать?") : (entry.isMap ? BtcShareUI.T("To normal map", "В обычную карту") : BtcShareUI.T("To normal save", "В обычный сейв"));
				bool flag19 = BtcShareUI.Button(new Rect(num6, y, num5, height), text3, flag18 ? BtcShareUI.Red : Color.white);
				if (flag19)
				{
					bool flag20 = !entry.hasFolder || flag18;
					if (flag20)
					{
						BtcShareUI.confirmKey = null;
						BtcShareUI.StartNormal(entry);
					}
					else
					{
						BtcShareUI.confirmKey = b;
						BtcShareUI.confirmUntil = Time.unscaledTime + 3f;
					}
				}
			}
			num6 -= num5 + (float)fs * 0.3f;
			int num7 = flag14 ? BtcReplayShare.State(entry.btcs) : 0;
			bool flag21 = flag14 && !flag13;
			if (flag21)
			{
				Rect r3 = new Rect(num6, y, num5, height);
				bool flag22 = num7 == 1;
				if (flag22)
				{
					BtcShareUI.Button(r3, BtcReplayShare.UiUploading(), new Color(1f, 1f, 1f, 0.5f));
				}
				else
				{
					bool flag23 = num7 == 2;
					if (flag23)
					{
						BtcShareUI.Button(r3, BtcReplayShare.UiCreate(), new Color(1f, 1f, 1f, 0.35f));
					}
					else
					{
						bool flag24 = BtcShareUI.Button(r3, (num7 == 3) ? BtcReplayShare.UiRetry() : BtcReplayShare.UiCreate(), Color.white);
						if (flag24)
						{
							BtcReplayShare.Create(entry.btcs);
						}
					}
				}
			}
			num6 -= num5 + (float)fs * 0.3f;
			bool flag25 = flag14 && !flag13;
			if (flag25)
			{
				bool flag26 = BtcReplayShare.FileCopied(entry.btcs);
				bool flag27 = BtcShareUI.Button(new Rect(num6, y, num5, height), BtcReplayShare.UiCopyFile(flag26), flag26 ? BtcShareUI.Green : Color.white);
				if (flag27)
				{
					BtcReplayShare.CopyFile(entry.btcs);
				}
			}
			num6 -= num5 + (float)fs * 0.3f;
			bool flag28 = entry.hasFolder && !flag13;
			if (flag28)
			{
				bool flag29 = BtcShareUI.Button(new Rect(num6, y, num5, height), entry.hasBtcs ? BtcShareUI.T("Re-pack", "Пересжать") : BtcShareUI.T("Pack to .btcs", "Сжать в .btcs"), Color.white);
				if (flag29)
				{
					BtcShareUI.StartPack(entry);
				}
			}
			bool flag30 = num7 == 2 || num7 == 3;
			if (flag30)
			{
				float num8 = rowH * 0.9f;
				Rect r4 = new Rect(0f, num, w, num8);
				num += num8;
				BtcShareUI.Fill(r4, (i % 2 == 0) ? new Color(1f, 1f, 1f, 0.03f) : new Color(1f, 1f, 1f, 0f));
				bool flag31 = num7 == 2;
				if (flag31)
				{
					string text4 = BtcReplayShare.Url(entry.btcs) + (BtcReplayShare.IsTemp(entry.btcs) ? BtcReplayShare.UiTempNote() : string.Empty);
					GUI.Label(new Rect((float)fs * 2f, r4.y, w - num5 * 1.25f - (float)fs * 6f, num8), text4, BtcShareUI.sLabel);
					bool flag32 = BtcReplayShare.Copied(entry.btcs);
					bool flag33 = BtcShareUI.Button(new Rect(w - num5 * 1.25f - (float)fs, r4.y + num8 * 0.1f, num5 * 1.25f, num8 * 0.8f), BtcReplayShare.UiCopy(flag32), flag32 ? BtcShareUI.Green : Color.white);
					if (flag33)
					{
						BtcReplayShare.Copy(entry.btcs);
					}
				}
				else
				{
					GUI.Label(new Rect((float)fs * 2f, r4.y, w - (float)fs * 4f, num8), BtcReplayShare.UiFailed(BtcReplayShare.Error(entry.btcs)), BtcShareUI.sLabel);
				}
			}
		}
		BtcShareUI.ContentH = num + rowH * 0.5f;
	}

	public static bool TabActive;

	public static float ContentH;

	internal static readonly string[] RootPaths = new string[]
	{
		"Info/Saves",
		"Info/UGC/Saves",
		"Info/Downloads/Saves",
		"Info/Maps",
		"Info/UGC/Maps",
		"Info/Downloads/Maps"
	};

	internal static List<BtcShareUI.Entry> list = new List<BtcShareUI.Entry>();

	internal static bool listLoaded;

	internal static string msg;

	internal static float msgUntil;

	internal static string confirmKey;

	internal static float confirmUntil;

	internal static string importUrl = string.Empty;

	internal static WWW dl;

	internal static Texture2D white;

	internal static GUIStyle sRow;

	internal static GUIStyle sLabel;

	internal static GUIStyle sNote;

	internal static GUIStyle sHead;

	internal static GUIStyle sBtn;

	internal static GUIStyle sVal;

	internal static GUIStyle sField;

	internal static int styleFs;

	internal static Color Green { get { return BtcMenu.Accent; } }

	internal static readonly Color Red = new Color(1f, 0.45f, 0.4f, 1f);

	internal static bool settingsDirty;

	internal sealed class Entry
	{
		public string name;

		public string folder;

		public string btcs;

		public string origin;

		public bool isMap;

		public bool hasFolder;

		public bool hasBtcs;

		public long btcsBytes;

		public string err;

		public volatile int job;

		public string jobKind;

		public string jobMsg;
	}
}
}
