using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BetterThanCastle
{
public static class BtcPaths
{
	public static string GameRoot
	{
		get
		{
			return Directory.GetParent(Application.dataPath).FullName;
		}
	}

	public static string Root
	{
		get
		{
			return Path.Combine(BtcPaths.GameRoot, "BetterThanCastle");
		}
	}

	public static string Packs
	{
		get
		{
			return Path.Combine(BtcPaths.Root, "ResourcePacks");
		}
	}

	public static string Configs
	{
		get
		{
			return Path.Combine(BtcPaths.Root, "configs");
		}
	}

	public static string Debug
	{
		get
		{
#if DEV
			return BetterThanCastle.Dev.BtcDevPaths.LogDir;   // DEV: all mod logs land in <repo>/BTClogs
#else
			return Path.Combine(BtcPaths.Root, "debug");
#endif
		}
	}

	public static string DumpDir
	{
		get
		{
			return Path.Combine(BtcPaths.Debug, "dump");
		}
	}

	public static string Config(string fileName)
	{
		return Path.Combine(BtcPaths.Configs, fileName);
	}

	public static string DebugFile(string fileName)
	{
		return Path.Combine(BtcPaths.Debug, fileName);
	}

	private const long LogTrimAbove = 8L * 1024 * 1024;
	private const int LogKeepTail = 2 * 1024 * 1024;

	/// <summary>
	/// Once per start: every debug log (BetterThanCastle/debug/*.txt|*.log, qol_blocked.log) bigger than 8 MB is cut to its
	/// last 2 MB. The append-only legacy logs grew to several GB on players' disks (unet_netdiag_client.log 3.3 GB).
	/// </summary>
	private static void TrimLogs()
	{
		try
		{
			List<string> files = new List<string>();
			if (Directory.Exists(BtcPaths.Debug))
			{
				files.AddRange(Directory.GetFiles(BtcPaths.Debug, "*.txt"));
				files.AddRange(Directory.GetFiles(BtcPaths.Debug, "*.log"));
			}
			files.Add(Path.Combine(BtcPaths.Root, "qol_blocked.log"));
			for (int i = 0; i < files.Count; i++)
			{
				try
				{
					FileInfo fi = new FileInfo(files[i]);
					if (!fi.Exists || fi.Length <= LogTrimAbove) continue;
					byte[] tail = new byte[LogKeepTail];
					using (FileStream fs = new FileStream(fi.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
					{
						fs.Seek(-LogKeepTail, SeekOrigin.End);
						int got = 0;
						while (got < tail.Length) { int n = fs.Read(tail, got, tail.Length - got); if (n <= 0) break; got += n; }
					}
					int start = Array.IndexOf(tail, (byte)'\n') + 1;   // begin at a whole line
					using (FileStream fw = new FileStream(fi.FullName, FileMode.Create, FileAccess.Write))
						fw.Write(tail, start, tail.Length - start);
					BtcLog.Info("Paths", "log " + fi.Name + " trimmed from " + (fi.Length >> 20) + " MB to 2 MB");
				}
				catch (Exception e) { BtcLog.Warn("Paths", "trim " + files[i] + ": " + e.Message); }
			}
		}
		catch (Exception e) { BtcLog.Warn("Paths", "log trim: " + e.Message); }
	}

	public static void Init()
	{
		bool flag = BtcPaths.inited;
		bool flag2 = !flag;
		if (flag2)
		{
			BtcPaths.inited = true;
			try
			{
				string text = Path.Combine(BtcPaths.GameRoot, "ResourcePacks");
				bool flag3 = Directory.Exists(BtcPaths.Root);
				Directory.CreateDirectory(BtcPaths.Root);
				bool flag4 = !Directory.Exists(BtcPaths.Packs) && Directory.Exists(text);
				bool flag5 = flag4;
				if (flag5)
				{
					Directory.Move(text, BtcPaths.Packs);
				}
				Directory.CreateDirectory(BtcPaths.Packs);
				Directory.CreateDirectory(BtcPaths.Configs);
				Directory.CreateDirectory(BtcPaths.Debug);
				string[] array = new string[]
				{
					"selected.txt",
					"options.txt",
					"coloredhp.txt",
					"hats.txt",
					"performance.txt",
					"betterconnection.txt"
				};
				for (int i = 0; i < array.Length; i++)
				{
					BtcPaths.MoveFile(Path.Combine(BtcPaths.Packs, array[i]), BtcPaths.Config(array[i]));
				}
				BtcPaths.MoveFile(Path.Combine(BtcPaths.Packs, "_log.txt"), BtcPaths.DebugFile("mod_log.txt"));
				BtcPaths.MoveFile(Path.Combine(BtcPaths.GameRoot, "hotkeys.cfg"), BtcPaths.Config("hotkeys.txt"));
				BtcPaths.MoveFile(Path.Combine(BtcPaths.GameRoot, "betterconnection_netlog.txt"), BtcPaths.DebugFile("betterconnection_netlog.txt"));
				BtcPaths.TrimLogs();
				string text2 = Path.Combine(BtcPaths.Packs, "_dump");
				bool flag6 = Directory.Exists(text2) && !Directory.Exists(BtcPaths.DumpDir);
				bool flag7 = flag6;
				if (flag7)
				{
					Directory.Move(text2, BtcPaths.DumpDir);
				}
			}
			catch (Exception ex)
			{
				BtcPaths.Debug_Warn("BtcPaths.Init failed: " + ex.Message);
			}
		}
	}

	private static void MoveFile(string from, string to)
	{
		try
		{
			bool flag = File.Exists(from) && !File.Exists(to);
			bool flag2 = flag;
			if (flag2)
			{
				File.Move(from, to);
			}
		}
		catch (Exception)
		{
		}
	}

	private static void Debug_Warn(string msg)
	{
		UnityEngine.Debug.LogWarning("[BetterThanCastle] " + msg);
	}

	public static string ReadKey(string fileName, string key, string def)
	{
		try
		{
			string path = BtcPaths.Config(fileName);
			bool flag = !File.Exists(path);
			bool flag2 = flag;
			if (flag2)
			{
				return def;
			}
			string[] array = File.ReadAllLines(path);
			for (int i = 0; i < array.Length; i++)
			{
				string text = array[i].Trim();
				bool flag3 = text.Length == 0 || text.StartsWith("#");
				bool flag4 = !flag3;
				if (flag4)
				{
					int num = text.IndexOf('=');
					bool flag5 = num > 0 && string.Equals(text.Substring(0, num).Trim(), key, StringComparison.OrdinalIgnoreCase);
					bool flag6 = flag5;
					if (flag6)
					{
						return text.Substring(num + 1).Trim();
					}
				}
			}
		}
		catch (Exception)
		{
		}
		return def;
	}

	public const string ModName = "Better Than Castle";

	public const string ModAuthor = "NotNonlokill";

	public const string ModVersion = BtcPlugin.Version;

	private static bool inited;
}
}
