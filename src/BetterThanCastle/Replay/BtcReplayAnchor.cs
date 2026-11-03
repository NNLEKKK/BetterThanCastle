using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using Brix.Components;
using Brix.Engine;
using Brix.Engine.Abstract;
using Brix.Engine.Nature;
using Brix.Legacy;
using Brix.Lifecycle.Pooling;
using UnityEngine;

namespace BetterThanCastle
{
public static class BtcReplayAnchor
{
	/// <summary>Called once when a forward seek (not a rewind) reaches its target. Visual reconcile after fast-forward (trees etc.).</summary>
	public static void AfterSeekForward()
	{
		try
		{
			Stopwatch sw = Stopwatch.StartNew();
			int fixedCut = 0;
			int stale = 0;
			int chunks = 0;
			List<XYZ> pos = new List<XYZ>();
			List<DamagedTree> skipped = BtcTreeCutSkipPatch.Skipped;
			for (int i = 0; i < skipped.Count; i++)
			{
				DamagedTree dt = skipped[i];
				if (dt == null) continue; // destroyed Unity object
				XYZ p = XYZ.FromVector3(dt.transform.position);
				try
				{
					if (BtcTreeCutSkipPatch.PosField != null)
					{
						XYZ q = (XYZ)BtcTreeCutSkipPatch.PosField.GetValue(dt);
						if (q != XYZ.zero) p = q;
					}
				}
				catch { }
				DamagedTree cur;
				if (DamagedTree.AllDamagedTrees.TryGetValue(p, out cur) && cur == dt) DamagedTree.AllDamagedTrees.Remove(p);
				TreeRegistry reg = TreeRegistry.Instance;
				if (reg != null) reg.Remove(p);
				if (dt.gameObject.activeSelf) ObjectPoolSingleton.Release(dt.gameObject);
				pos.Add(p);
				fixedCut++;
			}
			skipped.Clear();
			// stale entries: damaged trees whose object is gone or deactivated must not keep their batch instance excluded
			List<XYZ> dead = null;
			foreach (KeyValuePair<XYZ, DamagedTree> kv in DamagedTree.AllDamagedTrees)
			{
				if (kv.Value == null || !kv.Value.gameObject.activeInHierarchy)
				{
					if (dead == null) dead = new List<XYZ>();
					dead.Add(kv.Key);
				}
			}
			if (dead != null)
			{
				for (int i = 0; i < dead.Count; i++)
				{
					DamagedTree.AllDamagedTrees.Remove(dead[i]);
					pos.Add(dead[i]);
					stale++;
				}
			}
			if (pos.Count > 0)
			{
				try { Brix.Engine.Architecte.AppliquerChangements(); }
				catch (Exception e) { BtcLog.Error("Replay", "after seek AppliquerChangements: " + e.Message); }
				HashSet<int> done = new HashSet<int>();
				for (int i = 0; i < pos.Count; i++)
				{
					int g = Brix.Engine.Abstract.Voxel.GetGScalaire(pos[i].ToScalaire());
					if (!done.Add(g)) continue;
					Brix.Engine.Architecte.DirtyVoxel(pos[i]);
					MegaVoxel mv = Brix.Engine.Architecte.TryGetMegaVoxel(g);
					if (mv != null)
					{
						mv.UpdateNatureLists();
						chunks++;
					}
				}
			}
			BtcLog.Info("Replay", "after seek: trees fixed " + fixedCut + ", stale damaged " + stale + ", chunks rebuilt " + chunks + ", " + sw.ElapsedMilliseconds + " ms");
		}
		catch (Exception ex)
		{
			BtcLog.Error("Replay", "AfterSeekForward: " + ex);
		}
	}

	private static string T(string en, string ru)
	{
		return BtcLang.Russian ? ru : en;
	}

	private static void EnsureCfg()
	{
		bool cfg = BtcReplayAnchor._cfg;
		if (!cfg)
		{
			BtcReplayAnchor._cfg = true;
			try
			{
				BtcPaths.Init();
				BtcReplayAnchor._enabled = (BtcPaths.ReadKey("replay_anchor.txt", "Anchors", "1") != "0");
				BtcReplayAnchor._cacheOn = (BtcPaths.ReadKey("replay_anchor.txt", "Cache", "1") != "0");
				BtcReplayAnchor._sweepOn = (BtcPaths.ReadKey("replay_anchor.txt", "Sweep", "1") != "0");
				BtcReplayAnchor._forceRebuild = (BtcPaths.ReadKey("replay_anchor.txt", "ForceRebuild", "1") != "0");
				BtcReplayAnchor._resyncAll = (BtcPaths.ReadKey("replay_anchor.txt", "ResyncAll", "0") == "1");
			}
			catch (Exception)
			{
			}
		}
	}

	private static void SaveCfg()
	{
		try
		{
			BtcPaths.Init();
			File.WriteAllText(BtcPaths.Config("replay_anchor.txt"), string.Concat(new string[]
			{
				"# Rewind anchors (1 = on, 0 = off)\r\nAnchors=",
				BtcReplayAnchor._enabled ? "1" : "0",
				"\r\n# Cache anchors in BetterThanCastle/cash\r\nCache=",
				BtcReplayAnchor._cacheOn ? "1" : "0",
				"\r\n# Full column sweep after rewind (fixes crust leftovers)\r\nSweep=",
				BtcReplayAnchor._sweepOn ? "1" : "0",
				"\r\n# Force rebuild of nature meshes in touched chunks after rewind\r\nForceRebuild=",
				BtcReplayAnchor._forceRebuild ? "1" : "0",
				"\r\n# Rebuild nature meshes in ALL chunks after rewind (slow, diagnostic)\r\nResyncAll=",
				BtcReplayAnchor._resyncAll ? "1" : "0",
				"\r\n"
			}));
		}
		catch (Exception)
		{
		}
	}

	private static void Log(string s)
	{
		try
		{
			BtcPaths.Init();
			File.AppendAllText(BtcPaths.DebugFile("btc_replay_anchor.txt"), DateTime.Now.ToString("HH:mm:ss.fff") + " " + s + "\r\n");
		}
		catch (Exception)
		{
		}
	}

	public static bool GetEnabled()
	{
		BtcReplayAnchor.EnsureCfg();
		return BtcReplayAnchor._enabled;
	}

	public static void SetEnabled(bool v)
	{
		BtcReplayAnchor.EnsureCfg();
		BtcReplayAnchor._enabled = v;
		BtcReplayAnchor.SaveCfg();
	}

	public static bool GetCache()
	{
		BtcReplayAnchor.EnsureCfg();
		return BtcReplayAnchor._cacheOn;
	}

	public static void SetCache(bool v)
	{
		BtcReplayAnchor.EnsureCfg();
		BtcReplayAnchor._cacheOn = v;
		BtcReplayAnchor.SaveCfg();
	}

	public static bool GetSweep()
	{
		BtcReplayAnchor.EnsureCfg();
		return BtcReplayAnchor._sweepOn;
	}

	public static void SetSweep(bool v)
	{
		BtcReplayAnchor.EnsureCfg();
		BtcReplayAnchor._sweepOn = v;
		BtcReplayAnchor.SaveCfg();
	}

	public static string UiAnchors()
	{
		return BtcReplayAnchor.T("Fast rewind anchors", "Якоря быстрой перемотки");
	}

	public static string UiCache()
	{
		return BtcReplayAnchor.T("Cache anchors on disk", "Кэшировать якоря на диске");
	}

	public static string UiSweep()
	{
		return BtcReplayAnchor.T("Fix leftover terrain after rewind", "Чинить остатки террейна после перемотки");
	}

	public static string UiNote()
	{
		return BtcReplayAnchor.T("Anchors are built once when a replay opens (from its keyframes) and saved to BetterThanCastle/cash, so rewinds start from the nearest keyframe instead of the beginning.", "Якоря строятся один раз при открытии реплея (по ключевым кадрам) и сохраняются в BetterThanCastle/cash, поэтому перемотка идёт от ближайшего ключевого кадра, а не с начала.");
	}

	public static int StartPos
	{
		get
		{
			return (BtcReplayAnchor.Selected != 0 && BtcReplayAnchor._selIdx >= 0) ? BtcReplayAnchor._kf[BtcReplayAnchor._selIdx].startPos : 0;
		}
	}

	public static long StartMs
	{
		get
		{
			return (BtcReplayAnchor.Selected != 0 && BtcReplayAnchor._selIdx >= 0) ? BtcReplayAnchor._kf[BtcReplayAnchor._selIdx].msBefore : 0L;
		}
	}

	private static uint V7(byte[] b, ref int p)
	{
		uint num = 0U;
		int num2 = 0;
		while (p < b.Length)
		{
			int num3 = p;
			p = num3 + 1;
			byte b2 = b[num3];
			num |= (uint)((uint)(b2 & 127) << num2);
			bool flag = b2 < 128;
			if (flag)
			{
				break;
			}
			num2 += 7;
		}
		return num;
	}

