# Better Than Castle

A quality-of-life, multiplayer and competitive mod for **Castle Story** (Steam), shipped as one BepInEx 5 plugin: `BetterThanCastle.dll`.

[Русская версия ниже](#русский)

## Install

1. Download **`BetterThanCastle-Setup.exe`** from the [latest release](https://github.com/NNLEKKK/BetterThanCastle/releases/latest).
2. Run it and press **Install / update**. It finds Castle Story through Steam (or press *Browse* and pick the folder with `Castle Story.exe`), installs BepInEx 5.4.23.5 if it is missing, then the mod.
3. Start the game through Steam. The mod menu opens with **PageDown**.

Game files are never changed: the installer only adds the BepInEx loader (`winhttp.dll`, `doorstop_config.ini`, `BepInEx/core`) and `BepInEx/plugins/BetterThanCastle/BetterThanCastle.dll`. The same installer removes the mod (optionally BepInEx too).

Manual install: put [BepInEx 5.4.23.5 win x64](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5) into the game folder, then `BetterThanCastle.dll` into `BepInEx/plugins/`.

**Updates:** at game start the mod checks GitHub Releases and offers to update itself (the download is verified by SHA-256). It can be turned off in `BepInEx/config/com.btc.betterthancastle.cfg` (`[Update] CheckOnStart`).

## Features (short)

- **Mod menu** (PageDown): resource packs, overlays, HP rings, gameplay settings, profile, replays.
- **Advanced hotkeys**: camera bookmarks and slots, control groups, select all units of a profession, dig / break / shoot at the cursor, waypoints, line formation moves, orders from any camera distance, all rebindable.
- **Multiplayer (BetterConnection)**: stable connection with pause and reconnect, ping display, lobby colours and teams, spawn selection, map scale and symmetry tools, handicaps, match statistics. With BetterConnection **off** the mod behaves like vanilla on the wire, so it can play with unmodded players.
- **Replays**: recording, playback with seek, automatic sharing to the other players.
- **Micro duel** against a bot (Duel tab).
- Faster level loading and many performance fixes.

## Build from source

Requires Windows, the .NET SDK and a Castle Story copy with BepInEx in `game/csmod` (not in the repository).
`scripts/build.ps1 -Release` builds `dist/BetterThanCastle.dll`, `scripts/build-setup.ps1` builds the installer, `scripts/release.ps1` makes a release. See `docs/RUNBOOK.md`.

## License

The mod is released under the [GNU GPL v3](LICENSE). Castle Story belongs to Sauropod Studio; this project is not affiliated with it.
The installer bundles the unmodified official [BepInEx](https://github.com/BepInEx/BepInEx) 5.4.23.5 (LGPL-2.1).

---

## Русский

Мод для **Castle Story** (Steam): удобство, мультиплеер и соревновательная игра. Один плагин BepInEx 5: `BetterThanCastle.dll`.

### Установка

1. Скачайте **`BetterThanCastle-Setup.exe`** из [последнего релиза](https://github.com/NNLEKKK/BetterThanCastle/releases/latest).
2. Запустите и нажмите **«Установить / обновить»**. Установщик сам найдёт Castle Story в Steam (или нажмите «Обзор» и выберите папку с `Castle Story.exe`), поставит BepInEx 5.4.23.5, если его нет, и мод.
3. Запускайте игру через Steam. Меню мода открывается клавишей **PageDown**.

Файлы игры не изменяются: добавляется только загрузчик BepInEx и `BepInEx/plugins/BetterThanCastle/BetterThanCastle.dll`. Тем же установщиком мод удаляется (по желанию вместе с BepInEx).

**Обновления:** при запуске игры мод проверяет релизы на GitHub и предлагает обновиться (файл проверяется по SHA-256). Отключается в `BepInEx/config/com.btc.betterthancastle.cfg` (`[Update] CheckOnStart`).

### Возможности (кратко)

- **Меню мода** (PageDown): ресурс-паки, оверлеи, кольца HP, настройки геймплея, профиль, реплеи.
- **Расширенные хоткеи**: закладки и слоты камеры, группы юнитов, выбор всех юнитов профессии, копать / ломать / стрелять по курсору, вейпоинты, построение в линию, приказы с любого расстояния камеры; всё переназначается.
- **Мультиплеер (BetterConnection)**: стабильное соединение с паузой и переподключением, пинг, цвета и команды в лобби, выбор спавна, масштаб и симметрия карты, гандикапы, статистика матча. При **выключенном** BetterConnection мод по сети ведёт себя как ваниль и совместим с игроками без мода.
- **Реплеи**: запись, просмотр с перемоткой, автоматическая отправка другим игрокам.
- **Микродуэль** против бота (вкладка Duel).
- Ускоренная загрузка уровней и много исправлений производительности.

### Лицензия

Мод распространяется по [лицензии GNU GPL v3](LICENSE). Castle Story принадлежит Sauropod Studio; проект с ней не связан. В установщик входит неизменённый официальный [BepInEx](https://github.com/BepInEx/BepInEx) 5.4.23.5 (LGPL-2.1).
