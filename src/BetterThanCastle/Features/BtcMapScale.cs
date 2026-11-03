using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Brix.Assets;
using Brix.Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
public static class BtcMapScale
{
	private static void Defaults()
	{
		BtcMapScale.Pct = 100;
		BtcMapScale._seed = 0;
		BtcMapScale.DefaultsAdvanced();
	}

	private static void DefaultsAdvanced()
	{
		BtcMapScale._dTrees = 100;
		BtcMapScale._dPlants = 100;
		BtcMapScale._dIron = 100;
		BtcMapScale._dPyr = 100;
		BtcMapScale._dBoul = 100;
		BtcMapScale._mOre = true;
		BtcMapScale._hTol = 8;
		BtcMapScale._spread = 6;
		BtcMapScale._mRes = true;
		BtcMapScale._dRes = 100;
		BtcMapScale._vox = true;
		BtcMapScale._mPlayer = false;
		BtcMapScale._mCrystal = false;
		BtcMapScale._mCorr = false;
	}

	private static string Encode()
	{
		return string.Concat(new object[]
		{
			BtcMapScale.Pct,
			"|",
			BtcMapScale._seed,
			"|",
			BtcMapScale._dTrees,
			"|",
			BtcMapScale._dPlants,
			"|",
			BtcMapScale._dIron,
			"|",
			BtcMapScale._dPyr,
			"|",
			BtcMapScale._dBoul,
			"|",
			BtcMapScale._mRes ? 1 : 0,
			"|",
			BtcMapScale._dRes,
			"|",
			BtcMapScale._vox ? 1 : 0,
			"|",
			BtcMapScale._mPlayer ? 1 : 0,
			"|",
			BtcMapScale._mCrystal ? 1 : 0,
			"|",
			BtcMapScale._mCorr ? 1 : 0,
			"|",
			BtcMapScale._mOre ? 1 : 0,
			"|",
			BtcMapScale._hTol,
			"|",
			BtcMapScale._spread
		});
	}

	private static bool Decode(string s)
	{
		bool result;
		try
		{
			string[] array = (s ?? string.Empty).Split(new char[]
			{
				'|'
			});
			bool flag = array.Length < 13;
			if (flag)
			{
				result = false;
			}
			else
			{
				BtcMapScale.Pct = Mathf.Clamp(int.Parse(array[0]), 10, 10000);
				BtcMapScale._seed = Mathf.Clamp(int.Parse(array[1]), 0, 999999);
				BtcMapScale._dTrees = Mathf.Clamp(int.Parse(array[2]), 0, 2000);
				BtcMapScale._dPlants = Mathf.Clamp(int.Parse(array[3]), 0, 2000);
				BtcMapScale._dIron = Mathf.Clamp(int.Parse(array[4]), 0, 2000);
				BtcMapScale._dPyr = Mathf.Clamp(int.Parse(array[5]), 0, 2000);
				BtcMapScale._dBoul = Mathf.Clamp(int.Parse(array[6]), 0, 2000);
				BtcMapScale._mRes = (array[7] == "1");
				BtcMapScale._dRes = Mathf.Clamp(int.Parse(array[8]), 0, 2000);
				BtcMapScale._vox = (array[9] == "1");
				BtcMapScale._mPlayer = (array[10] == "1");
				BtcMapScale._mCrystal = (array[11] == "1");
				BtcMapScale._mCorr = (array[12] == "1");
				BtcMapScale._mOre = (array.Length < 14 || array[13] == "1");
				BtcMapScale._hTol = ((array.Length < 16) ? 8 : Mathf.Clamp(int.Parse(array[14]), 0, 255));
				BtcMapScale._spread = ((array.Length < 16) ? 6 : Mathf.Clamp(int.Parse(array[15]), 1, 64));
				result = true;
			}
		}
		catch (Exception)
		{
			result = false;
		}
		return result;
	}

	private static void LoadPrefsOnce()
	{
		bool prefsLoaded = BtcMapScale._prefsLoaded;
		if (!prefsLoaded)
		{
			BtcMapScale._prefsLoaded = true;
			BtcMapScale.Decode(PlayerPrefs.GetString("BtcMapScale", string.Empty));
		}
	}

	private static void Changed()
	{
		PlayerPrefs.SetString("BtcMapScale", BtcMapScale.Encode());
		BtcMapScale.Broadcast();
	}

	public static void RegisterHandlers(NetworkClient clientOverride)
	{
		bool active = NetworkServer.active;
		if (active)
		{
			BtcMapScale._prefsLoaded = false;
			BtcMapScale.LoadPrefsOnce();
		}
		else
		{
			BtcMapScale.Defaults();
			BtcMapScale._prefsLoaded = true;
		}
		UNetManager unetManager = NetworkManager.singleton as UNetManager;
		NetworkClient networkClient = clientOverride ?? ((unetManager != null) ? unetManager.LocalNetworkClient : null);
		bool flag = networkClient != null;
		if (flag)
		{
			networkClient.UnregisterHandler(97);
			networkClient.RegisterHandler(97, new NetworkMessageDelegate(BtcMapScale.OnState));
		}
	}

	public static void SendState(NetworkConnection conn)
	{
		bool flag = conn != null && BtcBc.BcEnabled;
		if (flag)
		{
			BtcMapScale.LoadPrefsOnce();
			conn.Send(97, new BtcMapScale.Msg
			{
				data = BtcMapScale.Encode()
			});
		}
	}

	private static void Broadcast()
	{
		bool flag = NetworkServer.active && BtcBc.BcEnabled;
		if (flag)
		{
			NetworkServer.SendToAll(97, new BtcMapScale.Msg
			{
				data = BtcMapScale.Encode()
			});
		}
	}

	private static void OnState(NetworkMessage netMsg)
	{
		try
		{
			BtcMapScale.Msg msg = netMsg.ReadMessage<BtcMapScale.Msg>();
			bool flag = !NetworkServer.active;
			if (flag)
			{
				BtcMapScale.Decode(msg.data);
			}
		}
		catch (Exception)
		{
		}
	}

	private static void Log(string s)
	{
		try
		{
			Directory.CreateDirectory(BtcPaths.Debug);
			File.AppendAllText(BtcPaths.DebugFile("btc_mapscale.txt"), DateTime.Now.ToString("HH:mm:ss") + " " + s + "\r\n");
		}
		catch (Exception)
		{
		}
	}

	private static string T(string en, string ru)
	{
		return BtcGameplay.T(en, ru);
	}

	private static string Hash(string s)
	{
		uint num = 2166136261U;
		for (int i = 0; i < s.Length; i++)
		{
			num ^= (uint)s[i];
			num *= 16777619U;
		}
		return num.ToString("x8");
	}

	private static float Rand(int a, int b, int c, int d)
	{
		uint num = 2166136261U;
		num = (num ^ (uint)BtcMapScale._seed) * 16777619U;
		num = (num ^ (uint)a) * 16777619U;
		num = (num ^ (uint)b) * 16777619U;
		num = (num ^ (uint)c) * 16777619U;
		num = (num ^ (uint)d) * 16777619U;
		num ^= num >> 15;
		num *= 2246822519U;
		num ^= num >> 13;
		num *= 3266489917U;
		num ^= num >> 16;
		return (num & 16777215U) / 16777216f;
	}

	private static void CopyDir(string from, string to)
	{
		foreach (string text in Directory.GetFiles(from))
		{
			File.Copy(text, Path.Combine(to, Path.GetFileName(text)), true);
		}
		foreach (string text2 in Directory.GetDirectories(from))
		{
			string text3 = Path.Combine(to, Path.GetFileName(text2));
			Directory.CreateDirectory(text3);
			BtcMapScale.CopyDir(text2, text3);
		}
	}

	private static void CleanOthers(string root, string keepName)
	{
		try
		{
			foreach (string path in Directory.GetDirectories(root))
			{
				bool flag = Path.GetFileName(path) != keepName;
				if (flag)
				{
					Directory.Delete(path, true);
				}
			}
		}
		catch (Exception)
		{
		}
	}

	private static void SetPath(Asset_Map map, string path)
	{
		string text = map.IsStaging() ? "_stagePath" : "_fullPath";
		FieldInfo fieldInfo = null;
		Type type = typeof(Asset_Map);
		while (type != null && fieldInfo == null)
		{
			fieldInfo = type.GetField(text, BindingFlags.Instance | BindingFlags.NonPublic);
			type = type.BaseType;
		}
		bool flag = fieldInfo == null;
		if (flag)
		{
			throw new Exception(text + " not found");
		}
		string text2 = Path.GetFullPath(path).Replace("\\", "/");
		bool flag2 = !text2.EndsWith("/");
		if (flag2)
		{
			text2 += "/";
		}
		fieldInfo.SetValue(map, text2);
	}

	private static void RestoreSymmetry()
	{
		try
		{
			bool flag = BtcMapScale._symRestore == null;
			if (flag)
			{
				BtcMapScale._symRestore = typeof(BtcSymmetry).GetMethod("Restore", BindingFlags.Static | BindingFlags.NonPublic);
			}
			bool flag2 = BtcMapScale._symRestore != null;
			if (flag2)
			{
				BtcMapScale._symRestore.Invoke(null, null);
			}
		}
		catch (Exception)
		{
		}
	}

	private static void Restore()
	{
		bool flag = BtcMapScale._swapped == null;
		if (!flag)
		{
			try
			{
				BtcMapScale.SetPath(BtcMapScale._swapped, BtcMapScale._origPath);
			}
			catch (Exception ex)
			{
				BtcMapScale.Log("Restore failed: " + ex.Message);
			}
			BtcMapScale._swapped = null;
			BtcMapScale._origPath = null;
		}
	}

	public static void Tick()
	{
		bool flag = BtcMapScale._swapped != null && BtcGameplay.InLobby;
		if (flag)
		{
			BtcMapScale.RestoreSymmetry();
			BtcMapScale.Restore();
		}
	}

