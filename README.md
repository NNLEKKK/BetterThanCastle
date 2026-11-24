# Better Than Castle

**Better Than Castle (BTC)** is a quality-of-life, multiplayer and practice mod for **Castle Story** (Steam). It is one BepInEx 5 plugin, `BetterThanCastle.dll`, and it adds a full in-game menu (**PageDown**) with everything it does.

[Русская версия ниже](#русский) · [Latest release](https://github.com/NNLEKKK/BetterThanCastle/releases/latest) · [What's new in the game](CHANGELOG.md)

---

## Install

1. Download **`BetterThanCastle-Setup.exe`** from the [latest release](https://github.com/NNLEKKK/BetterThanCastle/releases/latest).
2. Run it and press **Install / update**. It finds Castle Story through Steam (or press *Browse* and pick the folder with `Castle Story.exe`), installs BepInEx 5.4.23.5 if it is missing, then the mod.
3. Start the game through Steam and press **PageDown** to open the mod menu.

Game files are never changed. The installer only adds the BepInEx loader and `BepInEx/plugins/BetterThanCastle/BetterThanCastle.dll`, and the same installer removes it again (optionally BepInEx too).

Manual install: put [BepInEx 5.4.23.5](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5) into the game folder, then `BetterThanCastle.dll` into `BepInEx/plugins/`.

**Updates:** at game start the mod checks GitHub Releases and offers to update itself. Every download is verified by SHA-256. Turn it off with `[Update] CheckOnStart` in `BepInEx/config/com.btc.betterthancastle.cfg`.

---

## What it adds

### The mod menu (PageDown)
One menu for everything, in one style: an icon rail on the left, pages inside each tab, a **global search** (Ctrl+F) that finds any setting in any tab, favourites stars, foldable sections, one master switch per feature, and **Ctrl+Z** undo for almost every change. The menu remembers the open tab, page, folds and filters, so it comes back exactly as you left it.

- **Packs**: resource packs with thumbnails and texture counts, switched with a clear loading screen.
- **Visuals**: world overlays (HP rings, pings, barriers, focus marks), a colour palette editor, profiles you can copy as text, night and performance switches.
- **Hotkeys**: every mod bind in one place with categories, search, key combos, a key finder, a keyboard map and profiles. Vanilla binds can be edited too.
- **Gameplay**: the rules of your match (vanilla as the base, every change marked), rule requests for the host, presets with compare, map scale and symmetry, handicaps, timer and recovery time.
- **Profile**: a live preview of your lobby row, per-letter nick colours, gradients, animation, avatar, plate editor, and saved profile sets.
- **Share, Replay, Statistics, Notifications, What's new, Report a bug**: send maps to other players, browse and watch replays, see live match stats, read every message of the session, read the changelog, and file a bug report that already includes system info and log errors.
- **UI editor** (F7): drag, resize and hide on-screen elements on a live preview, with snapping. Save the whole look as a **setup** and share it as a short code.
- **Performance**: every speed and memory setting in one tab, including faster loading (the world is built in a few frames, the in-game interface is built in C#, and the load timer counts to the playable moment).

### Advanced hotkeys and orders
- Camera bookmarks and slots, a camera in the ground for digging from below, "jump to the cursor", zoom and follow binds.
- Control groups with an **add** key, select all units of a profession, double-click to select one unit only.
- Dig, break, shoot at the cursor, waypoints, line-formation moves, orders from any camera distance.
- **Focus without auto-run** and a focus marker with a hit sound, so you see which enemy your units are on.
- **Ping allies** on the map and the mini map (with BetterConnection).
- **Per-player work settings**: your trap re-arming, chopping-block radius and log keeping are your own, even in multiplayer.

### Gameplay improvements
- **Instant orders**: a whole squad starts moving at once, busy units do not sit out a pause, and fights no longer leave archers standing still.
- **Smoother pathing**: allies no longer block each other, frozen units are unstuck, routes take the near stairs, units walk straight lines over flat ground, and an early detour goes around walls and holes before the unit reaches them.
- **Work that makes sense**: a quarry or tunnel picks up only the ores switched on in it, cleanup leaves stumps alone, and crafting stations do not block the cells in front of them.
- **Terraforming** (Ctrl+G): mark blocks to dig or fill with earth, place tunnel and quarry ladders and landings, save tasks, and let workers haul to a stockpile.
- **Base shields**: energy barriers around your home and captured crystals. Enemies bounce off, artificers must stay near a barrier for 3 seconds to break it, and you choose cubes or spheres.
- **Phantoms** (off or on by rule): free ghost workers at your home crystal that walk through walls, can fly, and respawn for energy.
- **Wormhole wards**: crystal travel points, crafted at an altar and built as wards, for fast movement across the map.
- **Fair fog of war**: nobody, the host included, can select or order at objects their team cannot see.
- **Recovery time**: when the game time runs out, the team with fewer crystals gets a recovery period.

### Multiplayer (BetterConnection)
- **Stable connection**: a dropped link no longer pauses the match for everyone. Only the host decides on a pause, a short freeze or an alt-tab never pauses the game, and a player who lost the link rejoins the same game by itself.
- **Lobby**: extra colours (purple, pink, cyan, rainbow) and teams, real team flags, a host-chosen starting map and mode, ready that stays ready, spawn selection, and a version check that warns when players run different mod versions.
- **Sync**: smooth unit animations and movement, unit sounds like the host's, no fog sounds, and health bars that stay correct.
- **Maps and replays for everyone**: the host sends the map to clients as a small `.btcs` file, and replays are shared automatically.
- **Turn it off and the mod is vanilla on the wire**: with BetterConnection **off** the mod sends and expects exactly the vanilla messages, so it plays with unmodded players.

### Replays
- Recording with camera and cursor at 30 Hz, a replay bar with a timeline and an **activity graph** (APM and EPM of every player, idle, surge and peak marks).
- **Instant seek** with keyframes: jump to any moment without replaying the match.
- Pressed-keys view, a pinned mini map, a 3D view cone of the watched player, team pings in the replay, and automatic mark-ups of key moments.
- A library with map thumbnails, search, folders, favourites, and automatic cleanup of old caches. Screenshot button on the replay bar.

### Practice
- **Micro duel** against a bot on a small arena, with difficulty tiers, rematches without reloading and a match log.
- **Aim training** on an empty map: focus clicks against moving targets, with a Non-stop pack movement and a rating that rewards speed.
- Practice modes skip the game interface and the garbage-heavy systems, so they run smoothly.

### Map editor
- A **Carve** tool that breaks or adds voxels in sizes from 1x1 to 4x4, so caves and multi-level passages are easy.
- Live symmetry: objects, trees and terrain you place are mirrored at once.

---

## Build from source

Requires Windows, the .NET SDK and a Castle Story copy with BepInEx in `game/csmod` (not in the repository).
`scripts/build.ps1 -Release` builds `dist/BetterThanCastle.dll`, `scripts/build-setup.ps1` builds the installer, and `scripts/release.ps1` makes a release. See `docs/RUNBOOK.md`.

## License

The mod is released under the [GNU GPL v3](LICENSE). Castle Story belongs to Sauropod Studio; this project is not affiliated with it.
The installer bundles the unmodified official [BepInEx](https://github.com/BepInEx/BepInEx) 5.4.23.5 (LGPL-2.1).

---

# Русский

**Better Than Castle (BTC)** — мод для **Castle Story** (Steam), который делает игру удобнее, добавляет мультиплеер с устойчивым соединением и тренировочные режимы. Это один плагин BepInEx 5: `BetterThanCastle.dll`. Всё управляется из меню мода, которое открывается клавишей **PageDown**.

[Latest release](https://github.com/NNLEKKK/BetterThanCastle/releases/latest) · [Что нового в игре](CHANGELOG.md)

---

## Установка

1. Скачайте **`BetterThanCastle-Setup.exe`** из [последнего релиза](https://github.com/NNLEKKK/BetterThanCastle/releases/latest).
2. Запустите и нажмите **«Установить / обновить»**. Установщик сам найдёт Castle Story в Steam (или нажмите «Обзор» и выберите папку с `Castle Story.exe`), поставит BepInEx 5.4.23.5, если его нет, и мод.
3. Запускайте игру через Steam и нажмите **PageDown**, чтобы открыть меню мода.

Файлы игры не изменяются. Установщик добавляет только загрузчик BepInEx и `BepInEx/plugins/BetterThanCastle/BetterThanCastle.dll`, и тем же установщиком всё удаляется (по желанию вместе с BepInEx).

Ручная установка: положите [BepInEx 5.4.23.5](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5) в папку игры, затем `BetterThanCastle.dll` в `BepInEx/plugins/`.

**Обновления:** при запуске игры мод проверяет релизы на GitHub и предлагает обновиться. Каждый файл проверяется по SHA-256. Отключить можно параметром `[Update] CheckOnStart` в `BepInEx/config/com.btc.betterthancastle.cfg`.

---

## Что добавляет мод

### Меню мода (PageDown)
Всё в одном меню и в одном стиле: иконки слева, страницы внутри каждой вкладки, **глобальный поиск** (Ctrl+F), который находит любую настройку во всех вкладках, звёздочки избранного, сворачиваемые разделы, один главный переключатель на функцию и **Ctrl+Z** для отмены почти любого изменения. Меню запоминает открытую вкладку, страницу, свёрнутые разделы и фильтры, поэтому при следующем открытии всё выглядит так же.

- **Паки**: ресурс-паки с миниатюрами и количеством текстур, переключение с понятным экраном загрузки.
- **Визуалы**: оверлеи мира (кольца HP, пинги, барьеры, метки фокуса), редактор цветовой палитры, профили, которые можно скопировать текстом, переключатели ночи и производительности.
- **Хоткеи**: все бинды мода в одном месте: категории, поиск, комбинации клавиш, поиск клавиши, карта клавиатуры, профили. Ванильные бинды тоже можно менять.
- **Геймплей**: правила матча (за основу взят ваниль, каждое изменение помечено), запросы правил хосту, пресеты со сравнением, масштаб и симметрия карты, гандикапы, таймер и время восстановления.
- **Профиль**: живой предпросмотр вашей строки в лобби, покраска ника по буквам, градиенты, анимация, аватар, редактор плашки и наборы профилей.
- **Share, Replay, Statistics, Notifications, What's new, Report a bug**: отправка карт другим игрокам, библиотека реплеев, живая статистика матча, список всех сообщений сессии, журнал изменений и форма отчёта об ошибке, в которую уже подставлены данные системы и ошибки из логов.
- **Редактор интерфейса** (F7): перетаскивайте, меняйте размер и скрывайте элементы экрана на живом предпросмотре с привязкой. Весь вид можно сохранить как **набор** и передать коротким кодом.
- **Производительность**: все настройки скорости и памяти в одной вкладке. Загрузка стала быстрее: мир строится за несколько кадров, интерфейс игры собирается на C#, а таймер загрузки считает до момента, когда можно играть.

### Расширенные хоткеи и приказы
- Закладки и слоты камеры, камера под землёй для копания снизу, «перейти к курсору», бинды зума и слежения.
- Группы юнитов с клавишей **добавления**, выбор всех юнитов профессии, двойной клик выбирает только одного юнита.
- Копать, ломать, стрелять по курсору, вейпоинты, построение в линию, приказы с любого расстояния камеры.
- **Фокус без авто-запуска** и метка фокуса со звуком попадания, чтобы видеть, на какого врага смотрят ваши юниты.
- **Пинги союзникам** на карте и миникарте (нужен BetterConnection).
- **Личные настройки работы**: авто-перевзвод ваших ловушек, радиус пенька и сохранение брёвен — у каждого игрока свои, даже в мультиплеере.

### Улучшения геймплея
- **Мгновенные приказы**: весь отряд трогается сразу, занятые юниты не ждут паузу, а лучники не стоят на месте во время боя.
- **Плавнее путь**: союзники не мешают друг другу, зависшие юниты разблокируются, маршруты берут ближайшие лестницы, юниты идут прямыми линиями по ровной земле, а ранний обход обходит стены и ямы ещё до подхода.
- **Понятная работа**: карьер и туннель подбирают только включённые в них руды, очистка не трогает пеньки, а крафтильни не блокируют клетки перед собой.
- **Терраформинг** (Ctrl+G): отметьте блоки для копки или засыпки землёй, ставьте лестницы для туннелей и карьеров и площадки, сохраняйте задания и отправляйте рабочих относить камень на склад.
- **Базовые щиты**: энергетические барьеры вокруг домашнего и захваченных кристаллов. Враги отскакивают, артификатор должен простоять рядом с барьером 3 секунды, чтобы его сломать; вы выбираете кубы или сферы.
- **Фантомы** (по правилу): бесплатные призрачные рабочие у домашнего кристалла, которые ходят сквозь стены, умеют летать и возрождаются за энергию.
- **Червоточины-вехи**: точки быстрого перехода из кристаллов, создаются на алтаре и ставятся как вехи.
- **Честный туман войны**: никто, в том числе хост, не может выбирать и отдавать приказы объектам, которых не видит его команда.
- **Время восстановления**: когда игровое время вышло, при равном числе кристаллов — ничья, иначе время восстановления получает команда с меньшим числом кристаллов.

### Мультиплеер (BetterConnection)
- **Устойчивое соединение**: обрыв связи больше не ставит матч на паузу у всех. Паузу решает только хост, короткий фриз или переключение окна не останавливают игру, а игрок, потерявший связь, сам возвращается в ту же партию.
- **Лобби**: дополнительные цвета (фиолетовый, розовый, бирюзовый, радужный) и команды, настоящие флаги команд, стартовая карта и режим, которые выбирает хост, готовность, которая не сбрасывается сама, выбор спавна и проверка версии мода с подсказкой, если у игроков разные версии.
- **Синхронизация**: плавная анимация и движение юнитов, звуки юнитов такие же, как у хоста, без звуков тумана, корректные полоски здоровья.
- **Карты и реплеи для всех**: хост отправляет карту клиентам маленьким файлом `.btcs`, реплеи отправляются автоматически.
- **Выключите — и мод ведёт себя как ваниль по сети**: при выключенном BetterConnection мод отправляет и ожидает ровно ванильные сообщения, поэтому играет с игроками без мода.

### Реплеи
- Запись камеры и курсора с частотой 30 Гц, нижняя панель с таймлайном и **графиком активности** (APM и EPM каждого игрока, метки простоя, всплесков и пиков).
- **Мгновенная перемотка** по ключевым кадрам: переход к любому моменту без повторного просмотра матча.
- Вид нажатых клавиш, закреплённая миникарта, конус обзора 3D игрока, за которым вы следите, пинги команды и автоматические метки ключевых моментов.
- Библиотека с миниатюрами карт, поиском, папками, избранным и автоочисткой старых кэшей. На панели реплея есть кнопка скриншота.

### Тренировка
- **Микродуэль** против бота на маленькой арене: уровни сложности, реванш без перезагрузки и журнал матчей.
- **Тренировка прицеливания** на пустой карте: фокус-клики по движущимся целям, режим движения «Непрерывная стая» и рейтинг, который ценит скорость.
- Тренировочные режимы не показывают интерфейс игры и не используют тяжёлые системы, поэтому работают плавно.

### Редактор карт
- Инструмент **Вырезать** ломает или добавляет воксели размером от 1x1 до 4x4, поэтому пещеры и многоуровневые проходы делаются легко.
- Живая симметрия: объекты, деревья и рельеф, которые вы ставите, зеркалятся сразу.

---

## Сборка из исходников

Нужны Windows, .NET SDK и копия Castle Story с BepInEx в `game/csmod` (в репозитории её нет).
`scripts/build.ps1 -Release` собирает `dist/BetterThanCastle.dll`, `scripts/build-setup.ps1` собирает установщик, `scripts/release.ps1` делает релиз. См. `docs/RUNBOOK.md`.

## Лицензия

Мод распространяется по [лицензии GNU GPL v3](LICENSE). Castle Story принадлежит Sauropod Studio; проект с ней не связан.
В установщик входит неизменённый официальный [BepInEx](https://github.com/BepInEx/BepInEx) 5.4.23.5 (LGPL-2.1).
