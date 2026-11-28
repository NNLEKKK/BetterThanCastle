using System;
using System.Collections.Generic;
using System.IO;

namespace BetterThanCastle
{
public static class BtcLang
{
	public static bool Russian
	{
		get
		{
			BtcLang.Load();
			return BtcLang.ru;
		}
	}

	/// <summary>Switch "translate the mod's in-match elements" (rack filters, chopping blocks, terraform task); only matters while the language is Russian.</summary>
	public static bool GameRussian { get { return BtcLang.Russian && BtcMenuState.Bool("lang:game", true); } }

	/// <summary>Tr for elements drawn inside a match: English when the in-match translation switch is off.</summary>
	public static string TrG(string en) { return BtcLang.GameRussian ? BtcLang.Tr(en) : en; }

	public static void Toggle()
	{
		BtcLang.Load();
		BtcLang.ru = !BtcLang.ru;
		try
		{
			BtcPaths.Init();
			File.WriteAllText(BtcPaths.Config("language.txt"), "Language=" + (BtcLang.ru ? "ru" : "en") + "\r\n");
		}
		catch (Exception)
		{
		}
	}

	public static void SetRussian(bool ru)
	{
		BtcLang.Load();
		if (BtcLang.ru != ru)
		{
			BtcLang.Toggle();
		}
	}

	/// <summary>The Russian text of an English key whatever language is selected (search index); the key itself when there is no translation.</summary>
	public static string RuOf(string en)
	{
		BtcLang.Load();
		string t;
		if (!string.IsNullOrEmpty(en) && BtcLang.RuDict.TryGetValue(en.Trim(), out t)) return t;
		return en;
	}

	public static string Tr(string en)
	{
		BtcLang.Load();
		bool flag = !BtcLang.ru || string.IsNullOrEmpty(en);
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		string result;
		if (flag7)
		{
			result = en;
		}
		else
		{
			string text;
			bool flag8 = BtcLang.RuDict.TryGetValue(en, out text);
			bool flag9 = flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			if (flag14)
			{
				result = text;
			}
			else
			{
				string text2 = en.Trim();
				bool flag15 = text2.Length != en.Length && BtcLang.RuDict.TryGetValue(text2, out text);
				bool flag16 = flag15;
				bool flag17 = flag16;
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				if (flag21)
				{
					int length = en.Length - en.TrimStart(new char[0]).Length;
					int num = en.Length - en.TrimEnd(new char[0]).Length;
					result = en.Substring(0, length) + text.Trim() + en.Substring(en.Length - num);
				}
				else
				{
					result = en;
				}
			}
		}
		return result;
	}

	private static void Load()
	{
		bool flag = BtcLang.loaded;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		if (flag7)
		{
			BtcLang.loaded = true;
			BtcPaths.Init();
			BtcLang.ru = string.Equals(BtcPaths.ReadKey("language.txt", "Language", "en"), "ru", StringComparison.OrdinalIgnoreCase);
		}
	}

	private static bool loaded;

	private static bool ru;