	public static void Prepare()
	{
		BtcMapScale.RestoreSymmetry();
		BtcMapScale.Restore();
		try
		{
			bool flag = BtcMapScale.Pct == 100 || !BtcBc.BcEnabled;
			if (!flag)
			{
				Asset_Map map = GameParam.Map;
				bool flag2 = map == null || !map.IsNewGame();
				if (flag2)
				{
					BtcMapScale.Log("skip: not a new game");
				}
				else
				{
					string path = map.path;
					string path2 = Path.Combine(path, "Heightmap.png");
					bool flag3 = !File.Exists(path2);
					if (flag3)
					{
						BtcMapScale.Log("skip: no Heightmap.png in " + path);
					}
					else
					{
						string text = Path.Combine(BtcPaths.Root, "mapscale");
						string text2 = BtcMapScale.Hash(string.Concat(new object[]
						{
							path,
							"|",
							File.GetLastWriteTimeUtc(path2).Ticks,
							"|",
							4,
							"|",
							BtcMapScale.Encode()
						}));
						string text3 = Path.Combine(text, text2);
						bool flag4 = !File.Exists(Path.Combine(text3, ".ok"));
						if (flag4)
						{
							DateTime now = DateTime.Now;
							string text4 = text3 + "_tmp";
							bool flag5 = Directory.Exists(text4);
							if (flag5)
							{
								Directory.Delete(text4, true);
							}
							bool flag6 = Directory.Exists(text3);
							if (flag6)
							{
								Directory.Delete(text3, true);
							}
							Directory.CreateDirectory(text4);
							BtcMapScale.CopyDir(path, text4);
							BtcMapScale.Grid g = BtcMapScale.BuildHeightmap(path, Path.Combine(text4, "Heightmap.png"));
							BtcMapScale.BuildJson(text4, g);
							File.WriteAllText(Path.Combine(text4, ".ok"), "ok");
							Directory.Move(text4, text3);
							BtcMapScale.Log(string.Concat(new string[]
							{
								"built ",
								text2,
								" in ",
								(DateTime.Now - now).TotalMilliseconds.ToString("0"),
								" ms: ",
								BtcMapScale._info
							}));
						}
						else
						{
							BtcMapScale.Log("cache hit " + text2);
						}
						BtcMapScale.CleanOthers(text, text2);
						BtcMapScale.SetPath(map, text3);
						BtcMapScale._swapped = map;
						BtcMapScale._origPath = path;
					}
				}
			}
		}
		catch (Exception ex)
		{
			BtcMapScale._info = "FAILED: " + ex.Message;
			BtcMapScale.Log("Prepare failed: " + ex);
			BtcMapScale.Restore();
		}
	}

	private static void Bounds(Color32[] px, int w, int h, out int minX, out int minZ, out int maxX, out int maxZ)
	{
		minX = w;
		minZ = h;
		maxX = -1;
		maxZ = -1;
		for (int i = 0; i < h; i++)
		{
			int num = i * w;
			for (int j = 0; j < w; j++)
			{
				Color32 color = px[num + j];
				bool flag = (color.r | color.g | color.b | color.a) > 0;
				if (flag)
				{
					bool flag2 = j < minX;
					if (flag2)
					{
						minX = j;
					}
					bool flag3 = j > maxX;
					if (flag3)
					{
						maxX = j;
					}
					bool flag4 = i < minZ;
					if (flag4)
					{
						minZ = i;
					}
					bool flag5 = i > maxZ;
					if (flag5)
					{
						maxZ = i;
					}
				}
			}
		}
	}

	private static BtcMapScale.Grid BuildHeightmap(string orig, string outPath)
	{
		Texture2D texture2D = new Texture2D(2, 2, TextureFormat.RGBA32, false);
		texture2D.LoadImage(File.ReadAllBytes(Path.Combine(orig, "Heightmap.png")));
		BtcMapScale.Grid grid = new BtcMapScale.Grid();
		grid.w = texture2D.width;
		grid.h = texture2D.height;
		grid.src = texture2D.GetPixels32();
		UnityEngine.Object.Destroy(texture2D);
		BtcMapScale.Bounds(grid.src, grid.w, grid.h, out grid.minX, out grid.minZ, out grid.maxX, out grid.maxZ);
		bool flag = grid.maxX < grid.minX;
		if (flag)
		{
			throw new Exception("empty heightmap");
		}
		grid.k = (float)BtcMapScale.Pct / 100f;
		int num = grid.maxX - grid.minX + 1;
		int num2 = grid.maxZ - grid.minZ + 1;
		grid.W = Mathf.Max(1, Mathf.CeilToInt((float)num * grid.k));
		grid.H = Mathf.Max(1, Mathf.CeilToInt((float)num2 * grid.k));
		grid.nw = Mathf.Max(grid.w, grid.W + 32);
		grid.nh = Mathf.Max(grid.h, grid.H + 32);
		bool flag2 = grid.nw > 16384 || grid.nh > 16384 || (long)grid.nw * (long)grid.nh > 100000000L;
		if (flag2)
		{
			throw new Exception(string.Concat(new object[]
			{
				"scaled map is too large: ",
				grid.W,
				"x",
				grid.H,
				" px"
			}));
		}
		float num3 = (float)(grid.minX + grid.maxX + 1) * 0.5f;
		float num4 = (float)(grid.minZ + grid.maxZ + 1) * 0.5f;
		grid.nx0 = Mathf.Clamp(Mathf.RoundToInt(num3 - (float)grid.W * 0.5f), 0, grid.nw - grid.W);
		grid.nz0 = Mathf.Clamp(Mathf.RoundToInt(num4 - (float)grid.H * 0.5f), 0, grid.nh - grid.H);
		int[] array = new int[grid.W];
		for (int i = 0; i < grid.W; i++)
		{
			array[i] = Mathf.Clamp(grid.minX + Mathf.FloorToInt(((float)i + 0.5f) / grid.k), grid.minX, grid.maxX);
		}
		int[] array2 = new int[grid.H];
		for (int j = 0; j < grid.H; j++)
		{
			array2[j] = Mathf.Clamp(grid.minZ + Mathf.FloorToInt(((float)j + 0.5f) / grid.k), grid.minZ, grid.maxZ);
		}
		Color32[] array3 = new Color32[grid.nw * grid.nh];
		for (int k = 0; k < grid.H; k++)
		{
			int num5 = array2[k] * grid.w;
			int num6 = (grid.nz0 + k) * grid.nw + grid.nx0;
			for (int l = 0; l < grid.W; l++)
			{
				array3[num6 + l] = grid.src[num5 + array[l]];
			}
		}
		Texture2D texture2D2 = new Texture2D(grid.nw, grid.nh, TextureFormat.RGBA32, false);
		texture2D2.SetPixels32(array3);
		texture2D2.Apply(false);
		File.WriteAllBytes(outPath, texture2D2.EncodeToPNG());
		UnityEngine.Object.Destroy(texture2D2);
		grid.dst = array3;
		BtcMapScale._info = string.Concat(new object[]
		{
			"x",
			grid.k.ToString("0.##"),
			": ",
			num,
			"x",
			num2,
			" -> ",
			grid.W,
			"x",
			grid.H,
			" px, image ",
			grid.w,
			"x",
			grid.h,
			" -> ",
			grid.nw,
			"x",
			grid.nh
		});
		bool flag3 = grid.nw > 1024 || grid.nh > 1024;
		if (flag3)
		{
			BtcMapScale._info += " (above 1024 px: minimap is built for 1024, may need a separate hook)";
		}
		return grid;
	}

	private static double Num(JToken t)
	{
		bool flag = t == null || t.Type == JTokenType.Null;
		double result;
		if (flag)
		{
			result = 0.0;
		}
		else
		{
			result = (double)t;
		}
		return result;
	}

	private static JToken SetNum(JToken old, double v)
	{
		bool flag = old != null && old.Type == JTokenType.Integer;
		JToken result;
		if (flag)
		{
			result = new JValue((long)Math.Round(v));
		}
		else
		{
			result = new JValue(v);
		}
		return result;
	}

	private static string Str(JToken t)
	{
		return (t != null && t.Type == JTokenType.String) ? ((string)t) : null;
	}

	private static JObject PosObj(JObject o, string key)
	{
		bool flag = o == null;
		JObject result;
		if (flag)
		{
			result = null;
		}
		else
		{
			bool flag2 = key == null;
			if (flag2)
			{
				result = o;
			}
			else
			{
				result = (o[key] as JObject);
			}
		}
		return result;
	}

	private static void PutXYZ(JObject p, double x, double y, double z)
	{
		p["x"] = BtcMapScale.SetNum(p["x"], x);
		bool flag = p["y"] != null;
		if (flag)
		{
			p["y"] = BtcMapScale.SetNum(p["y"], y);
		}
		p["z"] = BtcMapScale.SetNum(p["z"], z);
	}

	private static int PixelG(Color32[] px, int w, int h, int x, int z)
	{
		bool flag = x < 0 || z < 0 || x >= w || z >= h;
		int result;
		if (flag)
		{
			result = -1;
		}
		else
		{
			result = (int)px[z * w + x].g;
		}
		return result;
	}

	private static double NewY(BtcMapScale.Grid g, double ox, double oy, double oz, double X, double Z)
	{
		int num = BtcMapScale.PixelG(g.src, g.w, g.h, (int)Math.Floor(ox), (int)Math.Floor(oz));
		int num2 = BtcMapScale.PixelG(g.dst, g.nw, g.nh, (int)Math.Floor(X), (int)Math.Floor(Z));
		bool flag = num > 0 && num2 > 0;
		double result;
		if (flag)
		{
			result = oy + (double)(num2 - num);
		}
		else
		{
			result = oy;
		}
		return result;
	}

	private static int Count(BtcMapScale.Grid g, int dens, int a, int b, int salt)
	{
		double num = (double)g.k * (double)g.k * (double)dens / 100.0;
		int num2 = (int)Math.Floor(num);
		double num3 = num - (double)num2;
		bool flag = (double)BtcMapScale.Rand(a, b, -7, salt) < num3;
		if (flag)
		{
			num2++;
		}
		return num2;
	}

	private static int MarkerDens(string group)
	{
		bool flag = group == "Player Starting Point";
		int result;
		if (flag)
		{
			result = (BtcMapScale._mPlayer ? 100 : -1);
		}
		else
		{
			bool flag2 = group == "Crystal";
			if (flag2)
			{
				result = (BtcMapScale._mCrystal ? 100 : -1);
			}
			else
			{
				bool flag3 = group == "Corruptron Spawn Point";
				if (flag3)
				{
					result = (BtcMapScale._mCorr ? 100 : -1);
				}
				else
				{
					result = -1;
				}
			}
		}
		return result;
	}

	private static void BuildJson(string dir, BtcMapScale.Grid g)
	{
		BtcMapScale.ProcessList(g, dir, "trees.json", null, null, (JObject o) => BtcMapScale._dTrees, 11);
		BtcMapScale.ProcessList(g, dir, "plants.json", null, null, (JObject o) => BtcMapScale._dPlants, 12);
		BtcMapScale.ProcessClusters(g, dir, "iron.json", "position", (JObject o) => BtcMapScale._mOre ? BtcMapScale._dIron : -1, 13);
		BtcMapScale.ProcessClusters(g, dir, "pyrite.json", "position", (JObject o) => BtcMapScale._mOre ? BtcMapScale._dPyr : -1, 14);
		BtcMapScale.ProcessList(g, dir, "boulders.json", "displayPos", null, (JObject o) => BtcMapScale._dBoul, 15);
		BtcMapScale.ProcessList(g, dir, "PlacedBlocks.json", "P", null, (JObject o) => -1, 16);
		BtcMapScale.ProcessList(g, dir, "LuaMarkersCount.json", "position", "localPosition", (JObject o) => BtcMapScale.MarkerDens(BtcMapScale.MarkerGroup(o)), 9001);
		BtcMapScale.ProcessVoxels(g, dir);
		BtcMapScale.ProcessGameObjects(g, dir);
		string text = Path.Combine(dir, "TerrainSurfaceLayer.json");
		bool flag = File.Exists(text) && new FileInfo(text).Length > 80L;
		if (flag)
		{
			BtcMapScale.Log("WARNING: TerrainSurfaceLayer.json is not empty, it is NOT scaled");
		}
	}

