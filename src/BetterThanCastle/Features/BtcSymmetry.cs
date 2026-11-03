using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Brix.Assets;
using Brix.Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
public static class BtcSymmetry
{
	private static int Side(float v, int sum, float eps = 0.001f)
	{
		float num = 2f * v - (float)sum;
		bool flag = num < -eps;
		int result;
		if (flag)
		{
			result = 0;
		}
		else
		{
			bool flag2 = num > eps;
			if (flag2)
			{
				result = 1;
			}
			else
			{
				result = 2;
			}
		}
		return result;
	}

	private static void Fold(int m, int keep, int sx, int sz, ref float x, ref float z, float eps = 0.001f)
	{
		int num = BtcSymmetry.Side(x, sx, eps);
		int num2 = BtcSymmetry.Side(z, sz, eps);
		bool flag = m == 1;
		if (flag)
		{
			bool flag2 = num != 2 && num != (keep & 1);
			if (flag2)
			{
				x = (float)sx - x;
			}
		}
		else
		{
			bool flag3 = m == 2;
			if (flag3)
			{
				bool flag4 = num2 != 2 && num2 != (keep & 1);
				if (flag4)
				{
					z = (float)sz - z;
				}
			}
			else
			{
				bool flag5 = m == 3;
				if (flag5)
				{
					bool flag6 = keep < 2;
					bool flag7;
					if (flag6)
					{
						flag7 = ((num != 2) ? (num != (keep & 1)) : (num2 == 1));
					}
					else
					{
						flag7 = ((num2 != 2) ? (num2 != (keep & 1)) : (num == 1));
					}
					bool flag8 = flag7;
					if (flag8)
					{
						x = (float)sx - x;
						z = (float)sz - z;
					}
				}
				else
				{
					bool flag9 = m == 4;
					if (flag9)
					{
						bool flag10 = num != 2 && num != (keep & 1);
						if (flag10)
						{
							x = (float)sx - x;
						}
						bool flag11 = num2 != 2 && num2 != keep >> 1;
						if (flag11)
						{
							z = (float)sz - z;
						}
					}
				}
			}
		}
	}

	public static Vector3 FoldPos(Vector3 p)
	{
		int am = BtcSymmetry._am;
		bool flag = am == 0;
		Vector3 result;
		if (flag)
		{
			result = p;
		}
		else
		{
			float x = p.x;
			float z = p.z;
			BtcSymmetry.Fold(am, BtcSymmetry._ak, BtcSymmetry._asx, BtcSymmetry._asz, ref x, ref z);
			bool flag2 = x == p.x && z == p.z;
			if (flag2)
			{
				result = p;
			}
			else
			{
				result = new Vector3(x, p.y, z);
			}
		}
		return result;
	}

	private static bool Kept(int m, int keep, int sx, int sz, float x, float z, float tol = 1f)
	{
		float num = x;
		float num2 = z;
		// Objects within 0.5 voxel of an axis count as ON the axis: always kept (never dropped).
		BtcSymmetry.Fold(m, keep, sx, sz, ref num, ref num2, tol > 1f ? 4f : 1f);
		return num == x && num2 == z;
	}

	// True when a mirrored copy lands within 1 voxel of (ox,oz): it would duplicate the original.
	private static bool Near(float ax, float az, float bx, float bz, float tol = 1f)
	{
		float dx = ax - bx;
		float dz = az - bz;
		return tol > 1f ? (dx * dx + dz * dz <= tol * tol) : (dx * dx + dz * dz < 1f);
	}

	private static void Elems(int m, out int[] fx, out int[] fz)
	{
		switch (m)
		{
		case 1:
			fx = new int[]
			{
				1
			};
			fz = new int[1];
			break;
		case 2:
			fx = new int[1];
			fz = new int[]
			{
				1
			};
			break;
		case 3:
			fx = new int[]
			{
				1
			};
			fz = new int[]
			{
				1
			};
			break;
		case 4:
			fx = new int[]
			{
				1,
				0,
				1
			};
			fz = new int[]
			{
				0,
				1,
				1
			};
			break;
		default:
			fx = new int[0];
			fz = new int[0];
			break;
		}
	}

	private static void Log(string s)
	{
		try
		{
			Directory.CreateDirectory(BtcPaths.Debug);
			File.AppendAllText(BtcPaths.DebugFile("btc_symmetry.txt"), DateTime.Now.ToString("HH:mm:ss") + " " + s + "\r\n");
		}
		catch (Exception)
		{
		}
	}

	internal static string T(string en, string ru)
	{
		return BtcGameplay.T(en, ru);
	}

