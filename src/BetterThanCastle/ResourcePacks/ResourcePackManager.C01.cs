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
	public static void Boot()
	{
		bool flag = ResourcePackManager.instance != null;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			GameObject gameObject = new GameObject("ResourcePackManager");
			UnityEngine.Object.DontDestroyOnLoad(gameObject);
			ResourcePackManager.instance = gameObject.AddComponent<ResourcePackManager>();
		}
	}

	private static string RootDir
	{
		get
		{
			BtcPaths.Init();
			return BtcPaths.Packs;
		}
	}

	private void Awake()
	{
		try
		{
			BtcPaths.Init();
			File.WriteAllText(ResourcePackManager.LogPath, string.Empty);
			ResourcePackManager.PackLog("[ResourcePacks] Started. Root: " + ResourcePackManager.RootDir);
			ResourcePackManager.PackLog("[ResourcePacks] Packs found: " + string.Join(", ", ResourcePackManager.ListPacks(true).ToArray()));
		}
		catch (Exception ex)
		{
			ResourcePackManager.PackWarn("[ResourcePacks] Cannot create folder: " + ex.Message);
		}
		this.LoadHatSettings();
		this.LoadOptions();
		this.ReloadHpConfig(false);
		BtcDiscord.SetEnabled(this.discordOn);
		SceneManager.sceneLoaded += this.OnSceneLoaded;
		Camera.onPreCull = (Camera.CameraCallback)Delegate.Combine(Camera.onPreCull, new Camera.CameraCallback(this.OnCamPreCull));
		Camera.onPostRender = (Camera.CameraCallback)Delegate.Combine(Camera.onPostRender, new Camera.CameraCallback(this.OnCamPostRender));
		this.burstUntil = Time.unscaledTime + 10f;
		string text = this.ReadSavedPack();
		bool flag = text != null && Directory.Exists(System.IO.Path.Combine(ResourcePackManager.RootDir, text));
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			this.SetPack(text, false);
		}
	}

	private void OnDestroy()
	{
		this.ReleaseMenuGameLock();
		BtcReplay.Shutdown();
		SceneManager.sceneLoaded -= this.OnSceneLoaded;
		Camera.onPreCull = (Camera.CameraCallback)Delegate.Remove(Camera.onPreCull, new Camera.CameraCallback(this.OnCamPreCull));
		Camera.onPostRender = (Camera.CameraCallback)Delegate.Remove(Camera.onPostRender, new Camera.CameraCallback(this.OnCamPostRender));
		this.ClearPredictions();
		this.RestoreLighting();
		bool flag = this.pathMat != null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			UnityEngine.Object.Destroy(this.pathMat);
		}
		bool flag9 = ResourcePackManager.instance == this;
		bool flag10 = flag9;
		bool flag11 = flag10;
		bool flag12 = flag11;
		bool flag13 = flag12;
		bool flag14 = flag13;
		bool flag15 = flag14;
		bool flag16 = flag15;
		if (flag16)
		{
			ResourcePackManager.instance = null;
		}
		bool flag17 = this.whiteTex != null;
		bool flag18 = flag17;
		bool flag19 = flag18;
		bool flag20 = flag19;
		bool flag21 = flag20;
		bool flag22 = flag21;
		bool flag23 = flag22;
		bool flag24 = flag23;
		if (flag24)
		{
			UnityEngine.Object.Destroy(this.whiteTex);
		}
		bool flag25 = this.svTex != null;
		bool flag26 = flag25;
		bool flag27 = flag26;
		bool flag28 = flag27;
		bool flag29 = flag28;
		bool flag30 = flag29;
		bool flag31 = flag30;
		bool flag32 = flag31;
		if (flag32)
		{
			UnityEngine.Object.Destroy(this.svTex);
		}
		bool flag33 = this.hueTex != null;
		bool flag34 = flag33;
		bool flag35 = flag34;
		bool flag36 = flag35;
		bool flag37 = flag36;
		bool flag38 = flag37;
		bool flag39 = flag38;
		bool flag40 = flag39;
		if (flag40)
		{
			UnityEngine.Object.Destroy(this.hueTex);
		}
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		this.noMatch.Clear();
		this.burstUntil = Time.unscaledTime + 10f;
		this.nextScan = 0f;
		this.hatsLevelApplyAt = Time.unscaledTime + 2f;
		this.hatsLevelTries = 0;
		this.hpNextScan = 0f;
	}

	private void Update()
	{
		bool capturing = BtcHotkeys.Capturing;
		bool escBlocked = BtcHotkeys.EscBlocked;
		bool flag = BtcHotkeys.MenuOpen != this.menuOpen;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			BtcHotkeys.MenuOpen = this.menuOpen;
		}
		BtcHotkeys.Tick();
		BtcDiscord.Poll();
		BtcOverlays.Tick();
		BtcUnitPlates.Tick();
		BtcInterface.Tick();
		BtcMatchStats.Tick();
		BtcMatchStatsUI.Tick();
		BtcReplay.Tick();
		BtcNetDiag.Tick();
		BtcSimplePhys.Tick();
		BtcKick.Tick();
		this.UpdateMenuGameLock();
		bool flag9 = ChatBoxDriver.Instance != null && ChatBoxDriver.Instance.IsFocused;
		bool flag10 = !flag9;
		bool flag11 = flag10;
		bool flag12 = flag11;
		bool flag13 = flag12;
		bool flag14 = flag13;
		bool flag15 = flag14;
		bool flag16 = flag15;
		bool flag17 = flag16;
		bool flag18 = flag17;
		bool flag19 = flag18;
		bool flag20 = flag19;
		if (flag20)
		{
			bool flag21 = Input.GetKeyDown(KeyCode.PageDown) && !capturing;
			bool flag22 = flag21;
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
				this.ToggleMenu();
			}
			bool flag32 = this.menuOpen && Input.GetKeyDown(KeyCode.Escape) && !escBlocked;
			bool flag33 = flag32;
			bool flag34 = flag33;
			bool flag35 = flag34;
			bool flag36 = flag35;
			bool flag37 = flag36;
			bool flag38 = flag37;
			bool flag39 = flag38;
			bool flag40 = flag39;
			bool flag41 = flag40;
			bool flag42 = flag41;
			if (flag42)
			{
				this.menuOpen = false;
			}
		}
		bool flag43 = this.dbgDumpTexPending;
		bool flag44 = flag43;
		bool flag45 = flag44;
		bool flag46 = flag45;
		bool flag47 = flag46;
		bool flag48 = flag47;
		bool flag49 = flag48;
		bool flag50 = flag49;
		bool flag51 = flag50;
		bool flag52 = flag51;
		bool flag53 = flag52;
		if (flag53)
		{
			this.dbgDumpTexPending = false;
			this.DumpTextures();
		}
		bool flag54 = this.dbgDumpBldPending;
		bool flag55 = flag54;
		bool flag56 = flag55;
		bool flag57 = flag56;
		bool flag58 = flag57;
		bool flag59 = flag58;
		bool flag60 = flag59;
		bool flag61 = flag60;
		bool flag62 = flag61;
		bool flag63 = flag62;
		bool flag64 = flag63;
		if (flag64)
		{
			this.dbgDumpBldPending = false;
			this.DumpBuildings();
		}
		this.UpdateAlwaysRings();
		this.ForceShowHp();
		this.ApplyStaminaRules();
		this.UpdateAmmoUnits();
		this.UpdateStatUnits();
		this.UpdateBuildingUnits();
		this.PollClientOrders();
		this.ProcessPredictions();
		this.UpdateBombMap();
		this.UpdateHitCounters();
		this.UpdateCenters();
		this.UpdateExplosionDist();
		this.UpdateCrystalTimers();
		this.UpdatePathUnits();
		this.UpdateEnemyTextures();
		bool flag65 = this.optSaveAt > 0f && Time.unscaledTime >= this.optSaveAt && !Input.GetMouseButton(0);
		bool flag66 = flag65;
		bool flag67 = flag66;
		bool flag68 = flag67;
		bool flag69 = flag68;
		bool flag70 = flag69;
		bool flag71 = flag70;
		bool flag72 = flag71;
		bool flag73 = flag72;
		bool flag74 = flag73;
		bool flag75 = flag74;
		if (flag75)
		{
			this.optSaveAt = 0f;
			this.SaveOptions();
		}
		this.UpdateLightCache();
		bool flag76 = this.hpSaveAt > 0f && Time.unscaledTime >= this.hpSaveAt && !Input.GetMouseButton(0);
		bool flag77 = flag76;
		bool flag78 = flag77;
		bool flag79 = flag78;
		bool flag80 = flag79;
		bool flag81 = flag80;
		bool flag82 = flag81;
		bool flag83 = flag82;
		bool flag84 = flag83;
		bool flag85 = flag84;
		bool flag86 = flag85;
		if (flag86)
		{
			this.hpSaveAt = 0f;
			this.SaveHpConfig();
		}
		bool flag87 = this.hatsPending.Count > 0;
		bool flag88 = flag87;
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
			List<string> types = new List<string>(this.hatsPending);
			this.hatsPending.Clear();
			this.ApplyHats(types);
		}
		bool flag98 = this.hatsLevelApplyAt > 0f && Time.unscaledTime >= this.hatsLevelApplyAt;
		bool flag99 = flag98;
		bool flag100 = flag99;
		bool flag101 = flag100;
		bool flag102 = flag101;
		bool flag103 = flag102;
		bool flag104 = flag103;
		bool flag105 = flag104;
		bool flag106 = flag105;
		bool flag107 = flag106;
		bool flag108 = flag107;
		if (flag108)
		{
			this.hatsLevelTries++;
			this.hatsLevelApplyAt = ((!this.ApplyHats(null) && this.hatsLevelTries < 8) ? (Time.unscaledTime + 2f) : 0f);
		}
		bool flag109 = this.currentPack == null;
		bool flag110 = !flag109;
		bool flag111 = flag110;
		bool flag112 = flag111;
		bool flag113 = flag112;
		bool flag114 = flag113;
		bool flag115 = flag114;
		bool flag116 = flag115;
		bool flag117 = flag116;
		bool flag118 = flag117;
		bool flag119 = flag118;
		if (flag119)
		{
			float unscaledTime = Time.unscaledTime;
			bool flag120 = unscaledTime < this.nextScan;
			bool flag121 = !flag120;
			bool flag122 = flag121;
			bool flag123 = flag122;
			bool flag124 = flag123;
			bool flag125 = flag124;
			bool flag126 = flag125;
			bool flag127 = flag126;
			bool flag128 = flag127;
			bool flag129 = flag128;
			bool flag130 = flag129;
			if (flag130)
			{
				this.nextScan = unscaledTime + ((unscaledTime < this.burstUntil) ? 1.5f : 10f);
				this.ApplyAll();
			}
		}
	}

	private void SetPack(string packName, bool showMessage)
	{
		this.currentPack = packName;
		this.index = ((packName == null) ? new Dictionary<string, ResourcePackManager.IndexItem>(StringComparer.OrdinalIgnoreCase) : ResourcePackManager.BuildIndex(System.IO.Path.Combine(ResourcePackManager.RootDir, packName)));
		this.noMatch.Clear();
		this.matchedFiles.Clear();
		this.ApplyAll();
		this.nextScan = Time.unscaledTime + 1.5f;
		int num = 0;
		foreach (KeyValuePair<int, ResourcePackManager.Entry> keyValuePair in this.entries)
		{
			bool applied = keyValuePair.Value.applied;
			bool flag = applied;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			if (flag7)
			{
				num++;
			}
		}
		List<string> list = ResourcePackManager.IndexFiles(this.index);
		int count = list.Count;
		List<string> list2 = new List<string>();
		for (int i = 0; i < list.Count; i++)
		{
			bool flag8 = !this.matchedFiles.Contains(list[i]);
			bool flag9 = flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			if (flag15)
			{
				list2.Add(System.IO.Path.GetFileNameWithoutExtension(list[i]));
			}
		}
		ResourcePackManager.PackLog(string.Concat(new object[]
		{
			"[ResourcePacks] Pack: ",
			packName ?? "Default",
			", files: ",
			count,
			", textures replaced: ",
			num,
			", files without matching texture: ",
			list2.Count
		}));
		bool flag16 = list2.Count > 0;
		bool flag17 = flag16;
		bool flag18 = flag17;
		bool flag19 = flag18;
		bool flag20 = flag19;
		bool flag21 = flag20;
		bool flag22 = flag21;
		bool flag23 = flag22;
		if (flag23)
		{
			list2.Sort();
			int num2 = Math.Min(list2.Count, 40);
			ResourcePackManager.PackLog(string.Concat(new object[]
			{
				"[ResourcePacks] No texture with these names (first ",
				num2,
				"): ",
				string.Join(", ", list2.GetRange(0, num2).ToArray())
			}));
		}
		this.SaveSelectedPack();
		if (showMessage)
		{
			string text = "Resource pack changed - " + (packName ?? "Default");
			bool flag24 = packName != null;
			bool flag25 = flag24;
			bool flag26 = flag25;
			bool flag27 = flag26;
			bool flag28 = flag27;
			bool flag29 = flag28;
			bool flag30 = flag29;
			bool flag31 = flag30;
			if (flag31)
			{
				text += ((count == 0) ? "\nno .png files found in this pack folder" : string.Concat(new object[]
				{
					"\nfiles: ",
					count,
					", replaced: ",
					num,
					", no match: ",
					list2.Count
				}));
			}
			this.ShowMessage(text);
		}
	}

	private string ReadSavedPack()
	{
		try
		{
			string path = BtcPaths.Config("selected.txt");
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
				string text = File.ReadAllText(path).Trim();
				return (text.Length > 0) ? text : null;
			}
		}
		catch (Exception)
		{
		}
		return null;
	}

	private void SaveSelectedPack()
	{
		try
		{
			File.WriteAllText(BtcPaths.Config("selected.txt"), this.currentPack ?? string.Empty);
		}
		catch (Exception)
		{
		}
	}

	private void ApplyAll()
	{
		Texture2D[] array = Resources.FindObjectsOfTypeAll<Texture2D>();
		for (int i = 0; i < array.Length; i++)
		{
			this.ProcessTexture(array[i]);
		}
		List<int> list = null;
		foreach (KeyValuePair<int, ResourcePackManager.Entry> keyValuePair in this.entries)
		{
			bool flag = keyValuePair.Value.tex == null;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				bool flag9 = list == null;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					list = new List<int>();
				}
				list.Add(keyValuePair.Key);
			}
		}
		bool flag17 = list != null;
		bool flag18 = flag17;
		bool flag19 = flag18;
		bool flag20 = flag19;
		bool flag21 = flag20;
		bool flag22 = flag21;
		bool flag23 = flag22;
		bool flag24 = flag23;
		if (flag24)
		{
			for (int j = 0; j < list.Count; j++)
			{
				this.entries.Remove(list[j]);
			}
		}
		bool flag25 = ResourcePackManager.cursorsDirty;
		bool flag26 = flag25;
		bool flag27 = flag26;
		bool flag28 = flag27;
		bool flag29 = flag28;
		bool flag30 = flag29;
		bool flag31 = flag30;
		bool flag32 = flag31;
		if (flag32)
		{
			ResourcePackManager.cursorsDirty = false;
			ResourcePackManager.RebuildCursors();
		}
	}

	private void ProcessTexture(Texture2D tex)
	{
		bool flag = tex == null;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			int instanceID = tex.GetInstanceID();
			ResourcePackManager.Entry entry;
			bool flag9 = this.entries.TryGetValue(instanceID, out entry);
			bool flag10 = !flag9 && this.noMatch.Contains(instanceID);
			bool flag11 = !flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			bool flag17 = flag16;
			if (flag17)
			{
				string name = tex.name;
				ResourcePackManager.IndexItem indexItem = null;
				bool flag18 = string.IsNullOrEmpty(name) || !this.index.TryGetValue(name, out indexItem);
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				bool flag25 = flag24;
				if (flag25)
				{
					bool flag26 = flag9;
					bool flag27 = flag26;
					bool flag28 = flag27;
					bool flag29 = flag28;
					bool flag30 = flag29;
					bool flag31 = flag30;
					bool flag32 = flag31;
					bool flag33 = flag32;
					if (flag33)
					{
						ResourcePackManager.Revert(entry);
					}
					else
					{
						this.noMatch.Add(instanceID);
					}
				}
				else
				{
					string text = indexItem.plain;
					bool flag34 = indexItem.bySig != null;
					bool flag35 = flag34;
					bool flag36 = flag35;
					bool flag37 = flag36;
					bool flag38 = flag37;
					bool flag39 = flag38;
					bool flag40 = flag39;
					bool flag41 = flag40;
					if (flag41)
					{
						string signature = this.GetSignature(tex, instanceID, flag9 ? entry : null);
						string text2 = ResourcePackManager.FindVariantFile(indexItem, signature);
						bool flag42 = text2 != null;
						bool flag43 = flag42;
						bool flag44 = flag43;
						bool flag45 = flag44;
						bool flag46 = flag45;
						bool flag47 = flag46;
						bool flag48 = flag47;
						bool flag49 = flag48;
						if (flag49)
						{
							text = text2;
						}
					}
					bool flag50 = text == null;
					bool flag51 = flag50;
					bool flag52 = flag51;
					bool flag53 = flag52;
					bool flag54 = flag53;
					bool flag55 = flag54;
					bool flag56 = flag55;
					bool flag57 = flag56;
					if (flag57)
					{
						bool flag58 = flag9;
						bool flag59 = flag58;
						bool flag60 = flag59;
						bool flag61 = flag60;
						bool flag62 = flag61;
						bool flag63 = flag62;
						bool flag64 = flag63;
						bool flag65 = flag64;
						if (flag65)
						{
							ResourcePackManager.Revert(entry);
						}
						else
						{
							this.noMatch.Add(instanceID);
						}
					}
					else
					{
						this.matchedFiles.Add(text);
						long ticks = File.GetLastWriteTimeUtc(text).Ticks;
						bool flag66 = flag9 && entry.file == text && entry.stamp == ticks;
						bool flag67 = !flag66;
						bool flag68 = flag67;
						bool flag69 = flag68;
						bool flag70 = flag69;
						bool flag71 = flag70;
						bool flag72 = flag71;
						bool flag73 = flag72;
						if (flag73)
						{
							bool flag74 = !flag9;
							bool flag75 = flag74;
							bool flag76 = flag75;
							bool flag77 = flag76;
							bool flag78 = flag77;
							bool flag79 = flag78;
							bool flag80 = flag79;
							bool flag81 = flag80;
							if (flag81)
							{
								string sig;
								byte[] original;
								try
								{
									original = ResourcePackManager.Snapshot(tex, out sig);
								}
								catch (Exception ex)
								{
									ResourcePackManager.PackWarn("[ResourcePacks] Cannot back up texture '" + name + "': " + ex.Message);
									this.noMatch.Add(instanceID);
									return;
								}
								entry = new ResourcePackManager.Entry();
								entry.tex = tex;
								entry.original = original;
								entry.sig = sig;
								this.entries[instanceID] = entry;
							}
							bool flag82 = false;
							try
							{
								flag82 = ResourcePackManager.LoadInto(tex, File.ReadAllBytes(text));
							}
							catch (Exception ex2)
							{
								ResourcePackManager.PackWarn("[ResourcePacks] Cannot load '" + text + "': " + ex2.Message);
							}
							bool flag83 = !flag82;
							bool flag84 = flag83;
							bool flag85 = flag84;
							bool flag86 = flag85;
							bool flag87 = flag86;
							bool flag88 = flag87;
							bool flag89 = flag88;
							bool flag90 = flag89;
							if (flag90)
							{
								ResourcePackManager.PackWarn(string.Concat(new object[]
								{
									"[ResourcePacks] LoadImage failed for ",
									text,
									" (target texture: ",
									tex.width,
									"x",
									tex.height,
									", ",
									tex.format,
									")"
								}));
								ResourcePackManager.LoadInto(tex, entry.original);
							}
							bool flag91 = string.Equals(name, "Curseurs", StringComparison.OrdinalIgnoreCase);
							bool flag92 = flag91;
							bool flag93 = flag92;
							bool flag94 = flag93;
							bool flag95 = flag94;
							bool flag96 = flag95;
							bool flag97 = flag96;
							bool flag98 = flag97;
							if (flag98)
							{
								ResourcePackManager.cursorsDirty = true;
							}
							entry.file = text;
							entry.stamp = ticks;
							entry.applied = flag82;
							bool flag99 = flag82;
							bool flag100 = flag99;
							bool flag101 = flag100;
							bool flag102 = flag101;
							bool flag103 = flag102;
							bool flag104 = flag103;
							bool flag105 = flag104;
							bool flag106 = flag105;
							if (flag106)
							{
								ResourcePackManager.PackLog(string.Concat(new object[]
								{
									"[ResourcePacks] Replaced '",
									name,
									"' (",
									tex.width,
									"x",
									tex.height,
									") <- ",
									text
								}));
							}
						}
					}
				}
			}
		}
	}

	private static bool LoadInto(Texture2D tex, byte[] data)
	{
		FilterMode filterMode = tex.filterMode;
		TextureWrapMode wrapMode = tex.wrapMode;
		int anisoLevel = tex.anisoLevel;
		float mipMapBias = tex.mipMapBias;
		bool result = tex.LoadImage(data);
		tex.filterMode = filterMode;
		tex.wrapMode = wrapMode;
		tex.anisoLevel = anisoLevel;
		tex.mipMapBias = mipMapBias;
		return result;
	}

	private void OnGUI()
	{
		BtcOverlays.OnGUI();
		BtcMatchStatsUI.OnGUI();
		BtcReplayUI.OnGUI();
		BtcGameplayUI.OnGUI();
		BtcSessionTick.OnGUI();
		BtcNick.OnGUI();
		ResourcePackManager.HpConfig hpConfig = this.hpCfg;
		bool flag = this.AltShow(hpConfig, hpConfig != null && hpConfig.altAmmo);
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
			this.DrawAmmoNumbers();
		}
		bool flag10 = this.AltShow(hpConfig, hpConfig != null && hpConfig.altNumbers);
		bool flag11 = flag10;
		bool flag12 = flag11;
		bool flag13 = flag12;
		bool flag14 = flag13;
		bool flag15 = flag14;
		bool flag16 = flag15;
		bool flag17 = flag16;
		bool flag18 = flag17;
		if (flag18)
		{
			this.DrawStatNumbers();
			this.DrawBuildingNumbers();
		}
		bool flag19 = this.AltShow(hpConfig, hpConfig != null && hpConfig.altBombMap);
		bool flag20 = flag19;
		bool flag21 = flag20;
		bool flag22 = flag21;
		bool flag23 = flag22;
		bool flag24 = flag23;
		bool flag25 = flag24;
		bool flag26 = flag25;
		bool flag27 = flag26;
		if (flag27)
		{
			this.DrawBombNumbers();
		}
		bool flag28 = this.AltShow(hpConfig, hpConfig != null && hpConfig.altDamage);
		bool flag29 = flag28;
		bool flag30 = flag29;
		bool flag31 = flag30;
		bool flag32 = flag31;
		bool flag33 = flag32;
		bool flag34 = flag33;
		bool flag35 = flag34;
		bool flag36 = flag35;
		if (flag36)
		{
			this.DrawHitCounters();
		}
		bool flag37 = this.AltShow(hpConfig, hpConfig != null && hpConfig.altCrystal);
		bool flag38 = flag37;
		bool flag39 = flag38;
		bool flag40 = flag39;
		bool flag41 = flag40;
		bool flag42 = flag41;
		bool flag43 = flag42;
		bool flag44 = flag43;
		bool flag45 = flag44;
		if (flag45)
		{
			this.DrawCrystalTimers();
		}
		this.DrawCenters();
		this.DrawExplosionDist();
		bool flag46 = this.menuOpen;
		bool flag47 = flag46;
		bool flag48 = flag47;
		bool flag49 = flag48;
		bool flag50 = flag49;
		bool flag51 = flag50;
		bool flag52 = flag51;
		bool flag53 = flag52;
		bool flag54 = flag53;
		if (flag54)
		{
			this.DrawMenu();
		}
		this.DrawMessage();
	}

	public ResourcePackManager()
	{
		this.entries = new Dictionary<int, ResourcePackManager.Entry>();
		this.noMatch = new HashSet<int>();
		this.matchedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		this.sigCache = new Dictionary<int, string>();
		this.index = new Dictionary<string, ResourcePackManager.IndexItem>(StringComparer.OrdinalIgnoreCase);
		this.menuPacks = new List<string>();
		this.hatsOff = new HashSet<string>(StringComparer.Ordinal);
		this.hatSets = new Dictionary<string, ResourcePackManager.HatSet>();
		this.hatSetFailed = new HashSet<string>();
		this.hatVanilla = new Dictionary<string, ResourcePackManager.HatVanilla>();
		this.hatsPending = new List<string>();
		this.hpContentH = 600f;
		bool[] array = new bool[7];
		array[0] = true;
		this.palOpen = array;
		this.svTexHue = -1f;
		this.hpCfgStamp = DateTime.MinValue;
		this.hpRings = new List<ResourcePackManager.HpRing>();
		this.hpRingIds = new HashSet<int>();
		this.autoRings = new Dictionary<Labor, ResourcePackManager.AutoRing>();
		this.autoTmp = new List<Labor>();
		this.ammoUnits = new List<ResourcePackManager.AmmoUnit>();
		this.ammoCache = new Dictionary<Labor, ResourcePackManager.AmmoUnit>();
		this.statUnits = new List<ResourcePackManager.StatUnit>();
		this.statCache = new Dictionary<Labor, ResourcePackManager.StatUnit>();
		this.statSeen = new HashSet<Labor>();
		this.bldUnits = new List<ResourcePackManager.BldUnit>();
		this.bldCache = new Dictionary<Transform, ResourcePackManager.BldUnit>();
		this.pathUnits = new List<ResourcePackManager.PathUnit>();
		this.pathPool = new List<ResourcePackManager.PathUnit>();
		this.pathTmp = new List<Vector3>();
		this.pathSeen = new HashSet<Labor>();
		this.pathSelected = new HashSet<Labor>();
		this.trails = new Dictionary<Labor, ResourcePackManager.TrailData>();
		this.trailDrop = new List<Labor>();
		this.pathCamFrame = -1;
		this.predQueue = new List<ResourcePackManager.PredReq>();
		this.predRoutes = new Dictionary<Labor, ResourcePackManager.PredRoute>();
		this.predDrop = new List<Labor>();
		this.predTmp = new List<Vector3>();
		this.predDiag = "waiting for an order";
		this.hitTracks = new Dictionary<Transform, ResourcePackManager.HitTrack>();
		this.hitSeen = new HashSet<Transform>();
		this.hitDrop = new List<Transform>();
		this.hitLearn = new Dictionary<string, float>();
		this.hitFloats = new List<ResourcePackManager.HitFloat>();
		this.crystalTracks = new List<ResourcePackManager.CrystalTrack>();
		this.centerCache = new Dictionary<Transform, ResourcePackManager.CenterEnt>();
		this.centerShown = new List<ResourcePackManager.CenterEnt>();
		this.centerSeen = new HashSet<Transform>();
		this.distFloats = new List<ResourcePackManager.DistFloat>();
		this.expSeen = new HashSet<int>();
		this.expNow = new HashSet<int>();
		this.bombBarrels = new List<ResourcePackManager.BombBarrel>();
		this.bombShown = new List<ResourcePackManager.BombGroup>();
		this.bombBuilding = null;
		this.bombGlobalSig = int.MinValue;
		this.bombMerge = new Dictionary<long, ResourcePackManager.BombCell>();
		this.bombPrefabs = new Dictionary<string, ExplosionGameplay>();
		this.dayBright = 1f;
		this.nightBright = 1f;
		this.lightSaved = new float[0];
		this.brightFrame = -1f;
		this.brightMul = 1f;
		this.enemyTexByName = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
		this.enemyTexStamp = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
		this.enemyMats = new List<ResourcePackManager.EnemyMat>();
		this.enemyMatIds = new HashSet<int>();
		this.enemyMatSeen = new HashSet<int>();
		this.enemyRendTmp = new List<Renderer>();
		this.bcContentH = 400f;
		this.dbgContentH = 300f;
	}

	private void ShowMessage(string text)
	{
		this.message = text;
		this.messageStart = Time.unscaledTime;
	}

	private void DumpTextures()
	{
		try
		{
			string dumpDir = BtcPaths.DumpDir;
			string text = System.IO.Path.Combine(dumpDir, "texture");
			string path = System.IO.Path.Combine(dumpDir, "manifest.txt");
			Directory.CreateDirectory(text);
			List<ResourcePackManager.ManifestEntry> list = ResourcePackManager.LoadManifest(path);
			Texture2D[] array = Resources.FindObjectsOfTypeAll<Texture2D>();
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("[Texture2D] count=" + array.Length + "   (name | size | format | mips | signature | file)");
			Dictionary<string, List<Texture2D>> dictionary = new Dictionary<string, List<Texture2D>>();
			List<string> list2 = new List<string>();
			foreach (Texture2D texture2D in array)
			{
				bool flag = texture2D == null;
				bool flag2 = !flag;
				bool flag3 = flag2;
				bool flag4 = flag3;
				bool flag5 = flag4;
				bool flag6 = flag5;
				bool flag7 = flag6;
				bool flag8 = flag7;
				if (flag8)
				{
					string name = texture2D.name;
					bool flag9 = string.IsNullOrEmpty(name) || texture2D.width <= 0 || texture2D.height <= 0 || texture2D.width > 4096 || texture2D.height > 4096;
					bool flag10 = flag9;
					bool flag11 = flag10;
					bool flag12 = flag11;
					bool flag13 = flag12;
					bool flag14 = flag13;
					bool flag15 = flag14;
					bool flag16 = flag15;
					if (flag16)
					{
						stringBuilder.AppendLine(string.Concat(new object[]
						{
							name,
							" | ",
							texture2D.width,
							"x",
							texture2D.height,
							" | ",
							texture2D.format,
							" | ",
							texture2D.mipmapCount,
							" | - | -"
						}));
					}
					else
					{
						List<Texture2D> list3;
						bool flag17 = !dictionary.TryGetValue(name, out list3);
						bool flag18 = flag17;
						bool flag19 = flag18;
						bool flag20 = flag19;
						bool flag21 = flag20;
						bool flag22 = flag21;
						bool flag23 = flag22;
						bool flag24 = flag23;
						if (flag24)
						{
							list3 = new List<Texture2D>();
							dictionary[name] = list3;
							list2.Add(name);
						}
						list3.Add(texture2D);
					}
				}
			}
			int num = 0;
			int num2 = 0;
			for (int j = 0; j < list2.Count; j++)
			{
				string text2 = list2[j];
				List<Texture2D> list4 = dictionary[text2];
				string[] array3 = new string[list4.Count];
				Dictionary<string, ResourcePackManager.Variant> dictionary2 = new Dictionary<string, ResourcePackManager.Variant>();
				int k = 0;
				while (k < list4.Count)
				{
					Texture2D texture2D2 = list4[k];
					string text3 = null;
					byte[] png = null;
					Texture2D texture2D3 = null;
					try
					{
						ResourcePackManager.Entry entry;
						bool flag25 = this.entries.TryGetValue(texture2D2.GetInstanceID(), out entry) && entry.original != null && entry.sig != null;
						bool flag26 = flag25;
						bool flag27 = flag26;
						bool flag28 = flag27;
						bool flag29 = flag28;
						bool flag30 = flag29;
						bool flag31 = flag30;
						bool flag32 = flag31;
						if (flag32)
						{
							text3 = entry.sig;
							png = entry.original;
						}
						else
						{
							texture2D3 = ResourcePackManager.ReadBack(texture2D2);
							text3 = ResourcePackManager.Signature(texture2D3);
						}
					}
					catch (Exception ex)
					{
						ResourcePackManager.PackWarn("[ResourcePacks] Dump: cannot read '" + text2 + "': " + ex.Message);
						bool flag33 = texture2D3 != null;
						bool flag34 = flag33;
						bool flag35 = flag34;
						bool flag36 = flag35;
						bool flag37 = flag36;
						bool flag38 = flag37;
						bool flag39 = flag38;
						bool flag40 = flag39;
						if (flag40)
						{
							UnityEngine.Object.Destroy(texture2D3);
						}
						goto IL_336;
					}
					goto IL_334;
					IL_336:
					k++;
					continue;
					IL_334:
					array3[k] = text3;
					bool flag41 = dictionary2.ContainsKey(text3);
					bool flag42 = flag41;
					bool flag43 = flag42;
					bool flag44 = flag43;
					bool flag45 = flag44;
					bool flag46 = flag45;
					bool flag47 = flag46;
					bool flag48 = flag47;
					if (flag48)
					{
						bool flag49 = texture2D3 != null;
						bool flag50 = flag49;
						bool flag51 = flag50;
						bool flag52 = flag51;
						bool flag53 = flag52;
						bool flag54 = flag53;
						bool flag55 = flag54;
						bool flag56 = flag55;
						if (flag56)
						{
							UnityEngine.Object.Destroy(texture2D3);
						}
					}
					else
					{
						dictionary2[text3] = new ResourcePackManager.Variant
						{
							copy = texture2D3,
							png = png
						};
					}
					goto IL_336;
				}
				List<ResourcePackManager.ManifestEntry> list5 = new List<ResourcePackManager.ManifestEntry>();
				for (int l = 0; l < list.Count; l++)
				{
					bool flag57 = list[l].name == text2;
					bool flag58 = flag57;
					bool flag59 = flag58;
					bool flag60 = flag59;
					bool flag61 = flag60;
					bool flag62 = flag61;
					bool flag63 = flag62;
					bool flag64 = flag63;
					if (flag64)
					{
						list5.Add(list[l]);
					}
				}
				HashSet<string> hashSet = new HashSet<string>();
				for (int m = 0; m < list5.Count; m++)
				{
					hashSet.Add(list5[m].sig);
				}
				foreach (KeyValuePair<string, ResourcePackManager.Variant> keyValuePair in dictionary2)
				{
					hashSet.Add(keyValuePair.Key);
				}
				bool flag65 = hashSet.Count <= 1;
				bool flag66 = !flag65;
				bool flag67 = flag66;
				bool flag68 = flag67;
				bool flag69 = flag68;
				bool flag70 = flag69;
				bool flag71 = flag70;
				bool flag72 = flag71;
				bool flag73 = flag72;
				if (flag73)
				{
					num2++;
				}
				string text4 = ResourcePackManager.SafeFileName(text2);
				bool flag74 = !flag65;
				bool flag75 = flag74;
				bool flag76 = flag75;
				bool flag77 = flag76;
				bool flag78 = flag77;
				bool flag79 = flag78;
				bool flag80 = flag79;
				bool flag81 = flag80;
				if (flag81)
				{
					for (int n = 0; n < list5.Count; n++)
					{
						bool flag82 = list5[n].file == text4;
						bool flag83 = flag82;
						bool flag84 = flag83;
						bool flag85 = flag84;
						bool flag86 = flag85;
						bool flag87 = flag86;
						bool flag88 = flag87;
						bool flag89 = flag88;
						if (flag89)
						{
							string text5 = text4 + "__" + list5[n].sig;
							ResourcePackManager.MoveDumpFile(text, list5[n].file, text5);
							list5[n].file = text5;
						}
					}
				}
				foreach (KeyValuePair<string, ResourcePackManager.Variant> keyValuePair2 in dictionary2)
				{
					string file = flag65 ? text4 : (text4 + "__" + keyValuePair2.Key);
					ResourcePackManager.ManifestEntry manifestEntry = ResourcePackManager.FindEntry(list5, keyValuePair2.Key);
					bool flag90 = manifestEntry != null;
					bool flag91 = flag90;
					bool flag92 = flag91;
					bool flag93 = flag92;
					bool flag94 = flag93;
					bool flag95 = flag94;
					bool flag96 = flag95;
					bool flag97 = flag96;
					if (flag97)
					{
						bool flag98 = File.Exists(System.IO.Path.Combine(text, manifestEntry.file + ".png"));
						bool flag99 = flag98;
						bool flag100 = flag99;
						bool flag101 = flag100;
						bool flag102 = flag101;
						bool flag103 = flag102;
						bool flag104 = flag103;
						bool flag105 = flag104;
						if (flag105)
						{
							continue;
						}
						manifestEntry.file = file;
					}
					else
					{
						manifestEntry = new ResourcePackManager.ManifestEntry();
						manifestEntry.name = text2;
						manifestEntry.sig = keyValuePair2.Key;
						manifestEntry.file = file;
						list.Add(manifestEntry);
						list5.Add(manifestEntry);
					}
					byte[] bytes = (keyValuePair2.Value.png != null) ? keyValuePair2.Value.png : keyValuePair2.Value.copy.EncodeToPNG();
					File.WriteAllBytes(System.IO.Path.Combine(text, manifestEntry.file + ".png"), bytes);
					num++;
				}
				foreach (KeyValuePair<string, ResourcePackManager.Variant> keyValuePair3 in dictionary2)
				{
					bool flag106 = keyValuePair3.Value.copy != null;
					bool flag107 = flag106;
					bool flag108 = flag107;
					bool flag109 = flag108;
					bool flag110 = flag109;
					bool flag111 = flag110;
					bool flag112 = flag111;
					bool flag113 = flag112;
					if (flag113)
					{
						UnityEngine.Object.Destroy(keyValuePair3.Value.copy);
					}
				}
				for (int num3 = 0; num3 < list4.Count; num3++)
				{
					Texture2D texture2D4 = list4[num3];
					string text6 = "-";
					bool flag114 = array3[num3] != null;
					bool flag115 = flag114;
					bool flag116 = flag115;
					bool flag117 = flag116;
					bool flag118 = flag117;
					bool flag119 = flag118;
					bool flag120 = flag119;
					bool flag121 = flag120;
					if (flag121)
					{
						ResourcePackManager.ManifestEntry manifestEntry2 = ResourcePackManager.FindEntry(list5, array3[num3]);
						bool flag122 = manifestEntry2 != null;
						bool flag123 = flag122;
						bool flag124 = flag123;
						bool flag125 = flag124;
						bool flag126 = flag125;
						bool flag127 = flag126;
						bool flag128 = flag127;
						bool flag129 = flag128;
						if (flag129)
						{
							text6 = manifestEntry2.file + ".png";
						}
					}
					stringBuilder.AppendLine(string.Concat(new object[]
					{
						text2,
						" | ",
						texture2D4.width,
						"x",
						texture2D4.height,
						" | ",
						texture2D4.format,
						" | ",
						texture2D4.mipmapCount,
						" | ",
						array3[num3] ?? "-",
						" | ",
						text6
					}));
				}
			}
			ResourcePackManager.SaveManifest(path, list);
			Sprite[] array4 = Resources.FindObjectsOfTypeAll<Sprite>();
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("[Sprite] count=" + array4.Length + "   (sprite name | texture name | texture rect)");
			for (int num4 = 0; num4 < array4.Length; num4++)
			{
				bool flag130 = array4[num4] == null;
				bool flag131 = !flag130;
				bool flag132 = flag131;
				bool flag133 = flag132;
				bool flag134 = flag133;
				bool flag135 = flag134;
				bool flag136 = flag135;
				bool flag137 = flag136;
				if (flag137)
				{
					Texture texture = array4[num4].texture;
					stringBuilder.AppendLine(string.Concat(new object[]
					{
						array4[num4].name,
						" | ",
						(texture != null) ? texture.name : "null",
						" | ",
						array4[num4].textureRect
					}));
				}
			}
			Texture[] array5 = Resources.FindObjectsOfTypeAll<Texture>();
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("[Other texture types] (type | name | size)");
			for (int num5 = 0; num5 < array5.Length; num5++)
			{
				bool flag138 = array5[num5] != null && !(array5[num5] is Texture2D);
				bool flag139 = flag138;
				bool flag140 = flag139;
				bool flag141 = flag140;
				bool flag142 = flag141;
				bool flag143 = flag142;
				bool flag144 = flag143;
				bool flag145 = flag144;
				if (flag145)
				{
					stringBuilder.AppendLine(string.Concat(new object[]
					{
						array5[num5].GetType().Name,
						" | ",
						array5[num5].name,
						" | ",
						array5[num5].width,
						"x",
						array5[num5].height
					}));
				}
			}
			File.WriteAllText(System.IO.Path.Combine(dumpDir, "list.txt"), stringBuilder.ToString());
			ResourcePackManager.PackLog(string.Concat(new object[]
			{
				"[ResourcePacks] Dump done: ",
				dumpDir,
				", new files: ",
				num,
				", names with several variants: ",
				num2
			}));
			this.ShowMessage(string.Concat(new object[]
			{
				"Dump: ",
				num,
				" new files\n",
				num2,
				" names with several variants"
			}));
		}
		catch (Exception arg)
		{
			ResourcePackManager.PackWarn("[ResourcePacks] Dump failed: " + arg);
			this.ShowMessage("Dump failed - see log");
		}
	}

	private static void WriteLogLine(string text)
	{
		try
		{
			File.AppendAllText(ResourcePackManager.LogPath, DateTime.Now.ToString("HH:mm:ss") + "  " + text + "\r\n");
		}
		catch (Exception)
		{
		}
	}

	private static void PackLog(string text)
	{
		Debug.Log(text);
		ResourcePackManager.WriteLogLine(text);
	}

	private static void PackWarn(string text)
	{
		Debug.LogWarning(text);
		ResourcePackManager.WriteLogLine("WARNING: " + text);
	}

	private static string LogPath
	{
		get
		{
			return BtcPaths.DebugFile("mod_log.txt");
		}
	}

	private static List<string> ListPacks(bool includeVanilla)
	{
		List<string> list = new List<string>();
		try
		{
			bool flag = Directory.Exists(ResourcePackManager.RootDir);
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				string[] directories = Directory.GetDirectories(ResourcePackManager.RootDir);
				Array.Sort<string>(directories, StringComparer.OrdinalIgnoreCase);
				for (int i = 0; i < directories.Length; i++)
				{
					string fileName = System.IO.Path.GetFileName(directories[i]);
					bool flag9 = string.Equals(fileName, "Vanilla", StringComparison.OrdinalIgnoreCase);
					bool flag10 = !fileName.StartsWith("_") && (includeVanilla || !flag9);
					bool flag11 = flag10;
					bool flag12 = flag11;
					bool flag13 = flag12;
					bool flag14 = flag13;
					bool flag15 = flag14;
					bool flag16 = flag15;
					bool flag17 = flag16;
					if (flag17)
					{
						list.Add(fileName);
					}
				}
			}
		}
		catch (Exception ex)
		{
			ResourcePackManager.PackWarn("[ResourcePacks] Cannot list packs: " + ex.Message);
		}
		return list;
	}

	private static bool ParseSig(string sig, out int w, out int h, out int[] nibbles)
	{
		w = 0;
		h = 0;
		nibbles = null;
		int num = sig.IndexOf('x');
		int num2 = sig.IndexOf('-');
		bool flag = num <= 0 || num2 <= num + 1 || sig.Length - num2 - 1 != 12;
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
			bool flag9 = !int.TryParse(sig.Substring(0, num), out w) || !int.TryParse(sig.Substring(num + 1, num2 - num - 1), out h);
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
				int[] array = new int[12];
				for (int i = 0; i < 12; i++)
				{
					char c = sig[num2 + 1 + i];
					bool flag17 = c >= '0' && c <= '9';
					bool flag18 = flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					bool flag21 = flag20;
					bool flag22 = flag21;
					bool flag23 = flag22;
					bool flag24 = flag23;
					int num3;
					if (flag24)
					{
						num3 = (int)(c - '0');
					}
					else
					{
						bool flag25 = c >= 'a' && c <= 'f';
						bool flag26 = flag25;
						bool flag27 = flag26;
						bool flag28 = flag27;
						bool flag29 = flag28;
						bool flag30 = flag29;
						bool flag31 = flag30;
						bool flag32 = flag31;
						if (flag32)
						{
							num3 = (int)(c - 'a' + '\n');
						}
						else
						{
							bool flag33 = c >= 'A' && c <= 'F';
							bool flag34 = !flag33;
							bool flag35 = flag34;
							bool flag36 = flag35;
							bool flag37 = flag36;
							bool flag38 = flag37;
							bool flag39 = flag38;
							bool flag40 = flag39;
							if (flag40)
							{
								return false;
							}
							num3 = (int)(c - 'A' + '\n');
						}
					}
					array[i] = num3;
				}
				nibbles = array;
				result = true;
			}
		}
		return result;
	}
}
}