	private static void Scan(byte[] b)
	{
		BtcReplayAnchor._kf.Clear();
		long num = 0L;
		int i = 0;
		while (i < b.Length)
		{
			int startPos = i;
			int num2 = (int)b[i++];
			long msBefore = num;
			num += (long)((ulong)BtcReplayAnchor.V7(b, ref i));
			bool flag = num2 == 0;
			if (flag)
			{
				i++;
				int num3 = (int)BtcReplayAnchor.V7(b, ref i);
				i += num3;
			}
			else
			{
				bool flag2 = num2 == 2;
				if (flag2)
				{
					BtcReplayAnchor.V7(b, ref i);
					i++;
					int num4 = (int)BtcReplayAnchor.V7(b, ref i);
					i += num4;
				}
				else
				{
					int num5 = (int)BtcReplayAnchor.V7(b, ref i);
					bool flag3 = num2 == 1;
					if (flag3)
					{
						BtcReplayAnchor.Kf item = default(BtcReplayAnchor.Kf);
						item.id = num5;
						item.startPos = startPos;
						item.msBefore = msBefore;
						item.msStart = num;
						BtcReplayAnchor._kf.Add(item);
					}
					else
					{
						bool flag4 = num2 == 3;
						if (flag4)
						{
							for (int j = BtcReplayAnchor._kf.Count - 1; j >= 0; j--)
							{
								bool flag5 = BtcReplayAnchor._kf[j].id == num5;
								if (flag5)
								{
									BtcReplayAnchor.Kf value = BtcReplayAnchor._kf[j];
									value.ended = true;
									BtcReplayAnchor._kf[j] = value;
									break;
								}
							}
						}
					}
				}
			}
		}
	}

	private static int IndexOfId(int id)
	{
		for (int i = 0; i < BtcReplayAnchor._kf.Count; i++)
		{
			bool flag = BtcReplayAnchor._kf[i].id == id;
			if (flag)
			{
				return i;
			}
		}
		return -1;
	}

	public static int OnRecord(int type, byte[] buf, int body)
	{
		bool flag = type == 0;
		int result;
		if (flag)
		{
			result = ((BtcReplayAnchor._active != 0 && BtcReplayAnchor._inWin) ? 0 : 1);
		}
		else
		{
			bool flag2 = type == 2;
			if (flag2)
			{
				int num = body;
				int num2 = (int)BtcReplayAnchor.V7(buf, ref num);
				result = ((BtcReplayAnchor._active != 0 && num2 == BtcReplayAnchor._active) ? 1 : 0);
			}
			else
			{
				bool flag3 = type == 1 || type == 3;
				if (flag3)
				{
					int num3 = body;
					int num4 = (int)BtcReplayAnchor.V7(buf, ref num3);
					bool flag4 = type == 1 && BtcReplayAnchor._state == 2;
					if (flag4)
					{
						BtcReplayAnchor.CaptureAt(num4);
					}
					bool flag5 = BtcReplayAnchor._active != 0 && num4 == BtcReplayAnchor._active;
					if (flag5)
					{
						bool flag6 = type == 1;
						if (flag6)
						{
							BtcReplayAnchor._inWin = true;
						}
						else
						{
							BtcReplayAnchor._inWin = false;
							BtcReplayAnchor._active = 0;
						}
					}
				}
				result = 0;
			}
		}
		return result;
	}

	public static void Select(float target)
	{
		BtcReplayAnchor.EnsureCfg();
		BtcReplayAnchor.Selected = 0;
		BtcReplayAnchor._selIdx = -1;
		BtcReplayAnchor._active = 0;
		BtcReplayAnchor._inWin = false;
		bool flag = !BtcReplayAnchor._enabled || BtcReplayAnchor._failed || BtcReplayAnchor._terr == null;
		if (!flag)
		{
			long num = (long)(target * 1000f);
			int num2 = -1;
			for (int i = 0; i < BtcReplayAnchor._kf.Count; i++)
			{
				BtcReplayAnchor.Kf kf = BtcReplayAnchor._kf[i];
				bool flag2 = kf.ended && BtcReplayAnchor._terr[i] != null && BtcReplayAnchor._nat[i] != null && kf.msStart <= num && kf.msStart >= 10000L;
				if (flag2)
				{
					num2 = i;
				}
			}
			bool flag3 = num2 >= 0;
			if (flag3)
			{
				BtcReplayAnchor._selIdx = num2;
				BtcReplayAnchor.Selected = BtcReplayAnchor._kf[num2].id;
			}
			BtcReplayAnchor.Log("select: target " + target.ToString("0.0") + " s -> " + ((num2 >= 0) ? string.Concat(new object[]
			{
				"keyframe ",
				BtcReplayAnchor.Selected,
				" at ",
				BtcReplayAnchor._kf[num2].msStart / 1000L,
				" s"
			}) : "none (feed from the start)"));
		}
	}

	public static void Begin()
	{
		BtcReplayAnchor._active = BtcReplayAnchor.Selected;
		BtcReplayAnchor._inWin = false;
	}

	public static void OnRewindFailed()
	{
		bool flag = BtcReplayAnchor.Selected != 0;
		if (flag)
		{
			BtcReplayAnchor._failed = true;
			BtcReplayAnchor.Log("rewind from a keyframe failed, anchors are disabled until the game restarts");
		}
		BtcReplayAnchor.Selected = 0;
		BtcReplayAnchor._active = 0;
		BtcReplayAnchor._inWin = false;
	}

	public static void Clear()
	{
		BtcReplayAnchor._kf.Clear();
		BtcReplayAnchor._terr = null;
		BtcReplayAnchor._nat = null;
		BtcReplayAnchor._buf = null;
		BtcReplayAnchor._base = null;
		BtcReplayAnchor._state = 0;
		BtcReplayAnchor.Selected = 0;
		BtcReplayAnchor._selIdx = -1;
		BtcReplayAnchor._active = 0;
		BtcReplayAnchor._inWin = false;
	}

	public static void OnBaseline(byte[] playBuf, string replayPath)
	{
		BtcReplayAnchor.EnsureCfg();
		BtcReplayAnchor.Clear();
		bool flag = !BtcReplayAnchor._enabled || playBuf == null;
		if (!flag)
		{
			try
			{
				Stopwatch stopwatch = Stopwatch.StartNew();
				BtcReplayAnchor._buf = playBuf;
				BtcReplayAnchor._name = BtcReplayAnchor.SafeName(replayPath);
				BtcReplayAnchor._hash = BtcReplayAnchor.Hash(playBuf);
				BtcReplayAnchor.Scan(playBuf);
				int num = 0;
				for (int i = 0; i < BtcReplayAnchor._kf.Count; i++)
				{
					bool ended = BtcReplayAnchor._kf[i].ended;
					if (ended)
					{
						num++;
					}
				}
				BtcReplayAnchor._terr = new byte[BtcReplayAnchor._kf.Count][];
				BtcReplayAnchor._nat = new byte[BtcReplayAnchor._kf.Count][];
				BtcReplayAnchor._base = BtcReplayAnchor.ReadLive();
				BtcReplayAnchor.Log(string.Concat(new object[]
				{
					"open ",
					BtcReplayAnchor._name,
					": ",
					BtcReplayAnchor._kf.Count,
					" keyframes (",
					num,
					" complete), index ",
					stopwatch.ElapsedMilliseconds,
					" ms"
				}));
				bool flag2 = num == 0 || BtcReplayAnchor._failed;
				if (!flag2)
				{
					int num2 = BtcReplayAnchor._cacheOn ? BtcReplayAnchor.LoadCache() : 0;
					bool flag3 = num2 >= num;
					if (flag3)
					{
						BtcReplayAnchor.Log("anchors loaded from the cache: " + num2);
					}
					else
					{
						BtcReplayAnchor._state = 1;
						BtcReplayAnchor._tReady = Time.unscaledTime + 2f;
						BtcReplayAnchor.Log("anchors to build: " + (num - num2));
					}
				}
			}
			catch (Exception arg)
			{
				BtcReplayAnchor._state = 0;
				BtcReplayAnchor.Log("OnBaseline: " + arg);
			}
		}
	}

