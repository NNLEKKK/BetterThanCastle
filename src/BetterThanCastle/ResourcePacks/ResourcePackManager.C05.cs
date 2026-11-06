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

	private static ResourcePackManager.HpOverride FindHpOverride(List<ResourcePackManager.HpOverride> list, string name)
	{
		for (int i = 0; i < list.Count; i++)
		{
			bool flag = list[i].name == name;
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

	private void DrawOverrideRows(Event e, ref float y, float w, float rowH, int fs, Color old, List<ResourcePackManager.HpOverride> list, int team)
	{
		for (int i = 0; i < ResourcePackManager.HpKnownFloatProps.Length + ResourcePackManager.HpKnownColorProps.Length; i++)
		{
			bool flag = i >= ResourcePackManager.HpKnownFloatProps.Length;
			string text = flag ? ResourcePackManager.HpKnownColorProps[i - ResourcePackManager.HpKnownFloatProps.Length] : ResourcePackManager.HpKnownFloatProps[i];
			ResourcePackManager.HpOverride hpOverride = ResourcePackManager.FindHpOverride(list, text);
			Rect row = new Rect(0f, y, w, rowH);
			this.DrawRowBackground(e, row, false, old);
			bool flag2 = e.type == EventType.Repaint;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			bool flag9 = flag8;
			if (flag9)
			{
				float num = (float)fs * 1.1f;
				this.DrawCheckbox(new Rect((float)fs, y + (rowH - num) * 0.5f, num, num), hpOverride != null);
			}
			bool flag10 = GUI.Button(new Rect(0f, y, w * 0.5f, rowH), text, this.menuHatItemStyle);
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			bool flag17 = flag16;
			if (flag17)
			{
				bool flag18 = hpOverride == null;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				bool flag25 = flag24;
				if (flag25)
				{
					hpOverride = new ResourcePackManager.HpOverride();
					hpOverride.name = text;
					hpOverride.id = Shader.PropertyToID(text);
					hpOverride.color = flag;
					hpOverride.mult = !flag;
					hpOverride.f = 1f;
					hpOverride.c = new Color(0.35f, 0.05f, 0.05f, 1f);
					list.Add(hpOverride);
				}
				else
				{
					bool flag26 = this.pickOv == hpOverride;
					bool flag27 = flag26;
					bool flag28 = flag27;
					bool flag29 = flag28;
					bool flag30 = flag29;
					bool flag31 = flag30;
					bool flag32 = flag31;
					bool flag33 = flag32;
					if (flag33)
					{
						this.pickOv = null;
					}
					list.Remove(hpOverride);
					hpOverride = null;
				}
				this.HpStructureEdited();
			}
			bool flag34 = hpOverride != null;
			bool flag35 = flag34;
			bool flag36 = flag35;
			bool flag37 = flag36;
			bool flag38 = flag37;
			bool flag39 = flag38;
			bool flag40 = flag39;
			bool flag41 = flag40;
			if (flag41)
			{
				bool color = hpOverride.color;
				bool flag42 = color;
				bool flag43 = flag42;
				bool flag44 = flag43;
				bool flag45 = flag44;
				bool flag46 = flag45;
				bool flag47 = flag46;
				bool flag48 = flag47;
				if (flag48)
				{
					float num2 = (float)fs * 1.5f;
					Rect rect = new Rect(w * 0.52f, y + (rowH - num2 * 0.75f) * 0.5f, num2, num2 * 0.75f);
					this.SwatchBox(rect, hpOverride.c, this.pickOv == hpOverride);
					bool flag49 = GUI.Button(rect, GUIContent.none, GUIStyle.none);
					bool flag50 = flag49;
					bool flag51 = flag50;
					bool flag52 = flag51;
					bool flag53 = flag52;
					bool flag54 = flag53;
					bool flag55 = flag54;
					bool flag56 = flag55;
					if (flag56)
					{
						this.TogglePicker(null, hpOverride);
					}
					GUI.Label(new Rect(rect.xMax + (float)fs * 0.6f, y, (float)fs * 6f, rowH), ResourcePackManager.HpHex(hpOverride.c), this.menuLabelStyle);
				}
				else
				{
					Rect hit = new Rect(w * 0.52f, y, w * 0.48f - (float)fs * 5.5f, rowH);
					float min = hpOverride.mult ? 0.1f : 0f;
					float max = hpOverride.mult ? 4f : Mathf.Max(1f, hpOverride.f * 2f);
					float num3 = this.SliderCore(e, hit, 300 + team * 50 + i, hpOverride.f, min, max, hpOverride.mult ? 0.05f : 0.01f, old);
					bool flag57 = num3 != hpOverride.f;
					bool flag58 = flag57;
					bool flag59 = flag58;
					bool flag60 = flag59;
					bool flag61 = flag60;
					bool flag62 = flag61;
					bool flag63 = flag62;
					bool flag64 = flag63;
					if (flag64)
					{
						hpOverride.f = num3;
						this.MarkHpDirty();
					}
					GUI.Label(new Rect(hit.xMax + (float)fs * 0.3f, y, (float)fs * 5f, rowH), (hpOverride.mult ? "x" : "=") + hpOverride.f.ToString("0.00", CultureInfo.InvariantCulture), this.menuValueStyle);
				}
			}
			y += rowH;
			bool flag65 = hpOverride != null && hpOverride.color && this.pickOv == hpOverride;
			bool flag66 = flag65;
			bool flag67 = flag66;
			bool flag68 = flag67;
			bool flag69 = flag68;
			bool flag70 = flag69;
			bool flag71 = flag70;
			bool flag72 = flag71;
			if (flag72)
			{
				y = this.DrawPicker(e, y, w, fs, old);
			}
		}
	}

	private void DrawHpRows(Event e, float w, float rowH, int fs, Color old)
	{
		bool flag = this.hpCfg == null;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		bool flag9 = flag8;
		if (flag9)
		{
			ResourcePackManager.HpConfig hpConfig = this.hpCfg;
			float num = 0f;
			bool flag10 = this.Section(e, ref num, w, rowH, old, "Rings (what is colored)", 0);
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
				bool flag19 = this.CheckRow(e, ref num, w, rowH, fs, old, "Color HP ring (ring 1)", hpConfig.rings[0]);
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
					hpConfig.rings = new bool[]
					{
						!hpConfig.rings[0],
						hpConfig.rings[1]
					};
					this.HpStructureEdited();
				}
				bool flag28 = this.CheckRow(e, ref num, w, rowH, fs, old, "Color stamina ring - knight (ring 2)", hpConfig.staminaColor);
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
					hpConfig.staminaColor = !hpConfig.staminaColor;
					this.HpStructureEdited();
				}
				bool flag37 = this.CheckRow(e, ref num, w, rowH, fs, old, "Color ammo ring - alchemist (ring 2)", hpConfig.ammoColor);
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
					hpConfig.ammoColor = !hpConfig.ammoColor;
					this.HpStructureEdited();
				}
				bool flag46 = this.CheckRow(e, ref num, w, rowH, fs, old, "Color enemy rings (off = vanilla)", hpConfig.colorEnemies);
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
					hpConfig.colorEnemies = !hpConfig.colorEnemies;
					this.HpStructureEdited();
				}
				bool flag55 = hpConfig.colorEnemies && this.CheckRow(e, ref num, w, rowH, fs, old, "   Separate enemy palettes", hpConfig.enemyPalette);
				bool flag56 = flag55;
				bool flag57 = flag56;
				bool flag58 = flag57;
				bool flag59 = flag58;
				bool flag60 = flag59;
				bool flag61 = flag60;
				bool flag62 = flag61;
				bool flag63 = flag62;
				if (flag63)
				{
					hpConfig.enemyPalette = !hpConfig.enemyPalette;
					this.HpStructureEdited();
				}
				bool flag64 = this.CheckRow(e, ref num, w, rowH, fs, old, "Selected unit: rings lighter / darker", hpConfig.selShiftOn);
				bool flag65 = flag64;
				bool flag66 = flag65;
				bool flag67 = flag66;
				bool flag68 = flag67;
				bool flag69 = flag68;
				bool flag70 = flag69;
				bool flag71 = flag70;
				bool flag72 = flag71;
				if (flag72)
				{
					hpConfig.selShiftOn = !hpConfig.selShiftOn;
					this.MarkHpDirty();
				}
				bool selShiftOn = hpConfig.selShiftOn;
				bool flag73 = selShiftOn;
				bool flag74 = flag73;
				bool flag75 = flag74;
				bool flag76 = flag75;
				bool flag77 = flag76;
				bool flag78 = flag77;
				bool flag79 = flag78;
				bool flag80 = flag79;
				if (flag80)
				{
					float num2 = this.SliderRow(e, ref num, w, rowH, fs, old, "   Shift (- darker, + lighter)", 16, hpConfig.selShift * 100f, -60f, 60f, 5f, "+0;-0;0", string.Empty, "%");
					bool flag81 = !Mathf.Approximately(num2 / 100f, hpConfig.selShift);
					bool flag82 = flag81;
					bool flag83 = flag82;
					bool flag84 = flag83;
					bool flag85 = flag84;
					bool flag86 = flag85;
					bool flag87 = flag86;
					bool flag88 = flag87;
					bool flag89 = flag88;
					if (flag89)
					{
						hpConfig.selShift = num2 / 100f;
						this.MarkHpDirty();
					}
				}
			}
			bool flag90 = this.Section(e, ref num, w, rowH, old, "Palettes", 1);
			bool flag91 = flag90;
			bool flag92 = flag91;
			bool flag93 = flag92;
			bool flag94 = flag93;
			bool flag95 = flag94;
			bool flag96 = flag95;
			bool flag97 = flag96;
			bool flag98 = flag97;
			if (flag98)
			{
				bool flag99 = hpConfig.colorEnemies && hpConfig.enemyPalette;
				this.DrawPaletteGroup(e, ref num, w, rowH, fs, old, 0, "HP - own", hpConfig.ownStops);
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
					this.DrawPaletteGroup(e, ref num, w, rowH, fs, old, 1, "HP - enemy", hpConfig.enemyStops);
				}
				this.DrawPaletteGroup(e, ref num, w, rowH, fs, old, 2, "Stamina - knight", hpConfig.stamOwnStops);
				bool flag109 = flag99;
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
					this.DrawPaletteGroup(e, ref num, w, rowH, fs, old, 3, "Stamina - enemy knight", hpConfig.stamEnemyStops);
				}
				this.DrawPaletteGroup(e, ref num, w, rowH, fs, old, 4, "Ammo - alchemist (10 bombs = 100%)", hpConfig.ammoStops);
				bool flag118 = this.CheckRow(e, ref num, w, rowH, fs, old, "Separate HP colors for buildings (storage, racks, crystals...)", hpConfig.bldColor);
				bool flag119 = flag118;
				bool flag120 = flag119;
				bool flag121 = flag120;
				bool flag122 = flag121;
				bool flag123 = flag122;
				bool flag124 = flag123;
				bool flag125 = flag124;
				bool flag126 = flag125;
				if (flag126)
				{
					hpConfig.bldColor = !hpConfig.bldColor;
					this.MarkHpDirty();
				}
				bool bldColor = hpConfig.bldColor;
				bool flag127 = bldColor;
				bool flag128 = flag127;
				bool flag129 = flag128;
				bool flag130 = flag129;
				bool flag131 = flag130;
				bool flag132 = flag131;
				bool flag133 = flag132;
				bool flag134 = flag133;
				if (flag134)
				{
					this.DrawPaletteGroup(e, ref num, w, rowH, fs, old, 5, "HP - buildings own", hpConfig.bldOwnStops);
					bool flag135 = flag99;
					bool flag136 = flag135;
					bool flag137 = flag136;
					bool flag138 = flag137;
					bool flag139 = flag138;
					bool flag140 = flag139;
					bool flag141 = flag140;
					bool flag142 = flag141;
					bool flag143 = flag142;
					if (flag143)
					{
						this.DrawPaletteGroup(e, ref num, w, rowH, fs, old, 6, "HP - buildings enemy", hpConfig.bldEnemyStops);
					}
				}
			}
			bool flag144 = this.Section(e, ref num, w, rowH, old, "Visibility", 2);
			bool flag145 = flag144;
			bool flag146 = flag145;
			bool flag147 = flag146;
			bool flag148 = flag147;
			bool flag149 = flag148;
			bool flag150 = flag149;
			bool flag151 = flag150;
			bool flag152 = flag151;
			if (flag152)
			{
				bool flag153 = this.CheckRow(e, ref num, w, rowH, fs, old, "Always show own rings (even if not selected / not hurt)", hpConfig.alwaysOwn);
				bool flag154 = flag153;
				bool flag155 = flag154;
				bool flag156 = flag155;
				bool flag157 = flag156;
				bool flag158 = flag157;
				bool flag159 = flag158;
				bool flag160 = flag159;
				bool flag161 = flag160;
				if (flag161)
				{
					hpConfig.alwaysOwn = !hpConfig.alwaysOwn;
					this.hpNextScan = 0f;
					this.MarkHpDirty();
				}
				bool flag162 = this.CheckRow(e, ref num, w, rowH, fs, old, "Always show enemy rings (even if not selected / not hurt)", hpConfig.alwaysEnemy);
				bool flag163 = flag162;
				bool flag164 = flag163;
				bool flag165 = flag164;
				bool flag166 = flag165;
				bool flag167 = flag166;
				bool flag168 = flag167;
				bool flag169 = flag168;
				bool flag170 = flag169;
				if (flag170)
				{
					hpConfig.alwaysEnemy = !hpConfig.alwaysEnemy;
					this.hpNextScan = 0f;
					this.MarkHpDirty();
				}
				bool flag171 = this.CheckRow(e, ref num, w, rowH, fs, old, "Stamina ring only for knights", hpConfig.knightStaminaOnly);
				bool flag172 = flag171;
				bool flag173 = flag172;
				bool flag174 = flag173;
				bool flag175 = flag174;
				bool flag176 = flag175;
				bool flag177 = flag176;
				bool flag178 = flag177;
				bool flag179 = flag178;
				if (flag179)
				{
					hpConfig.knightStaminaOnly = !hpConfig.knightStaminaOnly;
					this.MarkHpDirty();
				}
				bool flag180 = this.CheckRow(e, ref num, w, rowH, fs, old, "Alchemist ammo ring (bar)", hpConfig.ammoRing);
				bool flag181 = flag180;
				bool flag182 = flag181;
				bool flag183 = flag182;
				bool flag184 = flag183;
				bool flag185 = flag184;
				bool flag186 = flag185;
				bool flag187 = flag186;
				bool flag188 = flag187;
				if (flag188)
				{
					hpConfig.ammoRing = !hpConfig.ammoRing;
					this.MarkHpDirty();
				}
				bool flag189 = this.CheckRow(e, ref num, w, rowH, fs, old, "Show overlays only while LeftAlt is held", hpConfig.altOnly);
				bool flag190 = flag189;
				bool flag191 = flag190;
				bool flag192 = flag191;
				bool flag193 = flag192;
				bool flag194 = flag193;
				bool flag195 = flag194;
				bool flag196 = flag195;
				bool flag197 = flag196;
				if (flag197)
				{
					hpConfig.altOnly = !hpConfig.altOnly;
					this.MarkHpDirty();
				}
				bool altOnly = hpConfig.altOnly;
				bool flag198 = altOnly;
				bool flag199 = flag198;
				bool flag200 = flag199;
				bool flag201 = flag200;
				bool flag202 = flag201;
				bool flag203 = flag202;
				bool flag204 = flag203;
				bool flag205 = flag204;
				if (flag205)
				{
					bool flag206 = this.CheckRow(e, ref num, w, rowH, fs, old, "   Numbers: HP / stamina / regen (units, buildings)", hpConfig.altNumbers);
					bool flag207 = flag206;
					bool flag208 = flag207;
					bool flag209 = flag208;
					bool flag210 = flag209;
					bool flag211 = flag210;
					bool flag212 = flag211;
					bool flag213 = flag212;
					bool flag214 = flag213;
					if (flag214)
					{
						hpConfig.altNumbers = !hpConfig.altNumbers;
						this.MarkHpDirty();
					}
					bool flag215 = this.CheckRow(e, ref num, w, rowH, fs, old, "   Alchemist bomb count", hpConfig.altAmmo);
					bool flag216 = flag215;
					bool flag217 = flag216;
					bool flag218 = flag217;
					bool flag219 = flag218;
					bool flag220 = flag219;
					bool flag221 = flag220;
					bool flag222 = flag221;
					bool flag223 = flag222;
					if (flag223)
					{
						hpConfig.altAmmo = !hpConfig.altAmmo;
						this.MarkHpDirty();
					}
					bool flag224 = this.CheckRow(e, ref num, w, rowH, fs, old, "   Damage numbers and DPS", hpConfig.altDamage);
					bool flag225 = flag224;
					bool flag226 = flag225;
					bool flag227 = flag226;
					bool flag228 = flag227;
					bool flag229 = flag228;
					bool flag230 = flag229;
					bool flag231 = flag230;
					bool flag232 = flag231;
					if (flag232)
					{
						hpConfig.altDamage = !hpConfig.altDamage;
						this.MarkHpDirty();
					}
					bool flag233 = this.CheckRow(e, ref num, w, rowH, fs, old, "   Crystal timer", hpConfig.altCrystal);
					bool flag234 = flag233;
					bool flag235 = flag234;
					bool flag236 = flag235;
					bool flag237 = flag236;
					bool flag238 = flag237;
					bool flag239 = flag238;
					bool flag240 = flag239;
					bool flag241 = flag240;
					if (flag241)
					{
						hpConfig.altCrystal = !hpConfig.altCrystal;
						this.MarkHpDirty();
					}
					bool flag242 = this.CheckRow(e, ref num, w, rowH, fs, old, "   Bomb map (cells and numbers)", hpConfig.altBombMap);
					bool flag243 = flag242;
					bool flag244 = flag243;
					bool flag245 = flag244;
					bool flag246 = flag245;
					bool flag247 = flag246;
					bool flag248 = flag247;
					bool flag249 = flag248;
					bool flag250 = flag249;
					if (flag250)
					{
						hpConfig.altBombMap = !hpConfig.altBombMap;
						this.MarkHpDirty();
					}
					bool flag251 = this.CheckRow(e, ref num, w, rowH, fs, old, "   Forced rings (Always show own / enemy)", hpConfig.altRings);
					bool flag252 = flag251;
					bool flag253 = flag252;
					bool flag254 = flag253;
					bool flag255 = flag254;
					bool flag256 = flag255;
					bool flag257 = flag256;
					bool flag258 = flag257;
					bool flag259 = flag258;
					if (flag259)
					{
						hpConfig.altRings = !hpConfig.altRings;
						this.hpNextScan = 0f;
						this.MarkHpDirty();
					}
					bool flag260 = this.CheckRow(e, ref num, w, rowH, fs, old, "   Path lines", hpConfig.altPaths);
					bool flag261 = flag260;
					bool flag262 = flag261;
					bool flag263 = flag262;
					bool flag264 = flag263;
					bool flag265 = flag264;
					bool flag266 = flag265;
					bool flag267 = flag266;
					bool flag268 = flag267;
					if (flag268)
					{
						hpConfig.altPaths = !hpConfig.altPaths;
						this.MarkHpDirty();
					}
				}
			}
			bool flag269 = this.Section(e, ref num, w, rowH, old, "Bomb counter (alchemist)", 3);
			bool flag270 = flag269;
			bool flag271 = flag270;
			bool flag272 = flag271;
			bool flag273 = flag272;
			bool flag274 = flag273;
			bool flag275 = flag274;
			bool flag276 = flag275;
			bool flag277 = flag276;
			if (flag277)
			{
				bool flag278 = this.CheckRow(e, ref num, w, rowH, fs, old, "Show bomb count (0-10)", hpConfig.ammoNumber);
				bool flag279 = flag278;
				bool flag280 = flag279;
				bool flag281 = flag280;
				bool flag282 = flag281;
				bool flag283 = flag282;
				bool flag284 = flag283;
				bool flag285 = flag284;
				bool flag286 = flag285;
				if (flag286)
				{
					hpConfig.ammoNumber = !hpConfig.ammoNumber;
					this.ammoNextScan = 0f;
					bool flag287 = !hpConfig.ammoNumber;
					bool flag288 = flag287;
					bool flag289 = flag288;
					bool flag290 = flag289;
					bool flag291 = flag290;
					bool flag292 = flag291;
					bool flag293 = flag292;
					bool flag294 = flag293;
					bool flag295 = flag294;
					if (flag295)
					{
						this.pickNum = false;
						this.pickExtra = 0;
					}
					this.MarkHpDirty();
				}
				bool ammoNumber = hpConfig.ammoNumber;
				bool flag296 = ammoNumber;
				bool flag297 = flag296;
				bool flag298 = flag297;
				bool flag299 = flag298;
				bool flag300 = flag299;
				bool flag301 = flag300;
				bool flag302 = flag301;
				bool flag303 = flag302;
				if (flag303)
				{
					float num3 = this.SliderRow(e, ref num, w, rowH, fs, old, "   Size", 31, hpConfig.numSize, 0.3f, 4f, 0.05f, "0.00", "x", string.Empty);
					bool flag304 = num3 != hpConfig.numSize;
					bool flag305 = flag304;
					bool flag306 = flag305;
					bool flag307 = flag306;
					bool flag308 = flag307;
					bool flag309 = flag308;
					bool flag310 = flag309;
					bool flag311 = flag310;
					bool flag312 = flag311;
					if (flag312)
					{
						hpConfig.numSize = num3;
						this.MarkHpDirty();
					}
					float num4 = this.SliderRow(e, ref num, w, rowH, fs, old, "   Offset X", 32, hpConfig.numX, -200f, 200f, 1f, "0", string.Empty, " px");
					bool flag313 = num4 != hpConfig.numX;
					bool flag314 = flag313;
					bool flag315 = flag314;
					bool flag316 = flag315;
					bool flag317 = flag316;
					bool flag318 = flag317;
					bool flag319 = flag318;
					bool flag320 = flag319;
					bool flag321 = flag320;
					if (flag321)
					{
						hpConfig.numX = num4;
						this.MarkHpDirty();
					}
					float num5 = this.SliderRow(e, ref num, w, rowH, fs, old, "   Offset Y (up)", 33, hpConfig.numY, -200f, 200f, 1f, "0", string.Empty, " px");
					bool flag322 = num5 != hpConfig.numY;
					bool flag323 = flag322;
					bool flag324 = flag323;
					bool flag325 = flag324;
					bool flag326 = flag325;
					bool flag327 = flag326;
					bool flag328 = flag327;
					bool flag329 = flag328;
					bool flag330 = flag329;
					if (flag330)
					{
						hpConfig.numY = num5;
						this.MarkHpDirty();
					}
					float num6 = this.SliderRow(e, ref num, w, rowH, fs, old, "   Opacity", 34, hpConfig.numColor.a * 100f, 10f, 100f, 5f, "0", string.Empty, "%");
					bool flag331 = !Mathf.Approximately(num6 / 100f, hpConfig.numColor.a);
					bool flag332 = flag331;
					bool flag333 = flag332;
					bool flag334 = flag333;
					bool flag335 = flag334;
					bool flag336 = flag335;
					bool flag337 = flag336;
					bool flag338 = flag337;
					bool flag339 = flag338;
					if (flag339)
					{
						Color numColor = hpConfig.numColor;
						numColor.a = num6 / 100f;
						hpConfig.numColor = numColor;
						this.MarkHpDirty();
					}
					bool flag340 = this.CheckRow(e, ref num, w, rowH, fs, old, "   Color from ammo palette", hpConfig.numByAmmo);
					bool flag341 = flag340;
					bool flag342 = flag341;
					bool flag343 = flag342;
					bool flag344 = flag343;
					bool flag345 = flag344;
					bool flag346 = flag345;
					bool flag347 = flag346;
					bool flag348 = flag347;
					if (flag348)
					{
						hpConfig.numByAmmo = !hpConfig.numByAmmo;
						bool numByAmmo = hpConfig.numByAmmo;
						bool flag349 = numByAmmo;
						bool flag350 = flag349;
						bool flag351 = flag350;
						bool flag352 = flag351;
						bool flag353 = flag352;
						bool flag354 = flag353;
						bool flag355 = flag354;
						bool flag356 = flag355;
						if (flag356)
						{
							this.pickNum = false;
							this.pickExtra = 0;
						}
						this.MarkHpDirty();
					}
					bool flag357 = !hpConfig.numByAmmo;
					bool flag358 = flag357;
					bool flag359 = flag358;
					bool flag360 = flag359;
					bool flag361 = flag360;
					bool flag362 = flag361;
					bool flag363 = flag362;
					bool flag364 = flag363;
					bool flag365 = flag364;
					if (flag365)
					{
						Rect row = new Rect(0f, num, w, rowH);
						this.DrawRowBackground(e, row, false, old);
						GUI.Label(new Rect((float)fs, num, w * 0.42f, rowH), BtcLang.Tr("   Color"), this.menuLabelStyle);
						float num7 = (float)fs * 1.5f;
						Rect rect = new Rect(w * 0.47f, num + (rowH - num7 * 0.75f) * 0.5f, num7, num7 * 0.75f);
						this.SwatchBox(rect, hpConfig.numColor, this.pickNum);
						bool flag366 = GUI.Button(rect, GUIContent.none, GUIStyle.none);
						bool flag367 = flag366;
						bool flag368 = flag367;
						bool flag369 = flag368;
						bool flag370 = flag369;
						bool flag371 = flag370;
						bool flag372 = flag371;
						bool flag373 = flag372;
						bool flag374 = flag373;
						if (flag374)
						{
							this.TogglePickerNum();
						}
						GUI.Label(new Rect(rect.xMax + (float)fs * 0.6f, num, (float)fs * 6f, rowH), ResourcePackManager.HpHex(hpConfig.numColor), this.menuLabelStyle);
						num += rowH;
						bool flag375 = this.pickNum;
						bool flag376 = flag375;
						bool flag377 = flag376;
						bool flag378 = flag377;
						bool flag379 = flag378;
						bool flag380 = flag379;
						bool flag381 = flag380;
						bool flag382 = flag381;
						bool flag383 = flag382;
						if (flag383)
						{
							num = this.DrawPicker(e, num, w, fs, old);
						}
					}
					bool flag384 = this.CheckRow(e, ref num, w, rowH, fs, old, "   Outline", hpConfig.numOutline);
					bool flag385 = flag384;
					bool flag386 = flag385;
					bool flag387 = flag386;
					bool flag388 = flag387;
					bool flag389 = flag388;
					bool flag390 = flag389;
					bool flag391 = flag390;
					bool flag392 = flag391;
					if (flag392)
					{
						hpConfig.numOutline = !hpConfig.numOutline;
						this.MarkHpDirty();
					}
					bool flag393 = this.CheckRow(e, ref num, w, rowH, fs, old, "   Bold", hpConfig.numBold);
					bool flag394 = flag393;
					bool flag395 = flag394;
					bool flag396 = flag395;
					bool flag397 = flag396;
					bool flag398 = flag397;
					bool flag399 = flag398;
					bool flag400 = flag399;
					bool flag401 = flag400;
					if (flag401)
					{
						hpConfig.numBold = !hpConfig.numBold;
						this.MarkHpDirty();
					}
				}
			}
			bool flag402 = this.Section(e, ref num, w, rowH, old, "HP / stamina numbers", 6);
			bool flag403 = flag402;
			bool flag404 = flag403;
			bool flag405 = flag404;
			bool flag406 = flag405;
			bool flag407 = flag406;
			bool flag408 = flag407;
			bool flag409 = flag408;
			bool flag410 = flag409;
			if (flag410)
			{
				bool flag411 = this.CheckRow(e, ref num, w, rowH, fs, old, "HP number - own / allied", hpConfig.hpNumOwn);
				bool flag412 = flag411;
				bool flag413 = flag412;
				bool flag414 = flag413;
				bool flag415 = flag414;
				bool flag416 = flag415;
				bool flag417 = flag416;
				bool flag418 = flag417;
				bool flag419 = flag418;
				if (flag419)
				{
					hpConfig.hpNumOwn = !hpConfig.hpNumOwn;
					this.statNextScan = 0f;
					this.MarkHpDirty();
				}
				bool flag420 = this.CheckRow(e, ref num, w, rowH, fs, old, "HP number - enemy (visible only)", hpConfig.hpNumEnemy);
				bool flag421 = flag420;
				bool flag422 = flag421;
				bool flag423 = flag422;
				bool flag424 = flag423;
				bool flag425 = flag424;
				bool flag426 = flag425;
				bool flag427 = flag426;
				bool flag428 = flag427;
				if (flag428)
				{
					hpConfig.hpNumEnemy = !hpConfig.hpNumEnemy;
					this.statNextScan = 0f;
					this.MarkHpDirty();
				}
				bool flag429 = this.CheckRow(e, ref num, w, rowH, fs, old, "Stamina number - own / allied", hpConfig.stamNumOwn);
				bool flag430 = flag429;
				bool flag431 = flag430;
				bool flag432 = flag431;
				bool flag433 = flag432;
				bool flag434 = flag433;
				bool flag435 = flag434;
				bool flag436 = flag435;
				bool flag437 = flag436;
				if (flag437)
				{
					hpConfig.stamNumOwn = !hpConfig.stamNumOwn;
					this.statNextScan = 0f;
					this.MarkHpDirty();
				}
				bool flag438 = this.CheckRow(e, ref num, w, rowH, fs, old, "Stamina number - enemy (visible only)", hpConfig.stamNumEnemy);
				bool flag439 = flag438;
				bool flag440 = flag439;
				bool flag441 = flag440;
				bool flag442 = flag441;
				bool flag443 = flag442;
				bool flag444 = flag443;
				bool flag445 = flag444;
				bool flag446 = flag445;
				if (flag446)
				{
					hpConfig.stamNumEnemy = !hpConfig.stamNumEnemy;
					this.statNextScan = 0f;
					this.MarkHpDirty();
				}
				bool flag447 = this.CheckRow(e, ref num, w, rowH, fs, old, "Regen countdown (sec) - own / allied", hpConfig.regenNumOwn);
				bool flag448 = flag447;
				bool flag449 = flag448;
				bool flag450 = flag449;
				bool flag451 = flag450;
				bool flag452 = flag451;
				bool flag453 = flag452;
				bool flag454 = flag453;
				bool flag455 = flag454;
				if (flag455)
				{
					hpConfig.regenNumOwn = !hpConfig.regenNumOwn;
					this.statNextScan = 0f;
					this.MarkHpDirty();
				}
				bool flag456 = this.CheckRow(e, ref num, w, rowH, fs, old, "Regen countdown (sec) - enemy (visible only)", hpConfig.regenNumEnemy);
				bool flag457 = flag456;
				bool flag458 = flag457;
				bool flag459 = flag458;
				bool flag460 = flag459;
				bool flag461 = flag460;
				bool flag462 = flag461;
				bool flag463 = flag462;
				bool flag464 = flag463;
				if (flag464)
				{
					hpConfig.regenNumEnemy = !hpConfig.regenNumEnemy;
					this.statNextScan = 0f;
					this.MarkHpDirty();
				}
				bool flag465 = hpConfig.hpNumOwn || hpConfig.hpNumEnemy || hpConfig.stamNumOwn || hpConfig.stamNumEnemy || hpConfig.regenNumOwn || hpConfig.regenNumEnemy;
				bool flag466 = flag465;
				bool flag467 = flag466;
				bool flag468 = flag467;
				bool flag469 = flag468;
				bool flag470 = flag469;
				bool flag471 = flag470;
				bool flag472 = flag471;
				bool flag473 = flag472;
				if (flag473)
				{
					bool flag474 = this.CheckRow(e, ref num, w, rowH, fs, old, "   Stamina numbers only for knights", hpConfig.stamNumKnightOnly);
					bool flag475 = flag474;
					bool flag476 = flag475;
					bool flag477 = flag476;
					bool flag478 = flag477;
					bool flag479 = flag478;
					bool flag480 = flag479;
					bool flag481 = flag480;
					bool flag482 = flag481;
					if (flag482)
					{
						hpConfig.stamNumKnightOnly = !hpConfig.stamNumKnightOnly;
						this.MarkHpDirty();
					}
					bool flag483 = this.CheckRow(e, ref num, w, rowH, fs, old, "   Hide when full", hpConfig.statHideFull);
					bool flag484 = flag483;
					bool flag485 = flag484;
					bool flag486 = flag485;
					bool flag487 = flag486;
					bool flag488 = flag487;
					bool flag489 = flag488;
					bool flag490 = flag489;
					bool flag491 = flag490;
					if (flag491)
					{
						hpConfig.statHideFull = !hpConfig.statHideFull;
						this.MarkHpDirty();
					}
					bool flag492 = this.CheckRow(e, ref num, w, rowH, fs, old, "   Hide when camera is far from the unit", hpConfig.statHideFar);
					bool flag493 = flag492;
					bool flag494 = flag493;
					bool flag495 = flag494;
					bool flag496 = flag495;
					bool flag497 = flag496;
					bool flag498 = flag497;
					bool flag499 = flag498;
					bool flag500 = flag499;
					if (flag500)
					{
						hpConfig.statHideFar = !hpConfig.statHideFar;
						this.MarkHpDirty();
					}
					bool statHideFar = hpConfig.statHideFar;
					bool flag501 = statHideFar;
					bool flag502 = flag501;
					bool flag503 = flag502;
					bool flag504 = flag503;
					bool flag505 = flag504;
					bool flag506 = flag505;
					bool flag507 = flag506;
					bool flag508 = flag507;
					if (flag508)
					{
						float num8 = this.SliderRow(e, ref num, w, rowH, fs, old, "      Camera distance", 732, hpConfig.statHideDist, 10f, 300f, 5f, "0", string.Empty, " vox");
						bool flag509 = !Mathf.Approximately(num8, hpConfig.statHideDist);
						bool flag510 = flag509;
						bool flag511 = flag510;
						bool flag512 = flag511;
						bool flag513 = flag512;
						bool flag514 = flag513;
						bool flag515 = flag514;
						bool flag516 = flag515;
						bool flag517 = flag516;
						if (flag517)
						{
							hpConfig.statHideDist = num8;
							this.MarkHpDirty();
						}
					}
					Rect rect2 = new Rect(0f, num, w, rowH);
					this.DrawRowBackground(e, rect2, false, old);
					string[] array = new string[]
					{
						"42",
						"42/100",
						"42%"
					};
					bool flag518 = GUI.Button(rect2, BtcLang.Tr("   Format: ") + array[Mathf.Clamp(hpConfig.statFormat, 0, 2)] + "  " + BtcLang.Tr("(click to change)"), this.menuItemStyle);
					bool flag519 = flag518;
					bool flag520 = flag519;
					bool flag521 = flag520;
					bool flag522 = flag521;
					bool flag523 = flag522;
					bool flag524 = flag523;
					bool flag525 = flag524;
					bool flag526 = flag525;
					if (flag526)
					{
						hpConfig.statFormat = (hpConfig.statFormat + 1) % 3;
						this.MarkHpDirty();
					}
					num += rowH;
					float num9 = this.SliderRow(e, ref num, w, rowH, fs, old, "   Size", 701, hpConfig.statSize, 0.3f, 3f, 0.05f, "0.00", "x", string.Empty);
					bool flag527 = num9 != hpConfig.statSize;
					bool flag528 = flag527;
					bool flag529 = flag528;
					bool flag530 = flag529;
					bool flag531 = flag530;
					bool flag532 = flag531;
					bool flag533 = flag532;
					bool flag534 = flag533;
					bool flag535 = flag534;
					if (flag535)
					{
						hpConfig.statSize = num9;
						this.MarkHpDirty();
					}
					float num10 = this.SliderRow(e, ref num, w, rowH, fs, old, "   Opacity", 702, hpConfig.statAlpha * 100f, 10f, 100f, 5f, "0", string.Empty, "%");
					bool flag536 = !Mathf.Approximately(num10 / 100f, hpConfig.statAlpha);
					bool flag537 = flag536;
					bool flag538 = flag537;
					bool flag539 = flag538;
					bool flag540 = flag539;
					bool flag541 = flag540;
					bool flag542 = flag541;
					bool flag543 = flag542;
					bool flag544 = flag543;
					if (flag544)
					{
						hpConfig.statAlpha = num10 / 100f;
						this.MarkHpDirty();
					}
					bool flag545 = this.CheckRow(e, ref num, w, rowH, fs, old, "   Bold", hpConfig.statBold);
					bool flag546 = flag545;
					bool flag547 = flag546;
					bool flag548 = flag547;
					bool flag549 = flag548;
					bool flag550 = flag549;
					bool flag551 = flag550;
					bool flag552 = flag551;
					bool flag553 = flag552;
					if (flag553)
					{
						hpConfig.statBold = !hpConfig.statBold;
						this.MarkHpDirty();
					}
					bool flag554 = this.CheckRow(e, ref num, w, rowH, fs, old, "   Outline", hpConfig.statOutline);
					bool flag555 = flag554;
					bool flag556 = flag555;
					bool flag557 = flag556;
					bool flag558 = flag557;
					bool flag559 = flag558;
					bool flag560 = flag559;
					bool flag561 = flag560;
					bool flag562 = flag561;
					if (flag562)
					{
						hpConfig.statOutline = !hpConfig.statOutline;
						this.MarkHpDirty();
					}
					this.HeaderRow(e, ref num, w, rowH, fs, old, "HP number");
					float num11 = this.SliderRow(e, ref num, w, rowH, fs, old, "   Offset X", 703, hpConfig.hpNumX, -200f, 200f, 1f, "0", string.Empty, " px");
					bool flag563 = num11 != hpConfig.hpNumX;
					bool flag564 = flag563;
					bool flag565 = flag564;
					bool flag566 = flag565;
					bool flag567 = flag566;
					bool flag568 = flag567;
					bool flag569 = flag568;
					bool flag570 = flag569;
					bool flag571 = flag570;
					if (flag571)
					{
						hpConfig.hpNumX = num11;
						this.MarkHpDirty();
					}
					float num12 = this.SliderRow(e, ref num, w, rowH, fs, old, "   Offset Y (up)", 704, hpConfig.hpNumY, -200f, 200f, 1f, "0", string.Empty, " px");
					bool flag572 = num12 != hpConfig.hpNumY;
					bool flag573 = flag572;
					bool flag574 = flag573;
					bool flag575 = flag574;
					bool flag576 = flag575;
					bool flag577 = flag576;
					bool flag578 = flag577;
					bool flag579 = flag578;
					bool flag580 = flag579;
					if (flag580)
					{
						hpConfig.hpNumY = num12;
						this.MarkHpDirty();
					}
					bool flag581 = this.CheckRow(e, ref num, w, rowH, fs, old, "   Color from HP palette", hpConfig.hpNumByPalette);
					bool flag582 = flag581;
					bool flag583 = flag582;
					bool flag584 = flag583;
					bool flag585 = flag584;
					bool flag586 = flag585;
					bool flag587 = flag586;
					bool flag588 = flag587;
					bool flag589 = flag588;
					if (flag589)
					{
						hpConfig.hpNumByPalette = !hpConfig.hpNumByPalette;
						bool flag590 = hpConfig.hpNumByPalette && this.pickExtra == 1;
						bool flag591 = flag590;
						bool flag592 = flag591;
						bool flag593 = flag592;
						bool flag594 = flag593;
						bool flag595 = flag594;
						bool flag596 = flag595;
						bool flag597 = flag596;
						bool flag598 = flag597;
						if (flag598)
						{
							this.pickExtra = 0;
						}
						this.MarkHpDirty();
					}
					bool flag599 = !hpConfig.hpNumByPalette;
					bool flag600 = flag599;
					bool flag601 = flag600;
					bool flag602 = flag601;
					bool flag603 = flag602;
					bool flag604 = flag603;
					bool flag605 = flag604;
					bool flag606 = flag605;
					bool flag607 = flag606;
					if (flag607)
					{
						this.ColorRow(e, ref num, w, rowH, fs, old, "   Color", 1);
					}
					this.HeaderRow(e, ref num, w, rowH, fs, old, "Stamina number");
					float num13 = this.SliderRow(e, ref num, w, rowH, fs, old, "   Offset X", 705, hpConfig.stamNumX, -200f, 200f, 1f, "0", string.Empty, " px");
					bool flag608 = num13 != hpConfig.stamNumX;
					bool flag609 = flag608;
					bool flag610 = flag609;
					bool flag611 = flag610;
					bool flag612 = flag611;
					bool flag613 = flag612;
					bool flag614 = flag613;
					bool flag615 = flag614;
					bool flag616 = flag615;
					if (flag616)
					{
						hpConfig.stamNumX = num13;
						this.MarkHpDirty();
					}
					float num14 = this.SliderRow(e, ref num, w, rowH, fs, old, "   Offset Y (up)", 706, hpConfig.stamNumY, -200f, 200f, 1f, "0", string.Empty, " px");
					bool flag617 = num14 != hpConfig.stamNumY;
					bool flag618 = flag617;
					bool flag619 = flag618;
					bool flag620 = flag619;
					bool flag621 = flag620;
					bool flag622 = flag621;
					bool flag623 = flag622;
					bool flag624 = flag623;
					bool flag625 = flag624;
					if (flag625)
					{
						hpConfig.stamNumY = num14;
						this.MarkHpDirty();
					}
					bool flag626 = this.CheckRow(e, ref num, w, rowH, fs, old, "   Color from stamina palette", hpConfig.stamNumByPalette);
					bool flag627 = flag626;
					bool flag628 = flag627;
					bool flag629 = flag628;
					bool flag630 = flag629;
					bool flag631 = flag630;
					bool flag632 = flag631;
					bool flag633 = flag632;
					bool flag634 = flag633;
					if (flag634)
					{
						hpConfig.stamNumByPalette = !hpConfig.stamNumByPalette;
						bool flag635 = hpConfig.stamNumByPalette && this.pickExtra == 2;
						bool flag636 = flag635;
						bool flag637 = flag636;
						bool flag638 = flag637;
						bool flag639 = flag638;
						bool flag640 = flag639;
						bool flag641 = flag640;
						bool flag642 = flag641;
						bool flag643 = flag642;
						if (flag643)
						{
							this.pickExtra = 0;
						}
						this.MarkHpDirty();
					}
					bool flag644 = !hpConfig.stamNumByPalette;
					bool flag645 = flag644;
					bool flag646 = flag645;
					bool flag647 = flag646;
					bool flag648 = flag647;
					bool flag649 = flag648;
					bool flag650 = flag649;
					bool flag651 = flag650;
					bool flag652 = flag651;
					if (flag652)
					{
						this.ColorRow(e, ref num, w, rowH, fs, old, "   Color", 2);
					}
					this.HeaderRow(e, ref num, w, rowH, fs, old, "Regen countdown (shown while regen is waiting)");
					float num15 = this.SliderRow(e, ref num, w, rowH, fs, old, "   Offset Y (up)", 734, hpConfig.regenNumY, -200f, 200f, 1f, "0", string.Empty, " px");
					bool flag653 = num15 != hpConfig.regenNumY;
					bool flag654 = flag653;
					bool flag655 = flag654;
					bool flag656 = flag655;
					bool flag657 = flag656;
					bool flag658 = flag657;
					bool flag659 = flag658;
					bool flag660 = flag659;
					bool flag661 = flag660;
					if (flag661)
					{
						hpConfig.regenNumY = num15;
						this.MarkHpDirty();
					}
					this.ColorRow(e, ref num, w, rowH, fs, old, "   Color", 25);
				}
				this.HeaderRow(e, ref num, w, rowH, fs, old, "Buildings (storage, toolracks, crystals; size/format/opacity as above)");
				bool flag662 = this.CheckRow(e, ref num, w, rowH, fs, old, "HP number - own buildings", hpConfig.bldHpNumOwn);
				bool flag663 = flag662;
				bool flag664 = flag663;
				bool flag665 = flag664;
				bool flag666 = flag665;
				bool flag667 = flag666;
				bool flag668 = flag667;
				bool flag669 = flag668;
				bool flag670 = flag669;
				if (flag670)
				{
					hpConfig.bldHpNumOwn = !hpConfig.bldHpNumOwn;
					this.bldNextScan = 0f;
					this.MarkHpDirty();
				}
				bool flag671 = this.CheckRow(e, ref num, w, rowH, fs, old, "HP number - enemy buildings (visible only)", hpConfig.bldHpNumEnemy);
				bool flag672 = flag671;
				bool flag673 = flag672;
				bool flag674 = flag673;
				bool flag675 = flag674;
				bool flag676 = flag675;
				bool flag677 = flag676;
				bool flag678 = flag677;
				bool flag679 = flag678;
				if (flag679)
				{
					hpConfig.bldHpNumEnemy = !hpConfig.bldHpNumEnemy;
					this.bldNextScan = 0f;
					this.MarkHpDirty();
				}
				bool flag680 = this.CheckRow(e, ref num, w, rowH, fs, old, "Regen countdown - own buildings", hpConfig.bldRegenOwn);
				bool flag681 = flag680;
				bool flag682 = flag681;
				bool flag683 = flag682;
				bool flag684 = flag683;
				bool flag685 = flag684;
				bool flag686 = flag685;
				bool flag687 = flag686;
				bool flag688 = flag687;
				if (flag688)
				{
					hpConfig.bldRegenOwn = !hpConfig.bldRegenOwn;
					this.bldNextScan = 0f;
					this.MarkHpDirty();
				}
				bool flag689 = this.CheckRow(e, ref num, w, rowH, fs, old, "Regen countdown - enemy buildings (visible only)", hpConfig.bldRegenEnemy);
				bool flag690 = flag689;
				bool flag691 = flag690;
				bool flag692 = flag691;
				bool flag693 = flag692;
				bool flag694 = flag693;
				bool flag695 = flag694;
				bool flag696 = flag695;
				bool flag697 = flag696;
				if (flag697)
				{
					hpConfig.bldRegenEnemy = !hpConfig.bldRegenEnemy;
					this.bldNextScan = 0f;
					this.MarkHpDirty();
				}
				bool flag698 = hpConfig.bldHpNumOwn || hpConfig.bldHpNumEnemy;
				bool flag699 = flag698;
				bool flag700 = flag699;
				bool flag701 = flag700;
				bool flag702 = flag701;
				bool flag703 = flag702;
				bool flag704 = flag703;
				bool flag705 = flag704;
				bool flag706 = flag705;
				if (flag706)
				{
					float num16 = this.SliderRow(e, ref num, w, rowH, fs, old, "   HP number Offset Y (up)", 735, hpConfig.bldHpNumY, -200f, 200f, 1f, "0", string.Empty, " px");
					bool flag707 = num16 != hpConfig.bldHpNumY;
					bool flag708 = flag707;
					bool flag709 = flag708;
					bool flag710 = flag709;
					bool flag711 = flag710;
					bool flag712 = flag711;
					bool flag713 = flag712;
					bool flag714 = flag713;
					bool flag715 = flag714;
					if (flag715)
					{
						hpConfig.bldHpNumY = num16;
						this.MarkHpDirty();
					}
				}
				bool flag716 = hpConfig.bldRegenOwn || hpConfig.bldRegenEnemy;
				bool flag717 = flag716;
				bool flag718 = flag717;
				bool flag719 = flag718;
				bool flag720 = flag719;
				bool flag721 = flag720;
				bool flag722 = flag721;
				bool flag723 = flag722;
				bool flag724 = flag723;
				if (flag724)
				{
					float num17 = this.SliderRow(e, ref num, w, rowH, fs, old, "   Regen Offset Y (up)", 736, hpConfig.bldRegenY, -200f, 200f, 1f, "0", string.Empty, " px");
					bool flag725 = num17 != hpConfig.bldRegenY;
					bool flag726 = flag725;
					bool flag727 = flag726;
					bool flag728 = flag727;
					bool flag729 = flag728;
					bool flag730 = flag729;
					bool flag731 = flag730;
					bool flag732 = flag731;
					bool flag733 = flag732;
					if (flag733)
					{
						hpConfig.bldRegenY = num17;
						this.MarkHpDirty();
					}
				}
			}
			bool flag734 = this.Section(e, ref num, w, rowH, old, "Path lines (own / allied routes)", 7);
			bool flag735 = flag734;
			bool flag736 = flag735;
			bool flag737 = flag736;
			bool flag738 = flag737;
			bool flag739 = flag738;
			bool flag740 = flag739;
			bool flag741 = flag740;
			bool flag742 = flag741;
			if (flag742)
			{
				bool flag743 = this.CheckRow(e, ref num, w, rowH, fs, old, "Show routes", hpConfig.pathOwn);
				bool flag744 = flag743;
				bool flag745 = flag744;
				bool flag746 = flag745;
				bool flag747 = flag746;
				bool flag748 = flag747;
				bool flag749 = flag748;
				bool flag750 = flag749;
				bool flag751 = flag750;
				if (flag751)
				{
					hpConfig.pathOwn = !hpConfig.pathOwn;
					this.pathNextScan = 0f;
					this.MarkHpDirty();
				}
				bool pathOwn = hpConfig.pathOwn;
				bool flag752 = pathOwn;
				bool flag753 = flag752;
				bool flag754 = flag753;
				bool flag755 = flag754;
				bool flag756 = flag755;
				bool flag757 = flag756;
				bool flag758 = flag757;
				bool flag759 = flag758;
				if (flag759)
				{
					bool flag760 = this.CheckRow(e, ref num, w, rowH, fs, old, "   Only selected units", hpConfig.pathSelectedOnly);
					bool flag761 = flag760;
					bool flag762 = flag761;
					bool flag763 = flag762;
					bool flag764 = flag763;
					bool flag765 = flag764;
					bool flag766 = flag765;
					bool flag767 = flag766;
					bool flag768 = flag767;
					if (flag768)
					{
						hpConfig.pathSelectedOnly = !hpConfig.pathSelectedOnly;
						this.pathNextScan = 0f;
						this.MarkHpDirty();
					}
					bool flag769 = this.CheckRow(e, ref num, w, rowH, fs, old, "   Client: predict route from my orders", hpConfig.pathPredict);
					bool flag770 = flag769;
					bool flag771 = flag770;
					bool flag772 = flag771;
					bool flag773 = flag772;
					bool flag774 = flag773;
					bool flag775 = flag774;
					bool flag776 = flag775;
					bool flag777 = flag776;
					if (flag777)
					{
						hpConfig.pathPredict = !hpConfig.pathPredict;
						this.pathNextScan = 0f;
						this.MarkHpDirty();
					}
					bool flag778 = hpConfig.pathPredict && !NetworkServer.active;
					bool flag779 = flag778;
					bool flag780 = flag779;
					bool flag781 = flag780;
					bool flag782 = flag781;
					bool flag783 = flag782;
					bool flag784 = flag783;
					bool flag785 = flag784;
					bool flag786 = flag785;
					if (flag786)
					{
						this.HeaderRow(e, ref num, w, rowH, fs, old, BtcLang.Tr("   predict: ") + this.predDiag);
					}
					bool flag787 = this.CheckRow(e, ref num, w, rowH, fs, old, "   Client: movement trail (fallback)", hpConfig.pathTrail);
					bool flag788 = flag787;
					bool flag789 = flag788;
					bool flag790 = flag789;
					bool flag791 = flag790;
					bool flag792 = flag791;
					bool flag793 = flag792;
					bool flag794 = flag793;
					bool flag795 = flag794;
					if (flag795)
					{
						hpConfig.pathTrail = !hpConfig.pathTrail;
						this.pathNextScan = 0f;
						this.MarkHpDirty();
					}
					float num18 = this.SliderRow(e, ref num, w, rowH, fs, old, "   Line width", 707, hpConfig.pathWidth, 1f, 12f, 0.5f, "0.0", string.Empty, " px");
					bool flag796 = num18 != hpConfig.pathWidth;
					bool flag797 = flag796;
					bool flag798 = flag797;
					bool flag799 = flag798;
					bool flag800 = flag799;
					bool flag801 = flag800;
					bool flag802 = flag801;
					bool flag803 = flag802;
					bool flag804 = flag803;
					if (flag804)
					{
						hpConfig.pathWidth = num18;
						this.MarkHpDirty();
					}
					float num19 = this.SliderRow(e, ref num, w, rowH, fs, old, "   Height above ground", 708, hpConfig.pathLift, 0f, 2f, 0.05f, "0.00", string.Empty, string.Empty);
					bool flag805 = num19 != hpConfig.pathLift;
					bool flag806 = flag805;
					bool flag807 = flag806;
					bool flag808 = flag807;
					bool flag809 = flag808;
					bool flag810 = flag809;
					bool flag811 = flag810;
					bool flag812 = flag811;
					bool flag813 = flag812;
					if (flag813)
					{
						hpConfig.pathLift = num19;
						this.MarkHpDirty();
					}
					float num20 = this.SliderRow(e, ref num, w, rowH, fs, old, "   Opacity", 709, hpConfig.pathAlpha * 100f, 10f, 100f, 5f, "0", string.Empty, "%");
					bool flag814 = !Mathf.Approximately(num20 / 100f, hpConfig.pathAlpha);
					bool flag815 = flag814;
					bool flag816 = flag815;
					bool flag817 = flag816;
					bool flag818 = flag817;
					bool flag819 = flag818;
					bool flag820 = flag819;
					bool flag821 = flag820;
					bool flag822 = flag821;
					if (flag822)
					{
						hpConfig.pathAlpha = num20 / 100f;
						this.MarkHpDirty();
					}
					bool flag823 = this.CheckRow(e, ref num, w, rowH, fs, old, "   Draw over terrain", hpConfig.pathOnTop);
					bool flag824 = flag823;
					bool flag825 = flag824;
					bool flag826 = flag825;
					bool flag827 = flag826;
					bool flag828 = flag827;
					bool flag829 = flag828;
					bool flag830 = flag829;
					bool flag831 = flag830;
					if (flag831)
					{
						hpConfig.pathOnTop = !hpConfig.pathOnTop;
						this.MarkHpDirty();
					}
					bool flag832 = this.CheckRow(e, ref num, w, rowH, fs, old, "   Destination marker", hpConfig.pathMarker);
					bool flag833 = flag832;
					bool flag834 = flag833;
					bool flag835 = flag834;
					bool flag836 = flag835;
					bool flag837 = flag836;
					bool flag838 = flag837;
					bool flag839 = flag838;
					bool flag840 = flag839;
					if (flag840)
					{
						hpConfig.pathMarker = !hpConfig.pathMarker;
						this.MarkHpDirty();
					}
					this.ColorRow(e, ref num, w, rowH, fs, old, "   Color", 3);
				}
			}
			bool flag841 = this.Section(e, ref num, w, rowH, old, "Explosion map (barrels, damage to bricktrons)", 8);
			bool flag842 = flag841;
			bool flag843 = flag842;
			bool flag844 = flag843;
			bool flag845 = flag844;
			bool flag846 = flag845;
			bool flag847 = flag846;
			bool flag848 = flag847;
			bool flag849 = flag848;
			int num27;
			if (flag849)
			{
				bool flag850 = this.CheckRow(e, ref num, w, rowH, fs, old, "Show barrel explosion map", hpConfig.bombMap);
				bool flag851 = flag850;
				bool flag852 = flag851;
				bool flag853 = flag852;
				bool flag854 = flag853;
				bool flag855 = flag854;
				bool flag856 = flag855;
				bool flag857 = flag856;
				bool flag858 = flag857;
				if (flag858)
				{
					hpConfig.bombMap = !hpConfig.bombMap;
					this.bombNextScan = 0f;
					this.MarkHpDirty();
				}
				bool bombMap = hpConfig.bombMap;
				bool flag859 = bombMap;
				bool flag860 = flag859;
				bool flag861 = flag860;
				bool flag862 = flag861;
				bool flag863 = flag862;
				bool flag864 = flag863;
				bool flag865 = flag864;
				bool flag866 = flag865;
				if (flag866)
				{
					bool flag867 = this.CheckRow(e, ref num, w, rowH, fs, old, "   Performance mode (fewer barrels, 3 layers max, fewer numbers)", hpConfig.perfMode);
					bool flag868 = flag867;
					bool flag869 = flag868;
					bool flag870 = flag869;
					bool flag871 = flag870;
					bool flag872 = flag871;
					bool flag873 = flag872;
					bool flag874 = flag873;
					bool flag875 = flag874;
					if (flag875)
					{
						hpConfig.perfMode = !hpConfig.perfMode;
						this.bombNextScan = 0f;
						this.hitNextScan = 0f;
						this.crystalNextScan = 0f;
						this.MarkHpDirty();
					}
					float f = this.SliderRow(e, ref num, w, rowH, fs, old, "   Range (blocks from barrel)", 733, (float)hpConfig.bombMapRange, 1f, 8f, 1f, "0", string.Empty, " vox");
					int num21 = Mathf.Clamp(Mathf.RoundToInt(f), 1, 8);
					bool flag876 = num21 != hpConfig.bombMapRange;
					bool flag877 = flag876;
					bool flag878 = flag877;
					bool flag879 = flag878;
					bool flag880 = flag879;
					bool flag881 = flag880;
					bool flag882 = flag881;
					bool flag883 = flag882;
					bool flag884 = flag883;
					if (flag884)
					{
						hpConfig.bombMapRange = num21;
						this.MarkHpDirty();
					}
					bool flag885 = this.CheckRow(e, ref num, w, rowH, fs, old, "   3D (layers above and below)", hpConfig.bombMap3D);
					bool flag886 = flag885;
					bool flag887 = flag886;
					bool flag888 = flag887;
					bool flag889 = flag888;
					bool flag890 = flag889;
					bool flag891 = flag890;
					bool flag892 = flag891;
					bool flag893 = flag892;
					if (flag893)
					{
						hpConfig.bombMap3D = !hpConfig.bombMap3D;
						this.MarkHpDirty();
					}
					bool bombMap3D = hpConfig.bombMap3D;
					bool flag894 = bombMap3D;
					bool flag895 = flag894;
					bool flag896 = flag895;
					bool flag897 = flag896;
					bool flag898 = flag897;
					bool flag899 = flag898;
					bool flag900 = flag899;
					bool flag901 = flag900;
					if (flag901)
					{
						float f2 = this.SliderRow(e, ref num, w, rowH, fs, old, "   Layers up / down", 710, (float)hpConfig.bombMapLayers, 1f, 8f, 1f, "0", string.Empty, string.Empty);
						int num22 = Mathf.Clamp(Mathf.RoundToInt(f2), 1, 8);
						bool flag902 = num22 != hpConfig.bombMapLayers;
						bool flag903 = flag902;
						bool flag904 = flag903;
						bool flag905 = flag904;
						bool flag906 = flag905;
						bool flag907 = flag906;
						bool flag908 = flag907;
						bool flag909 = flag908;
						bool flag910 = flag909;
						if (flag910)
						{
							hpConfig.bombMapLayers = num22;
							this.MarkHpDirty();
						}
					}
					bool flag911 = this.CheckRow(e, ref num, w, rowH, fs, old, "   Numbers (damage)", hpConfig.bombMapNumbers);
					bool flag912 = flag911;
					bool flag913 = flag912;
					bool flag914 = flag913;
					bool flag915 = flag914;
					bool flag916 = flag915;
					bool flag917 = flag916;
					bool flag918 = flag917;
					bool flag919 = flag918;
					if (flag919)
					{
						hpConfig.bombMapNumbers = !hpConfig.bombMapNumbers;
						this.MarkHpDirty();
					}
					bool flag920 = hpConfig.bombMapNumbers && hpConfig.bombMap3D;
					bool flag921 = flag920;
					bool flag922 = flag921;
					bool flag923 = flag922;
					bool flag924 = flag923;
					bool flag925 = flag924;
					bool flag926 = flag925;
					bool flag927 = flag926;
					bool flag928 = flag927;
					if (flag928)
					{
						bool flag929 = this.CheckRow(e, ref num, w, rowH, fs, old, "      Numbers on every voxel (all layers)", hpConfig.bombMapAllNumbers);
						bool flag930 = flag929;
						bool flag931 = flag930;
						bool flag932 = flag931;
						bool flag933 = flag932;
						bool flag934 = flag933;
						bool flag935 = flag934;
						bool flag936 = flag935;
						bool flag937 = flag936;
						if (flag937)
						{
							hpConfig.bombMapAllNumbers = !hpConfig.bombMapAllNumbers;
							this.MarkHpDirty();
						}
					}
					bool bombMapNumbers = hpConfig.bombMapNumbers;
					bool flag938 = bombMapNumbers;
					bool flag939 = flag938;
					bool flag940 = flag939;
					bool flag941 = flag940;
					bool flag942 = flag941;
					bool flag943 = flag942;
					bool flag944 = flag943;
					bool flag945 = flag944;
					if (flag945)
					{
						float num23 = this.SliderRow(e, ref num, w, rowH, fs, old, "      Numbers opacity", 720, hpConfig.bombNumAlpha * 100f, 0f, 100f, 1f, "0", string.Empty, "%");
						bool flag946 = !Mathf.Approximately(num23 / 100f, hpConfig.bombNumAlpha);
						bool flag947 = flag946;
						bool flag948 = flag947;
						bool flag949 = flag948;
						bool flag950 = flag949;
						bool flag951 = flag950;
						bool flag952 = flag951;
						bool flag953 = flag952;
						bool flag954 = flag953;
						if (flag954)
						{
							hpConfig.bombNumAlpha = num23 / 100f;
							this.MarkHpDirty();
						}
						float num24 = this.SliderRow(e, ref num, w, rowH, fs, old, "      Hide numbers if camera farther", 731, hpConfig.bombNumDist, 5f, 150f, 5f, "0", string.Empty, " vox");
						bool flag955 = !Mathf.Approximately(num24, hpConfig.bombNumDist);
						bool flag956 = flag955;
						bool flag957 = flag956;
						bool flag958 = flag957;
						bool flag959 = flag958;
						bool flag960 = flag959;
						bool flag961 = flag960;
						bool flag962 = flag961;
						bool flag963 = flag962;
						if (flag963)
						{
							hpConfig.bombNumDist = num24;
							this.MarkHpDirty();
						}
					}
					float num25 = this.SliderRow(e, ref num, w, rowH, fs, old, "   Height offset", 712, hpConfig.bombMapYOffset, -3f, 6f, 0.05f, "0.00", string.Empty, " vox");
					bool flag964 = !Mathf.Approximately(num25, hpConfig.bombMapYOffset);
					bool flag965 = flag964;
					bool flag966 = flag965;
					bool flag967 = flag966;
					bool flag968 = flag967;
					bool flag969 = flag968;
					bool flag970 = flag969;
					bool flag971 = flag970;
					bool flag972 = flag971;
					if (flag972)
					{
						hpConfig.bombMapYOffset = num25;
						this.MarkHpDirty();
					}
					float num26 = this.SliderRow(e, ref num, w, rowH, fs, old, "   Cells opacity", 711, hpConfig.bombMapAlpha * 100f, 0f, 100f, 1f, "0", string.Empty, "%");
					bool flag973 = !Mathf.Approximately(num26 / 100f, hpConfig.bombMapAlpha);
					bool flag974 = flag973;
					bool flag975 = flag974;
					bool flag976 = flag975;
					bool flag977 = flag976;
					bool flag978 = flag977;
					bool flag979 = flag978;
					bool flag980 = flag979;
					bool flag981 = flag980;
					if (flag981)
					{
						hpConfig.bombMapAlpha = num26 / 100f;
						this.MarkHpDirty();
					}
					string[] array2 = new string[]
					{
						"Grey-white",
						"Pink",
						"Blue",
						"Green",
						"Yellow",
						"Dark orange",
						"Red",
						"Dark red"
					};
					this.HeaderRow(e, ref num, w, rowH, fs, old, "   Show only these colors (hidden ones are invisible, numbers too)");
					for (int i = 0; i < 8; i = num27 + 1)
					{
						bool flag982 = this.CheckRow(e, ref num, w, rowH, fs, old, "      " + array2[i], (hpConfig.bombBandMask >> i & 1) != 0);
						bool flag983 = flag982;
						bool flag984 = flag983;
						bool flag985 = flag984;
						bool flag986 = flag985;
						bool flag987 = flag986;
						bool flag988 = flag987;
						bool flag989 = flag988;
						bool flag990 = flag989;
						if (flag990)
						{
							ResourcePackManager.HpConfig hpConfig2 = hpConfig;
							ResourcePackManager.HpConfig hpConfig3 = hpConfig2;
							ResourcePackManager.HpConfig hpConfig4 = hpConfig3;
							hpConfig4.bombBandMask ^= 1 << i;
							this.MarkHpDirty();
						}
						num27 = i;
					}
					bool flag991 = this.CheckRow(e, ref num, w, rowH, fs, old, "   Colors by fixed damage (not % of max)", hpConfig.bombByDamage);
					bool flag992 = flag991;
					bool flag993 = flag992;
					bool flag994 = flag993;
					bool flag995 = flag994;
					bool flag996 = flag995;
					bool flag997 = flag996;
					bool flag998 = flag997;
					bool flag999 = flag998;
					if (flag999)
					{
						hpConfig.bombByDamage = !hpConfig.bombByDamage;
						this.MarkHpDirty();
					}
					this.HeaderRow(e, ref num, w, rowH, fs, old, "   Colors (weak -> strong damage)");
					for (int j = 0; j < 8; j = num27 + 1)
					{
						this.ColorRow(e, ref num, w, rowH, fs, old, "      " + array2[j], 4 + j);
						bool flag1000 = j >= 1 && hpConfig.bombByDamage;
						bool flag1001 = flag1000;
						bool flag1002 = flag1001;
						bool flag1003 = flag1002;
						bool flag1004 = flag1003;
						bool flag1005 = flag1004;
						bool flag1006 = flag1005;
						bool flag1007 = flag1006;
						bool flag1008 = flag1007;
						if (flag1008)
						{
							float min = (j >= 2) ? hpConfig.bombDmg[j - 2] : 1f;
							float max = (j <= 6) ? hpConfig.bombDmg[j] : 4000f;
							float num28 = this.SliderRow(e, ref num, w, rowH, fs, old, "         starts at", 740 + j, hpConfig.bombDmg[j - 1], 1f, 4000f, 1f, "0", string.Empty, " damage");
							num28 = Mathf.Clamp(num28, min, max);
							bool flag1009 = !Mathf.Approximately(num28, hpConfig.bombDmg[j - 1]);
							bool flag1010 = flag1009;
							bool flag1011 = flag1010;
							bool flag1012 = flag1011;
							bool flag1013 = flag1012;
							bool flag1014 = flag1013;
							bool flag1015 = flag1014;
							bool flag1016 = flag1015;
							bool flag1017 = flag1016;
							if (flag1017)
							{
								hpConfig.bombDmg[j - 1] = num28;
								this.MarkHpDirty();
							}
						}
						else
						{
							bool flag1018 = j >= 1;
							bool flag1019 = flag1018;
							bool flag1020 = flag1019;
							bool flag1021 = flag1020;
							bool flag1022 = flag1021;
							bool flag1023 = flag1022;
							bool flag1024 = flag1023;
							bool flag1025 = flag1024;
							bool flag1026 = flag1025;
							if (flag1026)
							{
								float min2 = (j >= 2) ? hpConfig.bombThr[j - 2] : 0f;
								float max2 = (j <= 6) ? hpConfig.bombThr[j] : 100f;
								float num29 = this.SliderRow(e, ref num, w, rowH, fs, old, "         starts at", 712 + j, hpConfig.bombThr[j - 1], 0.1f, 100f, 0.1f, "0.0", string.Empty, "% of max");
								num29 = Mathf.Clamp(num29, min2, max2);
								bool flag1027 = !Mathf.Approximately(num29, hpConfig.bombThr[j - 1]);
								bool flag1028 = flag1027;
								bool flag1029 = flag1028;
								bool flag1030 = flag1029;
								bool flag1031 = flag1030;
								bool flag1032 = flag1031;
								bool flag1033 = flag1032;
								bool flag1034 = flag1033;
								bool flag1035 = flag1034;
								if (flag1035)
								{
									hpConfig.bombThr[j - 1] = num29;
									this.MarkHpDirty();
								}
							}
						}
						num27 = j;
					}
				}
			}
			bool flag1036 = this.Section(e, ref num, w, rowH, old, "Damage counter / crystal timer", 9);
			bool flag1037 = flag1036;
			bool flag1038 = flag1037;
			bool flag1039 = flag1038;
			bool flag1040 = flag1039;
			bool flag1041 = flag1040;
			bool flag1042 = flag1041;
			bool flag1043 = flag1042;
			bool flag1044 = flag1043;
			if (flag1044)
			{
				this.HeaderRow(e, ref num, w, rowH, fs, old, "Hit damage counter (bricktrons, buildings)");
				bool flag1045 = this.CheckRow(e, ref num, w, rowH, fs, old, "Performance mode (lighter on FPS)", hpConfig.perfMode);
				bool flag1046 = flag1045;
				bool flag1047 = flag1046;
				bool flag1048 = flag1047;
				bool flag1049 = flag1048;
				bool flag1050 = flag1049;
				bool flag1051 = flag1050;
				bool flag1052 = flag1051;
				bool flag1053 = flag1052;
				if (flag1053)
				{
					hpConfig.perfMode = !hpConfig.perfMode;
					this.bombNextScan = 0f;
					this.hitNextScan = 0f;
					this.crystalNextScan = 0f;
					this.MarkHpDirty();
				}
				bool flag1054 = this.CheckRow(e, ref num, w, rowH, fs, old, "HP damage - own / allied", hpConfig.hitOwn);
				bool flag1055 = flag1054;
				bool flag1056 = flag1055;
				bool flag1057 = flag1056;
				bool flag1058 = flag1057;
				bool flag1059 = flag1058;
				bool flag1060 = flag1059;
				bool flag1061 = flag1060;
				bool flag1062 = flag1061;
				if (flag1062)
				{
					hpConfig.hitOwn = !hpConfig.hitOwn;
					this.hitNextScan = 0f;
					this.MarkHpDirty();
				}
				bool flag1063 = this.CheckRow(e, ref num, w, rowH, fs, old, "HP damage - enemies", hpConfig.hitEnemy);
				bool flag1064 = flag1063;
				bool flag1065 = flag1064;
				bool flag1066 = flag1065;
				bool flag1067 = flag1066;
				bool flag1068 = flag1067;
				bool flag1069 = flag1068;
				bool flag1070 = flag1069;
				bool flag1071 = flag1070;
				if (flag1071)
				{
					hpConfig.hitEnemy = !hpConfig.hitEnemy;
					this.hitNextScan = 0f;
					this.MarkHpDirty();
				}
				bool flag1072 = this.CheckRow(e, ref num, w, rowH, fs, old, "Stamina damage - own / allied", hpConfig.hitStamOwn);
				bool flag1073 = flag1072;
				bool flag1074 = flag1073;
				bool flag1075 = flag1074;
				bool flag1076 = flag1075;
				bool flag1077 = flag1076;
				bool flag1078 = flag1077;
				bool flag1079 = flag1078;
				bool flag1080 = flag1079;
				if (flag1080)
				{
					hpConfig.hitStamOwn = !hpConfig.hitStamOwn;
					this.hitNextScan = 0f;
					this.MarkHpDirty();
				}
				bool flag1081 = this.CheckRow(e, ref num, w, rowH, fs, old, "Stamina damage - enemies", hpConfig.hitStamEnemy);
				bool flag1082 = flag1081;
				bool flag1083 = flag1082;
				bool flag1084 = flag1083;
				bool flag1085 = flag1084;
				bool flag1086 = flag1085;
				bool flag1087 = flag1086;
				bool flag1088 = flag1087;
				bool flag1089 = flag1088;
				if (flag1089)
				{
					hpConfig.hitStamEnemy = !hpConfig.hitStamEnemy;
					this.hitNextScan = 0f;
					this.MarkHpDirty();
				}
				bool flag1090 = this.CheckRow(e, ref num, w, rowH, fs, old, "Buildings damage - own", hpConfig.hitBldOwn);
				bool flag1091 = flag1090;
				bool flag1092 = flag1091;
				bool flag1093 = flag1092;
				bool flag1094 = flag1093;
				bool flag1095 = flag1094;
				bool flag1096 = flag1095;
				bool flag1097 = flag1096;
				bool flag1098 = flag1097;
				if (flag1098)
				{
					hpConfig.hitBldOwn = !hpConfig.hitBldOwn;
					this.hitNextScan = 0f;
					this.MarkHpDirty();
				}
				bool flag1099 = this.CheckRow(e, ref num, w, rowH, fs, old, "Buildings damage - enemy (visible only)", hpConfig.hitBldEnemy);
				bool flag1100 = flag1099;
				bool flag1101 = flag1100;
				bool flag1102 = flag1101;
				bool flag1103 = flag1102;
				bool flag1104 = flag1103;
				bool flag1105 = flag1104;
				bool flag1106 = flag1105;
				bool flag1107 = flag1106;
				if (flag1107)
				{
					hpConfig.hitBldEnemy = !hpConfig.hitBldEnemy;
					this.hitNextScan = 0f;
					this.MarkHpDirty();
				}
				bool flag1108 = hpConfig.hitOwn || hpConfig.hitEnemy || hpConfig.hitStamOwn || hpConfig.hitStamEnemy;
				bool flag1109 = hpConfig.hitBldOwn || hpConfig.hitBldEnemy;
				bool flag1110 = flag1108 || flag1109;
				bool flag1111 = flag1110;
				bool flag1112 = flag1111;
				bool flag1113 = flag1112;
				bool flag1114 = flag1113;
				bool flag1115 = flag1114;
				bool flag1116 = flag1115;
				bool flag1117 = flag1116;
				bool flag1118 = flag1117;
				if (flag1118)
				{
					bool flag1119 = flag1108;
					bool flag1120 = flag1119;
					bool flag1121 = flag1120;
					bool flag1122 = flag1121;
					bool flag1123 = flag1122;
					bool flag1124 = flag1123;
					bool flag1125 = flag1124;
					bool flag1126 = flag1125;
					bool flag1127 = flag1126;
					if (flag1127)
					{
						bool flag1128 = this.CheckRow(e, ref num, w, rowH, fs, old, "   Auto DPS after the fight ends - bricktrons", hpConfig.hitDps);
						bool flag1129 = flag1128;
						bool flag1130 = flag1129;
						bool flag1131 = flag1130;
						bool flag1132 = flag1131;
						bool flag1133 = flag1132;
						bool flag1134 = flag1133;
						bool flag1135 = flag1134;
						bool flag1136 = flag1135;
						if (flag1136)
						{
							hpConfig.hitDps = !hpConfig.hitDps;
							this.MarkHpDirty();
						}
						bool hitDps = hpConfig.hitDps;
						bool flag1137 = hitDps;
						bool flag1138 = flag1137;
						bool flag1139 = flag1138;
						bool flag1140 = flag1139;
						bool flag1141 = flag1140;
						bool flag1142 = flag1141;
						bool flag1143 = flag1142;
						bool flag1144 = flag1143;
						if (flag1144)
						{
							bool flag1145 = this.CheckRow(e, ref num, w, rowH, fs, old, "      DPS counts stamina damage + HP together", hpConfig.dpsStamina);
							bool flag1146 = flag1145;
							bool flag1147 = flag1146;
							bool flag1148 = flag1147;
							bool flag1149 = flag1148;
							bool flag1150 = flag1149;
							bool flag1151 = flag1150;
							bool flag1152 = flag1151;
							bool flag1153 = flag1152;
							if (flag1153)
							{
								hpConfig.dpsStamina = !hpConfig.dpsStamina;
								this.MarkHpDirty();
							}
						}
					}
					bool flag1154 = flag1109;
					bool flag1155 = flag1154;
					bool flag1156 = flag1155;
					bool flag1157 = flag1156;
					bool flag1158 = flag1157;
					bool flag1159 = flag1158;
					bool flag1160 = flag1159;
					bool flag1161 = flag1160;
					bool flag1162 = flag1161;
					if (flag1162)
					{
						bool flag1163 = this.CheckRow(e, ref num, w, rowH, fs, old, "   Auto DPS after the fight ends - buildings", hpConfig.hitBldDps);
						bool flag1164 = flag1163;
						bool flag1165 = flag1164;
						bool flag1166 = flag1165;
						bool flag1167 = flag1166;
						bool flag1168 = flag1167;
						bool flag1169 = flag1168;
						bool flag1170 = flag1169;
						bool flag1171 = flag1170;
						if (flag1171)
						{
							hpConfig.hitBldDps = !hpConfig.hitBldDps;
							this.MarkHpDirty();
						}
					}
					bool flag1172 = (flag1108 && hpConfig.hitDps) || (flag1109 && hpConfig.hitBldDps);
					bool flag1173 = flag1172;
					bool flag1174 = flag1173;
					bool flag1175 = flag1174;
					bool flag1176 = flag1175;
					bool flag1177 = flag1176;
					bool flag1178 = flag1177;
					bool flag1179 = flag1178;
					bool flag1180 = flag1179;
					if (flag1180)
					{
						bool flag1181 = this.CheckRow(e, ref num, w, rowH, fs, old, "      Super precision (DPS to thousandths)", hpConfig.dpsPrecise);
						bool flag1182 = flag1181;
						bool flag1183 = flag1182;
						bool flag1184 = flag1183;
						bool flag1185 = flag1184;
						bool flag1186 = flag1185;
						bool flag1187 = flag1186;
						bool flag1188 = flag1187;
						bool flag1189 = flag1188;
						if (flag1189)
						{
							hpConfig.dpsPrecise = !hpConfig.dpsPrecise;
							this.MarkHpDirty();
						}
						float num30 = this.SliderRow(e, ref num, w, rowH, fs, old, "      Fight over after no damage for", 721, hpConfig.dpsSilence, 1f, 15f, 0.5f, "0.0", string.Empty, " s");
						bool flag1190 = !Mathf.Approximately(num30, hpConfig.dpsSilence);
						bool flag1191 = flag1190;
						bool flag1192 = flag1191;
						bool flag1193 = flag1192;
						bool flag1194 = flag1193;
						bool flag1195 = flag1194;
						bool flag1196 = flag1195;
						bool flag1197 = flag1196;
						bool flag1198 = flag1197;
						if (flag1198)
						{
							hpConfig.dpsSilence = num30;
							this.MarkHpDirty();
						}
						float num31 = this.SliderRow(e, ref num, w, rowH, fs, old, "      DPS stays for", 722, hpConfig.dpsShow, 1f, 10f, 0.5f, "0.0", string.Empty, " s");
						bool flag1199 = !Mathf.Approximately(num31, hpConfig.dpsShow);
						bool flag1200 = flag1199;
						bool flag1201 = flag1200;
						bool flag1202 = flag1201;
						bool flag1203 = flag1202;
						bool flag1204 = flag1203;
						bool flag1205 = flag1204;
						bool flag1206 = flag1205;
						bool flag1207 = flag1206;
						if (flag1207)
						{
							hpConfig.dpsShow = num31;
							this.MarkHpDirty();
						}
					}
					bool flag1208 = this.CheckRow(e, ref num, w, rowH, fs, old, "   Count the killing blow in full (no overkill loss)", hpConfig.hitFullDamage);
					bool flag1209 = flag1208;
					bool flag1210 = flag1209;
					bool flag1211 = flag1210;
					bool flag1212 = flag1211;
					bool flag1213 = flag1212;
					bool flag1214 = flag1213;
					bool flag1215 = flag1214;
					bool flag1216 = flag1215;
					if (flag1216)
					{
						hpConfig.hitFullDamage = !hpConfig.hitFullDamage;
						this.MarkHpDirty();
					}
					float num32 = this.SliderRow(e, ref num, w, rowH, fs, old, "   Number stays for", 723, hpConfig.hitTime, 0.3f, 3f, 0.1f, "0.0", string.Empty, " s");
					bool flag1217 = !Mathf.Approximately(num32, hpConfig.hitTime);
					bool flag1218 = flag1217;
					bool flag1219 = flag1218;
					bool flag1220 = flag1219;
					bool flag1221 = flag1220;
					bool flag1222 = flag1221;
					bool flag1223 = flag1222;
					bool flag1224 = flag1223;
					bool flag1225 = flag1224;
					if (flag1225)
					{
						hpConfig.hitTime = num32;
						this.MarkHpDirty();
					}
					float num33 = this.SliderRow(e, ref num, w, rowH, fs, old, "   Size", 724, hpConfig.hitSize, 0.3f, 3f, 0.05f, "0.00", "x", string.Empty);
					bool flag1226 = !Mathf.Approximately(num33, hpConfig.hitSize);
					bool flag1227 = flag1226;
					bool flag1228 = flag1227;
					bool flag1229 = flag1228;
					bool flag1230 = flag1229;
					bool flag1231 = flag1230;
					bool flag1232 = flag1231;
					bool flag1233 = flag1232;
					bool flag1234 = flag1233;
					if (flag1234)
					{
						hpConfig.hitSize = num33;
						this.MarkHpDirty();
					}
					float num34 = this.SliderRow(e, ref num, w, rowH, fs, old, "   Offset X (stamina numbers go to the left)", 725, hpConfig.hitX, -200f, 200f, 1f, "0", string.Empty, " px");
					bool flag1235 = !Mathf.Approximately(num34, hpConfig.hitX);
					bool flag1236 = flag1235;
					bool flag1237 = flag1236;
					bool flag1238 = flag1237;
					bool flag1239 = flag1238;
					bool flag1240 = flag1239;
					bool flag1241 = flag1240;
					bool flag1242 = flag1241;
					bool flag1243 = flag1242;
					if (flag1243)
					{
						hpConfig.hitX = num34;
						this.MarkHpDirty();
					}
					float num35 = this.SliderRow(e, ref num, w, rowH, fs, old, "   Offset Y (up)", 726, hpConfig.hitY, -200f, 200f, 1f, "0", string.Empty, " px");
					bool flag1244 = !Mathf.Approximately(num35, hpConfig.hitY);
					bool flag1245 = flag1244;
					bool flag1246 = flag1245;
					bool flag1247 = flag1246;
					bool flag1248 = flag1247;
					bool flag1249 = flag1248;
					bool flag1250 = flag1249;
					bool flag1251 = flag1250;
					bool flag1252 = flag1251;
					if (flag1252)
					{
						hpConfig.hitY = num35;
						this.MarkHpDirty();
					}
					this.ColorRow(e, ref num, w, rowH, fs, old, "   Color own / allied", 20);
					this.ColorRow(e, ref num, w, rowH, fs, old, "   Color enemy", 21);
					bool flag1253 = hpConfig.hitStamOwn || hpConfig.hitStamEnemy;
					bool flag1254 = flag1253;
					bool flag1255 = flag1254;
					bool flag1256 = flag1255;
					bool flag1257 = flag1256;
					bool flag1258 = flag1257;
					bool flag1259 = flag1258;
					bool flag1260 = flag1259;
					bool flag1261 = flag1260;
					if (flag1261)
					{
						this.ColorRow(e, ref num, w, rowH, fs, old, "   Color stamina damage", 26);
					}
					bool flag1262 = (flag1108 && hpConfig.hitDps) || (flag1109 && hpConfig.hitBldDps);
					bool flag1263 = flag1262;
					bool flag1264 = flag1263;
					bool flag1265 = flag1264;
					bool flag1266 = flag1265;
					bool flag1267 = flag1266;
					bool flag1268 = flag1267;
					bool flag1269 = flag1268;
					bool flag1270 = flag1269;
					if (flag1270)
					{
						this.ColorRow(e, ref num, w, rowH, fs, old, "   DPS color", 22);
					}
				}
				this.HeaderRow(e, ref num, w, rowH, fs, old, "Crystal capture timer");
				bool flag1271 = this.CheckRow(e, ref num, w, rowH, fs, old, "Show seconds to capture / to lose", hpConfig.crystalTimer);
				bool flag1272 = flag1271;
				bool flag1273 = flag1272;
				bool flag1274 = flag1273;
				bool flag1275 = flag1274;
				bool flag1276 = flag1275;
				bool flag1277 = flag1276;
				bool flag1278 = flag1277;
				bool flag1279 = flag1278;
				if (flag1279)
				{
					hpConfig.crystalTimer = !hpConfig.crystalTimer;
					this.crystalNextScan = 0f;
					this.MarkHpDirty();
				}
				bool crystalTimer = hpConfig.crystalTimer;
				bool flag1280 = crystalTimer;
				bool flag1281 = flag1280;
				bool flag1282 = flag1281;
				bool flag1283 = flag1282;
				bool flag1284 = flag1283;
				bool flag1285 = flag1284;
				bool flag1286 = flag1285;
				bool flag1287 = flag1286;
				if (flag1287)
				{
					float num36 = this.SliderRow(e, ref num, w, rowH, fs, old, "   Size", 727, hpConfig.crystalSize, 0.3f, 4f, 0.05f, "0.00", "x", string.Empty);
					bool flag1288 = !Mathf.Approximately(num36, hpConfig.crystalSize);
					bool flag1289 = flag1288;
					bool flag1290 = flag1289;
					bool flag1291 = flag1290;
					bool flag1292 = flag1291;
					bool flag1293 = flag1292;
					bool flag1294 = flag1293;
					bool flag1295 = flag1294;
					bool flag1296 = flag1295;
					if (flag1296)
					{
						hpConfig.crystalSize = num36;
						this.MarkHpDirty();
					}
					float num37 = this.SliderRow(e, ref num, w, rowH, fs, old, "   Offset X", 728, hpConfig.crystalX, -200f, 200f, 1f, "0", string.Empty, " px");
					bool flag1297 = !Mathf.Approximately(num37, hpConfig.crystalX);
					bool flag1298 = flag1297;
					bool flag1299 = flag1298;
					bool flag1300 = flag1299;
					bool flag1301 = flag1300;
					bool flag1302 = flag1301;
					bool flag1303 = flag1302;
					bool flag1304 = flag1303;
					bool flag1305 = flag1304;
					if (flag1305)
					{
						hpConfig.crystalX = num37;
						this.MarkHpDirty();
					}
					float num38 = this.SliderRow(e, ref num, w, rowH, fs, old, "   Offset Y (up)", 729, hpConfig.crystalY, -200f, 200f, 1f, "0", string.Empty, " px");
					bool flag1306 = !Mathf.Approximately(num38, hpConfig.crystalY);
					bool flag1307 = flag1306;
					bool flag1308 = flag1307;
					bool flag1309 = flag1308;
					bool flag1310 = flag1309;
					bool flag1311 = flag1310;
					bool flag1312 = flag1311;
					bool flag1313 = flag1312;
					bool flag1314 = flag1313;
					if (flag1314)
					{
						hpConfig.crystalY = num38;
						this.MarkHpDirty();
					}
					float num39 = this.SliderRow(e, ref num, w, rowH, fs, old, "   Height above crystal", 730, hpConfig.crystalLift, 0f, 8f, 0.1f, "0.0", string.Empty, " vox");
					bool flag1315 = !Mathf.Approximately(num39, hpConfig.crystalLift);
					bool flag1316 = flag1315;
					bool flag1317 = flag1316;
					bool flag1318 = flag1317;
					bool flag1319 = flag1318;
					bool flag1320 = flag1319;
					bool flag1321 = flag1320;
					bool flag1322 = flag1321;
					bool flag1323 = flag1322;
					if (flag1323)
					{
						hpConfig.crystalLift = num39;
						this.MarkHpDirty();
					}
					this.ColorRow(e, ref num, w, rowH, fs, old, "   Good for me (I capture / keep)", 23);
					this.ColorRow(e, ref num, w, rowH, fs, old, "   Bad for me (enemy captures / I lose)", 24);
				}
			}
			bool flag1324 = this.Section(e, ref num, w, rowH, old, "Entity center (explosion aim point)", 10);
			bool flag1325 = flag1324;
			bool flag1326 = flag1325;
			bool flag1327 = flag1326;
			bool flag1328 = flag1327;
			bool flag1329 = flag1328;
			bool flag1330 = flag1329;
			bool flag1331 = flag1330;
			bool flag1332 = flag1331;
			if (flag1332)
			{
				bool flag1333 = this.CheckRow(e, ref num, w, rowH, fs, old, "Show entity center (dot)", hpConfig.centerOn);
				bool flag1334 = flag1333;
				bool flag1335 = flag1334;
				bool flag1336 = flag1335;
				bool flag1337 = flag1336;
				bool flag1338 = flag1337;
				bool flag1339 = flag1338;
				bool flag1340 = flag1339;
				bool flag1341 = flag1340;
				if (flag1341)
				{
					hpConfig.centerOn = !hpConfig.centerOn;
					this.centerNextScan = 0f;
					this.MarkHpDirty();
				}
				bool flag1342 = this.CheckRow(e, ref num, w, rowH, fs, old, "Show explosion distance to the center (number after a blast)  [HEAVY: loads the game a lot]", hpConfig.centerDist);
				bool flag1343 = flag1342;
				bool flag1344 = flag1343;
				bool flag1345 = flag1344;
				bool flag1346 = flag1345;
				bool flag1347 = flag1346;
				bool flag1348 = flag1347;
				bool flag1349 = flag1348;
				bool flag1350 = flag1349;
				if (flag1350)
				{
					hpConfig.centerDist = !hpConfig.centerDist;
					this.centerNextScan = 0f;
					this.MarkHpDirty();
				}
				bool flag1351 = hpConfig.centerOn || hpConfig.centerDist;
				bool flag1352 = flag1351;
				bool flag1353 = flag1352;
				bool flag1354 = flag1353;
				bool flag1355 = flag1354;
				bool flag1356 = flag1355;
				bool flag1357 = flag1356;
				bool flag1358 = flag1357;
				bool flag1359 = flag1358;
				if (flag1359)
				{
					this.HeaderRow(e, ref num, w, rowH, fs, old, "Explosion damage is measured to the nearest dot of the target");
					bool flag1360 = this.CheckRow(e, ref num, w, rowH, fs, old, "   Units - own / allied", hpConfig.centerOwnUnits);
					bool flag1361 = flag1360;
					bool flag1362 = flag1361;
					bool flag1363 = flag1362;
					bool flag1364 = flag1363;
					bool flag1365 = flag1364;
					bool flag1366 = flag1365;
					bool flag1367 = flag1366;
					bool flag1368 = flag1367;
					if (flag1368)
					{
						hpConfig.centerOwnUnits = !hpConfig.centerOwnUnits;
						this.centerNextScan = 0f;
						this.MarkHpDirty();
					}
					bool flag1369 = this.CheckRow(e, ref num, w, rowH, fs, old, "   Units - enemy (visible only)", hpConfig.centerEnemyUnits);
					bool flag1370 = flag1369;
					bool flag1371 = flag1370;
					bool flag1372 = flag1371;
					bool flag1373 = flag1372;
					bool flag1374 = flag1373;
					bool flag1375 = flag1374;
					bool flag1376 = flag1375;
					bool flag1377 = flag1376;
					if (flag1377)
					{
						hpConfig.centerEnemyUnits = !hpConfig.centerEnemyUnits;
						this.centerNextScan = 0f;
						this.MarkHpDirty();
					}
					bool flag1378 = this.CheckRow(e, ref num, w, rowH, fs, old, "   Buildings / objects - own", hpConfig.centerOwnObjects);
					bool flag1379 = flag1378;
					bool flag1380 = flag1379;
					bool flag1381 = flag1380;
					bool flag1382 = flag1381;
					bool flag1383 = flag1382;
					bool flag1384 = flag1383;
					bool flag1385 = flag1384;
					bool flag1386 = flag1385;
					if (flag1386)
					{
						hpConfig.centerOwnObjects = !hpConfig.centerOwnObjects;
						this.centerNextScan = 0f;
						this.MarkHpDirty();
					}
					bool flag1387 = this.CheckRow(e, ref num, w, rowH, fs, old, "   Buildings / objects - enemy (visible only)", hpConfig.centerEnemyObjects);
					bool flag1388 = flag1387;
					bool flag1389 = flag1388;
					bool flag1390 = flag1389;
					bool flag1391 = flag1390;
					bool flag1392 = flag1391;
					bool flag1393 = flag1392;
					bool flag1394 = flag1393;
					bool flag1395 = flag1394;
					if (flag1395)
					{
						hpConfig.centerEnemyObjects = !hpConfig.centerEnemyObjects;
						this.centerNextScan = 0f;
						this.MarkHpDirty();
					}
					bool centerDist = hpConfig.centerDist;
					bool flag1396 = centerDist;
					bool flag1397 = flag1396;
					bool flag1398 = flag1397;
					bool flag1399 = flag1398;
					bool flag1400 = flag1399;
					bool flag1401 = flag1400;
					bool flag1402 = flag1401;
					bool flag1403 = flag1402;
					if (flag1403)
					{
						float num40 = this.SliderRow(e, ref num, w, rowH, fs, old, "   Distance number size", 743, hpConfig.centerDistSize, 0.3f, 3f, 0.05f, "0.00", "x", string.Empty);
						bool flag1404 = !Mathf.Approximately(num40, hpConfig.centerDistSize);
						bool flag1405 = flag1404;
						bool flag1406 = flag1405;
						bool flag1407 = flag1406;
						bool flag1408 = flag1407;
						bool flag1409 = flag1408;
						bool flag1410 = flag1409;
						bool flag1411 = flag1410;
						bool flag1412 = flag1411;
						if (flag1412)
						{
							hpConfig.centerDistSize = num40;
							this.MarkHpDirty();
						}
						float num41 = this.SliderRow(e, ref num, w, rowH, fs, old, "   Distance number stays for", 744, hpConfig.centerDistTime, 0.5f, 6f, 0.1f, "0.0", string.Empty, " s");
						bool flag1413 = !Mathf.Approximately(num41, hpConfig.centerDistTime);
						bool flag1414 = flag1413;
						bool flag1415 = flag1414;
						bool flag1416 = flag1415;
						bool flag1417 = flag1416;
						bool flag1418 = flag1417;
						bool flag1419 = flag1418;
						bool flag1420 = flag1419;
						bool flag1421 = flag1420;
						if (flag1421)
						{
							hpConfig.centerDistTime = num41;
							this.MarkHpDirty();
						}
					}
					float num42 = this.SliderRow(e, ref num, w, rowH, fs, old, "   Dot size", 741, hpConfig.centerSize, 0.3f, 4f, 0.05f, "0.00", "x", string.Empty);
					bool flag1422 = !Mathf.Approximately(num42, hpConfig.centerSize);
					bool flag1423 = flag1422;
					bool flag1424 = flag1423;
					bool flag1425 = flag1424;
					bool flag1426 = flag1425;
					bool flag1427 = flag1426;
					bool flag1428 = flag1427;
					bool flag1429 = flag1428;
					bool flag1430 = flag1429;
					if (flag1430)
					{
						hpConfig.centerSize = num42;
						this.MarkHpDirty();
					}
					float num43 = this.SliderRow(e, ref num, w, rowH, fs, old, "   Opacity", 742, hpConfig.centerAlpha * 100f, 10f, 100f, 5f, "0", string.Empty, "%");
					bool flag1431 = !Mathf.Approximately(num43 / 100f, hpConfig.centerAlpha);
					bool flag1432 = flag1431;
					bool flag1433 = flag1432;
					bool flag1434 = flag1433;
					bool flag1435 = flag1434;
					bool flag1436 = flag1435;
					bool flag1437 = flag1436;
					bool flag1438 = flag1437;
					bool flag1439 = flag1438;
					if (flag1439)
					{
						hpConfig.centerAlpha = num43 / 100f;
						this.MarkHpDirty();
					}
					this.ColorRow(e, ref num, w, rowH, fs, old, "   Color own / allied", 27);
					this.ColorRow(e, ref num, w, rowH, fs, old, "   Color enemy", 28);
				}
			}
			for (int k = 0; k < 5; k = num27 + 1)
			{
				bool flag1440 = this.Section(e, ref num, w, rowH, old, BtcOverlays.Names[k], 11 + k);
				bool flag1441 = flag1440;
				bool flag1442 = flag1441;
				bool flag1443 = flag1442;
				bool flag1444 = flag1443;
				bool flag1445 = flag1444;
				bool flag1446 = flag1445;
				if (flag1446)
				{
					this.DrawOverlaySection(e, ref num, w, rowH, fs, old, k);
				}
				num27 = k;
			}
			if (this.Section(e, ref num, w, rowH, old, "Interface", 16))
			{
				this.DrawUnitPlatesSection(e, ref num, w, rowH, fs, old);
			}
			if (this.Section(e, ref num, w, rowH, old, "Match statistics", 17))
			{
				this.DrawMatchStatsSection(e, ref num, w, rowH, fs, old);
			}
			if (this.Section(e, ref num, w, rowH, old, "Chat", 18))
			{
				this.DrawChatSection(e, ref num, w, rowH, fs, old);
			}
			bool flag1447 = this.Section(e, ref num, w, rowH, old, "Other", 4);
			bool flag1448 = flag1447;
			bool flag1449 = flag1448;
			bool flag1450 = flag1449;
			bool flag1451 = flag1450;
			bool flag1452 = flag1451;
			bool flag1453 = flag1452;
			bool flag1454 = flag1453;
			bool flag1455 = flag1454;
			if (flag1455)
			{
				bool flag1456 = this.CheckRow(e, ref num, w, rowH, fs, old, "Pause the game while this menu is open", hpConfig.menuPause);
				bool flag1457 = flag1456;
				bool flag1458 = flag1457;
				bool flag1459 = flag1458;
				bool flag1460 = flag1459;
				bool flag1461 = flag1460;
				bool flag1462 = flag1461;
				bool flag1463 = flag1462;
				bool flag1464 = flag1463;
				if (flag1464)
				{
					hpConfig.menuPause = !hpConfig.menuPause;
					this.MarkHpDirty();
				}
				bool flag1465 = this.CheckRow(e, ref num, w, rowH, fs, old, "Block game input while this menu is open", hpConfig.menuBlockInput);
				bool flag1466 = flag1465;
				bool flag1467 = flag1466;
				bool flag1468 = flag1467;
				bool flag1469 = flag1468;
				bool flag1470 = flag1469;
				bool flag1471 = flag1470;
				bool flag1472 = flag1471;
				bool flag1473 = flag1472;
				if (flag1473)
				{
					hpConfig.menuBlockInput = !hpConfig.menuBlockInput;
					this.MarkHpDirty();
				}
				bool flag1474 = this.CheckRow(e, ref num, w, rowH, fs, old, "Enemy skin from pack (<pack>/Enemy folder)", this.enemyTexOn);
				bool flag1475 = flag1474;
				bool flag1476 = flag1475;
				bool flag1477 = flag1476;
				bool flag1478 = flag1477;
				bool flag1479 = flag1478;
				bool flag1480 = flag1479;
				bool flag1481 = flag1480;
				bool flag1482 = flag1481;
				if (flag1482)
				{
					this.SetEnemyTex(!this.enemyTexOn);
				}
				if (this.CheckRow(e, ref num, w, rowH, fs, old, "Auto-equalize gamma (same brightness day and night)", this.autoGamma))
				{
					this.autoGamma = !this.autoGamma;
					this.optSaveAt = Time.unscaledTime + 0.6f;
					this.dirLightsNext = 0f;
					this.lumRef = 0f;
				}
				if (this.autoGamma)
				{
					float masterNew = this.BrightRow(e, ref num, w, rowH, fs, old, "Master gamma", 23, this.masterGamma);
					if (masterNew != this.masterGamma)
					{
						this.masterGamma = masterNew;
						this.optSaveAt = Time.unscaledTime + 0.6f;
					}
				}
				else
				{
				float num44 = this.BrightRow(e, ref num, w, rowH, fs, old, "Day brightness", 21, this.dayBright);
				bool flag1483 = num44 != this.dayBright;
				bool flag1484 = flag1483;
				bool flag1485 = flag1484;
				bool flag1486 = flag1485;
				bool flag1487 = flag1486;
				bool flag1488 = flag1487;
				bool flag1489 = flag1488;
				bool flag1490 = flag1489;
				bool flag1491 = flag1490;
				if (flag1491)
				{
					this.dayBright = num44;
					this.optSaveAt = Time.unscaledTime + 0.6f;
					this.dirLightsNext = 0f;
				}
				float num45 = this.BrightRow(e, ref num, w, rowH, fs, old, "Night brightness", 22, this.nightBright);
				bool flag1492 = num45 != this.nightBright;
				bool flag1493 = flag1492;
				bool flag1494 = flag1493;
				bool flag1495 = flag1494;
				bool flag1496 = flag1495;
				bool flag1497 = flag1496;
				bool flag1498 = flag1497;
				bool flag1499 = flag1498;
				bool flag1500 = flag1499;
				if (flag1500)
				{
					this.nightBright = num45;
					this.optSaveAt = Time.unscaledTime + 0.6f;
					this.dirLightsNext = 0f;
				}
				}
			}
			bool flag1501 = this.Section(e, ref num, w, rowH, old, "Advanced (shader overrides, intervals)", 5);
			bool flag1502 = flag1501;
			bool flag1503 = flag1502;
			bool flag1504 = flag1503;
			bool flag1505 = flag1504;
			bool flag1506 = flag1505;
			bool flag1507 = flag1506;
			bool flag1508 = flag1507;
			bool flag1509 = flag1508;
			if (flag1509)
			{
				Rect row2 = new Rect(0f, num, w, rowH);
				this.DrawRowBackground(e, row2, false, old);
				GUI.Label(new Rect((float)fs, num, w * 0.42f, rowH), BtcLang.Tr("Ring search every (s)"), this.menuLabelStyle);
				Rect hit = new Rect(w * 0.47f, num, w * 0.53f - (float)fs * 4.5f, rowH);
				float num46 = this.SliderCore(e, hit, 12, hpConfig.scanInterval, 1f, 60f, 1f, old);
				bool flag1510 = num46 != hpConfig.scanInterval;
				bool flag1511 = flag1510;
				bool flag1512 = flag1511;
				bool flag1513 = flag1512;
				bool flag1514 = flag1513;
				bool flag1515 = flag1514;
				bool flag1516 = flag1515;
				bool flag1517 = flag1516;
				bool flag1518 = flag1517;
				if (flag1518)
				{
					hpConfig.scanInterval = num46;
					this.MarkHpDirty();
				}
				GUI.Label(new Rect(hit.xMax + (float)fs * 0.3f, num, (float)fs * 4f, rowH), ResourcePackManager.HpNum(hpConfig.scanInterval), this.menuValueStyle);
				num += rowH;
				this.HeaderRow(e, ref num, w, rowH, fs, old, "Own overrides (ring shader properties)");
				this.DrawOverrideRows(e, ref num, w, rowH, fs, old, hpConfig.ownOv, 0);
				this.HeaderRow(e, ref num, w, rowH, fs, old, "Enemy overrides (only if enemies are colored)");
				this.DrawOverrideRows(e, ref num, w, rowH, fs, old, hpConfig.enemyOv, 1);
			}
			num += rowH * 0.3f;
			bool flag1519 = Time.unscaledTime < this.hpResetArmedUntil;
			Rect rect3 = new Rect(0f, num, w, rowH);
			this.DrawRowBackground(e, rect3, false, old);
			bool flag1520 = GUI.Button(rect3, BtcLang.Tr(flag1519 ? "Click again to reset ALL Colored HP settings" : "Reset to defaults"), this.menuItemStyle);
			bool flag1521 = flag1520;
			bool flag1522 = flag1521;
			bool flag1523 = flag1522;
			bool flag1524 = flag1523;
			bool flag1525 = flag1524;
			bool flag1526 = flag1525;
			bool flag1527 = flag1526;
			bool flag1528 = flag1527;
			if (flag1528)
			{
				bool flag1529 = flag1519;
				bool flag1530 = flag1529;
				bool flag1531 = flag1530;
				bool flag1532 = flag1531;
				bool flag1533 = flag1532;
				bool flag1534 = flag1533;
				bool flag1535 = flag1534;
				bool flag1536 = flag1535;
				bool flag1537 = flag1536;
				if (flag1537)
				{
					this.hpResetArmedUntil = 0f;
					this.ResetHpConfigToDefaults();
					return;
				}
				this.hpResetArmedUntil = Time.unscaledTime + 3f;
			}
			num += rowH;
			this.hpContentH = num + rowH * 0.3f;
		}
		else
		{
			float num47 = 0f;
			int num48;
			for (int l = 0; l < 5; l = num48 + 1)
			{
				bool flag1538 = this.Section(e, ref num47, w, rowH, old, BtcOverlays.Names[l], 11 + l);
				bool flag1539 = flag1538;
				bool flag1540 = flag1539;
				bool flag1541 = flag1540;
				bool flag1542 = flag1541;
				bool flag1543 = flag1542;
				bool flag1544 = flag1543;
				if (flag1544)
				{
					this.DrawOverlaySection(e, ref num47, w, rowH, fs, old, l);
				}
				num48 = l;
			}
			if (this.Section(e, ref num47, w, rowH, old, "Interface", 16))
			{
				this.DrawUnitPlatesSection(e, ref num47, w, rowH, fs, old);
			}
			if (this.Section(e, ref num47, w, rowH, old, "Match statistics", 17))
			{
				this.DrawMatchStatsSection(e, ref num47, w, rowH, fs, old);
			}
			if (this.Section(e, ref num47, w, rowH, old, "Chat", 18))
			{
				this.DrawChatSection(e, ref num47, w, rowH, fs, old);
			}
			this.hpContentH = num47 + rowH * 0.3f;
		}
	}
}
}
