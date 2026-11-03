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

	private void SaveHatSettings()
	{
		try
		{
			List<string> list = new List<string>(this.hatsOff);
			list.Sort(StringComparer.Ordinal);
			File.WriteAllLines(BtcPaths.Config("hats.txt"), list.ToArray());
		}
		catch (Exception)
		{
		}
	}

	private bool AllHatsOn()
	{
		return this.hatsOff.Count == 0;
	}

	private static void SetStyleColors(GUIStyle st, Color normal, Color hover)
	{
		st.normal.background = null;
		st.hover.background = null;
		st.active.background = null;
		st.focused.background = null;
		st.normal.textColor = normal;
		st.hover.textColor = hover;
		st.active.textColor = hover;
		st.focused.textColor = normal;
	}

	private void DrawCheckbox(Rect box, bool on)
	{
		Color color = GUI.color;
		GUI.color = new Color(1f, 1f, 1f, 0.7f);
		GUI.DrawTexture(box, this.whiteTex);
		GUI.color = new Color(0.06f, 0.06f, 0.08f, 1f);
		GUI.DrawTexture(new Rect(box.x + 2f, box.y + 2f, box.width - 4f, box.height - 4f), this.whiteTex);
		if (on)
		{
			GUI.color = new Color(0.55f, 0.85f, 0.55f, 1f);
			GUI.DrawTexture(new Rect(box.x + 4f, box.y + 4f, box.width - 8f, box.height - 8f), this.whiteTex);
		}
		GUI.color = color;
	}

	private void DrawRowBackground(Event e, Rect row, bool accent, Color old)
	{
		bool flag = e.type != EventType.Repaint;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			bool flag9 = row.Contains(e.mousePosition);
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				GUI.color = new Color(1f, 1f, 1f, 0.08f);
				GUI.DrawTexture(row, this.whiteTex);
			}
			if (accent)
			{
				GUI.color = new Color(0.55f, 0.85f, 0.55f, 1f);
				GUI.DrawTexture(new Rect(row.x, row.y + row.height * 0.2f, 3f, row.height * 0.6f), this.whiteTex);
			}
			GUI.color = old;
		}
	}

	private void DrawPackRows(Event e, float rowWidth, float rowH, Color old)
	{
		int num = this.menuPacks.Count + 1;
		for (int i = 0; i < num; i++)
		{
			string text = (i == 0) ? null : this.menuPacks[i - 1];
			bool flag = string.Equals(text, this.currentPack, StringComparison.OrdinalIgnoreCase);
			Rect rect = new Rect(0f, (float)i * rowH, rowWidth, rowH);
			this.DrawRowBackground(e, rect, flag, old);
			bool flag2 = GUI.Button(rect, text ?? "Default", flag ? this.menuActiveStyle : this.menuItemStyle) && !flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			bool flag9 = flag8;
			if (flag9)
			{
				this.SetPack(text, false);
			}
		}
	}

	private void DrawHatRows(Event e, float rowWidth, float rowH, int fontSize, Color old)
	{
		float num = (float)fontSize * 1.1f;
		bool flag = this.AllHatsOn();
		Rect rect = new Rect(0f, 0f, rowWidth, rowH);
		this.DrawRowBackground(e, rect, false, old);
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
			this.DrawCheckbox(new Rect((float)fontSize, rect.y + (rowH - num) * 0.5f, num, num), flag);
		}
		bool flag10 = GUI.Button(rect, BtcLang.Tr("All types"), this.menuHatItemStyle);
		bool flag11 = flag10;
		bool flag12 = flag11;
		bool flag13 = flag12;
		bool flag14 = flag13;
		bool flag15 = flag14;
		bool flag16 = flag15;
		bool flag17 = flag16;
		if (flag17)
		{
			for (int i = 0; i < ResourcePackManager.HatOrder.Count; i++)
			{
				this.SetTypeHat(ResourcePackManager.HatOrder[i].handle, !flag);
			}
			this.SaveHatSettings();
		}
		for (int j = 0; j < ResourcePackManager.HatOrder.Count; j++)
		{
			ResourcePackManager.HatSpec hatSpec = ResourcePackManager.HatOrder[j];
			bool flag18 = !this.hatsOff.Contains(hatSpec.handle);
			Rect rect2 = new Rect(0f, (float)(j + 1) * rowH, rowWidth, rowH);
			this.DrawRowBackground(e, rect2, false, old);
			bool flag19 = e.type == EventType.Repaint;
			bool flag20 = flag19;
			bool flag21 = flag20;
			bool flag22 = flag21;
			bool flag23 = flag22;
			bool flag24 = flag23;
			bool flag25 = flag24;
			bool flag26 = flag25;
			if (flag26)
			{
				this.DrawCheckbox(new Rect((float)fontSize, rect2.y + (rowH - num) * 0.5f, num, num), flag18);
			}
			bool flag27 = GUI.Button(rect2, hatSpec.label, this.menuHatItemStyle);
			bool flag28 = flag27;
			bool flag29 = flag28;
			bool flag30 = flag29;
			bool flag31 = flag30;
			bool flag32 = flag31;
			bool flag33 = flag32;
			bool flag34 = flag33;
			if (flag34)
			{
				this.SetTypeHat(hatSpec.handle, !flag18);
				this.SaveHatSettings();
			}
		}
	}

	static ResourcePackManager()
	{
		ResourcePackManager.HatOrder = new List<ResourcePackManager.HatSpec>();
		ResourcePackManager.HatSpecs = ResourcePackManager.BuildHatSpecs();
		ResourcePackManager.DefaultHpConfig = new string[]
		{
			"# Настройки Colored HP. Файл перечитывается автоматически, пока игра запущена и Colored HP включён - перезаходить не нужно.",
			"# Комментарии: строки, начинающиеся с # или ;, а также всё после // или ; в конце строки.",
			"# Цвет: #RRGGBB (например #33CC40) или R,G,B числами 0-255 (например 51,204,64). Можно добавить прозрачность: #RRGGBBAA или R,G,B,A.",
			"# Если удалить файл - при следующем запуске он создастся заново со значениями по умолчанию.",
			"",
			"[General]",
			"# Во сколько раз второй цвет дуги (Color2) темнее основного. 1 = такой же, 0.75 = на четверть темнее, 0 = чёрный.",
			"SecondColorBrightness = 0.75",
			"# Красить ли HP-дугу (кольцо 1): Rings = 1 - да, none - нет. Кольцо 2 теперь настраивается отдельно: StaminaColor / AmmoColor ниже.",
			"Rings = 1",
			"# Красить ли вражеские кольца. false - у врагов всё как в ванили: ни палитра, ни [EnemyOverrides] не применяются. true - красим по настройкам ниже.",
			"ColorEnemies = false",
			"# Always show: всегда показывать кольца (HP-дуга, дуга выносливости, обычные и покрашенные) у ВСЕХ юнитов: своих (и союзных) / вражеских,",
			"# даже когда юнит не выделен и не ранен. Вражеские кольца в тумане войны не показываются.",
			"AlwaysShowOwn = false",
			"AlwaysShowEnemy = false",
			"# Использовать отдельную палитру [Enemy] для вражеских юнитов (true/false). Если false - у всех палитра [Own].",
			"EnemyPalette = true",
			"# Кольцо 2. StaminaColor - красить дугу выносливости рыцаря палитрами [StaminaOwn]/[StaminaEnemy]; AmmoColor - красить дугу боезапаса алхимика палитрой [Ammo].",
			"StaminaColor = true",
			"AmmoColor = true",
			"StaminaSecondColorBrightness = 0.75",
			"AmmoSecondColorBrightness = 0.75",
			"# StaminaKnightOnly - дуга выносливости только у рыцаря (у алхимика - боезапас, у всех остальных скрыта). Враждебного алхимика мы никогда не показываем.",
			"StaminaKnightOnly = true",
			"# Алхимик: AmmoRing - показывать дугу боезапаса; AmmoNumber - цифра с числом бомб (0-10) у своих/союзных алхимиков.",
			"AmmoRing = true",
			"AmmoNumber = false",
			"AmmoNumberSize = 1",
			"AmmoNumberOffsetX = 0      // пиксели (для экрана высотой 1080): вправо",
			"AmmoNumberOffsetY = -20    // пиксели: вверх (минус - вниз)",
			"AmmoNumberColor = #FFFFFF  // можно с прозрачностью: #RRGGBBAA",
			"AmmoNumberColorByAmmo = false",
			"AmmoNumberOutline = true",
			"AmmoNumberBold = true",
			"# SelectedShift: у выделенного (не вражеского) юнита цвета колец светлее (+) или темнее (-): от -0.6 до 0.6. SelectedShiftEnabled - включить.",
			"SelectedShiftEnabled = false",
			"SelectedShift = 0.25",
			"# Слой аниматора кольца, в котором лежит цвет кольца, и состояние этого слоя, означающее врага (в ванили красное кольцо).",
			"# Своё кольцо в ванили - ColorMain_Yellow, враг - ColorMain_Red; можно указать другое состояние, например ColorMain_Blue.",
			"ColorLayer = MainColor",
			"EnemyState = ColorMain_Red",
			"# Как часто (секунды) искать новые кольца под юнитами. Больше - меньше нагрузка, но новые кольца красятся позже.",
			"ScanIntervalSeconds = 15",
			"# Как часто (секунды) проверять, не изменился ли этот файл (проверяется только дата изменения, это почти бесплатно).",
			"ConfigCheckSeconds = 2",
			"",
			"# Палитры. Строка \"процент = цвет\": цвет действует, когда HP юнита >= процента. Строк может быть сколько угодно, порядок любой.",
			"# Ниже самого малого процента действует цвет с самым малым процентом.",
			"",
			"[Own]",
			"70 = #33CC40    // 100-70% - зелёный",
			"50 = #F2D926    // 70-50% - жёлтый",
			"30 = #FF8C1A    // 50-30% - оранжевый",
			"10 = #E6261F    // 30-10% - красный",
			"0 = #800D0D     // 10-0% - тёмно-красный",
			"",
			"[Enemy]",
			"70 = #1AD9F2    // 100-70% - циан",
			"50 = #4080FF    // 70-50% - синий",
			"30 = #994DF2    // 50-30% - фиолетовый",
			"10 = #F233BF    // 30-10% - пурпурный",
			"0 = #590D66     // 10-0% - тёмно-фиолетовый",
			"",
			"# Палитры кольца 2: [StaminaOwn] / [StaminaEnemy] - выносливость рыцаря (% от максимума), [Ammo] - боезапас алхимика (10 бомб = 100%).",
			"",
			"[StaminaOwn]",
			"70 = #4DB8FF",
			"40 = #3D8BFF",
			"15 = #7A5CE6",
			"0 = #4B2A8F",
			"",
			"[StaminaEnemy]",
			"70 = #FFA64D",
			"40 = #E67A1F",
			"15 = #B8501A",
			"0 = #8F4A12",
			"",
			"[Ammo]",
			"70 = #FFB020",
			"40 = #FF7A1A",
			"20 = #E6401F",
			"0 = #801F0F",
			"",
			"# Переопределения свойств шейдера кольца: отдельно для своих и для врагов. Это способ сделать врага заметнее (обводка, толщина).",
			"# Строка \"_Свойство = значение\". Значение: число (толщина и т.п.), xN (умножить исходное значение на N) или цвет (#RRGGBB / R,G,B).",
			"# Известные свойства кольца: _Ring1Color, _Ring1Color2, _Ring1Thickness, _Ring1Width, _Ring2Color, _Ring2Color2, _Ring2Thickness,",
			"# _Ring2Width, _BaseThickness, _Radius, _OccludedOpacity. Что именно делает каждое - подбирается пробой: меняйте и смотрите в игре.",
			"# Свойство, которого нет у шейдера, просто пропускается. Чтобы отключить строку, поставьте перед ней #.",
			"",
			"[OwnOverrides]",
			"",
			"[EnemyOverrides]",
			"_Ring1Thickness = x1.5     // толще HP-дуга врага",
			"_Ring2Color = #590D0D      // тёмно-красный второй контур",
			"_Ring2Color2 = #590D0D"
		};
		ResourcePackManager.regenTexts = new string[1201];
		ResourcePackManager.BarrelDamageTable = new int[,]
		{
			{
				0,
				1601
			},
			{
				1,
				1289
			},
			{
				2,
				909
			},
			{
				4,
				680
			},
			{
				5,
				619
			},
			{
				8,
				469
			},
			{
				9,
				417
			},
			{
				10,
				380
			},
			{
				13,
				257
			},
			{
				16,
				159
			},
			{
				17,
				154
			},
			{
				18,
				149
			},
			{
				20,
				140
			},
			{
				25,
				120
			},
			{
				26,
				116
			},
			{
				29,
				105
			},
			{
				32,
				94
			},
			{
				34,
				87
			},
			{
				36,
				81
			},
			{
				37,
				78
			},
			{
				40,
				68
			},
			{
				41,
				65
			},
			{
				45,
				57
			},
			{
				49,
				42
			},
			{
				50,
				39
			},
			{
				52,
				34
			},
			{
				53,
				31
			},
			{
				58,
				18
			},
			{
				61,
				10
			},
			{
				65,
				0
			}
		};
		ResourcePackManager.EnemyTexProps = new string[]
		{
			"_MainTex",
			"_Diffuse",
			"_DiffuseMap",
			"_BaseMap",
			"_Albedo",
			"_Tex",
			"_Texture"
		};
		ResourcePackManager.HpKnownFloatProps = new string[]
		{
			"_Ring1Thickness",
			"_Ring1Width",
			"_Ring2Thickness",
			"_Ring2Width",
			"_BaseThickness",
			"_Radius",
			"_OccludedOpacity"
		};
		ResourcePackManager.HpKnownColorProps = new string[]
		{
			"_Ring1Color",
			"_Ring1Color2",
			"_Ring2Color",
			"_Ring2Color2"
		};
	}

	private static Dictionary<string, LuaStaticDriver> PeekStatics()
	{
		FieldInfo field = typeof(LuaDriverHQ).GetField("_allStatics", BindingFlags.Static | BindingFlags.NonPublic);
		return (field != null) ? (field.GetValue(null) as Dictionary<string, LuaStaticDriver>) : null;
	}

	private static T FindIn<T>(GameObject go) where T : Component
	{
		T component = go.GetComponent<T>();
		return (component != null) ? component : go.GetComponentInChildren<T>(true);
	}

	private bool ApplyHats(List<string> types)
	{
		bool flag = true;
		try
		{
			List<ResourcePackManager.HatSpec> list = new List<ResourcePackManager.HatSpec>();
			for (int i = 0; i < ResourcePackManager.HatOrder.Count; i++)
			{
				bool flag2 = types == null || types.Contains(ResourcePackManager.HatOrder[i].handle);
				bool flag3 = flag2;
				bool flag4 = flag3;
				bool flag5 = flag4;
				bool flag6 = flag5;
				bool flag7 = flag6;
				bool flag8 = flag7;
				bool flag9 = flag8;
				if (flag9)
				{
					list.Add(ResourcePackManager.HatOrder[i]);
				}
			}
			Dictionary<string, LuaStaticDriver> dictionary = ResourcePackManager.PeekStatics();
			Dictionary<string, ResourcePackManager.HatSpec> dictionary2 = new Dictionary<string, ResourcePackManager.HatSpec>();
			for (int j = 0; j < list.Count; j++)
			{
				ResourcePackManager.HatSpec hatSpec = list[j];
				bool flag10 = !this.hatsOff.Contains(hatSpec.handle);
				ResourcePackManager.HatSet hatSet = flag10 ? this.GetHatSet(hatSpec) : null;
				bool flag11 = flag10 && hatSet == null;
				bool flag12 = !flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				bool flag17 = flag16;
				bool flag18 = flag17;
				if (flag18)
				{
					ResourcePackManager.HatSet hatSet2 = hatSet;
					bool flag19 = hatSet2 == null;
					bool flag20 = flag19;
					bool flag21 = flag20;
					bool flag22 = flag21;
					bool flag23 = flag22;
					bool flag24 = flag23;
					bool flag25 = flag24;
					bool flag26 = flag25;
					if (flag26)
					{
						this.hatSets.TryGetValue(hatSpec.handle, out hatSet2);
					}
					ResourcePackManager.HatVanilla hatVanilla;
					bool flag27 = !this.hatVanilla.TryGetValue(hatSpec.handle, out hatVanilla);
					bool flag28 = flag27;
					bool flag29 = flag28;
					bool flag30 = flag29;
					bool flag31 = flag30;
					bool flag32 = flag31;
					bool flag33 = flag32;
					bool flag34 = flag33;
					if (flag34)
					{
						hatVanilla = new ResourcePackManager.HatVanilla();
						this.hatVanilla[hatSpec.handle] = hatVanilla;
					}
					GameObject gameObject = Factory.Peek(new Factory.AssetKey("Visuals", hatSpec.visual));
					MeshFilter meshFilter = (gameObject != null) ? gameObject.GetComponentInChildren<MeshFilter>() : null;
					MeshRenderer meshRenderer = (gameObject != null) ? gameObject.GetComponentInChildren<MeshRenderer>() : null;
					LuaStaticDriver luaStaticDriver = null;
					bool flag35 = dictionary != null;
					bool flag36 = flag35;
					bool flag37 = flag36;
					bool flag38 = flag37;
					bool flag39 = flag38;
					bool flag40 = flag39;
					bool flag41 = flag40;
					bool flag42 = flag41;
					if (flag42)
					{
						dictionary.TryGetValue(hatSpec.handle, out luaStaticDriver);
					}
					Grip grip = (luaStaticDriver != null && luaStaticDriver.baseDriver != null) ? ResourcePackManager.FindIn<Grip>(luaStaticDriver.baseDriver.gameObject) : null;
					bool flag43 = meshFilter == null || meshRenderer == null;
					bool flag44 = flag43;
					bool flag45 = flag44;
					bool flag46 = flag45;
					bool flag47 = flag46;
					bool flag48 = flag47;
					bool flag49 = flag48;
					bool flag50 = flag49;
					if (flag50)
					{
						flag = false;
					}
					bool flag51 = meshFilter != null && (hatSet2 == null || meshFilter.sharedMesh != hatSet2.mesh) && meshFilter.sharedMesh != null;
					bool flag52 = flag51;
					bool flag53 = flag52;
					bool flag54 = flag53;
					bool flag55 = flag54;
					bool flag56 = flag55;
					bool flag57 = flag56;
					bool flag58 = flag57;
					if (flag58)
					{
						hatVanilla.mesh = meshFilter.sharedMesh;
					}
					bool flag59 = meshRenderer != null && (hatSet2 == null || meshRenderer.sharedMaterial != hatSet2.mat) && meshRenderer.sharedMaterial != null;
					bool flag60 = flag59;
					bool flag61 = flag60;
					bool flag62 = flag61;
					bool flag63 = flag62;
					bool flag64 = flag63;
					bool flag65 = flag64;
					bool flag66 = flag65;
					if (flag66)
					{
						hatVanilla.mat = meshRenderer.sharedMaterial;
					}
					bool flag67 = grip != null && (hatSet2 == null || grip.gripMatrix != hatSet2.grip);
					bool flag68 = flag67;
					bool flag69 = flag68;
					bool flag70 = flag69;
					bool flag71 = flag70;
					bool flag72 = flag71;
					bool flag73 = flag72;
					bool flag74 = flag73;
					if (flag74)
					{
						hatVanilla.grip = grip.gripMatrix;
						hatVanilla.hasGrip = true;
					}
					bool flag75 = meshFilter != null && meshRenderer != null;
					bool flag76 = flag75;
					bool flag77 = flag76;
					bool flag78 = flag77;
					bool flag79 = flag78;
					bool flag80 = flag79;
					bool flag81 = flag80;
					bool flag82 = flag81;
					if (flag82)
					{
						Mesh mesh = flag10 ? hatSet.mesh : hatVanilla.mesh;
						Material material = flag10 ? hatSet.mat : hatVanilla.mat;
						bool flag83 = mesh != null && meshFilter.sharedMesh != mesh;
						bool flag84 = flag83;
						bool flag85 = flag84;
						bool flag86 = flag85;
						bool flag87 = flag86;
						bool flag88 = flag87;
						bool flag89 = flag88;
						bool flag90 = flag89;
						if (flag90)
						{
							meshFilter.sharedMesh = mesh;
						}
						bool flag91 = material != null && meshRenderer.sharedMaterial != material;
						bool flag92 = flag91;
						bool flag93 = flag92;
						bool flag94 = flag93;
						bool flag95 = flag94;
						bool flag96 = flag95;
						bool flag97 = flag96;
						bool flag98 = flag97;
						if (flag98)
						{
							meshRenderer.sharedMaterial = material;
						}
					}
					bool flag99 = grip != null && (flag10 || hatVanilla.hasGrip);
					bool flag100 = flag99;
					bool flag101 = flag100;
					bool flag102 = flag101;
					bool flag103 = flag102;
					bool flag104 = flag103;
					bool flag105 = flag104;
					bool flag106 = flag105;
					if (flag106)
					{
						grip.gripMatrix = (flag10 ? hatSet.grip : hatVanilla.grip);
					}
					dictionary2[hatSpec.handle] = hatSpec;
				}
			}
			bool flag107 = dictionary2.Count > 0;
			bool flag108 = flag107;
			bool flag109 = flag108;
			bool flag110 = flag109;
			bool flag111 = flag110;
			bool flag112 = flag111;
			bool flag113 = flag112;
			bool flag114 = flag113;
			if (flag114)
			{
				int num = 0;
				foreach (LuaDriver luaDriver in Resources.FindObjectsOfTypeAll<LuaDriver>())
				{
					ResourcePackManager.HatSpec spec = null;
					bool flag115 = luaDriver == null || luaDriver.Handle == null || !dictionary2.TryGetValue(luaDriver.Handle, out spec);
					bool flag116 = !flag115;
					bool flag117 = flag116;
					bool flag118 = flag117;
					bool flag119 = flag118;
					bool flag120 = flag119;
					bool flag121 = flag120;
					bool flag122 = flag121;
					if (flag122)
					{
						bool flag123 = this.ApplyToHatObject(luaDriver.gameObject, spec);
						bool flag124 = flag123;
						bool flag125 = flag124;
						bool flag126 = flag125;
						bool flag127 = flag126;
						bool flag128 = flag127;
						bool flag129 = flag128;
						bool flag130 = flag129;
						if (flag130)
						{
							num++;
						}
					}
				}
				ResourcePackManager.PackLog(string.Concat(new object[]
				{
					"[ResourcePacks] Hats applied (",
					dictionary2.Count,
					" types, ",
					num,
					" existing items updated)",
					flag ? string.Empty : ", factories not ready yet"
				}));
			}
		}
		catch (Exception arg)
		{
			ResourcePackManager.PackWarn("[ResourcePacks] Hat apply failed: " + arg);
			return true;
		}
		return flag;
	}

	private bool ApplyToHatObject(GameObject hat, ResourcePackManager.HatSpec spec)
	{
		bool flag = !this.hatsOff.Contains(spec.handle);
		ResourcePackManager.HatSet hatSet;
		this.hatSets.TryGetValue(spec.handle, out hatSet);
		ResourcePackManager.HatVanilla hatVanilla;
		this.hatVanilla.TryGetValue(spec.handle, out hatVanilla);
		MeshFilter componentInChildren = hat.GetComponentInChildren<MeshFilter>(true);
		MeshRenderer componentInChildren2 = hat.GetComponentInChildren<MeshRenderer>(true);
		bool flag2 = componentInChildren == null || componentInChildren2 == null || hatVanilla == null || (flag && hatSet == null);
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		bool flag9 = flag8;
		bool result;
		if (flag9)
		{
			result = false;
		}
		else
		{
			bool flag10 = hatVanilla.mesh == null && componentInChildren.sharedMesh != null && (hatSet == null || componentInChildren.sharedMesh != hatSet.mesh);
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			bool flag17 = flag16;
			if (flag17)
			{
				hatVanilla.mesh = componentInChildren.sharedMesh;
			}
			bool flag18 = hatVanilla.mat == null && componentInChildren2.sharedMaterial != null && (hatSet == null || componentInChildren2.sharedMaterial != hatSet.mat);
			bool flag19 = flag18;
			bool flag20 = flag19;
			bool flag21 = flag20;
			bool flag22 = flag21;
			bool flag23 = flag22;
			bool flag24 = flag23;
			bool flag25 = flag24;
			if (flag25)
			{
				hatVanilla.mat = componentInChildren2.sharedMaterial;
			}
			Grip grip = ResourcePackManager.FindIn<Grip>(hat);
			bool flag26 = grip != null && !hatVanilla.hasGrip && (hatSet == null || grip.gripMatrix != hatSet.grip);
			bool flag27 = flag26;
			bool flag28 = flag27;
			bool flag29 = flag28;
			bool flag30 = flag29;
			bool flag31 = flag30;
			bool flag32 = flag31;
			bool flag33 = flag32;
			if (flag33)
			{
				hatVanilla.grip = grip.gripMatrix;
				hatVanilla.hasGrip = true;
			}
			Mesh mesh = flag ? hatSet.mesh : hatVanilla.mesh;
			Material material = flag ? hatSet.mat : hatVanilla.mat;
			bool flag34 = false;
			bool flag35 = mesh != null && componentInChildren.sharedMesh != mesh;
			bool flag36 = flag35;
			bool flag37 = flag36;
			bool flag38 = flag37;
			bool flag39 = flag38;
			bool flag40 = flag39;
			bool flag41 = flag40;
			bool flag42 = flag41;
			if (flag42)
			{
				componentInChildren.sharedMesh = mesh;
				flag34 = true;
			}
			bool flag43 = material != null && componentInChildren2.sharedMaterial != material;
			bool flag44 = flag43;
			bool flag45 = flag44;
			bool flag46 = flag45;
			bool flag47 = flag46;
			bool flag48 = flag47;
			bool flag49 = flag48;
			bool flag50 = flag49;
			if (flag50)
			{
				componentInChildren2.sharedMaterial = material;
				flag34 = true;
			}
			bool flag51 = grip != null && (flag || hatVanilla.hasGrip);
			bool flag52 = flag51;
			bool flag53 = flag52;
			bool flag54 = flag53;
			bool flag55 = flag54;
			bool flag56 = flag55;
			bool flag57 = flag56;
			bool flag58 = flag57;
			if (flag58)
			{
				Matrix4x4 matrix4x = flag ? hatSet.grip : hatVanilla.grip;
				bool flag59 = grip.gripMatrix != matrix4x;
				bool flag60 = flag59;
				bool flag61 = flag60;
				bool flag62 = flag61;
				bool flag63 = flag62;
				bool flag64 = flag63;
				bool flag65 = flag64;
				bool flag66 = flag65;
				if (flag66)
				{
					grip.gripMatrix = matrix4x;
					flag34 = true;
					bool flag67 = !grip.collapseLimb;
					bool flag68 = flag67;
					bool flag69 = flag68;
					bool flag70 = flag69;
					bool flag71 = flag70;
					bool flag72 = flag71;
					bool flag73 = flag72;
					bool flag74 = flag73;
					if (flag74)
					{
						grip.TryAlignToSocket();
					}
				}
			}
			result = flag34;
		}
		return result;
	}

	private void SetTypeHat(string handle, bool halloween)
	{
		bool flag = !this.hatsOff.Contains(handle);
		bool flag2 = flag == halloween;
		bool flag3 = !flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		bool flag9 = flag8;
		if (flag9)
		{
			if (halloween)
			{
				this.hatsOff.Remove(handle);
			}
			else
			{
				this.hatsOff.Add(handle);
			}
			bool flag10 = !this.hatsPending.Contains(handle);
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			bool flag17 = flag16;
			if (flag17)
			{
				this.hatsPending.Add(handle);
			}
		}
	}

	private void LoadOptions()
	{
		this.coloredHp = false;
		this.enemyTexOn = false;
		this.discordOn = false;
		this.dayBright = 1f;
		this.nightBright = 1f;
		try
		{
			string path = BtcPaths.Config("options.txt");
			bool flag = File.Exists(path);
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				string[] array = File.ReadAllLines(path);
				for (int i = 0; i < array.Length; i++)
				{
					string text = array[i].Trim();
					bool flag9 = text.StartsWith("ColoredHP=", StringComparison.OrdinalIgnoreCase);
					bool flag10 = flag9;
					bool flag11 = flag10;
					bool flag12 = flag11;
					bool flag13 = flag12;
					bool flag14 = flag13;
					bool flag15 = flag14;
					bool flag16 = flag15;
					if (flag16)
					{
						this.coloredHp = text.EndsWith("1");
					}
					else
					{
						bool flag17 = text.StartsWith("EnemyTex=", StringComparison.OrdinalIgnoreCase);
						bool flag18 = flag17;
						bool flag19 = flag18;
						bool flag20 = flag19;
						bool flag21 = flag20;
						bool flag22 = flag21;
						bool flag23 = flag22;
						bool flag24 = flag23;
						if (flag24)
						{
							this.enemyTexOn = text.EndsWith("1");
						}
						else
						{
							bool flag25 = text.StartsWith("DiscordRPC=", StringComparison.OrdinalIgnoreCase);
							bool flag26 = flag25;
							bool flag27 = flag26;
							bool flag28 = flag27;
							bool flag29 = flag28;
							bool flag30 = flag29;
							bool flag31 = flag30;
							bool flag32 = flag31;
							if (flag32)
							{
								this.discordOn = text.EndsWith("1");
							}
							else
							{
								bool flag33 = text.StartsWith("DayBrightness=", StringComparison.OrdinalIgnoreCase);
								bool flag34 = flag33;
								bool flag35 = flag34;
								bool flag36 = flag35;
								bool flag37 = flag36;
								bool flag38 = flag37;
								bool flag39 = flag38;
								bool flag40 = flag39;
								if (flag40)
								{
									this.dayBright = ResourcePackManager.ParseBright(text);
								}
								else
								{
									bool flag41 = text.StartsWith("NightBrightness=", StringComparison.OrdinalIgnoreCase);
									bool flag42 = flag41;
									bool flag43 = flag42;
									bool flag44 = flag43;
									bool flag45 = flag44;
									bool flag46 = flag45;
									bool flag47 = flag46;
									bool flag48 = flag47;
									if (flag48)
									{
										this.nightBright = ResourcePackManager.ParseBright(text);
									}
								}
							}
						}
					}
				}
			}
		}
		catch (Exception ex)
		{
			ResourcePackManager.PackWarn("[ResourcePacks] Cannot read options.txt: " + ex.Message);
		}
	}

	private void SaveOptions()
	{
		try
		{
			File.WriteAllLines(BtcPaths.Config("options.txt"), new string[]
			{
				"ColoredHP=" + (this.coloredHp ? "1" : "0"),
				"EnemyTex=" + (this.enemyTexOn ? "1" : "0"),
				"DiscordRPC=" + (this.discordOn ? "1" : "0"),
				"DayBrightness=" + this.dayBright.ToString("0.00", CultureInfo.InvariantCulture),
				"NightBrightness=" + this.nightBright.ToString("0.00", CultureInfo.InvariantCulture)
			});
		}
		catch (Exception)
		{
		}
	}

	private void SetColoredHp(bool on)
	{
		bool flag = this.coloredHp == on;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			this.coloredHp = on;
			this.SaveOptions();
			if (on)
			{
				this.hpNextScan = 0f;
				this.hpNextCfgCheck = 0f;
			}
			else
			{
				this.RestoreHpRings();
			}
		}
	}

	private void LateUpdate()
	{
		bool flag = this.hpCfg == null || (!this.coloredHp && !this.AlwaysOwnNow(this.hpCfg) && !this.AlwaysEnemyNow(this.hpCfg) && this.hpForcedCount <= 0);
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			float unscaledTime = Time.unscaledTime;
			bool flag9 = unscaledTime >= this.hpNextCfgCheck;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				this.hpNextCfgCheck = unscaledTime + this.hpCfg.checkInterval;
				try
				{
					string hpConfigPath = ResourcePackManager.HpConfigPath;
					bool flag17 = !File.Exists(hpConfigPath) || File.GetLastWriteTimeUtc(hpConfigPath) != this.hpCfgStamp;
					bool flag18 = flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					bool flag21 = flag20;
					bool flag22 = flag21;
					bool flag23 = flag22;
					bool flag24 = flag23;
					if (flag24)
					{
						this.ReloadHpConfig(true);
					}
				}
				catch (Exception)
				{
				}
			}
			bool flag25 = unscaledTime >= this.hpNextScan;
			bool flag26 = flag25;
			bool flag27 = flag26;
			bool flag28 = flag27;
			bool flag29 = flag28;
			bool flag30 = flag29;
			bool flag31 = flag30;
			bool flag32 = flag31;
			if (flag32)
			{
				this.hpNextScan = unscaledTime + this.hpCfg.scanInterval;
				this.ScanHpRings();
			}
			for (int i = this.hpRings.Count - 1; i >= 0; i--)
			{
				ResourcePackManager.HpRing hpRing = this.hpRings[i];
				bool flag33 = hpRing.owner == null || hpRing.rend == null || hpRing.anim == null;
				bool flag34 = flag33;
				bool flag35 = flag34;
				bool flag36 = flag35;
				bool flag37 = flag36;
				bool flag38 = flag37;
				bool flag39 = flag38;
				bool flag40 = flag39;
				if (flag40)
				{
					this.hpRingIds.Remove((hpRing.owner != null) ? hpRing.owner.GetInstanceID() : 0);
					this.hpRings.RemoveAt(i);
				}
				else
				{
					bool flag41 = !hpRing.rend.enabled || !hpRing.rend.gameObject.activeInHierarchy;
					bool flag42 = !flag41;
					bool flag43 = flag42;
					bool flag44 = flag43;
					bool flag45 = flag44;
					bool flag46 = flag45;
					bool flag47 = flag46;
					bool flag48 = flag47;
					if (flag48)
					{
						bool flag49 = this.coloredHp;
						bool flag50 = flag49;
						bool flag51 = flag50;
						bool flag52 = flag51;
						bool flag53 = flag52;
						bool flag54 = flag53;
						bool flag55 = flag54;
						bool flag56 = flag55;
						if (flag56)
						{
							this.PaintRing(hpRing, hpRing.anim.GetFloat(ResourcePackManager.hpParamId));
						}
					}
				}
			}
		}
	}

	private static bool InitHpReflection()
	{
		bool flag = ResourcePackManager.circleRendField != null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		bool result;
		if (flag8)
		{
			result = (ResourcePackManager.circleAnimField != null);
		}
		else
		{
			BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.NonPublic;
			ResourcePackManager.circleRendField = typeof(SelectionCircle).GetField("_renderer", bindingAttr);
			ResourcePackManager.circleAnimField = typeof(SelectionCircle).GetField("animator", bindingAttr);
			ResourcePackManager.hpParamId = Animator.StringToHash("HP");
			ResourcePackManager.hpShowHpId = Animator.StringToHash("ShowHP");
			ResourcePackManager.hpShowStamId = Animator.StringToHash("ShowStam");
			ResourcePackManager.hpStamId = Animator.StringToHash("Stam");
			ResourcePackManager.hpSelectedId = Animator.StringToHash("Selected");
			ResourcePackManager.hpMainColorId = Animator.StringToHash("MainColor");
			ResourcePackManager.ringPropIds = new int[ResourcePackManager.RingColorProps.Length];
			for (int i = 0; i < ResourcePackManager.ringPropIds.Length; i++)
			{
				ResourcePackManager.ringPropIds[i] = Shader.PropertyToID(ResourcePackManager.RingColorProps[i]);
			}
			bool flag9 = ResourcePackManager.circleRendField == null || ResourcePackManager.circleAnimField == null;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				ResourcePackManager.PackWarn("[ResourcePacks] Colored HP: SelectionCircle fields not found (game version differs?)");
				ResourcePackManager.circleRendField = null;
				result = false;
			}
			else
			{
				result = true;
			}
		}
		return result;
	}

	private void ScanHpRings()
	{
		bool flag = !ResourcePackManager.InitHpReflection();
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			SelectionCircle[] array = Resources.FindObjectsOfTypeAll<SelectionCircle>();
			int num = 0;
			foreach (SelectionCircle selectionCircle in array)
			{
				bool flag9 = selectionCircle == null || !selectionCircle.gameObject.scene.IsValid();
				bool flag10 = !flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					bool flag17 = this.TryRegisterRing(selectionCircle);
					bool flag18 = flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					bool flag21 = flag20;
					bool flag22 = flag21;
					bool flag23 = flag22;
					bool flag24 = flag23;
					if (flag24)
					{
						num++;
					}
				}
			}
			bool flag25 = num > 0;
			bool flag26 = flag25;
			bool flag27 = flag26;
			bool flag28 = flag27;
			bool flag29 = flag28;
			bool flag30 = flag29;
			bool flag31 = flag30;
			bool flag32 = flag31;
			if (flag32)
			{
				ResourcePackManager.PackLog(string.Concat(new object[]
				{
					"[ResourcePacks] Colored HP: ",
					num,
					" new ring(s), ",
					this.hpRings.Count,
					" total"
				}));
			}
		}
	}

	private void RestoreHpRings()
	{
		for (int i = 0; i < this.hpRings.Count; i++)
		{
			ResourcePackManager.HpRing hpRing = this.hpRings[i];
			bool flag = hpRing.owner == null || hpRing.rend == null;
			bool flag2 = !flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				hpRing.rend.GetPropertyBlock(hpRing.block);
				for (int j = 0; j < ResourcePackManager.ringPropIds.Length; j++)
				{
					hpRing.block.SetColor(ResourcePackManager.ringPropIds[j], hpRing.originals[j]);
				}
				bool flag9 = this.hpCfg != null;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					ResourcePackManager.ResetRingOverrides(hpRing, this.hpCfg);
				}
				hpRing.team = -1;
				hpRing.ring2Painted = false;
				hpRing.rend.SetPropertyBlock(hpRing.block);
			}
		}
	}

	private static bool TryParseHpColor(string text, out Color c)
	{
		c = Color.white;
		text = text.Trim();
		bool flag = text.Length == 0;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		bool result;
		if (flag8)
		{
			result = false;
		}
		else
		{
			int[] array = new int[]
			{
				0,
				0,
				0,
				255
			};
			bool flag9 = text.IndexOf(',') >= 0;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				string[] array2 = text.Split(new char[]
				{
					','
				});
				bool flag17 = array2.Length < 3 || array2.Length > 4;
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				if (flag24)
				{
					return false;
				}
				for (int i = 0; i < array2.Length; i++)
				{
					bool flag25 = !int.TryParse(array2[i].Trim(), out array[i]);
					bool flag26 = flag25;
					bool flag27 = flag26;
					bool flag28 = flag27;
					bool flag29 = flag28;
					bool flag30 = flag29;
					bool flag31 = flag30;
					bool flag32 = flag31;
					if (flag32)
					{
						return false;
					}
				}
			}
			else
			{
				bool flag33 = text[0] == '#';
				bool flag34 = flag33;
				bool flag35 = flag34;
				bool flag36 = flag35;
				bool flag37 = flag36;
				bool flag38 = flag37;
				bool flag39 = flag38;
				bool flag40 = flag39;
				if (flag40)
				{
					text = text.Substring(1);
				}
				bool flag41 = text.Length != 6 && text.Length != 8;
				bool flag42 = flag41;
				bool flag43 = flag42;
				bool flag44 = flag43;
				bool flag45 = flag44;
				bool flag46 = flag45;
				bool flag47 = flag46;
				bool flag48 = flag47;
				if (flag48)
				{
					return false;
				}
				for (int j = 0; j < text.Length / 2; j++)
				{
					bool flag49 = !int.TryParse(text.Substring(j * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out array[j]);
					bool flag50 = flag49;
					bool flag51 = flag50;
					bool flag52 = flag51;
					bool flag53 = flag52;
					bool flag54 = flag53;
					bool flag55 = flag54;
					bool flag56 = flag55;
					if (flag56)
					{
						return false;
					}
				}
			}
			c = new Color((float)Mathf.Clamp(array[0], 0, 255) / 255f, (float)Mathf.Clamp(array[1], 0, 255) / 255f, (float)Mathf.Clamp(array[2], 0, 255) / 255f, (float)Mathf.Clamp(array[3], 0, 255) / 255f);
			result = true;
		}
		return result;
	}

	private static bool TryParseHpBool(string text, out bool value)
	{
		text = text.Trim().ToLowerInvariant();
		value = (text == "true" || text == "1" || text == "yes" || text == "on" || text == "да");
		return value || text == "false" || text == "0" || text == "no" || text == "off" || text == "нет";
	}

	private static string StripHpComment(string line)
	{
		string text = line.Trim();
		bool flag = text.Length == 0 || text[0] == '#' || text[0] == ';';
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		string result;
		if (flag8)
		{
			result = string.Empty;
		}
		else
		{
			int num = text.IndexOf("//", StringComparison.Ordinal);
			int num2 = text.IndexOf(';');
			bool flag9 = num < 0 || (num2 >= 0 && num2 < num);
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				num = num2;
			}
			result = ((num >= 0) ? text.Substring(0, num).Trim() : text);
		}
		return result;
	}

	private static float ParseHpFloat(string text, float fallback, float min, float max)
	{
		float value;
		bool flag = float.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		float result;
		if (flag8)
		{
			result = Mathf.Clamp(value, min, max);
		}
		else
		{
			result = fallback;
		}
		return result;
	}

	private static void ApplyHpConfigLines(string[] lines, ResourcePackManager.HpConfig cfg, List<string> problems)
	{
		string text = string.Empty;
		List<KeyValuePair<float, Color>>[] array = new List<KeyValuePair<float, Color>>[7];
		for (int i = 0; i < lines.Length; i++)
		{
			string text2 = ResourcePackManager.StripHpComment(lines[i]);
			bool flag = text2.Length == 0;
			bool flag2 = !flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				bool flag9 = text2[0] == '[' && text2[text2.Length - 1] == ']';
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					text = text2.Substring(1, text2.Length - 2).Trim().ToLowerInvariant();
					bool flag17 = text == "ownoverrides";
					bool flag18 = flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					bool flag21 = flag20;
					bool flag22 = flag21;
					bool flag23 = flag22;
					bool flag24 = flag23;
					if (flag24)
					{
						cfg.ownOv.Clear();
					}
					else
					{
						bool flag25 = text == "enemyoverrides";
						bool flag26 = flag25;
						bool flag27 = flag26;
						bool flag28 = flag27;
						bool flag29 = flag28;
						bool flag30 = flag29;
						bool flag31 = flag30;
						bool flag32 = flag31;
						if (flag32)
						{
							cfg.enemyOv.Clear();
						}
					}
				}
				else
				{
					int num = text2.IndexOf('=');
					bool flag33 = num <= 0;
					bool flag34 = flag33;
					bool flag35 = flag34;
					bool flag36 = flag35;
					bool flag37 = flag36;
					bool flag38 = flag37;
					bool flag39 = flag38;
					bool flag40 = flag39;
					if (flag40)
					{
						problems.Add("line " + (i + 1) + ": expected 'name = value'");
					}
					else
					{
						string text3 = text2.Substring(0, num).Trim();
						string text4 = text2.Substring(num + 1).Trim();
						int num2 = ResourcePackManager.HpPaletteIndex(text);
						bool flag41 = num2 >= 0;
						bool flag42 = flag41;
						bool flag43 = flag42;
						bool flag44 = flag43;
						bool flag45 = flag44;
						bool flag46 = flag45;
						bool flag47 = flag46;
						bool flag48 = flag47;
						if (flag48)
						{
							float num3;
							bool flag49 = !float.TryParse(text3.Replace(',', '.').TrimEnd(new char[]
							{
								'%'
							}), NumberStyles.Float, CultureInfo.InvariantCulture, out num3) || num3 < 0f || num3 > 100f;
							bool flag50 = flag49;
							bool flag51 = flag50;
							bool flag52 = flag51;
							bool flag53 = flag52;
							bool flag54 = flag53;
							bool flag55 = flag54;
							bool flag56 = flag55;
							if (flag56)
							{
								problems.Add("line " + (i + 1) + ": percent must be 0..100");
							}
							else
							{
								Color value;
								bool flag57 = !ResourcePackManager.TryParseHpColor(text4, out value);
								bool flag58 = flag57;
								bool flag59 = flag58;
								bool flag60 = flag59;
								bool flag61 = flag60;
								bool flag62 = flag61;
								bool flag63 = flag62;
								bool flag64 = flag63;
								if (flag64)
								{
									problems.Add(string.Concat(new object[]
									{
										"line ",
										i + 1,
										": bad color '",
										text4,
										"'"
									}));
								}
								else
								{
									List<KeyValuePair<float, Color>> list;
									bool flag65 = (list = array[num2]) == null;
									bool flag66 = flag65;
									bool flag67 = flag66;
									bool flag68 = flag67;
									bool flag69 = flag68;
									bool flag70 = flag69;
									bool flag71 = flag70;
									if (flag71)
									{
										list = (array[num2] = new List<KeyValuePair<float, Color>>());
									}
									List<KeyValuePair<float, Color>> list2 = list;
									list2.Add(new KeyValuePair<float, Color>(num3, value));
								}
							}
						}
						else
						{
							bool flag72 = text == "ownoverrides" || text == "enemyoverrides";
							bool flag73 = flag72;
							bool flag74 = flag73;
							bool flag75 = flag74;
							bool flag76 = flag75;
							bool flag77 = flag76;
							bool flag78 = flag77;
							bool flag79 = flag78;
							if (flag79)
							{
								ResourcePackManager.HpOverride hpOverride = ResourcePackManager.ParseHpOverride(text3, text4);
								bool flag80 = hpOverride == null;
								bool flag81 = flag80;
								bool flag82 = flag81;
								bool flag83 = flag82;
								bool flag84 = flag83;
								bool flag85 = flag84;
								bool flag86 = flag85;
								bool flag87 = flag86;
								if (flag87)
								{
									problems.Add(string.Concat(new object[]
									{
										"line ",
										i + 1,
										": bad value '",
										text4,
										"' for ",
										text3
									}));
								}
								else
								{
									((text == "ownoverrides") ? cfg.ownOv : cfg.enemyOv).Add(hpOverride);
								}
							}
							else
							{
								string text5 = text3.ToLowerInvariant();
								bool flag88 = text5 == "secondcolorbrightness";
								bool flag89 = flag88;
								bool flag90 = flag89;
								bool flag91 = flag90;
								bool flag92 = flag91;
								bool flag93 = flag92;
								bool flag94 = flag93;
								bool flag95 = flag94;
								if (flag95)
								{
									cfg.secondBrightness = ResourcePackManager.ParseHpFloat(text4, cfg.secondBrightness, 0f, 4f);
								}
								else
								{
									bool flag96 = text5 == "rings";
									bool flag97 = flag96;
									bool flag98 = flag97;
									bool flag99 = flag98;
									bool flag100 = flag99;
									bool flag101 = flag100;
									bool flag102 = flag101;
									bool flag103 = flag102;
									if (flag103)
									{
										bool flag104 = false;
										bool flag105 = false;
										string[] array2 = text4.Split(new char[]
										{
											','
										});
										for (int j = 0; j < array2.Length; j++)
										{
											string a = array2[j].Trim();
											flag104 |= (a == "1");
											flag105 |= (a == "2");
										}
										bool flag106 = flag104 || flag105;
										bool flag107 = flag106;
										bool flag108 = flag107;
										bool flag109 = flag108;
										bool flag110 = flag109;
										bool flag111 = flag110;
										bool flag112 = flag111;
										bool flag113 = flag112;
										if (flag113)
										{
											cfg.rings = new bool[]
											{
												flag104,
												flag105
											};
										}
										else
										{
											bool flag114 = text4.Trim().Equals("none", StringComparison.OrdinalIgnoreCase);
											bool flag115 = flag114;
											bool flag116 = flag115;
											bool flag117 = flag116;
											bool flag118 = flag117;
											bool flag119 = flag118;
											bool flag120 = flag119;
											bool flag121 = flag120;
											if (flag121)
											{
												cfg.rings = new bool[2];
											}
											else
											{
												problems.Add("line " + (i + 1) + ": Rings must be 1, 2, 1,2 or none");
											}
										}
									}
									else
									{
										bool flag122 = text5 == "alwaysshowown" || text5 == "alwaysshowenemy" || text5 == "alwaysshowhpown" || text5 == "alwaysshowhpenemy";
										bool flag123 = flag122;
										bool flag124 = flag123;
										bool flag125 = flag124;
										bool flag126 = flag125;
										bool flag127 = flag126;
										bool flag128 = flag127;
										bool flag129 = flag128;
										if (flag129)
										{
											bool flag131;
											bool flag130 = ResourcePackManager.TryParseHpBool(text4, out flag131);
											bool flag132 = flag130;
											bool flag133 = flag132;
											bool flag134 = flag133;
											bool flag135 = flag134;
											bool flag136 = flag135;
											bool flag137 = flag136;
											bool flag138 = flag137;
											if (flag138)
											{
												bool flag139 = text5 == "alwaysshowown" || text5 == "alwaysshowhpown";
												bool flag140 = flag139;
												bool flag141 = flag140;
												bool flag142 = flag141;
												bool flag143 = flag142;
												bool flag144 = flag143;
												bool flag145 = flag144;
												bool flag146 = flag145;
												if (flag146)
												{
													cfg.alwaysOwn = flag131;
												}
												else
												{
													cfg.alwaysEnemy = flag131;
												}
											}
											else
											{
												problems.Add(string.Concat(new object[]
												{
													"line ",
													i + 1,
													": ",
													text3,
													" must be true/false"
												}));
											}
										}
										else
										{
											bool flag147 = text5 == "colorenemies";
											bool flag148 = flag147;
											bool flag149 = flag148;
											bool flag150 = flag149;
											bool flag151 = flag150;
											bool flag152 = flag151;
											bool flag153 = flag152;
											bool flag154 = flag153;
											if (flag154)
											{
												bool colorEnemies;
												bool flag155 = ResourcePackManager.TryParseHpBool(text4, out colorEnemies);
												bool flag156 = flag155;
												bool flag157 = flag156;
												bool flag158 = flag157;
												bool flag159 = flag158;
												bool flag160 = flag159;
												bool flag161 = flag160;
												bool flag162 = flag161;
												if (flag162)
												{
													cfg.colorEnemies = colorEnemies;
												}
												else
												{
													problems.Add("line " + (i + 1) + ": ColorEnemies must be true/false");
												}
											}
											else
											{
												bool flag163 = text5 == "enemypalette";
												bool flag164 = flag163;
												bool flag165 = flag164;
												bool flag166 = flag165;
												bool flag167 = flag166;
												bool flag168 = flag167;
												bool flag169 = flag168;
												bool flag170 = flag169;
												if (flag170)
												{
													bool enemyPalette;
													bool flag171 = ResourcePackManager.TryParseHpBool(text4, out enemyPalette);
													bool flag172 = flag171;
													bool flag173 = flag172;
													bool flag174 = flag173;
													bool flag175 = flag174;
													bool flag176 = flag175;
													bool flag177 = flag176;
													bool flag178 = flag177;
													if (flag178)
													{
														cfg.enemyPalette = enemyPalette;
													}
													else
													{
														problems.Add("line " + (i + 1) + ": EnemyPalette must be true/false");
													}
												}
												else
												{
													bool flag179 = text5 == "colorlayer" && text4.Length > 0;
													bool flag180 = flag179;
													bool flag181 = flag180;
													bool flag182 = flag181;
													bool flag183 = flag182;
													bool flag184 = flag183;
													bool flag185 = flag184;
													bool flag186 = flag185;
													if (flag186)
													{
														cfg.layerName = text4;
													}
													else
													{
														bool flag187 = text5 == "enemystate" && text4.Length > 0;
														bool flag188 = flag187;
														bool flag189 = flag188;
														bool flag190 = flag189;
														bool flag191 = flag190;
														bool flag192 = flag191;
														bool flag193 = flag192;
														bool flag194 = flag193;
														if (flag194)
														{
															cfg.enemyState = text4;
														}
														else
														{
															bool flag195 = ResourcePackManager.ApplyExtraHpKey(text5, text3, text4, cfg, i, problems);
															bool flag196 = !flag195;
															bool flag197 = flag196;
															bool flag198 = flag197;
															bool flag199 = flag198;
															bool flag200 = flag199;
															bool flag201 = flag200;
															bool flag202 = flag201;
															if (flag202)
															{
																bool flag203 = text5 == "scanintervalseconds";
																bool flag204 = flag203;
																bool flag205 = flag204;
																bool flag206 = flag205;
																bool flag207 = flag206;
																bool flag208 = flag207;
																bool flag209 = flag208;
																bool flag210 = flag209;
																if (flag210)
																{
																	cfg.scanInterval = ResourcePackManager.ParseHpFloat(text4, cfg.scanInterval, 1f, 600f);
																}
																else
																{
																	bool flag211 = text5 == "configcheckseconds";
																	bool flag212 = flag211;
																	bool flag213 = flag212;
																	bool flag214 = flag213;
																	bool flag215 = flag214;
																	bool flag216 = flag215;
																	bool flag217 = flag216;
																	bool flag218 = flag217;
																	if (flag218)
																	{
																		cfg.checkInterval = ResourcePackManager.ParseHpFloat(text4, cfg.checkInterval, 0.5f, 600f);
																	}
																	else
																	{
																		problems.Add(string.Concat(new object[]
																		{
																			"line ",
																			i + 1,
																			": unknown setting '",
																			text3,
																			"' (section [",
																			text,
																			"])"
																		}));
																	}
																}
															}
														}
													}
												}
											}
										}
									}
								}
							}
						}
					}
				}
			}
		}
		bool flag219 = array[0] != null;
		bool flag220 = flag219;
		bool flag221 = flag220;
		bool flag222 = flag221;
		bool flag223 = flag222;
		bool flag224 = flag223;
		bool flag225 = flag224;
		bool flag226 = flag225;
		if (flag226)
		{
			cfg.ownStops = ResourcePackManager.ToHpStops(array[0]);
		}
		bool flag227 = array[1] != null;
		bool flag228 = flag227;
		bool flag229 = flag228;
		bool flag230 = flag229;
		bool flag231 = flag230;
		bool flag232 = flag231;
		bool flag233 = flag232;
		bool flag234 = flag233;
		if (flag234)
		{
			cfg.enemyStops = ResourcePackManager.ToHpStops(array[1]);
		}
		bool flag235 = array[2] != null;
		bool flag236 = flag235;
		bool flag237 = flag236;
		bool flag238 = flag237;
		bool flag239 = flag238;
		bool flag240 = flag239;
		bool flag241 = flag240;
		bool flag242 = flag241;
		if (flag242)
		{
			cfg.stamOwnStops = ResourcePackManager.ToHpStops(array[2]);
		}
		bool flag243 = array[3] != null;
		bool flag244 = flag243;
		bool flag245 = flag244;
		bool flag246 = flag245;
		bool flag247 = flag246;
		bool flag248 = flag247;
		bool flag249 = flag248;
		bool flag250 = flag249;
		if (flag250)
		{
			cfg.stamEnemyStops = ResourcePackManager.ToHpStops(array[3]);
		}
		bool flag251 = array[4] != null;
		bool flag252 = flag251;
		bool flag253 = flag252;
		bool flag254 = flag253;
		bool flag255 = flag254;
		bool flag256 = flag255;
		bool flag257 = flag256;
		bool flag258 = flag257;
		if (flag258)
		{
			cfg.ammoStops = ResourcePackManager.ToHpStops(array[4]);
		}
		bool flag259 = array[5] != null;
		bool flag260 = flag259;
		bool flag261 = flag260;
		bool flag262 = flag261;
		bool flag263 = flag262;
		bool flag264 = flag263;
		bool flag265 = flag264;
		bool flag266 = flag265;
		if (flag266)
		{
			cfg.bldOwnStops = ResourcePackManager.ToHpStops(array[5]);
		}
		bool flag267 = array[6] != null;
		bool flag268 = flag267;
		bool flag269 = flag268;
		bool flag270 = flag269;
		bool flag271 = flag270;
		bool flag272 = flag271;
		bool flag273 = flag272;
		bool flag274 = flag273;
		if (flag274)
		{
			cfg.bldEnemyStops = ResourcePackManager.ToHpStops(array[6]);
		}
	}
}
}