	public static void Tick(float now, bool playGo, bool seeking, bool rewinding)
	{
		bool flag = BtcReplayAnchor._state == 0;
		if (!flag)
		{
			try
			{
				bool flag2 = BtcReplayAnchor._state == 1;
				if (flag2)
				{
					bool flag3 = !playGo || seeking || rewinding || now < BtcReplayAnchor._tReady;
					if (!flag3)
					{
						int num = -1;
						for (int i = 0; i < BtcReplayAnchor._kf.Count; i++)
						{
							bool flag4 = BtcReplayAnchor._kf[i].ended && BtcReplayAnchor._terr[i] == null;
							if (flag4)
							{
								num = i;
							}
						}
						bool flag5 = num < 0;
						if (flag5)
						{
							BtcReplayAnchor._state = 0;
						}
						else
						{
							BtcReplayAnchor._state = 2;
							BtcReplayAnchor._tSeek = now;
							BtcReplayAnchor._sawSeek = false;
							BtcReplay.Say(BtcReplayAnchor.T("Building rewind anchors...", "Строим якоря перемотки..."));
							BtcReplay.SeekTo((float)BtcReplayAnchor._kf[num].msStart / 1000f + 0.05f);
						}
					}
				}
				else
				{
					bool flag6 = BtcReplayAnchor._state == 2;
					if (flag6)
					{
						bool flag7 = rewinding || now - BtcReplayAnchor._tSeek > 1800f;
						if (flag7)
						{
							BtcReplayAnchor.Log("build aborted");
							BtcReplayAnchor.FinishBuild(false);
						}
						else if (seeking)
						{
							BtcReplayAnchor._sawSeek = true;
						}
						else
						{
							bool flag8 = BtcReplayAnchor._sawSeek || now - BtcReplayAnchor._tSeek > 3f;
							if (flag8)
							{
								BtcReplayAnchor.FinishBuild(true);
							}
						}
					}
					else
					{
						bool flag9 = BtcReplayAnchor._state == 3;
						if (flag9)
						{
							bool flag10 = !seeking && !rewinding;
							if (flag10)
							{
								BtcReplayAnchor._state = 0;
							}
						}
					}
				}
			}
			catch (Exception arg)
			{
				BtcReplayAnchor._state = 0;
				BtcReplayAnchor.Log("Tick: " + arg);
			}
		}
	}

	private static void FinishBuild(bool toStart)
	{
		int num = 0;
		for (int i = 0; i < BtcReplayAnchor._kf.Count; i++)
		{
			bool flag = BtcReplayAnchor._terr[i] != null && BtcReplayAnchor._nat[i] != null;
			if (flag)
			{
				num++;
			}
		}
		BtcReplayAnchor.Log("build finished: " + num + " anchors");
		bool flag2 = BtcReplayAnchor._cacheOn && num > 0;
		if (flag2)
		{
			BtcReplayAnchor.SaveCache();
		}
		BtcReplayAnchor._state = 3;
		if (toStart)
		{
			BtcReplay.Say(BtcReplayAnchor.T("Anchors ready: " + num, "Якоря готовы: " + num));
			BtcReplay.SeekTo(0f);
		}
		else
		{
			BtcReplayAnchor._state = 0;
		}
	}

	private static void CaptureAt(int id)
	{
		int num = BtcReplayAnchor.IndexOfId(id);
		bool flag = num < 0 || !BtcReplayAnchor._kf[num].ended || BtcReplayAnchor._terr[num] != null;
		if (!flag)
		{
			try
			{
				Stopwatch stopwatch = Stopwatch.StartNew();
				byte[] array = BtcReplayAnchor.SnapTerrain();
				BtcReplayAnchor.NState cur = BtcReplayAnchor.ReadLive();
				byte[] array2 = BtcReplayAnchor.WriteNature(cur);
				bool flag2 = array != null && array2 != null;
				if (flag2)
				{
					BtcReplayAnchor._terr[num] = array;
					BtcReplayAnchor._nat[num] = array2;
				}
				BtcReplayAnchor.Log(string.Concat(new object[]
				{
					"anchor ",
					id,
					" at ",
					BtcReplayAnchor._kf[num].msStart / 1000L,
					" s: terrain ",
					(array != null) ? array.Length : -1,
					" B, nature ",
					(array2 != null) ? array2.Length : -1,
					" B, ",
					stopwatch.ElapsedMilliseconds,
					" ms"
				}));
			}
			catch (Exception ex)
			{
				BtcReplayAnchor.Log(string.Concat(new object[]
				{
					"CaptureAt ",
					id,
					": ",
					ex
				}));
			}
		}
	}

	private static string SafeName(string path)
	{
		string text = string.IsNullOrEmpty(path) ? "replay" : Path.GetFileNameWithoutExtension(path);
		char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
		for (int i = 0; i < invalidFileNameChars.Length; i++)
		{
			text = text.Replace(invalidFileNameChars[i], '_');
		}
		return (text.Length > 60) ? text.Substring(0, 60) : text;
	}

	private static ulong Hash(byte[] b)
	{
		ulong num = 14695981039346656037UL;
		int num2 = Math.Max(1, b.Length >> 20);
		for (int i = 0; i < b.Length; i += num2)
		{
			num = (num ^ (ulong)b[i]) * 1099511628211UL;
		}
		return (num ^ (ulong)((long)b.Length)) * 1099511628211UL;
	}

	private static string CachePath()
	{
		string text = Path.Combine(BtcPaths.Root, "cash");
		Directory.CreateDirectory(text);
		return Path.Combine(text, BtcReplayAnchor._name + "_" + BtcReplayAnchor._hash.ToString("x16") + ".btca");
	}

	private static void SaveCache()
	{
		try
		{
			string text = BtcReplayAnchor.CachePath();
			string text2 = text + ".tmp";
			using (FileStream fileStream = new FileStream(text2, FileMode.Create))
			{
				using (DeflateStream deflateStream = new DeflateStream(fileStream, CompressionMode.Compress))
				{
					using (BinaryWriter binaryWriter = new BinaryWriter(deflateStream))
					{
						binaryWriter.Write(1094931522);
						binaryWriter.Write(1);
						binaryWriter.Write((long)BtcReplayAnchor._hash);
						binaryWriter.Write(BtcReplayAnchor._kf.Count);
						for (int i = 0; i < BtcReplayAnchor._kf.Count; i++)
						{
							bool flag = BtcReplayAnchor._terr[i] != null && BtcReplayAnchor._nat[i] != null;
							binaryWriter.Write(BtcReplayAnchor._kf[i].id);
							binaryWriter.Write(flag);
							bool flag2 = flag;
							if (flag2)
							{
								binaryWriter.Write(BtcReplayAnchor._terr[i].Length);
								binaryWriter.Write(BtcReplayAnchor._terr[i]);
								binaryWriter.Write(BtcReplayAnchor._nat[i].Length);
								binaryWriter.Write(BtcReplayAnchor._nat[i]);
							}
						}
					}
				}
			}
			bool flag3 = File.Exists(text);
			if (flag3)
			{
				File.Delete(text);
			}
			File.Move(text2, text);
			BtcReplayAnchor.Log("cache saved: " + text);
		}
		catch (Exception ex)
		{
			BtcReplayAnchor.Log("SaveCache: " + ex.Message);
		}
	}

	private static int LoadCache()
	{
		int result;
		try
		{
			string path = BtcReplayAnchor.CachePath();
			bool flag = !File.Exists(path);
			if (flag)
			{
				result = 0;
			}
			else
			{
				int num = 0;
				using (FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read))
				{
					using (DeflateStream deflateStream = new DeflateStream(fileStream, CompressionMode.Decompress))
					{
						using (BinaryReader binaryReader = new BinaryReader(deflateStream))
						{
							bool flag2 = binaryReader.ReadInt32() != 1094931522 || binaryReader.ReadInt32() != 1 || binaryReader.ReadInt64() != (long)BtcReplayAnchor._hash;
							if (flag2)
							{
								BtcReplayAnchor.Log("cache: header mismatch, ignored");
								return 0;
							}
							int num2 = binaryReader.ReadInt32();
							for (int i = 0; i < num2; i++)
							{
								int id = binaryReader.ReadInt32();
								bool flag3 = binaryReader.ReadBoolean();
								bool flag4 = !flag3;
								if (!flag4)
								{
									byte[] array = binaryReader.ReadBytes(binaryReader.ReadInt32());
									byte[] array2 = binaryReader.ReadBytes(binaryReader.ReadInt32());
									int num3 = BtcReplayAnchor.IndexOfId(id);
									bool flag5 = num3 >= 0;
									if (flag5)
									{
										BtcReplayAnchor._terr[num3] = array;
										BtcReplayAnchor._nat[num3] = array2;
										num++;
									}
								}
							}
						}
					}
				}
				result = num;
			}
		}
		catch (Exception ex)
		{
			BtcReplayAnchor.Log("LoadCache: " + ex.Message);
			for (int j = 0; j < BtcReplayAnchor._kf.Count; j++)
			{
				BtcReplayAnchor._terr[j] = null;
				BtcReplayAnchor._nat[j] = null;
			}
			result = 0;
		}
		return result;
	}

