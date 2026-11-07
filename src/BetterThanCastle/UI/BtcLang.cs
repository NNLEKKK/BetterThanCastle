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
			"Shoot at the block or object under the cursor (selected archers, not Artificers)",
			"Стрелять по блоку или объекту под курсором (выделенные лучники, кроме артефакторов)"
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