	private static string MarkerGroup(JObject o)
	{
		JObject jobject = o["markerGroups"] as JObject;
		JArray jarray = (jobject != null) ? (jobject["$values"] as JArray) : (o["markerGroups"] as JArray);
		bool flag = jarray != null && jarray.Count > 0;
		string result;
		if (flag)
		{
			result = BtcMapScale.Str(jarray[0]);
		}
		else
		{
			result = null;
		}
		return result;
	}

	private static void ProcessList(BtcMapScale.Grid g, string dir, string name, string key, string key2, BtcMapScale.DensFn densFn, int salt)
	{
		string path = Path.Combine(dir, name);
		bool flag = !File.Exists(path);
		if (!flag)
		{
			JArray jarray = JArray.Parse(File.ReadAllText(path));
			JArray jarray2 = new JArray();
			HashSet<long> hashSet = new HashSet<long>();
			int num = 0;
			int num2 = 0;
			int[] array = new int[jarray.Count];
			int[] array2 = new int[jarray.Count];
			List<double[]> list = new List<double[]>();
			for (int i = 0; i < jarray.Count; i++)
			{
				array[i] = -1;
				array2[i] = -1;
				JObject o = jarray[i] as JObject;
				JObject jobject = BtcMapScale.PosObj(o, key);
				bool flag2 = jobject == null || jobject["x"] == null || jobject["z"] == null;
				if (!flag2)
				{
					array[i] = densFn(o);
					bool flag3 = array[i] >= 0;
					if (flag3)
					{
						array2[i] = list.Count;
						list.Add(new double[]
						{
							BtcMapScale.Num(jobject["x"]),
							BtcMapScale.Num(jobject["z"])
						});
					}
				}
			}
			float[] array3 = (list.Count > 0 && g.k > 1.0001f) ? BtcMapScale.Spacings(g, list) : new float[list.Count];
			BtcMapScale.PGrid pg = (key2 == null && list.Count > 0 && g.k > 1.0001f) ? new BtcMapScale.PGrid(g, list.Count) : null;
			for (int j = 0; j < jarray.Count; j++)
			{
				JObject jobject2 = jarray[j] as JObject;
				JObject jobject3 = BtcMapScale.PosObj(jobject2, key);
				bool flag4 = jobject3 == null || jobject3["x"] == null || jobject3["z"] == null;
				if (flag4)
				{
					jarray2.Add(jarray[j].DeepClone());
				}
				else
				{
					double num3 = BtcMapScale.Num(jobject3["x"]);
					double oy = BtcMapScale.Num(jobject3["y"]);
					double num4 = BtcMapScale.Num(jobject3["z"]);
					int num5 = array[j];
					bool flag5 = num5 >= 0;
					bool isInt = jobject3["x"].Type == JTokenType.Integer || key2 != null;
					double sp = flag5 ? ((double)array3[array2[j]]) : 0.0;
					int num6 = flag5 ? BtcMapScale.Count(g, num5, (int)Math.Round(num3), (int)Math.Round(num4), salt) : 1;
					bool flag6 = num6 == 0;
					if (flag6)
					{
						num++;
					}
					for (int k = 0; k < num6; k++)
					{
						double x;
						double z;
						bool flag7 = !BtcMapScale.Place(g, num3, num4, sp, k, num6, flag5, isInt, salt, (key2 == null) ? hashSet : null, pg, null, out x, out z);
						if (flag7)
						{
							num++;
						}
						else
						{
							double y = BtcMapScale.NewY(g, num3, oy, num4, x, z);
							JObject jobject4 = (JObject)jobject2.DeepClone();
							BtcMapScale.PutXYZ(BtcMapScale.PosObj(jobject4, key), x, y, z);
							bool flag8 = key2 != null;
							if (flag8)
							{
								JObject jobject5 = BtcMapScale.PosObj(jobject4, key2);
								bool flag9 = jobject5 != null && jobject5["x"] != null;
								if (flag9)
								{
									BtcMapScale.PutXYZ(jobject5, x, y, z);
								}
							}
							jarray2.Add(jobject4);
							num2++;
						}
					}
				}
			}
			File.WriteAllText(path, jarray2.ToString(Formatting.None, new JsonConverter[0]));
			BtcMapScale.Log(string.Concat(new object[]
			{
				name,
				": ",
				jarray.Count,
				" -> ",
				jarray2.Count,
				" (dropped ",
				num,
				", copies ",
				num2,
				")"
			}));
		}
	}

	private static void ProcessVoxels(BtcMapScale.Grid g, string dir)
	{
		string path = Path.Combine(dir, "RemovedVoxels.json");
		bool flag = !File.Exists(path);
		if (!flag)
		{
			JArray jarray = JArray.Parse(File.ReadAllText(path));
			JArray jarray2 = new JArray();
			HashSet<long> hashSet = new HashSet<long>();
			double num = (double)g.k;
			for (int i = 0; i < jarray.Count; i++)
			{
				JObject jobject = jarray[i] as JObject;
				bool flag2 = jobject == null || jobject["x"] == null || jobject["z"] == null;
				if (flag2)
				{
					jarray2.Add(jarray[i].DeepClone());
				}
				else
				{
					double num2 = BtcMapScale.Num(jobject["x"]);
					double num3 = BtcMapScale.Num(jobject["y"]);
					double num4 = BtcMapScale.Num(jobject["z"]);
					double num5 = (double)g.nx0 + (num2 - (double)g.minX) * num;
					double num6 = (double)g.nz0 + (num4 - (double)g.minZ) * num;
					int num7 = (int)Math.Floor(num5 + 0.0001);
					int num8 = (int)Math.Floor(num6 + 0.0001);
					int num9 = num7 + 1;
					int num10 = num8 + 1;
					bool flag3 = BtcMapScale._vox && num > 1.0001;
					if (flag3)
					{
						num9 = (int)Math.Ceiling(num5 + num - 0.0001);
						num10 = (int)Math.Ceiling(num6 + num - 0.0001);
					}
					for (int j = num7; j < num9; j++)
					{
						for (int k = num8; k < num10; k++)
						{
							bool flag4 = j < g.nx0 || k < g.nz0 || j >= g.nx0 + g.W || k >= g.nz0 + g.H;
							if (!flag4)
							{
								long item = (long)j << 40 | (long)k << 16 | (long)((int)num3 & 65535);
								bool flag5 = !hashSet.Add(item);
								if (!flag5)
								{
									JObject jobject2 = (JObject)jobject.DeepClone();
									jobject2["x"] = BtcMapScale.SetNum(jobject2["x"], (double)j);
									jobject2["z"] = BtcMapScale.SetNum(jobject2["z"], (double)k);
									bool flag6 = jobject2["y"] != null;
									if (flag6)
									{
										jobject2["y"] = BtcMapScale.SetNum(jobject2["y"], BtcMapScale.NewY(g, num2, num3, num4, (double)j, (double)k));
									}
									jarray2.Add(jobject2);
								}
							}
						}
					}
				}
			}
			File.WriteAllText(path, jarray2.ToString(Formatting.None, new JsonConverter[0]));
			BtcMapScale.Log(string.Concat(new object[]
			{
				"RemovedVoxels.json: ",
				jarray.Count,
				" -> ",
				jarray2.Count
			}));
		}
	}

	private static int Cat(string key)
	{
		bool flag = key == null;
		int result;
		if (flag)
		{
			result = 0;
		}
		else
		{
			bool flag2 = key == "Balises.WorldEditorMarker";
			if (flag2)
			{
				result = 1;
			}
			else
			{
				bool flag3 = key.StartsWith("RawResources.") || key.StartsWith("BuildResources.") || key.StartsWith("Blocks.");
				if (flag3)
				{
					result = 2;
				}
				else
				{
					bool flag4 = key.StartsWith("ObjetsDynamiques.") || key.StartsWith("Blueprints.");
					if (flag4)
					{
						result = 4;
					}
					else
					{
						bool flag5 = key.StartsWith("Test.Bricktron") || key.StartsWith("Accessories.");
						if (flag5)
						{
							result = 8;
						}
						else
						{
							result = 0;
						}
					}
				}
			}
		}
		return result;
	}

	private static double[] GoPos(JObject go)
	{
		JArray jarray = go["components"] as JArray;
		bool flag = jarray == null;
		double[] result;
		if (flag)
		{
			result = null;
		}
		else
		{
			foreach (JToken jtoken in jarray)
			{
				JObject jobject = jtoken as JObject;
				bool flag2 = jobject != null && jobject["$type"] != null && ((string)jobject["$type"]).StartsWith("UnityEngine.Transform");
				if (flag2)
				{
					JObject jobject2 = jobject["position"] as JObject;
					bool flag3 = jobject2 == null || jobject2["x"] == null || jobject2["z"] == null;
					if (flag3)
					{
						return null;
					}
					return new double[]
					{
						BtcMapScale.Num(jobject2["x"]),
						BtcMapScale.Num(jobject2["y"]),
						BtcMapScale.Num(jobject2["z"])
					};
				}
			}
			result = null;
		}
		return result;
	}

	private static string GoMarkerGroup(JObject go)
	{
		JArray jarray = go["components"] as JArray;
		bool flag = jarray == null;
		string result;
		if (flag)
		{
			result = null;
		}
		else
		{
			foreach (JToken jtoken in jarray)
			{
				JObject jobject = jtoken as JObject;
				bool flag2 = jobject != null && jobject["markerGroups"] != null;
				if (flag2)
				{
					return BtcMapScale.MarkerGroup(jobject);
				}
			}
			result = null;
		}
		return result;
	}

	private static int GoDens(JObject go)
	{
		string text = BtcMapScale.Str(go["$assetKey"]);
		bool flag = text == "Balises.WorldEditorMarker";
		int result;
		if (flag)
		{
			result = BtcMapScale.MarkerDens(BtcMapScale.GoMarkerGroup(go));
		}
		else
		{
			bool flag2 = BtcMapScale._mRes && text != null && (text.StartsWith("RawResources.") || text.StartsWith("BuildResources."));
			if (flag2)
			{
				result = BtcMapScale._dRes;
			}
			else
			{
				result = -1;
			}
		}
		return result;
	}

