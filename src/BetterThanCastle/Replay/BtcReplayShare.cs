using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace BetterThanCastle
{
public static class BtcReplayShare
{
	private static string T(string en, string ru)
	{
		return BtcLang.Russian ? ru : en;
	}

	private static string Key(string path)
	{
		return Path.GetFileName(path);
	}

	private static void Load()
	{
		bool flag = BtcReplayShare.loaded;
		bool flag2 = !flag;
		bool flag3 = flag2;
		if (flag3)
		{
			BtcReplayShare.loaded = true;
			try
			{
				BtcPaths.Init();
				string path = BtcPaths.Config("replay_links.txt");
				bool flag4 = !File.Exists(path);
				bool flag5 = !flag4;
				bool flag6 = flag5;
				if (flag6)
				{
					string[] array = File.ReadAllLines(path);
					for (int i = 0; i < array.Length; i++)
					{
						string[] array2 = array[i].Split(new char[]
						{
							'|'
						});
						bool flag7 = array2.Length >= 2 && array2[0].Length > 0 && array2[1].StartsWith("http");
						bool flag8 = flag7;
						bool flag9 = flag8;
						if (flag9)
						{
							BtcReplayShare.Item item = new BtcReplayShare.Item();
							item.url = array2[1];
							item.temp = (array2.Length >= 3 && array2[2] == "1");
							BtcReplayShare.items[array2[0]] = item;
						}
					}
				}
			}
			catch (Exception)
			{
			}
		}
	}

	private static void Save()
	{
		try
		{
			BtcPaths.Init();
			List<string> list = new List<string>();
			foreach (KeyValuePair<string, BtcReplayShare.Item> keyValuePair in BtcReplayShare.items)
			{
				bool flag = !string.IsNullOrEmpty(keyValuePair.Value.url);
				bool flag2 = flag;
				bool flag3 = flag2;
				if (flag3)
				{
					list.Add(string.Concat(new string[]
					{
						keyValuePair.Key,
						"|",
						keyValuePair.Value.url,
						"|",
						keyValuePair.Value.temp ? "1" : "0"
					}));
				}
			}
			File.WriteAllLines(BtcPaths.Config("replay_links.txt"), list.ToArray());
		}
		catch (Exception)
		{
		}
	}

	private static BtcReplayShare.Item Get(string path, bool create)
	{
		BtcReplayShare.Load();
		string key = BtcReplayShare.Key(path);
		BtcReplayShare.Item item;
		bool flag = !BtcReplayShare.items.TryGetValue(key, out item) && create;
		bool flag2 = flag;
		bool flag3 = flag2;
		if (flag3)
		{
			item = new BtcReplayShare.Item();
			BtcReplayShare.items[key] = item;
		}
		return item;
	}

	public static int State(string path)
	{
		BtcReplayShare.Item item = BtcReplayShare.Get(path, false);
		bool flag = item == null;
		bool flag2 = flag;
		bool flag3 = flag2;
		int result;
		if (flag3)
		{
			result = 0;
		}
		else
		{
			bool flag4 = item.www != null;
			bool flag5 = flag4;
			bool flag6 = flag5;
			if (flag6)
			{
				result = 1;
			}
			else
			{
				bool flag7 = !string.IsNullOrEmpty(item.url);
				bool flag8 = flag7;
				bool flag9 = flag8;
				if (flag9)
				{
					result = 2;
				}
				else
				{
					result = (string.IsNullOrEmpty(item.err) ? 0 : 3);
				}
			}
		}
		return result;
	}

	public static string Url(string path)
	{
		BtcReplayShare.Item item = BtcReplayShare.Get(path, false);
		return (item != null && item.url != null) ? item.url : string.Empty;
	}

	public static string Error(string path)
	{
		BtcReplayShare.Item item = BtcReplayShare.Get(path, false);
		return (item != null && item.err != null) ? item.err : string.Empty;
	}

	public static bool IsTemp(string path)
	{
		BtcReplayShare.Item item = BtcReplayShare.Get(path, false);
		return item != null && item.temp;
	}

	public static bool Copied(string path)
	{
		BtcReplayShare.Item item = BtcReplayShare.Get(path, false);
		return item != null && Time.unscaledTime < item.copiedUntil;
	}

	public static void Copy(string path)
	{
		BtcReplayShare.Item item = BtcReplayShare.Get(path, false);
		bool flag = item == null || string.IsNullOrEmpty(item.url);
		bool flag2 = !flag;
		bool flag3 = flag2;
		if (flag3)
		{
			GUIUtility.systemCopyBuffer = item.url;
			item.copiedUntil = Time.unscaledTime + 2f;
		}
	}

	public static void Create(string path)
	{
		BtcReplayShare.Item item = BtcReplayShare.Get(path, true);
		bool flag = item.www != null;
		bool flag2 = !flag;
		bool flag3 = flag2;
		if (flag3)
		{
			item.url = null;
			item.err = null;
			item.firstErr = null;
			item.log = null;
			item.stage = 0;
			item.temp = false;
			BtcReplayShare.Start(path, item);
		}
	}

	private static void Start(string path, BtcReplayShare.Item it)
	{
		try
		{
			byte[] array = BtcReplayShare.MakeZip(path);
			WWWForm wwwform = new WWWForm();
			it.temp = (it.stage == 1);
			bool flag = it.stage == 2;
			bool flag2 = flag;
			string text;
			if (flag2)
			{
				text = "https://upload.gofile.io/uploadfile";
				wwwform.AddBinaryData("file", array, Path.GetFileNameWithoutExtension(path) + ".zip", "application/zip");
			}
			else
			{
				wwwform.AddField("reqtype", "fileupload");
				text = "https://catbox.moe/user/api.php";
				bool temp = it.temp;
				bool flag3 = temp;
				if (flag3)
				{
					wwwform.AddField("time", "72h");
					text = "https://litterbox.catbox.moe/resources/internals/api.php";
				}
				wwwform.AddBinaryData("fileToUpload", array, Path.GetFileNameWithoutExtension(path) + ".zip", "application/zip");
			}
			it.www = new WWW(text, wwwform);
			BtcReplayShare.active++;
			BtcReplayShare.Log(string.Concat(new string[]
			{
				"START ",
				BtcReplayShare.HostName(it.stage),
				" ",
				text,
				" file=",
				Path.GetFileName(path),
				" size=",
				array.Length.ToString(),
				" B, upload name=",
				Path.GetFileNameWithoutExtension(path),
				".zip"
			}));
		}
		catch (Exception ex)
		{
			it.www = null;
			it.err = ex.Message;
			BtcReplayShare.Log("START EXCEPTION " + ex);
		}
	}

	public static void Poll()
	{
		bool flag = BtcReplayShare.active <= 0;
		bool flag2 = !flag;
		bool flag3 = flag2;
		if (flag3)
		{
			BtcReplayShare.keyBuf.Clear();
			foreach (KeyValuePair<string, BtcReplayShare.Item> keyValuePair in BtcReplayShare.items)
			{
				bool flag4 = keyValuePair.Value.www != null && keyValuePair.Value.www.isDone;
				bool flag5 = flag4;
				bool flag6 = flag5;
				if (flag6)
				{
					BtcReplayShare.keyBuf.Add(keyValuePair.Key);
				}
			}
			for (int i = 0; i < BtcReplayShare.keyBuf.Count; i++)
			{
				string text = BtcReplayShare.keyBuf[i];
				BtcReplayShare.Item item = BtcReplayShare.items[text];
				WWW www = item.www;
				item.www = null;
				BtcReplayShare.active--;
				string text2 = string.IsNullOrEmpty(www.error) ? (www.text ?? string.Empty).Trim() : string.Empty;
				string error = www.error;
				string text3 = (www.text ?? string.Empty).Trim();
				bool flag7 = text3.Length > 40;
				bool flag8 = flag7;
				bool flag9 = flag8;
				if (flag9)
				{
					text3 = text3.Substring(0, 40);
				}
				string text4 = (string.IsNullOrEmpty(error) ? "bad response" : error) + ((text3.Length > 0) ? (" [" + text3 + "]") : string.Empty);
				string text5 = string.Empty;
				string text6 = string.Empty;
				string text7 = string.Empty;
				int num = 0;
				try
				{
					text7 = (www.text ?? string.Empty);
					bool flag10 = text7.Length > 3000;
					bool flag11 = flag10;
					if (flag11)
					{
						text7 = text7.Substring(0, 3000) + "...[cut]";
					}
					num = ((www.bytes != null) ? www.bytes.Length : 0);
					IDictionary<string, string> responseHeaders = www.responseHeaders;
					bool flag12 = responseHeaders != null;
					bool flag13 = flag12;
					if (flag13)
					{
						foreach (KeyValuePair<string, string> keyValuePair2 in responseHeaders)
						{
							text6 = string.Concat(new string[]
							{
								text6,
								"\r\n    ",
								keyValuePair2.Key,
								": ",
								keyValuePair2.Value
							});
							bool flag14 = keyValuePair2.Key == "STATUS";
							bool flag15 = flag14;
							if (flag15)
							{
								text5 = keyValuePair2.Value;
							}
						}
					}
				}
				catch (Exception)
				{
				}
				BtcReplayShare.Log(string.Concat(new string[]
				{
					"RESULT ",
					BtcReplayShare.HostName(item.stage),
					" (",
					text,
					") status=[",
					text5,
					"] error=[",
					error ?? string.Empty,
					"] bytes=",
					num.ToString(),
					"\r\n  headers:",
					text6,
					"\r\n  body: ",
					text7,
					"\r\n"
				}));
				www.Dispose();
				bool flag16 = item.stage == 2 && string.IsNullOrEmpty(error);
				bool flag17 = flag16;
				if (flag17)
				{
					int num2 = (www == null) ? -1 : text7.IndexOf("\"downloadPage\":\"");
					bool flag18 = num2 >= 0;
					bool flag19 = flag18;
					if (flag19)
					{
						num2 += "\"downloadPage\":\"".Length;
						int num3 = text7.IndexOf('"', num2);
						bool flag20 = num3 > num2;
						bool flag21 = flag20;
						if (flag21)
						{
							text2 = text7.Substring(num2, num3 - num2).Replace("\\/", "/");
						}
					}
				}
				bool flag22 = text2.StartsWith("https://");
				bool flag23 = flag22;
				bool flag24 = flag23;
				if (flag24)
				{
					item.url = text2;
					BtcReplayShare.Log("OK " + text2);
					item.err = null;
					BtcReplayShare.Save();
				}
				else
				{
					bool flag25 = item.stage < 2;
					bool flag26 = flag25;
					bool flag27 = flag26;
					if (flag27)
					{
						item.log = ((item.log == null) ? string.Empty : (item.log + " | ")) + BtcReplayShare.HostName(item.stage) + ": " + text4;
						item.stage++;
						BtcReplayShare.Start(BtcReplayShare.FullPath(text), item);
					}
					else
					{
						BtcReplayShare.Log(string.Concat(new string[]
						{
							"FAILED all hosts: ",
							item.log,
							" | ",
							BtcReplayShare.HostName(item.stage),
							": ",
							text4
						}));
						item.err = string.Concat(new string[]
						{
							item.log,
							" | ",
							BtcReplayShare.HostName(item.stage),
							": ",
							text4
						});
					}
				}
			}
		}
	}

	private static string FullPath(string fileName)
	{
		string result;
		try
		{
			result = Path.Combine(BtcReplay.ReplaysDir, fileName);
		}
		catch (Exception)
		{
			result = fileName;
		}
		return result;
	}

	public static string UiCreate()
	{
		return BtcReplayShare.T("Create Link", "Создать ссылку");
	}

	public static string UiUploading()
	{
		return BtcReplayShare.T("Uploading...", "Загрузка...");
	}

	public static string UiRetry()
	{
		return BtcReplayShare.T("Retry", "Повторить");
	}

	public static string UiCopy(bool copied)
	{
		return copied ? BtcReplayShare.T("Copied!", "Скопировано!") : BtcReplayShare.T("Copy Link", "Копировать ссылку");
	}

	public static string UiTempNote()
	{
		return BtcReplayShare.T("  (expires in 72 h)", "  (живёт 72 ч)");
	}

	public static string UiFailed(string err)
	{
		return BtcReplayShare.T("Upload failed: ", "Не удалось загрузить: ") + err;
	}

	private static string HostName(int stage)
	{
		return (stage == 0) ? "catbox" : ((stage == 1) ? "litterbox" : "gofile");
	}

	public static int ImportZips()
	{
		int num = 0;
		try
		{
			string replaysDir = BtcReplay.ReplaysDir;
			bool flag = !Directory.Exists(replaysDir);
			bool flag2 = flag;
			if (flag2)
			{
				return 0;
			}
			string[] files = Directory.GetFiles(replaysDir, "*.zip");
			for (int i = 0; i < files.Length; i++)
			{
				try
				{
					bool flag3 = BtcReplayShare.ImportOne(files[i], replaysDir);
					bool flag4 = flag3;
					if (flag4)
					{
						num++;
					}
				}
				catch (Exception)
				{
				}
			}
		}
		catch (Exception)
		{
		}
		return num;
	}

	private static bool IsBtcr(string path)
	{
		byte[] array = new byte[5];
		using (FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
		{
			bool flag = fileStream.Read(array, 0, 5) < 5;
			bool flag2 = flag;
			if (flag2)
			{
				return false;
			}
		}
		return array[0] == 66 && array[1] == 84 && array[2] == 67 && array[3] == 82;
	}

	private static string UniqueBtcr(string dir, string baseName)
	{
		string text = Path.Combine(dir, baseName + ".btcr");
		int num = 1;
		while (File.Exists(text))
		{
			text = Path.Combine(dir, baseName + "_" + num.ToString() + ".btcr");
			num++;
		}
		return text;
	}

	private static bool ImportOne(string zip, string dir)
	{
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(zip);
		bool flag = BtcReplayShare.IsBtcr(zip);
		bool flag2 = flag;
		bool result;
		if (flag2)
		{
			File.Move(zip, BtcReplayShare.UniqueBtcr(dir, fileNameWithoutExtension));
			result = true;
		}
		else
		{
			byte[] array = new byte[2];
			using (FileStream fileStream = new FileStream(zip, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
			{
				bool flag3 = fileStream.Read(array, 0, 2) < 2;
				bool flag4 = flag3;
				if (flag4)
				{
					return false;
				}
			}
			bool flag5 = array[0] != 80 || array[1] != 75;
			bool flag6 = flag5;
			if (flag6)
			{
				result = false;
			}
			else
			{
				string text = Path.Combine(Path.GetTempPath(), "btcr_import_" + DateTime.Now.Ticks.ToString());
				Directory.CreateDirectory(text);
				bool flag7 = false;
				try
				{
					int[] progress = new int[]
					{
						10
					};
					lzip.decompress_File(zip, text, progress, null, null, null);
					string[] files = Directory.GetFiles(text, "*.btcr", SearchOption.AllDirectories);
					for (int i = 0; i < files.Length; i++)
					{
						bool flag8 = BtcReplayShare.IsBtcr(files[i]);
						bool flag9 = flag8;
						if (flag9)
						{
							File.Copy(files[i], BtcReplayShare.UniqueBtcr(dir, Path.GetFileNameWithoutExtension(files[i])));
							flag7 = true;
						}
					}
				}
				finally
				{
					try
					{
						Directory.Delete(text, true);
					}
					catch (Exception)
					{
					}
				}
				bool flag10 = flag7;
				bool flag11 = flag10;
				if (flag11)
				{
					File.Delete(zip);
				}
				result = flag7;
			}
		}
		return result;
	}

	private static void Log(string msg)
	{
		try
		{
			BtcPaths.Init();
			File.AppendAllText(BtcPaths.DebugFile("replay_share_log.txt"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + "\r\n");
		}
		catch (Exception)
		{
		}
	}

	private static byte[] MakeZip(string path)
	{
		string text = Path.Combine(Path.GetTempPath(), "btcr_share_" + DateTime.Now.Ticks.ToString() + ".zip");
		try
		{
			lzip.compress_File(0, text, path, false, Path.GetFileName(path), null, null, false);
			bool flag = File.Exists(text);
			bool flag2 = flag;
			if (flag2)
			{
				byte[] array = File.ReadAllBytes(text);
				bool flag3 = array.Length > 22 && array[0] == 80 && array[1] == 75;
				bool flag4 = flag3;
				if (flag4)
				{
					BtcReplayShare.Log("zip packed: " + array.Length.ToString() + " B");
					return array;
				}
			}
			BtcReplayShare.Log("zip pack produced no valid zip, uploading raw");
		}
		catch (Exception ex)
		{
			BtcReplayShare.Log("zip pack failed: " + ex.Message + ", uploading raw");
		}
		finally
		{
			try
			{
				bool flag5 = File.Exists(text);
				bool flag6 = flag5;
				if (flag6)
				{
					File.Delete(text);
				}
			}
			catch (Exception)
			{
			}
		}
		return File.ReadAllBytes(path);
	}

	public static void CopyFile(string path)
	{
		try
		{
			bool flag = string.IsNullOrEmpty(path) || !File.Exists(path);
			bool flag2 = !flag;
			if (flag2)
			{
				string text = Path.GetFullPath(path).Replace("'", "''");
				Process.Start(new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -WindowStyle Hidden -Command \"Set-Clipboard -LiteralPath '" + text + "'\"")
				{
					UseShellExecute = false,
					CreateNoWindow = true
				});
				BtcReplayShare.Item item = BtcReplayShare.Get(path, true);
				item.fileCopiedUntil = Time.unscaledTime + 2f;
				BtcReplayShare.Log(string.Concat(new string[]
				{
					"COPY FILE ",
					text,
					" size=",
					new FileInfo(path).Length.ToString(),
					" B"
				}));
			}
		}
		catch (Exception arg)
		{
			BtcReplayShare.Log("COPY FILE failed: " + arg);
		}
	}

	public static bool FileCopied(string path)
	{
		BtcReplayShare.Item item = BtcReplayShare.Get(path, false);
		return item != null && Time.unscaledTime < item.fileCopiedUntil;
	}

	public static string UiCopyFile(bool copied)
	{
		return copied ? BtcReplayShare.T("Copied!", "Скопировано!") : BtcReplayShare.T("Copy File", "Копировать файл");
	}

	private static readonly Dictionary<string, BtcReplayShare.Item> items = new Dictionary<string, BtcReplayShare.Item>();

	private static readonly List<string> keyBuf = new List<string>();

	private static bool loaded;

	private static int active;

	private class Item
	{
		public string url;

		public string err;

		public bool temp;

		public WWW www;

		public float copiedUntil;

		public string firstErr;

		public int stage;

		public string log;

		public float fileCopiedUntil;
	}
}
}