	private static FieldInfo Fld(string n)
	{
		return typeof(BtcTerrainUndo).GetField(n, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
	}

	private static bool Bind()
	{
		bool bound = BtcReplayAnchor._bound;
		bool result;
		if (bound)
		{
			result = true;
		}
		else
		{
			BindingFlags bindingAttr = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
			MethodInfo method = typeof(BtcTerrainUndo).GetMethod("Fix", bindingAttr);
			MethodInfo method2 = typeof(BtcTerrainUndo).GetMethod("FireAt", bindingAttr);
			BtcReplayAnchor._fX0 = BtcReplayAnchor.Fld("_x0");
			BtcReplayAnchor._fZ0 = BtcReplayAnchor.Fld("_z0");
			BtcReplayAnchor._fW = BtcReplayAnchor.Fld("_w");
			BtcReplayAnchor._fH = BtcReplayAnchor.Fld("_h");
			BtcReplayAnchor._fBot = BtcReplayAnchor.Fld("_bot");
			BtcReplayAnchor._fTop = BtcReplayAnchor.Fld("_top");
			BtcReplayAnchor._fTyp = BtcReplayAnchor.Fld("_typ");
			BtcReplayAnchor._fCrf = BtcReplayAnchor.Fld("_crf");
			BtcReplayAnchor._fVs = BtcReplayAnchor.Fld("_vstart");
			BtcReplayAnchor._fVt = BtcReplayAnchor.Fld("_vt");
			BtcReplayAnchor._fVv = BtcReplayAnchor.Fld("_vv");
			BtcReplayAnchor._fVr = BtcReplayAnchor.Fld("_vrange");
			BtcReplayAnchor._fHas = BtcReplayAnchor.Fld("_has");
			BtcReplayAnchor._fHasVox = BtcReplayAnchor.Fld("_hasVox");
			bool flag = method == null || method2 == null || BtcReplayAnchor._fBot == null || BtcReplayAnchor._fVr == null || BtcReplayAnchor._fHasVox == null || BtcReplayAnchor._fCrf == null;
			if (flag)
			{
				BtcReplayAnchor.Log("Bind: BtcTerrainUndo members not found");
				result = false;
			}
			else
			{
				BtcReplayAnchor._fix = (BtcReplayAnchor.FixD)Delegate.CreateDelegate(typeof(BtcReplayAnchor.FixD), method);
				BtcReplayAnchor._fire = (BtcReplayAnchor.FireD)Delegate.CreateDelegate(typeof(BtcReplayAnchor.FireD), method2);
				BtcReplayAnchor._bound = true;
				result = true;
			}
		}
		return result;
	}

	private static bool Differs(int b, int t, VoxelTerrainType ty, float cr, short bot, short top, byte typ, float crf)
	{
		return b != (int)bot || t != (int)top || (byte)ty != typ || Mathf.Abs(cr - crf) >= 0.0001f;
	}

	private static byte[] SnapTerrain()
	{
		bool flag = !BtcReplayAnchor.Bind() || !(bool)BtcReplayAnchor._fHas.GetValue(null);
		byte[] result;
		if (flag)
		{
			result = null;
		}
		else
		{
			int num = (int)BtcReplayAnchor._fX0.GetValue(null);
			int num2 = (int)BtcReplayAnchor._fZ0.GetValue(null);
			int num3 = (int)BtcReplayAnchor._fW.GetValue(null);
			int num4 = (int)BtcReplayAnchor._fH.GetValue(null);
			short[] array = (short[])BtcReplayAnchor._fBot.GetValue(null);
			short[] array2 = (short[])BtcReplayAnchor._fTop.GetValue(null);
			byte[] array3 = (byte[])BtcReplayAnchor._fTyp.GetValue(null);
			float[] array4 = (float[])BtcReplayAnchor._fCrf.GetValue(null);
			MemoryStream memoryStream = new MemoryStream();
			BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
			List<int> list = new List<int>();
			int num5 = num3 * num4;
			for (int i = 0; i < num5; i++)
			{
				int b;
				int t;
				VoxelTerrainType ty;
				float cr;
				VoxelEngine.GetColumnData(num + i / num4, num2 + i % num4, out b, out t, out ty, out cr);
				bool flag2 = BtcReplayAnchor.Differs(b, t, ty, cr, array[i], array2[i], array3[i], array4[i]);
				if (flag2)
				{
					list.Add(i);
				}
			}
			binaryWriter.Write(list.Count);
			for (int j = 0; j < list.Count; j++)
			{
				int num6 = list[j];
				int num7;
				int num8;
				VoxelTerrainType voxelTerrainType;
				float value;
				VoxelEngine.GetColumnData(num + num6 / num4, num2 + num6 % num4, out num7, out num8, out voxelTerrainType, out value);
				binaryWriter.Write(num6);
				binaryWriter.Write((short)num7);
				binaryWriter.Write((short)num8);
				binaryWriter.Write((byte)voxelTerrainType);
				binaryWriter.Write(value);
			}
			List<int> list2 = new List<int>();
			List<byte> list3 = new List<byte>();
			List<byte> list4 = new List<byte>();
			List<byte> list5 = new List<byte>();
			bool flag3 = (bool)BtcReplayAnchor._fHasVox.GetValue(null);
			if (flag3)
			{
				Dictionary<int, int> dictionary = (Dictionary<int, int>)BtcReplayAnchor._fVr.GetValue(null);
				int[] array5 = (int[])BtcReplayAnchor._fVs.GetValue(null);
				byte[] array6 = (byte[])BtcReplayAnchor._fVt.GetValue(null);
				byte[] array7 = (byte[])BtcReplayAnchor._fVv.GetValue(null);
				foreach (KeyValuePair<int, int> keyValuePair in dictionary)
				{
					int key = keyValuePair.Key;
					int num9 = Mathf.Max((keyValuePair.Value & 255) - 1, 0);
					int num10 = Mathf.Min((keyValuePair.Value >> 8) + 1, 254);
					int x = num + key / num4;
					int z = num2 + key % num4;
					int num11 = Mathf.Max((int)(array[key] - 1), 0);
					int num12 = Mathf.Min((int)(array2[key] + 1), 254);
					for (int k = num9; k <= num10; k++)
					{
						int num13 = 0;
						int num14 = 0;
						bool flag4 = array5[key] >= 0 && k >= num11 && k <= num12;
						if (flag4)
						{
							num13 = (int)array6[array5[key] + (k - num11)];
							num14 = (int)array7[array5[key] + (k - num11)];
						}
						VoxelHandle voxelHandle = Brix.Engine.Voxel.Engine.GetVoxelHandle(new XYZ(x, k, z));
						int num15 = Mathf.Clamp(Brix.Engine.Voxel.Engine.GetVoxelType(voxelHandle), 0, 255);
						int num16 = Mathf.Clamp(Brix.Engine.Voxel.Engine.GetVoxelVolume(voxelHandle), 0, 255);
						bool flag5 = (num13 == 0) ? (num15 != 0 || Brix.Engine.Voxel.Engine.IsVoxelNonEmpty(voxelHandle)) : (num15 != num13 || num16 != num14);
						bool flag6 = flag5;
						if (flag6)
						{
							list2.Add(key);
							list3.Add((byte)k);
							list4.Add((byte)num15);
							list5.Add((byte)num16);
						}
					}
				}
			}
			binaryWriter.Write(list2.Count);
			for (int l = 0; l < list2.Count; l++)
			{
				binaryWriter.Write(list2[l]);
				binaryWriter.Write(list3[l]);
				binaryWriter.Write(list4[l]);
				binaryWriter.Write(list5[l]);
			}
			binaryWriter.Flush();
			result = memoryStream.ToArray();
		}
		return result;
	}

	private static void NudgeCrust(List<int> cols, float[] want, int x0, int z0, int h)
	{
		bool flag = cols.Count == 0;
		if (!flag)
		{
			VoxelEngine.BeginChanges();
			for (int i = 0; i < cols.Count; i++)
			{
				float num = want[i];
				VoxelEngine.SetColumnCrustVolume(x0 + cols[i] / h, z0 + cols[i] % h, (num < 0.5f) ? (num + 0.33333334f) : (num - 0.33333334f));
			}
			VoxelEngine.ApplyChanges(false);
			VoxelEngine.BeginChanges();
			for (int j = 0; j < cols.Count; j++)
			{
				VoxelEngine.SetColumnCrustVolume(x0 + cols[j] / h, z0 + cols[j] % h, want[j]);
			}
			VoxelEngine.ApplyChanges(false);
		}
	}

	private static void ApplyTerrain(byte[] blob)
	{
		bool flag = blob == null || !BtcReplayAnchor.Bind();
		if (flag)
		{
			throw new Exception("no terrain anchor");
		}
		int num = (int)BtcReplayAnchor._fX0.GetValue(null);
		int num2 = (int)BtcReplayAnchor._fZ0.GetValue(null);
		int num3 = (int)BtcReplayAnchor._fH.GetValue(null);
		BinaryReader binaryReader = new BinaryReader(new MemoryStream(blob));
		int num4 = binaryReader.ReadInt32();
		List<int> list = new List<int>(num4);
		int[] array = new int[num4];
		float[] array2 = new float[num4];
		VoxelEngine.BeginChanges();
		for (int i = 0; i < num4; i++)
		{
			int num5 = binaryReader.ReadInt32();
			int height = (int)binaryReader.ReadInt16();
			int num6 = (int)binaryReader.ReadInt16();
			byte type = binaryReader.ReadByte();
			float num7 = binaryReader.ReadSingle();
			int x = num + num5 / num3;
			int z = num2 + num5 % num3;
			VoxelEngine.SetColumnBottom(x, z, height);
			VoxelEngine.SetColumnTopVoxel(x, z, num6);
			VoxelEngine.SetColumnType(x, z, (VoxelTerrainType)type);
			VoxelEngine.SetColumnCrustVolume(x, z, num7);
			list.Add(num5);
			array[i] = num6;
			array2[i] = num7;
		}
		VoxelEngine.ApplyChanges(false);
		int num8 = binaryReader.ReadInt32();
		int[] array3 = new int[num8];
		int[] array4 = new int[num8];
		int[] array5 = new int[num8];
		VoxelEngine.BeginChanges();
		for (int j = 0; j < num8; j++)
		{
			int num9 = binaryReader.ReadInt32();
			int num10 = (int)binaryReader.ReadByte();
			int num11 = (int)binaryReader.ReadByte();
			int v = (int)binaryReader.ReadByte();
			int num12 = num + num9 / num3;
			int num13 = num2 + num9 % num3;
			VoxelHandle voxelHandle = Brix.Engine.Voxel.Engine.GetVoxelHandle(new XYZ(num12, num10, num13));
			bool flag2 = num11 == 0;
			if (flag2)
			{
				Brix.Engine.Voxel.Engine.RemoveVoxel(voxelHandle);
			}
			else
			{
				bool flag3 = Brix.Engine.Voxel.Engine.GetVoxelType(voxelHandle) != num11;
				if (flag3)
				{
					Brix.Engine.Voxel.Engine.SetVoxelType(voxelHandle, num11);
				}
				Brix.Engine.Voxel.Engine.SetVoxelVolume(voxelHandle, v);
			}
			array3[j] = num12;
			array4[j] = num10;
			array5[j] = num13;
		}
		VoxelEngine.ApplyChanges(false);
		BtcReplayAnchor.NudgeCrust(list, array2, num, num2, num3);
		for (int k = 0; k < list.Count; k++)
		{
			BtcReplayAnchor._fire(num + list[k] / num3, array[k], num2 + list[k] % num3);
		}
		for (int l = 0; l < num8; l++)
		{
			BtcReplayAnchor._fire(array3[l], array4[l], array5[l]);
		}
		BtcReplayAnchor.Log(string.Concat(new object[]
		{
			"terrain anchor applied: ",
			num4,
			" columns, ",
			num8,
			" voxels"
		}));
	}

	private static void Sweep()
	{
		bool flag = !BtcReplayAnchor._sweepOn || !BtcReplayAnchor.Bind() || !(bool)BtcReplayAnchor._fHas.GetValue(null);
		if (!flag)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			int num = (int)BtcReplayAnchor._fX0.GetValue(null);
			int num2 = (int)BtcReplayAnchor._fZ0.GetValue(null);
			int num3 = (int)BtcReplayAnchor._fW.GetValue(null);
			int num4 = (int)BtcReplayAnchor._fH.GetValue(null);
			short[] array = (short[])BtcReplayAnchor._fBot.GetValue(null);
			short[] array2 = (short[])BtcReplayAnchor._fTop.GetValue(null);
			byte[] array3 = (byte[])BtcReplayAnchor._fTyp.GetValue(null);
			float[] array4 = (float[])BtcReplayAnchor._fCrf.GetValue(null);
			List<int> list = new List<int>();
			int num5 = 0;
			int num6 = num3 * num4;
			for (int i = 0; i < num6; i++)
			{
				int num7;
				int num8;
				VoxelTerrainType voxelTerrainType;
				float cr;
				VoxelEngine.GetColumnData(num + i / num4, num2 + i % num4, out num7, out num8, out voxelTerrainType, out cr);
				bool flag2 = BtcReplayAnchor.Differs(num7, num8, voxelTerrainType, cr, array[i], array2[i], array3[i], array4[i]);
				if (flag2)
				{
					list.Add(i);
					bool flag3 = num7 == (int)array[i] && num8 == (int)array2[i] && (byte)voxelTerrainType == array3[i];
					if (flag3)
					{
						num5++;
					}
				}
			}
			long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
			bool flag4 = list.Count == 0;
			if (flag4)
			{
				BtcReplayAnchor.Log(string.Concat(new object[]
				{
					"sweep: clean, ",
					num6,
					" columns, ",
					elapsedMilliseconds,
					" ms"
				}));
			}
			else
			{
				List<int> idx = new List<int>();
				List<int> oldTop = new List<int>();
				VoxelEngine.BeginChanges();
				for (int j = 0; j < list.Count; j++)
				{
					BtcReplayAnchor._fix(list[j], idx, oldTop);
				}
				VoxelEngine.ApplyChanges(false);
				float[] array5 = new float[list.Count];
				for (int k = 0; k < list.Count; k++)
				{
					array5[k] = array4[list[k]];
				}
				BtcReplayAnchor.NudgeCrust(list, array5, num, num2, num4);
				for (int l = 0; l < list.Count; l++)
				{
					int num9 = list[l];
					BtcReplayAnchor._fire(num + num9 / num4, (int)array2[num9], num2 + num9 % num4);
				}
				BtcReplayAnchor.Log(string.Concat(new object[]
				{
					"sweep: ",
					list.Count,
					" columns differed from the baseline (crust only ",
					num5,
					"), fixed, scan ",
					elapsedMilliseconds,
					" ms, total ",
					stopwatch.ElapsedMilliseconds,
					" ms"
				}));
			}
		}
	}

