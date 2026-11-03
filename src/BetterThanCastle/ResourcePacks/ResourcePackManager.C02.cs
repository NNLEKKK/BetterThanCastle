using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Brix.Engine;
using Brix.Engine.Nature;
using Brix.External.Factories;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.AI.Damage;
using Brix.Game.Components;
using Brix.Input;
using Brix.Input.ContextualOrders;
using Brix.Legacy;
using Brix.Lifecycle.Pooling;
using Brix.Lua;
using Brix.Network;
using Brix.Pathfinding.Test;
using Brix.Utils;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.PostProcessing;
using UnityEngine.SceneManagement;

namespace BetterThanCastle
{
public partial class ResourcePackManager : MonoBehaviour
{

	private static bool TryParseSigName(string baseName, out string name, out string sig)
	{
		name = baseName;
		sig = null;
		int num = baseName.LastIndexOf("__", StringComparison.Ordinal);
		bool flag = num <= 0;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		bool result;
		if (flag8)
		{
			result = false;
		}
		else
		{
			string text = baseName.Substring(num + 2);
			int num2;
			int num3;
			int[] array;
			bool flag9 = !ResourcePackManager.ParseSig(text, out num2, out num3, out array);
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				result = false;
			}
			else
			{
				name = baseName.Substring(0, num);
				sig = text.ToLowerInvariant();
				result = true;
			}
		}
		return result;
	}

