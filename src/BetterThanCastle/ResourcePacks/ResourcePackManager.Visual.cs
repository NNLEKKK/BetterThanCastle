using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
    /// <summary>
    /// Bridge between the Visuals tab (UI/Visual/BtcVisualTab.cs, new menu style) and the private colored-HP config (HpConfig) of ResourcePackManager.
    /// It declares every setting once (key, page, section, EN/RU label, getter/setter with the side effect the legacy menu used) so the tab can draw,
    /// search, undo and export them. cfg keys and behaviour are untouched: setters call the same MarkHpDirty / HpStructureEdited / HpPaletteEdited.
    /// </summary>
    public partial class ResourcePackManager
    {
        internal static bool VisualActive { get { return instance != null && instance.menuHp; } }

        private static HpConfig VC { get { return instance != null ? instance.hpCfg : null; } }
        private static void VDirty() { instance.MarkHpDirty(); }
        private static void VStruct() { instance.HpStructureEdited(); }
        private static void VPal() { instance.HpPaletteEdited(); }
        private static bool _visualBuilt;

        // ---- declaration helpers
        private static BtcVisualSetting Add(BtcVisualSetting s) { BtcVisualData.All.Add(s); return s; }

        private static void B(string key, string page, string sec, int ind, bool exp, string en, string ru, Func<bool> vis, Func<bool> g, Action<bool> s)
        {
            Add(new BtcVisualSetting { Key = key, Page = page, Section = sec, Indent = ind, Expert = exp, En = en, Ru = ru, Vis = vis, Kind = VKind.Bool, GetB = g, SetB = s });
        }

        private static void F(string key, string page, string sec, int ind, string en, string ru, Func<bool> vis, float min, float max, float step, string fmt, string suf, Func<float> g, Action<float> s)
        {
            Add(new BtcVisualSetting { Key = key, Page = page, Section = sec, Indent = ind, Expert = true, En = en, Ru = ru, Vis = vis, Kind = VKind.Float, Min = min, Max = max, Step = step, Fmt = fmt, Suf = suf, GetF = g, SetF = s });
        }

        private static void Cl(string key, string page, string sec, int ind, string en, string ru, Func<bool> vis, Func<Color> g, Action<Color> s)
        {
            Add(new BtcVisualSetting { Key = key, Page = page, Section = sec, Indent = ind, Expert = true, En = en, Ru = ru, Vis = vis, Kind = VKind.Color, GetC = g, SetC = s });
        }

        private static Color RgbKeep(Color nw, Color old) { nw.a = old.a; return nw; }
        private static Color RgbOne(Color nw) { nw.a = 1f; return nw; }

        /// <summary>Boolean setting stored as a plain flag that only needs the dirty mark.</summary>
        private static void Bd(string key, string page, string sec, int ind, bool exp, string en, string ru, Func<bool> vis, Func<bool> g, Action<bool> s)
        {
            B(key, page, sec, ind, exp, en, ru, vis, g, delegate (bool v) { s(v); VDirty(); });
        }

        internal static void EnsureVisualBuilt()
        {
            if (_visualBuilt) return;
            _visualBuilt = true;
            BuildVisual();
        }

        private static void BuildVisual()
        {
            BtcVisualData.Ready = delegate { return VC != null; };
            BtcVisualData.Reset = delegate { instance.ResetHpConfigToDefaults(); };
            BtcVisualData.GetMaster = delegate { return instance != null && instance.coloredHp; };
            BtcVisualData.SetMaster = delegate (bool v) { instance.SetColoredHp(v); };
            BtcVisualData.GetDiscord = delegate { return instance != null && instance.discordOn; };
            BtcVisualData.SetDiscord = delegate (bool v) { instance.SetDiscord(v); };
            BtcVisualData.ResetArmed = delegate { return instance != null && Time.unscaledTime < instance.hpResetArmedUntil; };
            BtcVisualData.ArmReset = delegate { instance.hpResetArmedUntil = Time.unscaledTime + 3f; };
            BtcVisualData.GetAutoGamma = delegate { return instance != null && instance.autoGamma; };
            BtcVisualData.SetAutoGamma = delegate (bool v) { instance.autoGamma = v; instance.optSaveAt = Time.unscaledTime + 0.6f; instance.dirLightsNext = 0f; instance.lumRef = 0f; };
            BtcVisualRows.ColorGet = delegate (int id) { return instance != null ? instance.GetExtraColor(id) : Color.white; };
            BtcVisualRows.ColorSet = delegate (int id, Color c) { if (instance != null) instance.SetExtraColor(id, c); };

            // ============================================================ RINGS: what is colored
            const string R = "rings";
            B("ring.hp", R, "what", 0, false, "Color HP ring (ring 1)", "Красить кольцо HP (кольцо 1)", null, () => VC.rings[0], v => { VC.rings = new bool[] { v, VC.rings[1] }; VStruct(); });
            B("ring.stam", R, "what", 0, false, "Color stamina ring (knight, ring 2)", "Красить кольцо стамины (рыцарь, кольцо 2)", null, () => VC.staminaColor, v => { VC.staminaColor = v; VStruct(); });
            B("ring.ammo", R, "what", 0, false, "Color ammo ring (alchemist, ring 2)", "Красить кольцо патронов (алхимик, кольцо 2)", null, () => VC.ammoColor, v => { VC.ammoColor = v; VStruct(); });
            B("ring.enemy", R, "what", 0, false, "Color enemy rings (off = vanilla)", "Красить кольца врагов (выкл = ваниль)", null, () => VC.colorEnemies, v => { VC.colorEnemies = v; VStruct(); });
            B("ring.enemypal", R, "what", 1, false, "Separate enemy palettes", "Отдельные палитры для врагов", () => VC.colorEnemies, () => VC.enemyPalette, v => { VC.enemyPalette = v; VStruct(); });
            Bd("ring.sel", R, "what", 0, false, "Selected unit: rings lighter / darker", "Выбранный юнит: кольца светлее / темнее", null, () => VC.selShiftOn, v => VC.selShiftOn = v);
            F("ring.selshift", R, "what", 1, "Shift (- darker, + lighter)", "Сдвиг (- темнее, + светлее)", () => VC.selShiftOn, -60f, 60f, 5f, "+0;-0;0", "%", () => VC.selShift * 100f, v => { VC.selShift = v / 100f; VDirty(); });

            // palettes (custom editor)
            AddPalette("pal.hp", "HP - own", "HP - свои", null, () => VC.ownStops, () => VC.secondBrightness, v => VC.secondBrightness = v, true);
            AddPalette("pal.hpe", "HP - enemy", "HP - враги", () => VC.colorEnemies && VC.enemyPalette, () => VC.enemyStops, null, null, false);
            AddPalette("pal.st", "Stamina - knight", "Стамина - рыцарь", null, () => VC.stamOwnStops, () => VC.stamSecondBrightness, v => VC.stamSecondBrightness = v, true);
            AddPalette("pal.ste", "Stamina - enemy knight", "Стамина - вражеский рыцарь", () => VC.colorEnemies && VC.enemyPalette, () => VC.stamEnemyStops, null, null, false);
            AddPalette("pal.am", "Ammo - alchemist (10 bombs = 100%)", "Патроны - алхимик (10 бомб = 100%)", null, () => VC.ammoStops, () => VC.ammoSecondBrightness, v => VC.ammoSecondBrightness = v, true);
            Bd("pal.bld", R, "pal", 0, false, "Separate HP colors for buildings (storage, racks, crystals...)", "Отдельные цвета HP для зданий (склады, стойки, кристаллы...)", null, () => VC.bldColor, v => VC.bldColor = v);
            AddPalette("pal.bo", "HP - buildings own", "HP - здания свои", () => VC.bldColor, () => VC.bldOwnStops, () => VC.bldSecondBrightness, v => VC.bldSecondBrightness = v, true);
            AddPalette("pal.be", "HP - buildings enemy", "HP - здания врагов", () => VC.bldColor && VC.colorEnemies && VC.enemyPalette, () => VC.bldEnemyStops, null, null, false);

            // visibility
            B("vis.own", R, "vis", 0, false, "Always show own rings (even if not selected / not hurt)", "Всегда показывать свои кольца (даже без выбора и ран)", null, () => VC.alwaysOwn, v => { VC.alwaysOwn = v; VStruct(); });
            B("vis.enemy", R, "vis", 0, false, "Always show enemy rings (even if not selected / not hurt)", "Всегда показывать кольца врагов (даже без выбора и ран)", null, () => VC.alwaysEnemy, v => { VC.alwaysEnemy = v; VStruct(); });
            B("vis.knight", R, "vis", 0, false, "Stamina ring only for knights", "Кольцо стамины только у рыцарей", null, () => VC.knightStaminaOnly, v => { VC.knightStaminaOnly = v; VStruct(); });
            B("vis.ammo", R, "vis", 0, false, "Alchemist ammo ring (bar)", "Кольцо патронов алхимика (полоса)", null, () => VC.ammoRing, v => { VC.ammoRing = v; VStruct(); });
            B("vis.alt", R, "vis", 0, false, "Show overlays only while LeftAlt is held", "Показывать оверлеи только при зажатом LeftAlt", null, () => VC.altOnly, v => { VC.altOnly = v; VStruct(); });
            Func<bool> alt = () => VC.altOnly;
            Bd("alt.num", R, "vis", 1, true, "Numbers: HP / stamina / regen (units, buildings)", "Числа: HP / стамина / реген (юниты, здания)", alt, () => VC.altNumbers, v => VC.altNumbers = v);
            Bd("alt.ammo", R, "vis", 1, true, "Alchemist bomb count", "Счётчик бомб алхимика", alt, () => VC.altAmmo, v => VC.altAmmo = v);
            Bd("alt.dmg", R, "vis", 1, true, "Damage numbers and DPS", "Числа урона и DPS", alt, () => VC.altDamage, v => VC.altDamage = v);
            Bd("alt.crys", R, "vis", 1, true, "Crystal timer", "Таймер кристалла", alt, () => VC.altCrystal, v => VC.altCrystal = v);
            Bd("alt.bomb", R, "vis", 1, true, "Bomb map (cells and numbers)", "Карта бомб (клетки и числа)", alt, () => VC.altBombMap, v => VC.altBombMap = v);
            Bd("alt.rings", R, "vis", 1, true, "Forced rings (always show own / enemy)", "Принудительные кольца (всегда свои / враги)", alt, () => VC.altRings, v => VC.altRings = v);
            Bd("alt.path", R, "vis", 1, true, "Path lines", "Линии пути", alt, () => VC.altPaths, v => VC.altPaths = v);

            // bomb counter (alchemist)
            Bd("ammo.on", R, "ammo", 0, false, "Show bomb count (0-10)", "Показывать число бомб (0-10)", null, () => VC.ammoNumber, v => VC.ammoNumber = v);
            Func<bool> am = () => VC.ammoNumber;
            F("ammo.size", R, "ammo", 1, "Size", "Размер", am, 0.3f, 4f, 0.05f, "0.00", "x", () => VC.numSize, v => { VC.numSize = v; VDirty(); });
            F("ammo.x", R, "ammo", 1, "Offset X", "Смещение X", am, -200f, 200f, 1f, "0", " px", () => VC.numX, v => { VC.numX = v; VDirty(); });
            F("ammo.y", R, "ammo", 1, "Offset Y (up)", "Смещение Y (вверх)", am, -200f, 200f, 1f, "0", " px", () => VC.numY, v => { VC.numY = v; VDirty(); });
            F("ammo.alpha", R, "ammo", 1, "Opacity", "Непрозрачность", am, 10f, 100f, 5f, "0", "%", () => VC.numColor.a * 100f, v => { Color c = VC.numColor; c.a = v / 100f; VC.numColor = c; VDirty(); });
            Bd("ammo.bypal", R, "ammo", 1, false, "Color from ammo palette", "Цвет из палитры патронов", am, () => VC.numByAmmo, v => VC.numByAmmo = v);
            Cl("ammo.color", R, "ammo", 1, "Color", "Цвет", () => VC.ammoNumber && !VC.numByAmmo, () => VC.numColor, c => { VC.numColor = RgbKeep(c, VC.numColor); VDirty(); });
            Bd("ammo.outline", R, "ammo", 1, true, "Outline", "Контур", am, () => VC.numOutline, v => VC.numOutline = v);
            Bd("ammo.bold", R, "ammo", 1, true, "Bold", "Жирный", am, () => VC.numBold, v => VC.numBold = v);

            // HP / stamina numbers
            Bd("n.hp.own", R, "num", 0, false, "HP number - own / allied", "Число HP - свои / союзники", null, () => VC.hpNumOwn, v => VC.hpNumOwn = v);
            Bd("n.hp.enemy", R, "num", 0, false, "HP number - enemy (visible only)", "Число HP - враги (только видимые)", null, () => VC.hpNumEnemy, v => VC.hpNumEnemy = v);
            Bd("n.st.own", R, "num", 0, false, "Stamina number - own / allied", "Число стамины - свои / союзники", null, () => VC.stamNumOwn, v => VC.stamNumOwn = v);
            Bd("n.st.enemy", R, "num", 0, false, "Stamina number - enemy (visible only)", "Число стамины - враги (только видимые)", null, () => VC.stamNumEnemy, v => VC.stamNumEnemy = v);
            Bd("n.rg.own", R, "num", 0, false, "Regen countdown (sec) - own / allied", "Обратный отсчёт регена (с) - свои", null, () => VC.regenNumOwn, v => VC.regenNumOwn = v);
            Bd("n.rg.enemy", R, "num", 0, false, "Regen countdown (sec) - enemy (visible only)", "Обратный отсчёт регена (с) - враги (только видимые)", null, () => VC.regenNumEnemy, v => VC.regenNumEnemy = v);
            Func<bool> anyN = () => VC.hpNumOwn || VC.hpNumEnemy || VC.stamNumOwn || VC.stamNumEnemy || VC.regenNumOwn || VC.regenNumEnemy;
            Func<bool> anySt = () => VC.stamNumOwn || VC.stamNumEnemy;
            Func<bool> anyHp = () => VC.hpNumOwn || VC.hpNumEnemy;
            Func<bool> anyRg = () => VC.regenNumOwn || VC.regenNumEnemy;
            Bd("n.knight", R, "num", 1, false, "Stamina numbers only for knights", "Числа стамины только у рыцарей", anySt, () => VC.stamNumKnightOnly, v => VC.stamNumKnightOnly = v);
            Bd("n.hidefull", R, "num", 1, false, "Hide when full", "Скрывать при полном значении", anyN, () => VC.statHideFull, v => VC.statHideFull = v);
            Bd("n.hidefar", R, "num", 1, false, "Hide when camera is far from the unit", "Скрывать, если камера далеко от юнита", anyN, () => VC.statHideFar, v => VC.statHideFar = v);
            F("n.fardist", R, "num", 2, "Camera distance", "Дистанция камеры", () => anyN() && VC.statHideFar, 10f, 300f, 5f, "0", " vox", () => VC.statHideDist, v => { VC.statHideDist = v; VDirty(); });
            Add(new BtcVisualSetting
            {
                Key = "n.format", Page = R, Section = "num", Indent = 1, En = "Format", Ru = "Формат", Vis = anyN, Kind = VKind.Choice, Choices = new[] { "42", "42/100", "42%" },
                GetI = () => Mathf.Clamp(VC.statFormat, 0, 2), SetI = v => { VC.statFormat = v; VDirty(); }
            });
            F("n.size", R, "num", 1, "Size", "Размер", anyN, 0.3f, 3f, 0.05f, "0.00", "x", () => VC.statSize, v => { VC.statSize = v; VDirty(); });
            F("n.alpha", R, "num", 1, "Opacity", "Непрозрачность", anyN, 10f, 100f, 5f, "0", "%", () => VC.statAlpha * 100f, v => { VC.statAlpha = v / 100f; VDirty(); });
            Bd("n.bold", R, "num", 1, true, "Bold", "Жирный", anyN, () => VC.statBold, v => VC.statBold = v);
            Bd("n.outline", R, "num", 1, true, "Outline", "Контур", anyN, () => VC.statOutline, v => VC.statOutline = v);
            F("n.hp.x", R, "num", 1, "HP number: offset X", "Число HP: смещение X", anyHp, -200f, 200f, 1f, "0", " px", () => VC.hpNumX, v => { VC.hpNumX = v; VDirty(); });
            F("n.hp.y", R, "num", 1, "HP number: offset Y (up)", "Число HP: смещение Y (вверх)", anyHp, -200f, 200f, 1f, "0", " px", () => VC.hpNumY, v => { VC.hpNumY = v; VDirty(); });
            Bd("n.hp.pal", R, "num", 1, false, "HP number: color from HP palette", "Число HP: цвет из палитры HP", anyHp, () => VC.hpNumByPalette, v => VC.hpNumByPalette = v);
            Cl("n.hp.col", R, "num", 1, "HP number: color", "Число HP: цвет", () => anyHp() && !VC.hpNumByPalette, () => VC.hpNumColor, c => { VC.hpNumColor = RgbKeep(c, VC.hpNumColor); VDirty(); });
            F("n.st.x", R, "num", 1, "Stamina number: offset X", "Число стамины: смещение X", anySt, -200f, 200f, 1f, "0", " px", () => VC.stamNumX, v => { VC.stamNumX = v; VDirty(); });
            F("n.st.y", R, "num", 1, "Stamina number: offset Y (up)", "Число стамины: смещение Y (вверх)", anySt, -200f, 200f, 1f, "0", " px", () => VC.stamNumY, v => { VC.stamNumY = v; VDirty(); });
            Bd("n.st.pal", R, "num", 1, false, "Stamina number: color from stamina palette", "Число стамины: цвет из палитры стамины", anySt, () => VC.stamNumByPalette, v => VC.stamNumByPalette = v);
            Cl("n.st.col", R, "num", 1, "Stamina number: color", "Число стамины: цвет", () => anySt() && !VC.stamNumByPalette, () => VC.stamNumColor, c => { VC.stamNumColor = RgbKeep(c, VC.stamNumColor); VDirty(); });
            F("n.rg.y", R, "num", 1, "Regen number: offset Y (up)", "Число регена: смещение Y (вверх)", anyRg, -200f, 200f, 1f, "0", " px", () => VC.regenNumY, v => { VC.regenNumY = v; VDirty(); });
            Cl("n.rg.col", R, "num", 1, "Regen number: color", "Число регена: цвет", anyRg, () => VC.regenNumColor, c => { VC.regenNumColor = RgbKeep(c, VC.regenNumColor); VDirty(); });
            Bd("n.b.hp.own", R, "num", 0, false, "HP number - own buildings", "Число HP - свои здания", null, () => VC.bldHpNumOwn, v => VC.bldHpNumOwn = v);
            Bd("n.b.hp.enemy", R, "num", 0, false, "HP number - enemy buildings (visible only)", "Число HP - здания врагов (только видимые)", null, () => VC.bldHpNumEnemy, v => VC.bldHpNumEnemy = v);
            Bd("n.b.rg.own", R, "num", 0, false, "Regen countdown - own buildings", "Отсчёт регена - свои здания", null, () => VC.bldRegenOwn, v => VC.bldRegenOwn = v);
            Bd("n.b.rg.enemy", R, "num", 0, false, "Regen countdown - enemy buildings (visible only)", "Отсчёт регена - здания врагов (только видимые)", null, () => VC.bldRegenEnemy, v => VC.bldRegenEnemy = v);
            F("n.b.hp.y", R, "num", 1, "Building HP number: offset Y (up)", "Число HP зданий: смещение Y (вверх)", () => VC.bldHpNumOwn || VC.bldHpNumEnemy, -200f, 200f, 1f, "0", " px", () => VC.bldHpNumY, v => { VC.bldHpNumY = v; VDirty(); });
            F("n.b.rg.y", R, "num", 1, "Building regen number: offset Y (up)", "Число регена зданий: смещение Y (вверх)", () => VC.bldRegenOwn || VC.bldRegenEnemy, -200f, 200f, 1f, "0", " px", () => VC.bldRegenY, v => { VC.bldRegenY = v; VDirty(); });

            // entity center
            Bd("c.on", R, "center", 0, false, "Show entity center (dot)", "Показывать центр сущности (точка)", null, () => VC.centerOn, v => VC.centerOn = v);
            Bd("c.dist", R, "center", 0, false, "Explosion distance to the center (number after a blast) - HEAVY", "Расстояние взрыва до центра (число после взрыва) - ТЯЖЁЛО", null, () => VC.centerDist, v => VC.centerDist = v);
            Func<bool> cAny = () => VC.centerOn || VC.centerDist;
            Bd("c.uo", R, "center", 1, false, "Units - own / allied", "Юниты - свои / союзники", cAny, () => VC.centerOwnUnits, v => VC.centerOwnUnits = v);
            Bd("c.ue", R, "center", 1, false, "Units - enemy (visible only)", "Юниты - враги (только видимые)", cAny, () => VC.centerEnemyUnits, v => VC.centerEnemyUnits = v);
            Bd("c.oo", R, "center", 1, false, "Buildings / objects - own", "Здания / объекты - свои", cAny, () => VC.centerOwnObjects, v => VC.centerOwnObjects = v);
            Bd("c.oe", R, "center", 1, false, "Buildings / objects - enemy (visible only)", "Здания / объекты - враги (только видимые)", cAny, () => VC.centerEnemyObjects, v => VC.centerEnemyObjects = v);
            F("c.dsize", R, "center", 1, "Distance number size", "Размер числа расстояния", () => VC.centerDist, 0.3f, 3f, 0.05f, "0.00", "x", () => VC.centerDistSize, v => { VC.centerDistSize = v; VDirty(); });
            F("c.dtime", R, "center", 1, "Distance number stays for", "Число расстояния держится", () => VC.centerDist, 0.5f, 6f, 0.1f, "0.0", " s", () => VC.centerDistTime, v => { VC.centerDistTime = v; VDirty(); });
            F("c.size", R, "center", 1, "Dot size", "Размер точки", () => VC.centerOn, 0.3f, 4f, 0.05f, "0.00", "x", () => VC.centerSize, v => { VC.centerSize = v; VDirty(); });
            F("c.alpha", R, "center", 1, "Dot opacity", "Непрозрачность точки", () => VC.centerOn, 10f, 100f, 5f, "0", "%", () => VC.centerAlpha * 100f, v => { VC.centerAlpha = v / 100f; VDirty(); });
            Cl("c.col.own", R, "center", 1, "Color own / allied", "Цвет своих / союзников", () => VC.centerOn, () => VC.centerOwnColor, c => { VC.centerOwnColor = RgbOne(c); VDirty(); });
            Cl("c.col.enemy", R, "center", 1, "Color enemy", "Цвет врагов", () => VC.centerOn, () => VC.centerEnemyColor, c => { VC.centerEnemyColor = RgbOne(c); VDirty(); });

            // advanced
            F("adv.scan", R, "adv", 0, "Ring search every (s)", "Поиск колец каждые (с)", null, 1f, 60f, 1f, "0", " s", () => VC.scanInterval, v => { VC.scanInterval = v; VDirty(); });
            BtcVisualData.OvNames = new string[HpKnownFloatProps.Length + HpKnownColorProps.Length];
            for (int i = 0; i < HpKnownFloatProps.Length; i++) BtcVisualData.OvNames[i] = HpKnownFloatProps[i];
            for (int i = 0; i < HpKnownColorProps.Length; i++) BtcVisualData.OvNames[HpKnownFloatProps.Length + i] = HpKnownColorProps[i];
            BtcVisualData.OvIsColor = i => i >= HpKnownFloatProps.Length;
            BtcVisualData.OvOn = (t, i) => FindHpOverride(OvList(t), BtcVisualData.OvNames[i]) != null;
            BtcVisualData.OvMult = (t, i) => { HpOverride o = FindHpOverride(OvList(t), BtcVisualData.OvNames[i]); return o != null && o.mult; };
            BtcVisualData.OvToggle = delegate (int t, int i)
            {
                List<HpOverride> list = OvList(t);
                string name = BtcVisualData.OvNames[i];
                HpOverride o = FindHpOverride(list, name);
                if (o == null)
                {
                    bool col = i >= HpKnownFloatProps.Length;
                    o = new HpOverride { name = name, id = Shader.PropertyToID(name), color = col, mult = !col, f = 1f, c = new Color(0.35f, 0.05f, 0.05f, 1f) };
                    list.Add(o);
                }
                else list.Remove(o);
                VStruct();
            };
            BtcVisualData.OvF = (t, i) => { HpOverride o = FindHpOverride(OvList(t), BtcVisualData.OvNames[i]); return o != null ? o.f : 1f; };
            BtcVisualData.OvSetF = (t, i, v) => { HpOverride o = FindHpOverride(OvList(t), BtcVisualData.OvNames[i]); if (o != null) { o.f = v; VDirty(); } };
            BtcVisualData.OvC = (t, i) => { HpOverride o = FindHpOverride(OvList(t), BtcVisualData.OvNames[i]); return o != null ? o.c : Color.white; };
            BtcVisualData.OvSetC = (t, i, c) => { HpOverride o = FindHpOverride(OvList(t), BtcVisualData.OvNames[i]); if (o != null) { o.c = c; VDirty(); } };

            // ============================================================ WORLD: path lines + explosion map
            const string W = "world";
            Bd("p.on", W, "path", 0, false, "Show routes", "Показывать маршруты", null, () => VC.pathOwn, v => VC.pathOwn = v);
            Func<bool> po = () => VC.pathOwn;
            Bd("p.sel", W, "path", 1, false, "Only selected units", "Только выбранные юниты", po, () => VC.pathSelectedOnly, v => VC.pathSelectedOnly = v);
            Bd("p.pred", W, "path", 1, false, "Client: predict route from my orders", "Клиент: предсказывать маршрут по моим приказам", () => VC.pathOwn && !NetworkServer.active, () => VC.pathPredict, v => VC.pathPredict = v);
            Bd("p.trail", W, "path", 1, false, "Client: movement trail (fallback)", "Клиент: след движения (запасной)", () => VC.pathOwn && !NetworkServer.active, () => VC.pathTrail, v => VC.pathTrail = v);
            F("p.w", W, "path", 1, "Line width", "Толщина линии", po, 1f, 12f, 0.5f, "0.0", " px", () => VC.pathWidth, v => { VC.pathWidth = v; VDirty(); });
            F("p.lift", W, "path", 1, "Height above ground", "Высота над землёй", po, 0f, 2f, 0.05f, "0.00", "", () => VC.pathLift, v => { VC.pathLift = v; VDirty(); });
            F("p.alpha", W, "path", 1, "Opacity", "Непрозрачность", po, 10f, 100f, 5f, "0", "%", () => VC.pathAlpha * 100f, v => { VC.pathAlpha = v / 100f; VDirty(); });
            Bd("p.top", W, "path", 1, false, "Draw over terrain", "Рисовать поверх рельефа", po, () => VC.pathOnTop, v => VC.pathOnTop = v);
            Bd("p.mark", W, "path", 1, false, "Destination marker", "Маркер цели", po, () => VC.pathMarker, v => VC.pathMarker = v);
            Cl("p.col", W, "path", 1, "Color", "Цвет", po, () => VC.pathOwnColor, c => { VC.pathOwnColor = RgbKeep(c, VC.pathOwnColor); VDirty(); });

            Bd("b.on", W, "bomb", 0, false, "Show barrel explosion map", "Показывать карту взрывов бочек", null, () => VC.bombMap, v => VC.bombMap = v);
            Func<bool> bm = () => VC.bombMap;
            Bd("b.perf", W, "bomb", 1, false, "Performance mode (fewer barrels, 3 layers max, fewer numbers)", "Режим производительности (меньше бочек, до 3 слоёв, меньше чисел)", bm, () => VC.perfMode, v => VC.perfMode = v);
            F("b.range", W, "bomb", 1, "Range (blocks from barrel)", "Радиус (блоков от бочки)", bm, 1f, 8f, 1f, "0", " vox", () => (float)VC.bombMapRange, v => { VC.bombMapRange = (int)v; VDirty(); });
            Bd("b.3d", W, "bomb", 1, false, "3D (layers above and below)", "3D (слои выше и ниже)", bm, () => VC.bombMap3D, v => VC.bombMap3D = v);
            F("b.layers", W, "bomb", 2, "Layers up / down", "Слоёв вверх / вниз", () => VC.bombMap && VC.bombMap3D, 1f, 8f, 1f, "0", "", () => (float)VC.bombMapLayers, v => { VC.bombMapLayers = (int)v; VDirty(); });
            Bd("b.num", W, "bomb", 1, false, "Numbers (damage)", "Числа (урон)", bm, () => VC.bombMapNumbers, v => VC.bombMapNumbers = v);
            Bd("b.allnum", W, "bomb", 2, true, "Numbers on every voxel (all layers)", "Числа на каждом вокселе (все слои)", () => VC.bombMap && VC.bombMapNumbers && VC.bombMap3D, () => VC.bombMapAllNumbers, v => VC.bombMapAllNumbers = v);
            F("b.numalpha", W, "bomb", 2, "Numbers opacity", "Непрозрачность чисел", () => VC.bombMap && VC.bombMapNumbers, 0f, 100f, 1f, "0", "%", () => VC.bombNumAlpha * 100f, v => { VC.bombNumAlpha = v / 100f; VDirty(); });
            F("b.numdist", W, "bomb", 2, "Hide numbers if camera farther", "Скрывать числа, если камера дальше", () => VC.bombMap && VC.bombMapNumbers, 5f, 150f, 5f, "0", " vox", () => VC.bombNumDist, v => { VC.bombNumDist = v; VDirty(); });
            F("b.yoff", W, "bomb", 1, "Height offset", "Смещение по высоте", bm, -3f, 6f, 0.05f, "0.00", " vox", () => VC.bombMapYOffset, v => { VC.bombMapYOffset = v; VDirty(); });
            F("b.alpha", W, "bomb", 1, "Cells opacity", "Непрозрачность клеток", bm, 0f, 100f, 1f, "0", "%", () => VC.bombMapAlpha * 100f, v => { VC.bombMapAlpha = v / 100f; VDirty(); });
            string[] bandEn = { "Grey-white", "Pink", "Blue", "Green", "Yellow", "Dark orange", "Red", "Dark red" };
            string[] bandRu = { "Серо-белый", "Розовый", "Синий", "Зелёный", "Жёлтый", "Тёмно-оранжевый", "Красный", "Тёмно-красный" };
            for (int i = 0; i < 8; i++)
            {
                int bi = i;
                B("b.band" + i, W, "bomb", 1, true, "Show: " + bandEn[i], "Показывать: " + bandRu[i], bm, () => (VC.bombBandMask >> bi & 1) != 0, v => { if (((VC.bombBandMask >> bi & 1) != 0) != v) VC.bombBandMask ^= 1 << bi; VDirty(); });
            }
            Bd("b.bydmg", W, "bomb", 1, true, "Colors by fixed damage (not % of max)", "Цвета по фиксированному урону (не % от максимума)", bm, () => VC.bombByDamage, v => VC.bombByDamage = v);
            for (int j = 0; j < 8; j++)
            {
                int bj = j;
                Cl("b.col" + j, W, "bomb", 1, "Color: " + bandEn[j], "Цвет: " + bandRu[j], bm, () => VC.bombColors[bj], c => { VC.bombColors[bj] = RgbOne(c); VDirty(); });
                if (j >= 1)
                {
                    F("b.dmg" + j, W, "bomb", 2, bandEn[j] + " starts at", bandRu[j] + " начинается с", () => VC.bombMap && VC.bombByDamage, 1f, 4000f, 1f, "0", " dmg", () => VC.bombDmg[bj - 1], v => { VC.bombDmg[bj - 1] = v; VDirty(); });
                    F("b.thr" + j, W, "bomb", 2, bandEn[j] + " starts at", bandRu[j] + " начинается с", () => VC.bombMap && !VC.bombByDamage, 0.1f, 100f, 0.1f, "0.0", "% max", () => VC.bombThr[bj - 1], v => { VC.bombThr[bj - 1] = v; VDirty(); });
                }
            }

            // ============================================================ PLATES / HUD: damage counter + crystal timer
            const string P = "plates";
            Bd("d.perf", P, "dmg", 0, false, "Performance mode (lighter on FPS)", "Режим производительности (меньше нагрузка на FPS)", null, () => VC.perfMode, v => VC.perfMode = v);
            Bd("d.hit.own", P, "dmg", 0, false, "HP damage - own / allied", "Урон по HP - свои / союзники", null, () => VC.hitOwn, v => VC.hitOwn = v);
            Bd("d.hit.enemy", P, "dmg", 0, false, "HP damage - enemies", "Урон по HP - враги", null, () => VC.hitEnemy, v => VC.hitEnemy = v);
            Bd("d.st.own", P, "dmg", 0, false, "Stamina damage - own / allied", "Урон по стамине - свои / союзники", null, () => VC.hitStamOwn, v => VC.hitStamOwn = v);
            Bd("d.st.enemy", P, "dmg", 0, false, "Stamina damage - enemies", "Урон по стамине - враги", null, () => VC.hitStamEnemy, v => VC.hitStamEnemy = v);
            Bd("d.b.own", P, "dmg", 0, false, "Buildings damage - own", "Урон по зданиям - свои", null, () => VC.hitBldOwn, v => VC.hitBldOwn = v);
            Bd("d.b.enemy", P, "dmg", 0, false, "Buildings damage - enemy (visible only)", "Урон по зданиям - враги (только видимые)", null, () => VC.hitBldEnemy, v => VC.hitBldEnemy = v);
            Func<bool> hitU = () => VC.hitOwn || VC.hitEnemy || VC.hitStamOwn || VC.hitStamEnemy;
            Func<bool> hitB = () => VC.hitBldOwn || VC.hitBldEnemy;
            Func<bool> hitAny = () => hitU() || hitB();
            Func<bool> dpsAny = () => (hitU() && VC.hitDps) || (hitB() && VC.hitBldDps);
            Bd("d.dps", P, "dmg", 1, false, "Auto DPS after the fight ends - bricktrons", "Авто-DPS после боя - бриктроны", hitU, () => VC.hitDps, v => VC.hitDps = v);
            Bd("d.dpsst", P, "dmg", 2, true, "DPS counts stamina damage + HP together", "DPS считает урон по стамине + HP вместе", () => hitU() && VC.hitDps, () => VC.dpsStamina, v => VC.dpsStamina = v);
            Bd("d.bdps", P, "dmg", 1, false, "Auto DPS after the fight ends - buildings", "Авто-DPS после боя - здания", hitB, () => VC.hitBldDps, v => VC.hitBldDps = v);
            Bd("d.precise", P, "dmg", 2, true, "Super precision (DPS to thousandths)", "Супер-точность (DPS до тысячных)", dpsAny, () => VC.dpsPrecise, v => VC.dpsPrecise = v);
            F("d.silence", P, "dmg", 2, "Fight over after no damage for", "Бой окончен без урона", dpsAny, 1f, 15f, 0.5f, "0.0", " s", () => VC.dpsSilence, v => { VC.dpsSilence = v; VDirty(); });
            F("d.dpsshow", P, "dmg", 2, "DPS stays for", "DPS держится", dpsAny, 1f, 10f, 0.5f, "0.0", " s", () => VC.dpsShow, v => { VC.dpsShow = v; VDirty(); });
            Bd("d.full", P, "dmg", 1, false, "Count the killing blow in full (no overkill loss)", "Считать добивающий удар полностью (без потери на овер-килл)", hitAny, () => VC.hitFullDamage, v => VC.hitFullDamage = v);
            F("d.time", P, "dmg", 1, "Number stays for", "Число держится", hitAny, 0.3f, 3f, 0.1f, "0.0", " s", () => VC.hitTime, v => { VC.hitTime = v; VDirty(); });
            F("d.size", P, "dmg", 1, "Size", "Размер", hitAny, 0.3f, 3f, 0.05f, "0.00", "x", () => VC.hitSize, v => { VC.hitSize = v; VDirty(); });
            F("d.x", P, "dmg", 1, "Offset X (stamina numbers go left)", "Смещение X (числа стамины слева)", hitAny, -200f, 200f, 1f, "0", " px", () => VC.hitX, v => { VC.hitX = v; VDirty(); });
            F("d.y", P, "dmg", 1, "Offset Y (up)", "Смещение Y (вверх)", hitAny, -200f, 200f, 1f, "0", " px", () => VC.hitY, v => { VC.hitY = v; VDirty(); });
            Cl("d.col.own", P, "dmg", 1, "Color own / allied", "Цвет своих / союзников", hitAny, () => VC.hitOwnColor, c => { VC.hitOwnColor = RgbOne(c); VDirty(); });
            Cl("d.col.enemy", P, "dmg", 1, "Color enemy", "Цвет врагов", hitAny, () => VC.hitEnemyColor, c => { VC.hitEnemyColor = RgbOne(c); VDirty(); });
            Cl("d.col.st", P, "dmg", 1, "Color stamina damage", "Цвет урона по стамине", () => VC.hitStamOwn || VC.hitStamEnemy, () => VC.hitStamColor, c => { VC.hitStamColor = RgbOne(c); VDirty(); });
            Cl("d.col.dps", P, "dmg", 1, "DPS color", "Цвет DPS", dpsAny, () => VC.dpsColor, c => { VC.dpsColor = RgbOne(c); VDirty(); });

            Bd("cr.on", P, "crystal", 0, false, "Show seconds to capture / to lose", "Показывать секунды до захвата / потери", null, () => VC.crystalTimer, v => VC.crystalTimer = v);
            Func<bool> cr = () => VC.crystalTimer;
            F("cr.size", P, "crystal", 1, "Size", "Размер", cr, 0.3f, 4f, 0.05f, "0.00", "x", () => VC.crystalSize, v => { VC.crystalSize = v; VDirty(); });
            F("cr.x", P, "crystal", 1, "Offset X", "Смещение X", cr, -200f, 200f, 1f, "0", " px", () => VC.crystalX, v => { VC.crystalX = v; VDirty(); });
            F("cr.y", P, "crystal", 1, "Offset Y (up)", "Смещение Y (вверх)", cr, -200f, 200f, 1f, "0", " px", () => VC.crystalY, v => { VC.crystalY = v; VDirty(); });
            F("cr.lift", P, "crystal", 1, "Height above crystal", "Высота над кристаллом", cr, 0f, 8f, 0.1f, "0.0", " vox", () => VC.crystalLift, v => { VC.crystalLift = v; VDirty(); });
            Cl("cr.up", P, "crystal", 1, "Good for me (I capture / keep)", "Хорошо для меня (я захватываю / держу)", cr, () => VC.crystalUpColor, c => { VC.crystalUpColor = RgbOne(c); VDirty(); });
            Cl("cr.down", P, "crystal", 1, "Bad for me (enemy captures / I lose)", "Плохо для меня (враг захватывает / я теряю)", cr, () => VC.crystalDownColor, c => { VC.crystalDownColor = RgbOne(c); VDirty(); });
            B("timer.ms", P, "gametimer", 0, false, "Show milliseconds in the game timer", "Показывать миллисекунды в игровом таймере", null, () => BtcTimer.ShowMs, v => BtcTimer.ShowMs = v);

            // base shields (the rule itself is a host setting on the Gameplay tab; this is only how barriers look and sound for you)
            Add(new BtcVisualSetting { Key = "sh.alpha", Page = "world", Section = "shield", En = "Barrier opacity", Ru = "Непрозрачность барьеров", Kind = VKind.Float, Min = 10f, Max = 150f, Step = 5f, Fmt = "0", Suf = "%",
                GetF = () => BtcMenuState.Int("shield:alpha", 100), SetF = v => { BtcMenuState.SetInt("shield:alpha", Mathf.RoundToInt(v)); BtcShieldFx.ReadCfg(); } });
            Add(new BtcVisualSetting { Key = "sh.vol", Page = "world", Section = "shield", En = "Barrier sound volume (0 = off)", Ru = "Громкость звуков барьеров (0 = выкл)", Kind = VKind.Float, Min = 0f, Max = 100f, Step = 5f, Fmt = "0", Suf = "%",
                GetF = () => BtcMenuState.Int("shield:vol", 70), SetF = v => { BtcMenuState.SetInt("shield:vol", Mathf.RoundToInt(v)); BtcShieldFx.Preview(BtcShieldFx.K.Up); } });
            Func<bool> shSnd = () => BtcMenuState.Int("shield:vol", 70) > 0;
            B("sh.sndUp", "world", "shield", 1, false, "Sound when a barrier goes up or down", "Звук, когда барьер включается или падает", shSnd, () => BtcMenuState.Bool("shield:sndUp", true), v => { BtcMenuState.SetBool("shield:sndUp", v); BtcShieldFx.ReadCfg(); if (v) BtcShieldFx.Preview(BtcShieldFx.K.Up); });
            B("sh.sndHit", "world", "shield", 1, false, "Sound when something hits a barrier nearby", "Звук удара о барьер рядом", shSnd, () => BtcMenuState.Bool("shield:sndHit", true), v => { BtcMenuState.SetBool("shield:sndHit", v); BtcShieldFx.ReadCfg(); if (v) BtcShieldFx.Preview(BtcShieldFx.K.Hit); });
            B("sh.sndTick", "world", "shield", 1, false, "Ticks in the last 5 seconds of a countdown", "Тиканье в последние 5 секунд отсчёта", shSnd, () => BtcMenuState.Bool("shield:sndTick", true), v => { BtcMenuState.SetBool("shield:sndTick", v); BtcShieldFx.ReadCfg(); if (v) BtcShieldFx.Preview(BtcShieldFx.K.Tick); });
            B("sh.notes", "world", "shield", 0, false, "Notices (shield up / down)", "Уведомления (барьер включён / снят)", null, () => BtcMenuState.Bool("shield:notes", true), v => { BtcMenuState.SetBool("shield:notes", v); BtcShieldFx.ReadCfg(); });
            B("sh.hud", "world", "shield", 0, false, "Barrier list with timers in the top-left corner", "Список барьеров с таймерами в левом верхнем углу", null, () => BtcMenuState.Bool("shield:hud", true), v => { BtcMenuState.SetBool("shield:hud", v); BtcShieldFx.ReadCfg(); });

            // ============================================================ SCREEN
            const string S = "screen";
            Bd("s.pause", S, "menu", 0, false, "Pause the game while this menu is open", "Ставить игру на паузу, пока открыто меню", null, () => VC.menuPause, v => VC.menuPause = v);
            Bd("s.block", S, "menu", 0, false, "Block game input while this menu is open", "Блокировать ввод в игру, пока открыто меню", null, () => VC.menuBlockInput, v => VC.menuBlockInput = v);
            B("s.enemytex", S, "menu", 0, false, "Enemy skin from pack (<pack>/Enemy folder)", "Скин врагов из пака (папка <пак>/Enemy)", null, () => instance.enemyTexOn, v => instance.SetEnemyTex(v));
            B("s.gamma", S, "light", 0, false, "Auto-equalize gamma (same brightness day and night)", "Автовыравнивание гаммы (одинаковая яркость днём и ночью)", null, () => instance.autoGamma, v => BtcVisualData.SetAutoGamma(v));
            Func<bool> auto = () => instance.autoGamma;
            Func<bool> manual = () => !instance.autoGamma;
            F("s.master", S, "light", 1, "Master gamma", "Общая гамма", auto, 0.3f, 4f, 0.05f, "0.00", "x", () => instance.masterGamma, v => { instance.masterGamma = v; instance.optSaveAt = Time.unscaledTime + 0.6f; });
            F("s.day", S, "light", 1, "Day brightness", "Яркость днём", manual, 0.3f, 4f, 0.05f, "0.00", "x", () => instance.dayBright, v => { instance.dayBright = v; instance.optSaveAt = Time.unscaledTime + 0.6f; instance.dirLightsNext = 0f; });
            F("s.night", S, "light", 1, "Night brightness", "Яркость ночью", manual, 0.3f, 4f, 0.05f, "0.00", "x", () => instance.nightBright, v => { instance.nightBright = v; instance.optSaveAt = Time.unscaledTime + 0.6f; instance.dirLightsNext = 0f; });
            // gamma sliders are plain (not expert-only): they are a core display setting
            for (int i = 0; i < BtcVisualData.All.Count; i++) if (BtcVisualData.All[i].Page == S && BtcVisualData.All[i].Kind == VKind.Float) BtcVisualData.All[i].Expert = false;
        }

        private static List<HpOverride> OvList(int team) { return team == 0 ? VC.ownOv : VC.enemyOv; }

        private static void AddPalette(string key, string en, string ru, Func<bool> vis, Func<List<HpStop>> list, Func<float> second, Action<float> setSecond, bool hasSecond)
        {
            BtcVisualPalette p = new BtcVisualPalette { Key = key, En = en, Ru = ru, Vis = vis };
            p.Count = () => list().Count;
            p.Pct = i => list()[i].pct;
            p.SetPct = (i, v) => { list()[i].pct = v; VPal(); };
            p.Col = i => list()[i].col;
            p.SetCol = (i, c) => { list()[i].col = c; VPal(); };
            p.Add = delegate
            {
                List<HpStop> l = list();
                l.Add(new HpStop { pct = l.Count > 0 ? Mathf.Round(l[l.Count - 1].pct * 0.5f) : 50f, col = l.Count > 0 ? l[l.Count - 1].col : Color.white });
                VPal();
            };
            p.Remove = i => { List<HpStop> l = list(); if (l.Count > 1) { l.RemoveAt(i); VPal(); } };
            if (hasSecond) { p.Second = second; p.SetSecond = v => { setSecond(v); VPal(); }; }
            p.Snap = delegate
            {
                List<HpStop> l = list();
                BtcPalSnap s = new BtcPalSnap { Pct = new float[l.Count], Col = new Color[l.Count], Second = second != null ? second() : 0f };
                for (int i = 0; i < l.Count; i++) { s.Pct[i] = l[i].pct; s.Col[i] = l[i].col; }
                return s;
            };
            p.Restore = delegate (object o)
            {
                BtcPalSnap s = (BtcPalSnap)o;
                List<HpStop> l = list();
                l.Clear();
                for (int i = 0; i < s.Pct.Length; i++) l.Add(new HpStop { pct = s.Pct[i], col = s.Col[i] });
                if (setSecond != null) setSecond(s.Second);
                VPal();
            };
            BtcVisualData.Palettes.Add(p);
        }
    }
}

#if DEV
namespace BetterThanCastle
{
    public partial class ResourcePackManager
    {
        /// <summary>DEV: opens the mod menu on the Visuals tab (scenario command "visualtest page expert").</summary>
        internal static void DevVisual(int page, bool expert)
        {
            if (instance == null) return;
            instance.menuOpen = true;
            instance.SelectTab(1);
            BtcVisualTab.Instance.SetPage(page);
            BtcUi.SetOpen("visual:simple", !expert);
        }
    }
}
#endif

#if DEV
namespace BetterThanCastle
{
    public partial class ResourcePackManager
    {
        /// <summary>DEV: opens the mod menu on the Profile tab (scenario command "profiletest page").</summary>
        internal static void DevProfile(int page)
        {
            if (instance == null) return;
            instance.menuOpen = true;
            instance.SelectTab(6);
            BtcMenuState.SetInt("profile:page", page);
        }
    }
}
#endif