	private static IDictionary TreeDict()
	{
		TreeRegistry instance = TreeRegistry.Instance;
		bool flag = instance == null;
		IDictionary result;
		if (flag)
		{
			result = null;
		}
		else
		{
			bool flag2 = BtcReplayAnchor._treeField == null;
			if (flag2)
			{
				BtcReplayAnchor._treeField = typeof(TreeRegistry).GetField("_treeData", BindingFlags.Instance | BindingFlags.NonPublic);
			}
			result = ((BtcReplayAnchor._treeField != null) ? (BtcReplayAnchor._treeField.GetValue(instance) as IDictionary) : null);
		}
		return result;
	}

	private static Dictionary<XYZ, BtcReplayAnchor.MV> ReadMin(MineralSerializedData[] a, bool iron)
	{
		Dictionary<XYZ, BtcReplayAnchor.MV> dictionary = new Dictionary<XYZ, BtcReplayAnchor.MV>(a.Length);
		for (int i = 0; i < a.Length; i++)
		{
			BtcReplayAnchor.MV value = default(BtcReplayAnchor.MV);
			value.g = a[i].growth;
			value.v = a[i].variation;
			value.d = (iron ? IronRegistry.GetDamage(a[i].position) : PyriteRegistry.GetDamage(a[i].position));
			dictionary[a[i].position] = value;
		}
		return dictionary;
	}

	private static BtcReplayAnchor.NState ReadLive()
	{
		BtcReplayAnchor.NState nstate = new BtcReplayAnchor.NState();
		IDictionary dictionary = BtcReplayAnchor.TreeDict();
		bool flag = dictionary != null;
		if (flag)
		{
			nstate.trees = new Dictionary<XYZ, int>(dictionary.Count);
			foreach (object obj in dictionary)
			{
				DictionaryEntry dictionaryEntry = (DictionaryEntry)obj;
				nstate.trees[(XYZ)dictionaryEntry.Key] = (int)dictionaryEntry.Value;
			}
		}
		PlantSerializedData[] serializedData = BrixSingleton<PlantRegistry>.Instance.SerializedData;
		nstate.plants = new Dictionary<XYZ, BtcReplayAnchor.PV>(serializedData.Length);
		for (int i = 0; i < serializedData.Length; i++)
		{
			BtcReplayAnchor.PV value = default(BtcReplayAnchor.PV);
			value.t = serializedData[i].t;
			value.s = serializedData[i].s;
			nstate.plants[new XYZ(serializedData[i].x, serializedData[i].y, serializedData[i].z)] = value;
		}
		nstate.boulders = new Dictionary<BtcReplayAnchor.BKey, BtcReplayAnchor.BVal>();
		List<XYZ> list = new List<XYZ>();
		BoulderRegistry.AddAllBoulderPositionsToList(ref list);
		HashSet<BoulderData> hashSet = new HashSet<BoulderData>();
		for (int j = 0; j < list.Count; j++)
		{
			BoulderData boulder = BoulderRegistry.GetBoulder(list[j]);
			bool flag2 = boulder == null || !hashSet.Add(boulder);
			if (!flag2)
			{
				BtcReplayAnchor.BKey key = default(BtcReplayAnchor.BKey);
				Vector3 centerPos = boulder.CenterPos;
				Vector3 offset = boulder.Offset;
				key.cx = centerPos.x;
				key.cy = centerPos.y;
				key.cz = centerPos.z;
				key.ox = offset.x;
				key.oy = offset.y;
				key.oz = offset.z;
				key.sz = (int)boulder.BoulderSize;
				BtcReplayAnchor.BVal value2 = default(BtcReplayAnchor.BVal);
				value2.growth = boulder.Growth;
				value2.root = boulder.RootPos;
				value2.damage = BoulderRegistry.GetDamage(boulder.RootPos);
				nstate.boulders[key] = value2;
			}
		}
		nstate.iron = BtcReplayAnchor.ReadMin(IronRegistry.GetSerializedMinerals(), true);
		nstate.pyr = BtcReplayAnchor.ReadMin(PyriteRegistry.GetSerializedMinerals(), false);
		return nstate;
	}

	private static void W(BinaryWriter w, XYZ p)
	{
		w.Write(p.x);
		w.Write(p.y);
		w.Write(p.z);
	}

