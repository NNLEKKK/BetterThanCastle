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

	private static void FinishHpConfig(ResourcePackManager.HpConfig cfg)
	{
		ResourcePackManager.RebuildHpPalettes(cfg);
		cfg.allOv.Clear();
		List<ResourcePackManager.HpOverride>[] array = new List<ResourcePackManager.HpOverride>[]
		{
			cfg.ownOv,
			cfg.enemyOv
		};
		for (int i = 0; i < array.Length; i++)
		{
			for (int j = 0; j < array[i].Count; j++)
			{
				ResourcePackManager.HpOverride hpOverride = array[i][j];
				int num = -1;
				for (int k = 0; k < cfg.allOv.Count; k++)
				{
					bool flag = cfg.allOv[k].id == hpOverride.id;
					bool flag2 = flag;
					bool flag3 = flag2;
					bool flag4 = flag3;
					bool flag5 = flag4;
					bool flag6 = flag5;
					bool flag7 = flag6;
					bool flag8 = flag7;
					if (flag8)
					{
						num = k;
						break;
					}
				}
				bool flag9 = num < 0;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					num = cfg.allOv.Count;
					cfg.allOv.Add(hpOverride);
				}
				hpOverride.slot = num;
			}
		}
		cfg.version = ++ResourcePackManager.hpCfgCounter;
		cfg.enemyShortHash = Animator.StringToHash(cfg.enemyState);
		cfg.enemyFullHash = Animator.StringToHash(cfg.layerName + "." + cfg.enemyState);
	}

	private void ReloadHpConfig(bool announce)
	{
		List<string> list = new List<string>();
		ResourcePackManager.HpConfig hpConfig = new ResourcePackManager.HpConfig();
		try
		{
			ResourcePackManager.ApplyHpConfigLines(ResourcePackManager.DefaultHpConfig, hpConfig, new List<string>());
			string hpConfigPath = ResourcePackManager.HpConfigPath;
			bool flag = !File.Exists(hpConfigPath);
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				File.WriteAllLines(hpConfigPath, ResourcePackManager.DefaultHpConfig, Encoding.UTF8);
			}
			else
			{
				ResourcePackManager.ApplyHpConfigLines(File.ReadAllLines(hpConfigPath), hpConfig, list);
			}
			this.hpCfgStamp = File.GetLastWriteTimeUtc(hpConfigPath);
		}
		catch (Exception ex)
		{
			list.Add(ex.Message);
		}
		ResourcePackManager.FinishHpConfig(hpConfig);
		bool flag9 = this.hpCfg != null;
		bool flag10 = flag9;
		bool flag11 = flag10;
		bool flag12 = flag11;
		bool flag13 = flag12;
		bool flag14 = flag13;
		bool flag15 = flag14;
		bool flag16 = flag15;
		if (flag16)
		{
			this.RestoreHpRings();
		}
		this.hpCfg = hpConfig;
		this.pickStop = null;
		this.pickOv = null;
		this.pickNum = false;
		this.pickExtra = 0;
		this.hpSaveAt = 0f;
		for (int i = 0; i < this.hpRings.Count; i++)
		{
			this.hpRings[i].mainLayer = -2;
		}
		for (int j = 0; j < list.Count; j++)
		{
			ResourcePackManager.PackWarn("[ResourcePacks] coloredhp.txt: " + list[j]);
		}
		ResourcePackManager.PackLog(string.Concat(new object[]
		{
			"[ResourcePacks] Colored HP config loaded (",
			hpConfig.own.mins.Length,
			" own / ",
			hpConfig.enemy.mins.Length,
			" enemy colors",
			(list.Count > 0) ? (", " + list.Count + " problem(s) - see log") : "",
			")"
		}));
		if (announce)
		{
			this.ShowMessage((list.Count > 0) ? "coloredhp.txt reloaded with errors - see debug/mod_log.txt" : "coloredhp.txt reloaded");
		}
	}

	private bool IsEnemyRing(ResourcePackManager.HpRing ring)
	{
		bool flag = ring.anim.GetInteger(ResourcePackManager.hpMainColorId) == 2;
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
			result = true;
		}
		else
		{
			bool flag9 = ring.mainLayer == -2;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				ring.mainLayer = ring.anim.GetLayerIndex(this.hpCfg.layerName);
			}
			bool flag17 = ring.mainLayer < 0;
			bool flag18 = flag17;
			bool flag19 = flag18;
			bool flag20 = flag19;
			bool flag21 = flag20;
			bool flag22 = flag21;
			bool flag23 = flag22;
			bool flag24 = flag23;
			if (flag24)
			{
				result = false;
			}
			else
			{
				AnimatorStateInfo animatorStateInfo = ring.anim.GetCurrentAnimatorStateInfo(ring.mainLayer);
				bool flag25 = ring.anim.IsInTransition(ring.mainLayer);
				bool flag26 = flag25;
				bool flag27 = flag26;
				bool flag28 = flag27;
				bool flag29 = flag28;
				bool flag30 = flag29;
				bool flag31 = flag30;
				bool flag32 = flag31;
				if (flag32)
				{
					animatorStateInfo = ring.anim.GetNextAnimatorStateInfo(ring.mainLayer);
				}
				result = (animatorStateInfo.shortNameHash == this.hpCfg.enemyShortHash || animatorStateInfo.fullPathHash == this.hpCfg.enemyFullHash);
			}
		}
		return result;
	}

	private void PaintRing(ResourcePackManager.HpRing ring, float ratio)
	{
		ResourcePackManager.HpConfig hpConfig = this.hpCfg;
		bool flag = (hpConfig.enemyPalette || !hpConfig.colorEnemies) && this.IsEnemyRing(ring);
		bool flag2 = flag && !ring.enemyLogged;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		bool flag9 = flag8;
		if (flag9)
		{
			ring.enemyLogged = true;
			ResourcePackManager.PackLog("[ResourcePacks] Colored HP: enemy ring detected");
		}
		bool flag10 = ring.cfgVer != hpConfig.version;
		bool flag11 = flag10;
		bool flag12 = flag11;
		bool flag13 = flag12;
		bool flag14 = flag13;
		bool flag15 = flag14;
		bool flag16 = flag15;
		bool flag17 = flag16;
		if (flag17)
		{
			ResourcePackManager.PrepareRingOverrides(ring, hpConfig);
		}
		bool flag18 = flag && !hpConfig.colorEnemies;
		bool flag19 = flag18;
		bool flag20 = flag19;
		bool flag21 = flag20;
		bool flag22 = flag21;
		bool flag23 = flag22;
		bool flag24 = flag23;
		bool flag25 = flag24;
		if (flag25)
		{
			bool flag26 = ring.team != 1;
			bool flag27 = flag26;
			bool flag28 = flag27;
			bool flag29 = flag28;
			bool flag30 = flag29;
			bool flag31 = flag30;
			bool flag32 = flag31;
			bool flag33 = flag32;
			if (flag33)
			{
				bool flag34 = hpConfig.allOv.Count > 0;
				bool flag35 = flag34;
				bool flag36 = flag35;
				bool flag37 = flag36;
				bool flag38 = flag37;
				bool flag39 = flag38;
				bool flag40 = flag39;
				bool flag41 = flag40;
				if (flag41)
				{
					ring.rend.GetPropertyBlock(ring.block);
					ResourcePackManager.ResetRingOverrides(ring, hpConfig);
					ring.rend.SetPropertyBlock(ring.block);
				}
				ring.team = 1;
			}
		}
		else
		{
			ResourcePackManager.ResolveRingUnit(ring);
			ResourcePackManager.HpPalette hpPalette = (ring.building && hpConfig.bldColor) ? (flag ? hpConfig.bldEnemy : hpConfig.bldOwn) : (flag ? hpConfig.enemy : hpConfig.own);
			bool flag42 = hpPalette.mins.Length == 0;
			bool flag43 = !flag42;
			bool flag44 = flag43;
			bool flag45 = flag44;
			bool flag46 = flag45;
			bool flag47 = flag46;
			bool flag48 = flag47;
			bool flag49 = flag48;
			if (flag49)
			{
				int num = hpPalette.Index(ratio * 100f);
				ring.rend.GetPropertyBlock(ring.block);
				int num2 = flag ? 1 : 0;
				bool flag50 = hpConfig.allOv.Count > 0;
				bool flag51 = flag50;
				bool flag52 = flag51;
				bool flag53 = flag52;
				bool flag54 = flag53;
				bool flag55 = flag54;
				bool flag56 = flag55;
				bool flag57 = flag56;
				if (flag57)
				{
					bool flag58 = ring.team != num2;
					bool flag59 = flag58;
					bool flag60 = flag59;
					bool flag61 = flag60;
					bool flag62 = flag61;
					bool flag63 = flag62;
					bool flag64 = flag63;
					bool flag65 = flag64;
					if (flag65)
					{
						ResourcePackManager.ResetRingOverrides(ring, hpConfig);
						ring.team = num2;
					}
					List<ResourcePackManager.HpOverride> list = flag ? hpConfig.enemyOv : hpConfig.ownOv;
					for (int i = 0; i < list.Count; i++)
					{
						ResourcePackManager.HpOverride hpOverride = list[i];
						bool flag66 = !ring.ovHas[hpOverride.slot];
						bool flag67 = !flag66;
						bool flag68 = flag67;
						bool flag69 = flag68;
						bool flag70 = flag69;
						bool flag71 = flag70;
						bool flag72 = flag71;
						bool flag73 = flag72;
						if (flag73)
						{
							bool color = hpOverride.color;
							bool flag74 = color;
							bool flag75 = flag74;
							bool flag76 = flag75;
							bool flag77 = flag76;
							bool flag78 = flag77;
							bool flag79 = flag78;
							bool flag80 = flag79;
							if (flag80)
							{
								ring.block.SetColor(hpOverride.id, hpOverride.c);
							}
							else
							{
								float num3 = hpOverride.f;
								bool mult = hpOverride.mult;
								bool flag81 = mult;
								bool flag82 = flag81;
								bool flag83 = flag82;
								bool flag84 = flag83;
								bool flag85 = flag84;
								bool flag86 = flag85;
								bool flag87 = flag86;
								if (flag87)
								{
									float @float = ring.block.GetFloat(hpOverride.id);
									float num4 = ring.ovLastF[hpOverride.slot];
									bool flag88 = float.IsNaN(num4);
									bool flag89 = flag88;
									bool flag90 = flag89;
									bool flag91 = flag90;
									bool flag92 = flag91;
									bool flag93 = flag92;
									bool flag94 = flag93;
									bool flag95 = flag94;
									if (flag95)
									{
										ring.ovRawF[hpOverride.slot] = ring.ovBaseF[hpOverride.slot];
									}
									else
									{
										bool flag96 = !Mathf.Approximately(@float, num4);
										bool flag97 = flag96;
										bool flag98 = flag97;
										bool flag99 = flag98;
										bool flag100 = flag99;
										bool flag101 = flag100;
										bool flag102 = flag101;
										bool flag103 = flag102;
										if (flag103)
										{
											ring.ovRawF[hpOverride.slot] = @float;
										}
									}
									num3 = ring.ovRawF[hpOverride.slot] * hpOverride.f;
									ring.ovLastF[hpOverride.slot] = num3;
								}
								ring.block.SetFloat(hpOverride.id, num3);
							}
						}
					}
				}
				ResourcePackManager.ResolveRingUnit(ring);
				float num5 = (hpConfig.selShiftOn && !flag && ring.anim.GetBool(ResourcePackManager.hpSelectedId)) ? hpConfig.selShift : 0f;
				bool flag104 = hpConfig.rings[0];
				bool flag105 = flag104;
				bool flag106 = flag105;
				bool flag107 = flag106;
				bool flag108 = flag107;
				bool flag109 = flag108;
				bool flag110 = flag109;
				bool flag111 = flag110;
				if (flag111)
				{
					Color color2 = hpPalette.main[num];
					Color color3 = hpPalette.second[num];
					bool flag112 = num5 != 0f;
					bool flag113 = flag112;
					bool flag114 = flag113;
					bool flag115 = flag114;
					bool flag116 = flag115;
					bool flag117 = flag116;
					bool flag118 = flag117;
					bool flag119 = flag118;
					if (flag119)
					{
						color2 = ResourcePackManager.ShiftColor(color2, num5);
						color3 = ResourcePackManager.ShiftColor(color3, num5);
					}
					ring.block.SetColor(ResourcePackManager.ringPropIds[0], color2);
					ring.block.SetColor(ResourcePackManager.ringPropIds[1], color3);
				}
				ResourcePackManager.HpPalette hpPalette2 = null;
				bool flag120 = ring.labor != null && ResourcePackManager.IsJob(ring.labor, Occupation.Job.Alchemist);
				bool flag121 = flag120;
				bool flag122 = flag121;
				bool flag123 = flag122;
				bool flag124 = flag123;
				bool flag125 = flag124;
				bool flag126 = flag125;
				bool flag127 = flag126;
				if (flag127)
				{
					bool ammoColor = hpConfig.ammoColor;
					bool flag128 = ammoColor;
					bool flag129 = flag128;
					bool flag130 = flag129;
					bool flag131 = flag130;
					bool flag132 = flag131;
					bool flag133 = flag132;
					bool flag134 = flag133;
					if (flag134)
					{
						hpPalette2 = hpConfig.ammo;
					}
				}
				else
				{
					bool staminaColor = hpConfig.staminaColor;
					bool flag135 = staminaColor;
					bool flag136 = flag135;
					bool flag137 = flag136;
					bool flag138 = flag137;
					bool flag139 = flag138;
					bool flag140 = flag139;
					bool flag141 = flag140;
					if (flag141)
					{
						hpPalette2 = (flag ? (hpConfig.enemyPalette ? hpConfig.stamEnemy : hpConfig.stamOwn) : hpConfig.stamOwn);
					}
				}
				bool @bool = ring.anim.GetBool(ResourcePackManager.hpShowStamId);
				bool flag142 = hpPalette2 != null && hpPalette2.mins.Length != 0 && @bool;
				bool flag143 = flag142;
				bool flag144 = flag143;
				bool flag145 = flag144;
				bool flag146 = flag145;
				bool flag147 = flag146;
				bool flag148 = flag147;
				bool flag149 = flag148;
				if (flag149)
				{
					ring.ring2Painted = true;
					int num6 = hpPalette2.Index(ring.anim.GetFloat(ResourcePackManager.hpStamId) * 100f);
					Color color4 = hpPalette2.main[num6];
					Color color5 = hpPalette2.second[num6];
					bool flag150 = num5 != 0f;
					bool flag151 = flag150;
					bool flag152 = flag151;
					bool flag153 = flag152;
					bool flag154 = flag153;
					bool flag155 = flag154;
					bool flag156 = flag155;
					bool flag157 = flag156;
					if (flag157)
					{
						color4 = ResourcePackManager.ShiftColor(color4, num5);
						color5 = ResourcePackManager.ShiftColor(color5, num5);
					}
					ring.block.SetColor(ResourcePackManager.ringPropIds[2], color4);
					ring.block.SetColor(ResourcePackManager.ringPropIds[3], color5);
				}
				else
				{
					bool ring2Painted = ring.ring2Painted;
					bool flag158 = ring2Painted;
					bool flag159 = flag158;
					bool flag160 = flag159;
					bool flag161 = flag160;
					bool flag162 = flag161;
					bool flag163 = flag162;
					bool flag164 = flag163;
					if (flag164)
					{
						ring.ring2Painted = false;
						ring.block.SetColor(ResourcePackManager.ringPropIds[2], ring.originals[2]);
						ring.block.SetColor(ResourcePackManager.ringPropIds[3], ring.originals[3]);
					}
				}
				ring.rend.SetPropertyBlock(ring.block);
			}
		}
	}

	private static string HpConfigPath
	{
		get
		{
			return BtcPaths.Config("coloredhp.txt");
		}
	}

	private static ResourcePackManager.HpOverride ParseHpOverride(string name, string val)
	{
		bool flag = !name.StartsWith("_", StringComparison.Ordinal) || name.IndexOf(' ') >= 0 || val.Length == 0;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		ResourcePackManager.HpOverride result;
		if (flag8)
		{
			result = null;
		}
		else
		{
			ResourcePackManager.HpOverride hpOverride = new ResourcePackManager.HpOverride();
			hpOverride.name = name;
			hpOverride.id = Shader.PropertyToID(name);
			int num = val.Split(new char[]
			{
				','
			}).Length - 1;
			bool flag9 = val[0] == '#' || num >= 2;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				hpOverride.color = ResourcePackManager.TryParseHpColor(val, out hpOverride.c);
				result = (hpOverride.color ? hpOverride : null);
			}
			else
			{
				bool flag17 = val[0] == 'x' || val[0] == 'X';
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				if (flag24)
				{
					hpOverride.mult = true;
					val = val.Substring(1);
				}
				float f;
				bool flag25 = !float.TryParse(val.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out f);
				bool flag26 = flag25;
				bool flag27 = flag26;
				bool flag28 = flag27;
				bool flag29 = flag28;
				bool flag30 = flag29;
				bool flag31 = flag30;
				bool flag32 = flag31;
				if (flag32)
				{
					result = null;
				}
				else
				{
					hpOverride.f = f;
					result = hpOverride;
				}
			}
		}
		return result;
	}

	private static void PrepareRingOverrides(ResourcePackManager.HpRing ring, ResourcePackManager.HpConfig cfg)
	{
		int count = cfg.allOv.Count;
		ring.ovHas = new bool[count];
		ring.ovBaseF = new float[count];
		ring.ovBaseC = new Color[count];
		ring.ovLastF = new float[count];
		ring.ovRawF = new float[count];
		for (int i = 0; i < count; i++)
		{
			ring.ovLastF[i] = float.NaN;
		}
		Material sharedMaterial = ring.rend.sharedMaterial;
		for (int j = 0; j < count; j++)
		{
			ResourcePackManager.HpOverride hpOverride = cfg.allOv[j];
			ring.ovHas[j] = (sharedMaterial != null && sharedMaterial.HasProperty(hpOverride.id));
			bool flag = ring.ovHas[j];
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				bool color = hpOverride.color;
				bool flag9 = color;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				if (flag15)
				{
					ring.ovBaseC[j] = sharedMaterial.GetColor(hpOverride.id);
				}
				else
				{
					ring.ovBaseF[j] = sharedMaterial.GetFloat(hpOverride.id);
				}
			}
		}
		ring.cfgVer = cfg.version;
		ring.team = -1;
	}

	private static void ResetRingOverrides(ResourcePackManager.HpRing ring, ResourcePackManager.HpConfig cfg)
	{
		bool flag = ring.cfgVer != cfg.version || ring.ovHas == null;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			for (int i = 0; i < cfg.allOv.Count; i++)
			{
				bool flag9 = !ring.ovHas[i];
				bool flag10 = !flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					bool color = cfg.allOv[i].color;
					bool flag17 = color;
					bool flag18 = flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					bool flag21 = flag20;
					bool flag22 = flag21;
					bool flag23 = flag22;
					if (flag23)
					{
						ring.block.SetColor(cfg.allOv[i].id, ring.ovBaseC[i]);
					}
					else
					{
						ring.ovLastF[i] = float.NaN;
						ring.block.SetFloat(cfg.allOv[i].id, ring.ovBaseF[i]);
					}
				}
			}
		}
	}

	private static List<ResourcePackManager.HpStop> ToHpStops(List<KeyValuePair<float, Color>> list)
	{
		List<ResourcePackManager.HpStop> list2 = new List<ResourcePackManager.HpStop>();
		for (int i = 0; i < list.Count; i++)
		{
			list2.Add(new ResourcePackManager.HpStop
			{
				pct = list[i].Key,
				col = list[i].Value
			});
		}
		ResourcePackManager.SortHpStops(list2);
		return list2;
	}

	private static void SortHpStops(List<ResourcePackManager.HpStop> stops)
	{
		stops.Sort((ResourcePackManager.HpStop x, ResourcePackManager.HpStop y) => y.pct.CompareTo(x.pct));
	}

	private static void RebuildHpPalettes(ResourcePackManager.HpConfig cfg)
	{
		bool flag = cfg.bldOwnStops.Count == 0;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			cfg.bldOwnStops = ResourcePackManager.CloneHpStops(cfg.ownStops);
		}
		bool flag9 = cfg.bldEnemyStops.Count == 0;
		bool flag10 = flag9;
		bool flag11 = flag10;
		bool flag12 = flag11;
		bool flag13 = flag12;
		bool flag14 = flag13;
		bool flag15 = flag14;
		bool flag16 = flag15;
		if (flag16)
		{
			cfg.bldEnemyStops = ResourcePackManager.CloneHpStops(cfg.enemyStops);
		}
		cfg.bldOwn = ResourcePackManager.BuildHpPalette(cfg.bldOwnStops, cfg.bldSecondBrightness);
		cfg.bldEnemy = ResourcePackManager.BuildHpPalette(cfg.bldEnemyStops, cfg.bldSecondBrightness);
		cfg.own = ResourcePackManager.BuildHpPalette(cfg.ownStops, cfg.secondBrightness);
		cfg.enemy = ResourcePackManager.BuildHpPalette(cfg.enemyStops, cfg.secondBrightness);
		cfg.stamOwn = ResourcePackManager.BuildHpPalette(cfg.stamOwnStops, cfg.stamSecondBrightness);
		cfg.stamEnemy = ResourcePackManager.BuildHpPalette(cfg.stamEnemyStops, cfg.stamSecondBrightness);
		cfg.ammo = ResourcePackManager.BuildHpPalette(cfg.ammoStops, cfg.ammoSecondBrightness);
	}

	private static string HpHex(Color c)
	{
		string text = "#" + Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f).ToString("X2") + Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255f).ToString("X2") + Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255f).ToString("X2");
		bool flag = c.a < 0.996f;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			text += Mathf.RoundToInt(Mathf.Clamp01(c.a) * 255f).ToString("X2");
		}
		return text;
	}

	private static string HpNum(float f)
	{
		return f.ToString("0.###", CultureInfo.InvariantCulture);
	}

	private static List<string> SerializeHpConfig(ResourcePackManager.HpConfig c)
	{
		List<string> list = new List<string>();
		list.Add("# Настройки Colored HP. Их можно менять в игре (меню -> Colored HP) или вручную в этом файле; файл перечитывается на лету.");
		list.Add("# Цвет: #RRGGBB или R,G,B (0-255). Палитра: строка \"процент = цвет\" - цвет действует, когда HP юнита >= процента.");
		list.Add("# Переопределения свойств шейдера: число, xN (умножить исходное значение) или цвет. Файл при сохранении из меню переписывается целиком.");
		list.Add("");
		list.Add("[General]");
		list.Add("SecondColorBrightness = " + ResourcePackManager.HpNum(c.secondBrightness));
		list.Add("Rings = " + (c.rings[0] ? "1" : "none"));
		list.Add("ColorEnemies = " + (c.colorEnemies ? "true" : "false"));
		list.Add("AlwaysShowOwn = " + (c.alwaysOwn ? "true" : "false"));
		list.Add("AlwaysShowEnemy = " + (c.alwaysEnemy ? "true" : "false"));
		list.Add("EnemyPalette = " + (c.enemyPalette ? "true" : "false"));
		list.Add("StaminaColor = " + (c.staminaColor ? "true" : "false"));
		list.Add("AmmoColor = " + (c.ammoColor ? "true" : "false"));
		list.Add("StaminaSecondColorBrightness = " + ResourcePackManager.HpNum(c.stamSecondBrightness));
		list.Add("AmmoSecondColorBrightness = " + ResourcePackManager.HpNum(c.ammoSecondBrightness));
		list.Add("StaminaKnightOnly = " + (c.knightStaminaOnly ? "true" : "false"));
		list.Add("AmmoRing = " + (c.ammoRing ? "true" : "false"));
		list.Add("AmmoNumber = " + (c.ammoNumber ? "true" : "false"));
		list.Add("AmmoNumberSize = " + ResourcePackManager.HpNum(c.numSize));
		list.Add("AmmoNumberOffsetX = " + ResourcePackManager.HpNum(c.numX));
		list.Add("AmmoNumberOffsetY = " + ResourcePackManager.HpNum(c.numY));
		list.Add("AmmoNumberColor = " + ResourcePackManager.HpHex(c.numColor));
		list.Add("AmmoNumberColorByAmmo = " + (c.numByAmmo ? "true" : "false"));
		list.Add("AmmoNumberOutline = " + (c.numOutline ? "true" : "false"));
		list.Add("AmmoNumberBold = " + (c.numBold ? "true" : "false"));
		list.Add("SelectedShiftEnabled = " + (c.selShiftOn ? "true" : "false"));
		list.Add("SelectedShift = " + ResourcePackManager.HpNum(c.selShift));
		list.Add("HpNumberOwn = " + (c.hpNumOwn ? "true" : "false"));
		list.Add("HpNumberEnemy = " + (c.hpNumEnemy ? "true" : "false"));
		list.Add("StaminaNumberOwn = " + (c.stamNumOwn ? "true" : "false"));
		list.Add("StaminaNumberEnemy = " + (c.stamNumEnemy ? "true" : "false"));
		list.Add("StaminaNumberKnightOnly = " + (c.stamNumKnightOnly ? "true" : "false"));
		list.Add("StatNumberHideFull = " + (c.statHideFull ? "true" : "false"));
		list.Add("StatNumberFormat = " + c.statFormat);
		list.Add("StatNumberSize = " + ResourcePackManager.HpNum(c.statSize));
		list.Add("StatNumberOpacity = " + ResourcePackManager.HpNum(c.statAlpha));
		list.Add("StatNumberBold = " + (c.statBold ? "true" : "false"));
		list.Add("StatNumberOutline = " + (c.statOutline ? "true" : "false"));
		list.Add("HpNumberOffsetX = " + ResourcePackManager.HpNum(c.hpNumX));
		list.Add("HpNumberOffsetY = " + ResourcePackManager.HpNum(c.hpNumY));
		list.Add("HpNumberByPalette = " + (c.hpNumByPalette ? "true" : "false"));
		list.Add("HpNumberColor = " + ResourcePackManager.HpHex(c.hpNumColor));
		list.Add("StaminaNumberOffsetX = " + ResourcePackManager.HpNum(c.stamNumX));
		list.Add("StaminaNumberOffsetY = " + ResourcePackManager.HpNum(c.stamNumY));
		list.Add("StaminaNumberByPalette = " + (c.stamNumByPalette ? "true" : "false"));
		list.Add("StaminaNumberColor = " + ResourcePackManager.HpHex(c.stamNumColor));
		list.Add("PathOwn = " + (c.pathOwn ? "true" : "false"));
		list.Add("PathTrail = " + (c.pathTrail ? "true" : "false"));
		list.Add("PathPredict = " + (c.pathPredict ? "true" : "false"));
		list.Add("BombMap = " + (c.bombMap ? "true" : "false"));
		list.Add("BombMap3D = " + (c.bombMap3D ? "true" : "false"));
		list.Add("BombMapNumbers = " + (c.bombMapNumbers ? "true" : "false"));
		list.Add("BombMapOpacity = " + ResourcePackManager.HpNum(c.bombMapAlpha));
		list.Add("BombMapLayersV2 = " + c.bombMapLayers);
		list.Add("BombMapNumberOpacity = " + ResourcePackManager.HpNum(c.bombNumAlpha));
		list.Add("BombMapRange = " + c.bombMapRange);
		list.Add("BombMapColorMask = " + c.bombBandMask);
		list.Add("BombByDamage = " + (c.bombByDamage ? "true" : "false"));
		for (int i = 0; i < c.bombDmg.Length; i++)
		{
			list.Add(string.Concat(new object[]
			{
				"BombDamageStep",
				i + 1,
				" = ",
				ResourcePackManager.HpNum(c.bombDmg[i])
			}));
		}
		list.Add("BuildingColor = " + (c.bldColor ? "true" : "false"));
		list.Add("BuildingSecondColorBrightness = " + ResourcePackManager.HpNum(c.bldSecondBrightness));
		list.Add("BuildingHpNumberOwn = " + (c.bldHpNumOwn ? "true" : "false"));
		list.Add("BuildingHpNumberEnemy = " + (c.bldHpNumEnemy ? "true" : "false"));
		list.Add("BuildingRegenNumberOwn = " + (c.bldRegenOwn ? "true" : "false"));
		list.Add("BuildingRegenNumberEnemy = " + (c.bldRegenEnemy ? "true" : "false"));
		list.Add("BuildingHpNumberOffsetY = " + ResourcePackManager.HpNum(c.bldHpNumY));
		list.Add("BuildingRegenNumberOffsetY = " + ResourcePackManager.HpNum(c.bldRegenY));
		list.Add("RegenNumberOwn = " + (c.regenNumOwn ? "true" : "false"));
		list.Add("RegenNumberEnemy = " + (c.regenNumEnemy ? "true" : "false"));
		list.Add("RegenNumberOffsetY = " + ResourcePackManager.HpNum(c.regenNumY));
		list.Add("RegenNumberColor = " + ResourcePackManager.HpHex(c.regenNumColor));
		list.Add("BombMapNumberDistance = " + ResourcePackManager.HpNum(c.bombNumDist));
		list.Add("StatNumberHideFar = " + (c.statHideFar ? "true" : "false"));
		list.Add("StatNumberHideDistance = " + ResourcePackManager.HpNum(c.statHideDist));
		list.Add("HitOwn = " + (c.hitOwn ? "true" : "false"));
		list.Add("HitEnemy = " + (c.hitEnemy ? "true" : "false"));
		list.Add("HitDps = " + (c.hitDps ? "true" : "false"));
		list.Add("HitStaminaOwn = " + (c.hitStamOwn ? "true" : "false"));
		list.Add("HitStaminaEnemy = " + (c.hitStamEnemy ? "true" : "false"));
		list.Add("DpsIncludeStamina = " + (c.dpsStamina ? "true" : "false"));
		list.Add("HitBuildingOwn = " + (c.hitBldOwn ? "true" : "false"));
		list.Add("HitBuildingEnemy = " + (c.hitBldEnemy ? "true" : "false"));
		list.Add("HitBuildingDps = " + (c.hitBldDps ? "true" : "false"));
		list.Add("HitFullDamage = " + (c.hitFullDamage ? "true" : "false"));
		list.Add("MenuPause = " + (c.menuPause ? "true" : "false"));
		list.Add("MenuBlockInput = " + (c.menuBlockInput ? "true" : "false"));
		list.Add("CenterOn = " + (c.centerOn ? "true" : "false"));
		list.Add("CenterDistance = " + (c.centerDist ? "true" : "false"));
		list.Add("CenterDistanceSize = " + ResourcePackManager.HpNum(c.centerDistSize));
		list.Add("CenterDistanceTime = " + ResourcePackManager.HpNum(c.centerDistTime));
		list.Add("CenterOwnUnits = " + (c.centerOwnUnits ? "true" : "false"));
		list.Add("CenterEnemyUnits = " + (c.centerEnemyUnits ? "true" : "false"));
		list.Add("CenterOwnObjects = " + (c.centerOwnObjects ? "true" : "false"));
		list.Add("CenterEnemyObjects = " + (c.centerEnemyObjects ? "true" : "false"));
		list.Add("CenterSize = " + ResourcePackManager.HpNum(c.centerSize));
		list.Add("CenterOpacity = " + ResourcePackManager.HpNum(c.centerAlpha));
		list.Add("CenterOwnColor = " + ResourcePackManager.HpHex(c.centerOwnColor));
		list.Add("CenterEnemyColor = " + ResourcePackManager.HpHex(c.centerEnemyColor));
		list.Add("AltOnly = " + (c.altOnly ? "true" : "false"));
		list.Add("AltNumbers = " + (c.altNumbers ? "true" : "false"));
		list.Add("AltAmmo = " + (c.altAmmo ? "true" : "false"));
		list.Add("AltDamage = " + (c.altDamage ? "true" : "false"));
		list.Add("AltCrystal = " + (c.altCrystal ? "true" : "false"));
		list.Add("AltBombMap = " + (c.altBombMap ? "true" : "false"));
		list.Add("AltRings = " + (c.altRings ? "true" : "false"));
		list.Add("AltPaths = " + (c.altPaths ? "true" : "false"));
		list.Add("DpsPrecise = " + (c.dpsPrecise ? "true" : "false"));
		list.Add("PerfMode = " + (c.perfMode ? "true" : "false"));
		list.Add("HitTime = " + ResourcePackManager.HpNum(c.hitTime));
		list.Add("HitSize = " + ResourcePackManager.HpNum(c.hitSize));
		list.Add("HitOffsetX = " + ResourcePackManager.HpNum(c.hitX));
		list.Add("HitOffsetY = " + ResourcePackManager.HpNum(c.hitY));
		list.Add("DpsSilence = " + ResourcePackManager.HpNum(c.dpsSilence));
		list.Add("DpsShow = " + ResourcePackManager.HpNum(c.dpsShow));
		list.Add("HitOwnColor = " + ResourcePackManager.HpHex(c.hitOwnColor));
		list.Add("HitEnemyColor = " + ResourcePackManager.HpHex(c.hitEnemyColor));
		list.Add("DpsColor = " + ResourcePackManager.HpHex(c.dpsColor));
		list.Add("HitStaminaColor = " + ResourcePackManager.HpHex(c.hitStamColor));
		list.Add("CrystalTimer = " + (c.crystalTimer ? "true" : "false"));
		list.Add("CrystalSize = " + ResourcePackManager.HpNum(c.crystalSize));
		list.Add("CrystalOffsetX = " + ResourcePackManager.HpNum(c.crystalX));
		list.Add("CrystalOffsetY = " + ResourcePackManager.HpNum(c.crystalY));
		list.Add("CrystalLift = " + ResourcePackManager.HpNum(c.crystalLift));
		list.Add("CrystalUpColor = " + ResourcePackManager.HpHex(c.crystalUpColor));
		list.Add("CrystalDownColor = " + ResourcePackManager.HpHex(c.crystalDownColor));
		list.Add("BombMapOffset = " + ResourcePackManager.HpNum(c.bombMapYOffset));
		list.Add("BombMapAllNumbers = " + (c.bombMapAllNumbers ? "true" : "false"));
		for (int j = 0; j < 8; j++)
		{
			list.Add(string.Concat(new object[]
			{
				"BombColor",
				j,
				" = ",
				ResourcePackManager.HpHex(c.bombColors[j])
			}));
		}
		for (int k = 0; k < 7; k++)
		{
			list.Add(string.Concat(new object[]
			{
				"BombThreshold",
				k + 1,
				" = ",
				ResourcePackManager.HpNum(c.bombThr[k])
			}));
		}
		list.Add("PathSelectedOnly = " + (c.pathSelectedOnly ? "true" : "false"));
		list.Add("PathOnTop = " + (c.pathOnTop ? "true" : "false"));
		list.Add("PathMarker = " + (c.pathMarker ? "true" : "false"));
		list.Add("PathWidth = " + ResourcePackManager.HpNum(c.pathWidth));
		list.Add("PathLift = " + ResourcePackManager.HpNum(c.pathLift));
		list.Add("PathOpacity = " + ResourcePackManager.HpNum(c.pathAlpha));
		list.Add("PathOwnColor = " + ResourcePackManager.HpHex(c.pathOwnColor));
		list.Add("ColorLayer = " + c.layerName);
		list.Add("EnemyState = " + c.enemyState);
		list.Add("ScanIntervalSeconds = " + ResourcePackManager.HpNum(c.scanInterval));
		list.Add("ConfigCheckSeconds = " + ResourcePackManager.HpNum(c.checkInterval));
		string[] array = new string[]
		{
			"[Own]",
			"[Enemy]",
			"[StaminaOwn]",
			"[StaminaEnemy]",
			"[Ammo]",
			"[BuildingOwn]",
			"[BuildingEnemy]"
		};
		List<ResourcePackManager.HpStop>[] array2 = new List<ResourcePackManager.HpStop>[]
		{
			c.ownStops,
			c.enemyStops,
			c.stamOwnStops,
			c.stamEnemyStops,
			c.ammoStops,
			c.bldOwnStops,
			c.bldEnemyStops
		};
		for (int l = 0; l < array.Length; l++)
		{
			list.Add("");
			list.Add(array[l]);
			List<ResourcePackManager.HpStop> list2 = new List<ResourcePackManager.HpStop>(array2[l]);
			ResourcePackManager.SortHpStops(list2);
			for (int m = 0; m < list2.Count; m++)
			{
				list.Add(ResourcePackManager.HpNum(list2[m].pct) + " = " + ResourcePackManager.HpHex(list2[m].col));
			}
		}
		for (int n = 0; n < 2; n++)
		{
			list.Add("");
			list.Add((n == 0) ? "[OwnOverrides]" : "[EnemyOverrides]");
			List<ResourcePackManager.HpOverride> list3 = (n == 0) ? c.ownOv : c.enemyOv;
			for (int num = 0; num < list3.Count; num++)
			{
				ResourcePackManager.HpOverride hpOverride = list3[num];
				list.Add(hpOverride.name + " = " + (hpOverride.color ? ResourcePackManager.HpHex(hpOverride.c) : ((hpOverride.mult ? "x" : string.Empty) + ResourcePackManager.HpNum(hpOverride.f))));
			}
		}
		return list;
	}

	private void SaveHpConfig()
	{
		bool flag = this.hpCfg == null;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			try
			{
				string hpConfigPath = ResourcePackManager.HpConfigPath;
				File.WriteAllLines(hpConfigPath, ResourcePackManager.SerializeHpConfig(this.hpCfg).ToArray(), Encoding.UTF8);
				this.hpCfgStamp = File.GetLastWriteTimeUtc(hpConfigPath);
			}
			catch (Exception ex)
			{
				ResourcePackManager.PackWarn("[ResourcePacks] Cannot save coloredhp.txt: " + ex.Message);
			}
		}
	}

	private void MarkHpDirty()
	{
		this.hpSaveAt = Time.unscaledTime + 0.6f;
	}

	private void HpPaletteEdited()
	{
		ResourcePackManager.RebuildHpPalettes(this.hpCfg);
		this.MarkHpDirty();
	}

	private void HpStructureEdited()
	{
		this.RestoreHpRings();
		ResourcePackManager.FinishHpConfig(this.hpCfg);
		this.MarkHpDirty();
	}

	private void ResetHpConfigToDefaults()
	{
		try
		{
			File.WriteAllLines(ResourcePackManager.HpConfigPath, ResourcePackManager.DefaultHpConfig, Encoding.UTF8);
		}
		catch (Exception ex)
		{
			ResourcePackManager.PackWarn("[ResourcePacks] Cannot reset coloredhp.txt: " + ex.Message);
		}
		this.ReloadHpConfig(false);
	}

	private bool DragRect(Event e, Rect r, int id, out float tx, out float ty)
	{
		tx = 0f;
		ty = 0f;
		bool flag = e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition);
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			this.hpDragId = id;
		}
		bool flag9 = this.hpDragId == id && e.button == 0 && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag);
		bool flag10 = flag9;
		bool flag11 = flag10;
		bool flag12 = flag11;
		bool flag13 = flag12;
		bool flag14 = flag13;
		bool flag15 = flag14;
		bool flag16 = flag15;
		bool result;
		if (flag16)
		{
			tx = Mathf.Clamp01((e.mousePosition.x - r.x) / r.width);
			ty = Mathf.Clamp01((e.mousePosition.y - r.y) / r.height);
			e.Use();
			result = true;
		}
		else
		{
			result = false;
		}
		return result;
	}

	private float SliderCore(Event e, Rect hit, int id, float value, float min, float max, float step, Color old)
	{
		float num;
		float num2;
		bool flag = this.DragRect(e, hit, id, out num, out num2);
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			float num3 = min + num * (max - min);
			bool flag9 = step > 0f;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				num3 = Mathf.Round(num3 / step) * step;
			}
			value = Mathf.Clamp(num3, min, max);
		}
		bool flag17 = e.type == EventType.Repaint;
		bool flag18 = flag17;
		bool flag19 = flag18;
		bool flag20 = flag19;
		bool flag21 = flag20;
		bool flag22 = flag21;
		bool flag23 = flag22;
		bool flag24 = flag23;
		if (flag24)
		{
			float num4 = Mathf.Clamp01((value - min) / (max - min));
			Rect position = new Rect(hit.x, hit.y + hit.height * 0.5f - 2f, hit.width, 4f);
			GUI.color = new Color(1f, 1f, 1f, 0.22f);
			GUI.DrawTexture(position, this.whiteTex);
			GUI.color = new Color(0.55f, 0.85f, 0.55f, 1f);
			GUI.DrawTexture(new Rect(position.x, position.y, position.width * num4, position.height), this.whiteTex);
			GUI.color = Color.white;
			float num5 = hit.height * 0.5f;
			GUI.DrawTexture(new Rect(position.x + position.width * num4 - num5 * 0.25f, hit.y + hit.height * 0.5f - num5 * 0.5f, num5 * 0.5f, num5), this.whiteTex);
			GUI.color = old;
		}
		return value;
	}

	private bool CheckRow(Event e, ref float y, float w, float rowH, int fs, Color old, string label, bool on)
	{
		label = BtcLang.Tr(label);
		Rect rect = new Rect(0f, y, w, rowH);
		y += rowH;
		this.DrawRowBackground(e, rect, false, old);
		bool flag = e.type == EventType.Repaint;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			float num = (float)fs * 1.1f;
			this.DrawCheckbox(new Rect((float)fs, rect.y + (rowH - num) * 0.5f, num, num), on);
		}
		return GUI.Button(rect, label, this.menuHatItemStyle);
	}

	private void HeaderRow(Event e, ref float y, float w, float rowH, int fs, Color old, string text)
	{
		text = BtcLang.Tr(text);
		bool flag = e.type == EventType.Repaint;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			GUI.color = new Color(1f, 1f, 1f, 0.12f);
			GUI.DrawTexture(new Rect((float)fs, y + rowH * 0.15f, w - (float)fs * 2f, 1f), this.whiteTex);
			GUI.color = old;
		}
		GUI.Label(new Rect((float)fs, y + rowH * 0.15f, w - (float)fs * 2f, rowH * 0.85f), text, this.menuHeaderStyle);
		y += rowH;
	}

	private void SwatchBox(Rect r, Color c, bool selected)
	{
		bool flag = Event.current.type != EventType.Repaint;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			Color color = GUI.color;
			GUI.color = (selected ? new Color(0.55f, 0.85f, 0.55f, 1f) : new Color(1f, 1f, 1f, 0.7f));
			GUI.DrawTexture(r, this.whiteTex);
			c.a = 1f;
			GUI.color = c;
			GUI.DrawTexture(new Rect(r.x + 2f, r.y + 2f, r.width - 4f, r.height - 4f), this.whiteTex);
			GUI.color = color;
		}
	}

	private void EnsurePickerTextures()
	{
		bool flag = this.hueTex == null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			this.hueTex = new Texture2D(1, 64, TextureFormat.RGBA32, false);
			this.hueTex.hideFlags = HideFlags.HideAndDontSave;
			this.hueTex.wrapMode = TextureWrapMode.Clamp;
			this.hueTex.filterMode = FilterMode.Bilinear;
			Color32[] array = new Color32[64];
			for (int i = 0; i < 64; i++)
			{
				array[i] = Color.HSVToRGB(1f - (float)i / 63f, 1f, 1f);
			}
			this.hueTex.SetPixels32(array);
			this.hueTex.Apply();
		}
		bool flag9 = this.svTex == null;
		bool flag10 = flag9;
		bool flag11 = flag10;
		bool flag12 = flag11;
		bool flag13 = flag12;
		bool flag14 = flag13;
		bool flag15 = flag14;
		bool flag16 = flag15;
		if (flag16)
		{
			this.svTex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
			this.svTex.hideFlags = HideFlags.HideAndDontSave;
			this.svTex.wrapMode = TextureWrapMode.Clamp;
			this.svTex.filterMode = FilterMode.Bilinear;
			this.svTexHue = -1f;
		}
		bool flag17 = this.svTexHue != this.pickH;
		bool flag18 = flag17;
		bool flag19 = flag18;
		bool flag20 = flag19;
		bool flag21 = flag20;
		bool flag22 = flag21;
		bool flag23 = flag22;
		bool flag24 = flag23;
		if (flag24)
		{
			Color32[] array2 = new Color32[1024];
			for (int j = 0; j < 32; j++)
			{
				for (int k = 0; k < 32; k++)
				{
					array2[j * 32 + k] = Color.HSVToRGB(this.pickH, (float)k / 31f, (float)j / 31f);
				}
			}
			this.svTex.SetPixels32(array2);
			this.svTex.Apply();
			this.svTexHue = this.pickH;
		}
	}

	private Color PickedColor()
	{
		bool flag = this.pickExtra != 0;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		Color result;
		if (flag8)
		{
			result = this.GetExtraColor(this.pickExtra);
		}
		else
		{
			bool flag9 = this.pickNum && this.hpCfg != null;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				result = this.hpCfg.numColor;
			}
			else
			{
				result = ((this.pickStop != null) ? this.pickStop.col : ((this.pickOv != null) ? this.pickOv.c : Color.white));
			}
		}
		return result;
	}

	private void SetPickedColor(Color c)
	{
		bool flag = this.pickExtra != 0;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			this.SetExtraColor(this.pickExtra, c);
		}
		else
		{
			bool flag9 = this.pickNum && this.hpCfg != null;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				c.a = this.hpCfg.numColor.a;
				this.hpCfg.numColor = c;
				this.MarkHpDirty();
			}
			else
			{
				bool flag17 = this.pickStop != null;
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				if (flag24)
				{
					c.a = this.pickStop.col.a;
					this.pickStop.col = c;
					this.HpPaletteEdited();
				}
				else
				{
					bool flag25 = this.pickOv != null;
					bool flag26 = flag25;
					bool flag27 = flag26;
					bool flag28 = flag27;
					bool flag29 = flag28;
					bool flag30 = flag29;
					bool flag31 = flag30;
					bool flag32 = flag31;
					if (flag32)
					{
						c.a = this.pickOv.c.a;
						this.pickOv.c = c;
						this.MarkHpDirty();
					}
				}
			}
		}
	}

	private void TogglePicker(ResourcePackManager.HpStop st, ResourcePackManager.HpOverride ov)
	{
		bool flag = (st != null && this.pickStop == st) || (ov != null && this.pickOv == ov);
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			this.pickStop = null;
			this.pickOv = null;
			this.pickNum = false;
			this.pickExtra = 0;
		}
		else
		{
			this.pickStop = st;
			this.pickOv = ov;
			this.pickNum = false;
			this.pickExtra = 0;
			Color.RGBToHSV(this.PickedColor(), out this.pickH, out this.pickS, out this.pickV);
			this.svTexHue = -1f;
		}
	}

	private float DrawPicker(Event e, float y, float w, int fs, Color old)
	{
		float num = (float)fs * 7f;
		float num2 = num + (float)fs * 1.2f;
		this.EnsurePickerTextures();
		Rect rect = new Rect((float)fs * 3.4f, y + (float)fs * 0.6f, num, num);
		Rect rect2 = new Rect(rect.xMax + (float)fs * 0.8f, rect.y, (float)fs * 1.3f, num);
		Rect r = new Rect(rect2.xMax + (float)fs * 1.4f, rect.y, (float)fs * 3.2f, (float)fs * 3.2f);
		bool flag = false;
		float num3;
		float num4;
		bool flag2 = this.DragRect(e, rect, 9001, out num3, out num4);
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		bool flag9 = flag8;
		if (flag9)
		{
			this.pickS = num3;
			this.pickV = 1f - num4;
			flag = true;
		}
		bool flag10 = this.DragRect(e, rect2, 9002, out num3, out num4);
		bool flag11 = flag10;
		bool flag12 = flag11;
		bool flag13 = flag12;
		bool flag14 = flag13;
		bool flag15 = flag14;
		bool flag16 = flag15;
		bool flag17 = flag16;
		if (flag17)
		{
			this.pickH = num4;
			flag = true;
		}
		bool flag18 = flag;
		bool flag19 = flag18;
		bool flag20 = flag19;
		bool flag21 = flag20;
		bool flag22 = flag21;
		bool flag23 = flag22;
		bool flag24 = flag23;
		bool flag25 = flag24;
		if (flag25)
		{
			this.SetPickedColor(Color.HSVToRGB(this.pickH, this.pickS, this.pickV));
			this.EnsurePickerTextures();
		}
		Color color = this.PickedColor();
		bool flag26 = e.type == EventType.Repaint;
		bool flag27 = flag26;
		bool flag28 = flag27;
		bool flag29 = flag28;
		bool flag30 = flag29;
		bool flag31 = flag30;
		bool flag32 = flag31;
		bool flag33 = flag32;
		if (flag33)
		{
			GUI.color = Color.white;
			GUI.DrawTexture(rect, this.svTex);
			GUI.DrawTexture(rect2, this.hueTex);
			float num5 = rect.x + this.pickS * rect.width;
			float num6 = rect.y + (1f - this.pickV) * rect.height;
			GUI.color = Color.black;
			GUI.DrawTexture(new Rect(num5 - 6f, num6 - 6f, 12f, 12f), this.whiteTex);
			GUI.color = Color.white;
			GUI.DrawTexture(new Rect(num5 - 5f, num6 - 5f, 10f, 10f), this.whiteTex);
			Color color2 = color;
			color2.a = 1f;
			GUI.color = color2;
			GUI.DrawTexture(new Rect(num5 - 3f, num6 - 3f, 6f, 6f), this.whiteTex);
			float num7 = rect2.y + this.pickH * rect2.height;
			GUI.color = Color.black;
			GUI.DrawTexture(new Rect(rect2.x - 3f, num7 - 2.5f, rect2.width + 6f, 5f), this.whiteTex);
			GUI.color = Color.white;
			GUI.DrawTexture(new Rect(rect2.x - 3f, num7 - 1.5f, rect2.width + 6f, 3f), this.whiteTex);
			GUI.color = old;
		}
		this.SwatchBox(r, color, false);
		GUI.Label(new Rect(r.x - (float)fs, r.yMax + (float)fs * 0.3f, r.width + (float)fs * 4f, (float)fs * 1.5f), ResourcePackManager.HpHex(color), this.menuLabelStyle);
		return y + num2;
	}

	private void DrawStopRows(Event e, ref float y, float w, float rowH, int fs, Color old, List<ResourcePackManager.HpStop> list, int team)
	{
		for (int i = 0; i < list.Count; i++)
		{
			ResourcePackManager.HpStop hpStop = list[i];
			Rect row = new Rect(0f, y, w, rowH);
			this.DrawRowBackground(e, row, false, old);
			float num = (float)fs * 1.5f;
			Rect rect = new Rect((float)fs, y + (rowH - num * 0.75f) * 0.5f, num, num * 0.75f);
			this.SwatchBox(rect, hpStop.col, this.pickStop == hpStop);
			bool flag = GUI.Button(rect, GUIContent.none, GUIStyle.none);
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				this.TogglePicker(hpStop, null);
			}
			Rect hit = new Rect((float)fs * 3.4f, y, w - (float)fs * 3.4f - (float)fs * 8f, rowH);
			float num2 = this.SliderCore(e, hit, ((team < 2) ? (100 + team * 50) : (600 + (team - 2) * 50)) + i, hpStop.pct, 0f, 100f, 1f, old);
			bool flag9 = num2 != hpStop.pct;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				hpStop.pct = num2;
				this.HpPaletteEdited();
			}
			GUI.Label(new Rect(hit.xMax + (float)fs * 0.5f, y, (float)fs * 3.4f, rowH), ResourcePackManager.HpNum(hpStop.pct) + "%", this.menuValueStyle);
			bool flag17 = list.Count > 1 && GUI.Button(new Rect(w - (float)fs * 3.2f, y, (float)fs * 2.2f, rowH), "x", this.menuValueStyle);
			bool flag18 = flag17;
			bool flag19 = flag18;
			bool flag20 = flag19;
			bool flag21 = flag20;
			bool flag22 = flag21;
			bool flag23 = flag22;
			bool flag24 = flag23;
			if (flag24)
			{
				bool flag25 = this.pickStop == hpStop;
				bool flag26 = flag25;
				bool flag27 = flag26;
				bool flag28 = flag27;
				bool flag29 = flag28;
				bool flag30 = flag29;
				bool flag31 = flag30;
				bool flag32 = flag31;
				if (flag32)
				{
					this.pickStop = null;
				}
				list.RemoveAt(i);
				this.HpPaletteEdited();
				y += rowH;
				return;
			}
			y += rowH;
			bool flag33 = this.pickStop == hpStop;
			bool flag34 = flag33;
			bool flag35 = flag34;
			bool flag36 = flag35;
			bool flag37 = flag36;
			bool flag38 = flag37;
			bool flag39 = flag38;
			bool flag40 = flag39;
			if (flag40)
			{
				y = this.DrawPicker(e, y, w, fs, old);
			}
		}
		Rect rect2 = new Rect(0f, y, w, rowH);
		this.DrawRowBackground(e, rect2, false, old);
		bool flag41 = GUI.Button(rect2, BtcLang.Tr("+ Add color"), this.menuHatItemStyle);
		bool flag42 = flag41;
		bool flag43 = flag42;
		bool flag44 = flag43;
		bool flag45 = flag44;
		bool flag46 = flag45;
		bool flag47 = flag46;
		bool flag48 = flag47;
		if (flag48)
		{
			list.Add(new ResourcePackManager.HpStop
			{
				pct = ((list.Count > 0) ? Mathf.Round(list[list.Count - 1].pct * 0.5f) : 50f),
				col = ((list.Count > 0) ? list[list.Count - 1].col : Color.white)
			});
			this.HpPaletteEdited();
		}
		y += rowH;
	}
}
}
