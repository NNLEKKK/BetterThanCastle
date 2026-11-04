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

	private bool HiddenByAlt(ResourcePackManager.HpConfig cfg, bool group)
	{
		return false;
	}

	private bool PerfButtonRow(Event e, ref float y, float w, float rowH, Color old, string label)
	{
		label = BtcLang.Tr(label);
		Rect rect = new Rect(0f, y, w, rowH);
		y += rowH;
		this.DrawRowBackground(e, rect, false, old);
		return GUI.Button(rect, "   " + label, this.menuItemStyle);
	}

	private void SetDiscord(bool on)
	{
		bool flag = this.discordOn == on;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			this.discordOn = on;
			this.SaveOptions();
			BtcDiscord.SetEnabled(on);
			bool flag9 = on && string.IsNullOrEmpty(BtcDiscord.ClientId);
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				this.ShowMessage(BtcLang.Tr("Discord RPC: set ClientId in configs/discord.txt"));
			}
		}
	}

	private void NoteRow(ref float y, float w, int fs, string text)
	{
		text = BtcLang.Tr(text);
		float num = this.menuNoteStyle.CalcHeight(new GUIContent(text), w - (float)fs * 2f);
		GUI.Label(new Rect((float)fs, y + 2f, w - (float)fs * 2f, num), text, this.menuNoteStyle);
		y += num + (float)fs * 0.5f;
	}

	private void ValueRow(ref float y, float w, float rowH, int fs, string label, string value)
	{
		label = BtcLang.Tr(label);
		GUI.Label(new Rect((float)fs, y, w * 0.5f, rowH), label, this.menuLabelStyle);
		GUI.Label(new Rect(w * 0.4f, y, w * 0.6f - (float)fs, rowH), BtcLang.Tr(value), this.menuValueStyle);
		y += rowH;
	}

	private void DrawBcRows(Event e, float w, float rowH, int fs, Color old)
	{
		float num = 0f;
		bool bcEnabled = BtcBc.BcEnabled;
		bool bcCanChangeEnabled = BtcBc.BcCanChangeEnabled;
		bool flag = this.CheckRow(e, ref num, w, rowH, fs, old, "BetterConnection (master switch)", bcEnabled) && bcCanChangeEnabled;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		bool flag9 = flag8;
		bool flag10 = flag9;
		if (flag10)
		{
			BtcBc.BcSetEnabled(!bcEnabled);
			bcEnabled = BtcBc.BcEnabled;
		}
		bool flag11 = !bcCanChangeEnabled;
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
			this.NoteRow(ref num, w, fs, "Locked while hosting or connected. Leave the lobby/game to switch it.");
		}
		this.NoteRow(ref num, w, fs, bcEnabled ? "On: improved connection (heartbeat, auto-reconnect instead of kicking to the menu, pings, voluntary pause). Everyone in the game needs BetterConnection too." : "Off: fully vanilla networking. You can play with vanilla players, as host or as client. All BetterConnection options below are hidden.");
		bool flag21 = !bcEnabled;
		bool flag22 = flag21;
		bool flag23 = flag22;
		bool flag24 = flag23;
		bool flag25 = flag24;
		bool flag26 = flag25;
		bool flag27 = flag26;
		bool flag28 = flag27;
		bool flag29 = flag28;
		bool flag30 = flag29;
		if (flag30)
		{
			this.bcContentH = num + rowH * 0.3f;
		}
		else
		{
			this.HeaderRow(e, ref num, w, rowH, fs, old, "On screen and diagnostics");
			bool bcShowPing = BtcBc.BcShowPing;
			bool flag31 = this.CheckRow(e, ref num, w, rowH, fs, old, "Hide ping display", !bcShowPing);
			bool flag32 = flag31;
			bool flag33 = flag32;
			bool flag34 = flag33;
			bool flag35 = flag34;
			bool flag36 = flag35;
			bool flag37 = flag36;
			bool flag38 = flag37;
			bool flag39 = flag38;
			bool flag40 = flag39;
			if (flag40)
			{
				BtcBc.BcSetShowPing(!bcShowPing);
			}
			bool bcShowPauseLabel = BtcBc.BcShowPauseLabel;
			bool flag41 = this.CheckRow(e, ref num, w, rowH, fs, old, "Hide \"Client pause: allowed\" label", !bcShowPauseLabel);
			bool flag42 = flag41;
			bool flag43 = flag42;
			bool flag44 = flag43;
			bool flag45 = flag44;
			bool flag46 = flag45;
			bool flag47 = flag46;
			bool flag48 = flag47;
			bool flag49 = flag48;
			bool flag50 = flag49;
			if (flag50)
			{
				BtcBc.BcSetShowPauseLabel(!bcShowPauseLabel);
			}
			bool bcDiagLog = BtcBc.BcDiagLog;
			bool flag51 = this.CheckRow(e, ref num, w, rowH, fs, old, "Diagnostic log (unet_netdiag_*.log)", bcDiagLog);
			bool flag52 = flag51;
			bool flag53 = flag52;
			bool flag54 = flag53;
			bool flag55 = flag54;
			bool flag56 = flag55;
			bool flag57 = flag56;
			bool flag58 = flag57;
			bool flag59 = flag58;
			bool flag60 = flag59;
			if (flag60)
			{
				BtcBc.BcSetDiagLog(!bcDiagLog);
			}
			this.HeaderRow(e, ref num, w, rowH, fs, old, "Session");
			bool bcInSession = BtcBc.BcInSession;
			this.ValueRow(ref num, w, rowH, fs, "Status", BtcBc.BcStatusText());
			bool flag68 = bcInSession;
			bool flag69 = flag68;
			bool flag70 = flag69;
			bool flag71 = flag70;
			bool flag72 = flag71;
			bool flag73 = flag72;
			bool flag74 = flag73;
			bool flag75 = flag74;
			bool flag76 = flag75;
			bool flag77 = flag76;
			if (flag77)
			{
				bool flag78 = this.PerfButtonRow(e, ref num, w, rowH, old, BtcBc.BcVoluntaryPauseActive ? "Resume the game (voluntary pause is on)" : "Pause the game (voluntary pause)");
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
					BtcBc.BcTogglePause();
				}
				bool bcClientsCanPause = BtcBc.BcClientsCanPause;
				bool bcIsHost = BtcBc.BcIsHost;
				bool flag88 = bcIsHost;
				bool flag89 = flag88;
				bool flag90 = flag89;
				bool flag91 = flag90;
				bool flag92 = flag91;
				bool flag93 = flag92;
				bool flag94 = flag93;
				bool flag95 = flag94;
				bool flag96 = flag95;
				if (flag96)
				{
					bool flag97 = this.CheckRow(e, ref num, w, rowH, fs, old, "Clients are allowed to pause the game", bcClientsCanPause);
					bool flag98 = flag97;
					bool flag99 = flag98;
					bool flag100 = flag99;
					bool flag101 = flag100;
					bool flag102 = flag101;
					bool flag103 = flag102;
					bool flag104 = flag103;
					bool flag105 = flag104;
					bool flag106 = flag105;
					if (flag106)
					{
						BtcBc.BcToggleClientPausePermission();
					}
				}
				else
				{
					this.ValueRow(ref num, w, rowH, fs, "Pause allowed by host", bcClientsCanPause ? "Yes" : "No");
				}
				this.HeaderRow(e, ref num, w, rowH, fs, old, "Pings");
				List<string> list = BtcBc.BcGetPingLines();
				bool flag107 = list.Count == 0;
				bool flag108 = flag107;
				bool flag109 = flag108;
				bool flag110 = flag109;
				bool flag111 = flag110;
				bool flag112 = flag111;
				bool flag113 = flag112;
				bool flag114 = flag113;
				bool flag115 = flag114;
				bool flag116 = flag115;
				if (flag116)
				{
					GUI.Label(new Rect((float)fs, num, w - (float)fs * 2f, rowH), BtcLang.Tr("No data yet"), this.menuNoteStyle);
					num += rowH;
				}
				BtcKick.PingRows(e, ref num, w, rowH, fs, old, this.menuLabelStyle, list);
			}
			else
			{
				this.NoteRow(ref num, w, fs, "Pause, pings and client permissions appear here during a network game.");
			}
			this.bcContentH = num + rowH * 0.3f;
		}
	}

	private void DrawDbgRows(Event e, float w, float rowH, int fs, Color old)
	{
		float num = 0f;
		this.HeaderRow(e, ref num, w, rowH, fs, old, "Dumps (written to BetterThanCastle/debug/dump)");
		bool flag = this.PerfButtonRow(e, ref num, w, rowH, old, "Dump textures");
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			this.dbgDumpTexPending = true;
		}
		this.NoteRow(ref num, w, fs, "List of the game's textures plus their original PNGs - the base for making a resource pack.");
		bool flag9 = this.PerfButtonRow(e, ref num, w, rowH, old, "Dump buildings");
		bool flag10 = flag9;
		bool flag11 = flag10;
		bool flag12 = flag11;
		bool flag13 = flag12;
		bool flag14 = flag13;
		bool flag15 = flag14;
		bool flag16 = flag15;
		if (flag16)
		{
			this.dbgDumpBldPending = true;
		}
		this.NoteRow(ref num, w, fs, "HP components of non-bricktron objects (buildings): BetterThanCastle/debug/dump/buildings.txt.");
		this.dbgContentH = num + rowH * 0.3f;
	}

	private void DrawLangButton(Rect panel, float rowH, float pad)
	{
		float num = rowH * 2.6f;
		Rect position = new Rect(panel.xMax - num - pad, panel.y + pad * 0.5f, num, rowH * 0.85f);
		bool russian = BtcLang.Russian;
		bool flag = GUI.Button(position, russian ? "RU | en" : "EN | ru", this.menuTabStyle);
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		if (flag7)
		{
			BtcLang.Toggle();
		}
	}

	private void DrawPackTab(Event e, float w, float rowH, int fs, Color old)
	{
		this.DrawPackRows(e, w, rowH, old);
		float num = (float)(this.menuPacks.Count + 1) * rowH;
		Rect rect = new Rect(0f, num, w, rowH);
		this.DrawRowBackground(e, rect, this.hatsOpen, old);
		bool flag = GUI.Button(rect, (this.hatsOpen ? "[-] " : "[+] ") + BtcLang.Tr("Halloween Hats"), this.menuHatItemStyle);
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		if (flag7)
		{
			this.hatsOpen = !this.hatsOpen;
		}
		bool flag8 = this.hatsOpen;
		bool flag9 = flag8;
		bool flag10 = flag9;
		bool flag11 = flag10;
		bool flag12 = flag11;
		bool flag13 = flag12;
		bool flag14 = flag13;
		if (flag14)
		{
			GUI.BeginGroup(new Rect(0f, num + rowH, w, (float)(ResourcePackManager.HatOrder.Count + 1) * rowH));
			this.DrawHatRows(e, w, rowH, fs, old);
			GUI.EndGroup();
		}
		float num2 = (float)(this.menuPacks.Count + 2 + (this.hatsOpen ? (ResourcePackManager.HatOrder.Count + 1) : 0)) * rowH;
		GUI.BeginGroup(new Rect(0f, num2, w, this.dbgContentH + rowH));
		this.DrawDbgRows(e, w, rowH, fs, old);
		GUI.EndGroup();
	}

	private static uint ComputeStringHash(string s)
	{
		uint num = 0U;
		bool flag = s != null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		if (flag7)
		{
			num = 2166136261U;
			for (int i = 0; i < s.Length; i++)
			{
				num = ((uint)s[i] ^ num) * 16777619U;
			}
		}
		return num;
	}

	private void DrawUnitPlatesSection(Event e, ref float y, float w, float rowH, int fs, Color old)
	{
		float yy = y;
		string[] tabs = BtcInterface.TabNames;
		int cols = Mathf.Clamp((int)(w / (rowH * 3.6f)), 2, tabs.Length);
		float bw = w / (float)cols;
		for (int i = 0; i < tabs.Length; i++)
		{
			Rect r = new Rect((float)(i % cols) * bw, yy + (float)(i / cols) * rowH, bw - 2f, rowH - 2f);
			if (GUI.Button(r, BtcLang.Tr(tabs[i]), (i == BtcInterface.Tab) ? this.menuTabActiveStyle : this.menuTabStyle)) BtcInterface.Tab = i;
		}
		yy += (float)((tabs.Length + cols - 1) / cols) * rowH;
		if (BtcInterface.Tab == 0)
		{
			BtcUnitPlates.DrawSettings(delegate (string label, int id, float v, float mn, float mx, float st, string fmt, string suf) { return this.SliderRow(e, ref yy, w, rowH, fs, old, label, id, v, mn, mx, st, fmt, string.Empty, suf); });
		}
		else
		{
			bool any = BtcInterface.DrawTab(delegate (string label, int id, float v, float mn, float mx, float st, string fmt, string suf) { return this.SliderRow(e, ref yy, w, rowH, fs, old, label, id, v, mn, mx, st, fmt, string.Empty, suf); }, BtcInterface.Tab);
			if (!any)
			{
				GUI.Label(new Rect((float)fs, yy, w - (float)fs, rowH), BtcLang.Russian ? "Нет панелей (панели появляются в матче)" : "No panels here (HUD panels appear in a match)", this.menuLabelStyle);
				yy += rowH;
			}
		}
		y = yy;
	}

	// Overlays tab header (above the scroll list, next to Discord Rich Presence): window mode row + skip intro row.
	private void DrawDisplayHeader(Event e, float y, Rect area, float rowH, int pad, float box, Color old)
	{
		Rect lr = new Rect(area.x, y, area.width, rowH);
		this.DrawRowBackground(e, lr, false, old);
		float lw = area.width * 0.34f;
		GUI.Label(new Rect(area.x + (float)pad, y, lw - (float)pad, rowH), BtcLang.Russian ? "Режим окна" : "Window mode", this.menuLabelStyle);
		string[] names = BtcLang.Russian ? ResourcePackManager.DisplayNamesRu : ResourcePackManager.DisplayNamesEn;
		float bw = (area.width - lw) / 3f;
		int cur = BtcDisplay.Mode;
		for (int i = 0; i < 3; i++)
		{
			if (GUI.Button(new Rect(area.x + lw + bw * (float)i, y, bw, rowH), names[i], cur == i + 1 ? this.menuActiveStyle : this.menuItemStyle))
			{
				BtcDisplay.Apply(i + 1, 1);
			}
		}
		Rect sr = new Rect(area.x, y + rowH, area.width, rowH);
		bool skip = BtcConfig.SkipIntro != null && BtcConfig.SkipIntro.Value;
		this.DrawRowBackground(e, sr, skip, old);
		if (e.type == EventType.Repaint)
		{
			this.DrawCheckbox(new Rect(sr.x + (float)pad, sr.y + (rowH - box) * 0.5f, box, box), skip);
		}
		if (GUI.Button(sr, BtcLang.Russian ? "Пропускать интро (заставки при запуске)" : "Skip intro (logo screens on startup)", this.menuHatItemStyle) && BtcConfig.SkipIntro != null)
		{
			BtcConfig.SkipIntro.Value = !skip;
		}
	}

	private static readonly string[] DisplayNamesEn = new string[] { "Fullscreen", "Windowed", "Borderless" };
	private static readonly string[] DisplayNamesRu = new string[] { "Полный экран", "Окно", "Без рамки" };

	private void DrawMatchStatsSection(Event e, ref float y, float w, float rowH, int fs, Color old)
	{
		float yy = y;
		BtcMatchStatsUI.DrawSettings((string label, bool on) => this.CheckRow(e, ref yy, w, rowH, fs, old, label, on), (string label, string val) => this.PerfButtonRow(e, ref yy, w, rowH, old, label + ": " + val), delegate (string label, int id, float v, float mn, float mx, float st, string fmt, string suf) { return this.SliderRow(e, ref yy, w, rowH, fs, old, label, id, v, mn, mx, st, fmt, string.Empty, suf); });
		y = yy;
	}

	private void DrawOverlaySection(Event e, ref float y, float w, float rowH, int fs, Color old, int idx)
	{
		float yy = y;
		bool flag = idx == 4;
		if (flag)
		{
			BtcBlockMarks.DrawSettings((string label, bool on) => this.CheckRow(e, ref yy, w, rowH, fs, old, label, on), (string label, string val) => this.PerfButtonRow(e, ref yy, w, rowH, old, label + ": " + val), (string label, int id, float v, float mn, float mx, float st, string fmt, string suf) => this.SliderRow(e, ref yy, w, rowH, fs, old, label, id, v, mn, mx, st, fmt, string.Empty, suf), delegate(string label, int id)
			{
				this.ColorRow(e, ref yy, w, rowH, fs, old, label, id);
			});
		}
		else
		{
			BtcOverlays.DrawOne(idx, (string label, bool on) => this.CheckRow(e, ref yy, w, rowH, fs, old, label, on), (string label, string val) => this.PerfButtonRow(e, ref yy, w, rowH, old, label + ": " + val));
		}
		y = yy;
	}

	private const string FolderName = "ResourcePacks";

	private const string TextureSubFolder = "texture";

	private const string SelectionFileName = "selected.txt";

	private const float MessageTime = 1.5f;

	private const float MessageFade = 0.3f;

	private const float ScanInterval = 10f;

	private const float BurstInterval = 1.5f;

	private const float BurstDuration = 10f;

	private static ResourcePackManager instance;

	private readonly Dictionary<int, ResourcePackManager.Entry> entries;

	private readonly HashSet<int> noMatch;

	private string currentPack;

	private float nextScan;

	private float burstUntil;

	private string message;

	private float messageStart;

	private GUIStyle style;

	private readonly HashSet<string> matchedFiles;

	private const string VanillaPackName = "Vanilla";

	private const int VariantTolerance = 3;

	private readonly Dictionary<int, string> sigCache;

	private Dictionary<string, ResourcePackManager.IndexItem> index;

	private bool menuOpen;

	private List<string> menuPacks;

	private Vector2 menuScroll;

	private Texture2D whiteTex;

	private GUIStyle menuTitleStyle;

	private GUIStyle menuItemStyle;

	private GUIStyle menuActiveStyle;

	private int menuStyleHeight;

	private const string CursorAtlasName = "Curseurs";

	private static bool cursorsDirty;

	private const string HatsFileName = "hats.txt";

	private const string HalloweenDir = "Info/Lua/Mods/halloween2017";

	private GUIStyle menuLinkLeftStyle;

	private GUIStyle menuLinkRightStyle;

	private GUIStyle menuHatItemStyle;

	private readonly HashSet<string> hatsOff;

	private readonly Dictionary<string, ResourcePackManager.HatSet> hatSets;

	private readonly HashSet<string> hatSetFailed;

	private static readonly Dictionary<string, ResourcePackManager.HatSpec> HatSpecs;

	private const float HatApplyDelay = 2f;

	private const int HatApplyMaxTries = 8;

	private readonly Dictionary<string, ResourcePackManager.HatVanilla> hatVanilla;

	private readonly List<string> hatsPending;

	private float hatsLevelApplyAt;

	private int hatsLevelTries;

	private static readonly List<ResourcePackManager.HatSpec> HatOrder;

	private const string OptionsFileName = "options.txt";

	private bool coloredHp;

	private float hpNextScan;

	private static FieldInfo circleRendField;

	private static FieldInfo circleAnimField;

	private static int hpParamId;

	private static int[] ringPropIds;

	private static readonly string[] RingColorProps = new string[]
	{
		"_Ring1Color",
		"_Ring1Color2",
		"_Ring2Color",
		"_Ring2Color2"
	};

	private readonly List<ResourcePackManager.HpRing> hpRings;

	private readonly HashSet<int> hpRingIds;

	private const string HpConfigFileName = "coloredhp.txt";

	private float hpNextCfgCheck;

	private DateTime hpCfgStamp;

	private ResourcePackManager.HpConfig hpCfg;

	private static readonly string[] DefaultHpConfig;

	private static int hpCfgCounter;

	private bool menuHp;

	private GUIStyle menuLabelStyle;

	private GUIStyle menuValueStyle;

	private GUIStyle menuHeaderStyle;

	private int hpDragId;

	private float hpContentH;

	private float hpSaveAt;

	private float hpResetArmedUntil;

	private ResourcePackManager.HpStop pickStop;

	private ResourcePackManager.HpOverride pickOv;

	private float pickH;

	private float pickS;

	private float pickV;

	private Texture2D svTex;

	private Texture2D hueTex;

	private float svTexHue;

	private static readonly string[] HpKnownFloatProps;

	private static readonly string[] HpKnownColorProps;

	private static int hpShowHpId;

	private static int hpMainColorId;

	private int hpForcedCount;

	private GUIStyle menuTabStyle;

	private GUIStyle menuTabActiveStyle;

	private static int hpShowStamId;

	private static int hpStamId;

	private readonly Dictionary<Labor, ResourcePackManager.AutoRing> autoRings;

	private readonly List<Labor> autoTmp;

	private float autoNextScan;

	private static FieldInfo dispenserMapField;

	private const float AutoScanInterval = 0.25f;

	private const float AutoAdaptInterval = 0.15f;

	private const string EnemyTexFolder = "Enemy";

	private const float EnemyTexInterval = 1f;

	private bool enemyTexOn;

	private string enemyTexPack;

	private float enemyTexNext;

	private int enemyTexTick;

	private readonly Dictionary<string, Texture2D> enemyTexByName;

	private readonly Dictionary<string, DateTime> enemyTexStamp;

	private readonly List<ResourcePackManager.EnemyMat> enemyMats;

	private readonly HashSet<int> enemyMatIds;

	private readonly HashSet<int> enemyMatSeen;

	private readonly List<Renderer> enemyRendTmp;

	private static readonly string[] EnemyTexProps;

	private float dayBright;

	private float nightBright;

	private float optSaveAt;

	private Light[] dirLights;

	private float dirLightsNext;

	private bool lightApplied;

	private Color ambientSaved;

	private float[] lightSaved;

	private bool pickNum;

	private static int hpSelectedId;

	private float stamNextRegister;

	private bool stamReflFailed;

	private readonly List<ResourcePackManager.AmmoUnit> ammoUnits;

	private readonly Dictionary<Labor, ResourcePackManager.AmmoUnit> ammoCache;

	private float ammoNextScan;

	private static MethodInfo circleOffsetMethod;

	private GUIStyle ammoStyle;

	private int hpSection;

	private readonly bool[] palOpen;

	private int pickExtra;

	private readonly List<ResourcePackManager.StatUnit> statUnits;

	private readonly Dictionary<Labor, ResourcePackManager.StatUnit> statCache;

	private readonly HashSet<Labor> statSeen;

	private float statNextScan;

	private GUIStyle statStyle;

	private const int PathMaxUnits = 64;

	private const int PathMaxNodes = 400;

	private readonly List<ResourcePackManager.PathUnit> pathUnits;

	private readonly List<ResourcePackManager.PathUnit> pathPool;

	private readonly List<Vector3> pathTmp;

	private readonly HashSet<Labor> pathSeen;

	private readonly HashSet<Labor> pathSelected;

	private float pathNextScan;

	private Material pathMat;

	private bool pathMatFailed;

	private Camera pathCamCache;

	private int pathCamFrame;

	private bool pathDrawWarned;

	private const int TrailMaxPoints = 60;

	private const float TrailMinStep = 0.25f;

	private const float TrailIdleClear = 2f;

	private readonly Dictionary<Labor, ResourcePackManager.TrailData> trails;

	private readonly List<Labor> trailDrop;

	private int pathScanId;

	private const int PredStepBudget = 150;

	private const int PredCallsPerFrame = 4;

	private const int PredMaxCalls = 600;

	private const int PredMaxQueue = 48;

	private readonly List<ResourcePackManager.PredReq> predQueue;

	private readonly Dictionary<Labor, ResourcePackManager.PredRoute> predRoutes;

	private readonly List<Labor> predDrop;

	private readonly List<Vector3> predTmp;

	private bool predWarned;

	private static bool predReflTried;

	private static bool predReflOk;

	private static MethodInfo mPredInitOrigin;

	private static MethodInfo mPredInitSearch;

	private static MethodInfo mPredWork;

	private static MethodInfo mPredInitImprove;

	private static MethodInfo mPredLoopImprove;

	private static MethodInfo mPredWrapUp;

	private static bool pollReflTried;

	private static object pollGiver;

	private static FieldInfo fPollActors;

	private static FieldInfo fPollPos;

	private static FieldInfo fPollObj;

	private static FieldInfo fPollType;

	private static FieldInfo fPollTime;

	private float pollLastStamp;

	private string predDiag;

	private const int BombMaxBarrels = 24;

	private const float BombDrawDistance = 90f;

	private List<ResourcePackManager.BombBarrel> bombBarrels;

	private readonly Dictionary<string, ExplosionGameplay> bombPrefabs;

	private float bombNextScan;

	private bool bombWarned;

	private static FieldInfo fBombTemplate;

	private GUIStyle bombStyle;

	private static readonly int[,] BarrelDamageTable;

	private const float BarrelMaxDamage = 1601f;

	private readonly List<ResourcePackManager.HitFloat> hitFloats;

	private readonly List<ResourcePackManager.CrystalTrack> crystalTracks;

	private float hitNextScan;

	private float crystalNextScan;

	private bool crystalWarned;

	private GUIStyle overlayStyle;

	private static FieldInfo fCapClock;

	private static FieldInfo fClockProg;

	private const int BarrelMinDamage = 10;

	private const float BombDrawDistancePerf = 60f;

	private Vector3 bombSortPos;

	private List<ResourcePackManager.BombGroup> bombShown;

	private List<ResourcePackManager.BombGroup> bombBuilding;

	private int bombGlobalSig;

	private readonly Dictionary<long, ResourcePackManager.BombCell> bombMerge;

	private static FieldInfo fRegenDmg;

	private static FieldInfo fRegenSta;

	private static PropertyInfo pTsDelta;

	private static FieldInfo fTsDelta;

	private static bool regenReflFail;

	private static readonly string[] regenTexts;

	private readonly List<ResourcePackManager.BldUnit> bldUnits;

	private readonly Dictionary<Transform, ResourcePackManager.BldUnit> bldCache;

	private float bldNextScan;

	private readonly Dictionary<Transform, ResourcePackManager.HitTrack> hitTracks;

	private readonly HashSet<Transform> hitSeen;

	private readonly List<Transform> hitDrop;

	private readonly Dictionary<string, float> hitLearn;

	private const KeyCode MenuKey = KeyCode.PageDown;

	private const int ExplosionLayerMask = 69632;

	private readonly Dictionary<Transform, ResourcePackManager.CenterEnt> centerCache;

	private readonly List<ResourcePackManager.CenterEnt> centerShown;

	private readonly HashSet<Transform> centerSeen;

	private float centerNextScan;

	private Texture2D dotTex;

	private readonly List<ResourcePackManager.DistFloat> distFloats;

	private readonly HashSet<int> expSeen;

	private readonly HashSet<int> expNow;

	private bool expPrimed;

	private bool menuInputLocked;

	private float menuInputCheckAt;

	private object esBlocked;

	private bool esWasEnabled;

	private bool menuPauseOwned;

	private bool menuPausePending;

	private bool menuPauseRelease;

	private bool menuPauseTried;

	private bool menuPauseSolo;

	private float menuPauseReqAt;

	private bool menuLockWarned;

	private float expNextScan;

	private float brightFrame;

	private float brightMul;

	private GUIStyle menuBannerStyle;

	private GUIStyle menuBannerSubStyle;

	private GUIStyle menuNoteStyle;

	private bool discordOn;

	private bool menuBc;

	private bool menuDbg;

	private float bcContentH;

	private float dbgContentH;

	private bool dbgDumpTexPending;

	private bool dbgDumpBldPending;

	private bool hatsOpen;

	private class Entry
	{
		public Texture2D tex;

		public byte[] original;

		public string file;

		public long stamp;

		public bool applied;

		public string sig;
	}
}
}