	private static void Shift(JToken t, double dx, double dy, double dz)
	{
		JObject jobject = t as JObject;
		bool flag = jobject == null || jobject["x"] == null || jobject["z"] == null;
		if (!flag)
		{
			jobject["x"] = BtcMapScale.SetNum(jobject["x"], BtcMapScale.Num(jobject["x"]) + dx);
			bool flag2 = jobject["y"] != null;
			if (flag2)
			{
				jobject["y"] = BtcMapScale.SetNum(jobject["y"], BtcMapScale.Num(jobject["y"]) + dy);
			}
			jobject["z"] = BtcMapScale.SetNum(jobject["z"], BtcMapScale.Num(jobject["z"]) + dz);
		}
	}

	private static void WalkShift(JToken t, double dx, double dy, double dz)
	{
		JObject jobject = t as JObject;
		bool flag = jobject != null;
		if (flag)
		{
			string text = (jobject["$type"] != null) ? ((string)jobject["$type"]) : string.Empty;
			bool flag2 = text.StartsWith("UnityEngine.Transform");
			if (flag2)
			{
				BtcMapScale.Shift(jobject["position"], dx, dy, dz);
			}
			else
			{
				bool flag3 = text.StartsWith("Brix.Legacy.Beacon,");
				if (flag3)
				{
					BtcMapScale.Shift(jobject["position"], dx, dy, dz);
				}
			}
			List<JToken> list = new List<JToken>();
			foreach (JProperty jproperty in jobject.Properties())
			{
				list.Add(jproperty.Value);
			}
			for (int i = 0; i < list.Count; i++)
			{
				BtcMapScale.WalkShift(list[i], dx, dy, dz);
			}
		}
		else
		{
			JArray jarray = t as JArray;
			bool flag4 = jarray != null;
			if (flag4)
			{
				foreach (JToken t2 in jarray)
				{
					BtcMapScale.WalkShift(t2, dx, dy, dz);
				}
			}
		}
	}

	private static void CollectIds(JToken t, List<string> ids)
	{
		JObject jobject = t as JObject;
		bool flag = jobject != null;
		if (flag)
		{
			bool flag2 = jobject["$id"] != null && jobject["$id"].Type == JTokenType.String && ((string)jobject["$id"]).StartsWith("go:");
			if (flag2)
			{
				ids.Add((string)jobject["$id"]);
			}
			foreach (JProperty jproperty in jobject.Properties())
			{
				BtcMapScale.CollectIds(jproperty.Value, ids);
			}
		}
		else
		{
			JArray jarray = t as JArray;
			bool flag3 = jarray != null;
			if (flag3)
			{
				foreach (JToken t2 in jarray)
				{
					BtcMapScale.CollectIds(t2, ids);
				}
			}
		}
	}

	private static string ReplaceId(string s, string o, string n)
	{
		return s.Replace("\"" + o + "\"", "\"" + n + "\"").Replace("/" + o + "/", "/" + n + "/").Replace("/" + o + "\"", "/" + n + "\"");
	}

	private static string Mapped(Dictionary<string, string> remap, string ck, string id)
	{
		string key = ck + "|" + id;
		string text;
		bool flag = !remap.TryGetValue(key, out text);
		if (flag)
		{
			text = "go:" + Guid.NewGuid().ToString();
			remap[key] = text;
		}
		return text;
	}

	private static void ProcessGameObjects(BtcMapScale.Grid g, string dir)
	{
		string path = Path.Combine(dir, "gameobjects.json");
		bool flag = !File.Exists(path);
		if (!flag)
		{
			JArray jarray = JArray.Parse(File.ReadAllText(path));
			int count = jarray.Count;
			Dictionary<string, int> dictionary = new Dictionary<string, int>();
			JObject[] array = new JObject[count];
			double[][] array2 = new double[count][];
			bool[] array3 = new bool[count];
			for (int i = 0; i < count; i++)
			{
				JObject jobject = jarray[i] as JObject;
				array[i] = jobject;
				bool flag2 = jobject == null;
				if (!flag2)
				{
					string text = BtcMapScale.Str(jobject["$id"]);
					bool flag3 = text != null && !dictionary.ContainsKey(text);
					if (flag3)
					{
						dictionary[text] = i;
					}
					bool flag4 = BtcMapScale.Cat(BtcMapScale.Str(jobject["$assetKey"])) != 0;
					if (flag4)
					{
						array2[i] = BtcMapScale.GoPos(jobject);
						array3[i] = (array2[i] != null);
					}
				}
			}
			int[] array4 = new int[count];
			for (int j = 0; j < count; j++)
			{
				array4[j] = -1;
				bool flag5 = array[j] == null;
				if (!flag5)
				{
					int num = j;
					int num2 = 0;
					while (num >= 0 && num2++ < 64)
					{
						bool flag6 = array3[num];
						if (flag6)
						{
							array4[j] = num;
						}
						string text2 = BtcMapScale.Str(array[num]["$parent"]);
						int num3;
						num = ((text2 != null && dictionary.TryGetValue(text2, out num3)) ? num3 : -1);
					}
				}
			}
			Dictionary<int, List<double[]>> dictionary2 = new Dictionary<int, List<double[]>>();
			int num4 = 0;
			int num5 = 0;
			int[] array5 = new int[count];
			float[] array6 = new float[count];
			List<int> list = new List<int>();
			List<double[]> list2 = new List<double[]>();
			List<int> list3 = new List<int>();
			List<double[]> list4 = new List<double[]>();
			for (int k = 0; k < count; k++)
			{
				array5[k] = -1;
				bool flag7 = array4[k] != k;
				if (!flag7)
				{
					array5[k] = BtcMapScale.GoDens(array[k]);
					bool flag8 = array5[k] < 0;
					if (!flag8)
					{
						double[] array7 = array2[k];
						bool flag9 = BtcMapScale.Cat(BtcMapScale.Str(array[k]["$assetKey"])) == 1;
						if (flag9)
						{
							list.Add(k);
							list2.Add(new double[]
							{
								array7[0],
								array7[2]
							});
						}
						else
						{
							list3.Add(k);
							list4.Add(new double[]
							{
								array7[0],
								array7[2]
							});
						}
					}
				}
			}
			BtcMapScale.PGrid pgrid = (g.k > 1.0001f && list3.Count > 0) ? new BtcMapScale.PGrid(g, list3.Count) : null;
			bool flag10 = g.k > 1.0001f;
			if (flag10)
			{
				float[] array8 = BtcMapScale.Spacings(g, list2);
				for (int l = 0; l < list.Count; l++)
				{
					array6[list[l]] = array8[l];
				}
				float[] array9 = BtcMapScale.Spacings(g, list4);
				for (int m = 0; m < list3.Count; m++)
				{
					array6[list3[m]] = array9[m];
				}
			}
			for (int n = 0; n < count; n++)
			{
				bool flag11 = array4[n] != n;
				if (!flag11)
				{
					double[] array10 = array2[n];
					double num6 = array10[0];
					double num7 = array10[1];
					double num8 = array10[2];
					int num9 = array5[n];
					bool flag12 = num9 >= 0;
					bool isInt = num6 == Math.Floor(num6) && num8 == Math.Floor(num8);
					int salt = (BtcMapScale.Cat(BtcMapScale.Str(array[n]["$assetKey"])) == 1) ? 9001 : 9101;
					int num10 = flag12 ? BtcMapScale.Count(g, num9, (int)Math.Round(num6), (int)Math.Round(num8), salt) : 1;
					List<double[]> list5 = new List<double[]>();
					for (int num11 = 0; num11 < num10; num11++)
					{
						double num12;
						double num13;
						bool flag13 = !BtcMapScale.Place(g, num6, num8, (double)array6[n], num11, num10, flag12, isInt, salt, null, (BtcMapScale.Cat(BtcMapScale.Str(array[n]["$assetKey"])) == 1) ? null : pgrid, null, out num12, out num13);
						if (!flag13)
						{
							double num14 = BtcMapScale.NewY(g, num6, num7, num8, num12, num13);
							list5.Add(new double[]
							{
								num12 - num6,
								num14 - num7,
								num13 - num8
							});
						}
					}
					dictionary2[n] = list5;
					num4++;
					num5 += list5.Count;
				}
			}
			JArray jarray2 = new JArray();
			Dictionary<string, string> remap = new Dictionary<string, string>();
			for (int num15 = 0; num15 < count; num15++)
			{
				int num16 = array4[num15];
				bool flag14 = num16 < 0;
				if (flag14)
				{
					jarray2.Add(jarray[num15].DeepClone());
				}
				else
				{
					List<double[]> list6;
					bool flag15 = !dictionary2.TryGetValue(num16, out list6);
					if (flag15)
					{
						jarray2.Add(jarray[num15].DeepClone());
					}
					else
					{
						for (int num17 = 0; num17 < list6.Count; num17++)
						{
							double[] array11 = list6[num17];
							JObject jobject2 = (JObject)array[num15].DeepClone();
							BtcMapScale.WalkShift(jobject2, array11[0], array11[1], array11[2]);
							bool flag16 = num17 == 0;
							if (flag16)
							{
								jarray2.Add(jobject2);
							}
							else
							{
								List<string> list7 = new List<string>();
								BtcMapScale.CollectIds(jobject2, list7);
								int num18 = num15;
								int num19 = 0;
								while (num18 != num16 && num19++ < 64)
								{
									string text3 = BtcMapScale.Str(array[num18]["$parent"]);
									int num20 = default(int);
									bool flag17 = text3 == null || !dictionary.TryGetValue(text3, out num20);
									if (flag17)
									{
										break;
									}
									list7.Add(text3);
									num18 = num20;
								}
								string text4 = jobject2.ToString(Formatting.None, new JsonConverter[0]);
								string ck = num16 + "/" + num17;
								for (int num21 = 0; num21 < list7.Count; num21++)
								{
									text4 = BtcMapScale.ReplaceId(text4, list7[num21], BtcMapScale.Mapped(remap, ck, list7[num21]));
								}
								jarray2.Add(JObject.Parse(text4));
							}
						}
					}
				}
			}
			File.WriteAllText(path, jarray2.ToString(Formatting.None, new JsonConverter[0]));
			BtcMapScale.Log(string.Concat(new object[]
			{
				"gameobjects.json: ",
				count,
				" -> ",
				jarray2.Count,
				" entries (roots ",
				num4,
				", root copies ",
				num5,
				")"
			}));
		}
	}