	private static XYZ R(BinaryReader r)
	{
		int x = r.ReadInt32();
		int y = r.ReadInt32();
		int z = r.ReadInt32();
		return new XYZ(x, y, z);
	}

	private static void WriteMin(BinaryWriter w, Dictionary<XYZ, BtcReplayAnchor.MV> d)
	{
		w.Write(d.Count);
		foreach (KeyValuePair<XYZ, BtcReplayAnchor.MV> keyValuePair in d)
		{
			BtcReplayAnchor.W(w, keyValuePair.Key);
			w.Write(keyValuePair.Value.g);
			w.Write(keyValuePair.Value.v);
			w.Write(keyValuePair.Value.d);
		}
	}

	private static Dictionary<XYZ, BtcReplayAnchor.MV> ReadMinBlob(BinaryReader r)
	{
		int num = r.ReadInt32();
		Dictionary<XYZ, BtcReplayAnchor.MV> dictionary = new Dictionary<XYZ, BtcReplayAnchor.MV>(num);
		for (int i = 0; i < num; i++)
		{
			XYZ key = BtcReplayAnchor.R(r);
			dictionary[key] = new BtcReplayAnchor.MV
			{
				g = r.ReadSingle(),
				v = r.ReadInt32(),
				d = r.ReadSingle()
			};
		}
		return dictionary;
	}

	private static byte[] WriteNature(BtcReplayAnchor.NState cur)
	{
		BtcReplayAnchor.NState @base = BtcReplayAnchor._base;
		bool flag = @base == null;
		byte[] result;
		if (flag)
		{
			result = null;
		}
		else
		{
			MemoryStream memoryStream = new MemoryStream();
			BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
			binaryWriter.Write(cur.trees != null && @base.trees != null);
			bool flag2 = cur.trees != null && @base.trees != null;
			if (flag2)
			{
				List<KeyValuePair<XYZ, int>> list = new List<KeyValuePair<XYZ, int>>();
				List<XYZ> list2 = new List<XYZ>();
				foreach (KeyValuePair<XYZ, int> item in cur.trees)
				{
					int num;
					bool flag3 = !@base.trees.TryGetValue(item.Key, out num) || num != item.Value;
					if (flag3)
					{
						list.Add(item);
					}
				}
				foreach (KeyValuePair<XYZ, int> keyValuePair in @base.trees)
				{
					bool flag4 = !cur.trees.ContainsKey(keyValuePair.Key);
					if (flag4)
					{
						list2.Add(keyValuePair.Key);
					}
				}
				binaryWriter.Write(list.Count);
				for (int i = 0; i < list.Count; i++)
				{
					BtcReplayAnchor.W(binaryWriter, list[i].Key);
					binaryWriter.Write(list[i].Value);
				}
				binaryWriter.Write(list2.Count);
				for (int j = 0; j < list2.Count; j++)
				{
					BtcReplayAnchor.W(binaryWriter, list2[j]);
				}
			}
			List<KeyValuePair<XYZ, BtcReplayAnchor.PV>> list3 = new List<KeyValuePair<XYZ, BtcReplayAnchor.PV>>();
			List<XYZ> list4 = new List<XYZ>();
			foreach (KeyValuePair<XYZ, BtcReplayAnchor.PV> item2 in cur.plants)
			{
				BtcReplayAnchor.PV pv;
				bool flag5 = !@base.plants.TryGetValue(item2.Key, out pv) || pv.t != item2.Value.t || Mathf.Abs(pv.s - item2.Value.s) > 0.0001f;
				if (flag5)
				{
					list3.Add(item2);
				}
			}
			foreach (KeyValuePair<XYZ, BtcReplayAnchor.PV> keyValuePair2 in @base.plants)
			{
				bool flag6 = !cur.plants.ContainsKey(keyValuePair2.Key);
				if (flag6)
				{
					list4.Add(keyValuePair2.Key);
				}
			}
			binaryWriter.Write(list3.Count);
			for (int k = 0; k < list3.Count; k++)
			{
				BtcReplayAnchor.W(binaryWriter, list3[k].Key);
				binaryWriter.Write(list3[k].Value.t);
				binaryWriter.Write(list3[k].Value.s);
			}
			binaryWriter.Write(list4.Count);
			for (int l = 0; l < list4.Count; l++)
			{
				BtcReplayAnchor.W(binaryWriter, list4[l]);
			}
			binaryWriter.Write(cur.boulders.Count);
			foreach (KeyValuePair<BtcReplayAnchor.BKey, BtcReplayAnchor.BVal> keyValuePair3 in cur.boulders)
			{
				binaryWriter.Write(keyValuePair3.Key.cx);
				binaryWriter.Write(keyValuePair3.Key.cy);
				binaryWriter.Write(keyValuePair3.Key.cz);
				binaryWriter.Write(keyValuePair3.Key.ox);
				binaryWriter.Write(keyValuePair3.Key.oy);
				binaryWriter.Write(keyValuePair3.Key.oz);
				binaryWriter.Write(keyValuePair3.Key.sz);
				binaryWriter.Write(keyValuePair3.Value.growth);
				BtcReplayAnchor.W(binaryWriter, keyValuePair3.Value.root);
				binaryWriter.Write(keyValuePair3.Value.damage);
			}
			BtcReplayAnchor.WriteMin(binaryWriter, cur.iron);
			BtcReplayAnchor.WriteMin(binaryWriter, cur.pyr);
			binaryWriter.Flush();
			result = memoryStream.ToArray();
		}
		return result;
	}

	private static BtcReplayAnchor.NState ReadNature(byte[] blob)
	{
		BtcReplayAnchor.NState @base = BtcReplayAnchor._base;
		BinaryReader binaryReader = new BinaryReader(new MemoryStream(blob));
		BtcReplayAnchor.NState nstate = new BtcReplayAnchor.NState();
		bool flag = binaryReader.ReadBoolean();
		if (flag)
		{
			nstate.trees = new Dictionary<XYZ, int>(@base.trees);
			int num = binaryReader.ReadInt32();
			for (int i = 0; i < num; i++)
			{
				XYZ key = BtcReplayAnchor.R(binaryReader);
				nstate.trees[key] = binaryReader.ReadInt32();
			}
			int num2 = binaryReader.ReadInt32();
			for (int j = 0; j < num2; j++)
			{
				nstate.trees.Remove(BtcReplayAnchor.R(binaryReader));
			}
		}
		nstate.plants = new Dictionary<XYZ, BtcReplayAnchor.PV>(@base.plants);
		int num3 = binaryReader.ReadInt32();
		for (int k = 0; k < num3; k++)
		{
			XYZ key2 = BtcReplayAnchor.R(binaryReader);
			BtcReplayAnchor.PV value = default(BtcReplayAnchor.PV);
			value.t = binaryReader.ReadInt32();
			value.s = binaryReader.ReadSingle();
			nstate.plants[key2] = value;
		}
		int num4 = binaryReader.ReadInt32();
		for (int l = 0; l < num4; l++)
		{
			nstate.plants.Remove(BtcReplayAnchor.R(binaryReader));
		}
		int num5 = binaryReader.ReadInt32();
		nstate.boulders = new Dictionary<BtcReplayAnchor.BKey, BtcReplayAnchor.BVal>(num5);
		for (int m = 0; m < num5; m++)
		{
			BtcReplayAnchor.BKey key3 = default(BtcReplayAnchor.BKey);
			key3.cx = binaryReader.ReadSingle();
			key3.cy = binaryReader.ReadSingle();
			key3.cz = binaryReader.ReadSingle();
			key3.ox = binaryReader.ReadSingle();
			key3.oy = binaryReader.ReadSingle();
			key3.oz = binaryReader.ReadSingle();
			key3.sz = binaryReader.ReadInt32();
			BtcReplayAnchor.BVal value2 = default(BtcReplayAnchor.BVal);
			value2.growth = binaryReader.ReadSingle();
			value2.root = BtcReplayAnchor.R(binaryReader);
			value2.damage = binaryReader.ReadSingle();
			nstate.boulders[key3] = value2;
		}
		nstate.iron = BtcReplayAnchor.ReadMinBlob(binaryReader);
		nstate.pyr = BtcReplayAnchor.ReadMinBlob(binaryReader);
		return nstate;
	}

	private static bool MinDiffers(Dictionary<XYZ, BtcReplayAnchor.MV> a, Dictionary<XYZ, BtcReplayAnchor.MV> b)
	{
		bool flag = a.Count != b.Count;
		bool result;
		if (flag)
		{
			result = true;
		}
		else
		{
			foreach (KeyValuePair<XYZ, BtcReplayAnchor.MV> keyValuePair in a)
			{
				BtcReplayAnchor.MV mv;
				bool flag2 = !b.TryGetValue(keyValuePair.Key, out mv) || mv.v != keyValuePair.Value.v || Mathf.Abs(mv.g - keyValuePair.Value.g) > 0.0001f || Mathf.Abs(mv.d - keyValuePair.Value.d) > 0.0001f;
				if (flag2)
				{
					return true;
				}
			}
			result = false;
		}
		return result;
	}

