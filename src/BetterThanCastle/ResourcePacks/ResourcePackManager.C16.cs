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

	private class IndexItem
	{
		public string plain;

		public Dictionary<string, string> bySig;
	}

	private class ManifestEntry
	{
		public string name;

		public string sig;

		public string file;
	}

	private class Variant
	{
		public Texture2D copy;

		public byte[] png;
	}

	private class HatSpec
	{
		public HatSpec(string handle, string label, string visual, string folder, string shader, string specSuffix, Color falloff, float gloss, Vector3 gripPos, Vector3 gripEuler, Vector3 normalsEuler)
		{
			this.handle = handle;
			this.label = label;
			this.visual = visual;
			this.folder = folder;
			this.shader = shader;
			this.specSuffix = specSuffix;
			this.falloff = falloff;
			this.gloss = gloss;
			this.gripPos = gripPos;
			this.gripEuler = gripEuler;
			this.normalsEuler = normalsEuler;
		}

		public string handle;

		public string folder;

		public string shader;

		public string specSuffix;

		public Color falloff;

		public float gloss;

		public Vector3 gripPos;

		public Vector3 gripEuler;

		public Vector3 normalsEuler;

		public string label;

		public string visual;
	}

	private class HatSet
	{
		public Mesh mesh;

		public Material mat;

		public Matrix4x4 grip;
	}

	private class HatVanilla
	{
		public Mesh mesh;

		public Material mat;

		public Matrix4x4 grip;

		public bool hasGrip;
	}

	private class HpRing
	{
		public SelectionCircle owner;

		public Renderer rend;

		public Animator anim;

		public MaterialPropertyBlock block;

		public Color[] originals;

		public int mainLayer = -2;

		public bool enemyLogged;

		public int team = -1;

		public int cfgVer = -1;

		public bool[] ovHas;

		public float[] ovBaseF;

		public Color[] ovBaseC;

		public bool forcedShow;

		public bool forcedStam;

		public int parentId = -1;

		public Labor labor;

		public bool allied;

		public bool ring2Painted;

		public float[] ovLastF;

		public float[] ovRawF;

		public bool building;
	}

	private class HpPalette
	{
		public int Index(float percent)
		{
			for (int i = 0; i < this.mins.Length; i++)
			{
				bool flag = percent >= this.mins[i];
				bool flag2 = flag;
				bool flag3 = flag2;
				bool flag4 = flag3;
				bool flag5 = flag4;
				bool flag6 = flag5;
				bool flag7 = flag6;
				bool flag8 = flag7;
				if (flag8)
				{
					return i;
				}
			}
			return this.mins.Length - 1;
		}

		public float[] mins = new float[0];

		public Color[] main = new Color[0];

		public Color[] second = new Color[0];
	}

	private class HpConfig
	{
		public HpConfig()
		{
			this.own = new ResourcePackManager.HpPalette();
			this.enemy = new ResourcePackManager.HpPalette();
			this.secondBrightness = 0.75f;
			bool[] array = new bool[2];
			array[0] = true;
			this.rings = array;
			this.centerOn = false;
			this.centerDist = false;
			this.centerDistSize = 1f;
			this.centerDistTime = 2f;
			this.centerOwnUnits = true;
			this.centerEnemyUnits = true;
			this.centerOwnObjects = true;
			this.centerEnemyObjects = true;
			this.centerSize = 1f;
			this.centerAlpha = 1f;
			this.centerOwnColor = new Color(1f, 0.92f, 0.1f, 1f);
			this.centerEnemyColor = new Color(1f, 0.25f, 0.9f, 1f);
			this.menuPause = true;
			this.menuBlockInput = true;
			this.altOnly = false;
			this.altNumbers = true;
			this.altAmmo = true;
			this.altDamage = true;
			this.altCrystal = true;
			this.altBombMap = true;
			this.altRings = false;
			this.altPaths = false;
			this.alwaysOwn = false;
			this.alwaysEnemy = false;
			this.colorEnemies = false;
			this.enemyPalette = true;
			this.staminaColor = true;
			this.ammoColor = true;
			this.stamSecondBrightness = 0.75f;
			this.ammoSecondBrightness = 0.75f;
			this.stamOwnStops = new List<ResourcePackManager.HpStop>();
			this.stamEnemyStops = new List<ResourcePackManager.HpStop>();
			this.ammoStops = new List<ResourcePackManager.HpStop>();
			this.stamOwn = new ResourcePackManager.HpPalette();
			this.stamEnemy = new ResourcePackManager.HpPalette();
			this.ammo = new ResourcePackManager.HpPalette();
			this.knightStaminaOnly = true;
			this.ammoRing = true;
			this.ammoNumber = false;
			this.numSize = 1f;
			this.numX = 0f;
			this.numY = -20f;
			this.numColor = Color.white;
			this.numByAmmo = false;
			this.numOutline = true;
			this.numBold = true;
			this.hpNumOwn = false;
			this.hpNumEnemy = false;
			this.stamNumOwn = false;
			this.stamNumEnemy = false;
			this.stamNumKnightOnly = true;
			this.statHideFull = false;
			this.statFormat = 1;
			this.statSize = 0.8f;
			this.statAlpha = 1f;
			this.statBold = true;
			this.statOutline = true;
			this.hpNumX = 0f;
			this.hpNumY = -20f;
			this.stamNumX = 0f;
			this.stamNumY = -42f;
			this.hpNumByPalette = true;
			this.stamNumByPalette = true;
			this.hpNumColor = Color.white;
			this.stamNumColor = new Color(0.55f, 0.8f, 1f, 1f);
			this.pathOwn = false;
			this.bombMap = false;
			this.bombMap3D = false;
			this.bombMapNumbers = false;
			this.bombMapAlpha = 0.45f;
			this.bombMapLayers = 7;
			this.bombMapYOffset = 1f;
			this.bombMapAllNumbers = false;
			this.bombNumAlpha = 1f;
			this.bombMapRange = 8;
			this.bombNumDist = 90f;
			this.bldColor = false;
			this.bldOwnStops = new List<ResourcePackManager.HpStop>();
			this.bldEnemyStops = new List<ResourcePackManager.HpStop>();
			this.bldSecondBrightness = 0.75f;
			this.bldOwn = new ResourcePackManager.HpPalette();
			this.bldEnemy = new ResourcePackManager.HpPalette();
			this.bldHpNumOwn = false;
			this.bldHpNumEnemy = false;
			this.bldRegenOwn = false;
			this.bldRegenEnemy = false;
			this.bldHpNumY = -20f;
			this.bldRegenY = -42f;
			this.bombByDamage = false;
			this.bombDmg = new float[]
			{
				51f,
				83f,
				136f,
				320f,
				576f,
				833f,
				1217f
			};
			this.regenNumOwn = false;
			this.regenNumEnemy = false;
			this.regenNumY = -64f;
			this.regenNumColor = new Color(0.55f, 1f, 0.6f, 1f);
			this.bombBandMask = 255;
			this.statHideFar = false;
			this.statHideDist = 60f;
			this.hitOwn = false;
			this.hitEnemy = false;
			this.hitDps = false;
			this.hitStamOwn = false;
			this.hitStamEnemy = false;
			this.dpsStamina = true;
			this.hitBldOwn = false;
			this.hitBldEnemy = false;
			this.hitBldDps = false;
			this.hitFullDamage = true;
			this.hitStamColor = new Color(0.35f, 0.7f, 1f, 1f);
			this.hitTime = 1f;
			this.hitSize = 1f;
			this.hitX = 26f;
			this.hitY = 34f;
			this.dpsSilence = 5f;
			this.dpsShow = 3f;
			this.hitOwnColor = new Color(1f, 0.32f, 0.28f, 1f);
			this.hitEnemyColor = new Color(1f, 0.85f, 0.25f, 1f);
			this.dpsColor = new Color(1f, 0.6f, 0.15f, 1f);
			this.dpsPrecise = false;
			this.perfMode = false;
			this.crystalTimer = false;
			this.crystalSize = 1.3f;
			this.crystalX = 0f;
			this.crystalY = 0f;
			this.crystalLift = 2.5f;
			this.crystalUpColor = new Color(0.25f, 0.95f, 0.35f, 1f);
			this.crystalDownColor = new Color(1f, 0.25f, 0.22f, 1f);
			this.bombColors = new Color[]
			{
				new Color(0.86f, 0.86f, 0.9f, 1f),
				new Color(0.88f, 0.45f, 0.68f, 1f),
				new Color(0.22f, 0.42f, 0.95f, 1f),
				new Color(0.28f, 0.74f, 0.32f, 1f),
				new Color(0.98f, 0.84f, 0.22f, 1f),
				new Color(0.8f, 0.38f, 0.04f, 1f),
				new Color(0.92f, 0.12f, 0.1f, 1f),
				new Color(0.5f, 0.02f, 0.02f, 1f)
			};
			this.bombThr = new float[]
			{
				3.2f,
				5.2f,
				8.5f,
				20f,
				36f,
				52f,
				76f
			};
			this.pathPredict = true;
			this.pathTrail = true;
			this.pathSelectedOnly = true;
			this.pathOnTop = true;
			this.pathMarker = true;
			this.pathWidth = 3f;
			this.pathLift = 0.35f;
			this.pathAlpha = 0.9f;
			this.pathOwnColor = new Color(0.35f, 1f, 0.45f, 1f);
			this.selShiftOn = false;
			this.selShift = 0.25f;
			this.layerName = "MainColor";
			this.enemyState = "ColorMain_Red";
			this.scanInterval = 15f;
			this.checkInterval = 2f;
			this.ownOv = new List<ResourcePackManager.HpOverride>();
			this.enemyOv = new List<ResourcePackManager.HpOverride>();
			this.allOv = new List<ResourcePackManager.HpOverride>();
		}

		public ResourcePackManager.HpPalette own;

		public ResourcePackManager.HpPalette enemy;

		public float secondBrightness;

		public bool[] rings;

		public bool enemyPalette;

		public string layerName;

		public string enemyState;

		public int enemyShortHash;

		public int enemyFullHash;

		public float scanInterval;

		public float checkInterval;

		public List<ResourcePackManager.HpOverride> ownOv;

		public List<ResourcePackManager.HpOverride> enemyOv;

		public List<ResourcePackManager.HpOverride> allOv;

		public int version;

		public bool colorEnemies;

		public List<ResourcePackManager.HpStop> ownStops = new List<ResourcePackManager.HpStop>();

		public List<ResourcePackManager.HpStop> enemyStops = new List<ResourcePackManager.HpStop>();

		public bool alwaysOwn;

		public bool alwaysEnemy;

		public bool staminaColor;

		public bool ammoColor;

		public float stamSecondBrightness;

		public float ammoSecondBrightness;

		public List<ResourcePackManager.HpStop> stamOwnStops;

		public List<ResourcePackManager.HpStop> stamEnemyStops;

		public List<ResourcePackManager.HpStop> ammoStops;

		public ResourcePackManager.HpPalette stamOwn;

		public ResourcePackManager.HpPalette stamEnemy;

		public ResourcePackManager.HpPalette ammo;

		public bool knightStaminaOnly;

		public bool ammoRing;

		public bool ammoNumber;

		public float numSize;

		public float numX;

		public float numY;

		public Color numColor;

		public bool numByAmmo;

		public bool numOutline;

		public bool numBold;

		public bool selShiftOn;

		public float selShift;

		public bool hpNumOwn;

		public bool hpNumEnemy;

		public bool stamNumOwn;

		public bool stamNumEnemy;

		public bool stamNumKnightOnly;

		public bool statHideFull;

		public int statFormat;

		public float statSize;

		public float statAlpha;

		public bool statBold;

		public bool statOutline;

		public float hpNumX;

		public float hpNumY;

		public float stamNumX;

		public float stamNumY;

		public bool hpNumByPalette;

		public bool stamNumByPalette;

		public Color hpNumColor;

		public Color stamNumColor;

		public bool pathOwn;

		public bool pathSelectedOnly;

		public bool pathOnTop;

		public bool pathMarker;

		public float pathWidth;

		public float pathLift;

		public float pathAlpha;

		public Color pathOwnColor;

		public bool pathPredict;

		public bool pathTrail;

		public bool bombMap;

		public bool bombMap3D;

		public bool bombMapNumbers;

		public float bombMapAlpha;

		public int bombMapLayers;

		public float bombMapYOffset;

		public bool bombMapAllNumbers;

		public Color[] bombColors;

		public float[] bombThr;

		public float bombNumAlpha;

		public bool hitOwn;

		public bool hitEnemy;

		public bool hitDps;

		public float hitTime;

		public float hitSize;

		public float hitX;

		public float hitY;

		public float dpsSilence;

		public float dpsShow;

		public Color hitOwnColor;

		public Color hitEnemyColor;

		public Color dpsColor;

		public bool crystalTimer;

		public float crystalSize;

		public float crystalX;

		public float crystalY;

		public float crystalLift;

		public Color crystalUpColor;

		public Color crystalDownColor;

		public bool dpsPrecise;

		public bool perfMode;

		public int bombMapRange;

		public float bombNumDist;

		public bool statHideFar;

		public float statHideDist;

		public int bombBandMask;

		public bool bombByDamage;

		public float[] bombDmg;

		public bool regenNumOwn;

		public bool regenNumEnemy;

		public float regenNumY;

		public Color regenNumColor;

		public bool bldColor;

		public List<ResourcePackManager.HpStop> bldOwnStops;

		public List<ResourcePackManager.HpStop> bldEnemyStops;

		public float bldSecondBrightness;

		public ResourcePackManager.HpPalette bldOwn;

		public ResourcePackManager.HpPalette bldEnemy;

		public bool bldHpNumOwn;

		public bool bldHpNumEnemy;

		public bool bldRegenOwn;

		public bool bldRegenEnemy;

		public float bldHpNumY;

		public float bldRegenY;

		public bool hitStamOwn;

		public bool hitStamEnemy;

		public bool dpsStamina;

		public bool hitBldOwn;

		public bool hitBldEnemy;

		public bool hitBldDps;

		public bool hitFullDamage;

		public Color hitStamColor;

		public bool centerOn;

		public bool centerOwnUnits;

		public bool centerEnemyUnits;

		public bool centerOwnObjects;

		public bool centerEnemyObjects;

		public float centerSize;

		public float centerAlpha;

		public Color centerOwnColor;

		public Color centerEnemyColor;

		public bool altOnly;

		public bool altNumbers;

		public bool altAmmo;

		public bool altDamage;

		public bool altCrystal;

		public bool altBombMap;

		public bool altRings;

		public bool altPaths;

		public bool centerDist;

		public float centerDistSize;

		public float centerDistTime;

		public bool menuPause;

		public bool menuBlockInput;
	}

	private class HpOverride
	{
		public string name;

		public int id;

		public bool color;

		public bool mult;

		public float f;

		public Color c;

		public int slot;
	}

	private class HpStop
	{
		public float pct;

		public Color col;
	}

	private class AutoRing
	{
		public Labor labor;

		public GameObject go;

		public SelectionCircle circle;

		public Renderer rend;

		public bool enemy;

		public float nextAdapt;
	}

	private class EnemyMat
	{
		public Material mat;

		public string prop;

		public Texture orig;

		public int id;

		public Renderer rend;

		public int slot;

		public Material shared;
	}

	private class AmmoUnit
	{
		public Labor labor;

		public CharacterState state;

		public Vector3 localOffset;
	}

	private class StatUnit
	{
		public Labor labor;

		public BricktronDamageReceiver dr;

		public Vector3 localOffset;

		public bool enemy;

		public bool knight;

		public float regenRem;

		public float regenRead = -10f;

		private string hpStr, stamStr;

		private float hpC = -1f, hpM = -1f, stC = -1f, stM = -1f;

		private int hpF = -1, stF = -1;

		public string HpText(float cur, float max, int fmt)
		{
			if (hpStr == null || hpC != cur || hpM != max || hpF != fmt)
			{
				hpC = cur; hpM = max; hpF = fmt;
				hpStr = ResourcePackManager.FormatStat(cur, max, fmt);
			}
			return hpStr;
		}

		public string StamText(float cur, float max, int fmt)
		{
			if (stamStr == null || stC != cur || stM != max || stF != fmt)
			{
				stC = cur; stM = max; stF = fmt;
				stamStr = ResourcePackManager.FormatStat(cur, max, fmt);
			}
			return stamStr;
		}
	}

	private class PathUnit
	{
		public Labor labor;

		public readonly List<Vector3> pts = new List<Vector3>();

		public bool trail;
	}

	private class TrailData
	{
		public readonly List<Vector3> pts = new List<Vector3>();

		public float lastMove;

		public int scanId;
	}

	private class PredReq
	{
		public Labor labor;

		public SearchPathRequest req;

		public Vector3 dest;

		public bool exact;

		public bool retried;

		public int stage;

		public int calls;

		public float created;
	}

	private class PredRoute
	{
		public readonly List<Vector3> pts = new List<Vector3>();

		public int progress;

		public float created;

		public float lastMove;

		public Vector3 lastPos;

		public bool moved;
	}

	private class BombCell
	{
		public Vector3 center;

		public int value;

		public float t;

		public string text;

		public bool layer;

		public int band;
	}

	private class BombBarrel
	{
		public Explosifs ex;

		public ExplosionGameplay.FalloffCurve curve;

		public bool useTable;

		public float maxV = 1f;

		public Vector3 cc;

		public int vx;

		public int vy;

		public int vz;

		public int id;
	}

	private class HitFloat
	{
		public Vector3 localOffset;

		public Vector3 world;

		public float amount;

		public float t0;

		public float lastHit;

		public bool enemy;

		public bool dps;

		public string text;

		public Transform tr;

		public bool stam;
	}

	private class HitTrack
	{
		public HitTrack()
		{
			this.losses = new List<ResourcePackManager.HitLoss>();
			this.evts = new List<ResourcePackManager.HitEvt>();
		}

		public Vector3 localOffset;

		public Vector3 lastWorld;

		public bool enemy;

		public float lastHp = -1f;

		public float lastSeen;

		public float burstStart;

		public float burstLast;

		public float burstTotal;

		public int burstHits;

		public ResourcePackManager.HitFloat lastFloat;

		public Transform tr;

		public DamageReceiver dr;

		public BricktronDamageReceiver bdr;

		public bool building;

		public string victimKey;

		public float lastStam = -1f;

		public float deadAt;

		public ResourcePackManager.HitFloat lastStamFloat;

		public DamageReceiver.DamageReactionDelegate handler;

		public readonly List<ResourcePackManager.HitLoss> losses;

		public readonly List<ResourcePackManager.HitEvt> evts;

		public float lastEvtAt = -100f;

		public string lastEvtKey;

		public bool goneDone;
	}

	private class CrystalTrack
	{
		public IsCapturable cap;

		public CaptureClock clock;

		public CaptureProgress prog;

		public float lastCur = -1f;

		public float lastChange;

		public int dir;

		public int good = 1;

		public int lastRel;

		public int shownSecs = -1;

		public string text = string.Empty;
	}

	private class BombGroup
	{
		public readonly List<ResourcePackManager.BombBarrel> members = new List<ResourcePackManager.BombBarrel>();

		public readonly List<ResourcePackManager.BombCell> cells = new List<ResourcePackManager.BombCell>();

		public readonly List<ResourcePackManager.BombCell> numCells = new List<ResourcePackManager.BombCell>();

		public readonly List<Mesh> meshes = new List<Mesh>();

		public Vector3 center;

		public float reach;

		public int sig;

		public int meshSig = int.MinValue;

		public bool built;
	}

	private class BldUnit
	{
		public Transform tr;

		public DamageReceiver dr;

		public Vector3 localOffset;

		public bool enemy;

		public bool none;

		public float regenRem;

		public float regenRead = -10f;

		private string hpStr, stamStr;

		private float hpC = -1f, hpM = -1f, stC = -1f, stM = -1f;

		private int hpF = -1, stF = -1;

		public string HpText(float cur, float max, int fmt)
		{
			if (hpStr == null || hpC != cur || hpM != max || hpF != fmt)
			{
				hpC = cur; hpM = max; hpF = fmt;
				hpStr = ResourcePackManager.FormatStat(cur, max, fmt);
			}
			return hpStr;
		}

		public string StamText(float cur, float max, int fmt)
		{
			if (stamStr == null || stC != cur || stM != max || stF != fmt)
			{
				stC = cur; stM = max; stF = fmt;
				stamStr = ResourcePackManager.FormatStat(cur, max, fmt);
			}
			return stamStr;
		}

		public string victimKey;

		public LocalFactionVision lv;

		public bool lvDone;
	}

	private class HitLoss
	{
		public float t;

		public float gt;

		public float hp;

		public float stam;

		public bool lethal;

		public bool stamDone;
	}

	private class HitEvt
	{
		public float t;

		public DamageReaction reaction;

		public string key;
	}

	private class CenterEnt
	{
		public Transform tr;

		public readonly List<Transform> pts = new List<Transform>();

		public bool skip;

		public bool enemy;

		public LocalFactionVision lv;

		public bool lvDone;
	}

	private class DistFloat
	{
		public Transform pt;

		public Vector3 world;

		public string text;

		public bool enemy;

		public float amount;

		public float t0;
	}
}
}
