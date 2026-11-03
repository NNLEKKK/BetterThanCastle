using System;
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