	private static MineralSerializedData[] ToArr(Dictionary<XYZ, BtcReplayAnchor.MV> d)
	{
		MineralSerializedData[] array = new MineralSerializedData[d.Count];
		int num = 0;
		foreach (KeyValuePair<XYZ, BtcReplayAnchor.MV> keyValuePair in d)
		{
			array[num].position = keyValuePair.Key;
			array[num].growth = keyValuePair.Value.g;
			array[num].variation = keyValuePair.Value.v;
			num++;
		}
		return array;
	}

	private static void ApplyState(BtcReplayAnchor.NState t)
	{
		BtcReplayAnchor._touched.Clear();
		BtcReplayAnchor.NState nstate = BtcReplayAnchor.ReadLive();
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		bool flag = t.trees != null && nstate.trees != null;
		if (flag)
		{
			TreeRegistry instance = TreeRegistry.Instance;
			List<XYZ> list = new List<XYZ>();
			List<KeyValuePair<XYZ, int>> list2 = new List<KeyValuePair<XYZ, int>>();
			foreach (KeyValuePair<XYZ, int> keyValuePair in nstate.trees)
			{
				int num4;
				bool flag2 = !t.trees.TryGetValue(keyValuePair.Key, out num4);
				if (flag2)
				{
					list.Add(keyValuePair.Key);
				}
				else
				{
					bool flag3 = num4 != keyValuePair.Value;
					if (flag3)
					{
						list.Add(keyValuePair.Key);
						list2.Add(new KeyValuePair<XYZ, int>(keyValuePair.Key, num4));
					}
				}
			}
			foreach (KeyValuePair<XYZ, int> item in t.trees)
			{
				bool flag4 = !nstate.trees.ContainsKey(item.Key);
				if (flag4)
				{
					list2.Add(item);
				}
			}
			for (int i = 0; i < list.Count; i++)
			{
				instance.Remove(list[i]);
				BtcReplayAnchor._touched.Add(list[i]);
			}
			for (int j = 0; j < list2.Count; j++)
			{
				bool flag5 = !instance.Add(list2[j].Key, list2[j].Value);
				if (flag5)
				{
					num3++;
				}
				BtcReplayAnchor._touched.Add(list2[j].Key);
			}
			num = list.Count;
			num2 = list2.Count;
		}
		int num5 = 0;
		int num6 = 0;
		List<XYZ> list3 = new List<XYZ>();
		List<KeyValuePair<XYZ, BtcReplayAnchor.PV>> list4 = new List<KeyValuePair<XYZ, BtcReplayAnchor.PV>>();
		foreach (KeyValuePair<XYZ, BtcReplayAnchor.PV> keyValuePair2 in nstate.plants)
		{
			BtcReplayAnchor.PV pv;
			bool flag6 = !t.plants.TryGetValue(keyValuePair2.Key, out pv);
			if (flag6)
			{
				list3.Add(keyValuePair2.Key);
			}
			else
			{
				bool flag7 = pv.t != keyValuePair2.Value.t || Mathf.Abs(pv.s - keyValuePair2.Value.s) > 0.0001f;
				if (flag7)
				{
					list3.Add(keyValuePair2.Key);
					list4.Add(new KeyValuePair<XYZ, BtcReplayAnchor.PV>(keyValuePair2.Key, pv));
				}
			}
		}
		foreach (KeyValuePair<XYZ, BtcReplayAnchor.PV> item2 in t.plants)
		{
			bool flag8 = !nstate.plants.ContainsKey(item2.Key);
			if (flag8)
			{
				list4.Add(item2);
			}
		}
		for (int k = 0; k < list3.Count; k++)
		{
			PlantRegistry.Remove(list3[k]);
			BtcReplayAnchor._touched.Add(list3[k]);
		}
		for (int l = 0; l < list4.Count; l++)
		{
			PlantRegistry.Add(list4[l].Key, list4[l].Value.t, list4[l].Value.s);
			BtcReplayAnchor._touched.Add(list4[l].Key);
		}
		num5 = list3.Count;
		num6 = list4.Count;
		List<XYZ> list5 = new List<XYZ>();
		List<BtcReplayAnchor.BKey> list6 = new List<BtcReplayAnchor.BKey>();
		foreach (KeyValuePair<BtcReplayAnchor.BKey, BtcReplayAnchor.BVal> keyValuePair3 in nstate.boulders)
		{
			BtcReplayAnchor.BVal bval;
			bool flag9 = !t.boulders.TryGetValue(keyValuePair3.Key, out bval);
			if (flag9)
			{
				list5.Add(keyValuePair3.Value.root);
			}
			else
			{
				bool flag10 = Mathf.Abs(bval.growth - keyValuePair3.Value.growth) > 0.0001f || Mathf.Abs(bval.damage - keyValuePair3.Value.damage) > 0.0001f;
				if (flag10)
				{
					list5.Add(keyValuePair3.Value.root);
					list6.Add(keyValuePair3.Key);
				}
			}
		}
		foreach (KeyValuePair<BtcReplayAnchor.BKey, BtcReplayAnchor.BVal> keyValuePair4 in t.boulders)
		{
			bool flag11 = !nstate.boulders.ContainsKey(keyValuePair4.Key);
			if (flag11)
			{
				list6.Add(keyValuePair4.Key);
			}
		}
		for (int m = 0; m < list5.Count; m++)
		{
			BoulderRegistry.Remove(list5[m]);
			BtcReplayAnchor._touched.Add(list5[m]);
		}
		for (int n = 0; n < list6.Count; n++)
		{
			BtcReplayAnchor.BKey bkey = list6[n];
			BoulderRegistry.Add(new Vector3(bkey.cx, bkey.cy, bkey.cz), t.boulders[bkey].growth, new Vector3(bkey.ox, bkey.oy, bkey.oz), (BoulderSize)bkey.sz);
			BtcReplayAnchor._touched.Add(t.boulders[bkey].root);
		}
		bool flag12 = BtcReplayAnchor.MinDiffers(nstate.iron, t.iron);
		bool flag13 = BtcReplayAnchor.MinDiffers(nstate.pyr, t.pyr);
		bool flag14 = flag12;
		if (flag14)
		{
			foreach (XYZ xyz in nstate.iron.Keys)
			{
				IronRegistry.Remove(xyz);
				BtcReplayAnchor._touched.Add(xyz);
			}
			IronRegistry.BuildMineralsFromSerialized(BtcReplayAnchor.ToArr(t.iron));
			foreach (XYZ item3 in t.iron.Keys)
			{
				BtcReplayAnchor._touched.Add(item3);
			}
		}
		bool flag15 = flag13;
		if (flag15)
		{
			foreach (XYZ xyz2 in nstate.pyr.Keys)
			{
				PyriteRegistry.Remove(xyz2);
				BtcReplayAnchor._touched.Add(xyz2);
			}
			PyriteRegistry.BuildMineralsFromSerialized(BtcReplayAnchor.ToArr(t.pyr));
			foreach (XYZ item4 in t.pyr.Keys)
			{
				BtcReplayAnchor._touched.Add(item4);
			}
		}
		BtcReplayAnchor.Log(string.Concat(new object[]
		{
			"nature: trees -",
			num,
			" +",
			num2,
			" (skipped ",
			num3,
			"), plants -",
			num5,
			" +",
			num6,
			", boulders -",
			list5.Count,
			" +",
			list6.Count,
			", iron ",
			flag12 ? "rebuilt" : "same",
			", pyrite ",
			flag13 ? "rebuilt" : "same"
		}));
		BtcReplayAnchor.NState nstate2 = BtcReplayAnchor.ReadLive();
		int num7 = 0;
		bool flag16 = t.trees != null && nstate2.trees != null;
		if (flag16)
		{
			num7 += BtcReplayAnchor.Diff(t.trees, nstate2.trees, "trees");
		}
		num7 += Math.Abs(t.plants.Count - nstate2.plants.Count);
		num7 += Math.Abs(t.boulders.Count - nstate2.boulders.Count);
		bool flag17 = num7 != 0;
		if (flag17)
		{
			BtcReplayAnchor.Log(string.Concat(new object[]
			{
				"VERIFY: registry differs from the target by ",
				num7,
				" entries (trees ",
				(nstate2.trees != null) ? nstate2.trees.Count : -1,
				"/",
				(t.trees != null) ? t.trees.Count : -1,
				", plants ",
				nstate2.plants.Count,
				"/",
				t.plants.Count,
				", boulders ",
				nstate2.boulders.Count,
				"/",
				t.boulders.Count,
				")"
			}));
		}
		else
		{
			BtcReplayAnchor.Log("verify: registries match the target");
		}
	}

