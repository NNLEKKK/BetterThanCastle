using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
public static class BtcLoadBoost
{
	public static WaitForSeconds Poll()
	{
		return new WaitForSeconds(BtcLoadBoost.Enabled ? BtcLoadBoost.PollSeconds : 1f);
	}

	public static void Begin()
	{
		BtcGc.Resume("new load");
		BtcLoadBoost.Loading = true;
		BtcLoadBoost.RefreshTreeCalls = 0;
		BtcLoadBoost.RefreshTreeMs = 0.0;
		BtcLoadBoost._t0 = Stopwatch.GetTimestamp();
		BtcLoadBoost._last = BtcLoadBoost._t0;
		BtcLoadBoost._started = true;
		BtcGc.Pause("load begin");
		BtcLoadBoost.Write(string.Concat(new string[]
		{
			"---- load begin (",
			NetworkServer.active ? "host" : "client",
			", boost=",
			BtcLoadBoost.Enabled.ToString(),
			") ----"
		}));
	}

	public static void Mark(string phase)
	{
		bool flag = phase == "StartStep done";
		bool flag2 = flag;
		if (flag2)
		{
			BtcLoadBoost.Loading = false;
			BtcGc.Resume("StartStep done");
		}
		else
		{
			bool flag3 = !BtcLoadBoost._started;
			if (flag3)
			{
				BtcLoadBoost.Begin();
			}
		}
		long timestamp = Stopwatch.GetTimestamp();
		double num = (double)(timestamp - BtcLoadBoost._t0) / (double)Stopwatch.Frequency;
		double num2 = (double)(timestamp - BtcLoadBoost._last) / (double)Stopwatch.Frequency;
		BtcLoadBoost._last = timestamp;
		BtcLoadBoost.Write(string.Format("{0,8:F2}s  +{1,7:F2}s  {2}{3}", new object[]
		{
			num,
			num2,
			phase,
			BtcLoadBoost.GcInfo()
		}));
		bool flag4 = flag;
		if (flag4)
		{
			BtcLoadBoost._started = false;
		}
	}

	private static void Write(string line)
	{
		string message = "[BtcLoad] " + line;
		UnityEngine.Debug.Log(message);
		try
		{
			string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), "BtcLoadProfile.log");
			File.AppendAllText(path, DateTime.Now.ToString("HH:mm:ss") + " " + line + "\r\n");
		}
		catch (Exception)
		{
		}
	}

	public static string MapChecksum(string path)
	{
		bool flag = !BtcLoadBoost.Enabled || !BtcLoadBoost.FastMapHash;
		bool flag2 = flag;
		string result;
		if (flag2)
		{
			result = Legitimizer.GenerateOneHashForAllFilesIn(path).hash;
		}
		else
		{
			try
			{
				string[] files = Directory.GetFiles(path);
				Array.Sort<string>(files, StringComparer.OrdinalIgnoreCase);
				StringBuilder stringBuilder = new StringBuilder(files.Length * 48 + 16);
				for (int i = 0; i < files.Length; i++)
				{
					FileInfo fileInfo = new FileInfo(files[i]);
					stringBuilder.Append(fileInfo.Name).Append('|').Append(fileInfo.Length).Append('|').Append(fileInfo.LastWriteTimeUtc.Ticks).Append(';');
				}
				string text = stringBuilder.ToString();
				string a = null;
				string text2 = null;
				bool flag3 = BtcLoadBoost._sigCache.TryGetValue(path, out a) && a == text && BtcLoadBoost._hashCache.TryGetValue(path, out text2);
				bool flag4 = flag3 && text2 != null;
				bool flag5 = flag4;
				if (flag5)
				{
					result = text2;
				}
				else
				{
					byte[] array = MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(text));
					StringBuilder stringBuilder2 = new StringBuilder(32);
					for (int j = 0; j < array.Length; j++)
					{
						stringBuilder2.Append(array[j].ToString("X2"));
					}
					string text3 = stringBuilder2.ToString();
					BtcLoadBoost._sigCache[path] = text;
					BtcLoadBoost._hashCache[path] = text3;
					result = text3;
				}
			}
			catch (Exception ex)
			{
				BtcLoadBoost.Write("MapChecksum fallback: " + ex.Message);
				result = Legitimizer.GenerateOneHashForAllFilesIn(path).hash;
			}
		}
		return result;
	}

	static BtcLoadBoost()
	{
		BtcLoadBoost._sigCache = new Dictionary<string, string>();
		BtcLoadBoost._hashCache = new Dictionary<string, string>();
	}

	public static void Note(string s)
	{
		double num = (double)(Stopwatch.GetTimestamp() - BtcLoadBoost._t0) / (double)Stopwatch.Frequency;
		BtcLoadBoost.Write(string.Format("{0,8:F2}s  {1}", num, s));
	}

	public static bool MenuSliceOn()
	{
		return BtcLoadBoost.Enabled && BtcLoadBoost.Loading && BtcLoadBoost.MenuBudgetMs > 0f;
	}

	public static double ElapsedMs(long t0)
	{
		return (double)(Stopwatch.GetTimestamp() - t0) * 1000.0 / (double)Stopwatch.Frequency;
	}

	public static string GcInfo()
	{
		return string.Concat(new string[]
		{
			" gc0=",
			GC.CollectionCount(0).ToString(),
			" heap=",
			(GC.GetTotalMemory(false) / 1048576L).ToString(),
			"MB"
		});
	}

	public static bool Enabled = true;

	public static bool FastMapHash = true;

	public static float FrameBudgetMs = 250f;

	public static float PollSeconds = 0.1f;

	private static long _t0;

	private static long _last;

	private static bool _started;

	private static readonly Dictionary<string, string> _sigCache;

	private static readonly Dictionary<string, string> _hashCache;

	public static bool Loading;

	public static float MenuBudgetMs = 100000f;

	public static int RefreshTreeCalls;

	public static double RefreshTreeMs;

	public sealed class Slicer
	{
		public Slicer()
		{
			this._t0 = Stopwatch.GetTimestamp();
		}

		public bool Due()
		{
			this._n++;
			bool flag = !BtcLoadBoost.Enabled;
			bool flag2 = flag;
			bool result;
			if (flag2)
			{
				bool flag3 = this._n >= 10000;
				bool flag4 = flag3;
				if (flag4)
				{
					this._n = 0;
					result = true;
				}
				else
				{
					result = false;
				}
			}
			else
			{
				bool flag5 = (this._n & 15) != 0;
				bool flag6 = flag5;
				if (flag6)
				{
					result = false;
				}
				else
				{
					long num = (long)((double)BtcLoadBoost.FrameBudgetMs * 0.001 * (double)Stopwatch.Frequency);
					result = (Stopwatch.GetTimestamp() - this._t0 >= num);
				}
			}
			return result;
		}

		public void Restart()
		{
			this._t0 = Stopwatch.GetTimestamp();
			this._n = 0;
		}

		private long _t0;

		private int _n;
	}
}
}