	private static GUIStyle Make(int size, TextAnchor anchor, FontStyle fontStyle, Color color)
	{
		return new GUIStyle(GUI.skin.label)
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

	private static void Styles(int fs)
	{
		bool flag = BtcMapScale._label != null && BtcMapScale._styleFs == fs;
		if (!flag)
		{
			BtcMapScale._styleFs = fs;
			BtcMapScale._label = BtcMapScale.Make(fs, TextAnchor.MiddleLeft, FontStyle.Normal, Color.white);
			BtcMapScale._note = BtcMapScale.Make(Mathf.Max(10, (int)((float)fs * 0.8f)), TextAnchor.UpperLeft, FontStyle.Normal, new Color(1f, 1f, 1f, 0.55f));
			BtcMapScale._note.wordWrap = true;
			BtcMapScale._sub = BtcMapScale.Make(fs, TextAnchor.MiddleLeft, FontStyle.Bold, BtcMapScale.Accent);
			BtcMapScale._btn = BtcMapScale.Make(Mathf.Max(10, (int)((float)fs * 0.9f)), TextAnchor.MiddleCenter, FontStyle.Normal, Color.white);
		}
	}

	private static Texture2D White()
	{
		bool flag = BtcMapScale._white == null;
		if (flag)
		{
			BtcMapScale._white = new Texture2D(1, 1, TextureFormat.RGBA32, false);
			BtcMapScale._white.SetPixel(0, 0, Color.white);
			BtcMapScale._white.Apply();
			BtcMapScale._white.hideFlags = HideFlags.HideAndDontSave;
		}
		return BtcMapScale._white;
	}

	private static void Note(ref float y, float w, int fs, string text)
	{
		float num = BtcMapScale._note.CalcHeight(new GUIContent(text), w - (float)fs * 2f);
		GUI.Label(new Rect((float)fs, y + 2f, w - (float)fs * 2f, num), text, BtcMapScale._note);
		y += num + (float)fs * 0.5f;
	}

	private static bool Button(Event e, Rect r, string text, Color color, Color old)
	{
		bool flag = e.type == EventType.Repaint;
		if (flag)
		{
			GUI.color = (r.Contains(e.mousePosition) ? new Color(1f, 1f, 1f, 0.16f) : new Color(1f, 1f, 1f, 0.07f));
			GUI.DrawTexture(r, BtcMapScale.White());
			GUI.color = old;
		}
		Color textColor = BtcMapScale._btn.normal.textColor;
		BtcMapScale._btn.normal.textColor = color;
		bool flag2 = GUI.Button(r, text, BtcMapScale._btn);
		BtcMapScale._btn.normal.textColor = textColor;
		return flag2 && !BtcHotkeys.UiClickSwallowed;
	}

	private static int Stepper(Event e, ref float y, float w, float rowH, int fs, Color old, string label, int v, int def, int min, int max, int step, bool on)
	{
		float num = y;
		y += rowH;
		GUI.Label(new Rect((float)fs, num, w * 0.5f, rowH), label, BtcMapScale._label);
		float num2 = rowH * 0.9f;
		Rect r = new Rect(w - (float)fs - num2, num + rowH * 0.05f, num2, rowH * 0.9f);
		Rect position = new Rect(r.x - num2 * 1.8f, num + rowH * 0.05f, num2 * 1.8f, rowH * 0.9f);
		Rect r2 = new Rect(position.x - num2, num + rowH * 0.05f, num2, rowH * 0.9f);
		Color color = (v != def) ? BtcMapScale.ChangedC : Color.white;
		GUI.Label(position, v.ToString(), BtcMapScale._btn);
		bool flag = BtcMapScale.Button(e, r2, "-", color, old) && on;
		if (flag)
		{
			v = Mathf.Max(min, v - step);
		}
		bool flag2 = BtcMapScale.Button(e, r, "+", color, old) && on;
		if (flag2)
		{
			v = Mathf.Min(max, v + step);
		}
		return v;
	}

	private static bool Toggle(Event e, ref float y, float w, float rowH, int fs, Color old, string text, bool v, bool edit, ref bool dirty)
	{
		Rect r = new Rect((float)fs, y + rowH * 0.05f, w - (float)fs * 2f, rowH * 0.9f);
		bool flag = BtcMapScale.Button(e, r, (v ? "[x] " : "[ ] ") + text, v ? BtcMapScale.Accent : Color.white, old) && edit;
		if (flag)
		{
			v = !v;
			dirty = true;
		}
		y += rowH;
		return v;
	}

	private static int Dens(Event e, ref float y, float w, float rowH, int fs, Color old, string label, string desc, int v, bool edit, ref bool dirty)
	{
		int num = BtcMapScale.Stepper(e, ref y, w, rowH, fs, old, label, v, 100, 0, 2000, 25, edit);
		bool flag = num != v;
		if (flag)
		{
			dirty = true;
		}
		BtcMapScale.Note(ref y, w, fs, desc);
		return num;
	}

	public static void Draw(Event e, ref float y, float w, float rowH, int fs, Color old)
	{
		BtcMapScale.Styles(fs);
		BtcMapScale.Tick();
		BtcMapScale.LoadPrefsOnce();
		bool flag = BtcGameplay.CanEdit && BtcBc.BcEnabled;
		bool flag2 = false;
		GUI.Label(new Rect((float)fs, y, w - (float)fs * 2f, rowH), BtcMapScale.T("Map scale", "Масштаб карты"), BtcMapScale._sub);
		y += rowH;
		BtcMapScale.Note(ref y, w, fs, BtcMapScale.T("Relative map scale: the map becomes longer and wider by this factor with the same proportions and the same density of trees, plants, ore and resources (x3 = 9 times the area). Terrain height is not scaled, so slopes get gentler. The map is rebuilt as a cached copy before the game starts, the original file is never changed. Works only with BetterConnection, every player needs this mod version. Applied before the map symmetry.", "Относительный масштаб карты: карта становится длиннее и шире в указанное число раз с теми же пропорциями и той же плотностью деревьев, растений, руды и ресурсов (x3 = площадь в 9 раз больше). Высота рельефа не масштабируется, поэтому склоны становятся более пологими. Карта пересобирается как кэшированная копия перед стартом игры, исходный файл не меняется. Работает только с BetterConnection, у всех игроков должна стоять эта версия мода. Применяется до симметрии карты."));
		bool flag3 = !BtcBc.BcEnabled;
		if (flag3)
		{
			BtcMapScale.Note(ref y, w, fs, BtcMapScale.T("BetterConnection is off: map scale is not applied.", "BetterConnection выключен: масштаб карты не применяется."));
		}
		else
		{
			bool flag4 = !flag;
			if (flag4)
			{
				BtcMapScale.Note(ref y, w, fs, BtcMapScale.T("Only the host can change these settings in the lobby.", "Менять эти настройки в лобби может только хост."));
			}
		}
		int[] array = new int[]
		{
			50,
			100,
			200,
			300,
			400,
			500
		};
		float num = (w - (float)fs * 2f) / (float)array.Length;
		for (int i = 0; i < array.Length; i++)
		{
			Rect r = new Rect((float)fs + num * (float)i, y + rowH * 0.05f, num - 4f, rowH * 0.85f);
			string text = (array[i] % 100 == 0) ? ((array[i] / 100).ToString() + "x") : (((float)array[i] / 100f).ToString("0.0") + "x");
			bool flag5 = BtcMapScale.Button(e, r, text, (array[i] == BtcMapScale.Pct) ? BtcMapScale.Accent : Color.white, old) && flag && array[i] != BtcMapScale.Pct;
			if (flag5)
			{
				BtcMapScale.Pct = array[i];
				flag2 = true;
			}
		}
		y += rowH;
		int num2 = BtcMapScale.Stepper(e, ref y, w, rowH, fs, old, BtcMapScale.T("Scale, %", "Масштаб, %"), BtcMapScale.Pct, 100, 10, 10000, 25, flag);
		bool flag6 = num2 != BtcMapScale.Pct;
		if (flag6)
		{
			BtcMapScale.Pct = num2;
			flag2 = true;
		}
		bool flag7 = BtcMapScale.Pct != 100;
		if (flag7)
		{
			float num3 = (float)BtcMapScale.Pct / 100f;
			BtcMapScale.Note(ref y, w, fs, string.Concat(new string[]
			{
				BtcMapScale.T("Length and width x", "Длина и ширина x"),
				num3.ToString("0.##"),
				BtcMapScale.T(", area x", ", площадь x"),
				(num3 * num3).ToString("0.##"),
				"."
			}));
		}
		bool flag8 = BtcMapScale.Pct >= 300;
		if (flag8)
		{
			BtcMapScale.Note(ref y, w, fs, BtcMapScale.T("WARNING: large scales mean a lot more objects, more memory, a longer map build and a heavier game. The vanilla world size limit may apply: if the map does not load, lower the scale (details in btc_mapscale.txt in the Debug folder).", "ВНИМАНИЕ: большой масштаб = намного больше объектов, больше памяти, дольше сборка карты и тяжелее игра. Может действовать ванильный лимит размера мира: если карта не грузится, уменьши масштаб (подробности в btc_mapscale.txt в папке Debug)."));
		}
		bool flag9 = BtcMapScale._info != null;
		if (flag9)
		{
			BtcMapScale.Note(ref y, w, fs, BtcMapScale.T("Last build: ", "Последняя сборка: ") + BtcMapScale._info);
		}
		Rect r2 = new Rect((float)fs, y + rowH * 0.05f, w - (float)fs * 2f, rowH * 0.9f);
		bool flag10 = BtcMapScale.Button(e, r2, (BtcMapScale._adv ? "[x] " : "[ ] ") + BtcMapScale.T("Advanced settings", "Расширенные настройки"), BtcMapScale._adv ? BtcMapScale.Accent : Color.white, old);
		if (flag10)
		{
			BtcMapScale._adv = !BtcMapScale._adv;
		}
		y += rowH;
		bool adv = BtcMapScale._adv;
		if (adv)
		{
			BtcMapScale.Note(ref y, w, fs, BtcMapScale.T("Density: percent of the proportional amount. 100 = the same density as the original map (x3 map = 9 copies of every object spread over its enlarged area), 0 = none, 200 = twice as dense. Copies are spread evenly over the enlarged area around the original spot (following the local density), not in clumps.", "Плотность: процент от пропорционального количества. 100 = такая же плотность как в оригинале (карта x3 = 9 копий каждого объекта по её увеличенной площади), 0 = нет, 200 = вдвое плотнее. Копии раскидываются равномерно по увеличенной площади вокруг исходного места (с учётом местной плотности), а не кучками."));
			BtcMapScale._dTrees = BtcMapScale.Dens(e, ref y, w, rowH, fs, old, BtcMapScale.T("Trees, %", "Деревья, %"), BtcMapScale.T("Trees from trees.json.", "Деревья из trees.json."), BtcMapScale._dTrees, flag, ref flag2);
			BtcMapScale._dPlants = BtcMapScale.Dens(e, ref y, w, rowH, fs, old, BtcMapScale.T("Plants, %", "Растения, %"), BtcMapScale.T("Grass and bushes from plants.json.", "Трава и кусты из plants.json."), BtcMapScale._dPlants, flag, ref flag2);
			BtcMapScale._mOre = BtcMapScale.Toggle(e, ref y, w, rowH, fs, old, BtcMapScale.T("Multiply surface ore (iron, brimstone)", "Размножать поверхностные залежи (железо, бримстоун)"), BtcMapScale._mOre, flag, ref flag2);
			BtcMapScale.Note(ref y, w, fs, BtcMapScale.T("Off: iron and brimstone deposits are only moved proportionally, their count stays exactly as on the original map (the bigger map just has more empty space). Deep ore is not affected.", "Выкл: залежи железа и бримстоуна только переносятся пропорционально, их количество остаётся ровно как на оригинальной карте (на большой карте просто больше свободного места). Глубинной руды это не касается."));
			bool mOre = BtcMapScale._mOre;
			if (mOre)
			{
				BtcMapScale._dIron = BtcMapScale.Dens(e, ref y, w, rowH, fs, old, BtcMapScale.T("Iron deposits, %", "Железо, %"), BtcMapScale.T("Surface iron deposits from iron.json.", "Поверхностные залежи железа из iron.json."), BtcMapScale._dIron, flag, ref flag2);
				BtcMapScale._dPyr = BtcMapScale.Dens(e, ref y, w, rowH, fs, old, BtcMapScale.T("Brimstone deposits, %", "Бримстоун, %"), BtcMapScale.T("Surface brimstone (pyrite) deposits from pyrite.json.", "Поверхностные залежи бримстоуна (пирита) из pyrite.json."), BtcMapScale._dPyr, flag, ref flag2);
			}
			BtcMapScale._dBoul = BtcMapScale.Dens(e, ref y, w, rowH, fs, old, BtcMapScale.T("Rocks, %", "Камни, %"), BtcMapScale.T("Boulders from boulders.json.", "Валуны из boulders.json."), BtcMapScale._dBoul, flag, ref flag2);
			int num4 = BtcMapScale.Stepper(e, ref y, w, rowH, fs, old, BtcMapScale.T("Height tolerance", "Допуск по высоте"), BtcMapScale._hTol, 8, 0, 255, 1, flag);
			bool flag11 = num4 != BtcMapScale._hTol;
			if (flag11)
			{
				BtcMapScale._hTol = num4;
				flag2 = true;
			}
			BtcMapScale.Note(ref y, w, fs, BtcMapScale.T("A copy may only stand where the terrain height differs from its original spot by at most this many units, so trees, ore and rocks never jump onto a mountain or into a pit. 0 = no check.", "Копия может стоять только там, где высота рельефа отличается от высоты оригинального места не больше чем на столько единиц: деревья, руда и камни не прыгают на гору или в яму. 0 = без проверки."));
			int num5 = BtcMapScale.Stepper(e, ref y, w, rowH, fs, old, BtcMapScale.T("Max scatter, px", "Макс. разброс, px"), BtcMapScale._spread, 6, 1, 64, 1, flag);
			bool flag12 = num5 != BtcMapScale._spread;
			if (flag12)
			{
				BtcMapScale._spread = num5;
				flag2 = true;
			}
			BtcMapScale.Note(ref y, w, fs, BtcMapScale.T("How far from the original place copies may stand, in pixels of the ORIGINAL map (multiplied by the scale). Lower = copies stay closer to where objects were (sparse objects like rocks do not spread over the whole map), higher = more even but looser.", "Как далеко от исходного места могут стоять копии, в пикселях ОРИГИНАЛЬНОЙ карты (умножается на масштаб). Меньше = копии держатся ближе к тому, где были объекты (редкие объекты вроде камней не расползаются по всей карте), больше = равномернее, но свободнее."));
			BtcMapScale._mRes = BtcMapScale.Toggle(e, ref y, w, rowH, fs, old, BtcMapScale.T("Multiply loose resources (stone, crystals, iron on the ground)", "Размножать россыпные ресурсы (камень, кристаллы, железо на земле)"), BtcMapScale._mRes, flag, ref flag2);
			bool mRes = BtcMapScale._mRes;
			if (mRes)
			{
				BtcMapScale._dRes = BtcMapScale.Dens(e, ref y, w, rowH, fs, old, BtcMapScale.T("Loose resources, %", "Россыпные ресурсы, %"), BtcMapScale.T("Density of RawResources / BuildResources objects. When off they are only moved proportionally.", "Плотность объектов RawResources / BuildResources. Если выключено, они только переносятся пропорционально."), BtcMapScale._dRes, flag, ref flag2);
			}
			BtcMapScale._vox = BtcMapScale.Toggle(e, ref y, w, rowH, fs, old, BtcMapScale.T("Stretch terrain holes (removed voxels)", "Растягивать выемки в рельефе (убранные воксели)"), BtcMapScale._vox, flag, ref flag2);
			BtcMapScale.Note(ref y, w, fs, BtcMapScale.T("On: every removed voxel becomes a block of the same enlarged size as the terrain. Off: holes are only moved and stay small.", "Вкл: каждый убранный воксель становится блоком того же увеличенного размера, что и рельеф. Выкл: выемки только переносятся и остаются маленькими."));
			BtcMapScale.Note(ref y, w, fs, BtcMapScale.T("Editor markers: by default they are only moved proportionally (one per player stays one). Tick to multiply them like other objects.", "Маркеры редактора: по умолчанию они только переносятся пропорционально (один старт на игрока остаётся одним). Включи, чтобы размножать их как остальные объекты."));
			BtcMapScale._mPlayer = BtcMapScale.Toggle(e, ref y, w, rowH, fs, old, BtcMapScale.T("Multiply player start points", "Размножать стартовые точки игроков"), BtcMapScale._mPlayer, flag, ref flag2);
			BtcMapScale._mCrystal = BtcMapScale.Toggle(e, ref y, w, rowH, fs, old, BtcMapScale.T("Multiply crystal markers", "Размножать маркеры кристаллов"), BtcMapScale._mCrystal, flag, ref flag2);
			BtcMapScale._mCorr = BtcMapScale.Toggle(e, ref y, w, rowH, fs, old, BtcMapScale.T("Multiply Corruptron spawn points", "Размножать точки спавна Корраптронов"), BtcMapScale._mCorr, flag, ref flag2);
			int num6 = BtcMapScale.Stepper(e, ref y, w, rowH, fs, old, BtcMapScale.T("Scatter seed", "Сид разброса"), BtcMapScale._seed, 0, 0, 999999, 1, flag);
			bool flag13 = num6 != BtcMapScale._seed;
			if (flag13)
			{
				BtcMapScale._seed = num6;
				flag2 = true;
			}
			Rect r3 = new Rect((float)fs, y + rowH * 0.05f, (w - (float)fs * 2f) * 0.5f - 4f, rowH * 0.9f);
			bool flag14 = BtcMapScale.Button(e, r3, BtcMapScale.T("Random seed", "Случайный сид"), Color.white, old) && flag;
			if (flag14)
			{
				BtcMapScale._seed = Environment.TickCount % 1000000;
				bool flag15 = BtcMapScale._seed < 0;
				if (flag15)
				{
					BtcMapScale._seed = -BtcMapScale._seed;
				}
				flag2 = true;
			}
			Rect r4 = new Rect(r3.xMax + 8f, r3.y, r3.width, r3.height);
			bool flag16 = BtcMapScale.Button(e, r4, BtcMapScale.T("Reset advanced", "Сбросить расширенные"), BtcMapScale.Warn, old) && flag;
			if (flag16)
			{
				BtcMapScale.DefaultsAdvanced();
				flag2 = true;
			}
			y += rowH;
			BtcMapScale.Note(ref y, w, fs, BtcMapScale.T("The seed only changes where copies of objects stand, same seed = same map on every machine. Deep ore is generated from noise by position, so its density stays as in the original on its own. Buildings, units and blocks are moved proportionally and never duplicated. Terrain height is not scaled.", "Сид меняет только расстановку копий объектов, один сид = одинаковая карта у всех. Глубинная руда генерируется шумом по позиции, поэтому её плотность сама остаётся как в оригинале. Постройки, юниты и блоки переносятся пропорционально и никогда не дублируются. Высота рельефа не масштабируется."));
		}
		bool flag17 = flag2;
		if (flag17)
		{
			BtcMapScale.Changed();
		}
		y += rowH * 0.2f;
	}

	// Note: this type is marked as 'beforefieldinit'.
	static BtcMapScale()
	{
		BtcMapScale._mOre = true;
		BtcMapScale._mRes = true;
		BtcMapScale._dRes = 100;
		BtcMapScale._vox = true;
		BtcMapScale.Accent = new Color(0.55f, 0.85f, 0.55f, 1f);
		BtcMapScale.Warn = new Color(1f, 0.45f, 0.45f, 1f);
		BtcMapScale.ChangedC = new Color(1f, 0.85f, 0.4f, 1f);
	}

	private static double CenterIn(double v, double start, double len, double half)
	{
		double num = start + half;
		double num2 = start + len - half;
		bool flag = num > num2;
		double result;
		if (flag)
		{
			result = start + len * 0.5;
		}
		else
		{
			result = ((v < num) ? num : ((v > num2) ? num2 : v));
		}
		return result;
	}

	private static float[] Spacings(BtcMapScale.Grid g, List<double[]> pts)
	{
		int count = pts.Count;
		float[] array = new float[count];
		bool flag = count == 0;
		float[] result;
		if (flag)
		{
			result = array;
		}
		else
		{
			double num = (double)(g.maxX - g.minX + 1);
			double num2 = (double)(g.maxZ - g.minZ + 1);
			double num3 = Math.Max(1.0, Math.Sqrt(num * num2 / (double)count));
			double num4 = num3 * 2.0;
			int num5 = (int)Math.Ceiling(num / num4) + 1;
			int num6 = (int)Math.Ceiling(num2 / num4) + 1;
			int[] array2 = new int[count];
			int[] array3 = new int[num5 * num6 + 1];
			for (int i = 0; i < count; i++)
			{
				int num7 = Mathf.Clamp((int)((pts[i][0] - (double)g.minX) / num4), 0, num5 - 1);
				int num8 = Mathf.Clamp((int)((pts[i][1] - (double)g.minZ) / num4), 0, num6 - 1);
				array2[i] = num8 * num5 + num7;
				array3[array2[i] + 1]++;
			}
			for (int j = 0; j < num5 * num6; j++)
			{
				array3[j + 1] += array3[j];
			}
			int[] array4 = new int[num5 * num6];
			int[] array5 = new int[count];
			for (int k = 0; k < count; k++)
			{
				int num9 = array2[k];
				array5[array3[num9] + array4[num9]] = k;
				array4[num9]++;
			}
			double[] array6 = new double[8];
			double num10 = num3 * 2.0;
			for (int l = 0; l < count; l++)
			{
				double num11 = pts[l][0];
				double num12 = pts[l][1];
				int num13 = array2[l] % num5;
				int num14 = array2[l] / num5;
				int num15 = 0;
				for (int m = 0; m <= 3; m++)
				{
					for (int n = -m; n <= m; n++)
					{
						int num16 = num14 + n;
						bool flag2 = num16 < 0 || num16 >= num6;
						if (!flag2)
						{
							for (int num17 = -m; num17 <= m; num17++)
							{
								bool flag3 = Math.Max(Math.Abs(num17), Math.Abs(n)) != m;
								if (!flag3)
								{
									int num18 = num13 + num17;
									bool flag4 = num18 < 0 || num18 >= num5;
									if (!flag4)
									{
										int num19 = num16 * num5 + num18;
										for (int num20 = array3[num19]; num20 < array3[num19 + 1]; num20++)
										{
											int num21 = array5[num20];
											bool flag5 = num21 == l;
											if (!flag5)
											{
												double num22 = pts[num21][0] - num11;
												double num23 = pts[num21][1] - num12;
												double num24 = num22 * num22 + num23 * num23;
												bool flag6 = num15 < 8;
												int num25;
												if (flag6)
												{
													num25 = num15;
													array6[num25] = num24;
													num15++;
												}
												else
												{
													bool flag7 = num24 < array6[7];
													if (!flag7)
													{
														goto IL_354;
													}
													num25 = 7;
													array6[num25] = num24;
												}
												while (num25 > 0 && array6[num25] < array6[num25 - 1])
												{
													double num26 = array6[num25];
													array6[num25] = array6[num25 - 1];
													array6[num25 - 1] = num26;
													num25--;
												}
											}
											IL_354:;
										}
									}
								}
							}
						}
					}
					double num27 = (double)m * num4;
					bool flag8 = num15 >= 8 && array6[7] <= num27 * num27;
					if (flag8)
					{
						break;
					}
				}
				double val = num10;
				bool flag9 = num15 > 0;
				if (flag9)
				{
					val = Math.Sqrt(array6[num15 - 1]) * Math.Sqrt(3.141592653589793 / (double)num15);
				}
				array[l] = (float)Math.Max(1.0, Math.Min(num10, val));
			}
			result = array;
		}
		return result;
	}

	private static bool SurfaceOk(BtcMapScale.Grid g, double ox, double oz, double X, double Z)
	{
		int num = BtcMapScale.PixelG(g.src, g.w, g.h, (int)Math.Floor(ox), (int)Math.Floor(oz));
		bool flag = num <= 0;
		bool result;
		if (flag)
		{
			result = true;
		}
		else
		{
			int num2 = BtcMapScale.PixelG(g.dst, g.nw, g.nh, (int)Math.Floor(X), (int)Math.Floor(Z));
			bool flag2 = num2 <= 0;
			result = (!flag2 && (BtcMapScale._hTol <= 0 || Math.Abs(num2 - num) <= BtcMapScale._hTol));
		}
		return result;
	}

	private static void Fin(BtcMapScale.Grid g, bool isInt, ref double X, ref double Z)
	{
		if (isInt)
		{
			X = Math.Floor(X);
			Z = Math.Floor(Z);
		}
		double num = (double)(g.nx0 + g.W - 1) + (isInt ? 0.0 : 0.99);
		double num2 = (double)(g.nz0 + g.H - 1) + (isInt ? 0.0 : 0.99);
		bool flag = X < (double)g.nx0;
		if (flag)
		{
			X = (double)g.nx0;
		}
		bool flag2 = X > num;
		if (flag2)
		{
			X = num;
		}
		bool flag3 = Z < (double)g.nz0;
		if (flag3)
		{
			Z = (double)g.nz0;
		}
		bool flag4 = Z > num2;
		if (flag4)
		{
			Z = num2;
		}
	}

	private static long Key(double X, double Z)
	{
		return (long)((int)X) << 32 | (long)((ulong)((int)Z));
	}

	private static bool Fits(BtcMapScale.Grid g, double ox, double oz, double x, double z, bool isInt, HashSet<long> used, double[] offs)
	{
		bool flag = !BtcMapScale.SurfaceOk(g, ox, oz, x, z);
		bool result;
		if (flag)
		{
			result = false;
		}
		else
		{
			bool flag2 = used != null && isInt && used.Contains(BtcMapScale.Key(x, z));
			if (flag2)
			{
				result = false;
			}
			else
			{
				bool flag3 = offs != null;
				if (flag3)
				{
					int num = 0;
					while (num + 1 < offs.Length)
					{
						double num2 = x + offs[num];
						double num3 = z + offs[num + 1];
						bool flag4 = num2 < (double)g.nx0 || num3 < (double)g.nz0 || num2 >= (double)(g.nx0 + g.W) || num3 >= (double)(g.nz0 + g.H);
						if (flag4)
						{
							return false;
						}
						bool flag5 = !BtcMapScale.SurfaceOk(g, ox + offs[num], oz + offs[num + 1], num2, num3);
						if (flag5)
						{
							return false;
						}
						bool flag6 = used != null && isInt && used.Contains(BtcMapScale.Key(num2, num3));
						if (flag6)
						{
							return false;
						}
						num += 2;
					}
				}
				result = true;
			}
		}
		return result;
	}

	private static void Take(HashSet<long> used, bool isInt, double x, double z, double[] offs)
	{
		bool flag = used == null || !isInt;
		if (!flag)
		{
			used.Add(BtcMapScale.Key(x, z));
			bool flag2 = offs != null;
			if (flag2)
			{
				int num = 0;
				while (num + 1 < offs.Length)
				{
					used.Add(BtcMapScale.Key(x + offs[num], z + offs[num + 1]));
					num += 2;
				}
			}
		}
	}

	private static bool Place(BtcMapScale.Grid g, double ox, double oz, double sp, int c, int n, bool mul, bool isInt, int salt, HashSet<long> used, BtcMapScale.PGrid pg, double[] offs, out double X, out double Z)
	{
		double num = (double)g.k;
		double num2 = (double)g.nx0 + (ox - (double)g.minX) * num;
		double num3 = (double)g.nz0 + (oz - (double)g.minZ) * num;
		X = num2;
		Z = num3;
		bool flag = !mul;
		bool result;
		if (flag)
		{
			BtcMapScale.Fin(g, isInt, ref X, ref Z);
			result = true;
		}
		else
		{
			bool flag2 = num <= 1.0001;
			if (flag2)
			{
				BtcMapScale.Fin(g, isInt, ref X, ref Z);
				bool flag3 = !BtcMapScale.Fits(g, ox, oz, X, Z, isInt, used, offs);
				if (flag3)
				{
					result = false;
				}
				else
				{
					BtcMapScale.Take(used, isInt, X, Z, offs);
					result = true;
				}
			}
			else
			{
				double num4 = Math.Max(1.0, sp) * 0.5;
				bool flag4 = pg != null;
				if (flag4)
				{
					num4 = Math.Min(num4, (double)BtcMapScale._spread);
				}
				num4 *= num;
				double num5 = BtcMapScale.CenterIn(num2, (double)g.nx0, (double)g.W, num4);
				double num6 = BtcMapScale.CenterIn(num3, (double)g.nz0, (double)g.H, num4);
				double limit = Math.Min(sp, 2.0 * num4 / Math.Sqrt((double)Math.Max(1, n)));
				int a = (int)Math.Floor(ox);
				int b = (int)Math.Floor(oz);
				double[] array = new double[]
				{
					1.0,
					0.6,
					0.3
				};
				for (int i = 0; i < 3; i++)
				{
					double num7 = num4 * array[i];
					bool flag5 = false;
					double num8 = -1.0;
					double num9 = 0.0;
					double num10 = 0.0;
					for (int j = 0; j < 4; j++)
					{
						int c2 = c * 12 + i * 4 + j;
						double num11 = num5 + (2.0 * (double)BtcMapScale.Rand(a, b, c2, salt) - 1.0) * num7;
						double num12 = num6 + (2.0 * (double)BtcMapScale.Rand(a, b, c2, salt + 1) - 1.0) * num7;
						BtcMapScale.Fin(g, isInt, ref num11, ref num12);
						bool flag6 = !BtcMapScale.Fits(g, ox, oz, num11, num12, isInt, used, offs);
						if (!flag6)
						{
							double num13 = (pg != null) ? pg.Near(num11, num12, limit) : 0.0;
							bool flag7 = !flag5 || num13 > num8;
							if (flag7)
							{
								flag5 = true;
								num8 = num13;
								num9 = num11;
								num10 = num12;
							}
							bool flag8 = pg == null;
							if (flag8)
							{
								break;
							}
						}
					}
					bool flag9 = flag5;
					if (flag9)
					{
						X = num9;
						Z = num10;
						BtcMapScale.Take(used, isInt, X, Z, offs);
						bool flag10 = pg != null;
						if (flag10)
						{
							pg.Add(X, Z);
						}
						return true;
					}
				}
				X = num2;
				Z = num3;
				BtcMapScale.Fin(g, isInt, ref X, ref Z);
				bool flag11 = !BtcMapScale.Fits(g, ox, oz, X, Z, isInt, used, offs);
				if (flag11)
				{
					result = false;
				}
				else
				{
					BtcMapScale.Take(used, isInt, X, Z, offs);
					bool flag12 = pg != null;
					if (flag12)
					{
						pg.Add(X, Z);
					}
					result = true;
				}
			}
		}
		return result;
	}

	private static long CellKey(int x, int z)
	{
		return (long)x << 32 | (long)((ulong)z);
	}

	private static int Root(int[] par, int i)
	{
		while (par[i] != i)
		{
			par[i] = par[par[i]];
			i = par[i];
		}
		return i;
	}

	private static void ProcessClusters(BtcMapScale.Grid g, string dir, string name, string key, BtcMapScale.DensFn densFn, int salt)
	{
		string path = Path.Combine(dir, name);
		bool flag = !File.Exists(path);
		if (!flag)
		{
			JArray jarray = JArray.Parse(File.ReadAllText(path));
			int count = jarray.Count;
			double[] array = new double[count];
			double[] array2 = new double[count];
			double[] array3 = new double[count];
			bool[] array4 = new bool[count];
			int[] array5 = new int[count];
			for (int i = 0; i < count; i++)
			{
				array5[i] = i;
				JObject o = jarray[i] as JObject;
				JObject jobject = BtcMapScale.PosObj(o, key);
				bool flag2 = jobject == null || jobject["x"] == null || jobject["z"] == null;
				if (!flag2)
				{
					array4[i] = true;
					array[i] = BtcMapScale.Num(jobject["x"]);
					array2[i] = BtcMapScale.Num(jobject["y"]);
					array3[i] = BtcMapScale.Num(jobject["z"]);
				}
			}
			Dictionary<long, List<int>> dictionary = new Dictionary<long, List<int>>();
			for (int j = 0; j < count; j++)
			{
				bool flag3 = !array4[j];
				if (!flag3)
				{
					int num = (int)Math.Floor(array[j] / 3.0);
					int num2 = (int)Math.Floor(array3[j] / 3.0);
					for (int k = -1; k <= 1; k++)
					{
						for (int l = -1; l <= 1; l++)
						{
							List<int> list;
							bool flag4 = !dictionary.TryGetValue(BtcMapScale.CellKey(num + l, num2 + k), out list);
							if (!flag4)
							{
								for (int m = 0; m < list.Count; m++)
								{
									int num3 = list[m];
									double num4 = array[j] - array[num3];
									double num5 = array3[j] - array3[num3];
									bool flag5 = num4 * num4 + num5 * num5 <= 6.25;
									if (flag5)
									{
										int num6 = BtcMapScale.Root(array5, j);
										int num7 = BtcMapScale.Root(array5, num3);
										bool flag6 = num6 != num7;
										if (flag6)
										{
											array5[Math.Max(num6, num7)] = Math.Min(num6, num7);
										}
									}
								}
							}
						}
					}
					List<int> list2;
					bool flag7 = !dictionary.TryGetValue(BtcMapScale.CellKey(num, num2), out list2);
					if (flag7)
					{
						list2 = new List<int>();
						dictionary[BtcMapScale.CellKey(num, num2)] = list2;
					}
					list2.Add(j);
				}
			}
			Dictionary<int, List<int>> dictionary2 = new Dictionary<int, List<int>>();
			List<int> list3 = new List<int>();
			for (int n = 0; n < count; n++)
			{
				bool flag8 = !array4[n];
				if (!flag8)
				{
					int num8 = BtcMapScale.Root(array5, n);
					List<int> list4;
					bool flag9 = !dictionary2.TryGetValue(num8, out list4);
					if (flag9)
					{
						list4 = new List<int>();
						dictionary2[num8] = list4;
						list3.Add(num8);
					}
					list4.Add(n);
				}
			}
			int[] array6 = new int[list3.Count];
			List<double[]> list5 = new List<double[]>();
			for (int num9 = 0; num9 < list3.Count; num9++)
			{
				List<int> list6 = dictionary2[list3[num9]];
				double num10 = 0.0;
				double num11 = 0.0;
				for (int num12 = 0; num12 < list6.Count; num12++)
				{
					num10 += array[list6[num12]];
					num11 += array3[list6[num12]];
				}
				num10 /= (double)list6.Count;
				num11 /= (double)list6.Count;
				int num13 = list6[0];
				double num14 = double.MaxValue;
				for (int num15 = 0; num15 < list6.Count; num15++)
				{
					double num16 = (array[list6[num15]] - num10) * (array[list6[num15]] - num10) + (array3[list6[num15]] - num11) * (array3[list6[num15]] - num11);
					bool flag10 = num16 < num14;
					if (flag10)
					{
						num14 = num16;
						num13 = list6[num15];
					}
				}
				array6[num9] = num13;
				list5.Add(new double[]
				{
					array[num13],
					array3[num13]
				});
			}
			float[] array7 = (list5.Count > 0 && g.k > 1.0001f) ? BtcMapScale.Spacings(g, list5) : new float[list5.Count];
			BtcMapScale.PGrid pg = (list5.Count > 0 && g.k > 1.0001f) ? new BtcMapScale.PGrid(g, list5.Count) : null;
			JArray jarray2 = new JArray();
			HashSet<long> used = new HashSet<long>();
			int num17 = 0;
			int num18 = 0;
			for (int num19 = 0; num19 < list3.Count; num19++)
			{
				List<int> list7 = dictionary2[list3[num19]];
				int num20 = array6[num19];
				double num21 = array[num20];
				double num22 = array3[num20];
				JObject o2 = jarray[num20] as JObject;
				int num23 = densFn(o2);
				bool flag11 = num23 >= 0;
				bool isInt = BtcMapScale.PosObj(o2, key)["x"].Type == JTokenType.Integer;
				double[] array8 = new double[list7.Count * 2];
				for (int num24 = 0; num24 < list7.Count; num24++)
				{
					array8[num24 * 2] = array[list7[num24]] - num21;
					array8[num24 * 2 + 1] = array3[list7[num24]] - num22;
				}
				int num25 = flag11 ? BtcMapScale.Count(g, num23, (int)Math.Round(num21), (int)Math.Round(num22), salt) : 1;
				bool flag12 = num25 == 0;
				if (flag12)
				{
					num17++;
				}
				for (int num26 = 0; num26 < num25; num26++)
				{
					double num27;
					double num28;
					bool flag13 = !BtcMapScale.Place(g, num21, num22, flag11 ? ((double)array7[num19]) : 0.0, num26, num25, flag11, isInt, salt, used, pg, array8, out num27, out num28);
					if (flag13)
					{
						num17++;
					}
					else
					{
						for (int num29 = 0; num29 < list7.Count; num29++)
						{
							int num30 = list7[num29];
							double x = num27 + array8[num29 * 2];
							double z = num28 + array8[num29 * 2 + 1];
							double y = BtcMapScale.NewY(g, array[num30], array2[num30], array3[num30], x, z);
							JObject jobject2 = (JObject)jarray[num30].DeepClone();
							BtcMapScale.PutXYZ(BtcMapScale.PosObj(jobject2, key), x, y, z);
							jarray2.Add(jobject2);
							num18++;
						}
					}
				}
			}
			for (int num31 = 0; num31 < count; num31++)
			{
				bool flag14 = !array4[num31];
				if (flag14)
				{
					jarray2.Add(jarray[num31].DeepClone());
				}
			}
			File.WriteAllText(path, jarray2.ToString(Formatting.None, new JsonConverter[0]));
			BtcMapScale.Log(string.Concat(new object[]
			{
				name,
				": ",
				count,
				" pieces in ",
				list3.Count,
				" groups -> ",
				jarray2.Count,
				" (dropped groups ",
				num17,
				", pieces ",
				num18,
				")"
			}));
		}
	}

	public static int Pct = 100;

	private static int _seed;

	private static int _dTrees = 100;

	private static int _dPlants = 100;

	private static int _dIron = 100;

	private static int _dPyr = 100;

	private static int _dBoul = 100;

	private static bool _mRes;

	private static int _dRes;

	private static bool _vox;

	private static bool _mPlayer;

	private static bool _mCrystal;

	private static bool _mCorr;

	private static bool _adv;

	private const short MsgState = 97;

	private const int Ver = 4;

	private const int SaltMarker = 9001;

	private static bool _prefsLoaded;

	private static Asset_Map _swapped;

	private static string _origPath;

	private static string _info;

	private static MethodInfo _symRestore;

	private static readonly Color Accent;

	private static readonly Color Warn;

	private static readonly Color ChangedC;

	private static GUIStyle _label;

	private static GUIStyle _note;

	private static GUIStyle _sub;

	private static GUIStyle _btn;

	private static int _styleFs;

	private static Texture2D _white;

	private static bool _mOre;

	private static int _hTol = 8;

	private static int _spread = 6;

	private sealed class Grid
	{
		public int w;

		public int h;

		public int minX;

		public int minZ;

		public int maxX;

		public int maxZ;

		public int nw;

		public int nh;

		public int nx0;

		public int nz0;

		public int W;

		public int H;

		public float k;

		public Color32[] src;

		public Color32[] dst;
	}

	private delegate int DensFn(JObject o);

	public sealed class Msg : MessageBase
	{
		public override void Serialize(NetworkWriter w)
		{
			w.Write(this.data ?? string.Empty);
		}

		public override void Deserialize(NetworkReader r)
		{
			this.data = r.ReadString();
		}

		public string data = string.Empty;
	}

	private sealed class PGrid
	{
		public PGrid(BtcMapScale.Grid g, int count)
		{
			double val = Math.Max(1.0, Math.Sqrt((double)(g.maxX - g.minX + 1) * (double)(g.maxZ - g.minZ + 1) / (double)Math.Max(1, count)));
			this.cs = Math.Max(2.0, val);
			this.bx0 = g.nx0;
			this.bz0 = g.nz0;
			this.gw = (int)Math.Ceiling((double)g.W / this.cs) + 1;
			this.gh = (int)Math.Ceiling((double)g.H / this.cs) + 1;
			this.head = new int[this.gw * this.gh];
			for (int i = 0; i < this.head.Length; i++)
			{
				this.head[i] = -1;
			}
		}

		public void Add(double x, double z)
		{
			int num = Mathf.Clamp((int)((x - (double)this.bx0) / this.cs), 0, this.gw - 1);
			int num2 = Mathf.Clamp((int)((z - (double)this.bz0) / this.cs), 0, this.gh - 1);
			int num3 = num2 * this.gw + num;
			int count = this.xs.Count;
			this.xs.Add((float)x);
			this.zs.Add((float)z);
			this.nxt.Add(this.head[num3]);
			this.head[num3] = count;
		}

		public double Near(double x, double z, double limit)
		{
			int num = Mathf.Clamp((int)((x - (double)this.bx0) / this.cs), 0, this.gw - 1);
			int num2 = Mathf.Clamp((int)((z - (double)this.bz0) / this.cs), 0, this.gh - 1);
			int num3 = (int)Math.Ceiling(limit / this.cs);
			double num4 = limit * limit;
			for (int i = Math.Max(0, num2 - num3); i <= Math.Min(this.gh - 1, num2 + num3); i++)
			{
				for (int j = Math.Max(0, num - num3); j <= Math.Min(this.gw - 1, num + num3); j++)
				{
					for (int k = this.head[i * this.gw + j]; k >= 0; k = this.nxt[k])
					{
						double num5 = (double)this.xs[k] - x;
						double num6 = (double)this.zs[k] - z;
						double num7 = num5 * num5 + num6 * num6;
						bool flag = num7 < num4;
						if (flag)
						{
							num4 = num7;
						}
					}
				}
			}
			return Math.Sqrt(num4);
		}

		private double cs;

		private int gw;

		private int gh;

		private int bx0;

		private int bz0;

		private int[] head;

		private List<float> xs = new List<float>();

		private List<float> zs = new List<float>();

		private List<int> nxt = new List<int>();
	}
}
}
