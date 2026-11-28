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





	private const string FolderName = "ResourcePacks";

	private const string TextureSubFolder = "texture";

	private const string SelectionFileName = "selected.txt";

	private const float MessageTime = 1.5f;

	private const float MessageFade = 0.3f;

	private const float ScanInterval = 10f;

	private const float BurstInterval = 1.5f;

	private const float BurstDuration = 10f;

	private static ResourcePackManager instance;

	/// <summary>The accent changed (UI editor): recolour the styles that baked the colour in.</summary>
	internal static void RecolorStyles()
	{
		ResourcePackManager m = ResourcePackManager.instance;
		if (m == null) return;
		if (m.menuBannerStyle != null) m.menuBannerStyle.normal.textColor = BtcMenu.AccentA(1f);
		if (m.menuHeaderStyle != null) m.menuHeaderStyle.normal.textColor = BtcMenu.AccentA(0.95f);
		if (m.menuActiveStyle != null) ResourcePackManager.SetStyleColors(m.menuActiveStyle, BtcMenu.AccentA(1f), BtcMenu.AccentA(1f));
	}

	/// <summary>Closes the main mod menu (e.g. when a replay starts from its list).</summary>
	internal static bool MenuIsOpen { get { return ResourcePackManager.instance != null && ResourcePackManager.instance.menuOpen; } }

	/// <summary>Opens the main menu on the UI editor (replayPage = on its Replay page).</summary>
	internal static void OpenUiEditor(bool replayPage)
	{
		ResourcePackManager m = ResourcePackManager.instance;
		if (m == null) return;
		if (!m.menuOpen) m.ToggleMenu();
		if (replayPage) BtcMenuState.SetInt("uiedit:page", BtcUiEditorTab.PReplay);
		m.SelectTab(12);
	}

	/// <summary>F7: opens the UI editor everywhere, closes the menu when the editor is already open.</summary>
	internal static void ToggleUiEditor()
	{
		if (ResourcePackManager.MenuIsOpen && BtcMenu.CurrentId == 12) { BtcUiEditorTab.Live = false; ResourcePackManager.CloseMenu(); return; }
		bool replay = BtcReplay.Mode == BtcReplay.State.Playing;
		bool inMatch = false;
		try { inMatch = Architecte.commence; } catch (Exception) { }
		ResourcePackManager.OpenUiEditor(replay);
		// in a match the editor works over the real screen (live mode); main menu and replays keep the editor tab
		if (inMatch && !replay) BtcUiEditorTab.StartLive();
	}

	/// <summary>Layout page button "Edit on the real screen": closes the window and starts the live overlay.</summary>
	internal static void StartUiEditorLive()
	{
		ResourcePackManager.OpenUiEditor(false);
		BtcUiEditorTab.StartLive();
	}

	/// <summary>Opens the main menu on the Replay tab library (a replay file was pasted or dropped).</summary>
	internal static void OpenReplays()
	{
		ResourcePackManager m = ResourcePackManager.instance;
		if (m == null) return;
		if (!m.menuOpen) m.ToggleMenu();
		BtcReplayLibrary.SettingsOpen = false;
		m.SelectTab(7);
	}

	/// <summary>Opens the main mod menu (launcher button).</summary>
	internal static void OpenMenu()
	{
		if (ResourcePackManager.instance != null && !ResourcePackManager.instance.menuOpen) ResourcePackManager.instance.ToggleMenu();
	}

	internal static void CloseMenu()
	{
		if (ResourcePackManager.instance != null) ResourcePackManager.instance.menuOpen = false;
	}

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
	private int dirLightsScene = -1;

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
