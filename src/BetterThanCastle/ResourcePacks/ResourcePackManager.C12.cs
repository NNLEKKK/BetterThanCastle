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

	private void RebuildBombMeshes(ResourcePackManager.BombGroup g, ResourcePackManager.HpConfig cfg)
	{
		this.FreeBombGroupMeshes(g);
		bool bombMap3D = cfg.bombMap3D;
		int num = bombMap3D ? 24 : 4;
		int num2 = 64000 / num;
		float bombMapYOffset = cfg.bombMapYOffset;
		List<Vector3> list = new List<Vector3>(Mathf.Min(g.cells.Count, num2) * num);
		List<Color32> list2 = new List<Color32>(Mathf.Min(g.cells.Count, num2) * num);
		int i = 0;
		int count = g.cells.Count;
		while (i < count)
		{
			list.Clear();
			list2.Clear();
			int num3 = Mathf.Min(count, i + num2);
			while (i < num3)
			{
				ResourcePackManager.BombCell bombCell = g.cells[i];
				bombCell.band = ResourcePackManager.BombBandFor(cfg, bombCell.value, bombCell.t);
				bool flag = (cfg.bombBandMask >> bombCell.band & 1) == 0;
				bool flag2 = !flag;
				bool flag3 = flag2;
				bool flag4 = flag3;
				bool flag5 = flag4;
				bool flag6 = flag5;
				bool flag7 = flag6;
				bool flag8 = flag7;
				if (flag8)
				{
					Color c = cfg.bombColors[bombCell.band];
					c.a = cfg.bombMapAlpha;
					Color32 item = c;
					float x = bombCell.center.x;
					float z = bombCell.center.z;
					float x2 = x - 0.47f;
					float x3 = x + 0.47f;
					float z2 = z - 0.47f;
					float z3 = z + 0.47f;
					bool flag9 = !bombMap3D;
					bool flag10 = flag9;
					bool flag11 = flag10;
					bool flag12 = flag11;
					bool flag13 = flag12;
					bool flag14 = flag13;
					bool flag15 = flag14;
					bool flag16 = flag15;
					if (flag16)
					{
						float y = bombCell.center.y - 0.5f + bombMapYOffset + 0.03f;
						list.Add(new Vector3(x2, y, z2));
						list.Add(new Vector3(x2, y, z3));
						list.Add(new Vector3(x3, y, z3));
						list.Add(new Vector3(x3, y, z2));
						for (int j = 0; j < 4; j++)
						{
							list2.Add(item);
						}
					}
					else
					{
						float y2 = bombCell.center.y - 0.47f + bombMapYOffset;
						float y3 = bombCell.center.y + 0.47f + bombMapYOffset;
						list.Add(new Vector3(x2, y2, z2));
						list.Add(new Vector3(x2, y2, z3));
						list.Add(new Vector3(x3, y2, z3));
						list.Add(new Vector3(x3, y2, z2));
						list.Add(new Vector3(x2, y3, z2));
						list.Add(new Vector3(x2, y3, z3));
						list.Add(new Vector3(x3, y3, z3));
						list.Add(new Vector3(x3, y3, z2));
						list.Add(new Vector3(x2, y2, z2));
						list.Add(new Vector3(x3, y2, z2));
						list.Add(new Vector3(x3, y3, z2));
						list.Add(new Vector3(x2, y3, z2));
						list.Add(new Vector3(x2, y2, z3));
						list.Add(new Vector3(x3, y2, z3));
						list.Add(new Vector3(x3, y3, z3));
						list.Add(new Vector3(x2, y3, z3));
						list.Add(new Vector3(x2, y2, z2));
						list.Add(new Vector3(x2, y2, z3));
						list.Add(new Vector3(x2, y3, z3));
						list.Add(new Vector3(x2, y3, z2));
						list.Add(new Vector3(x3, y2, z2));
						list.Add(new Vector3(x3, y2, z3));
						list.Add(new Vector3(x3, y3, z3));
						list.Add(new Vector3(x3, y3, z2));
						for (int k = 0; k < 24; k++)
						{
							list2.Add(item);
						}
					}
				}
				i++;
			}
			bool flag17 = list.Count == 0;
			bool flag18 = !flag17;
			bool flag19 = flag18;
			bool flag20 = flag19;
			bool flag21 = flag20;
			bool flag22 = flag21;
			bool flag23 = flag22;
			bool flag24 = flag23;
			if (flag24)
			{
				int[] array = new int[list.Count];
				for (int l = 0; l < array.Length; l++)
				{
					array[l] = l;
				}
				Mesh mesh = new Mesh();
				mesh.hideFlags = HideFlags.HideAndDontSave;
				mesh.vertices = list.ToArray();
				mesh.colors32 = list2.ToArray();
				mesh.SetIndices(array, MeshTopology.Quads, 0);
				g.meshes.Add(mesh);
			}
		}
	}

	private void DumpBuildings()
	{
		try
		{
			string dumpDir = BtcPaths.DumpDir;
			Directory.CreateDirectory(dumpDir);
			StringBuilder stringBuilder = new StringBuilder();
			DamageReceiver[] array = UnityEngine.Object.FindObjectsOfType<DamageReceiver>();
			Dictionary<string, int> dictionary = new Dictionary<string, int>();
			int num = 0;
			foreach (DamageReceiver damageReceiver in array)
			{
				bool flag = damageReceiver == null || damageReceiver is BricktronDamageReceiver;
				bool flag2 = !flag;
				bool flag3 = flag2;
				bool flag4 = flag3;
				bool flag5 = flag4;
				bool flag6 = flag5;
				bool flag7 = flag6;
				bool flag8 = flag7;
				if (flag8)
				{
					string name = damageReceiver.GetType().Name;
					int num2;
					dictionary.TryGetValue(name, out num2);
					dictionary[name] = num2 + 1;
					bool flag9 = num >= 120 || num2 >= 12;
					bool flag10 = !flag9;
					bool flag11 = flag10;
					bool flag12 = flag11;
					bool flag13 = flag12;
					bool flag14 = flag13;
					bool flag15 = flag14;
					bool flag16 = flag15;
					if (flag16)
					{
						num++;
						GameObject gameObject = damageReceiver.gameObject;
						string text = gameObject.name;
						Transform parent = gameObject.transform.parent;
						while (parent != null)
						{
							text = parent.name + "/" + text;
							parent = parent.parent;
						}
						stringBuilder.AppendLine(string.Concat(new object[]
						{
							"== ",
							name,
							" | ",
							text,
							" | active=",
							gameObject.activeInHierarchy.ToString(),
							" | layer=",
							LayerMask.LayerToName(gameObject.layer),
							" | pos=",
							gameObject.transform.position
						}));
						stringBuilder.AppendLine(string.Concat(new object[]
						{
							"   HP ",
							damageReceiver.CurrentHP,
							" / ",
							damageReceiver.MaxHP,
							" | regen/s=",
							damageReceiver.RegenPerSecond,
							" | delay=",
							damageReceiver._delayBeforeRegenPops
						}));
						Component[] components = gameObject.GetComponents<Component>();
						StringBuilder stringBuilder2 = new StringBuilder();
						for (int j = 0; j < components.Length; j++)
						{
							bool flag17 = components[j] != null;
							bool flag18 = flag17;
							bool flag19 = flag18;
							bool flag20 = flag19;
							bool flag21 = flag20;
							bool flag22 = flag21;
							bool flag23 = flag22;
							bool flag24 = flag23;
							if (flag24)
							{
								stringBuilder2.Append(components[j].GetType().Name).Append(", ");
							}
						}
						stringBuilder.AppendLine("   components: " + stringBuilder2);
						Transform root = gameObject.transform.root;
						Labor componentInParent = gameObject.GetComponentInParent<Labor>();
						stringBuilder.AppendLine("   root=" + root.name + " | Labor in parents=" + ((componentInParent != null) ? componentInParent.name : "none"));
						SelectionCircle[] componentsInChildren = root.GetComponentsInChildren<SelectionCircle>(true);
						int num3 = 0;
						while (num3 < componentsInChildren.Length && num3 < 3)
						{
							SelectionCircle selectionCircle = componentsInChildren[num3];
							string text2 = "no renderer";
							bool flag25 = ResourcePackManager.circleRendField == null;
							bool flag26 = flag25;
							bool flag27 = flag26;
							bool flag28 = flag27;
							bool flag29 = flag28;
							bool flag30 = flag29;
							bool flag31 = flag30;
							bool flag32 = flag31;
							if (flag32)
							{
								ResourcePackManager.circleRendField = typeof(SelectionCircle).GetField("_renderer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
							}
							bool flag33 = ResourcePackManager.circleRendField != null;
							bool flag34 = flag33;
							bool flag35 = flag34;
							bool flag36 = flag35;
							bool flag37 = flag36;
							bool flag38 = flag37;
							bool flag39 = flag38;
							bool flag40 = flag39;
							if (flag40)
							{
								Renderer renderer = ResourcePackManager.circleRendField.GetValue(selectionCircle) as Renderer;
								bool flag41 = renderer != null && renderer.sharedMaterial != null;
								bool flag42 = flag41;
								bool flag43 = flag42;
								bool flag44 = flag43;
								bool flag45 = flag44;
								bool flag46 = flag45;
								bool flag47 = flag46;
								bool flag48 = flag47;
								if (flag48)
								{
									text2 = "renderer=" + renderer.name + " shader=" + ((renderer.sharedMaterial.shader != null) ? renderer.sharedMaterial.shader.name : "?");
								}
							}
							stringBuilder.AppendLine(string.Concat(new string[]
							{
								"   ring: ",
								selectionCircle.name,
								" (active=",
								selectionCircle.gameObject.activeInHierarchy.ToString(),
								") ",
								text2
							}));
							num3++;
						}
						bool flag49 = componentsInChildren.Length == 0;
						bool flag50 = flag49;
						bool flag51 = flag50;
						bool flag52 = flag51;
						bool flag53 = flag52;
						bool flag54 = flag53;
						bool flag55 = flag54;
						bool flag56 = flag55;
						if (flag56)
						{
							stringBuilder.AppendLine("   ring: none under root");
						}
					}
				}
			}
			stringBuilder.Insert(0, "Non-bricktron damage receivers by type: " + string.Join(", ", new List<string>(ResourcePackManager.MapCounts(dictionary)).ToArray()) + "\r\n\r\n");
			File.WriteAllText(System.IO.Path.Combine(dumpDir, "buildings.txt"), stringBuilder.ToString());
			ResourcePackManager.PackLog("[ResourcePacks] Building dump written: " + dumpDir);
			this.ShowMessage("Building dump: " + num + " objects\ndebug/dump/buildings.txt");
		}
		catch (Exception arg)
		{
			ResourcePackManager.PackWarn("[ResourcePacks] Building dump failed: " + arg);
			this.ShowMessage("Building dump failed - see log");
		}
	}

	private static IEnumerable<string> MapCounts(Dictionary<string, int> counts)
	{
		foreach (KeyValuePair<string, int> kv in counts)
		{
			yield return kv.Key + "=" + kv.Value;
		}
		Dictionary<string, int>.Enumerator enumerator = default(Dictionary<string, int>.Enumerator);
		yield break;
		yield break;
	}

	private static float TimestampDelta(object ts)
	{
		bool flag = ts == null;
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
			result = 99999f;
		}
		else
		{
			BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
			bool flag9 = ResourcePackManager.pTsDelta == null && ResourcePackManager.fTsDelta == null;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				Type type = ts.GetType();
				ResourcePackManager.pTsDelta = type.GetProperty("Delta", bindingAttr);
				bool flag17 = ResourcePackManager.pTsDelta == null;
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				if (flag24)
				{
					ResourcePackManager.fTsDelta = type.GetField("Delta", bindingAttr);
				}
				bool flag25 = ResourcePackManager.pTsDelta == null && ResourcePackManager.fTsDelta == null;
				bool flag26 = flag25;
				bool flag27 = flag26;
				bool flag28 = flag27;
				bool flag29 = flag28;
				bool flag30 = flag29;
				bool flag31 = flag30;
				bool flag32 = flag31;
				if (flag32)
				{
					throw new MissingMemberException("Delta not found on " + type.Name);
				}
			}
			object value = (ResourcePackManager.pTsDelta != null) ? ResourcePackManager.pTsDelta.GetValue(ts, null) : ResourcePackManager.fTsDelta.GetValue(ts);
			result = Convert.ToSingle(value);
		}
		return result;
	}

	private static string RegenText(float rem)
	{
		int num = Mathf.Clamp(Mathf.CeilToInt(rem * 10f), 0, 1200);
		bool flag = ResourcePackManager.regenTexts[num] == null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			ResourcePackManager.regenTexts[num] = ((float)num / 10f).ToString("0.0", CultureInfo.InvariantCulture);
		}
		return ResourcePackManager.regenTexts[num];
	}

	private static int BombBandFor(ResourcePackManager.HpConfig cfg, int value, float t)
	{
		int num = 0;
		bool bombByDamage = cfg.bombByDamage;
		bool flag = bombByDamage;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		int result;
		if (flag7)
		{
			for (int i = 0; i < cfg.bombDmg.Length; i++)
			{
				bool flag8 = (float)value >= cfg.bombDmg[i];
				bool flag9 = flag8;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				if (flag15)
				{
					num = i + 1;
				}
			}
			result = num;
		}
		else
		{
			float num2 = t * 100f;
			for (int j = 0; j < cfg.bombThr.Length; j++)
			{
				bool flag16 = num2 >= cfg.bombThr[j];
				bool flag17 = flag16;
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				if (flag23)
				{
					num = j + 1;
				}
			}
			result = num;
		}
		return result;
	}

	private static List<ResourcePackManager.HpStop> CloneHpStops(List<ResourcePackManager.HpStop> src)
	{
		List<ResourcePackManager.HpStop> list = new List<ResourcePackManager.HpStop>();
		for (int i = 0; i < src.Count; i++)
		{
			list.Add(new ResourcePackManager.HpStop
			{
				pct = src[i].pct,
				col = src[i].col
			});
		}
		return list;
	}

	private void UpdateBuildingUnits()
	{
		ResourcePackManager.HpConfig hpConfig = this.hpCfg;
		bool flag = hpConfig != null && (hpConfig.bldHpNumOwn || hpConfig.bldRegenOwn);
		bool flag2 = hpConfig != null && (hpConfig.bldHpNumEnemy || hpConfig.bldRegenEnemy);
		bool flag3 = !flag && !flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		bool flag9 = flag8;
		bool flag10 = flag9;
		if (flag10)
		{
			bool flag11 = this.bldUnits.Count > 0;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			bool flag17 = flag16;
			bool flag18 = flag17;
			if (flag18)
			{
				this.bldUnits.Clear();
			}
		}
		else
		{
			float unscaledTime = Time.unscaledTime;
			bool flag19 = unscaledTime < this.bldNextScan;
			bool flag20 = !flag19;
			bool flag21 = flag20;
			bool flag22 = flag21;
			bool flag23 = flag22;
			bool flag24 = flag23;
			bool flag25 = flag24;
			bool flag26 = flag25;
			if (flag26)
			{
				this.bldNextScan = unscaledTime + 0.25f;
				this.bldUnits.Clear();
				ResourcePackManager.PruneDead<Transform, ResourcePackManager.BldUnit>(this.bldCache, 600);
				bool flag27 = flag;
				bool flag28 = flag27;
				bool flag29 = flag28;
				bool flag30 = flag29;
				bool flag31 = flag30;
				bool flag32 = flag31;
				bool flag33 = flag32;
				bool flag34 = flag33;
				if (flag34)
				{
					this.CollectBuildings(UIGameObserver.dynamicObjects, false);
				}
				bool flag35 = flag2;
				bool flag36 = flag35;
				bool flag37 = flag36;
				bool flag38 = flag37;
				bool flag39 = flag38;
				bool flag40 = flag39;
				bool flag41 = flag40;
				bool flag42 = flag41;
				if (flag42)
				{
					this.CollectBuildings(UIGameObserver.ennemyDynamicObjects, true);
				}
			}
		}
	}

	private void CollectBuildings(UIGameObserver.DynamicObjects obs, bool enemy)
	{
		bool flag = obs == null || obs.All == null;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			foreach (Transform transform in obs.All)
			{
				bool flag9 = transform == null || !transform.gameObject.activeInHierarchy;
				bool flag10 = !flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					ResourcePackManager.BldUnit bldUnit;
					bool flag17 = !this.bldCache.TryGetValue(transform, out bldUnit);
					bool flag18 = flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					bool flag21 = flag20;
					bool flag22 = flag21;
					bool flag23 = flag22;
					bool flag24 = flag23;
					if (flag24)
					{
						bldUnit = new ResourcePackManager.BldUnit();
						bldUnit.tr = transform;
						bldUnit.dr = transform.GetComponent<DamageReceiver>();
						bldUnit.none = (bldUnit.dr == null);
						bldUnit.localOffset = (bldUnit.none ? Vector3.zero : ResourcePackManager.GetRingOffset(transform.gameObject));
						this.bldCache[transform] = bldUnit;
					}
					bool flag25 = bldUnit.none || bldUnit.dr == null || bldUnit.dr.CurrentHP <= 0f || bldUnit.dr is BricktronDamageReceiver;
					bool flag26 = !flag25;
					bool flag27 = flag26;
					bool flag28 = flag27;
					bool flag29 = flag28;
					bool flag30 = flag29;
					bool flag31 = flag30;
					bool flag32 = flag31;
					if (flag32)
					{
						bool flag33 = enemy && !ResourcePackManager.BldVisibleForMe(bldUnit);
						bool flag34 = !flag33;
						bool flag35 = flag34;
						bool flag36 = flag35;
						bool flag37 = flag36;
						bool flag38 = flag37;
						bool flag39 = flag38;
						bool flag40 = flag39;
						if (flag40)
						{
							bldUnit.enemy = enemy;
							this.bldUnits.Add(bldUnit);
						}
					}
				}
			}
		}
	}

	private void DrawBuildingNumbers()
	{
		ResourcePackManager.HpConfig hpConfig = this.hpCfg;
		bool flag = hpConfig == null || this.bldUnits.Count == 0 || Event.current.type != EventType.Repaint;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			Camera main = Camera.main;
			bool flag9 = main == null;
			bool flag10 = !flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				float num = (float)Screen.height / 1080f;
				int num2 = Mathf.Max(8, Mathf.RoundToInt(28f * hpConfig.statSize * num));
				bool flag17 = this.statStyle == null;
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				if (flag24)
				{
					this.statStyle = new GUIStyle(GUI.skin.label);
					this.statStyle.alignment = TextAnchor.MiddleCenter;
					this.statStyle.wordWrap = false;
					this.statStyle.clipping = TextClipping.Overflow;
				}
				this.statStyle.fontSize = num2;
				this.statStyle.fontStyle = (hpConfig.statBold ? FontStyle.Bold : FontStyle.Normal);
				GUI.depth = 6;
				Color color = GUI.color;
				float ow = Mathf.Max(1f, (float)num2 * 0.06f);
				Vector3 position = main.transform.position;
				float num3 = hpConfig.statHideDist * hpConfig.statHideDist;
				float unscaledTime = Time.unscaledTime;
				for (int i = 0; i < this.bldUnits.Count; i++)
				{
					ResourcePackManager.BldUnit bldUnit = this.bldUnits[i];
					bool flag25 = bldUnit.tr == null || bldUnit.dr == null || !bldUnit.tr.gameObject.activeInHierarchy;
					bool flag26 = !flag25;
					bool flag27 = flag26;
					bool flag28 = flag27;
					bool flag29 = flag28;
					bool flag30 = flag29;
					bool flag31 = flag30;
					bool flag32 = flag31;
					if (flag32)
					{
						bool flag33 = hpConfig.statHideFar && (bldUnit.tr.position - position).sqrMagnitude > num3;
						bool flag34 = !flag33;
						bool flag35 = flag34;
						bool flag36 = flag35;
						bool flag37 = flag36;
						bool flag38 = flag37;
						bool flag39 = flag38;
						bool flag40 = flag39;
						if (flag40)
						{
							Vector3 vector = main.WorldToScreenPoint(bldUnit.tr.TransformPoint(bldUnit.localOffset));
							bool flag41 = vector.z <= 0f || vector.x < -100f || vector.x > (float)Screen.width + 100f || vector.y < -100f || vector.y > (float)Screen.height + 100f;
							bool flag42 = !flag41;
							bool flag43 = flag42;
							bool flag44 = flag43;
							bool flag45 = flag44;
							bool flag46 = flag45;
							bool flag47 = flag46;
							bool flag48 = flag47;
							if (flag48)
							{
								float currentHP = bldUnit.dr.CurrentHP;
								float maxHP = bldUnit.dr.MaxHP;
								bool flag49 = maxHP <= 0f;
								bool flag50 = !flag49;
								bool flag51 = flag50;
								bool flag52 = flag51;
								bool flag53 = flag52;
								bool flag54 = flag53;
								bool flag55 = flag54;
								bool flag56 = flag55;
								if (flag56)
								{
									bool flag57 = currentHP >= maxHP - 0.01f;
									bool flag58 = (bldUnit.enemy ? hpConfig.bldHpNumEnemy : hpConfig.bldHpNumOwn) && (!hpConfig.statHideFull || !flag57);
									bool flag59 = (bldUnit.enemy ? hpConfig.bldRegenEnemy : hpConfig.bldRegenOwn) && !ResourcePackManager.regenReflFail && !flag57;
									bool flag60 = flag58;
									bool flag61 = flag60;
									bool flag62 = flag61;
									bool flag63 = flag62;
									bool flag64 = flag63;
									bool flag65 = flag64;
									bool flag66 = flag65;
									bool flag67 = flag66;
									if (flag67)
									{
										bool flag68 = bldUnit.enemy && hpConfig.enemyPalette;
										ResourcePackManager.HpPalette hpPalette = hpConfig.bldColor ? (flag68 ? hpConfig.bldEnemy : hpConfig.bldOwn) : (flag68 ? hpConfig.enemy : hpConfig.own);
										Color col = Color.white;
										bool flag69 = hpPalette.mins.Length != 0;
										bool flag70 = flag69;
										bool flag71 = flag70;
										bool flag72 = flag71;
										bool flag73 = flag72;
										bool flag74 = flag73;
										bool flag75 = flag74;
										bool flag76 = flag75;
										if (flag76)
										{
											col = hpPalette.main[hpPalette.Index(currentHP / maxHP * 100f)];
										}
										col.a = hpConfig.statAlpha;
										this.DrawStatLabel(hpConfig, vector, num, num2, ow, 0f, hpConfig.bldHpNumY, bldUnit.HpText(currentHP, maxHP, hpConfig.statFormat), col);
									}
									bool flag77 = flag59;
									bool flag78 = flag77;
									bool flag79 = flag78;
									bool flag80 = flag79;
									bool flag81 = flag80;
									bool flag82 = flag81;
									bool flag83 = flag82;
									bool flag84 = flag83;
									if (flag84)
									{
										bool flag85 = unscaledTime - bldUnit.regenRead > 0.1f;
										bool flag86 = flag85;
										bool flag87 = flag86;
										bool flag88 = flag87;
										bool flag89 = flag88;
										bool flag90 = flag89;
										bool flag91 = flag90;
										bool flag92 = flag91;
										if (flag92)
										{
											bldUnit.regenRead = unscaledTime;
											bldUnit.regenRem = this.ReadRegenRemaining(bldUnit.dr);
										}
										float num4 = bldUnit.regenRem - (unscaledTime - bldUnit.regenRead);
										bool flag93 = num4 > 0.05f;
										bool flag94 = flag93;
										bool flag95 = flag94;
										bool flag96 = flag95;
										bool flag97 = flag96;
										bool flag98 = flag97;
										bool flag99 = flag98;
										bool flag100 = flag99;
										if (flag100)
										{
											Color regenNumColor = hpConfig.regenNumColor;
											regenNumColor.a = hpConfig.regenNumColor.a * hpConfig.statAlpha;
											this.DrawStatLabel(hpConfig, vector, num, num2, ow, 0f, hpConfig.bldRegenY, ResourcePackManager.RegenText(num4), regenNumColor);
										}
									}
								}
							}
						}
					}
				}
				GUI.color = color;
			}
		}
	}

	private float ReadRegenRemaining(DamageReceiver dr)
	{
		float result;
		try
		{
			bool flag = dr.ExternalRegenerator != null || dr.EffectiveRegenPerSecond <= 0f;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				result = 0f;
			}
			else
			{
				bool flag9 = ResourcePackManager.fRegenDmg == null;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
					ResourcePackManager.fRegenDmg = typeof(DamageReceiver).GetField("_lastDamageTimestamp", bindingAttr);
					ResourcePackManager.fRegenSta = typeof(BricktronDamageReceiver).GetField("_lastStaminaLostTimestamp", bindingAttr);
					bool flag17 = ResourcePackManager.fRegenDmg == null;
					bool flag18 = flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					bool flag21 = flag20;
					bool flag22 = flag21;
					bool flag23 = flag22;
					bool flag24 = flag23;
					if (flag24)
					{
						ResourcePackManager.regenReflFail = true;
						ResourcePackManager.PackWarn("[ResourcePacks] Regen countdown: timestamp field not found");
						return 0f;
					}
				}
				float delayBeforeRegenPops = dr._delayBeforeRegenPops;
				float num = delayBeforeRegenPops - ResourcePackManager.TimestampDelta(ResourcePackManager.fRegenDmg.GetValue(dr));
				bool flag25 = ResourcePackManager.fRegenSta != null && dr is BricktronDamageReceiver;
				bool flag26 = flag25;
				bool flag27 = flag26;
				bool flag28 = flag27;
				bool flag29 = flag28;
				bool flag30 = flag29;
				bool flag31 = flag30;
				bool flag32 = flag31;
				if (flag32)
				{
					num = Mathf.Max(num, delayBeforeRegenPops - ResourcePackManager.TimestampDelta(ResourcePackManager.fRegenSta.GetValue(dr)));
				}
				result = Mathf.Max(0f, num);
			}
		}
		catch (Exception ex)
		{
			ResourcePackManager.regenReflFail = true;
			ResourcePackManager.PackWarn("[ResourcePacks] Regen countdown failed: " + ex.Message);
			result = 0f;
		}
		return result;
	}

	private static bool HitAnyOn(ResourcePackManager.HpConfig cfg)
	{
		return cfg != null && (cfg.hitOwn || cfg.hitEnemy || cfg.hitStamOwn || cfg.hitStamEnemy || cfg.hitBldOwn || cfg.hitBldEnemy);
	}

	private static bool HitDpsOn(ResourcePackManager.HpConfig cfg, ResourcePackManager.HitTrack ht)
	{
		return ht.building ? cfg.hitBldDps : cfg.hitDps;
	}

	private void DetachHitTrack(ResourcePackManager.HitTrack ht)
	{
		bool flag = ht != null && ht.handler != null && ht.dr != null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			ht.dr.EventClientDamageReaction -= ht.handler;
		}
		bool flag9 = ht != null;
		bool flag10 = flag9;
		bool flag11 = flag10;
		bool flag12 = flag11;
		bool flag13 = flag12;
		bool flag14 = flag13;
		bool flag15 = flag14;
		bool flag16 = flag15;
		if (flag16)
		{
			ht.handler = null;
		}
	}

	private void ClearHitTracks()
	{
		foreach (KeyValuePair<Transform, ResourcePackManager.HitTrack> keyValuePair in this.hitTracks)
		{
			this.DetachHitTrack(keyValuePair.Value);
		}
		this.hitTracks.Clear();
		this.hitFloats.Clear();
	}

	private static string CleanHitName(string n)
	{
		bool flag = string.IsNullOrEmpty(n);
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
			result = "?";
		}
		else
		{
			int num = n.IndexOf('(');
			bool flag9 = num > 0;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				n = n.Substring(0, num);
			}
			result = n.Trim();
		}
		return result;
	}

	private static string HitAttackerKey(GameObject a)
	{
		bool flag = a == null;
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
			result = "?";
		}
		else
		{
			Labor component = a.GetComponent<Labor>();
			bool flag9 = component != null && component.Occupation != null;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				result = "J" + component.Occupation.CurrentJob.ToString();
			}
			else
			{
				result = "O" + ResourcePackManager.CleanHitName(a.name);
			}
		}
		return result;
	}

	private ResourcePackManager.HitTrack GetHitTrack(Transform key, DamageReceiver dr, bool building, string victimKey)
	{
		ResourcePackManager.HitTrack hitTrack;
		bool flag = !this.hitTracks.TryGetValue(key, out hitTrack) || hitTrack.dr == null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			bool flag9 = hitTrack != null;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				this.DetachHitTrack(hitTrack);
			}
			hitTrack = new ResourcePackManager.HitTrack();
			hitTrack.tr = key;
			hitTrack.dr = dr;
			hitTrack.bdr = (dr as BricktronDamageReceiver);
			hitTrack.building = building;
			hitTrack.victimKey = victimKey;
			hitTrack.localOffset = ResourcePackManager.GetRingOffset(key.gameObject);
			hitTrack.lastHp = dr.CurrentHP;
			hitTrack.lastStam = ((hitTrack.bdr != null) ? hitTrack.bdr.CurrentStamina : 0f);
			hitTrack.lastWorld = key.TransformPoint(hitTrack.localOffset);
			ResourcePackManager.HitTrack self = hitTrack;
			hitTrack.handler = delegate(DamageReaction reaction, Vector3 contact, Vector3 force, float lift, GameObject attacker)
			{
				this.OnHitEvent(self, reaction, attacker);
			};
			dr.EventClientDamageReaction += hitTrack.handler;
			this.hitTracks[key] = hitTrack;
		}
		return hitTrack;
	}

	private void CollectHitBuildings(UIGameObserver.DynamicObjects obs, bool enemy, float now)
	{
		bool flag = obs == null || obs.All == null;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			foreach (Transform transform in obs.All)
			{
				bool flag9 = transform == null || !transform.gameObject.activeInHierarchy || !this.hitSeen.Add(transform);
				bool flag10 = !flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					ResourcePackManager.BldUnit bldUnit;
					bool flag17 = !this.bldCache.TryGetValue(transform, out bldUnit);
					bool flag18 = flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					bool flag21 = flag20;
					bool flag22 = flag21;
					bool flag23 = flag22;
					bool flag24 = flag23;
					if (flag24)
					{
						bldUnit = new ResourcePackManager.BldUnit();
						bldUnit.tr = transform;
						bldUnit.dr = transform.GetComponent<DamageReceiver>();
						bldUnit.none = (bldUnit.dr == null);
						bldUnit.localOffset = (bldUnit.none ? Vector3.zero : ResourcePackManager.GetRingOffset(transform.gameObject));
						this.bldCache[transform] = bldUnit;
					}
					bool flag25 = bldUnit.none || bldUnit.dr == null || bldUnit.dr is BricktronDamageReceiver || bldUnit.dr.CurrentHP <= 0f;
					bool flag26 = !flag25;
					bool flag27 = flag26;
					bool flag28 = flag27;
					bool flag29 = flag28;
					bool flag30 = flag29;
					bool flag31 = flag30;
					bool flag32 = flag31;
					if (flag32)
					{
						bool flag33 = enemy && !ResourcePackManager.BldVisibleForMe(bldUnit);
						bool flag34 = !flag33;
						bool flag35 = flag34;
						bool flag36 = flag35;
						bool flag37 = flag36;
						bool flag38 = flag37;
						bool flag39 = flag38;
						bool flag40 = flag39;
						if (flag40)
						{
							bool flag41 = bldUnit.victimKey == null;
							bool flag42 = flag41;
							bool flag43 = flag42;
							bool flag44 = flag43;
							bool flag45 = flag44;
							bool flag46 = flag45;
							bool flag47 = flag46;
							bool flag48 = flag47;
							if (flag48)
							{
								bldUnit.victimKey = bldUnit.dr.GetType().Name + ":" + ResourcePackManager.CleanHitName(transform.name);
							}
							ResourcePackManager.HitTrack hitTrack = this.GetHitTrack(transform, bldUnit.dr, true, bldUnit.victimKey);
							hitTrack.enemy = enemy;
							hitTrack.lastSeen = now;
							hitTrack.lastWorld = transform.TransformPoint(hitTrack.localOffset);
						}
					}
				}
			}
		}
	}

	private void OnHitEvent(ResourcePackManager.HitTrack ht, DamageReaction reaction, GameObject attacker)
	{
		ResourcePackManager.HpConfig hpConfig = this.hpCfg;
		bool flag = ht == null || hpConfig == null || reaction == DamageReaction.Ignored || reaction == DamageReaction.Failed;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			ResourcePackManager.HitEvt hitEvt = new ResourcePackManager.HitEvt();
			hitEvt.t = Time.unscaledTime;
			hitEvt.reaction = reaction;
			hitEvt.key = ResourcePackManager.HitAttackerKey(attacker);
			ht.lastEvtAt = hitEvt.t;
			ht.lastEvtKey = hitEvt.key;
			ht.evts.Add(hitEvt);
			this.MatchHits(hpConfig, ht, hitEvt.t);
		}
	}

	private void MatchHits(ResourcePackManager.HpConfig cfg, ResourcePackManager.HitTrack ht, float now)
	{
		for (int i = ht.evts.Count - 1; i >= 0; i--)
		{
			bool flag = now - ht.evts[i].t > 1f;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				ht.evts.RemoveAt(i);
			}
		}
		int j = 0;
		while (j < ht.losses.Count)
		{
			ResourcePackManager.HitLoss hitLoss = ht.losses[j];
			int num = -1;
			float num2 = 0.5f;
			for (int k = 0; k < ht.evts.Count; k++)
			{
				float num3 = Mathf.Abs(ht.evts[k].t - hitLoss.t);
				bool flag9 = num3 <= num2;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					num2 = num3;
					num = k;
				}
			}
			bool flag17 = num < 0;
			bool flag18 = flag17;
			bool flag19 = flag18;
			bool flag20 = flag19;
			bool flag21 = flag20;
			bool flag22 = flag21;
			bool flag23 = flag22;
			bool flag24 = flag23;
			if (flag24)
			{
				bool flag25 = now - hitLoss.t > 0.6f;
				bool flag26 = flag25;
				bool flag27 = flag26;
				bool flag28 = flag27;
				bool flag29 = flag28;
				bool flag30 = flag29;
				bool flag31 = flag30;
				bool flag32 = flag31;
				if (flag32)
				{
					ht.losses.RemoveAt(j);
				}
				else
				{
					j++;
				}
			}
			else
			{
				ResourcePackManager.HitEvt hitEvt = ht.evts[num];
				int num4 = 0;
				for (int l = 0; l < ht.evts.Count; l++)
				{
					bool flag33 = Mathf.Abs(ht.evts[l].t - hitEvt.t) <= 0.3f;
					bool flag34 = flag33;
					bool flag35 = flag34;
					bool flag36 = flag35;
					bool flag37 = flag36;
					bool flag38 = flag37;
					bool flag39 = flag38;
					bool flag40 = flag39;
					if (flag40)
					{
						num4++;
					}
				}
				int num5 = 0;
				for (int m = 0; m < ht.losses.Count; m++)
				{
					bool flag41 = Mathf.Abs(ht.losses[m].t - hitLoss.t) <= 0.3f;
					bool flag42 = flag41;
					bool flag43 = flag42;
					bool flag44 = flag43;
					bool flag45 = flag44;
					bool flag46 = flag45;
					bool flag47 = flag46;
					bool flag48 = flag47;
					if (flag48)
					{
						num5++;
					}
				}
				ht.evts.RemoveAt(num);
				ht.losses.RemoveAt(j);
				this.ApplyHitPair(cfg, ht, hitLoss, hitEvt, num4 == 1 && num5 == 1, now);
			}
		}
	}

	private void ApplyHitPair(ResourcePackManager.HpConfig cfg, ResourcePackManager.HitTrack ht, ResourcePackManager.HitLoss d, ResourcePackManager.HitEvt e, bool single, float now)
	{
		bool flag = d.stam > 0.001f && !d.stamDone;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			this.RegisterDamage(cfg, ht, d.stam, true, now, d.gt, false);
			d.stamDone = true;
		}
		string key = e.key + ">" + ht.victimKey;
		bool flag9 = e.reaction == DamageReaction.Die;
		bool flag10 = flag9;
		bool flag11 = flag10;
		bool flag12 = flag11;
		bool flag13 = flag12;
		bool flag14 = flag13;
		bool flag15 = flag14;
		bool flag16 = flag15;
		if (flag16)
		{
			float num = 0f;
			bool flag17 = cfg.hitFullDamage && d.lethal && this.hitLearn.TryGetValue(key, out num) && num > d.hp + 0.5f;
			bool flag18 = flag17;
			bool flag19 = flag18;
			bool flag20 = flag19;
			bool flag21 = flag20;
			bool flag22 = flag21;
			bool flag23 = flag22;
			bool flag24 = flag23;
			if (flag24)
			{
				this.RegisterDamage(cfg, ht, num - d.hp, false, now, d.gt, true);
			}
		}
		else
		{
			bool flag25 = single && d.stam <= 0.001f && d.hp > 0.5f && e.reaction != DamageReaction.Block;
			bool flag26 = flag25;
			bool flag27 = flag26;
			bool flag28 = flag27;
			bool flag29 = flag28;
			bool flag30 = flag29;
			bool flag31 = flag30;
			bool flag32 = flag31;
			if (flag32)
			{
				this.hitLearn[key] = d.hp;
			}
		}
	}

	private void RegisterDamage(ResourcePackManager.HpConfig cfg, ResourcePackManager.HitTrack ht, float amount, bool stamina, float now, float gt, bool extra)
	{
		bool flag;
		if (stamina)
		{
			flag = (ht.enemy ? cfg.hitStamEnemy : cfg.hitStamOwn);
		}
		else
		{
			bool building = ht.building;
			bool flag2 = building;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				flag = (ht.enemy ? cfg.hitBldEnemy : cfg.hitBldOwn);
			}
			else
			{
				flag = (ht.enemy ? cfg.hitEnemy : cfg.hitOwn);
			}
		}
		bool flag9 = flag && amount >= 0.5f;
		bool flag10 = flag9;
		bool flag11 = flag10;
		bool flag12 = flag11;
		bool flag13 = flag12;
		bool flag14 = flag13;
		bool flag15 = flag14;
		bool flag16 = flag15;
		if (flag16)
		{
			Vector3 vector = ht.lastWorld;
			bool flag17 = ht.tr != null && ht.tr.gameObject.activeInHierarchy;
			bool flag18 = flag17;
			bool flag19 = flag18;
			bool flag20 = flag19;
			bool flag21 = flag20;
			bool flag22 = flag21;
			bool flag23 = flag22;
			bool flag24 = flag23;
			if (flag24)
			{
				vector = ht.tr.TransformPoint(ht.localOffset);
				ht.lastWorld = vector;
			}
			ResourcePackManager.HitFloat hitFloat = stamina ? ht.lastStamFloat : ht.lastFloat;
			bool flag25 = hitFloat != null && !hitFloat.dps && now - hitFloat.lastHit < 0.25f && this.hitFloats.Contains(hitFloat);
			bool flag26 = flag25;
			bool flag27 = flag26;
			bool flag28 = flag27;
			bool flag29 = flag28;
			bool flag30 = flag29;
			bool flag31 = flag30;
			bool flag32 = flag31;
			if (flag32)
			{
				hitFloat.amount += amount;
				hitFloat.t0 = now;
				hitFloat.lastHit = now;
			}
			else
			{
				hitFloat = new ResourcePackManager.HitFloat();
				hitFloat.tr = ht.tr;
				hitFloat.localOffset = ht.localOffset;
				hitFloat.world = vector;
				hitFloat.enemy = ht.enemy;
				hitFloat.amount = amount;
				hitFloat.t0 = now;
				hitFloat.lastHit = now;
				hitFloat.dps = false;
				hitFloat.stam = stamina;
				this.hitFloats.Add(hitFloat);
				if (stamina)
				{
					ht.lastStamFloat = hitFloat;
				}
				else
				{
					ht.lastFloat = hitFloat;
				}
			}
			hitFloat.text = "-" + Mathf.Max(1, Mathf.RoundToInt(hitFloat.amount)).ToString();
		}
		bool flag33 = ResourcePackManager.HitDpsOn(cfg, ht) && (!stamina || cfg.dpsStamina);
		bool flag34 = flag33;
		bool flag35 = flag34;
		bool flag36 = flag35;
		bool flag37 = flag36;
		bool flag38 = flag37;
		bool flag39 = flag38;
		bool flag40 = flag39;
		if (flag40)
		{
			if (extra)
			{
				bool flag41 = ht.burstHits == 0;
				bool flag42 = flag41;
				bool flag43 = flag42;
				bool flag44 = flag43;
				bool flag45 = flag44;
				bool flag46 = flag45;
				bool flag47 = flag46;
				bool flag48 = flag47;
				if (flag48)
				{
					return;
				}
			}
			else
			{
				bool flag49 = ht.burstHits > 0 && gt - ht.burstLast >= cfg.dpsSilence;
				bool flag50 = flag49;
				bool flag51 = flag50;
				bool flag52 = flag51;
				bool flag53 = flag52;
				bool flag54 = flag53;
				bool flag55 = flag54;
				bool flag56 = flag55;
				if (flag56)
				{
					this.FlushBurst(ht, now);
				}
				bool flag57 = ht.burstHits == 0;
				bool flag58 = flag57;
				bool flag59 = flag58;
				bool flag60 = flag59;
				bool flag61 = flag60;
				bool flag62 = flag61;
				bool flag63 = flag62;
				bool flag64 = flag63;
				if (flag64)
				{
					ht.burstStart = gt;
					ht.burstTotal = 0f;
					ht.burstHits = 1;
				}
				else
				{
					bool flag65 = gt > ht.burstLast + 0.0001f;
					bool flag66 = flag65;
					bool flag67 = flag66;
					bool flag68 = flag67;
					bool flag69 = flag68;
					bool flag70 = flag69;
					bool flag71 = flag70;
					bool flag72 = flag71;
					if (flag72)
					{
						ht.burstHits++;
					}
				}
				ht.burstLast = Mathf.Max(ht.burstLast, gt);
			}
			ht.burstTotal += amount;
		}
	}

	private bool AltShow(ResourcePackManager.HpConfig cfg, bool group)
	{
		return cfg == null || !cfg.altOnly || !group || Input.GetKey(KeyCode.LeftAlt);
	}

	private bool AlwaysOwnNow(ResourcePackManager.HpConfig cfg)
	{
		return cfg.alwaysOwn && this.AltShow(cfg, cfg.altRings);
	}

	private bool AlwaysEnemyNow(ResourcePackManager.HpConfig cfg)
	{
		return cfg.alwaysEnemy && this.AltShow(cfg, cfg.altRings);
	}

	private void HandleHitGone(ResourcePackManager.HpConfig cfg, ResourcePackManager.HitTrack ht, float now, float gt)
	{
		bool flag = ht == null || ht.goneDone || !ht.building;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			bool flag9 = ht.dr == null || ht.tr == null || !ht.tr.gameObject.activeInHierarchy;
			bool flag10 = !flag9;
			bool flag11 = !flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			bool flag17 = flag16;
			if (flag17)
			{
				ht.goneDone = true;
				bool flag18 = ht.lastHp <= 0.5f || now - ht.lastEvtAt > 1.5f;
				bool flag19 = !flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				bool flag25 = flag24;
				if (flag25)
				{
					float lastHp = ht.lastHp;
					this.RegisterDamage(cfg, ht, lastHp, false, now, gt, false);
					float num = 0f;
					bool flag26 = cfg.hitFullDamage && ht.lastEvtKey != null && this.hitLearn.TryGetValue(ht.lastEvtKey + ">" + ht.victimKey, out num) && num > lastHp + 0.5f;
					bool flag27 = flag26;
					bool flag28 = flag27;
					bool flag29 = flag28;
					bool flag30 = flag29;
					bool flag31 = flag30;
					bool flag32 = flag31;
					bool flag33 = flag32;
					if (flag33)
					{
						this.RegisterDamage(cfg, ht, num - lastHp, false, now, gt, true);
					}
					ht.lastHp = 0f;
					bool flag34 = ht.deadAt <= 0f;
					bool flag35 = flag34;
					bool flag36 = flag35;
					bool flag37 = flag36;
					bool flag38 = flag37;
					bool flag39 = flag38;
					bool flag40 = flag39;
					bool flag41 = flag40;
					if (flag41)
					{
						ht.deadAt = now;
					}
				}
			}
		}
	}
}
}
