using System;
using System.IO;
using BepInEx;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Duel tab (menu tab 8) in the new menu style: start button with state, match options, bot difficulty chips, the unit table with presets,
    /// logs folder. Settings live in BtcDuelConfig (unchanged cfg keys). Every change pushes an undo step (scope "duel"); folds are persisted.
    /// </summary>
    internal sealed class BtcDuelTab : BtcMenuTab
    {
        internal static readonly BtcDuelTab Instance = new BtcDuelTab();

        internal override int Id { get { return 8; } }
        internal override bool IsActive { get { return BtcMenu.CurrentId == 8; } }
        internal override string UndoScope { get { return Scope; } }

        internal const string Scope = "duel";

        private Vector2 _scroll;
        private float _h;
        private GUIStyle _ttl, _lab, _dim, _ctr;
        private int _fs;
        private readonly System.Collections.Generic.List<string> _keys = new System.Collections.Generic.List<string>();

        private static string Tt(string en, string ru) { return BtcReplayUI.T(en, ru); }

        private void Styles(int fs)
        {
            if (_ttl != null && _fs == fs) return;
            _fs = fs;
            GUIStyle b = BtcReplayUI.sBarLabel ?? BtcReplayUI.sLabel;
            _ttl = new GUIStyle(b); _ttl.fontSize = Mathf.RoundToInt(fs * 1.15f); _ttl.fontStyle = FontStyle.Bold; _ttl.alignment = TextAnchor.MiddleLeft; _ttl.normal.textColor = Color.white;
            _lab = new GUIStyle(b); _lab.fontSize = Mathf.RoundToInt(fs * 0.95f); _lab.alignment = TextAnchor.MiddleLeft; _lab.clipping = TextClipping.Clip; _lab.normal.textColor = Color.white;
            _dim = new GUIStyle(_lab); _dim.fontSize = Mathf.RoundToInt(fs * 0.8f); _dim.normal.textColor = new Color(1f, 1f, 1f, 0.5f);
            _ctr = new GUIStyle(_lab); _ctr.alignment = TextAnchor.MiddleCenter;
        }

        internal override void Draw(Rect area, int fs)
        {
            Styles(fs);
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == BtcMenu.MenuKeyMain)
            {
                GUIUtility.keyboardControl = 0;
                BtcMenu.PgDownFrame = Time.frameCount;
                ResourcePackManager.CloseMenu();
                e.Use();
                return;
            }
            float pad = fs, ib = fs * 1.9f;
            float y0 = area.y + fs * 0.4f;
            GUI.Label(new Rect(area.x + pad, y0, area.width * 0.5f, ib), Tt("Micro duel vs bot", "Микродуэль против бота"), _ttl);

            float y = y0 + ib + fs * 0.5f;
            float fx = area.x + pad;
            bool allOpen = true;
            for (int i = 0; i < _keys.Count; i++) if (!BtcUi.IsOpen(_keys[i])) { allOpen = false; break; }
            if (BtcReplayIcons.Plain(new Rect(fx, y, ib, ib), allOpen ? BtcReplayIcons.ChevD : BtcReplayIcons.ChevR, Color.white, allOpen ? Tt("Collapse all sections", "Свернуть все разделы") : Tt("Expand all sections", "Развернуть все разделы")))
                BtcUi.SetOpen(_keys, !allOpen);
            if (BtcReplayIcons.Plain(new Rect(fx + ib + 3f, y, ib, ib), BtcReplayIcons.Folder, Color.white, Tt("Open the duel logs folder", "Открыть папку логов дуэлей")))
                OpenLogs();
            y += ib + fs * 0.6f;
            BtcReplayUI.Fill(new Rect(area.x + pad, y - fs * 0.25f, area.width - pad * 2f, 1f), BtcMenu.Line);

            Rect view = new Rect(area.x, y, area.width, area.yMax - y);
            float vw = view.width - (_h > view.height ? 14f : 0f);
            _scroll = GUI.BeginScrollView(view, _scroll, new Rect(0f, 0f, vw, _h));
            float cy = fs * 0.2f;
            _keys.Clear();
            Body(ref cy, vw, fs, e);
            GUI.EndScrollView();
            _h = cy + fs * 3f;
            BtcReplayIcons.DrawTip(fs, area.xMax, false);
        }

        private string KeyAdd(string k) { _keys.Add(k); return k; }

        private static void OpenLogs()
        {
            try
            {
                string dir = Path.Combine(Path.Combine(Paths.GameRootPath, "BetterThanCastle"), "duel-logs");
                Directory.CreateDirectory(dir);
                System.Diagnostics.Process.Start("explorer.exe", "\"" + dir + "\"");
            }
            catch (Exception ex) { BtcLog.Warn("Duel", "open logs failed: " + ex.Message); }
        }

        private void Body(ref float y, float w, int fs, Event e)
        {
            bool active = BtcDuel.Active;
            // start card
            {
                float h = fs * 3.2f, pad = fs;
                Rect card = new Rect(pad, y, w - pad * 2f, h);
                bool hov = card.Contains(e.mousePosition) && !active;
                BtcReplayUI.Fill(card, active ? new Color(1f, 1f, 1f, 0.04f) : new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, hov ? 0.28f : 0.18f));
                BtcReplayUI.Fill(new Rect(card.x, card.y + h * 0.15f, 3f, h * 0.7f), active ? new Color(0.95f, 0.78f, 0.1f, 1f) : BtcMenu.Accent);
                float g = h * 0.5f;
                BtcReplayIcons.Glyph(active ? BtcReplayIcons.Swords : BtcReplayIcons.Play, new Rect(card.x + fs * 0.9f, card.center.y - g * 0.5f, g, g), active ? new Color(0.95f, 0.78f, 0.1f, 1f) : BtcMenu.Accent);
                GUI.Label(new Rect(card.x + fs * 3f, card.y, card.width - fs * 3.5f, h * 0.6f), active ? Tt("Duel is running", "Дуэль идёт") : Tt("Start duel", "Начать дуэль"), _ttl);
                GUI.Label(new Rect(card.x + fs * 3f, card.y + h * 0.5f, card.width - fs * 3.5f, h * 0.45f),
                    active ? Tt("Use Rematch / Exit on screen. Changes apply at the next rematch.", "Реванш / Выход на экране. Изменения сработают со следующего реванша.")
                           : BtcDuelConfig.Total(BtcDuelConfig.PlayerCount) + " vs " + BtcDuelConfig.Total(BtcDuelConfig.BotCount) + "  ·  " + BtcDuelConfig.Difficulty, _dim);
                if (!active && BtcSfx.Btn(card, "", BtcReplayUI.sBtn)) BtcDuel.Launch();
                y += h + fs * 0.5f;
            }
            BtcUi.Note(ref y, w, fs, Tt("Train pure micro on a small arena: safe zones, neutral, mid. Win by kill & escape or by holding the mid for 5 s. Settings apply to the next match (Rematch is instant).",
                "Тренировка микро на маленькой арене: сейфзоны, нейтраль, мид. Победа: kill & escape или удержание мида 5 с. Настройки применяются со следующего матча (реванш мгновенный)."));

            if (BtcUi.Fold(ref y, w, fs, KeyAdd("duel:match"), Tt("Match", "Матч"), 0))
            {
                BtcUi.ToggleUndo(ref y, w, fs, Scope, Tt("Allow animation skips", "Скипы анимации"), BtcDuelConfig.SkipAnims,
                    Tt("The move-attack trick right before a hit", "Мув-атак трюк прямо перед ударом"), delegate (bool v) { BtcDuelConfig.SkipAnims = v; BtcDuelConfig.Save(); });
                BtcUi.ToggleUndo(ref y, w, fs, Scope, Tt("Infinite heal", "Бесконечный хил"), BtcDuelConfig.InfiniteHeal,
                    Tt("10 HP/s for everyone", "10 HP/с у всех"), delegate (bool v) { BtcDuelConfig.InfiniteHeal = v; BtcDuelConfig.Save(); });
                BtcUi.ToggleUndo(ref y, w, fs, Scope, Tt("Large arena x3", "Большая арена x3"), BtcDuelConfig.ArenaScale == 3,
                    "300x60", delegate (bool v) { BtcDuelConfig.ArenaScale = v ? 3 : 1; BtcDuelConfig.Save(); });
                y += fs * 0.3f;
            }
            if (BtcUi.Fold(ref y, w, fs, KeyAdd("duel:bot"), Tt("Bot difficulty", "Сложность бота"), 0))
                Difficulty(ref y, w, fs, e);
            if (BtcUi.Fold(ref y, w, fs, KeyAdd("duel:units"), Tt("Units", "Юниты"), 0))
                Units(ref y, w, fs, e);
        }

        private void Difficulty(ref float y, float w, int fs, Event e)
        {
            int n = Enum.GetValues(typeof(BtcDuelDifficulty)).Length;
            float pad = fs, gap = 3f, ch = fs * 2f;
            float cw = (w - pad * 2f - gap * (n - 1)) / n;
            for (int i = 0; i < n; i++)
            {
                Rect r = new Rect(pad + i * (cw + gap), y, cw, ch);
                bool on = (int)BtcDuelConfig.Difficulty == i, hov = r.Contains(e.mousePosition);
                BtcReplayUI.Fill(r, on ? new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.28f) : new Color(1f, 1f, 1f, hov ? 0.12f : 0.05f));
                GUI.Label(r, i == 6 ? "B0" : ((BtcDuelDifficulty)i).ToString(), _ctr);
                if (BtcSfx.Btn(r, "", BtcReplayUI.sBtn) && !on)
                {
                    BtcDuelDifficulty old = BtcDuelConfig.Difficulty;
                    BtcDuelConfig.Difficulty = (BtcDuelDifficulty)i; BtcDuelConfig.Save();
                    BtcUndo.Push(Scope, Tt("Bot difficulty", "Сложность бота"), delegate { BtcDuelConfig.Difficulty = old; BtcDuelConfig.Save(); });
                }
            }
            y += ch + fs * 0.4f;
            string[] dsc = {
                Tt("Easy: attacks the nearest, no tricks.", "Easy: бьёт ближайшего, без приёмов."),
                Tt("Medium: focus fire and escapes, slow reactions, no skips.", "Medium: фокус и уход, медленные реакции, без скипов."),
                Tt("Hard: slow imprecise skips, rotates wounded units.", "Hard: медленные неточные скипы, ротация раненых."),
                Tt("Extreme: near-perfect skips and reactions.", "Extreme: почти идеальные скипы и реакции."),
                Tt("Chosen: everything the bot can do.", "Chosen: всё, что умеет бот."),
                Tt("Custom: same as Chosen for now.", "Custom: пока как Chosen."),
                Tt("BreaktronZero (B0): neural network bot trained in the lab simulator (plays like Chosen if no policy is available).", "BreaktronZero (B0): нейросетевой бот, обученный в симуляции (если нет весов, играет как Chosen).") };
            BtcUi.Note(ref y, w, fs, dsc[(int)BtcDuelConfig.Difficulty]);
        }

        private void Units(ref float y, float w, int fs, Event e)
        {
            float pad = fs, rh = fs * 2.1f;
            float c1 = w * 0.40f, cw = w * 0.27f;
            GUI.Label(new Rect(pad + fs * 0.6f, y, c1, rh), Tt("Unit", "Юнит"), _dim);
            GUI.Label(new Rect(c1, y, cw, rh), Tt("You", "Ты") + " (" + BtcDuelConfig.Total(BtcDuelConfig.PlayerCount) + ")", _dim);
            GUI.Label(new Rect(c1 + cw, y, cw, rh), Tt("Bot", "Бот") + " (" + BtcDuelConfig.Total(BtcDuelConfig.BotCount) + ")", _dim);
            y += rh;
            for (int k = 0; k < BtcDuelConfig.UnitKinds; k++)
            {
                Rect row = new Rect(pad, y, w - pad * 2f, rh);
                BtcReplayUI.Fill(row, new Color(1f, 1f, 1f, k % 2 == 0 ? 0.04f : 0.02f));
                GUI.Label(new Rect(row.x + fs * 0.6f, y, c1 - row.x, rh), UnitName(k), _lab);
                Counter(new Rect(c1, y + rh * 0.1f, cw - fs * 0.6f, rh * 0.8f), BtcDuelConfig.PlayerCount, k, e, Tt("Your ", "Твои ") + UnitName(k));
                Counter(new Rect(c1 + cw, y + rh * 0.1f, cw - fs * 0.6f, rh * 0.8f), BtcDuelConfig.BotCount, k, e, Tt("Bot ", "Бот ") + UnitName(k));
                y += rh + 2f;
            }
            BtcUi.Note(ref y, w, fs, Tt("Shift = +/-5. Barrel worker: a worker carrying an explosive barrel (separate unit).", "Shift = +/-5. Barrel worker: работник с бочкой в руках (отдельный юнит)."));
            y += fs * 0.2f;
            if (BtcUi.Action(ref y, w, fs, BtcReplayIcons.Copy, Tt("Bot gets my units", "Боту те же юниты, что мне"), Tt("Mirror match", "Зеркальный матч")))
                Replace(BtcDuelConfig.BotCount, BtcDuelConfig.PlayerCount, "Mirror");
            if (BtcUi.Action(ref y, w, fs, BtcReplayIcons.Undo, Tt("Reset to 5 halberdiers each", "Сбросить: по 5 алебардистов"), null))
            {
                int[] a = new int[BtcDuelConfig.UnitKinds]; a[(int)BtcDuelUnit.Halberdier] = 5;
                Replace(BtcDuelConfig.PlayerCount, a, "Reset"); Replace(BtcDuelConfig.BotCount, a, "Reset");
            }
            y += fs * 0.3f;
        }

        private static string UnitName(int k)
        {
            switch ((BtcDuelUnit)k)
            {
                case BtcDuelUnit.Worker: return Tt("Worker", "Работник");
                case BtcDuelUnit.BarrelWorker: return Tt("Barrel worker", "Работник с бочкой");
                case BtcDuelUnit.Knight: return Tt("Knight", "Рыцарь");
                case BtcDuelUnit.Archer: return Tt("Archer", "Лучник");
                case BtcDuelUnit.Halberdier: return Tt("Halberdier", "Алебардист");
                case BtcDuelUnit.Arbalist: return Tt("Arbalist", "Арбалетчик");
                case BtcDuelUnit.Alchemist: return Tt("Alchemist", "Алхимик");
                default: return Tt("Artificer", "Артефактор");
            }
        }

        private static void Replace(int[] dst, int[] src, string label)
        {
            int[] old = (int[])dst.Clone();
            Array.Copy(src, dst, dst.Length);
            BtcDuelConfig.Save();
            BtcUndo.Push(Scope, label, delegate { Array.Copy(old, dst, dst.Length); BtcDuelConfig.Save(); });
        }

        private void Counter(Rect r, int[] arr, int k, Event e, string label)
        {
            float bw = r.height;
            int step = e.shift ? 5 : 1;
            Rect m = new Rect(r.x, r.y, bw, r.height), p = new Rect(r.xMax - bw, r.y, bw, r.height);
            BtcReplayUI.Fill(m, new Color(1f, 1f, 1f, m.Contains(e.mousePosition) ? 0.18f : 0.09f));
            BtcReplayUI.Fill(p, new Color(1f, 1f, 1f, p.Contains(e.mousePosition) ? 0.18f : 0.09f));
            BtcReplayUI.Fill(new Rect(m.x + bw * 0.28f, m.center.y - 1f, bw * 0.44f, 2f), Color.white);
            BtcReplayUI.Fill(new Rect(p.x + bw * 0.28f, p.center.y - 1f, bw * 0.44f, 2f), Color.white);
            BtcReplayUI.Fill(new Rect(p.center.x - 1f, p.y + bw * 0.28f, 2f, bw * 0.44f), Color.white);
            GUI.Label(new Rect(m.xMax, r.y, r.width - bw * 2f, r.height), arr[k].ToString(), _ctr);
            int old = arr[k];
            if (BtcSfx.Btn(m, "", BtcReplayUI.sBtn)) arr[k] = Mathf.Max(0, old - step);
            if (BtcSfx.Btn(p, "", BtcReplayUI.sBtn)) arr[k] = Mathf.Min(60, old + step);
            if (arr[k] != old)
            {
                int[] a = arr; int kk = k; int was = old;
                BtcDuelConfig.Save();
                BtcUndo.Push(Scope, label, delegate { a[kk] = was; BtcDuelConfig.Save(); }, "duel:cnt:" + label);
            }
        }
    }
}