	private static string Encode()
	{
		return string.Concat(new object[]
		{
			BtcSymmetry.Mode,
			"|",
			BtcSymmetry._keep,
			"|",
			BtcSymmetry._dx,
			"|",
			BtcSymmetry._dz,
			"|",
			BtcSymmetry._obj ? 1 : 0,
			"|",
			BtcSymmetry._seed,
			"|",
			BtcSymmetry._go
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
			bool flag = array.Length < 6;
			if (flag)
			{
				result = false;
			}
			else
			{
				BtcSymmetry.Mode = Mathf.Clamp(int.Parse(array[0]), 0, 4);
				BtcSymmetry._keep = Mathf.Clamp(int.Parse(array[1]), 0, 3);
				BtcSymmetry._dx = Mathf.Clamp(int.Parse(array[2]), -400, 400);
				BtcSymmetry._dz = Mathf.Clamp(int.Parse(array[3]), -400, 400);
				BtcSymmetry._obj = (array[4] == "1");
				BtcSymmetry._seed = Mathf.Clamp(int.Parse(array[5]), 0, 999999);
				BtcSymmetry._seedStr = BtcSymmetry._seed.ToString();
				BtcSymmetry._go = ((array.Length > 6) ? Mathf.Clamp(int.Parse(array[6]), 0, 7) : 0);
				bool flag2 = BtcSymmetry.Mode < 3;
				if (flag2)
				{
					BtcSymmetry._keep &= 1;
				}
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
		bool prefsLoaded = BtcSymmetry._prefsLoaded;
		if (!prefsLoaded)
		{
			BtcSymmetry._prefsLoaded = true;
			BtcSymmetry.Decode(PlayerPrefs.GetString("BtcSymmetry", string.Empty));
		}
	}

	private static void Changed()
	{
		PlayerPrefs.SetString("BtcSymmetry", BtcSymmetry.Encode());
		BtcSymmetry.Broadcast();
	}

	public static void RegisterHandlers(NetworkClient clientOverride)
	{
		bool active = NetworkServer.active;
		if (active)
		{
			BtcSymmetry._prefsLoaded = false;
			BtcSymmetry.LoadPrefsOnce();
		}
		else
		{
			BtcSymmetry.Mode = 0;
			BtcSymmetry._keep = 0;
			BtcSymmetry._obj = false;
			BtcSymmetry._go = 0;
		}
		UNetManager unetManager = NetworkManager.singleton as UNetManager;
		NetworkClient networkClient = clientOverride ?? ((unetManager != null) ? unetManager.LocalNetworkClient : null);
		bool flag = networkClient != null;
		if (flag)
		{
			networkClient.UnregisterHandler(89);
			networkClient.RegisterHandler(89, new NetworkMessageDelegate(BtcSymmetry.OnState));
		}
	}

	public static void SendState(NetworkConnection conn)
	{
		bool flag = conn != null && BtcBc.BcEnabled;
		if (flag)
		{
			BtcSymmetry.LoadPrefsOnce();
			conn.Send(89, new BtcSymmetry.Msg
			{
				data = BtcSymmetry.Encode()
			});
		}
	}

	private static void Broadcast()
	{
		bool flag = NetworkServer.active && BtcBc.BcEnabled;
		if (flag)
		{
			NetworkServer.SendToAll(89, new BtcSymmetry.Msg
			{
				data = BtcSymmetry.Encode()
			});
		}
	}

	private static void OnState(NetworkMessage netMsg)
	{
		try
		{
			BtcSymmetry.Msg msg = netMsg.ReadMessage<BtcSymmetry.Msg>();
			bool flag = !NetworkServer.active;
			if (flag)
			{
				BtcSymmetry.Decode(msg.data);
			}
		}
		catch (Exception)
		{
		}
	}

	public static void Prepare()
	{
		BtcSymmetry.Restore();
		BtcSymmetry._am = 0;
		try
		{
			bool flag = BtcSymmetry.Mode == 0 || !BtcBc.BcEnabled;
			if (!flag)
			{
				Asset_Map map = GameParam.Map;
				bool flag2 = map == null || !map.IsNewGame();
				if (flag2)
				{
					BtcSymmetry.Log("skip: not a new game");
				}
				else
				{
					string path = map.path;
					string path2 = Path.Combine(path, "Heightmap.png");
					bool flag3 = !File.Exists(path2);
					if (flag3)
					{
						BtcSymmetry.Log("skip: no Heightmap.png in " + path);
					}
					else
					{
						int mode = BtcSymmetry.Mode;
						int num = (mode < 3) ? (BtcSymmetry._keep & 1) : BtcSymmetry._keep;
						bool obj = BtcSymmetry._obj;
						int go = BtcSymmetry._go;
						int seed = BtcSymmetry._seed;
						int dx = BtcSymmetry._dx;
						int dz = BtcSymmetry._dz;
						Texture2D texture2D = new Texture2D(2, 2, TextureFormat.RGBA32, false);
						texture2D.LoadImage(File.ReadAllBytes(path2));
						int width = texture2D.width;
						int height = texture2D.height;
						Color32[] pixels = texture2D.GetPixels32();
						int num2;
						int num3;
						int num4;
						int num5;
						BtcSymmetry.Bounds(pixels, width, height, out num2, out num3, out num4, out num5);
						bool flag4 = num4 < num2;
						if (flag4)
						{
							UnityEngine.Object.Destroy(texture2D);
							BtcSymmetry.Log("skip: empty heightmap");
						}
						else
						{
							int num6 = num2 + num4 + dx;
							int num7 = num3 + num5 + dz;
							string text = Path.Combine(BtcPaths.Root, "symmetry");
							string text2 = BtcSymmetry.Hash(string.Concat(new object[]
							{
								path,
								"|",
								File.GetLastWriteTimeUtc(path2).Ticks,
								"|",
								mode,
								"|",
								num,
								"|",
								num6,
								"|",
								num7,
								"|",
								obj ? 1 : 0,
								"|",
								go,
								"|",
								seed
							}));
							string text3 = Path.Combine(text, text2);
							bool flag5 = !File.Exists(Path.Combine(text3, ".ok"));
							if (flag5)
							{
								DateTime now = DateTime.Now;
								string text4 = text3 + "_tmp";
								bool flag6 = Directory.Exists(text4);
								if (flag6)
								{
									Directory.Delete(text4, true);
								}
								bool flag7 = Directory.Exists(text3);
								if (flag7)
								{
									Directory.Delete(text3, true);
								}
								Directory.CreateDirectory(text4);
								BtcSymmetry.CopyDir(path, text4);
								BtcSymmetry.BuildHeightmap(texture2D, pixels, width, height, mode, num, num6, num7, Path.Combine(text4, "Heightmap.png"));
								BtcSymmetry.BuildJson(text4, mode, num, num6, num7, obj, go);
								BtcSymmetry.PatchSeed(text4, seed);
								File.WriteAllText(Path.Combine(text4, ".ok"), "ok");
								Directory.Move(text4, text3);
								BtcSymmetry.Log(string.Concat(new object[]
								{
									"built ",
									text2,
									" mode=",
									mode,
									" keep=",
									num,
									" Sx=",
									num6,
									" Sz=",
									num7,
									" objs=",
									obj.ToString(),
									" go=",
									go,
									" seed=",
									seed,
									" in ",
									(DateTime.Now - now).TotalMilliseconds.ToString("0"),
									" ms"
								}));
							}
							else
							{
								BtcSymmetry.Log("cache hit " + text2);
							}
							UnityEngine.Object.Destroy(texture2D);
							BtcSymmetry.CleanOthers(text, text2);
							BtcSymmetry.SetPath(map, text3);
							BtcSymmetry._swapped = map;
							BtcSymmetry._origPath = path;
							BtcSymmetry._ak = num;
							BtcSymmetry._asx = num6;
							BtcSymmetry._asz = num7;
							BtcSymmetry._am = mode;
						}
					}
				}
			}
		}
		catch (Exception arg)
		{
			BtcSymmetry.Log("Prepare failed: " + arg);
			BtcSymmetry.Restore();
			BtcSymmetry._am = 0;
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

	private static void Restore()
	{
		bool flag = BtcSymmetry._swapped == null;
		if (!flag)
		{
			try
			{
				BtcSymmetry.SetPath(BtcSymmetry._swapped, BtcSymmetry._origPath);
			}
			catch (Exception ex)
			{
				BtcSymmetry.Log("Restore failed: " + ex.Message);
			}
			BtcSymmetry._swapped = null;
			BtcSymmetry._origPath = null;
		}
	}

	public static void Tick()
	{
		bool flag = (BtcSymmetry._swapped != null || BtcSymmetry._am != 0) && BtcGameplay.InLobby;
		if (flag)
		{
			BtcSymmetry.Restore();
			BtcSymmetry._am = 0;
		}
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
			BtcSymmetry.CopyDir(text2, text3);
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

	private static void BuildHeightmap(Texture2D tex, Color32[] src, int w, int h, int m, int keep, int sx, int sz, string outPath)
	{
		Color32[] array = new Color32[src.Length];
		for (int i = 0; i < h; i++)
		{
			int num = i * w;
			for (int j = 0; j < w; j++)
			{
				float num2 = (float)j;
				float num3 = (float)i;
				BtcSymmetry.Fold(m, keep, sx, sz, ref num2, ref num3);
				int num4 = (int)num2;
				int num5 = (int)num3;
				bool flag = num4 >= 0 && num4 < w && num5 >= 0 && num5 < h;
				if (flag)
				{
					array[num + j] = src[num5 * w + num4];
				}
			}
		}
		tex.SetPixels32(array);
		tex.Apply(false);
		File.WriteAllBytes(outPath, tex.EncodeToPNG());
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

	private static JToken SetNum(JToken old, double v, bool intHint)
	{
		bool flag = (old != null && old.Type == JTokenType.Integer) || (old == null && intHint);
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

	private static void FlipVec(JToken t, bool fx, bool fz, bool intHint, int sx, int sz)
	{
		JObject jobject = t as JObject;
		bool flag = jobject == null;
		if (!flag)
		{
			if (fx)
			{
				jobject["x"] = BtcSymmetry.SetNum(jobject["x"], (double)sx - BtcSymmetry.Num(jobject["x"]), intHint);
			}
			if (fz)
			{
				jobject["z"] = BtcSymmetry.SetNum(jobject["z"], (double)sz - BtcSymmetry.Num(jobject["z"]), intHint);
			}
		}
	}

	private static void FlipRot(JToken t, bool fx, bool fz)
	{
		JObject jobject = t as JObject;
		bool flag = jobject == null;
		if (!flag)
		{
			double num = BtcSymmetry.Num(jobject["w"]);
			double num2 = BtcSymmetry.Num(jobject["y"]);
			if (fz)
			{
				double num3 = num;
				num = num2;
				num2 = num3;
			}
			if (fx)
			{
				num2 = -num2;
			}
			jobject["w"] = new JValue(num);
			jobject["y"] = new JValue(num2);
		}
	}

	private static JArray Expand(JArray src, BtcSymmetry.PosFn pos, BtcSymmetry.MvFn mv, int m, int keep, int sx, int sz, out int dropped, out int added, float tol = 1f)
	{
		dropped = 0;
		added = 0;
		int[] array;
		int[] array2;
		BtcSymmetry.Elems(m, out array, out array2);
		JArray jarray = new JArray();
		List<float[]> list = new List<float[]>();
		foreach (JToken jtoken in src)
		{
			float[] array3 = pos(jtoken);
			bool flag = array3 == null;
			if (flag)
			{
				jarray.Add(jtoken.DeepClone());
			}
			else
			{
				bool flag2 = !BtcSymmetry.Kept(m, keep, sx, sz, array3[0], array3[1], tol);
				if (flag2)
				{
					dropped++;
				}
				else
				{
					jarray.Add(jtoken.DeepClone());
					list.Clear();
					for (int i = 0; i < array.Length; i++)
					{
						float num = (array[i] == 1) ? ((float)sx - array3[0]) : array3[0];
						float num2 = (array2[i] == 1) ? ((float)sz - array3[1]) : array3[1];
						bool flag3 = BtcSymmetry.Near(num, num2, array3[0], array3[1], tol);
						if (!flag3)
						{
							bool flag4 = false;
							for (int j = 0; j < list.Count; j++)
							{
								bool flag5 = BtcSymmetry.Near(list[j][0], list[j][1], num, num2, tol);
								if (flag5)
								{
									flag4 = true;
									break;
								}
							}
							bool flag6 = flag4;
							if (!flag6)
							{
								list.Add(new float[]
								{
									num,
									num2
								});
								JToken jtoken2 = jtoken.DeepClone();
								mv(jtoken2, array[i] == 1, array2[i] == 1);
								jarray.Add(jtoken2);
								added++;
							}
						}
					}
				}
			}
		}
		return jarray;
	}

	private static float[] PosFlat(JToken t)
	{
		JObject jobject = t as JObject;
		bool flag = jobject == null || jobject["x"] == null || jobject["z"] == null;
		float[] result;
		if (flag)
		{
			result = null;
		}
		else
		{
			result = new float[]
			{
				(float)BtcSymmetry.Num(jobject["x"]),
				(float)BtcSymmetry.Num(jobject["z"])
			};
		}
		return result;
	}

	private static float[] PosOf(JToken t, string key)
	{
		JObject jobject = t as JObject;
		bool flag = jobject == null;
		float[] result;
		if (flag)
		{
			result = null;
		}
		else
		{
			result = BtcSymmetry.PosFlat(jobject[key]);
		}
		return result;
	}

	private static float[] PosPosition(JToken t)
	{
		return BtcSymmetry.PosOf(t, "position");
	}

	private static float[] PosDisplay(JToken t)
	{
		return BtcSymmetry.PosOf(t, "displayPos");
	}

	private static float[] PosBlock(JToken t)
	{
		return BtcSymmetry.PosOf(t, "P");
	}

	private static void ProcessFile(string dir, string name, BtcSymmetry.PosFn pos, BtcSymmetry.MvFn mv, int m, int keep, int sx, int sz, float tol = 1f)
	{
		string path = Path.Combine(dir, name);
		bool flag = !File.Exists(path);
		if (!flag)
		{
			JArray jarray = JArray.Parse(File.ReadAllText(path));
			int num;
			int num2;
			JArray jarray2 = BtcSymmetry.Expand(jarray, pos, mv, m, keep, sx, sz, out num, out num2, tol);
			File.WriteAllText(path, jarray2.ToString(Formatting.None, new JsonConverter[0]));
			BtcSymmetry.Log(string.Concat(new object[]
			{
				name,
				": ",
				jarray.Count,
				" -> ",
				jarray2.Count,
				" (dropped ",
				num,
				", added ",
				num2,
				")"
			}));
		}
	}

	private static void BuildJson(string dir, int m, int keep, int sx, int sz, bool objs, int go)
	{
		BtcSymmetry.ProcessFile(dir, "trees.json", new BtcSymmetry.PosFn(BtcSymmetry.PosFlat), delegate(JToken t, bool fx, bool fz)
		{
			BtcSymmetry.FlipVec(t, fx, fz, true, sx, sz);
		}, m, keep, sx, sz);
		BtcSymmetry.ProcessFile(dir, "plants.json", new BtcSymmetry.PosFn(BtcSymmetry.PosFlat), delegate(JToken t, bool fx, bool fz)
		{
			BtcSymmetry.FlipVec(t, fx, fz, true, sx, sz);
		}, m, keep, sx, sz);
		BtcSymmetry.ProcessFile(dir, "RemovedVoxels.json", new BtcSymmetry.PosFn(BtcSymmetry.PosFlat), delegate(JToken t, bool fx, bool fz)
		{
			BtcSymmetry.FlipVec(t, fx, fz, true, sx, sz);
		}, m, keep, sx, sz);
		BtcSymmetry.ProcessFile(dir, "iron.json", new BtcSymmetry.PosFn(BtcSymmetry.PosPosition), delegate(JToken t, bool fx, bool fz)
		{
			BtcSymmetry.FlipVec(((JObject)t)["position"], fx, fz, true, sx, sz);
		}, m, keep, sx, sz);
		BtcSymmetry.ProcessFile(dir, "pyrite.json", new BtcSymmetry.PosFn(BtcSymmetry.PosPosition), delegate(JToken t, bool fx, bool fz)
		{
			BtcSymmetry.FlipVec(((JObject)t)["position"], fx, fz, true, sx, sz);
		}, m, keep, sx, sz);
		BtcSymmetry.ProcessFile(dir, "boulders.json", new BtcSymmetry.PosFn(BtcSymmetry.PosDisplay), delegate(JToken t, bool fx, bool fz)
		{
			BtcSymmetry.MvBoulder(t, fx, fz, sx, sz);
		}, m, keep, sx, sz);
		string text = Path.Combine(dir, "TerrainSurfaceLayer.json");
		bool flag = File.Exists(text) && new FileInfo(text).Length > 80L;
		if (flag)
		{
			BtcSymmetry.Log("WARNING: TerrainSurfaceLayer.json is not empty, it is NOT mirrored");
		}
		if (objs)
		{
			BtcSymmetry.ProcessFile(dir, "PlacedBlocks.json", new BtcSymmetry.PosFn(BtcSymmetry.PosBlock), delegate(JToken t, bool fx, bool fz)
			{
				BtcSymmetry.FlipVec(((JObject)t)["P"], fx, fz, true, sx, sz);
				BtcSymmetry.FlipRot(((JObject)t)["R"], fx, fz);
			}, m, keep, sx, sz);
			BtcSymmetry.ProcessFile(dir, "LuaMarkersCount.json", new BtcSymmetry.PosFn(BtcSymmetry.PosPosition), delegate(JToken t, bool fx, bool fz)
			{
				JObject jobject = (JObject)t;
				BtcSymmetry.FlipVec(jobject["position"], fx, fz, false, sx, sz);
				BtcSymmetry.FlipVec(jobject["localPosition"], fx, fz, false, sx, sz);
				BtcSymmetry.FlipRot(jobject["rotation"], fx, fz);
				BtcSymmetry.FlipRot(jobject["localRotation"], fx, fz);
			}, m, keep, sx, sz, 4.01f);
		}
		bool flag2 = objs || go != 0;
		if (flag2)
		{
			BtcSymmetry.BuildGameObjects(dir, m, keep, sx, sz, objs, go);
		}
	}

	private static void MvBoulder(JToken t, bool fx, bool fz, int sx, int sz)
	{
		JObject jobject = (JObject)t;
		BtcSymmetry.FlipVec(jobject["displayPos"], fx, fz, false, sx, sz);
		JObject jobject2 = jobject["offset"] as JObject;
		bool flag = jobject2 != null;
		if (flag)
		{
			bool flag2 = fx && jobject2["x"] != null;
			if (flag2)
			{
				jobject2["x"] = new JValue(-BtcSymmetry.Num(jobject2["x"]));
			}
			bool flag3 = fz && jobject2["z"] != null;
			if (flag3)
			{
				jobject2["z"] = new JValue(-BtcSymmetry.Num(jobject2["z"]));
			}
		}
	}

	private static int Cat(string key)
	{
		bool flag = string.IsNullOrEmpty(key);
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

	private static string Str(JToken t)
	{
		return (t != null && t.Type == JTokenType.String) ? ((string)t) : null;
	}

	private static string Mapped(Dictionary<string, string> remap, int ck, string id)
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

	private static string ReplaceId(string s, string o, string n)
	{
		return s.Replace("\"" + o + "\"", "\"" + n + "\"").Replace("/" + o + "/", "/" + n + "/").Replace("/" + o + "\"", "/" + n + "\"");
	}

	private static void BuildGameObjects(string dir, int m, int keep, int sx, int sz, bool objs, int go)
	{
		string path = Path.Combine(dir, "gameobjects.json");
		bool flag = !File.Exists(path);
		if (!flag)
		{
			JArray jarray = JArray.Parse(File.ReadAllText(path));
			int count = jarray.Count;
			int num = (objs ? 1 : 0) | go << 1;
			int[] array;
			int[] array2;
			BtcSymmetry.Elems(m, out array, out array2);
			Dictionary<string, int> dictionary = new Dictionary<string, int>();
			JObject[] array3 = new JObject[count];
			float[][] array4 = new float[count][];
			bool[] array5 = new bool[count];
			for (int i = 0; i < count; i++)
			{
				JObject jobject = jarray[i] as JObject;
				array3[i] = jobject;
				bool flag2 = jobject == null;
				if (!flag2)
				{
					string text = BtcSymmetry.Str(jobject["$id"]);
					bool flag3 = text != null && !dictionary.ContainsKey(text);
					if (flag3)
					{
						dictionary[text] = i;
					}
					bool flag4 = (BtcSymmetry.Cat(BtcSymmetry.Str(jobject["$assetKey"])) & num) != 0;
					if (flag4)
					{
						array4[i] = BtcSymmetry.MarkerPos(jobject);
						array5[i] = (array4[i] != null);
					}
				}
			}
			int[] array6 = new int[count];
			for (int j = 0; j < count; j++)
			{
				array6[j] = -1;
				bool flag5 = array3[j] == null;
				if (!flag5)
				{
					int num2 = j;
					int num3 = 0;
					while (num2 >= 0 && num3++ < 64)
					{
						bool flag6 = array5[num2];
						if (flag6)
						{
							array6[j] = num2;
						}
						string text2 = BtcSymmetry.Str(array3[num2]["$parent"]);
						int num4;
						num2 = ((text2 != null && dictionary.TryGetValue(text2, out num4)) ? num4 : -1);
					}
				}
			}
			Dictionary<int, List<int>> dictionary2 = new Dictionary<int, List<int>>();
			List<float[]> list = new List<float[]>();
			int num5 = 0;
			int num6 = 0;
			for (int k = 0; k < count; k++)
			{
				bool flag7 = array6[k] != k;
				if (!flag7)
				{
					float[] array7 = array4[k];
					bool flag8 = !BtcSymmetry.Kept(m, keep, sx, sz, array7[0], array7[1], 4.01f);
					if (flag8)
					{
						num5++;
					}
					else
					{
						list.Clear();
						List<int> list2 = new List<int>();
						for (int l = 0; l < array.Length; l++)
						{
							float num7 = (array[l] == 1) ? ((float)sx - array7[0]) : array7[0];
							float num8 = (array2[l] == 1) ? ((float)sz - array7[1]) : array7[1];
							bool flag9 = BtcSymmetry.Near(num7, num8, array7[0], array7[1], 4.01f);
							if (!flag9)
							{
								bool flag10 = false;
								for (int n = 0; n < list.Count; n++)
								{
									bool flag11 = BtcSymmetry.Near(list[n][0], list[n][1], num7, num8, 4.01f);
									if (flag11)
									{
										flag10 = true;
										break;
									}
								}
								bool flag12 = flag10;
								if (!flag12)
								{
									list.Add(new float[]
									{
										num7,
										num8
									});
									list2.Add(l);
								}
							}
						}
						dictionary2[k] = list2;
					}
				}
			}
			JArray jarray2 = new JArray();
			Dictionary<string, string> remap = new Dictionary<string, string>();
			for (int num9 = 0; num9 < count; num9++)
			{
				bool flag13 = array6[num9] < 0;
				if (flag13)
				{
					jarray2.Add(jarray[num9].DeepClone());
				}
				else
				{
					int num10 = array6[num9];
					List<int> list3;
					bool flag14 = !dictionary2.TryGetValue(num10, out list3);
					if (!flag14)
					{
						jarray2.Add(jarray[num9].DeepClone());
						for (int num11 = 0; num11 < list3.Count; num11++)
						{
							int num12 = list3[num11];
							int ck = num10 * 8 + num12;
							JObject jobject2 = (JObject)array3[num9].DeepClone();
							BtcSymmetry.WalkFlip(jobject2, array[num12] == 1, array2[num12] == 1, sx, sz);
							List<string> list4 = new List<string>();
							BtcSymmetry.CollectIds(jobject2, list4);
							int num13 = num9;
							int num14 = 0;
							while (num13 != num10 && num14++ < 64)
							{
								string text3 = BtcSymmetry.Str(array3[num13]["$parent"]);
								int num15 = default(int);
								bool flag15 = text3 == null || !dictionary.TryGetValue(text3, out num15);
								if (flag15)
								{
									break;
								}
								list4.Add(text3);
								num13 = num15;
							}
							string text4 = jobject2.ToString(Formatting.None, new JsonConverter[0]);
							for (int num16 = 0; num16 < list4.Count; num16++)
							{
								text4 = BtcSymmetry.ReplaceId(text4, list4[num16], BtcSymmetry.Mapped(remap, ck, list4[num16]));
							}
							jarray2.Add(JObject.Parse(text4));
							num6++;
						}
					}
				}
			}
			File.WriteAllText(path, jarray2.ToString(Formatting.None, new JsonConverter[0]));
			BtcSymmetry.Log(string.Concat(new object[]
			{
				"gameobjects.json (mask ",
				num,
				"): roots dropped ",
				num5,
				", objects added ",
				num6
			}));
		}
	}

	private static float[] MarkerPos(JObject go)
	{
		JArray jarray = go["components"] as JArray;
		bool flag = jarray == null;
		float[] result;
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
					return BtcSymmetry.PosFlat(jobject["position"]);
				}
			}
			result = null;
		}
		return result;
	}

	private static void WalkFlip(JToken t, bool fx, bool fz, int sx, int sz)
	{
		JObject jobject = t as JObject;
		bool flag = jobject != null;
		if (flag)
		{
			string text = (jobject["$type"] != null) ? ((string)jobject["$type"]) : string.Empty;
			bool flag2 = text.StartsWith("UnityEngine.Transform");
			if (flag2)
			{
				BtcSymmetry.FlipVec(jobject["position"], fx, fz, false, sx, sz);
				BtcSymmetry.FlipRot(jobject["rotation"], fx, fz);
			}
			else
			{
				bool flag3 = text.StartsWith("Brix.Legacy.Beacon,");
				if (flag3)
				{
					BtcSymmetry.FlipVec(jobject["position"], fx, fz, true, sx, sz);
				}
			}
			List<JToken> list = new List<JToken>();
			foreach (JProperty jproperty in jobject.Properties())
			{
				list.Add(jproperty.Value);
			}
			for (int i = 0; i < list.Count; i++)
			{
				BtcSymmetry.WalkFlip(list[i], fx, fz, sx, sz);
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
					BtcSymmetry.WalkFlip(t2, fx, fz, sx, sz);
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
				BtcSymmetry.CollectIds(jproperty.Value, ids);
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
					BtcSymmetry.CollectIds(t2, ids);
				}
			}
		}
	}

	private static JObject Reid(JObject c)
	{
		List<string> list = new List<string>();
		BtcSymmetry.CollectIds(c, list);
		string text = c.ToString(Formatting.None, new JsonConverter[0]);
		for (int i = 0; i < list.Count; i++)
		{
			text = text.Replace(list[i], "go:" + Guid.NewGuid().ToString());
		}
		return JObject.Parse(text);
	}

	private static void PatchSeed(string dir, int seed)
	{
		bool flag = seed <= 0;
		if (!flag)
		{
			string path = Path.Combine(dir, "noise.json");
			bool flag2 = !File.Exists(path);
			if (flag2)
			{
				BtcSymmetry.Log("seed " + seed + " ignored: map has no noise.json");
			}
			else
			{
				string text = File.ReadAllText(path);
				Regex regex = new Regex("\"__GLOBALSEED\"\\s*:\\s*-?\\d+");
				bool flag3 = !regex.IsMatch(text);
				if (flag3)
				{
					BtcSymmetry.Log("seed " + seed + " ignored: no __GLOBALSEED in noise.json");
				}
				else
				{
					text = regex.Replace(text, "\"__GLOBALSEED\": " + seed, 1);
					File.WriteAllText(path, text);
				}
			}
		}
	}

	private static void EnsurePreview()
	{
		bool flag = Time.realtimeSinceStartup < BtcSymmetry._pvNext;
		if (!flag)
		{
			BtcSymmetry._pvNext = Time.realtimeSinceStartup + 1f;
			Asset_Map map = GameParam.Map;
			string text = null;
			bool flag2 = map != null;
			if (flag2)
			{
				try
				{
					string path = Path.Combine(map.path, "Heightmap.png");
					bool flag3 = File.Exists(path);
					if (flag3)
					{
						text = map.path + "|" + File.GetLastWriteTimeUtc(path).Ticks;
					}
				}
				catch (Exception)
				{
				}
			}
			bool flag4 = text == BtcSymmetry._pvKey;
			if (!flag4)
			{
				BtcSymmetry._pvKey = text;
				BtcSymmetry._pvOk = false;
				bool flag5 = BtcSymmetry._pvTex != null;
				if (flag5)
				{
					UnityEngine.Object.Destroy(BtcSymmetry._pvTex);
					BtcSymmetry._pvTex = null;
				}
				BtcSymmetry._marks.Clear();
				bool flag6 = text == null;
				if (!flag6)
				{
					try
					{
						BtcSymmetry.BuildPreview(map.path);
						BtcSymmetry._pvOk = true;
					}
					catch (Exception ex)
					{
						BtcSymmetry.Log("preview failed: " + ex.Message);
					}
				}
			}
		}
	}

	private static void BuildPreview(string path)
	{
		Texture2D texture2D = new Texture2D(2, 2, TextureFormat.RGBA32, false);
		texture2D.LoadImage(File.ReadAllBytes(Path.Combine(path, "Heightmap.png")));
		int width = texture2D.width;
		int height = texture2D.height;
		Color32[] pixels = texture2D.GetPixels32();
		UnityEngine.Object.Destroy(texture2D);
		int num;
		int num2;
		int num3;
		int num4;
		BtcSymmetry.Bounds(pixels, width, height, out num, out num2, out num3, out num4);
		bool flag = num3 < num;
		if (!flag)
		{
			int num5 = num3 - num + 1;
			int num6 = num4 - num2 + 1;
			Color32[] array = new Color32[num5 * num6];
			for (int i = 0; i < num6; i++)
			{
				for (int j = 0; j < num5; j++)
				{
					Color32 color = pixels[(i + num2) * width + (j + num)];
					bool flag2 = (color.r | color.g | color.b | color.a) == 0;
					if (!flag2)
					{
						Color color2 = Color.HSVToRGB((float)(color.b * 37 % 100) / 100f, 0.4f, 0.3f + 0.7f * (float)color.g / 200f);
						array[i * num5 + j] = new Color32((byte)(color2.r * 255f), (byte)(color2.g * 255f), (byte)(color2.b * 255f), byte.MaxValue);
					}
				}
			}
			Texture2D texture2D2 = new Texture2D(num5, num6, TextureFormat.RGBA32, false);
			texture2D2.filterMode = FilterMode.Point;
			texture2D2.hideFlags = HideFlags.HideAndDontSave;
			texture2D2.SetPixels32(array);
			texture2D2.Apply(false);
			BtcSymmetry._pvTex = texture2D2;
			BtcSymmetry._pvPx = array;
			BtcSymmetry._pvGen++;
			BtcSymmetry._pvMinX = num;
			BtcSymmetry._pvMinZ = num2;
			BtcSymmetry._pvW = num5;
			BtcSymmetry._pvH = num6;
			BtcSymmetry._defSx = num + num3;
			BtcSymmetry._defSz = num2 + num4;
			string path2 = Path.Combine(path, "noise.json");
			BtcSymmetry._hasNoise = File.Exists(path2);
			string path3 = Path.Combine(path, "LuaMarkersCount.json");
			bool flag3 = File.Exists(path3);
			if (flag3)
			{
				JArray jarray = JArray.Parse(File.ReadAllText(path3));
				foreach (JToken jtoken in jarray)
				{
					JObject jobject = jtoken as JObject;
					bool flag4 = jobject == null || jobject["position"] == null;
					if (!flag4)
					{
						string text = string.Empty;
						JToken jtoken2 = (jobject["markerGroups"] != null) ? jobject["markerGroups"]["$values"] : null;
						JArray jarray2 = jtoken2 as JArray;
						bool flag5 = jarray2 != null && jarray2.Count > 0;
						if (flag5)
						{
							text = (string)jarray2[0];
						}
						string text2 = (jobject["markerTag"] != null) ? ((string)jobject["markerTag"]) : string.Empty;
						float num7 = 0f;
						bool flag6 = text.IndexOf("Start", StringComparison.OrdinalIgnoreCase) >= 0 || text2.IndexOf("Start", StringComparison.OrdinalIgnoreCase) >= 0;
						if (flag6)
						{
							num7 = 1f;
						}
						else
						{
							bool flag7 = text.IndexOf("Crystal", StringComparison.OrdinalIgnoreCase) >= 0;
							if (flag7)
							{
								num7 = 2f;
							}
							else
							{
								bool flag8 = text.IndexOf("Corruptron", StringComparison.OrdinalIgnoreCase) >= 0;
								if (flag8)
								{
									num7 = 3f;
								}
							}
						}
						BtcSymmetry._marks.Add(new float[]
						{
							(float)BtcSymmetry.Num(jobject["position"]["x"]),
							(float)BtcSymmetry.Num(jobject["position"]["z"]),
							num7
						});
					}
				}
			}
		}
	}

	// Alt preview: heightmap + markers as they will be after symmetry. Cached; rebuilt only when settings change.
	private static bool EnsureSym(int m, int sx, int sz)
	{
		if (BtcSymmetry._pvPx == null || BtcSymmetry._pvTex == null)
		{
			return false;
		}
		int keep = BtcSymmetry._keep;
		bool obj = BtcSymmetry._obj;
		if (BtcSymmetry._symTex != null && BtcSymmetry._symGen == BtcSymmetry._pvGen && BtcSymmetry._symMode == m && BtcSymmetry._symKeep == keep && BtcSymmetry._symSx == sx && BtcSymmetry._symSz == sz && BtcSymmetry._symObj == obj)
		{
			return true;
		}
		try
		{
			int w = BtcSymmetry._pvW;
			int h = BtcSymmetry._pvH;
			Color32[] dst = new Color32[w * h];
			for (int i = 0; i < h; i++)
			{
				for (int j = 0; j < w; j++)
				{
					float fx = (float)(j + BtcSymmetry._pvMinX);
					float fz = (float)(i + BtcSymmetry._pvMinZ);
					BtcSymmetry.Fold(m, keep, sx, sz, ref fx, ref fz);
					int px = (int)fx - BtcSymmetry._pvMinX;
					int pz = (int)fz - BtcSymmetry._pvMinZ;
					if (px >= 0 && px < w && pz >= 0 && pz < h)
					{
						dst[i * w + j] = BtcSymmetry._pvPx[pz * w + px];
					}
				}
			}
			if (BtcSymmetry._symTex != null)
			{
				UnityEngine.Object.Destroy(BtcSymmetry._symTex);
			}
			Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
			tex.filterMode = FilterMode.Point;
			tex.hideFlags = HideFlags.HideAndDontSave;
			tex.SetPixels32(dst);
			tex.Apply(false);
			BtcSymmetry._symTex = tex;
			BtcSymmetry._symMarks.Clear();
			int[] ex;
			int[] ez;
			BtcSymmetry.Elems(m, out ex, out ez);
			List<float[]> marks = BtcSymmetry._marks;
			for (int k = 0; k < marks.Count; k++)
			{
				float[] mk = marks[k];
				if (!obj)
				{
					BtcSymmetry._symMarks.Add(mk);
					continue;
				}
				if (!BtcSymmetry.Kept(m, keep, sx, sz, mk[0], mk[1], mk[2] == 3f ? 1f : 4.01f))
				{
					continue;
				}
				BtcSymmetry._symMarks.Add(mk);
				int first = BtcSymmetry._symMarks.Count;
				for (int l = 0; l < ex.Length; l++)
				{
					float cx = (ex[l] == 1) ? ((float)sx - mk[0]) : mk[0];
					float cz = (ez[l] == 1) ? ((float)sz - mk[1]) : mk[1];
					bool dup = BtcSymmetry.Near(cx, cz, mk[0], mk[1], mk[2] == 3f ? 1f : 4.01f);
					for (int n = first; n < BtcSymmetry._symMarks.Count && !dup; n++)
					{
						dup = BtcSymmetry.Near(BtcSymmetry._symMarks[n][0], BtcSymmetry._symMarks[n][1], cx, cz, mk[2] == 3f ? 1f : 4.01f);
					}
					if (!dup)
					{
						BtcSymmetry._symMarks.Add(new float[] { cx, cz, mk[2] });
					}
				}
			}
			BtcSymmetry._symGen = BtcSymmetry._pvGen;
			BtcSymmetry._symMode = m;
			BtcSymmetry._symKeep = keep;
			BtcSymmetry._symSx = sx;
			BtcSymmetry._symSz = sz;
			BtcSymmetry._symObj = obj;
			return true;
		}
		catch (Exception ex2)
		{
			BtcSymmetry.Log("alt preview failed: " + ex2.Message);
			BtcSymmetry._symGen = -1;
			return false;
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

	internal static void Styles(int fs)
	{
		bool flag = BtcSymmetry._label != null && BtcSymmetry._styleFs == fs;
		if (!flag)
		{
			BtcSymmetry._styleFs = fs;
			BtcSymmetry._label = BtcSymmetry.Make(fs, TextAnchor.MiddleLeft, FontStyle.Normal, Color.white);
			BtcSymmetry._note = BtcSymmetry.Make(Mathf.Max(10, (int)((float)fs * 0.8f)), TextAnchor.UpperLeft, FontStyle.Normal, new Color(1f, 1f, 1f, 0.55f));
			BtcSymmetry._note.wordWrap = true;
			BtcSymmetry._sub = BtcSymmetry.Make(fs, TextAnchor.MiddleLeft, FontStyle.Bold, BtcSymmetry.Accent);
			BtcSymmetry._btn = BtcSymmetry.Make(Mathf.Max(10, (int)((float)fs * 0.9f)), TextAnchor.MiddleCenter, FontStyle.Normal, Color.white);
			BtcSymmetry._field = new GUIStyle(GUI.skin.textField);
			BtcSymmetry._field.fontSize = Mathf.Max(10, (int)((float)fs * 0.9f));
			BtcSymmetry._field.alignment = TextAnchor.MiddleCenter;
		}
	}

	internal static Texture2D White()
	{
		bool flag = BtcSymmetry._white == null;
		if (flag)
		{
			BtcSymmetry._white = new Texture2D(1, 1, TextureFormat.RGBA32, false);
			BtcSymmetry._white.SetPixel(0, 0, Color.white);
			BtcSymmetry._white.Apply();
			BtcSymmetry._white.hideFlags = HideFlags.HideAndDontSave;
		}
		return BtcSymmetry._white;
	}

	internal static void Note(ref float y, float w, int fs, string text)
	{
		float num = BtcSymmetry._note.CalcHeight(new GUIContent(text), w - (float)fs * 2f);
		GUI.Label(new Rect((float)fs, y + 2f, w - (float)fs * 2f, num), text, BtcSymmetry._note);
		y += num + (float)fs * 0.5f;
	}

	internal static bool Button(Event e, Rect r, string text, Color color, Color old)
	{
		bool flag = e.type == EventType.Repaint;
		if (flag)
		{
			GUI.color = (r.Contains(e.mousePosition) ? new Color(1f, 1f, 1f, 0.16f) : new Color(1f, 1f, 1f, 0.07f));
			GUI.DrawTexture(r, BtcSymmetry.White());
			GUI.color = old;
		}
		Color textColor = BtcSymmetry._btn.normal.textColor;
		BtcSymmetry._btn.normal.textColor = color;
		bool flag2 = GUI.Button(r, text, BtcSymmetry._btn);
		BtcSymmetry._btn.normal.textColor = textColor;
		return flag2 && !BtcHotkeys.UiClickSwallowed;
	}

	private static int Stepper(Event e, ref float y, float w, float rowH, int fs, Color old, string label, int v, int def, int min, int max, int step, bool on)
	{
		float num = y;
		y += rowH;
		GUI.Label(new Rect((float)fs, num, w * 0.5f, rowH), label, BtcSymmetry._label);
		float num2 = rowH * 0.9f;
		Rect r = new Rect(w - (float)fs - num2, num + rowH * 0.05f, num2, rowH * 0.9f);
		Rect position = new Rect(r.x - num2 * 1.8f, num + rowH * 0.05f, num2 * 1.8f, rowH * 0.9f);
		Rect r2 = new Rect(position.x - num2, num + rowH * 0.05f, num2, rowH * 0.9f);
		Color color = (v != def) ? BtcSymmetry.ChangedC : Color.white;
		GUI.Label(position, v.ToString(), BtcSymmetry._btn);
		bool flag = BtcSymmetry.Button(e, r2, "-", color, old) && on;
		if (flag)
		{
			v = Mathf.Max(min, v - step);
		}
		bool flag2 = BtcSymmetry.Button(e, r, "+", color, old) && on;
		if (flag2)
		{
			v = Mathf.Min(max, v + step);
		}
		return v;
	}

	private static void Dim(Rect r, Color old)
	{
		bool flag = BtcSymmetry._noDim || r.width <= 0f || r.height <= 0f;
		if (!flag)
		{
			GUI.color = new Color(0f, 0f, 0f, 0.62f);
			GUI.DrawTexture(r, BtcSymmetry.White());
			GUI.color = old;
		}
	}

	internal static void Line(Rect r, Color c, Color old)
	{
		GUI.color = c;
		GUI.DrawTexture(r, BtcSymmetry.White());
		GUI.color = old;
	}

	private static string KeepText(int m, int k)
	{
		bool flag = m == 1;
		string result;
		if (flag)
		{
			result = ((k == 0) ? BtcSymmetry.T("left half", "левая половина") : BtcSymmetry.T("right half", "правая половина"));
		}
		else
		{
			bool flag2 = m == 2;
			if (flag2)
			{
				result = ((k == 0) ? BtcSymmetry.T("bottom half", "нижняя половина") : BtcSymmetry.T("top half", "верхняя половина"));
			}
			else
			{
				bool flag3 = m == 3;
				if (flag3)
				{
					switch (k)
					{
					case 0:
						result = BtcSymmetry.T("left half", "левая половина");
						break;
					case 1:
						result = BtcSymmetry.T("right half", "правая половина");
						break;
					case 2:
						result = BtcSymmetry.T("bottom half", "нижняя половина");
						break;
					default:
						result = BtcSymmetry.T("top half", "верхняя половина");
						break;
					}
				}
				else
				{
					string str = ((k & 1) == 0) ? BtcSymmetry.T("left", "левый") : BtcSymmetry.T("right", "правый");
					string str2 = (k >> 1 == 0) ? BtcSymmetry.T("bottom", "нижний") : BtcSymmetry.T("top", "верхний");
					result = str2 + " " + str + BtcSymmetry.T(" quarter", " квадрант");
				}
			}
		}
		return result;
	}

	public static void Draw(Event e, ref float y, float w, float rowH, int fs, Color old)
	{
		BtcSymmetry.Styles(fs);
		BtcSymmetry.Tick();
		BtcSymmetry.LoadPrefsOnce();
		BtcSymmetry.EnsurePreview();
		bool flag = BtcGameplay.CanEdit && BtcBc.BcEnabled;
		bool flag2 = false;
		BtcSymmetry.Note(ref y, w, fs, BtcSymmetry.T("Map symmetry: the master half is copied onto the other half before the game starts (terrain, voxel types, trees, plants, rocks, iron, brimstone). Deep ore stays random per game (or by seed) but is identical on both halves. The original map file is never changed. Works only with BetterConnection, every player needs this mod version.", "Симметрия карты: выбранная половина копируется на другую перед стартом игры (рельеф, типы вокселей, деревья, растения, камни, железо, бримстоун). Руда под землёй остаётся случайной в каждой катке (или по сиду), но на обеих половинах одинаковая. Исходный файл карты не меняется. Работает только с BetterConnection, у всех игроков должна стоять эта версия мода."));
		bool flag3 = !BtcBc.BcEnabled;
		if (flag3)
		{
			BtcSymmetry.Note(ref y, w, fs, BtcSymmetry.T("BetterConnection is off: symmetry is not applied.", "BetterConnection выключен: симметрия не применяется."));
		}
		else
		{
			bool flag4 = !flag;
			if (flag4)
			{
				BtcSymmetry.Note(ref y, w, fs, BtcSymmetry.T("Only the host can change these settings in the lobby.", "Менять эти настройки в лобби может только хост."));
			}
		}
		string[] array = new string[]
		{
			BtcSymmetry.T("Off", "Выкл"),
			BtcSymmetry.T("Mirror |", "Зеркало |"),
			BtcSymmetry.T("Mirror -", "Зеркало -"),
			BtcSymmetry.T("Rotate 180", "Поворот 180"),
			BtcSymmetry.T("4-fold", "4-кратная")
		};
		float num = (w - (float)fs * 2f) / (float)array.Length;
		for (int i = 0; i < array.Length; i++)
		{
			Rect r = new Rect((float)fs + num * (float)i, y + rowH * 0.05f, num - 4f, rowH * 0.85f);
			bool flag5 = BtcSymmetry.Button(e, r, array[i], (i == BtcSymmetry.Mode) ? BtcSymmetry.Accent : Color.white, old) && flag && i != BtcSymmetry.Mode;
			if (flag5)
			{
				BtcSymmetry.Mode = i;
				bool flag6 = i < 3;
				if (flag6)
				{
					BtcSymmetry._keep &= 1;
				}
				flag2 = true;
			}
		}
		y += rowH;
		int mode = BtcSymmetry.Mode;
		bool flag7 = mode != 0;
		if (flag7)
		{
			bool flag8 = !BtcSymmetry._pvOk || BtcSymmetry._pvTex == null;
			if (flag8)
			{
				BtcSymmetry.Note(ref y, w, fs, BtcSymmetry.T("No map selected or the map has no heightmap: preview unavailable.", "Карта не выбрана или у неё нет heightmap: превью недоступно."));
			}
			else
			{
				int num2 = BtcSymmetry._defSx + BtcSymmetry._dx;
				int num3 = BtcSymmetry._defSz + BtcSymmetry._dz;
				float num4 = w - (float)fs * 2f;
				float num5 = rowH * 9f;
				float num6 = Mathf.Min(num4 / (float)BtcSymmetry._pvW, num5 / (float)BtcSymmetry._pvH);
				Rect position = new Rect((float)fs, y, (float)BtcSymmetry._pvW * num6, (float)BtcSymmetry._pvH * num6);
				float num7 = position.x + ((float)num2 * 0.5f - (float)BtcSymmetry._pvMinX + 0.5f) / (float)BtcSymmetry._pvW * position.width;
				float num8 = position.yMax - ((float)num3 * 0.5f - (float)BtcSymmetry._pvMinZ + 0.5f) / (float)BtcSymmetry._pvH * position.height;
				num7 = Mathf.Clamp(num7, position.x, position.xMax);
				num8 = Mathf.Clamp(num8, position.y, position.yMax);
				bool flag9 = e.type == EventType.MouseDown && e.button == 0 && flag && position.Contains(e.mousePosition) && !BtcHotkeys.UiClickSwallowed;
				if (flag9)
				{
					float x = e.mousePosition.x;
					float y2 = e.mousePosition.y;
					int num9 = BtcSymmetry._keep;
					bool flag10 = mode == 1;
					if (flag10)
					{
						num9 = ((x > num7) ? 1 : 0);
					}
					else
					{
						bool flag11 = mode == 2;
						if (flag11)
						{
							num9 = ((y2 < num8) ? 1 : 0);
						}
						else
						{
							bool flag12 = mode == 3;
							if (flag12)
							{
								float num10 = (x - num7) / (position.width * 0.5f);
								float num11 = (num8 - y2) / (position.height * 0.5f);
								bool flag13 = Mathf.Abs(num10) >= Mathf.Abs(num11);
								if (flag13)
								{
									num9 = ((num10 > 0f) ? 1 : 0);
								}
								else
								{
									num9 = 2 + ((num11 > 0f) ? 1 : 0);
								}
							}
							else
							{
								num9 = (((x > num7) ? 1 : 0) | ((y2 < num8) ? 2 : 0));
							}
						}
					}
					bool flag14 = num9 != BtcSymmetry._keep;
					if (flag14)
					{
						BtcSymmetry._keep = num9;
						flag2 = true;
					}
					e.Use();
				}
				bool flag15 = e.type == EventType.Repaint;
				if (flag15)
				{
					GUI.color = new Color(0.08f, 0.08f, 0.08f, 1f);
					GUI.DrawTexture(position, BtcSymmetry.White());
					GUI.color = old;
					bool altPv = e.alt && BtcSymmetry.EnsureSym(mode, num2, num3);
						GUI.DrawTexture(position, altPv ? BtcSymmetry._symTex : BtcSymmetry._pvTex, ScaleMode.StretchToFill, true);
						BtcSymmetry._noDim = altPv;
					int keep = BtcSymmetry._keep;
					bool flag16 = mode == 1 || (mode == 3 && keep < 2);
					if (flag16)
					{
						bool flag17 = (keep & 1) == 0;
						if (flag17)
						{
							BtcSymmetry.Dim(new Rect(num7, position.y, position.xMax - num7, position.height), old);
						}
						else
						{
							BtcSymmetry.Dim(new Rect(position.x, position.y, num7 - position.x, position.height), old);
						}
					}
					else
					{
						bool flag18 = mode == 2 || mode == 3;
						if (flag18)
						{
							bool flag19 = (keep & 1) == 0;
							if (flag19)
							{
								BtcSymmetry.Dim(new Rect(position.x, position.y, position.width, num8 - position.y), old);
							}
							else
							{
								BtcSymmetry.Dim(new Rect(position.x, num8, position.width, position.yMax - num8), old);
							}
						}
						else
						{
							bool flag20 = (keep & 1) == 1;
							bool flag21 = keep >> 1 == 1;
							bool flag22 = flag20;
							if (flag22)
							{
								BtcSymmetry.Dim(new Rect(position.x, position.y, num7 - position.x, position.height), old);
							}
							else
							{
								BtcSymmetry.Dim(new Rect(num7, position.y, position.xMax - num7, position.height), old);
							}
							float num12 = flag20 ? num7 : position.x;
							float num13 = flag20 ? position.xMax : num7;
							bool flag23 = flag21;
							if (flag23)
							{
								BtcSymmetry.Dim(new Rect(num12, num8, num13 - num12, position.yMax - num8), old);
							}
							else
							{
								BtcSymmetry.Dim(new Rect(num12, position.y, num13 - num12, num8 - position.y), old);
							}
						}
					}
					BtcSymmetry._noDim = false;
						List<float[]> pvMarks = altPv ? BtcSymmetry._symMarks : BtcSymmetry._marks;
						for (int j = 0; j < pvMarks.Count; j++)
					{
						float[] array2 = pvMarks[j];
						float num14 = position.x + (array2[0] - (float)BtcSymmetry._pvMinX + 0.5f) / (float)BtcSymmetry._pvW * position.width;
						float num15 = position.yMax - (array2[1] - (float)BtcSymmetry._pvMinZ + 0.5f) / (float)BtcSymmetry._pvH * position.height;
						Color c = (array2[2] == 1f) ? new Color(1f, 0.9f, 0.2f, 1f) : ((array2[2] == 2f) ? new Color(0.3f, 0.9f, 1f, 1f) : ((array2[2] == 3f) ? new Color(1f, 0.3f, 0.3f, 1f) : new Color(1f, 1f, 1f, 0.7f)));
						float num16 = (array2[2] == 1f) ? 6f : 4f;
						BtcSymmetry.Line(new Rect(num14 - num16 * 0.5f, num15 - num16 * 0.5f, num16, num16), c, old);
					}
					Color c2 = new Color(0.55f, 0.85f, 0.55f, 0.95f);
					bool flag24 = mode == 1 || mode == 3 || mode == 4;
					if (flag24)
					{
						BtcSymmetry.Line(new Rect(num7 - 1f, position.y, 2f, position.height), c2, old);
					}
					bool flag25 = mode == 2 || mode == 3 || mode == 4;
					if (flag25)
					{
						BtcSymmetry.Line(new Rect(position.x, num8 - 1f, position.width, 2f), c2, old);
					}
				}
				y += position.height + rowH * 0.2f;
				BtcSymmetry.Note(ref y, w, fs, BtcSymmetry.T("Click the preview to choose the master half. Bright = copied, dark = replaced. Yellow dots = start points, cyan = crystals, red = corruptron spawns.", "Кликните по превью, чтобы выбрать образец. Яркая часть копируется, тёмная будет заменена. Жёлтые точки: стартовые точки, голубые: кристаллы, красные: спавны коррупторов.") + " " + BtcSymmetry.T("Master: ", "Образец: ") + BtcSymmetry.KeepText(mode, BtcSymmetry._keep));
			}
			int dx = BtcSymmetry._dx;
			int dz = BtcSymmetry._dz;
			bool flag26 = mode == 1 || mode == 3 || mode == 4;
			if (flag26)
			{
				BtcSymmetry._dx = BtcSymmetry.Stepper(e, ref y, w, rowH, fs, old, BtcSymmetry.T("Axis shift X (half-voxels)", "Сдвиг оси X (полвокселя)"), BtcSymmetry._dx, 0, -400, 400, 1, flag);
			}
			bool flag27 = mode == 2 || mode == 3 || mode == 4;
			if (flag27)
			{
				BtcSymmetry._dz = BtcSymmetry.Stepper(e, ref y, w, rowH, fs, old, BtcSymmetry.T("Axis shift Z (half-voxels)", "Сдвиг оси Z (полвокселя)"), BtcSymmetry._dz, 0, -400, 400, 1, flag);
			}
			bool flag28 = dx != BtcSymmetry._dx || dz != BtcSymmetry._dz;
			if (flag28)
			{
				flag2 = true;
			}
			bool flag29 = BtcSymmetry._dx != 0 || BtcSymmetry._dz != 0;
			if (flag29)
			{
				Rect r2 = new Rect((float)fs, y + rowH * 0.05f, w * 0.4f, rowH * 0.9f);
				bool flag30 = BtcSymmetry.Button(e, r2, BtcSymmetry.T("Reset axis to centre", "Ось в центр"), BtcSymmetry.Warn, old) && flag;
				if (flag30)
				{
					BtcSymmetry._dx = 0;
					BtcSymmetry._dz = 0;
					flag2 = true;
				}
				y += rowH;
			}
			Rect r3 = new Rect((float)fs, y + rowH * 0.05f, w - (float)fs * 2f, rowH * 0.9f);
			string text = (BtcSymmetry._obj ? "[x] " : "[ ] ") + BtcSymmetry.T("Also mirror editor markers (start points, crystals), placed blocks", "Зеркалить также маркеры редактора (старты, кристаллы) и поставленные блоки");
			bool flag31 = BtcSymmetry.Button(e, r3, text, BtcSymmetry._obj ? BtcSymmetry.Accent : Color.white, old) && flag;
			if (flag31)
			{
				BtcSymmetry._obj = !BtcSymmetry._obj;
				flag2 = true;
			}
			y += rowH;
			bool obj = BtcSymmetry._obj;
			if (obj)
			{
				BtcSymmetry.Note(ref y, w, fs, BtcSymmetry.T("Markers on the replaced half are removed and recreated as mirror copies.", "Маркеры на заменяемой половине удаляются и создаются заново зеркальными копиями."));
			}
			string[] array3 = new string[]
			{
				BtcSymmetry.T("Loose resources", "Россыпные ресурсы"),
				BtcSymmetry.T("Buildings", "Здания"),
				BtcSymmetry.T("Units", "Юниты")
			};
			float num17 = (w - (float)fs * 2f) / 3f;
			for (int k = 0; k < 3; k++)
			{
				Rect r4 = new Rect((float)fs + num17 * (float)k, y + rowH * 0.05f, num17 - 4f, rowH * 0.9f);
				bool flag32 = (BtcSymmetry._go & 1 << k) != 0;
				bool flag33 = BtcSymmetry.Button(e, r4, (flag32 ? "[x] " : "[ ] ") + array3[k], flag32 ? BtcSymmetry.Accent : Color.white, old) && flag;
				if (flag33)
				{
					BtcSymmetry._go ^= 1 << k;
					flag2 = true;
				}
			}
			y += rowH;
			bool flag34 = BtcSymmetry._go != 0;
			if (flag34)
			{
				BtcSymmetry.Note(ref y, w, fs, BtcSymmetry.T("Selected objects on the replaced half are removed; objects on the master half are copied together with their contents. Cloned units and buildings keep their original faction.", "Выбранные объекты на заменяемой половине удаляются; объекты образца копируются вместе с содержимым. У клонов юнитов и зданий фракция остаётся прежней."));
			}
			GUI.Label(new Rect((float)fs, y, w * 0.55f, rowH), BtcSymmetry.T("Ore seed (0 = random)", "Сид руды (0 = случайный)"), BtcSymmetry._label);
			Rect position2 = new Rect(w - (float)fs - rowH * 3.4f, y + rowH * 0.05f, rowH * 3.4f, rowH * 0.9f);
			string text2 = GUI.TextField(position2, BtcSymmetry._seedStr, 6, BtcSymmetry._field);
			bool flag35 = text2 != BtcSymmetry._seedStr;
			if (flag35)
			{
				StringBuilder stringBuilder = new StringBuilder();
				for (int l = 0; l < text2.Length; l++)
				{
					bool flag36 = text2[l] >= '0' && text2[l] <= '9';
					if (flag36)
					{
						stringBuilder.Append(text2[l]);
					}
				}
				string text3 = stringBuilder.ToString();
				bool flag37 = flag;
				if (flag37)
				{
					BtcSymmetry._seedStr = text3;
					int num18;
					bool flag38 = !int.TryParse(text3, out num18);
					if (flag38)
					{
						num18 = 0;
					}
					bool flag39 = num18 != BtcSymmetry._seed;
					if (flag39)
					{
						BtcSymmetry._seed = num18;
						flag2 = true;
					}
				}
			}
			y += rowH;
			bool flag40 = BtcSymmetry._seed > 0 && BtcSymmetry._pvOk && !BtcSymmetry._hasNoise;
			if (flag40)
			{
				BtcSymmetry.Note(ref y, w, fs, BtcSymmetry.T("This map has no noise.json: the seed cannot be forced, ore stays random.", "У этой карты нет noise.json: сид задать нельзя, руда останется случайной."));
			}
			if (BtcSymmetry._pvOk)
			{
				BtcSymmetry.Note(ref y, w, fs, BtcSymmetry.T("Hold Alt: preview the map after symmetry", "Удерживайте Alt: превью карты после симметрии"));
			}
		}
		bool flag41 = flag2;
		if (flag41)
		{
			BtcSymmetry.Changed();
		}
		y += rowH * 0.2f;
		BtcSpawnPickUI.Draw(e, ref y, w, rowH, fs, old);
	}

	// Spawn selection UI (UI/BtcSpawnPickUI.cs) reuses the lobby map preview. False when no preview is available.
	internal static bool SpawnPreview(out Texture2D tex, out int minX, out int minZ, out int pw, out int ph, out List<float[]> marks)
	{
		BtcSymmetry.EnsurePreview();
		tex = BtcSymmetry._pvTex;
		minX = BtcSymmetry._pvMinX;
		minZ = BtcSymmetry._pvMinZ;
		pw = BtcSymmetry._pvW;
		ph = BtcSymmetry._pvH;
		marks = BtcSymmetry._marks;
		return BtcSymmetry._pvOk && tex != null && pw > 0 && ph > 0;
	}

	public static int Mode;

	private static int _keep;

	private static int _dx;

	private static int _dz;

	private static bool _obj;

	private static int _go;

	private static int _seed;

	private static string _seedStr = "0";

	private static volatile int _am;

	private static volatile int _ak;

	private static volatile int _asx;

	private static volatile int _asz;

	private static string _pvKey;

	private static Color32[] _pvPx;

	private static int _pvGen;

	private static Texture2D _symTex;

	private static readonly List<float[]> _symMarks = new List<float[]>();

	private static bool _noDim;

	private static int _symGen = -1;

	private static int _symMode;

	private static int _symKeep;

	private static int _symSx;

	private static int _symSz;

	private static bool _symObj;


	private static bool _pvOk;

	private static Texture2D _pvTex;

	private static int _pvMinX;

	private static int _pvMinZ;

	private static int _pvW;

	private static int _pvH;

	private static int _defSx;

	private static int _defSz;

	private static bool _hasNoise;

	private static readonly List<float[]> _marks = new List<float[]>();

	private static float _pvNext;

	private static Asset_Map _swapped;

	private static string _origPath;

	private static bool _prefsLoaded;

	private const short MsgState = 89;

	internal static GUIStyle _label;

	private static GUIStyle _note;

	private static GUIStyle _sub;

	private static GUIStyle _btn;

	private static GUIStyle _field;

	private static int _styleFs;

	private static Texture2D _white;

	internal static readonly Color Accent = new Color(0.55f, 0.85f, 0.55f, 1f);

	private static readonly Color Warn = new Color(1f, 0.45f, 0.45f, 1f);

	private static readonly Color ChangedC = new Color(1f, 0.85f, 0.4f, 1f);

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

	private delegate float[] PosFn(JToken t);

	private delegate void MvFn(JToken t, bool fx, bool fz);
}
}