	private static readonly Dictionary<string, string> RuDict = new Dictionary<string, string>
	{
		{
			"Hold and sweep over blocks: like line-move, but units go only on diagonals (like a chess bishop)",
			"Зажать и водить по блокам: как линия, но юниты встают только по диагонали (как слон), соседние блоки пропускаются"
		},
		{
			"Hold and sweep over blocks: each selected unit goes to its own block (line formation)",
			"Зажать и провести по блокам: каждый выделенный юнит идёт на свой блок (построение в линию)"
		},
		{
			"Line-move: mode (hold / toggle)",
			"Линия: режим (удержание / переключатель)"
		},
		{
			"Line-move: pick the nearest free unit for each block (0 = selection order, 1 = nearest)",
			"Линия: брать ближайшего свободного юнита на каждый блок (0 = по порядку выделения, 1 = ближайший)"
		},
		{
			"Profile",
			"Профиль"
		},
		{
			"Revert",
			"Вернуть"
		},
		{
			"Auto-apply on color changes",
			"Применять сразу при смене цвета"
		},
		{
			"The nick can be changed only in the main menu or in the lobby.",
			"Ник можно менять только в главном меню или в лобби."
		},
		{
			"Rainbow is on: in the lobby it overrides these colors. Turn it off in the Rainbow tab.",
			"Радуга включена: в лобби она перекрывает эти цвета. Выключи её во вкладке «Радуга»."
		},
		{
			"Long nick (BetterConnection)",
			"Длинный ник (BetterConnection)"
		},
		{
			"Short nick (vanilla lobby)",
			"Короткий ник (ванильное лобби)"
		},
		{
			"In use now: ",
			"Сейчас используется: "
		},
		{
			"long",
			"длинный"
		},
		{
			"short",
			"короткий"
		},
		{
			" (chosen by BetterConnection). Short limit is 30 characters including color tags.",
			" (выбирается по BetterConnection). Лимит короткого: 30 символов вместе с тегами цвета."
		},
		{
			"Nick saved. It will be used when the other mode is active.",
			"Ник сохранён. Он будет использоваться, когда включится другой режим."
		},
		{
			"Rainbow works only with the long nick (BetterConnection on).",
			"Радуга работает только с длинным ником (при включённом BetterConnection)."
		},
		{
			" (saved)",
			" (сохранён)"
		},
		{
			" (this run only)",
			" (только этот запуск)"
		},
		{
			"Add stop",
			"Добавить цвет"
		},
		{
			"Animate the nick in the lobby",
			"Анимировать ник в лобби"
		},
		{
			"Apply",
			"Применить"
		},
		{
			"Aurora",
			"Аврора"
		},
		{
			"Autumn",
			"Осень"
		},
		{
			"Avatar",
			"Аватар"
		},
		{
			"Berry",
			"Ягода"
		},
		{
			"Cancel",
			"Отмена"
		},
		{
			"Candy",
			"Конфета"
		},
		{
			"Clear all",
			"Сбросить всё"
		},
		{
			"Clear range",
			"Сбросить диапазон"
		},
		{
			"Colors",
			"Цвета"
		},
		{
			"Cotton",
			"Сахарная вата"
		},
		{
			"Custom",
			"Свой"
		},
		{
			"Cyber",
			"Кибер"
		},
		{
			"Does not fit the length limit.",
			"Не помещается в лимит длины."
		},
		{
			"Effective rate: ",
			"Реальная частота: "
		},
		{
			"Error: ",
			"Ошибка: "
		},
		{
			"File not found: ",
			"Файл не найден: "
		},
		{
			"Fire",
			"Огонь"
		},
		{
			"Folder: BetterThanCastle/Avatars (png, jpg, gif)",
			"Папка: BetterThanCastle/Avatars (png, jpg, gif)"
		},
		{
			"Forest",
			"Лес"
		},
		{
			"From char",
			"С символа"
		},
		{
			"Galaxy",
			"Галактика"
		},
		{
			"Gold",
			"Золото"
		},
		{
			"Gradient was coarsened to fit the length limit.",
			"Градиент упрощён, чтобы уложиться в лимит длины."
		},
		{
			"Gradient: ",
			"Градиент: "
		},
		{
			"Gradient: all",
			"Градиент: весь ник"
		},
		{
			"Gradient: range",
			"Градиент: диапазон"
		},
		{
			"Gradients",
			"Градиенты"
		},
		{
			"Gradients tab",
			"вкладка Градиенты"
		},
		{
			"Ice",
			"Лёд"
		},
		{
			"Keep this nick for good",
			"Оставить ник насовсем"
		},
		{
			"Lava",
			"Лава"
		},
		{
			"Length: ",
			"Длина: "
		},
		{
			"Loading...",
			"Загрузка..."
		},
		{
			"Mint",
			"Мята"
		},
		{
			"Neon",
			"Неон"
		},
		{
			"Nick",
			"Ник"
		},
		{
			"Nick changed: ",
			"Ник изменён: "
		},
		{
			"Nick: ",
			"Ник: "
		},
		{
			"No custom avatar (Steam)",
			"Без своей аватарки (Steam)"
		},
		{
			"No custom avatar.",
			"Своей аватарки нет."
		},
		{
			"Ocean",
			"Океан"
		},
		{
			"Open folder",
			"Открыть папку"
		},
		{
			"Paint all",
			"Покрасить весь ник"
		},
		{
			"Paint range",
			"Покрасить диапазон"
		},
		{
			"Pastel",
			"Пастель"
		},
		{
			"Peach",
			"Персик"
		},
		{
			"Rainbow",
			"Радуга"
		},
		{
			"Random",
			"Случайно"
		},
		{
			"Refresh",
			"Обновить"
		},
		{
			"Repeat",
			"Повторы"
		},
		{
			"Reverse",
			"Обратный"
		},
		{
			"Royal",
			"Королевский"
		},
		{
			"Sakura",
			"Сакура"
		},
		{
			"Silver",
			"Серебро"
		},
		{
			"Speed",
			"Скорость"
		},
		{
			"Strip colors",
			"Убрать цвета"
		},
		{
			"Sunset",
			"Закат"
		},
		{
			"The nick cannot be empty.",
			"Ник не может быть пустым."
		},
		{
			"To char",
			"По символ"
		},
		{
			"Toxic",
			"Токсик"
		},
		{
			"Updates/sec",
			"Обновлений/сек"
		},
		{
			"Vampire",
			"Вампир"
		},
		{
			"Visible: ",
			"Видимых: "
		},
		{
			"Works only in the lobby, never in the game. The nick is frozen on the last frame when the match starts. Long nicks are updated less often to spare the network.",
			"Работает только в лобби, не в игре. Когда матч начинается, ник замирает на последнем кадре. Длинные ники обновляются реже, чтобы беречь сеть."
		},
		{
			"frames",
			"кадров"
		},
		{
			"Drop the carried object (barrel etc.) at the cursor (selected units)",
			"Бросить переносимый предмет (бочку и т.п.) на пол под курсором (выделенные юниты)"
		},
		{
			"Break object under the cursor (selected units)",
			"Сломать объект под курсором (выделенные юниты)"
		},
		{
			"Artificer focus mark: mark the enemy under the cursor for Sentinel Wards (selected Artificer)",
			"Метка мага: пометить врага под курсором для Sentinel Ward (выделенный маг)"
		},
		{
			"Ping allies: mark the point under the cursor (or on the mini map) for your team (needs BetterConnection)",
			"Пинг союзникам: метка в точке под курсором (или на миникарте) для своей команды (нужен BetterConnection)"
		},
		{
			"Chopping block radius: hold and turn the mouse wheel over a selected chopping block (how far its workers go for logs, Shift = 5 blocks per notch)",
			"Радиус пенька для колки: зажать и крутить колесо при выделенном пеньке (как далеко его рабочие ходят за брёвнами, Shift = 5 блоков за щелчок)"
		},
		{
			"Chopping block: default log pickup radius of your blocks (blocks, 0 = no limit; needs BetterConnection)",
			"Пенёк для колки: радиус сбора брёвен ваших пеньков по умолчанию (в блоках, 0 = без лимита; нужен BetterConnection)"
		},
		{
			"Traps: your workers re-arm fired traps by themselves at the match start (switch it for all your traps in a trap's pie menu; needs BetterConnection)",
			"Трапы: ваши рабочие сами перезаряжают сработавшие трапы в начале матча (переключается для всех ваших трапов в пай-меню трапа; нужен BetterConnection)"
		},
		{
			"Tree cutting: your workers only fell trees, logs and stumps stay on the ground (needs BetterConnection)",
			"Рубка леса: ваши рабочие только валят деревья, брёвна и пни остаются на земле (нужен BetterConnection)"
		},
		{
			"Blueprints: anchor point. Before saving, hover a selected mark and press this, it becomes the block under the cursor when the blueprint is placed; while placing, press it over another block of the plan to move the anchor there",
			"Чертежи: опорная точка. Перед сохранением наведите курсор на выделенную метку и нажмите это, она станет блоком под курсором при постановке чертежа; при постановке нажмите над другим блоком плана, чтобы перенести опору туда"
		},
		{
			"Blueprints: hold to select your marks of every build task at once (click or box), for example to save a blueprint with several tasks",
			"Чертежи: удерживайте, чтобы выделять свои метки сразу из всех заданий строительства (кликом или рамкой), например чтобы сохранить чертёж с несколькими заданиями"
		},
		{
			"Advanced blueprints: pie menu on your blue marks of stockpiles, tool racks and crafting stations sets their filter or crafting queue in advance; the building gets it when it is built, saved blueprints keep it (needs BetterConnection)",
			"Продвинутые чертежи: пай-меню на ваших синих метках складов, стоек и мастерских заранее задаёт фильтр или очередь крафта; здание получает их при постройке, сохранённые чертежи их помнят (нужен BetterConnection)"
		},
		{ "Log pickup radius", "Радиус сбора брёвен" },
		{ "no limit", "без лимита" },
		{ "default", "по умолчанию" },
		{
			"Shoot at the block or object under the cursor (selected archers, not Artificers)",
			"Стрелять по блоку или объекту под курсором (выделенные лучники, кроме артефакторов)"
		},
		{
			"Focus without auto-run: ranged units hold position and shoot the focused enemy (needs BetterConnection)",
			"Фокус без автобега: дальнобойные юниты стоят на месте и стреляют по цели фокуса (нужен BetterConnection)"
		},
		{
			"Focus without auto-run: key (a keyboard key, or Right mouse button; with a modifier it stays separate from the vanilla focus bind)",
			"Фокус без автобега: кнопка (клавиша или правая кнопка мыши; с модификатором не конфликтует с ванильным фокусом)"
		},
		{
			"Go detonate the barrel under the cursor (selected units)",
			"Идти взрывать бочку под курсором (выделенные юниты)"
		},
		{
			"Camera: fast modes",
			"Камера: режимы ускорения"
		},
		{
			"Fast mode 1",
			"Режим ускорения 1"
		},
		{
			"Fast mode 2",
			"Режим ускорения 2"
		},
		{
			"Fast mode 3",
			"Режим ускорения 3"
		},
		{
			"Colors: yellow = changed from default, red = conflict (same keys as another function), orange = waiting for a key. Binds apply at once, but hotkeys are muted while this menu is open - close it to test.",
			"Цвета: жёлтый = изменено относительно умолчания, красный = конфликт (те же клавиши у другой функции), оранжевый = ждёт нажатия. Бинды применяются сразу, но пока меню открыто, хоткеи отключены - закройте его, чтобы проверить."
		},
		{
			"Advanced Hotkeys",
			"Расширенные хоткеи"
		},
		{
			"Advanced Hotkeys (master switch)",
			"Расширенные хоткеи (главный выключатель)"
		},
		{
			"Off: every function of this tab is disabled. Hotkeys never fire while this menu is open or the chat is focused.",
			"Выкл: все функции этой вкладки отключены. Хоткеи не срабатывают, пока открыто это меню или активен чат."
		},
		{
			"Click a key field, then press the new key. Hold Ctrl / Shift / Alt first for a combo (the last key is the moment of the press). Press only a modifier to bind the modifier itself. Esc cancels.",
			"Нажмите на поле клавиши, затем нужную клавишу. Для комбо сначала зажмите Ctrl / Shift / Alt (последняя клавиша = момент нажатия). Только модификатор = бинд на сам модификатор. Esc отменяет."
		},
		{
			"Unbind all functions",
			"Снять все бинды"
		},
		{
			"Reset everything to defaults",
			"Сбросить всё по умолчанию"
		},
		{
			"Click again to confirm",
			"Нажмите ещё раз для подтверждения"
		},
		{
			"Reset",
			"Сброс"
		},
		{
			"Clear",
			"Очистить"
		},
		{
			"conflict",
			"конфликт"
		},
		{
			"(not set)",
			"(не задано)"
		},
		{
			"Press keys... (Esc = cancel)",
			"Нажмите клавиши... (Esc = отмена)"
		},
		{
			"Give orders from any camera distance (vanilla ignores clicks when the camera is zoomed out too far)",
			"Приказы с любого расстояния камеры (в ванили клик не проходит, если камера слишком далеко)"
		},
		{ "Terraform", "Терраформ" },
		{ "Show Editor and movie panels", "Показывать панели Editor и movie" },
		{ "Open / close the terraforming tool (dig, fill, any shape; needs BetterConnection)", "Открыть / закрыть инструмент терраформинга (копать, насыпать, любая форма; нужен BetterConnection)" },
		{ "Terraforming needs BetterConnection (BC tab)", "Для терраформинга нужен BetterConnection (вкладка BC)" },
		{ "The shape is too big", "Форма слишком большая" },
		{ "Dig", "Копать" },
		{ "Fill", "Насыпать" },
		{ "Erase", "Стереть" },
		{ "Block", "Блок" },
		{ "Line", "Линия" },
		{ "Box", "Коробка" },
		{ "Width", "Ширина" },
		{ "Height", "Высота" },
		{ "Tool", "Инструмент" },
		{ "No terraform task of yours here", "Здесь нет вашего задания терраформинга" },
		{ "Editing the task: change it, then Save changes", "Редактирование задания: измените его и нажмите «Сохранить изменения»" },
		{ "Editing a task", "Редактируется задание" },
		{ "Click a cell of one of your terraform tasks (RMB cancels)", "Кликните по клетке своего задания терраформинга (ПКМ - отмена)" },
		{ "Save changes", "Сохранить изменения" },
		{ "Stop editing", "Отменить редактирование" },
		{ "The task stays as it was", "Задание останется как было" },
		{ "Edit a task", "Изменить задание" },
		{ "Pick one of your terraform tasks on the map", "Выберите своё задание терраформинга на карте" },
		{ "Task changed", "Задание изменено" },
		{ "Ladders on steps", "Лестницы на ступеньках" },
		{ "Every 1-high step you dig gets a quarry ladder (on Create)", "На каждую выкопанную ступеньку в 1 блок ставится лестница карьера (при создании)" },
		{ "Dug resources", "Выкопанное" },
		{ "Keep stone", "Собирать камень" },
		{ "Keep brimstone", "Собирать серу" },
		{ "Keep iron", "Собирать железо" },
		{ "No stockpiles", "Без складов" },
		{ "Workers never walk to storage: a full bag is dropped by the task, the fill uses it", "Рабочие не ходят на склады: полный мешок высыпается у задания, насыпка берёт оттуда" },
		{ "Task", "Задание" },
		{ "Ctrl+Z brings it back", "Ctrl+Z вернёт" },
		{ "Close (Esc)", "Закрыть (Esc)" },
		{ "Stone", "Камень" },
		{ "Brimstone", "Сера" },
		{ "Iron", "Железо" },
		{ "Create task", "Создать задание" },
		{ "Shape cleared (Ctrl+Z brings it back)", "Форма очищена (Ctrl+Z вернёт её)" },
		{ "Terraforming task created", "Задание терраформинга создано" },
		{ "LMB paint, RMB cancel, Ctrl+Z undo, Shift+wheel height, Ctrl+wheel width, Esc close", "ЛКМ рисовать, ПКМ отмена, Ctrl+Z назад, Shift+колесо высота, Ctrl+колесо ширина, Esc закрыть" },
		{ "Ladder", "Лестница" },
		{ "Tunnel", "Туннель" },
		{ "Quarry", "Карьер" },
		{ "Landing", "Площадка" },
		{ "Rotate (R)", "Повернуть (R)" },
		{ "Dig {0}, fill {1}, ladders {2}", "Копать {0}, насыпать {1}, лестниц {2}" },
		{ "A tunnel ladder needs a full block under it and a wall behind it", "Туннельной лестнице нужен полный блок снизу и стена сзади" },
		{ "A quarry ladder replaces a terrain block that stands on something", "Лестница карьера заменяет блок земли, под которым есть опора" },
		{ "Tunnel ladder: click a wall (it faces away from it). Quarry ladder: click a block, R rotates", "Туннельная лестница: клик по стене (встанет к ней спиной). Лестница карьера: клик по блоку, R поворачивает" },
		{ "A quarry ladder replaces a full terrain block that stands on something", "Лестница карьера заменяет полный блок земли (не корку), под которым есть опора" },
		{ "No mark here", "Здесь нет метки" },
		{ "Rotate (RMB or R)", "Повернуть (ПКМ или R)" },
		{ "Dig room for ladders", "Копать место под лестницы" },
		{ "Queue only this kit part", "В очередь только эту часть набора" },
		{
			"Better Workbenches: by default your crafting stations skip kit parts no rack has room for (switch it per station in its panel; needs BetterConnection)",
			"Улучшенные верстаки: по умолчанию ваши крафтильни пропускают части наборов, под которые нет места на стойках (переключается для каждой станции в её панели; нужен BetterConnection)"
		},
		{ "Rack limit ON: kit parts no rack has room for are skipped, the next order is crafted", "Лимит стоек ВКЛ: части наборов, под которые нет места на стойках, пропускаются, крафтится следующий заказ" },
		{ "Rack limit OFF: kit parts are crafted without limit", "Лимит стоек ВЫКЛ: части наборов крафтятся без ограничения" },
		{ "click to switch for this station", "клик - переключить для этой станции" },
		{ "Crafting stations", "Крафтильни" },
		{ "Selected crafting station: queue kit part 1 (part buttons of its panel, left to right)", "Выбранная крафтильня: в очередь часть набора 1 (кнопки частей в её панели слева направо)" },
		{ "Selected crafting station: queue kit part 2 (part buttons of its panel, left to right)", "Выбранная крафтильня: в очередь часть набора 2 (кнопки частей в её панели слева направо)" },
		{ "Selected crafting station: queue kit part 3 (part buttons of its panel, left to right)", "Выбранная крафтильня: в очередь часть набора 3 (кнопки частей в её панели слева направо)" },
		{ "Selected crafting station: queue kit part 4 (part buttons of its panel, left to right)", "Выбранная крафтильня: в очередь часть набора 4 (кнопки частей в её панели слева направо)" },
		{ "Selected crafting station: queue kit part 5 (part buttons of its panel, left to right)", "Выбранная крафтильня: в очередь часть набора 5 (кнопки частей в её панели слева направо)" },
		{ "Selected crafting station: queue kit part 6 (part buttons of its panel, left to right)", "Выбранная крафтильня: в очередь часть набора 6 (кнопки частей в её панели слева направо)" },
		{ "Selected crafting station: switch its rack limit (Better Workbenches)", "Выбранная крафтильня: переключить её лимит стоек (Улучшенные верстаки)" },
		{ "click: remove one, Shift+click: remove the whole stack", "клик: убрать один, Shift+клик: убрать всю стопку" },
		{
			"Better Workbenches: by default one more worker brings what the next orders of your crafting stations need while another one crafts (switch it per station in its panel; needs BetterConnection)",
			"Улучшенные верстаки: по умолчанию, пока один рабочий крафтит, ещё один подносит то, что нужно следующим заказам ваших крафтилен (переключается для каждой станции в её панели; нужен BetterConnection)"
		},
		{ "Selected crafting station: switch its helper (Better Workbenches)", "Выбранная крафтильня: переключить её помощника (Улучшенные верстаки)" },
		{ "Helper ON: while one worker crafts, one more brings what the next orders need", "Помощник ВКЛ: пока один рабочий крафтит, ещё один подносит то, что нужно следующим заказам" },
		{ "Helper OFF: only the crafting worker fetches, for the current order", "Помощник ВЫКЛ: подносит только тот, кто крафтит, и только для текущего заказа" },
		{ "Marks the block above a ladder (and a tunnel ladder's own block) for digging", "Блок над лестницей (и блок самой туннельной лестницы) помечается под раскопку" },
		{ "Keep dug resources", "Собирать выкопанное" },
		{ "Nothing drops: no stockpile needed", "Ничего не выпадает: склады не нужны" },
		{ "Kept stone goes straight into the fill; a full bag with nothing to fill leaves the task", "Камень сразу идёт в насыпь; с полным мешком и без насыпи рабочий уходит с задания" },
		{ "Clear all marks", "Очистить все метки" },
		{
			"LMB hold and drag: marks follow the face; Alt+drag: into the blocks. Ctrl+LMB deletes a mark, RMB rotates a ladder, Ctrl+Z undo, Shift / Ctrl+wheel height / width, Esc close",
			"ЛКМ зажать и вести: метки идут по грани; Alt+вести: вглубь блоков. Ctrl+ЛКМ удаляет метку, ПКМ поворачивает лестницу, Ctrl+Z назад, Shift / Ctrl+колесо высота / ширина, Esc закрыть"
		},
		{ "Create or clear your marks first, then pick the task with Edit a task", "Сначала создайте или очистите свои метки, потом выберите задание через «Изменить задание»" },
		{ "Your tasks ({0})", "Ваши задания ({0})" },
		{ "#{0}: {1} left, {2}", "№{0}: осталось {1}, {2}" },
		{ "Edit", "Изм." },
		{ "Click again to delete the task", "Нажмите ещё раз, чтобы удалить задание" },
		{ "high priority", "высокий приоритет" },
		{ "low priority", "низкий приоритет" },
		{ "normal priority", "обычный приоритет" },
		{ "paused", "на паузе" },
		{
			"LMB hold and drag: a straight line (on a wall: level, along it or straight into the hill; Alt: up or down the wall; on top: Alt digs down); ladders: drag up or down the slope. Ctrl+LMB deletes a mark, RMB rotates a ladder, Ctrl+Z undo, Shift / Ctrl+wheel height / width, Esc close. Edit a task: select it in the task list",
			"ЛКМ зажать и вести: прямая линия (на стене: ровно вдоль неё или прямо вглубь горы; с Alt вверх или вниз по стене; сверху: с Alt вниз); лестницы: вести вверх или вниз по склону. Ctrl+ЛКМ удаляет метку, ПКМ поворачивает лестницу, Ctrl+Z назад, Shift / Ctrl+колесо высота / ширина, Esc закрыть. Изменить задание: выберите его в списке заданий"
		},
		{ "Terraforming tool: dig mode","Инструмент терраформинга: режим копки" },
		{ "Terraforming tool: fill mode", "Инструмент терраформинга: режим насыпи" },
		{ "Terraforming tool: erase mode", "Инструмент терраформинга: режим стирания" },
		{ "Terraforming tool: ladder mode", "Инструмент терраформинга: режим лестниц" },
		{ "New terraforming task (dig any shape, ladders; needs BetterConnection)", "Новое задание терраформинга (копать любую форму, лестницы; нужен BetterConnection)" },
		{ "Terraforming: dig mode", "Терраформинг: режим копки" },
		{ "Terraforming: ladder mode", "Терраформинг: режим лестниц" },
		{
			"Terraforming: hold and turn the mouse wheel to change the brush width (the camera does not zoom meanwhile)",
			"Терраформинг: зажать и крутить колесо мыши - ширина кисти (камера при этом не зумится)"
		},
		{
			"Terraforming: hold and turn the mouse wheel to change the brush height (the camera does not zoom meanwhile)",
			"Терраформинг: зажать и крутить колесо мыши - высота кисти (камера при этом не зумится)"
		},
		{ "The host did not create the task", "Хост не создал задание" },
		{ "Kept resources", "Собираемые ресурсы" },
		{ "Haul to stockpile", "Относ на склад" },
		{ "Haul to stockpile when the bag is full", "Относить на склад, когда мешок полон" },
		{ "A worker whose bag is full carries the stone to a stockpile at once, then returns to the task", "Рабочий с полным мешком сразу относит камень на склад и возвращается к заданию" },
		{ "Ladders", "Лестницы" },
		{ "Marks", "Метки" },
		{ "Nothing to undo", "Откатывать нечего" },
		{ "Undone", "Откат" },
		{ "Undone: the task is deleted", "Откат: задание удалено" },
		{ "Delete a mark", "Удаление метки" },
		{
			"Dig: hold LMB and drag a straight line (on a wall: level, along it or into the hill, Alt: up / down; on top: Alt digs down)",
			"Копать: зажать ЛКМ и вести прямую линию (на стене: ровно вдоль неё или вглубь горы, Alt: вверх / вниз; сверху: Alt копает вниз)"
		},
		{
			"Tunnel ladder: click a wall (it stands with its back to it), drag up or down the slope for a chain. RMB / R rotate",
			"Туннельная лестница: клик по стене (встанет к ней спиной), вести вверх или вниз по склону - цепочка. ПКМ / R поворот"
		},
		{ "Quarry ladder: replaces a full block, drag for a staircase. RMB / R rotate", "Лестница карьера: заменяет полный блок, вести - лестничный пролёт. ПКМ / R поворот" },
		{ "Landing: a flat block to stand on, drag for a row", "Площадка: ровный блок, на котором можно стоять, вести - ряд" },
		{ "Fill: hold LMB and drag (Alt: out of the face). Workers bring stones, 10 per block, and fill from the bottom up", "Насыпь: зажать ЛКМ и вести (Alt: от грани наружу). Рабочие носят камни, 10 на блок, и насыпают снизу вверх" },
		{ "Terraforming: fill mode", "Терраформинг: режим насыпи" },
		{ "off: the block is destroyed, nothing drops", "выкл.: блок уничтожается, ничего не выпадает" },
		{ "Brush {0} wide, {1} high. Click - / +, or hold {2} / {3} and turn the mouse wheel", "Кисть: ширина {0}, высота {1}. Нажмите - / + или зажмите {2} / {3} и крутите колесо мыши" },
		{ "Terraforming: brush width - 1", "Терраформинг: ширина кисти - 1" },
		{ "Terraforming: brush width + 1", "Терраформинг: ширина кисти + 1" },
		{ "Terraforming: brush height - 1", "Терраформинг: высота кисти - 1" },
		{ "Terraforming: brush height + 1", "Терраформинг: высота кисти + 1" },
		{ "Dig any shape, tunnel and quarry ladders, landings", "Копать любую форму, туннельные лестницы и лестницы карьера, площадки" },
		{ "Bear traps", "Капканы" },
		{ "Bear traps (own)", "Капканы (свои)" },
		{ "Bear traps task: needs BetterConnection and the host rule", "Задание «Капканы»: нужен BetterConnection и правило хоста" },
		{ "Click traps or drag a frame over them to make a task", "Кликните по капканам или обведите их рамкой, чтобы создать задание" },
		{ "Traps leave the automatic task by going into an own task", "Капкан уходит из общего задания, только когда попадает в своё задание" },
		{ "Undo: bear traps task", "Отмена: задание «Капканы»" },
		{ "Own task for a group of traps: click traps or drag a frame over them (the other traps stay in the automatic task)", "Своё задание на группу капканов: кликните по капканам или обведите их рамкой (остальные капканы остаются в общем задании)" },
		{
			"Double-click on a unit selects only that unit (vanilla selects all units of its kind)",
			"Двойной клик по юниту выделяет только его (в ванили выделяются все юниты этого типа)"
		},
		{
			"Toggle (press to switch on / off)",
			"Переключатель (нажал - вкл, ещё раз - выкл)"
		},
		{
			"Hold (active while held)",
			"Удержание (пока зажато)"
		},
		{
			"Safety",
			"Безопасность"
		},
		{
			"Camera: remember and return",
			"Камера: запомнить и вернуться"
		},
		{
			"Camera: slots",
			"Камера: слоты"
		},
		{
			"View",
			"Обзор"
		},
		{
			"Control groups",
			"Контрольные группы"
		},
		{
			"Orders",
			"Приказы"
		},
		{
			"Select all units of a profession",
			"Выделить всех юнитов профессии"
		},
		{
			"While one of these keys is held, all hotkeys are off",
			"Пока зажата одна из этих клавиш, все хоткеи выключены"
		},
		{
			"While this key is held, holding RMB does not open the pie menu",
			"Пока зажата эта клавиша, удержание ПКМ не открывает круговое меню"
		},
		{
			"Remember / return (camera and selection)",
			"Запомнить / вернуться (камера и выделение)"
		},
		{
			"Remember only (no jump)",
			"Только запомнить (без прыжка)"
		},
		{
			"Save slot",
			"Сохранить слот"
		},
		{
			"Go to slot",
			"Перейти к слоту"
		},
		{
			"Next occupied slot",
			"Следующий занятый слот"
		},
		{
			"Previous occupied slot",
			"Предыдущий занятый слот"
		},
		{
			"Key",
			"Клавиша"
		},
		{
			"Mode (hold / toggle)",
			"Режим (удержание / переключатель)"
		},
		{
			"Move speed multiplier",
			"Множитель скорости перемещения"
		},
		{
			"Zoom speed multiplier",
			"Множитель скорости зума"
		},
		{
			"Orbit speed multiplier",
			"Множитель скорости вращения"
		},
		{
			"Toggle X-Ray view",
			"Включить / выключить обзор сквозь землю (X-Ray)"
		},
		{
			"Jump camera to the selection",
			"Прыжок камеры к выделению"
		},
		{
			"Show / hide the groups panel",
			"Показать / скрыть панель групп"
		},
		{
			"Camera in the ground",
			"Камера в земле"
		},
		{
			"Put the camera into the ground under the cursor, looking level (press again to go back)",
			"Поставить камеру в землю под курсором, взгляд параллельно земле (повторное нажатие возвращает назад)"
		},
		{
			"Depth below the top of the block (blocks)",
			"Глубина под верхом блока (блоков)"
		},
		{
			"Step back from the point (blocks)",
			"Отступ назад от точки (блоков)"
		},
		{
			"Tilt (degrees, + looks down, 0 = level)",
			"Наклон (градусы, + вниз, 0 = параллельно земле)"
		},
		{
			"Keep the camera turn (off = turn to the nearest block axis)",
			"Сохранять поворот камеры (выкл = по ближайшей оси блоков)"
		},
		{
			"Jump to the cursor",
			"Прыжок к курсору"
		},
		{
			"Jump the camera right over the block under the cursor or the mini map point (same level, angle and zoom; hold to keep jumping)",
			"Перенести камеру ровно над блоком под курсором или точкой миникарты (тот же уровень, угол и зум; зажатие повторяет прыжки)"
		},
		{
			"Hold: wait before repeating (ms)",
			"Зажатие: задержка перед повтором (мс)"
		},
		{
			"Hold: repeat every (ms, 0 = no repeat)",
			"Зажатие: повтор каждые (мс, 0 = без повтора)"
		},
		{
			"Keep the height above the ground (off = like WASD, the Shift+scroll level stays)",
			"Сохранять высоту над землёй (выкл = как WASD, уровень Shift+скролл не меняется)"
		},
		{
			"Mouse turn",
			"Поворот мышью"
		},
		{
			"Hold and move the mouse: the camera turns with it (1x = 360 degrees per screen width, 180 degrees per screen height)",
			"Зажмите и ведите мышью: камера поворачивается за ней (1x = 360 градусов на ширину экрана, 180 градусов на высоту)"
		},
		{
			"Sensitivity left / right",
			"Чувствительность влево / вправо"
		},
		{
			"Same sensitivity up / down (off = its own value below)",
			"Та же чувствительность вверх / вниз (выкл = своё значение ниже)"
		},
		{
			"Sensitivity up / down",
			"Чувствительность вверх / вниз"
		},
		{
			"Turn left / right",
			"Поворот влево / вправо"
		},
		{
			"Tilt up / down",
			"Наклон вверх / вниз"
		},
		{
			"Invert left / right",
			"Инвертировать влево / вправо"
		},
		{
			"Invert up / down",
			"Инвертировать вверх / вниз"
		},
		{
			"Smoothing (0 = follows the mouse at once)",
			"Сглаживание (0 = сразу за мышью)"
		},
		{
			"Turn around (0 = view centre, 1 = the camera itself)",
			"Поворот вокруг (0 = центр обзора, 1 = самой камеры)"
		},
		{
			"Lowest tilt (degrees, negative = looks up)",
			"Наименьший наклон (градусы, минус = взгляд вверх)"
		},
		{
			"Highest tilt (degrees, 90 = straight down)",
			"Наибольший наклон (градусы, 90 = прямо вниз)"
		},
		{
			"Insta zoom",
			"Мгновенный зум"
		},
		{
			"Zoom in at once; press again to zoom back out to where you were",
			"Мгновенно приблизить; повторное нажатие отдаляет обратно, как было"
		},
		{
			"Proportional (zoom by a share of the current distance; off = by a fixed number of blocks)",
			"Пропорционально (зум на долю текущего расстояния; выкл = на фиксированное число блоков)"
		},
		{
			"Proportional: zoom by (percent of the distance)",
			"Пропорционально: зум на (процент расстояния)"
		},
		{
			"Fixed: zoom by (blocks)",
			"Фиксированно: зум на (блоков)"
		},
		{
			"Never closer than (blocks)",
			"Не ближе чем (блоков)"
		},
		{
			"Zoom out instead of in",
			"Отдалять вместо приближения"
		},
		{
			"Zoom towards the point under the cursor or the mini map (off = the view centre)",
			"Зум к точке под курсором или на миникарте (выкл = к центру обзора)"
		},
		{
			"Towards the cursor: move the view there (like Jump to the cursor; off = the point stays under the cursor)",
			"К курсору: переносить обзор туда (как прыжок к курсору; выкл = точка остаётся под курсором)"
		},
		{
			"Tap on a bricktron: follow it (until you move the camera some other way or press Esc)",
			"Тап по бриктрону: следить за ним (пока не сдвинете камеру иначе или не нажмёте Esc)"
		},
		{
			"Zooming back out yourself (wheel or anything else) counts as the zoom back: the next press zooms in again",
			"Если вы сами отдалили камеру (колесом или чем угодно), это считается отзумом: следующее нажатие снова приближает"
		},
		{
			"Double tap: back into the previous zoom point (ms window, 0 = off)",
			"Двойной тап: обратно в прошлую точку зума (окно в мс, 0 = выкл)"
		},
		{
			"Re-zoom: back into the last zoom point, wherever the cursor is",
			"Резум: обратно в последнюю точку зума, где бы ни был курсор"
		},
		{
			"Add to group: hold this key + a group's select key to add the selected units to that group",
			"Добавить в группу: зажмите эту клавишу + клавишу выбора группы, чтобы добавить выделенных юнитов в эту группу"
		},
		{
			"Honest wheel zoom",
			"Честный зум колесом"
		},
		{
			"Honest wheel zoom: every notch zooms by the same step at once, no speed-up and no inertia (replaces the game's wheel zoom; Shift+wheel still sets the level)",
			"Честный зум колесом: каждый щелчок колеса сразу зумит на одинаковый шаг, без ускорений и инерции (заменяет игровой зум колесом; Shift+колесо по-прежнему меняет уровень)"
		},
		{
			"Proportional (percent of the distance per notch; off = blocks per notch)",
			"Пропорционально (процент расстояния за щелчок; выкл = блоков за щелчок)"
		},
		{
			"Proportional: step per notch (percent)",
			"Пропорционально: шаг за щелчок (процент)"
		},
		{
			"Fixed: step per notch (blocks)",
			"Фиксированно: шаг за щелчок (блоков)"
		},
		{
			"Closest zoom (blocks)",
			"Ближайший зум (блоков)"
		},
		{
			"Farthest zoom (blocks)",
			"Дальний зум (блоков)"
		},
		{
			"Zoom towards the cursor (the point under the cursor stays under it)",
			"Зум к курсору (точка под курсором остаётся под ним)"
		},
		{
			"Invert the wheel",
			"Инвертировать колесо"
		},
		{
			"Glide per notch (sec, 0 = instant)",
			"Плавность щелчка (сек, 0 = мгновенно)"
		},
		{
			"Camera history",
			"История камеры"
		},
		{
			"Camera back: to where it was before the last jump, zoom or turn",
			"Камера назад: туда, где она была до последнего прыжка, зума или поворота"
		},
		{
			"Camera forward: undo a step back",
			"Камера вперёд: отменить шаг назад"
		},
		{
			"Remember up to (steps)",
			"Помнить до (шагов)"
		},
		{
			"While in the ground: Jump to the cursor does nothing",
			"Когда камера в земле: прыжок к курсору не работает"
		},
		{
			"While in the ground: Insta zoom does nothing",
			"Когда камера в земле: мгновенный зум не работает"
		},
		{
			"Animation (sec, 0 = instant)",
			"Анимация (сек, 0 = мгновенно)"
		},
		{
			"Camera when a map loads",
			"Камера при загрузке карты"
		},
		{
			"Multi-camera",
			"Мультикамера"
		},
		{
			"Split the screen: add one more camera view (up to 8, the new view starts where you look)",
			"Разделить экран: добавить ещё одну камеру (до 8, новая смотрит туда же, куда вы)"
		},
		{
			"Split into two / collapse to one camera (press again to bring the old layout back)",
			"Разделить на два / схлопнуть в одну камеру (повторное нажатие возвращает прежнюю раскладку)"
		},
		{
			"Close the view under the cursor",
			"Закрыть экран под курсором"
		},
		{
			"Fullscreen the view under the cursor (press again to go back)",
			"Развернуть экран под курсором на весь монитор (повторно: назад)"
		},
		{
			"Pull the view under the cursor out as a free window / put it back into the grid",
			"Вытащить экран под курсором в свободное окно / вернуть в сетку"
		},
		{
			"Put every free window back into the grid",
			"Вернуть все свободные окна в сетку"
		},
		{
			"Control the next view (stays until the mouse moves)",
			"Управлять следующим экраном (держится, пока не сдвинете мышь)"
		},
		{
			"Control the previous view (stays until the mouse moves)",
			"Управлять предыдущим экраном (держится, пока не сдвинете мышь)"
		},
		{
			"Every view jumps to where the current one looks",
			"Все экраны переходят туда, куда смотрит текущий"
		},
		{
			"Control this view (stays until the mouse moves)",
			"Управлять этим экраном (держится, пока не сдвинете мышь)"
		},
		{
			"Hold + left mouse drag anywhere in a view moves it as a free window, + right mouse drag resizes it",
			"Удерживать + перетаскивание левой кнопкой в любом месте экрана двигает его как свободное окно, правой кнопкой меняет размер"
		},
		{
			"Show the small title bar (number, free / max / close buttons) on every view",
			"Показывать маленькую полоску на каждом экране (номер, кнопки: окно / во весь / закрыть)"
		},
		{
			"Lighter picture on the views you are not controlling (no ambient occlusion), saves video card time",
			"Упрощённая картинка на экранах, которыми вы не управляете (без затенения углов), экономит видеокарту"
		},
		{
			"Remember the split between games (every view starts where the camera starts)",
			"Помнить раскладку между играми (каждый экран стартует там же, где камера)"
		},
		{
			"Set the camera angle when a map loads",
			"Ставить угол камеры при загрузке карты"
		},
		{
			"Tilt (degrees, 90 = straight down)",
			"Наклон (градусы, 90 = прямо вниз)"
		},
		{
			"Also set the turn (off = keep the game's turn)",
			"Ставить и поворот (выкл = поворот игры)"
		},
		{
			"Turn (degrees, 0 = north)",
			"Поворот (градусы, 0 = север)"
		},
		{
			"Zoom distance (blocks, 0 = keep the game's)",
			"Дальность зума (блоков, 0 = как у игры)"
		},
		{
			"Wait after the map is ready (sec)",
			"Задержка после готовности карты (сек)"
		},
		{
			"Quick turn",
			"Быстрый поворот"
		},
		{
			"Hold, draw an arrow with the mouse, release: the camera turns to look where the arrow points (Esc cancels)",
			"Зажмите, проведите мышью стрелку и отпустите: камера развернётся туда, куда указывает стрелка (Esc отменяет)"
		},
		{
			"Turn around (0 = view centre, 1 = arrow start, 2 = the camera itself)",
			"Поворот вокруг (0 = центр обзора, 1 = начало стрелки, 2 = самой камеры)"
		},
		{
			"Snap the angle to steps of (degrees, 0 = free)",
			"Шаг угла (градусы, 0 = свободно)"
		},
		{
			"Save group",
			"Сохранить группу"
		},
		{
			"Select group",
			"Выбрать группу"
		},
		{
			"Save selection into the first free group",
			"Сохранить выделение в первую свободную группу"
		},
		{
			"Clear all groups",
			"Очистить все группы"
		},
		{
			"Cycle through occupied groups",
			"Листать занятые группы по кругу"
		},
		{
			"Cycle through occupied groups (no camera jump)",
			"Листать занятые группы по кругу (камера не двигается)"
		},
		{
			"LMB / RMB can be bound too (press the mouse button while a field is waiting). A bare click fires everywhere outside the game interface, so for LMB / RMB it is better to hold Ctrl / Shift / Alt.",
			"ЛКМ / ПКМ тоже можно назначить (нажмите кнопку мыши, пока поле ждёт клавишу). Голый клик срабатывает везде вне интерфейса игры, поэтому для ЛКМ / ПКМ лучше зажимать Ctrl / Shift / Alt."
		},
		{
			"Auto-assign units to free groups by proximity",
			"Авто-раздача юнитов по свободным группам (по близости)"
		},
		{
			"Auto-group cluster radius (blocks)",
			"Радиус кластера авто-групп (блоки)"
		},
		{
			"Dig the block under the cursor (selected units)",
			"Копать блок под курсором (выделенные юниты)"
		},
		{
			"Select all Builders",
			"Выделить всех строителей"
		},
		{
			"Select all Halberdiers",
			"Выделить всех алебардщиков"
		},
		{
			"Select all Knights",
			"Выделить всех рыцарей"
		},
		{
			"Select all Archers",
			"Выделить всех лучников"
		},
		{
			"Select all Arbalists",
			"Выделить всех арбалетчиков"
		},
		{
			"Select all Alchemists",
			"Выделить всех алхимиков"
		},
		{
			"Select all Artificers",
			"Выделить всех артефакторов"
		},
		{
			"Format:",
			"Формат:"
		},
		{
			"predict:",
			"предсказание:"
		},
		{
			"Discord RPC: set ClientId in configs/discord.txt",
			"Discord RPC: укажите ClientId в configs/discord.txt"
		},
		{
			"starts at",
			"начинается с"
		},
		{
			"Camera distance",
			"Дистанция камеры"
		},
		{
			"DPS counts stamina damage + HP together",
			"DPS считает урон по выносливости вместе с HP"
		},
		{
			"DPS stays for",
			"DPS держится"
		},
		{
			"Fight over after no damage for",
			"Бой окончен, если нет урона"
		},
		{
			"Hide numbers if camera farther",
			"Скрывать числа, если камера дальше"
		},
		{
			"Numbers on every voxel (all layers)",
			"Числа на каждом вокселе (все слои)"
		},
		{
			"Numbers opacity",
			"Прозрачность чисел"
		},
		{
			"Super precision (DPS to thousandths)",
			"Суперточность (DPS до тысячных)"
		},
		{
			"2nd color brightness",
			"Яркость 2-го цвета"
		},
		{
			"3D (layers above and below)",
			"3D (слои выше и ниже)"
		},
		{
			"Alchemist bomb count",
			"Число бомб алхимика"
		},
		{
			"Auto DPS after the fight ends - bricktrons",
			"Авто-DPS после боя - бриктроны"
		},
		{
			"Auto DPS after the fight ends - buildings",
			"Авто-DPS после боя - здания"
		},
		{
			"Bad for me (enemy captures / I lose)",
			"Плохо для меня (враг захватывает / я теряю)"
		},
		{
			"Bold",
			"Жирный"
		},
		{
			"Bomb map (cells and numbers)",
			"Карта бомб (клетки и числа)"
		},
		{
			"Buildings / objects - enemy (visible only)",
			"Здания / объекты - вражеские (только видимые)"
		},
		{
			"Buildings / objects - own",
			"Здания / объекты - свои"
		},
		{
			"Cells opacity",
			"Прозрачность клеток"
		},
		{
			"Client: movement trail (fallback)",
			"Клиент: след движения (запасной вариант)"
		},
		{
			"Client: predict route from my orders",
			"Клиент: предсказывать маршрут по моим приказам"
		},
		{
			"Color",
			"Цвет"
		},
		{
			"Color enemy",
			"Цвет врага"
		},
		{
			"Color from HP palette",
			"Цвет из палитры HP"
		},
		{
			"Color from ammo palette",
			"Цвет из палитры боезапаса"
		},
		{
			"Color from stamina palette",
			"Цвет из палитры выносливости"
		},
		{
			"Color own / allied",
			"Цвет своих / союзных"
		},
		{
			"Color stamina damage",
			"Цвет урона по выносливости"
		},
		{
			"Colors (weak -> strong damage)",
			"Цвета (слабый -> сильный урон)"
		},
		{
			"Colors by fixed damage (not % of max)",
			"Цвета по фикс. урону (не % от макс.)"
		},
		{
			"Count the killing blow in full (no overkill loss)",
			"Засчитывать добивающий удар полностью (без потери на оверкилл)"
		},
		{
			"Crystal timer",
			"Таймер кристалла"
		},
		{
			"DPS color",
			"Цвет DPS"
		},
		{
			"Damage numbers and DPS",
			"Числа урона и DPS"
		},
		{
			"Destination marker",
			"Маркер цели"
		},
		{
			"Distance number size",
			"Размер числа дистанции"
		},
		{
			"Distance number stays for",
			"Число дистанции держится"
		},
		{
			"Dot size",
			"Размер точки"
		},
		{
			"Draw over terrain",
			"Рисовать поверх местности"
		},
		{
			"Forced rings (Always show own / enemy)",
			"Принудительные кольца (Всегда показывать своих / врагов)"
		},
		{
			"Format: ",
			"Формат: "
		},
		{
			"Good for me (I capture / keep)",
			"Хорошо для меня (я захватываю / держу)"
		},
		{
			"HP number Offset Y (up)",
			"Смещение Y числа HP (вверх)"
		},
		{
			"Height above crystal",
			"Высота над кристаллом"
		},
		{
			"Height above ground",
			"Высота над землёй"
		},
		{
			"Height offset",
			"Смещение по высоте"
		},
		{
			"Hide beyond",
			"Скрывать дальше"
		},
		{
			"Hide when camera is far from the unit",
			"Скрывать, когда камера далеко от юнита"
		},
		{
			"Hide when full",
			"Скрывать, когда полное"
		},
		{
			"Layers up / down",
			"Слоёв вверх / вниз"
		},
		{
			"Line width",
			"Толщина линии"
		},
		{
			"Number stays for",
			"Число держится"
		},
		{
			"Numbers (damage)",
			"Числа (урон)"
		},
		{
			"Numbers: HP / stamina / regen (units, buildings)",
			"Числа: HP / выносливость / реген (юниты, здания)"
		},
		{
			"Offset X",
			"Смещение X"
		},
		{
			"Offset X (stamina numbers go to the left)",
			"Смещение X (числа выносливости уходят влево)"
		},
		{
			"Offset Y (up)",
			"Смещение Y (вверх)"
		},
		{
			"Only selected units",
			"Только выбранные юниты"
		},
		{
			"Opacity",
			"Непрозрачность"
		},
		{
			"Outline",
			"Обводка"
		},
		{
			"Path lines",
			"Линии пути"
		},
		{
			"Performance mode (fewer barrels, 3 layers max, fewer numbers)",
			"Режим производительности (меньше бочек, макс. 3 слоя, меньше чисел)"
		},
		{
			"Range (blocks from barrel)",
			"Радиус (блоков от бочки)"
		},
		{
			"Regen Offset Y (up)",
			"Смещение Y регена (вверх)"
		},
		{
			"Separate enemy palettes",
			"Отдельные палитры врагов"
		},
		{
			"Shift (- darker, + lighter)",
			"Сдвиг (- темнее, + светлее)"
		},
		{
			"Show only these colors (hidden ones are invisible, numbers too)",
			"Показывать только эти цвета (скрытые невидимы, числа тоже)"
		},
		{
			"Size",
			"Размер"
		},
		{
			"Stamina numbers only for knights",
			"Числа выносливости только у рыцарей"
		},
		{
			"Units - enemy (visible only)",
			"Юниты - вражеские (только видимые)"
		},
		{
			"Units - own / allied",
			"Юниты - свои / союзные"
		},
		{
			"predict: ",
			"предсказание: "
		},
		{
			"(click to change)",
			"(нажмите, чтобы изменить)"
		},
		{
			"damage",
			" урона"
		},
		{
			"off",
			"выкл"
		},
		{
			"px",
			"пикс"
		},
		{
			"vox",
			"вокс"
		},
		{
			"% of max",
			"% от макс."
		},
		{
			"1 bone",
			"1 кость"
		},
		{
			"2 bones",
			"2 кости"
		},
		{
			"4 bones",
			"4 кости"
		},
		{
			"Advanced (shader overrides, intervals)",
			"Дополнительно (шейдеры, интервалы)"
		},
		{
			"Alchemist ammo ring (bar)",
			"Кольцо боезапаса алхимика (полоска)"
		},
		{
			"All types",
			"Все типы"
		},
		{
			"Always show enemy rings (even if not selected / not hurt)",
			"Всегда показывать кольца врагов (даже если не выбраны / не ранены)"
		},
		{
			"Always show own rings (even if not selected / not hurt)",
			"Всегда показывать свои кольца (даже если не выбраны / не ранены)"
		},
		{
			"Ammo - alchemist (10 bombs = 100%)",
			"Боезапас - алхимик (10 бомб = 100%)"
		},
		{
			"Animators: skip bone updates off-screen (experimental)",
			"Аниматоры: не обновлять кости вне экрана (эксперимент)"
		},
		{
			"Anisotropic filtering",
			"Анизотропная фильтрация"
		},
		{
			"Anti-aliasing (MSAA)",
			"Сглаживание (MSAA)"
		},
		{
			"As game",
			"Как в игре"
		},
		{
			"Balanced",
			"Сбалансированный"
		},
		{
			"Better Than Castle",
			"Better Than Castle"
		},
		{
			"BetterConnection",
			"BetterConnection"
		},
		{
			"BetterConnection (master switch)",
			"BetterConnection (главный переключатель)"
		},
		{
			"Block game input while this menu is open",
			"Блокировать ввод игры, пока меню открыто"
		},
		{
			"Bloom off",
			"Без блума"
		},
		{
			"Blue",
			"Синий"
		},
		{
			"Bomb counter (alchemist)",
			"Счётчик бомб (алхимик)"
		},
		{
			"Buildings (storage, toolracks, crystals; size/format/opacity as above)",
			"Здания (склады, стойки, кристаллы; размер/формат/прозрачность как выше)"
		},
		{
			"Buildings damage - enemy (visible only)",
			"Урон по зданиям - вражеским (только видимые)"
		},
		{
			"Buildings damage - own",
			"Урон по зданиям - своим"
		},
		{
			"Camera effects",
			"Эффекты камеры"
		},
		{
			"Camera view distance",
			"Дальность обзора камеры"
		},
		{
			"Cheap mode: main camera only, computed once per frame",
			"Дешёвый режим: только главная камера, расчёт раз за кадр"
		},
		{
			"Click again to reset ALL Colored HP settings",
			"Нажмите ещё раз, чтобы сбросить ВСЕ настройки Colored HP"
		},
		{
			"Clicking a value cycles it. \"As game\" = the mod does not touch it.",
			"Нажатие по значению переключает его. «Как в игре» = мод не трогает."
		},
		{
			"Clients are allowed to pause the game",
			"Клиентам разрешено ставить игру на паузу"
		},
		{
			"Color HP ring (ring 1)",
			"Красить HP-кольцо (кольцо 1)"
		},
		{
			"Color ammo ring - alchemist (ring 2)",
			"Красить кольцо боезапаса - алхимик (кольцо 2)"
		},
		{
			"Color enemy rings (off = vanilla)",
			"Красить вражеские кольца (выкл = как в ванили)"
		},
		{
			"Color stamina ring - knight (ring 2)",
			"Красить кольцо выносливости - рыцарь (кольцо 2)"
		},
		{
			"Colored HP (master switch)",
			"Colored HP (главный переключатель)"
		},
		{
			"Crystal capture timer",
			"Таймер захвата кристалла"
		},
		{
			"Damage counter / crystal timer",
			"Счётчик урона / таймер кристалла"
		},
		{
			"Damage counting always runs (numbers stay correct when Alt is pressed).",
			"Подсчёт урона идёт всегда (числа остаются верными при нажатии Alt)."
		},
		{
			"Dark orange",
			"Тёмно-оранжевый"
		},
		{
			"Dark red",
			"Тёмно-красный"
		},
		{
			"Day brightness",
			"Яркость днём"
		},
		{
			"Day/Night brightness of the mod: cheap mode",
			"Яркость мода день/ночь: дешёвый режим"
		},
		{
			"Debug",
			"Отладка"
		},
		{
			"Diagnostic log (unet_netdiag_*.log)",
			"Диагностический лог (unet_netdiag_*.log)"
		},
		{
			"Discord Rich Presence",
			"Discord Rich Presence"
		},
		{
			"Dump buildings",
			"Выгрузить здания"
		},
		{
			"Dump textures",
			"Выгрузить текстуры"
		},
		{
			"Dumps (written to BetterThanCastle/debug/dump)",
			"Дампы (пишутся в BetterThanCastle/debug/dump)"
		},
		{
			"Eighth",
			"Восьмая"
		},
		{
			"Enemy overrides (only if enemies are colored)",
			"Переопределения врагов (только если враги покрашены)"
		},
		{
			"Enemy skin from pack (<pack>/Enemy folder)",
			"Скин врага из пака (папка <пак>/Enemy)"
		},
		{
			"Entity center (explosion aim point)",
			"Центр сущности (точка прицела взрыва)"
		},
		{
			"Every 2nd frame",
			"Каждый 2-й кадр"
		},
		{
			"Every frame",
			"Каждый кадр"
		},
		{
			"Explosion damage is measured to the nearest dot of the target",
			"Урон взрыва считается до ближайшей точки цели"
		},
		{
			"Explosion map (barrels, damage to bricktrons)",
			"Карта взрывов (бочки, урон бриктронам)"
		},
		{
			"FPS limit",
			"Лимит FPS"
		},
		{
			"Fireflies (look only)",
			"Светлячки (только вид)"
		},
		{
			"Fireflies spawn units: only their look is hidden, logic keeps running.",
			"Светлячки создают юнитов: скрывается только вид, логика продолжает работать."
		},
		{
			"Force low",
			"Принудительно низко"
		},
		{
			"Forced",
			"Принудительно"
		},
		{
			"Frame rate / VSync",
			"Частота кадров / VSync"
		},
		{
			"Full",
			"Полное"
		},
		{
			"Full (soft)",
			"Полное (мягкое)"
		},
		{
			"Game default",
			"По умолчанию в игре"
		},
		{
			"Green",
			"Зелёный"
		},
		{
			"Grey-white",
			"Серо-белый"
		},
		{
			"HP - buildings enemy",
			"HP - здания врага"
		},
		{
			"HP - buildings own",
			"HP - свои здания"
		},
		{
			"HP - enemy",
			"HP - враг"
		},
		{
			"HP - own",
			"HP - свои"
		},
		{
			"HP / stamina numbers",
			"Числа HP / выносливости"
		},
		{
			"HP components of non-bricktron objects (buildings): BetterThanCastle/debug/dump/buildings.txt.",
			"HP-компоненты небриктронных объектов (здания): BetterThanCastle/debug/dump/buildings.txt."
		},
		{
			"HP damage - enemies",
			"Урон по HP - враги"
		},
		{
			"HP damage - own / allied",
			"Урон по HP - свои / союзные"
		},
		{
			"HP number",
			"Число HP"
		},
		{
			"HP number - enemy (visible only)",
			"Число HP - враг (только видимые)"
		},
		{
			"HP number - enemy buildings (visible only)",
			"Число HP - вражеские здания (только видимые)"
		},
		{
			"HP number - own / allied",
			"Число HP - свои / союзные"
		},
		{
			"HP number - own buildings",
			"Число HP - свои здания"
		},
		{
			"Half",
			"Половина"
		},
		{
			"Hard only",
			"Только жёсткие"
		},
		{
			"Hide \"Client pause: allowed\" label",
			"Скрыть надпись «Пауза клиента: разрешена»"
		},
		{
			"Hide all",
			"Скрыть все"
		},
		{
			"Hide far ones",
			"Скрывать дальние"
		},
		{
			"Hide ping display",
			"Скрыть отображение пинга"
		},
		{
			"High",
			"Высокое"
		},
		{
			"Hit damage counter (bricktrons, buildings)",
			"Счётчик урона за удар (бриктроны, здания)"
		},
		{
			"LOD bias (0 = as game)",
			"Смещение LOD (0 = как в игре)"
		},
		{
			"Lens flares off",
			"Без бликов линз"
		},
		{
			"Light mode for numbers, damage, crystals, bomb map",
			"Лёгкий режим для чисел, урона, кристаллов, карты бомб"
		},
		{
			"Light range limit (0 = off)",
			"Лимит дальности света (0 = выкл)"
		},
		{
			"Lights",
			"Освещение"
		},
		{
			"List of the game's textures plus their original PNGs - the base for making a resource pack.",
			"Список текстур игры и их оригинальные PNG - основа для создания ресурс-пака."
		},
		{
			"Locked while hosting or connected. Leave the lobby/game to switch it.",
			"Заблокировано при хосте или подключении. Выйдите из лобби/игры, чтобы изменить."
		},
		{
			"Low",
			"Низкое"
		},
		{
			"Low (cheapest)",
			"Низкое (самое дешёвое)"
		},
		{
			"Low view distance makes far terrain pop in. Animators: UI untouched.",
			"Малая дальность обзора вызывает появление далёкой местности. Аниматоры: интерфейс не затронут."
		},
		{
			"Max point/spot lights",
			"Макс. точечных/прожекторных источников"
		},
		{
			"Medium",
			"Среднее"
		},
		{
			"Minimal",
			"Минимальное"
		},
		{
			"Nature LOD (trees, rocks)",
			"LOD природы (деревья, камни)"
		},
		{
			"Night brightness",
			"Яркость ночью"
		},
		{
			"Auto-equalize gamma (same brightness day and night)",
			"Автоматически выравнивать гамму (одинаковая яркость днём и ночью)"
		},
		{
			"Master gamma",
			"Мастер-гамма"
		},
		{
			"No",
			"Нет"
		},
		{
			"No data yet",
			"Данных пока нет"
		},
		{
			"None",
			"Нет"
		},
		{
			"Off",
			"Выкл"
		},
		{
			"Off: fully vanilla networking. You can play with vanilla players, as host or as client. All BetterConnection options below are hidden.",
			"Выкл: полностью ванильная сеть. Можно играть с ванильными игроками, как хост или клиент. Все опции BetterConnection ниже скрыты."
		},
		{
			"On",
			"Вкл"
		},
		{
			"On screen and diagnostics",
			"На экране и диагностика"
		},
		{
			"On: improved connection (heartbeat, auto-reconnect instead of kicking to the menu, pings, voluntary pause). Everyone in the game needs BetterConnection too.",
			"Вкл: улучшенное соединение (heartbeat, автопереподключение вместо вылета в меню, пинги, добровольная пауза). BetterConnection нужен всем в игре."
		},
		{
			"Or turn off single post effects:",
			"Или отключить отдельные пост-эффекты:"
		},
		{
			"Other",
			"Другое"
		},
		{
			"Other camera effects off (sun shafts, fog, DoF...)",
			"Прочие эффекты камеры выкл (лучи солнца, туман, DoF...)"
		},
		{
			"Overlays",
			"Оверлеи"
		},
		{
			"Interface",
			"Интерфейс"
		},
		{
			"Units",
			"Юниты"
		},
		{
			"Minimap",
			"Миникарта"
		},
		{
			"Bottom bar",
			"Нижняя панель"
		},
		{
			"Top bar",
			"Верхняя панель"
		},
		{
			"Own overrides (ring shader properties)",
			"Свои переопределения (свойства шейдера колец)"
		},
		{
			"Palettes",
			"Палитры"
		},
		{
			"Particle effects",
			"Эффекты частиц"
		},
		{
			"Particle range (0 = off)",
			"Дальность частиц (0 = выкл)"
		},
		{
			"Particles / fireflies",
			"Частицы / светлячки"
		},
		{
			"Path lines (own / allied routes)",
			"Линии пути (маршруты своих / союзных)"
		},
		{
			"Pause allowed by host",
			"Пауза разрешена хостом"
		},
		{
			"Pause the game (voluntary pause)",
			"Поставить игру на паузу (добровольная пауза)"
		},
		{
			"Pause the game while this menu is open",
			"Ставить игру на паузу, пока меню открыто"
		},
		{
			"Pause, pings and client permissions appear here during a network game.",
			"Пауза, пинги и разрешения клиентов появятся здесь в сетевой игре."
		},
		{
			"Performance",
			"Производительность"
		},
		{
			"Performance mode (lighter on FPS)",
			"Режим производительности (меньше нагрузка на FPS)"
		},
		{
			"Performance overrides ON (master switch)",
			"Переопределения производительности ВКЛ (главный переключатель)"
		},
		{
			"Pings",
			"Пинги"
		},
		{
			"Pink",
			"Розовый"
		},
		{
			"Pixel lights",
			"Пиксельные источники света"
		},
		{
			"Point / spot lights: no shadows",
			"Точечные / прожекторные источники: без теней"
		},
		{
			"Potato",
			"Картошка"
		},
		{
			"Presets (set everything, then tweak below)",
			"Пресеты (задают всё, затем донастройте ниже)"
		},
		{
			"Quality / textures",
			"Качество / текстуры"
		},
		{
			"Quarter",
			"Четверть"
		},
		{
			"Realtime reflection probes",
			"Зонды отражений в реальном времени"
		},
		{
			"Red",
			"Красный"
		},
		{
			"Reduced",
			"Сниженное"
		},
		{
			"Regen countdown (sec) - enemy (visible only)",
			"Отсчёт регена (сек) - враг (только видимые)"
		},
		{
			"Regen countdown (sec) - own / allied",
			"Отсчёт регена (сек) - свои / союзные"
		},
		{
			"Regen countdown (shown while regen is waiting)",
			"Отсчёт регена (виден, пока реген ждёт)"
		},
		{
			"Regen countdown - enemy buildings (visible only)",
			"Отсчёт регена - вражеские здания (только видимые)"
		},
		{
			"Regen countdown - own buildings",
			"Отсчёт регена - свои здания"
		},
		{
			"Reset to defaults",
			"Сбросить по умолчанию"
		},
		{
			"Resource Packs",
			"Ресурс-паки"
		},
		{
			"Resume the game (voluntary pause is on)",
			"Продолжить игру (добровольная пауза включена)"
		},
		{
			"Ring search every (s)",
			"Поиск колец каждые (с)"
		},
		{
			"Rings (what is colored)",
			"Кольца (что красится)"
		},
		{
			"SSAO (ambient occlusion)",
			"SSAO (затенение окружения)"
		},
		{
			"Scan scene now (also written to debug/mod_log.txt)",
			"Сканировать сцену (также пишется в debug/mod_log.txt)"
		},
		{
			"Scene info (what is heavy)",
			"Информация о сцене (что тяжёлое)"
		},
		{
			"Selected unit: rings lighter / darker",
			"Выбранный юнит: кольца светлее / темнее"
		},
		{
			"Separate HP colors for buildings (storage, racks, crystals...)",
			"Отдельные цвета HP для зданий (склады, стойки, кристаллы...)"
		},
		{
			"Session",
			"Сессия"
		},
		{
			"Settings",
			"Настройки"
		},
		{
			"Shadow cascades",
			"Каскады теней"
		},
		{
			"Shadow distance (0 = as game)",
			"Дальность теней (0 = как в игре)"
		},
		{
			"Shadow resolution",
			"Разрешение теней"
		},
		{
			"Shadows",
			"Тени"
		},
		{
			"Show",
			"Показать"
		},
		{
			"Show barrel explosion map",
			"Показывать карту взрывов бочек"
		},
		{
			"Show bomb count (0-10)",
			"Показывать число бомб (0-10)"
		},
		{
			"Show entity center (dot)",
			"Показывать центр сущности (точка)"
		},
		{
			"Show explosion distance to the center (number after a blast)  [HEAVY: loads the game a lot]",
			"Показывать дистанцию взрыва до центра (число после взрыва)  [ТЯЖЁЛО: сильно грузит игру]"
		},
		{
			"Show overlays only while LeftAlt is held",
			"Показывать оверлеи только при зажатом LeftAlt"
		},
		{
			"Show routes",
			"Показывать маршруты"
		},
		{
			"Show seconds to capture / to lose",
			"Показывать секунды до захвата / до потери"
		},
		{
			"Skinning (bones per vertex)",
			"Скиннинг (костей на вершину)"
		},
		{
			"Skip updating overlays hidden by Alt-only mode",
			"Не обновлять оверлеи, скрытые режимом «только Alt»"
		},
		{
			"Soft particles",
			"Мягкие частицы"
		},
		{
			"Stamina - enemy knight",
			"Выносливость - вражеский рыцарь"
		},
		{
			"Stamina - knight",
			"Выносливость - рыцарь"
		},
		{
			"Stamina damage - enemies",
			"Урон по выносливости - враги"
		},
		{
			"Stamina damage - own / allied",
			"Урон по выносливости - свои / союзные"
		},
		{
			"Stamina number",
			"Число выносливости"
		},
		{
			"Stamina number - enemy (visible only)",
			"Число выносливости - враг (только видимые)"
		},
		{
			"Stamina number - own / allied",
			"Число выносливости - свои / союзные"
		},
		{
			"Stamina ring only for knights",
			"Кольцо выносливости только у рыцарей"
		},
		{
			"Status",
			"Статус"
		},
		{
			"Texture resolution",
			"Разрешение текстур"
		},
		{
			"This mod's overlays",
			"Оверлеи этого мода"
		},
		{
			"Unlimited",
			"Без ограничений"
		},
		{
			"VSync",
			"VSync"
		},
		{
			"Very high",
			"Очень высокое"
		},
		{
			"Visibility",
			"Видимость"
		},
		{
			"Whole post-processing stack off (colour grading too)",
			"Весь пост-процессинг выкл (цветокоррекция тоже)"
		},
		{
			"World / units",
			"Мир / юниты"
		},
		{
			"Yellow",
			"Жёлтый"
		},
		{
			"Yes",
			"Да"
		},
		{
			"by NotNonlokill",
			"by NotNonlokill"
		},
		{
			"Enabled",
			"Включено"
		},
		{
			"Unit",
			"Юнит"
		},
		{
			"Mage Passive Radius",
			"Радиус пассивки мага"
		},
		{
			"Packs",
			"Паки"
		},
		{
			"Hats",
			"Шапки"
		},
		{
			"Halloween Hats",
			"Хэллоуинские шапки"
		},
		{
			"Bomb Trajectory",
			"Траектория бомбы"
		},
		{
			"Path Timer",
			"Таймер пути"
		},
		{
			"Smash Trajectory",
			"Траектория отброса"
		},
		{
			"Only while holding LeftAlt",
			"Только при зажатом LeftAlt"
		},
		{
			"Radius (blocks)",
			"Радиус (блоки)"
		},
		{
			"Climb penalty (sec)",
			"Штраф за подъём (сек)"
		},
		{
			"Step-down penalty (sec)",
			"Штраф за спуск (сек)"
		},
		{
			"Run speed (m/s)",
			"Скорость бега (м/с)"
		},
		{
			"Show landing label",
			"Подпись точки падения"
		},
		{
			"Hit block",
			"Попадание в блок"
		},
		{
			"Floor",
			"Пол"
		},
		{
			"Abyss",
			"Бездна"
		},
		{
			"Add a waypoint at the cursor (selected units walk the queue in order)",
			"Поставить вейпоинт под курсором (выделенные идут по очереди точек)"
		},
		{
			"Waypoints: show on map",
			"Вейпоинты: показывать на карте"
		},
		{
			"Waypoints: only for selected units",
			"Вейпоинты: только для выделенных юнитов"
		},
		{
			"Waypoints: show numbers",
			"Вейпоинты: показывать номера"
		},
		{
			"Waypoint size",
			"Размер вейпоинта"
		},
		{
			"Waypoint opacity",
			"Прозрачность вейпоинта"
		},
		{
			"Waypoint color",
			"Цвет вейпоинта"
		},
		{
			"Waypoint arrival radius (blocks)",
			"Радиус прибытия к вейпоинту (блоки)"
		},
		{
			"Waypoint look-ahead (sec, go to the next point early so units do not stop)",
			"Опережение вейпоинта (сек, идти к следующей точке заранее, чтобы не тормозили)"
		},
		{
			"Waypoint: skip it when the unit stands still for (sec, 0 = never)",
			"Вейпоинт: пропуск, если юнит стоит на месте (сек, 0 = никогда)"
		},
		{
			"Skip unreachable waypoint after (sec, 0 = never)",
			"Пропуск недостижимого вейпоинта через (сек, 0 = никогда)"
		},
		{
			"Effect:",
			"Эффект:"
		},
		{
			"Gradient scroll",
			"Прокрутка градиента"
		},
		{
			"Wave",
			"Волна"
		},
		{
			"Shine",
			"Блик"
		},
		{
			"Flicker",
			"Мерцание"
		},
		{
			"Crossfade",
			"Перетекание"
		},
		{
			"Effects use the colors of the applied nick (Colors / Gradients tabs). Speed, Updates/sec and Repeat apply to all of them.",
			"Эффекты берут цвета применённого ника (вкладки Цвета / Градиенты). Скорость, Обновл./сек и Повтор работают для всех."
		},
		{
			"Plate",
			"Плашка"
		},
		{
			"Player list plate (visible to players with the mod)",
			"Плашка в списке игроков (видна игрокам с модом)"
		},
		{
			"Cycle",
			"Цикл"
		},
		{
			"Pulse",
			"Пульс"
		},
		{
			"Color A",
			"Цвет A"
		},
		{
			"Color B",
			"Цвет B"
		},
		{
			"Preview",
			"Предпросмотр"
		},
		{
			"Players with the mod see your plate in the lobby player list. Others see the default one.",
			"Игроки с модом видят твою плашку в списке игроков лобби. Остальные видят обычную."
		},
		{
			"Language",
			"Язык"
		}
	};
}
}