	private static int Diff(Dictionary<XYZ, int> a, Dictionary<XYZ, int> b, string what)
	{
		int num = 0;
		foreach (KeyValuePair<XYZ, int> keyValuePair in a)
		{
			int num2;
			bool flag = !b.TryGetValue(keyValuePair.Key, out num2) || num2 != keyValuePair.Value;
			if (flag)
			{
				bool flag2 = num < 5;
				if (flag2)
				{
					BtcReplayAnchor.Log(string.Concat(new object[]
					{
						"  ",
						what,
						" mismatch at ",
						keyValuePair.Key.x,
						",",
						keyValuePair.Key.y,
						",",
						keyValuePair.Key.z
					}));
				}
				num++;
			}
		}
		foreach (KeyValuePair<XYZ, int> keyValuePair2 in b)
		{
			bool flag3 = !a.ContainsKey(keyValuePair2.Key);
			if (flag3)
			{
				num++;
			}
		}
		return num;
	}

	private static void Resync()
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		try
		{
			Brix.Engine.Architecte.AppliquerChangements();
		}
		catch (Exception arg)
		{
			BtcReplayAnchor.Log("AppliquerChangements failed: " + arg);
		}
		HashSet<int> hashSet = new HashSet<int>();
		bool forceRebuild = BtcReplayAnchor._forceRebuild;
		if (forceRebuild)
		{
			for (int i = 0; i < BtcReplayAnchor._touched.Count; i++)
			{
				XYZ p = BtcReplayAnchor._touched[i];
				int gscalaire = Brix.Engine.Abstract.Voxel.GetGScalaire(p.ToScalaire());
				bool flag = hashSet.Add(gscalaire);
				if (flag)
				{
					Brix.Engine.Architecte.DirtyVoxel(p);
				}
			}
		}
		int num = 0;
		bool resyncAll = BtcReplayAnchor._resyncAll;
		if (resyncAll)
		{
			foreach (KeyValuePair<int, MegaVoxel> keyValuePair in Brix.Engine.Architecte._megaVoxels)
			{
				bool hasNature = keyValuePair.Value.HasNature;
				if (hasNature)
				{
					hashSet.Add(keyValuePair.Key);
				}
			}
		}
		foreach (int num2 in hashSet)
		{
			try
			{
				MegaVoxel megaVoxel = Brix.Engine.Architecte.TryGetMegaVoxel(num2);
				bool flag2 = megaVoxel != null;
				if (flag2)
				{
					megaVoxel.UpdateNatureLists();
					num++;
				}
			}
			catch (Exception ex)
			{
				BtcReplayAnchor.Log(string.Concat(new object[]
				{
					"UpdateNatureLists ",
					num2,
					": ",
					ex.Message
				}));
			}
		}
		BtcReplayAnchor.Log(string.Concat(new object[]
		{
			"resync: ",
			BtcReplayAnchor._touched.Count,
			" positions touched, ",
			num,
			" chunks rebuilt, ",
			stopwatch.ElapsedMilliseconds,
			" ms"
		}));
		BtcReplayAnchor._touched.Clear();
	}

	public static void ApplyWorld()
	{
		BtcReplayAnchor.EnsureCfg();
		Stopwatch stopwatch = Stopwatch.StartNew();
		try
		{
			BtcReplayAnchor.Sweep();
			BtcReplayAnchor.NState nstate = BtcReplayAnchor._base;
			bool flag = BtcReplayAnchor.Selected != 0 && BtcReplayAnchor._selIdx >= 0;
			if (flag)
			{
				BtcReplayAnchor.ApplyTerrain(BtcReplayAnchor._terr[BtcReplayAnchor._selIdx]);
				nstate = BtcReplayAnchor.ReadNature(BtcReplayAnchor._nat[BtcReplayAnchor._selIdx]);
			}
			bool flag2 = nstate != null;
			if (flag2)
			{
				BtcReplayAnchor.ApplyState(nstate);
			}
			BtcReplayAnchor.Resync();
			BtcReplayAnchor.Log(string.Concat(new object[]
			{
				"world prepared for the rewind in ",
				stopwatch.ElapsedMilliseconds,
				" ms",
				(BtcReplayAnchor.Selected != 0) ? (", anchor " + BtcReplayAnchor.Selected) : ", baseline"
			}));
		}
		catch (Exception arg)
		{
			BtcReplayAnchor.Log("ApplyWorld: " + arg);
			bool flag3 = BtcReplayAnchor.Selected != 0;
			if (flag3)
			{
				BtcReplayAnchor._failed = true;
				BtcReplayAnchor.Selected = 0;
				BtcReplayAnchor._selIdx = -1;
				try
				{
					BtcTerrainUndo.Restore();
					BtcReplayBoost.RestoreNature();
				}
				catch (Exception ex)
				{
					BtcReplayAnchor.Log("rollback: " + ex.Message);
				}
			}
		}
	}

	private static bool _cfg;

	private static bool _enabled = true;

	private static bool _cacheOn = true;

	private static bool _sweepOn = true;

	private static bool _forceRebuild = true;

	private static bool _resyncAll;

	private static readonly List<BtcReplayAnchor.Kf> _kf = new List<BtcReplayAnchor.Kf>();

	private static byte[][] _terr;

	private static byte[][] _nat;

	private static byte[] _buf;

	private static string _name = "replay";

	private static ulong _hash;

	public static int Selected;

	private static int _selIdx = -1;

	private static int _active;

	private static bool _inWin;

	private static bool _failed;

	private static int _state;

	private static float _tReady;

	private static float _tSeek;

	private static bool _sawSeek;

	private static bool _bound;

	private static BtcReplayAnchor.FixD _fix;

	private static BtcReplayAnchor.FireD _fire;

	private static FieldInfo _fX0;

	private static FieldInfo _fZ0;

	private static FieldInfo _fW;

	private static FieldInfo _fH;

	private static FieldInfo _fBot;

	private static FieldInfo _fTop;

	private static FieldInfo _fTyp;

	private static FieldInfo _fCrf;

	private static FieldInfo _fVs;

	private static FieldInfo _fVt;

	private static FieldInfo _fVv;

	private static FieldInfo _fVr;

	private static FieldInfo _fHas;

	private static FieldInfo _fHasVox;

	private static BtcReplayAnchor.NState _base;

	private static FieldInfo _treeField;

	private static readonly List<XYZ> _touched = new List<XYZ>();

	private struct Kf
	{
		public int id;

		public int startPos;

		public long msBefore;

		public long msStart;

		public bool ended;
	}

	private delegate bool FixD(int i, List<int> idx, List<int> oldTop);

	private delegate void FireD(int x, int y, int z);

	private struct PV
	{
		public int t;

		public float s;
	}

	private struct MV
	{
		public float g;

		public int v;

		public float d;
	}

	private struct BKey : IEquatable<BtcReplayAnchor.BKey>
	{
		public bool Equals(BtcReplayAnchor.BKey o)
		{
			return this.cx == o.cx && this.cy == o.cy && this.cz == o.cz && this.ox == o.ox && this.oy == o.oy && this.oz == o.oz && this.sz == o.sz;
		}

		public override bool Equals(object o)
		{
			return o is BtcReplayAnchor.BKey && this.Equals((BtcReplayAnchor.BKey)o);
		}

		public override int GetHashCode()
		{
			int num = this.cx.GetHashCode();
			num = num * 31 + this.cy.GetHashCode();
			num = num * 31 + this.cz.GetHashCode();
			num = num * 31 + this.ox.GetHashCode();
			num = num * 31 + this.oy.GetHashCode();
			num = num * 31 + this.oz.GetHashCode();
			return num * 31 + this.sz;
		}

		public float cx;

		public float cy;

		public float cz;

		public float ox;

		public float oy;

		public float oz;

		public int sz;
	}

	private struct BVal
	{
		public float growth;

		public XYZ root;

		public float damage;
	}

	private sealed class NState
	{
		public Dictionary<XYZ, int> trees;

		public Dictionary<XYZ, BtcReplayAnchor.PV> plants;

		public Dictionary<BtcReplayAnchor.BKey, BtcReplayAnchor.BVal> boulders;

		public Dictionary<XYZ, BtcReplayAnchor.MV> iron;

		public Dictionary<XYZ, BtcReplayAnchor.MV> pyr;
	}
}

[HarmonyLib.HarmonyPatch(typeof(DamagedTree), "Cut")]
internal static class BtcTreeCutSkipPatch
{
	public static readonly List<DamagedTree> Skipped = new List<DamagedTree>();
	public static readonly FieldInfo ReadyField = typeof(DamagedTree).GetField("_ready", BindingFlags.Instance | BindingFlags.NonPublic);
	public static readonly FieldInfo CutField = typeof(DamagedTree).GetField("_wasCut", BindingFlags.Instance | BindingFlags.NonPublic);
	public static readonly FieldInfo PosField = typeof(DamagedTree).GetField("_pos", BindingFlags.Instance | BindingFlags.NonPublic);

	// Vanilla Cut returns early when the tree is not ready yet (fast replay seek); remember such trees for AfterSeekForward.
	private static void Prefix(DamagedTree __instance)
	{
		try
		{
			if (!BtcReplay.Active || ReadyField == null || CutField == null) return;
			if ((bool)CutField.GetValue(__instance) || (bool)ReadyField.GetValue(__instance)) return;
			if (!Skipped.Contains(__instance)) Skipped.Add(__instance);
		}
		catch (Exception e) { BtcLog.Error("Hook", "DamagedTree.Cut skip: " + e.Message); }
	}
}
}