	private static string FindVariantFile(ResourcePackManager.IndexItem item, string sig)
	{
		bool flag = item.bySig == null || sig == null;
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
			result = null;
		}
		else
		{
			string text;
			bool flag9 = item.bySig.TryGetValue(sig, out text);
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				result = text;
			}
			else
			{
				int num;
				int num2;
				int[] array;
				bool flag17 = !ResourcePackManager.ParseSig(sig, out num, out num2, out array);
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				if (flag24)
				{
					result = null;
				}
				else
				{
					string text2 = null;
					int num3 = 4;
					foreach (KeyValuePair<string, string> keyValuePair in item.bySig)
					{
						int num4;
						int num5;
						int[] array2;
						bool flag25 = !ResourcePackManager.ParseSig(keyValuePair.Key, out num4, out num5, out array2) || num4 != num || num5 != num2;
						bool flag26 = !flag25;
						bool flag27 = flag26;
						bool flag28 = flag27;
						bool flag29 = flag28;
						bool flag30 = flag29;
						bool flag31 = flag30;
						bool flag32 = flag31;
						if (flag32)
						{
							int num6 = 0;
							for (int i = 0; i < 12; i++)
							{
								num6 += Math.Abs(array[i] - array2[i]);
							}
							bool flag33 = num6 < num3;
							bool flag34 = flag33;
							bool flag35 = flag34;
							bool flag36 = flag35;
							bool flag37 = flag36;
							bool flag38 = flag37;
							bool flag39 = flag38;
							bool flag40 = flag39;
							if (flag40)
							{
								num3 = num6;
								text2 = keyValuePair.Value;
							}
						}
					}
					result = text2;
				}
			}
		}
		return result;
	}

	private static List<string> IndexFiles(Dictionary<string, ResourcePackManager.IndexItem> map)
	{
		List<string> list = new List<string>();
		foreach (KeyValuePair<string, ResourcePackManager.IndexItem> keyValuePair in map)
		{
			bool flag = keyValuePair.Value.plain != null;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				list.Add(keyValuePair.Value.plain);
			}
			bool flag9 = keyValuePair.Value.bySig != null;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				foreach (KeyValuePair<string, string> keyValuePair2 in keyValuePair.Value.bySig)
				{
					list.Add(keyValuePair2.Value);
				}
			}
		}
		return list;
	}

	private static Dictionary<string, ResourcePackManager.IndexItem> BuildIndex(string packDir)
	{
		Dictionary<string, ResourcePackManager.IndexItem> dictionary = new Dictionary<string, ResourcePackManager.IndexItem>(StringComparer.OrdinalIgnoreCase);
		try
		{
			ResourcePackManager.PackLog(string.Concat(new string[]
			{
				"[ResourcePacks] Reading pack folder: ",
				packDir,
				" (exists: ",
				Directory.Exists(packDir).ToString(),
				")"
			}));
			bool flag = Directory.Exists(packDir);
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				string[] files = Directory.GetFiles(packDir, "*.png", SearchOption.AllDirectories);
				for (int i = 0; i < files.Length; i++)
				{
					bool flag9 = !files[i].EndsWith(".png", StringComparison.OrdinalIgnoreCase);
					bool flag10 = !flag9;
					bool flag11 = flag10;
					bool flag12 = flag11;
					bool flag13 = flag12;
					bool flag14 = flag13;
					bool flag15 = flag14;
					bool flag16 = flag15;
					if (flag16)
					{
						bool flag17 = ResourcePackManager.IsInEnemyFolder(packDir, files[i]);
						bool flag18 = !flag17;
						bool flag19 = flag18;
						bool flag20 = flag19;
						bool flag21 = flag20;
						bool flag22 = flag21;
						bool flag23 = flag22;
						bool flag24 = flag23;
						if (flag24)
						{
							string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(files[i]);
							string key;
							string key2;
							bool flag25 = ResourcePackManager.TryParseSigName(fileNameWithoutExtension, out key, out key2);
							ResourcePackManager.IndexItem indexItem = null;
							bool flag26 = !dictionary.TryGetValue(key, out indexItem);
							bool flag27 = flag26;
							bool flag28 = flag27;
							bool flag29 = flag28;
							bool flag30 = flag29;
							bool flag31 = flag30;
							bool flag32 = flag31;
							bool flag33 = flag32;
							if (flag33)
							{
								indexItem = new ResourcePackManager.IndexItem();
								dictionary[key] = indexItem;
							}
							bool flag34 = flag25;
							bool flag35 = flag34;
							bool flag36 = flag35;
							bool flag37 = flag36;
							bool flag38 = flag37;
							bool flag39 = flag38;
							bool flag40 = flag39;
							bool flag41 = flag40;
							if (flag41)
							{
								bool flag42 = indexItem.bySig == null;
								bool flag43 = flag42;
								bool flag44 = flag43;
								bool flag45 = flag44;
								bool flag46 = flag45;
								bool flag47 = flag46;
								bool flag48 = flag47;
								bool flag49 = flag48;
								if (flag49)
								{
									indexItem.bySig = new Dictionary<string, string>();
								}
								indexItem.bySig[key2] = files[i];
							}
							else
							{
								indexItem.plain = files[i];
							}
						}
					}
				}
				bool flag50 = dictionary.Count == 0;
				bool flag51 = flag50;
				bool flag52 = flag51;
				bool flag53 = flag52;
				bool flag54 = flag53;
				bool flag55 = flag54;
				bool flag56 = flag55;
				bool flag57 = flag56;
				if (flag57)
				{
					string[] fileSystemEntries = Directory.GetFileSystemEntries(packDir);
					ResourcePackManager.PackWarn(string.Concat(new object[]
					{
						"[ResourcePacks] No .png files in pack. Folder contains ",
						fileSystemEntries.Length,
						" entries: ",
						string.Join(" | ", fileSystemEntries)
					}));
				}
			}
		}
		catch (Exception ex)
		{
			ResourcePackManager.PackWarn("[ResourcePacks] Cannot read pack '" + packDir + "': " + ex.Message);
		}
		return dictionary;
	}

	private static void Revert(ResourcePackManager.Entry e)
	{
		bool flag = e.applied && e.tex != null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			ResourcePackManager.LoadInto(e.tex, e.original);
			bool flag9 = string.Equals(e.tex.name, "Curseurs", StringComparison.OrdinalIgnoreCase);
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				ResourcePackManager.cursorsDirty = true;
			}
		}
		e.applied = false;
		e.file = null;
	}

	private static Texture2D ReadBack(Texture2D tex)
	{
		RenderTexture temporary = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
		RenderTexture active = RenderTexture.active;
		Texture2D texture2D = null;
		Texture2D result;
		try
		{
			Graphics.Blit(tex, temporary);
			RenderTexture.active = temporary;
			texture2D = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
			texture2D.ReadPixels(new Rect(0f, 0f, (float)tex.width, (float)tex.height), 0, 0);
			texture2D.Apply();
			result = texture2D;
		}
		catch (Exception)
		{
			bool flag = texture2D != null;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				UnityEngine.Object.Destroy(texture2D);
			}
			throw;
		}
		finally
		{
			RenderTexture.active = active;
			RenderTexture.ReleaseTemporary(temporary);
		}
		return result;
	}

	private static byte[] Snapshot(Texture2D tex, out string sig)
	{
		Texture2D texture2D = ResourcePackManager.ReadBack(tex);
		byte[] result;
		try
		{
			sig = ResourcePackManager.Signature(texture2D);
			result = texture2D.EncodeToPNG();
		}
		finally
		{
			UnityEngine.Object.Destroy(texture2D);
		}
		return result;
	}

	private static string Signature(Texture2D tex)
	{
		int width = tex.width;
		int height = tex.height;
		Color32[] pixels = tex.GetPixels32();
		long[] array = new long[12];
		int[] array2 = new int[4];
		int num = Math.Max(1, Math.Max(width, height) / 256);
		for (int i = 0; i < height; i += num)
		{
			int num2 = i * 2 / height;
			for (int j = 0; j < width; j += num)
			{
				int num3 = num2 * 2 + j * 2 / width;
				Color32 color = pixels[i * width + j];
				array[num3 * 3] += (long)((ulong)color.r);
				array[num3 * 3 + 1] += (long)((ulong)color.g);
				array[num3 * 3 + 2] += (long)((ulong)color.b);
				array2[num3]++;
			}
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append(width).Append('x').Append(height).Append('-');
		for (int k = 0; k < 4; k++)
		{
			for (int l = 0; l < 3; l++)
			{
				int num4 = (array2[k] > 0) ? ((int)(array[k * 3 + l] / (long)array2[k]) >> 4) : 0;
				stringBuilder.Append("0123456789abcdef"[num4]);
			}
		}
		return stringBuilder.ToString();
	}

	private string GetSignature(Texture2D tex, int id, ResourcePackManager.Entry e)
	{
		bool flag = e != null && e.sig != null;
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
			result = e.sig;
		}
		else
		{
			string text;
			bool flag9 = this.sigCache.TryGetValue(id, out text);
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				result = text;
			}
			else
			{
				try
				{
					Texture2D texture2D = ResourcePackManager.ReadBack(tex);
					try
					{
						text = ResourcePackManager.Signature(texture2D);
					}
					finally
					{
						UnityEngine.Object.Destroy(texture2D);
					}
				}
				catch (Exception ex)
				{
					ResourcePackManager.PackWarn("[ResourcePacks] Cannot read texture '" + tex.name + "' for signature: " + ex.Message);
					return null;
				}
				this.sigCache[id] = text;
				result = text;
			}
		}
		return result;
	}

	private static string SafeFileName(string name)
	{
		string text = name;
		char[] invalidFileNameChars = System.IO.Path.GetInvalidFileNameChars();
		for (int i = 0; i < invalidFileNameChars.Length; i++)
		{
			text = text.Replace(invalidFileNameChars[i], '_');
		}
		return text;
	}

	private static List<ResourcePackManager.ManifestEntry> LoadManifest(string path)
	{
		List<ResourcePackManager.ManifestEntry> list = new List<ResourcePackManager.ManifestEntry>();
		try
		{
			bool flag = File.Exists(path);
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				string[] array = File.ReadAllLines(path);
				for (int i = 0; i < array.Length; i++)
				{
					string[] array2 = array[i].Split(new char[]
					{
						'\t'
					});
					bool flag9 = array2.Length == 3;
					bool flag10 = flag9;
					bool flag11 = flag10;
					bool flag12 = flag11;
					bool flag13 = flag12;
					bool flag14 = flag13;
					bool flag15 = flag14;
					bool flag16 = flag15;
					if (flag16)
					{
						list.Add(new ResourcePackManager.ManifestEntry
						{
							name = array2[0],
							sig = array2[1],
							file = array2[2]
						});
					}
				}
			}
		}
		catch (Exception)
		{
		}
		return list;
	}

	private static void SaveManifest(string path, List<ResourcePackManager.ManifestEntry> list)
	{
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < list.Count; i++)
		{
			stringBuilder.AppendLine(string.Concat(new string[]
			{
				list[i].name,
				"\t",
				list[i].sig,
				"\t",
				list[i].file
			}));
		}
		File.WriteAllText(path, stringBuilder.ToString());
	}

	private static ResourcePackManager.ManifestEntry FindEntry(List<ResourcePackManager.ManifestEntry> list, string sig)
	{
		for (int i = 0; i < list.Count; i++)
		{
			bool flag = list[i].sig == sig;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				return list[i];
			}
		}
		return null;
	}

	private static void MoveDumpFile(string dir, string from, string to)
	{
		string text = System.IO.Path.Combine(dir, from + ".png");
		string text2 = System.IO.Path.Combine(dir, to + ".png");
		bool flag = !File.Exists(text);
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			bool flag9 = File.Exists(text2);
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				File.Delete(text2);
			}
			File.Move(text, text2);
		}
	}

	private int[] tabIds = new int[0];

	private Rect[] tabLay = new Rect[0];

	private string[] tabNames = new string[0];

	// Lays the tabs out in as many rows as needed so that they never overlap. tabLay[i]: x, row index (in y), width.
	private int LayoutTabs(float availW, float pad)
	{
		this.tabNames = new string[]
		{
			BtcLang.Tr("Resource Packs"),
			BtcLang.Tr("Overlays"),
			"BetterConnection",
			BtcLang.Tr("Advanced Hotkeys"),
			BtcGameplay.T("Gameplay Settings", "Настройки геймплея"),
			"Share",
			BtcLang.Tr("Profile"),
			BtcReplayUI.T("Replay", "Реплеи"),
			BtcGameplay.T("Duel", "Дуэль")
		};
		int num = BtcReplay.FeatureOn ? this.tabNames.Length : (this.tabNames.Length - 1);
		this.tabIds = new int[num];
		this.tabLay = new Rect[num];
		float[] array = new float[num];
		int tabN = 0;
		for (int i = 0; i < this.tabNames.Length; i++)
		{
			if (i == 7 && !BtcReplay.FeatureOn) continue;
			this.tabIds[tabN] = i;
			array[tabN] = this.menuTabActiveStyle.CalcSize(new GUIContent(this.tabNames[i])).x + pad * 2f;
			tabN++;
		}
		int num2 = 0;
		int num3 = 0;
		float num4 = 0f;
		for (int j = 0; j <= num; j++)
		{
			bool flag = j == num;
			if (!flag && (num4 + array[j] <= availW || j == num3))
			{
				num4 += array[j];
				continue;
			}
			float num5 = Mathf.Min(availW / Mathf.Max(1f, num4), (num2 > 0 || j < num) ? 1.5f : 1.5f);
			float num6 = 0f;
			for (int k = num3; k < j; k++)
			{
				float num7 = array[k] * num5;
				this.tabLay[k] = new Rect(num6, (float)num2, num7, 0f);
				num6 += num7;
			}
			if (flag)
			{
				break;
			}
			num2++;
			num3 = j;
			num4 = array[j];
		}
		return num2 + 1;
	}

	private void ToggleMenu()
	{
		this.menuOpen = !this.menuOpen;
		bool flag = this.menuOpen;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		bool flag9 = flag8;
		if (flag9)
		{
			this.menuPacks = ResourcePackManager.ListPacks(true);
			this.menuScroll = Vector2.zero;
			this.menuHp = false;
			this.menuBc = false;
			this.menuDbg = false;
			BtcHotkeysUI.TabActive = false;
			BtcReplayUI.TabActive = false;
			BtcShareUI.TabActive = false;
			BtcNick.TabActive = false;
			BtcGameplayUI.TabActive = false;
			BtcDuelUI.TabActive = false;
			BtcReplayUI.OnMenuOpen();
			BtcShareUI.OnMenuOpen();
			BtcNick.OnMenuOpen();
			this.pickStop = null;
			this.pickOv = null;
			this.pickNum = false;
			this.pickExtra = 0;
		}
	}

	private void EnsureMenuStyles(int fontSize)
	{
		bool flag = this.whiteTex == null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			this.whiteTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
			this.whiteTex.SetPixel(0, 0, Color.white);
			this.whiteTex.Apply();
			this.whiteTex.hideFlags = HideFlags.HideAndDontSave;
		}
		bool flag9 = this.menuItemStyle != null && this.menuStyleHeight == Screen.height;
		bool flag10 = !flag9;
		bool flag11 = flag10;
		bool flag12 = flag11;
		bool flag13 = flag12;
		bool flag14 = flag13;
		bool flag15 = flag14;
		bool flag16 = flag15;
		if (flag16)
		{
			this.menuStyleHeight = Screen.height;
			this.menuTitleStyle = new GUIStyle(GUI.skin.label);
			this.menuTitleStyle.alignment = TextAnchor.MiddleCenter;
			this.menuTitleStyle.fontSize = fontSize;
			this.menuTitleStyle.normal.textColor = new Color(1f, 1f, 1f, 0.45f);
			this.menuBannerStyle = new GUIStyle(GUI.skin.label);
			this.menuBannerStyle.alignment = TextAnchor.MiddleCenter;
			this.menuBannerStyle.fontSize = Mathf.RoundToInt((float)fontSize * 1.7f);
			this.menuBannerStyle.fontStyle = FontStyle.Bold;
			this.menuBannerStyle.normal.textColor = new Color(0.55f, 0.85f, 0.55f, 1f);
			this.menuBannerSubStyle = new GUIStyle(GUI.skin.label);
			this.menuBannerSubStyle.alignment = TextAnchor.UpperCenter;
			this.menuBannerSubStyle.fontSize = Mathf.Max(10, Mathf.RoundToInt((float)fontSize * 0.75f));
			this.menuBannerSubStyle.normal.textColor = new Color(1f, 1f, 1f, 0.45f);
			this.menuItemStyle = new GUIStyle(GUI.skin.button);
			this.menuItemStyle.alignment = TextAnchor.MiddleLeft;
			this.menuItemStyle.fontSize = fontSize;
			this.menuItemStyle.padding = new RectOffset(fontSize, fontSize, 0, 0);
			this.menuItemStyle.border = new RectOffset(0, 0, 0, 0);
			this.menuItemStyle.margin = new RectOffset(0, 0, 0, 0);
			ResourcePackManager.SetStyleColors(this.menuItemStyle, new Color(1f, 1f, 1f, 0.85f), Color.white);
			this.menuActiveStyle = new GUIStyle(this.menuItemStyle);
			this.menuActiveStyle.fontStyle = FontStyle.Bold;
			Color color = new Color(0.55f, 0.85f, 0.55f, 1f);
			ResourcePackManager.SetStyleColors(this.menuActiveStyle, color, color);
			this.menuTabStyle = new GUIStyle(this.menuItemStyle);
			this.menuTabStyle.alignment = TextAnchor.MiddleCenter;
			this.menuTabStyle.padding = new RectOffset(0, 0, 0, 0);
			this.menuTabActiveStyle = new GUIStyle(this.menuActiveStyle);
			this.menuTabActiveStyle.alignment = TextAnchor.MiddleCenter;
			this.menuTabActiveStyle.padding = new RectOffset(0, 0, 0, 0);
			this.menuHatItemStyle = new GUIStyle(this.menuItemStyle);
			this.menuHatItemStyle.padding = new RectOffset(Mathf.RoundToInt((float)fontSize * 2.8f), fontSize, 0, 0);
			this.menuLinkLeftStyle = new GUIStyle(this.menuItemStyle);
			this.menuLinkLeftStyle.padding = new RectOffset(0, 0, 0, 0);
			this.menuLinkLeftStyle.alignment = TextAnchor.MiddleLeft;
			ResourcePackManager.SetStyleColors(this.menuLinkLeftStyle, new Color(1f, 1f, 1f, 0.55f), Color.white);
			this.menuLinkRightStyle = new GUIStyle(this.menuLinkLeftStyle);
			this.menuLinkRightStyle.alignment = TextAnchor.MiddleRight;
			this.menuLabelStyle = new GUIStyle(GUI.skin.label);
			this.menuLabelStyle.alignment = TextAnchor.MiddleLeft;
			this.menuLabelStyle.fontSize = fontSize;
			this.menuLabelStyle.padding = new RectOffset(0, 0, 0, 0);
			this.menuLabelStyle.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
			this.menuValueStyle = new GUIStyle(this.menuLabelStyle);
			this.menuValueStyle.alignment = TextAnchor.MiddleRight;
			this.menuValueStyle.normal.textColor = new Color(1f, 1f, 1f, 0.7f);
			this.menuHeaderStyle = new GUIStyle(this.menuLabelStyle);
			this.menuHeaderStyle.fontStyle = FontStyle.Bold;
			this.menuHeaderStyle.normal.textColor = new Color(0.55f, 0.85f, 0.55f, 0.95f);
			this.menuNoteStyle = new GUIStyle(this.menuLabelStyle);
			this.menuNoteStyle.fontSize = Mathf.Max(10, Mathf.RoundToInt((float)fontSize * 0.85f));
			this.menuNoteStyle.wordWrap = true;
			this.menuNoteStyle.alignment = TextAnchor.UpperLeft;
			this.menuNoteStyle.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
		}
	}

	private void DrawMenu()
	{
		int num = Mathf.Max(14, Screen.height / 38);
		this.EnsureMenuStyles(num);
		float num2 = (float)num * 2f;
		float num3 = num2 * 1.1f;
		float num4 = (float)num * 0.6f;
		bool flag = this.menuHp || this.menuBc || BtcHotkeysUI.TabActive || BtcGameplayUI.TabActive || BtcReplayUI.TabActive || BtcShareUI.TabActive || BtcNick.TabActive || BtcDuelUI.TabActive || this.hatsOpen;
		float num5 = Mathf.Min((float)Screen.width * 0.92f, (float)num * (flag ? 58f : 52f));
		float tabRowH = num3;
		int tabRows = this.LayoutTabs(num5 - num4 * 2f, (float)num * 0.6f);
		num3 = tabRowH * (float)tabRows;
		int num6 = this.menuPacks.Count + 1 + 1 + (this.hatsOpen ? (ResourcePackManager.HatOrder.Count + 1) : 0);
		float num7 = BtcDuelUI.TabActive ? BtcDuelUI.ContentH : BtcNick.TabActive ? BtcNick.ContentH : (BtcShareUI.TabActive ? BtcShareUI.ContentH : (BtcGameplayUI.TabActive ? BtcGameplayUI.ContentH : (BtcReplayUI.TabActive ? BtcReplayUI.ContentH : (BtcHotkeysUI.TabActive ? BtcHotkeysUI.ContentH : (this.menuHp ? this.hpContentH : (this.menuBc ? this.bcContentH : ((float)num6 * num2 + this.dbgContentH + num2)))))));
		float num8 = this.menuHp ? (((this.hpCfg != null) ? num2 : 0f) + num2 + num2) : 0f;
		float num9 = num2 * 1.75f;
		float num10 = flag ? ((float)Screen.height * 0.85f) : Mathf.Min(num9 + num3 + num8 + num7 + num4 * 2f, (float)Screen.height * 0.7f);
		Rect rect = new Rect(((float)Screen.width - num5) * 0.5f, ((float)Screen.height - num10) * 0.5f, num5, num10);
		Event current = Event.current;
		bool flag2 = current.rawType == EventType.MouseUp;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		bool flag9 = flag8;
		bool flag10 = flag9;
		bool flag11 = flag10;
		if (flag11)
		{
			this.hpDragId = 0;
		}
		bool flag12 = current.type == EventType.MouseDown && !rect.Contains(current.mousePosition);
		bool flag13 = flag12;
		bool flag14 = flag13;
		bool flag15 = flag14;
		bool flag16 = flag15;
		bool flag17 = flag16;
		bool flag18 = flag17;
		bool flag19 = flag18;
		bool flag20 = flag19;
		bool flag21 = flag20;
		if (flag21)
		{
			this.menuOpen = false;
			current.Use();
		}
		else
		{
			bool flag22 = current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape && !BtcHotkeys.EscBlocked;
			bool flag23 = flag22;
			bool flag24 = flag23;
			bool flag25 = flag24;
			bool flag26 = flag25;
			bool flag27 = flag26;
			bool flag28 = flag27;
			bool flag29 = flag28;
			bool flag30 = flag29;
			bool flag31 = flag30;
			if (flag31)
			{
				this.menuOpen = false;
				current.Use();
			}
			else
			{
				GUI.depth = -1001;
				Color color = GUI.color;
				bool flag32 = current.type == EventType.Repaint;
				bool flag33 = flag32;
				bool flag34 = flag33;
				bool flag35 = flag34;
				bool flag36 = flag35;
				bool flag37 = flag36;
				bool flag38 = flag37;
				bool flag39 = flag38;
				bool flag40 = flag39;
				bool flag41 = flag40;
				if (flag41)
				{
					GUI.color = new Color(0.06f, 0.06f, 0.08f, 0.93f);
					GUI.DrawTexture(rect, this.whiteTex);
				}
				GUI.color = color;
				GUI.Label(new Rect(rect.x, rect.y + num4 * 0.4f, rect.width, num2 * 1.15f), BtcLang.Tr("Better Than Castle"), this.menuBannerStyle);
				GUI.Label(new Rect(rect.x, rect.y + num4 * 0.4f + num2 * 1.1f, rect.width, num2 * 0.6f), BtcLang.Tr("by NotNonlokill"), this.menuBannerSubStyle);
				this.DrawLangButton(rect, num2, num4);
				bool flag42 = current.type == EventType.Repaint;
				bool flag43 = flag42;
				bool flag44 = flag43;
				bool flag45 = flag44;
				bool flag46 = flag45;
				bool flag47 = flag46;
				bool flag48 = flag47;
				bool flag49 = flag48;
				bool flag50 = flag49;
				bool flag51 = flag50;
				if (flag51)
				{
					GUI.color = new Color(1f, 1f, 1f, 0.12f);
					GUI.DrawTexture(new Rect(rect.x + num4, rect.y + num9 - num4 * 0.15f, rect.width - num4 * 2f, 1f), this.whiteTex);
					GUI.color = color;
				}
				Rect rect2 = new Rect(rect.x + num4, rect.y + num9 + num4 * 0.5f, rect.width - num4 * 2f, num3);
				int num11 = BtcDuelUI.TabActive ? 8 : BtcReplayUI.TabActive ? 7 : (BtcNick.TabActive ? 6 : (BtcShareUI.TabActive ? 5 : (BtcGameplayUI.TabActive ? 4 : (BtcHotkeysUI.TabActive ? 3 : (this.menuBc ? 2 : (this.menuHp ? 1 : 0))))));
				for (int i = 0; i < this.tabIds.Length; i++)
				{
					int id = this.tabIds[i];
					Rect position = new Rect(rect2.x + this.tabLay[i].x, rect2.y + this.tabLay[i].y * tabRowH, this.tabLay[i].width, tabRowH);
					if (current.type == EventType.Repaint)
					{
						GUI.color = ((id == num11) ? new Color(0.55f, 0.85f, 0.55f, 0.18f) : new Color(1f, 1f, 1f, 0.05f));
						GUI.DrawTexture(new Rect(position.x + 1f, position.y, position.width - 2f, position.height - 1f), this.whiteTex);
						GUI.color = color;
					}
					bool enabled = GUI.enabled;
					if (id == 4 && !BtcBc.BcEnabled)
					{
						GUI.enabled = false;
					}
					bool clicked = GUI.Button(position, this.tabNames[id], (id == num11) ? this.menuTabActiveStyle : this.menuTabStyle) && id != num11;
					GUI.enabled = enabled;
					if (clicked)
					{
						this.menuHp = (id == 1);
						this.menuBc = (id == 2);
						BtcHotkeysUI.TabActive = (id == 3);
						BtcGameplayUI.TabActive = (id == 4);
						BtcShareUI.TabActive = (id == 5);
						BtcNick.TabActive = (id == 6);
						BtcReplayUI.TabActive = (id == 7);
							BtcDuelUI.TabActive = (id == 8);
						if (id == 6)
						{
							BtcNick.OnMenuOpen();
						}
						BtcHotkeys.CancelCapture();
						this.menuScroll = Vector2.zero;
						this.pickStop = null;
						this.pickOv = null;
						this.pickNum = false;
						this.pickExtra = 0;
						return;
					}
				}
				bool flag78 = num8 > 0f;
				bool flag79 = flag78;
				bool flag80 = flag79;
				bool flag81 = flag80;
				bool flag82 = flag81;
				bool flag83 = flag82;
				bool flag84 = flag83;
				bool flag85 = flag84;
				bool flag86 = flag85;
				bool flag87 = flag86;
				if (flag87)
				{
					float num13 = rect.y + num9 + num3 + num4 * 0.5f;
					float num14 = (float)num * 1.1f;
					bool flag88 = this.hpCfg != null;
					bool flag89 = flag88;
					bool flag90 = flag89;
					bool flag91 = flag90;
					bool flag92 = flag91;
					bool flag93 = flag92;
					bool flag94 = flag93;
					bool flag95 = flag94;
					bool flag96 = flag95;
					bool flag97 = flag96;
					if (flag97)
					{
						Rect rect3 = new Rect(rect.x, num13, rect.width, num2);
						this.DrawRowBackground(current, rect3, this.coloredHp, color);
						bool flag98 = current.type == EventType.Repaint;
						bool flag99 = flag98;
						bool flag100 = flag99;
						bool flag101 = flag100;
						bool flag102 = flag101;
						bool flag103 = flag102;
						bool flag104 = flag103;
						bool flag105 = flag104;
						bool flag106 = flag105;
						bool flag107 = flag106;
						if (flag107)
						{
							this.DrawCheckbox(new Rect(rect3.x + (float)num, rect3.y + (num2 - num14) * 0.5f, num14, num14), this.coloredHp);
						}
						bool flag108 = GUI.Button(rect3, BtcLang.Tr("Colored HP (master switch)"), this.menuHatItemStyle);
						bool flag109 = flag108;
						bool flag110 = flag109;
						bool flag111 = flag110;
						bool flag112 = flag111;
						bool flag113 = flag112;
						bool flag114 = flag113;
						bool flag115 = flag114;
						bool flag116 = flag115;
						bool flag117 = flag116;
						if (flag117)
						{
							this.SetColoredHp(!this.coloredHp);
						}
						num13 += num2;
					}
					Rect rect4 = new Rect(rect.x, num13, rect.width, num2);
					this.DrawRowBackground(current, rect4, this.discordOn, color);
					bool flag118 = current.type == EventType.Repaint;
					bool flag119 = flag118;
					bool flag120 = flag119;
					bool flag121 = flag120;
					bool flag122 = flag121;
					bool flag123 = flag122;
					bool flag124 = flag123;
					bool flag125 = flag124;
					bool flag126 = flag125;
					bool flag127 = flag126;
					if (flag127)
					{
						this.DrawCheckbox(new Rect(rect4.x + (float)num, rect4.y + (num2 - num14) * 0.5f, num14, num14), this.discordOn);
					}
					bool flag128 = GUI.Button(rect4, BtcLang.Tr("Discord Rich Presence"), this.menuHatItemStyle);
					bool flag129 = flag128;
					bool flag130 = flag129;
					bool flag131 = flag130;
					bool flag132 = flag131;
					bool flag133 = flag132;
					bool flag134 = flag133;
					bool flag135 = flag134;
					bool flag136 = flag135;
					bool flag137 = flag136;
					if (flag137)
					{
						this.SetDiscord(!this.discordOn);
					}
				}
				Rect position2 = new Rect(rect.x, rect.y + num9 + num3 + num4 * 0.5f + num8, rect.width, rect.height - num9 - num3 - num4 * 1.5f - num8);
				Rect viewRect = new Rect(0f, 0f, position2.width - ((num7 > position2.height) ? 16f : 0f), num7);
				this.menuScroll = GUI.BeginScrollView(position2, this.menuScroll, viewRect);
				bool flag138 = this.menuHp;
				bool flag139 = flag138;
				bool flag140 = flag139;
				bool flag141 = flag140;
				bool flag142 = flag141;
				bool flag143 = flag142;
				bool flag144 = flag143;
				bool flag145 = flag144;
				bool flag146 = flag145;
				bool flag147 = flag146;
				if (flag147)
				{
					this.DrawHpRows(current, viewRect.width, num2, num, color);
				}
				else
				{
					bool flag148 = false;
					bool flag149 = flag148;
					bool flag150 = flag149;
					bool flag151 = flag150;
					bool flag152 = flag151;
					bool flag153 = flag152;
					bool flag154 = flag153;
					bool flag155 = flag154;
					bool flag156 = flag155;
					bool flag157 = flag156;
					if (flag157)
					{
					}
					else
					{
						bool flag158 = this.menuBc;
						bool flag159 = flag158;
						bool flag160 = flag159;
						bool flag161 = flag160;
						bool flag162 = flag161;
						bool flag163 = flag162;
						bool flag164 = flag163;
						bool flag165 = flag164;
						bool flag166 = flag165;
						bool flag167 = flag166;
						if (flag167)
						{
							this.DrawBcRows(current, viewRect.width, num2, num, color);
						}
						else
						{
							bool flag168 = this.menuDbg;
							bool flag169 = flag168;
							bool flag170 = flag169;
							bool flag171 = flag170;
							bool flag172 = flag171;
							bool flag173 = flag172;
							bool flag174 = flag173;
							bool flag175 = flag174;
							bool flag176 = flag175;
							bool flag177 = flag176;
							if (flag177)
							{
								this.DrawDbgRows(current, viewRect.width, num2, num, color);
							}
							else
							{
								bool tabActive = BtcHotkeysUI.TabActive;
								bool flag178 = tabActive;
								bool flag179 = flag178;
								bool flag180 = flag179;
								bool flag181 = flag180;
								bool flag182 = flag181;
								bool flag183 = flag182;
								if (flag183)
								{
									BtcHotkeysUI.Draw(current, viewRect.width, num2, num, color);
								}
								else
								{
									bool tabActive2 = BtcReplayUI.TabActive || BtcDuelUI.TabActive;
									bool flag184 = tabActive2;
									bool flag185 = flag184;
									bool flag186 = flag185;
									bool flag187 = flag186;
									bool flag188 = flag187;
									if (flag188)
									{
										bool flag189 = BtcDuelUI.TabActive ? BtcDuelUI.DrawTab(current, viewRect.width, num2, num, color) : BtcReplayUI.Draw(current, viewRect.width, num2, num, color);
										bool flag190 = flag189;
										bool flag191 = flag190;
										bool flag192 = flag191;
										bool flag193 = flag192;
										bool flag194 = flag193;
										if (flag194)
										{
											this.menuOpen = false;
										}
									}
									else
									{
										bool tabActive3 = BtcGameplayUI.TabActive;
										bool flag195 = tabActive3;
										bool flag196 = flag195;
										bool flag197 = flag196;
										if (flag197)
										{
											BtcGameplayUI.Draw(current, viewRect.width, num2, num, color);
										}
										else
										{
											bool tabActive4 = BtcNick.TabActive;
											bool flag198 = tabActive4;
											if (flag198)
											{
												BtcNick.Draw(current, viewRect.width, num2, num, color);
											}
											else
											{
												bool tabActive5 = BtcShareUI.TabActive;
												bool flag199 = tabActive5;
												if (flag199)
												{
													BtcShareUI.Draw(current, viewRect.width, num2, num, color);
												}
												else
												{
													this.DrawPackTab(current, viewRect.width, num2, num, color);
												}
											}
										}
									}
								}
							}
						}
					}
				}
				GUI.EndScrollView();
				GUI.color = color;
				bool flag200 = current.type == EventType.MouseDown && rect.Contains(current.mousePosition);
				bool flag201 = flag200;
				bool flag202 = flag201;
				bool flag203 = flag202;
				bool flag204 = flag203;
				bool flag205 = flag204;
				bool flag206 = flag205;
				bool flag207 = flag206;
				bool flag208 = flag207;
				bool flag209 = flag208;
				if (flag209)
				{
					current.Use();
				}
			}
		}
	}

	private void DrawMessage()
	{
		bool flag = this.message == null || Event.current.type != EventType.Repaint;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			float num = Time.unscaledTime - this.messageStart;
			bool flag9 = num >= 1.5f;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				this.message = null;
			}
			else
			{
				float a = (num < 1.2f) ? 1f : Mathf.Clamp01((1.5f - num) / 0.3f);
				bool flag17 = this.style == null;
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				if (flag24)
				{
					this.style = new GUIStyle(GUI.skin.label);
					this.style.alignment = TextAnchor.MiddleCenter;
					this.style.fontStyle = FontStyle.Bold;
				}
				this.style.fontSize = Mathf.Max(16, Screen.height / 28);
				GUI.depth = -1000;
				Rect position = new Rect(0f, (float)Screen.height * 0.5f - 80f, (float)Screen.width, 160f);
				Color color = GUI.color;
				GUI.color = new Color(0f, 0f, 0f, a);
				GUI.Label(new Rect(position.x + 2f, position.y + 2f, position.width, position.height), this.message, this.style);
				GUI.color = new Color(1f, 1f, 1f, a);
				GUI.Label(position, this.message, this.style);
				GUI.color = color;
			}
		}
	}

	private static void RebuildCursors()
	{
		try
		{
			Curseur curseur = Curseur.instance;
			bool flag = curseur == null;
			bool flag2 = !flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				MethodInfo method = typeof(Curseur).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
				bool flag9 = method == null;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					ResourcePackManager.PackWarn("[ResourcePacks] Curseur.Awake not found - cursors not refreshed");
				}
				else
				{
					method.Invoke(curseur, null);
					ResourcePackManager.PackLog("[ResourcePacks] Cursors rebuilt from 'Curseurs'");
				}
			}
		}
		catch (Exception arg)
		{
			ResourcePackManager.PackWarn("[ResourcePacks] Cannot rebuild cursors: " + arg);
		}
	}

	private static Dictionary<string, ResourcePackManager.HatSpec> BuildHatSpecs()
	{
		Dictionary<string, ResourcePackManager.HatSpec> dictionary = new Dictionary<string, ResourcePackManager.HatSpec>();
		ResourcePackManager.HatSpec[] array = new ResourcePackManager.HatSpec[]
		{
			new ResourcePackManager.HatSpec("fy_ArbalistHat", "Arbalist - Cowboy", "Kepi", "Cowboy", "Shader Forge/FalloffBumpDiffuseSpecularReflective", "Specular", new Color(0.537f, 0.368f, 0.184f, 1f), 0.5f, new Vector3(0f, -0.229089f, -0.006397f), new Vector3(353.6631f, 0f, 0f), Vector3.zero),
			new ResourcePackManager.HatSpec("fy_AlchemistHat", "Alchemist - Clown", "Bonnet", "Clown", "Shader Forge/FalloffBumpDiffuseSpecular", "Specular", new Color(1f, 0.228f, 0f, 1f), 1f, new Vector3(-0.00011f, -0.023184f, -0.099536f), Vector3.zero, Vector3.zero),
			new ResourcePackManager.HatSpec("fy_HalberdierHelmet", "Halberdier - Pirate", "KettleHat", "Pirate", "Shader Forge/FalloffBumpDiffuse", null, new Color(0f, 0f, 0f, 1f), -1f, Vector3.zero, Vector3.zero, new Vector3(-90f, 0f, 0f)),
			new ResourcePackManager.HatSpec("fy_ArtificerHat", "Artificer - Vampire", "ArtificerHat", "Vampire", "Shader Forge/FalloffBumpDiffuseSpecular", "Specular", new Color(0.537f, 0.267f, 0.184f, 1f), 0.618f, new Vector3(0f, 0.133287f, 0.006121f), Vector3.zero, Vector3.zero),
			new ResourcePackManager.HatSpec("fy_KnightHelmetProp", "Knight - Frankenstein", "Helmet", "Frankenstein", "Shader Forge/FalloffBumpDiffuseSpecular", "Specular", new Color(0.537f, 0.369f, 0.184f, 1f), 1f, new Vector3(0f, -0.102833f, 0f), Vector3.zero, Vector3.zero),
			new ResourcePackManager.HatSpec("fy_ArcherCapProp", "Archer - Witch hat", "Cap", "WitchHat", "Shader Forge/FalloffBumpDiffuseSpecularReflective", "Gloss", new Color(0f, 0.004f, 0.322f, 1f), 0.5f, new Vector3(0f, -0.147958f, 0f), new Vector3(347.7499f, 0f, 0f), Vector3.zero)
		};
		for (int i = 0; i < array.Length; i++)
		{
			dictionary[array[i].handle] = array[i];
			ResourcePackManager.HatOrder.Add(array[i]);
		}
		return dictionary;
	}

	private static string ResolveModFile(string relative)
	{
		string text = System.IO.Path.Combine(Directory.GetParent(Application.dataPath).FullName, relative);
		bool flag = File.Exists(text);
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
			result = text;
		}
		else
		{
			result = (File.Exists(relative) ? relative : null);
		}
		return result;
	}

	private static Texture2D LoadHatTexture(string path, bool normalMap)
	{
		byte[] data = File.ReadAllBytes(path);
		bool flag = !normalMap;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		Texture2D result;
		if (flag8)
		{
			Texture2D texture2D = new Texture2D(2, 2);
			texture2D.LoadImage(data);
			result = texture2D;
		}
		else
		{
			Texture2D texture2D2 = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
			texture2D2.LoadImage(data);
			Texture2D texture2D3 = new Texture2D(texture2D2.width, texture2D2.height, TextureFormat.RGBA32, false, true);
			Color[] pixels = texture2D2.GetPixels();
			for (int i = 0; i < pixels.Length; i++)
			{
				pixels[i].a = pixels[i].r;
				pixels[i].r = pixels[i].g;
				pixels[i].b = pixels[i].g;
			}
			texture2D3.SetPixels(pixels);
			texture2D3.Apply();
			UnityEngine.Object.Destroy(texture2D2);
			result = texture2D3;
		}
		return result;
	}

	private ResourcePackManager.HatSet GetHatSet(ResourcePackManager.HatSpec spec)
	{
		ResourcePackManager.HatSet hatSet;
		bool flag = this.hatSets.TryGetValue(spec.handle, out hatSet);
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		ResourcePackManager.HatSet result;
		if (flag8)
		{
			result = hatSet;
		}
		else
		{
			bool flag9 = this.hatSetFailed.Contains(spec.handle);
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				result = null;
			}
			else
			{
				try
				{
					string text = "Info/Lua/Mods/halloween2017/" + spec.folder + "/" + spec.folder;
					string text2 = ResourcePackManager.ResolveModFile(text + ".obj");
					bool flag17 = text2 == null;
					bool flag18 = flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					bool flag21 = flag20;
					bool flag22 = flag21;
					bool flag23 = flag22;
					bool flag24 = flag23;
					if (flag24)
					{
						throw new FileNotFoundException("File not found: " + text + ".obj");
					}
					string text3 = ResourcePackManager.ResolveModFile(text + "_Diffuse.jpg");
					string text4 = ResourcePackManager.ResolveModFile(text + "_Normals.jpg");
					string text5 = (spec.specSuffix != null) ? ResourcePackManager.ResolveModFile(text + "_" + spec.specSuffix + ".jpg") : null;
					bool flag25 = text3 == null || text4 == null || (spec.specSuffix != null && text5 == null);
					bool flag26 = flag25;
					bool flag27 = flag26;
					bool flag28 = flag27;
					bool flag29 = flag28;
					bool flag30 = flag29;
					bool flag31 = flag30;
					bool flag32 = flag31;
					if (flag32)
					{
						throw new FileNotFoundException("Texture files not found next to " + text + ".obj");
					}
					Shader shader = Shader.Find(spec.shader);
					bool flag33 = shader == null;
					bool flag34 = flag33;
					bool flag35 = flag34;
					bool flag36 = flag35;
					bool flag37 = flag36;
					bool flag38 = flag37;
					bool flag39 = flag38;
					bool flag40 = flag39;
					if (flag40)
					{
						throw new Exception("Shader not found: " + spec.shader);
					}
					Mesh mesh = ObjImporter.ImportFile(text2);
					bool flag41 = spec.normalsEuler != Vector3.zero;
					bool flag42 = flag41;
					bool flag43 = flag42;
					bool flag44 = flag43;
					bool flag45 = flag44;
					bool flag46 = flag45;
					bool flag47 = flag46;
					bool flag48 = flag47;
					if (flag48)
					{
						Quaternion rotation = Quaternion.Euler(spec.normalsEuler);
						Vector3[] normals = mesh.normals;
						for (int i = 0; i < normals.Length; i++)
						{
							normals[i] = rotation * normals[i];
						}
						mesh.normals = normals;
					}
					LuaMesh.RecalculateTangents(mesh);
					Material material = new Material(shader);
					material.SetTexture("_Diffuse", ResourcePackManager.LoadHatTexture(text3, false));
					material.SetTexture("_Normals", ResourcePackManager.LoadHatTexture(text4, true));
					bool flag49 = text5 != null;
					bool flag50 = flag49;
					bool flag51 = flag50;
					bool flag52 = flag51;
					bool flag53 = flag52;
					bool flag54 = flag53;
					bool flag55 = flag54;
					bool flag56 = flag55;
					if (flag56)
					{
						material.SetTexture("_Specular", ResourcePackManager.LoadHatTexture(text5, false));
					}
					material.SetColor("_FalloffColor", spec.falloff);
					bool flag57 = spec.gloss >= 0f;
					bool flag58 = flag57;
					bool flag59 = flag58;
					bool flag60 = flag59;
					bool flag61 = flag60;
					bool flag62 = flag61;
					bool flag63 = flag62;
					bool flag64 = flag63;
					if (flag64)
					{
						material.SetFloat("_Gloss", spec.gloss);
					}
					hatSet = new ResourcePackManager.HatSet();
					hatSet.mesh = mesh;
					hatSet.mat = material;
					hatSet.grip = Matrix4x4.TRS(spec.gripPos, Quaternion.Euler(spec.gripEuler), Vector3.one);
					this.hatSets[spec.handle] = hatSet;
					ResourcePackManager.PackLog(string.Concat(new string[]
					{
						"[ResourcePacks] Halloween hat loaded: ",
						spec.folder,
						" (",
						spec.handle,
						")"
					}));
					result = hatSet;
				}
				catch (Exception ex)
				{
					this.hatSetFailed.Add(spec.handle);
					ResourcePackManager.PackWarn("[ResourcePacks] Cannot load Halloween hat '" + spec.folder + "': " + ex.Message);
					result = null;
				}
			}
		}
		return result;
	}

	private void LoadHatSettings()
	{
		this.hatsOff.Clear();
		try
		{
			string path = BtcPaths.Config("hats.txt");
			bool flag = File.Exists(path);
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				string[] array = File.ReadAllLines(path);
				for (int i = 0; i < array.Length; i++)
				{
					string text = array[i].Trim();
					bool flag9 = ResourcePackManager.HatSpecs.ContainsKey(text);
					bool flag10 = flag9;
					bool flag11 = flag10;
					bool flag12 = flag11;
					bool flag13 = flag12;
					bool flag14 = flag13;
					bool flag15 = flag14;
					bool flag16 = flag15;
					if (flag16)
					{
						this.hatsOff.Add(text);
					}
				}
			}
		}
		catch (Exception ex)
		{
			ResourcePackManager.PackWarn("[ResourcePacks] Cannot read hats.txt: " + ex.Message);
		}
	}
}
}
