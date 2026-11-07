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

	private static Color ShiftColor(Color c, float s)
	{
		bool flag = s >= 0f;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		Color result;
		if (flag8)
		{
			result = new Color(Mathf.Lerp(c.r, 1f, s), Mathf.Lerp(c.g, 1f, s), Mathf.Lerp(c.b, 1f, s), c.a);
		}
		else
		{
			float num = 1f + s;
			result = new Color(c.r * num, c.g * num, c.b * num, c.a);
		}
		return result;
	}

	private static ResourcePackManager.HpPalette BuildHpPalette(List<ResourcePackManager.HpStop> src, float secondBrightness)
	{
		List<ResourcePackManager.HpStop> list = new List<ResourcePackManager.HpStop>(src);
		ResourcePackManager.SortHpStops(list);
		ResourcePackManager.HpPalette hpPalette = new ResourcePackManager.HpPalette();
		hpPalette.mins = new float[list.Count];
		hpPalette.main = new Color[list.Count];
		hpPalette.second = new Color[list.Count];
		for (int i = 0; i < list.Count; i++)
		{
			Color col = list[i].col;
			hpPalette.mins[i] = list[i].pct;
			hpPalette.main[i] = col;
			hpPalette.second[i] = new Color(col.r * secondBrightness, col.g * secondBrightness, col.b * secondBrightness, col.a);
		}
		return hpPalette;
	}

	private static bool IsJob(Labor labor, Occupation.Job job)
	{
		return labor != null && labor.Occupation != null && labor.Occupation.CurrentJob == job;
	}

	private static void ResolveRingUnit(ResourcePackManager.HpRing ring)
	{
		Transform transform = (ring.owner != null) ? ring.owner.transform.parent : null;
		int num = (transform != null) ? transform.GetInstanceID() : 0;
		bool flag = num == ring.parentId;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			ring.parentId = num;
			ring.labor = ((transform != null) ? transform.GetComponent<Labor>() : null);
			ring.building = (transform != null && ring.labor == null && transform.GetComponent<DamageReceiver>() != null);
			ring.allied = false;
			bool flag9 = ring.labor != null;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				try
				{
					ring.allied = Affiliation.BelongsToAllied(transform.gameObject);
				}
				catch (Exception)
				{
				}
			}
		}
	}

	private static Dictionary<GameObject, SelectionCircle> GetDispenserMap()
	{
		SelectionCircleDispenser selectionCircleDispenser = SelectionCircleDispenser.Instance;
		bool flag = selectionCircleDispenser == null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		Dictionary<GameObject, SelectionCircle> result;
		if (flag8)
		{
			result = null;
		}
		else
		{
			bool flag9 = ResourcePackManager.dispenserMapField == null;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				ResourcePackManager.dispenserMapField = typeof(SelectionCircleDispenser).GetField("highlightByGameObject", BindingFlags.Instance | BindingFlags.NonPublic);
			}
			result = ((ResourcePackManager.dispenserMapField != null) ? (ResourcePackManager.dispenserMapField.GetValue(selectionCircleDispenser) as Dictionary<GameObject, SelectionCircle>) : null);
		}
		return result;
	}

	private void RegisterGameRings()
	{
		bool flag = this.stamReflFailed;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			bool flag9 = !ResourcePackManager.InitHpReflection();
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				this.stamReflFailed = true;
			}
			else
			{
				Dictionary<GameObject, SelectionCircle> dispenserMap = ResourcePackManager.GetDispenserMap();
				bool flag17 = dispenserMap == null;
				bool flag18 = !flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				if (flag24)
				{
					foreach (KeyValuePair<GameObject, SelectionCircle> keyValuePair in dispenserMap)
					{
						bool flag25 = keyValuePair.Value != null;
						bool flag26 = flag25;
						bool flag27 = flag26;
						bool flag28 = flag27;
						bool flag29 = flag28;
						bool flag30 = flag29;
						bool flag31 = flag30;
						bool flag32 = flag31;
						if (flag32)
						{
							this.TryRegisterRing(keyValuePair.Value);
						}
					}
				}
			}
		}
	}

	private void ApplyStaminaRules()
	{
		ResourcePackManager.HpConfig hpConfig = this.hpCfg;
		bool flag = hpConfig == null;
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
			bool flag9 = unscaledTime >= this.stamNextRegister;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				this.stamNextRegister = unscaledTime + 0.25f;
				this.RegisterGameRings();
			}
			for (int i = 0; i < this.hpRings.Count; i++)
			{
				ResourcePackManager.HpRing hpRing = this.hpRings[i];
				bool flag17 = hpRing.owner == null || hpRing.rend == null || hpRing.anim == null || !hpRing.rend.enabled || !hpRing.rend.gameObject.activeInHierarchy;
				bool flag18 = !flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				if (flag24)
				{
					ResourcePackManager.ResolveRingUnit(hpRing);
					bool flag25 = hpRing.labor == null;
					bool flag26 = !flag25;
					bool flag27 = flag26;
					bool flag28 = flag27;
					bool flag29 = flag28;
					bool flag30 = flag29;
					bool flag31 = flag30;
					bool flag32 = flag31;
					if (flag32)
					{
						bool flag33 = ResourcePackManager.IsJob(hpRing.labor, Occupation.Job.Alchemist);
						bool flag34 = flag33;
						bool flag35 = flag34;
						bool flag36 = flag35;
						bool flag37 = flag36;
						bool flag38 = flag37;
						bool flag39 = flag38;
						bool flag40 = flag39;
						bool flag41;
						if (flag40)
						{
							flag41 = (!hpRing.allied || !hpConfig.ammoRing);
						}
						else
						{
							flag41 = (hpConfig.knightStaminaOnly && !ResourcePackManager.IsJob(hpRing.labor, Occupation.Job.Knight));
						}
						bool flag42 = flag41 && hpRing.anim.GetBool(ResourcePackManager.hpShowStamId);
						bool flag43 = flag42;
						bool flag44 = flag43;
						bool flag45 = flag44;
						bool flag46 = flag45;
						bool flag47 = flag46;
						bool flag48 = flag47;
						bool flag49 = flag48;
						if (flag49)
						{
							hpRing.anim.SetBool(ResourcePackManager.hpShowStamId, false);
							hpRing.forcedStam = false;
						}
					}
				}
			}
		}
	}

	private void UpdateAmmoUnits()
	{
		ResourcePackManager.HpConfig hpConfig = this.hpCfg;
		bool flag = hpConfig == null || !hpConfig.ammoNumber || this.HiddenByAlt(hpConfig, hpConfig.altAmmo);
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			bool flag9 = this.ammoUnits.Count > 0;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				this.ammoUnits.Clear();
			}
		}
		else
		{
			float unscaledTime = Time.unscaledTime;
			bool flag17 = unscaledTime < this.ammoNextScan;
			bool flag18 = !flag17;
			bool flag19 = flag18;
			bool flag20 = flag19;
			bool flag21 = flag20;
			bool flag22 = flag21;
			bool flag23 = flag22;
			bool flag24 = flag23;
			if (flag24)
			{
				this.ammoNextScan = unscaledTime + 0.2f;
				this.ammoUnits.Clear();
				ResourcePackManager.PruneDead<Labor, ResourcePackManager.AmmoUnit>(this.ammoCache, 200);
				this.CollectAmmoUnits(UIGameObserver.bricktrons);
				this.CollectAmmoUnits(UIGameObserver.alliedBricktrons);
			}
		}
	}

	private void CollectAmmoUnits(UIGameObserver.Bricktron observer)
	{
		bool flag = observer == null;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			foreach (Labor labor in observer.CheckCreation())
			{
				bool flag9 = labor == null || !labor.gameObject.activeInHierarchy || !ResourcePackManager.IsJob(labor, Occupation.Job.Alchemist);
				bool flag10 = !flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					bool flag17 = !Affiliation.BelongsToAllied(labor.gameObject);
					bool flag18 = !flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					bool flag21 = flag20;
					bool flag22 = flag21;
					bool flag23 = flag22;
					bool flag24 = flag23;
					if (flag24)
					{
						BricktronDamageReceiver bricktronDamageReceiver = labor.damageReceiver as BricktronDamageReceiver;
						bool flag25 = bricktronDamageReceiver != null && bricktronDamageReceiver.CurrentHP <= 0f;
						bool flag26 = !flag25;
						bool flag27 = flag26;
						bool flag28 = flag27;
						bool flag29 = flag28;
						bool flag30 = flag29;
						bool flag31 = flag30;
						bool flag32 = flag31;
						if (flag32)
						{
							ResourcePackManager.AmmoUnit ammoUnit;
							bool flag33 = !this.ammoCache.TryGetValue(labor, out ammoUnit) || ammoUnit.state == null;
							bool flag34 = flag33;
							bool flag35 = flag34;
							bool flag36 = flag35;
							bool flag37 = flag36;
							bool flag38 = flag37;
							bool flag39 = flag38;
							bool flag40 = flag39;
							if (flag40)
							{
								ammoUnit = new ResourcePackManager.AmmoUnit();
								ammoUnit.labor = labor;
								ammoUnit.state = labor.GetComponent<CharacterState>();
								ammoUnit.localOffset = Vector3.zero;
								try
								{
									bool flag41 = ResourcePackManager.circleOffsetMethod == null;
									bool flag42 = flag41;
									bool flag43 = flag42;
									bool flag44 = flag43;
									bool flag45 = flag44;
									bool flag46 = flag45;
									bool flag47 = flag46;
									bool flag48 = flag47;
									if (flag48)
									{
										ResourcePackManager.circleOffsetMethod = typeof(SelectionCircle).GetMethod("GetCircleOffset", BindingFlags.Static | BindingFlags.NonPublic);
									}
									bool flag49 = ResourcePackManager.circleOffsetMethod != null;
									bool flag50 = flag49;
									bool flag51 = flag50;
									bool flag52 = flag51;
									bool flag53 = flag52;
									bool flag54 = flag53;
									bool flag55 = flag54;
									bool flag56 = flag55;
									if (flag56)
									{
										ammoUnit.localOffset = (Vector3)ResourcePackManager.circleOffsetMethod.Invoke(null, new object[]
										{
											labor.gameObject
										});
									}
								}
								catch (Exception)
								{
								}
								this.ammoCache[labor] = ammoUnit;
							}
							bool flag57 = ammoUnit.state != null;
							bool flag58 = flag57;
							bool flag59 = flag58;
							bool flag60 = flag59;
							bool flag61 = flag60;
							bool flag62 = flag61;
							bool flag63 = flag62;
							bool flag64 = flag63;
							if (flag64)
							{
								this.ammoUnits.Add(ammoUnit);
							}
						}
					}
				}
			}
		}
	}

	private void DrawAmmoNumbers()
	{
		ResourcePackManager.HpConfig hpConfig = this.hpCfg;
		bool flag = hpConfig == null || !hpConfig.ammoNumber || this.ammoUnits.Count == 0 || Event.current.type != EventType.Repaint;
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
				int num2 = Mathf.Max(8, Mathf.RoundToInt(28f * hpConfig.numSize * num));
				bool flag17 = this.ammoStyle == null;
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				if (flag24)
				{
					this.ammoStyle = new GUIStyle(GUI.skin.label);
					this.ammoStyle.alignment = TextAnchor.MiddleCenter;
					this.ammoStyle.wordWrap = false;
					this.ammoStyle.clipping = TextClipping.Overflow;
				}
				this.ammoStyle.fontSize = num2;
				this.ammoStyle.fontStyle = (hpConfig.numBold ? FontStyle.Bold : FontStyle.Normal);
				GUI.depth = 5;
				Color color = GUI.color;
				float num3 = Mathf.Max(1f, (float)num2 * 0.06f);
				for (int i = 0; i < this.ammoUnits.Count; i++)
				{
					ResourcePackManager.AmmoUnit ammoUnit = this.ammoUnits[i];
					bool flag25 = ammoUnit.labor == null || ammoUnit.state == null || !ammoUnit.labor.gameObject.activeInHierarchy;
					bool flag26 = !flag25;
					bool flag27 = flag26;
					bool flag28 = flag27;
					bool flag29 = flag28;
					bool flag30 = flag29;
					bool flag31 = flag30;
					bool flag32 = flag31;
					if (flag32)
					{
						Vector3 vector = main.WorldToScreenPoint(ammoUnit.labor.transform.TransformPoint(ammoUnit.localOffset));
						bool flag33 = vector.z <= 0f;
						bool flag34 = !flag33;
						bool flag35 = flag34;
						bool flag36 = flag35;
						bool flag37 = flag36;
						bool flag38 = flag37;
						bool flag39 = flag38;
						bool flag40 = flag39;
						if (flag40)
						{
							int num4 = Mathf.Clamp(ammoUnit.state.AmmoCount, 0, 99);
							float num5 = vector.x + hpConfig.numX * num;
							float num6 = (float)Screen.height - vector.y - hpConfig.numY * num;
							Rect position = new Rect(num5 - (float)num2 * 2f, num6 - (float)num2, (float)num2 * 4f, (float)num2 * 2f);
							Color numColor = hpConfig.numColor;
							bool flag41 = hpConfig.numByAmmo && hpConfig.ammo.mins.Length != 0;
							bool flag42 = flag41;
							bool flag43 = flag42;
							bool flag44 = flag43;
							bool flag45 = flag44;
							bool flag46 = flag45;
							bool flag47 = flag46;
							bool flag48 = flag47;
							if (flag48)
							{
								Color color2 = hpConfig.ammo.main[hpConfig.ammo.Index((float)num4 * 10f)];
								numColor = new Color(color2.r, color2.g, color2.b, hpConfig.numColor.a);
							}
							string text = num4.ToString();
							bool numOutline = hpConfig.numOutline;
							bool flag49 = numOutline;
							bool flag50 = flag49;
							bool flag51 = flag50;
							bool flag52 = flag51;
							bool flag53 = flag52;
							bool flag54 = flag53;
							bool flag55 = flag54;
							if (flag55)
							{
								GUI.color = new Color(0f, 0f, 0f, numColor.a);
								GUI.Label(new Rect(position.x - num3, position.y, position.width, position.height), text, this.ammoStyle);
								GUI.Label(new Rect(position.x + num3, position.y, position.width, position.height), text, this.ammoStyle);
								GUI.Label(new Rect(position.x, position.y - num3, position.width, position.height), text, this.ammoStyle);
								GUI.Label(new Rect(position.x, position.y + num3, position.width, position.height), text, this.ammoStyle);
							}
							GUI.color = numColor;
							GUI.Label(position, text, this.ammoStyle);
						}
					}
				}
				GUI.color = color;
			}
		}
	}

	private float SliderRow(Event e, ref float y, float w, float rowH, int fs, Color old, string label, int id, float value, float min, float max, float step, string format, string prefix, string suffix)
	{
		label = BtcLang.Tr(label);
		Rect row = new Rect(0f, y, w, rowH);
		this.DrawRowBackground(e, row, false, old);
		GUI.Label(new Rect((float)fs, y, w * 0.42f, rowH), label, this.menuLabelStyle);
		Rect hit = new Rect(w * 0.47f, y, w * 0.53f - (float)fs * 4.5f, rowH);
		float result = this.SliderCore(e, hit, id, value, min, max, step, old);
		GUI.Label(new Rect(hit.xMax + (float)fs * 0.3f, y, (float)fs * 4.2f, rowH), prefix + result.ToString(format, CultureInfo.InvariantCulture) + suffix, this.menuValueStyle);
		y += rowH;
		return result;
	}

	private void TogglePickerNum()
	{
		bool flag = this.pickNum;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			this.pickNum = false;
			this.pickExtra = 0;
		}
		else
		{
			this.pickNum = true;
			this.pickExtra = 0;
			this.pickStop = null;
			this.pickOv = null;
			Color.RGBToHSV(this.PickedColor(), out this.pickH, out this.pickS, out this.pickV);
			this.svTexHue = -1f;
		}
	}

	private void DrawPaletteGroup(Event e, ref float y, float w, float rowH, int fs, Color old, int idx, string title, List<ResourcePackManager.HpStop> list)
	{
		Rect rect = new Rect(0f, y, w, rowH);
		y += rowH;
		this.DrawRowBackground(e, rect, false, old);
		bool flag = GUI.Button(rect, (this.palOpen[idx] ? "   v " : "   > ") + BtcLang.Tr(title), this.menuItemStyle);
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			this.palOpen[idx] = !this.palOpen[idx];
		}
		bool flag9 = !this.palOpen[idx];
		bool flag10 = !flag9;
		bool flag11 = flag10;
		bool flag12 = flag11;
		bool flag13 = flag12;
		bool flag14 = flag13;
		bool flag15 = flag14;
		bool flag16 = flag15;
		if (flag16)
		{
			ResourcePackManager.HpConfig hpConfig = this.hpCfg;
			bool flag17 = idx == 0 || idx == 2 || idx == 4 || idx == 5;
			bool flag18 = flag17;
			bool flag19 = flag18;
			bool flag20 = flag19;
			bool flag21 = flag20;
			bool flag22 = flag21;
			bool flag23 = flag22;
			bool flag24 = flag23;
			if (flag24)
			{
				float num = (idx == 0) ? hpConfig.secondBrightness : ((idx == 2) ? hpConfig.stamSecondBrightness : ((idx == 5) ? hpConfig.bldSecondBrightness : hpConfig.ammoSecondBrightness));
				float num2 = this.SliderRow(e, ref y, w, rowH, fs, old, "   2nd color brightness", (idx == 0) ? 11 : ((idx == 2) ? 13 : ((idx == 5) ? 15 : 14)), num, 0f, 1.5f, 0.05f, "0.00", string.Empty, string.Empty);
				bool flag25 = num2 != num;
				bool flag26 = flag25;
				bool flag27 = flag26;
				bool flag28 = flag27;
				bool flag29 = flag28;
				bool flag30 = flag29;
				bool flag31 = flag30;
				bool flag32 = flag31;
				if (flag32)
				{
					bool flag33 = idx == 0;
					bool flag34 = flag33;
					bool flag35 = flag34;
					bool flag36 = flag35;
					bool flag37 = flag36;
					bool flag38 = flag37;
					bool flag39 = flag38;
					bool flag40 = flag39;
					if (flag40)
					{
						hpConfig.secondBrightness = num2;
					}
					else
					{
						bool flag41 = idx == 2;
						bool flag42 = flag41;
						bool flag43 = flag42;
						bool flag44 = flag43;
						bool flag45 = flag44;
						bool flag46 = flag45;
						bool flag47 = flag46;
						bool flag48 = flag47;
						if (flag48)
						{
							hpConfig.stamSecondBrightness = num2;
						}
						else
						{
							bool flag49 = idx == 5;
							bool flag50 = flag49;
							bool flag51 = flag50;
							bool flag52 = flag51;
							bool flag53 = flag52;
							bool flag54 = flag53;
							bool flag55 = flag54;
							bool flag56 = flag55;
							if (flag56)
							{
								hpConfig.bldSecondBrightness = num2;
							}
							else
							{
								hpConfig.ammoSecondBrightness = num2;
							}
						}
					}
					this.HpPaletteEdited();
				}
			}
			this.DrawStopRows(e, ref y, w, rowH, fs, old, list, idx);
		}
	}

	private bool Section(Event e, ref float y, float w, float rowH, Color old, string label, int idx)
	{
		bool flag = this.hpSection == idx;
		Rect rect = new Rect(0f, y, w, rowH);
		y += rowH;
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
			GUI.color = (flag ? new Color(0.55f, 0.85f, 0.55f, 0.12f) : new Color(1f, 1f, 1f, 0.04f));
			GUI.DrawTexture(rect, this.whiteTex);
			GUI.color = old;
		}
		this.DrawRowBackground(e, rect, false, old);
		bool flag10 = GUI.Button(rect, (flag ? "v " : "> ") + BtcLang.Tr(label), flag ? this.menuActiveStyle : this.menuItemStyle);
		bool flag11 = flag10;
		bool flag12 = flag11;
		bool flag13 = flag12;
		bool flag14 = flag13;
		bool flag15 = flag14;
		bool flag16 = flag15;
		bool flag17 = flag16;
		if (flag17)
		{
			this.hpSection = (flag ? -1 : idx);
			flag = !flag;
		}
		return flag;
	}

	private static Vector3 GetRingOffset(GameObject go)
	{
		try
		{
			bool flag = ResourcePackManager.circleOffsetMethod == null;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				ResourcePackManager.circleOffsetMethod = typeof(SelectionCircle).GetMethod("GetCircleOffset", BindingFlags.Static | BindingFlags.NonPublic);
			}
			bool flag9 = ResourcePackManager.circleOffsetMethod != null;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				return (Vector3)ResourcePackManager.circleOffsetMethod.Invoke(null, new object[]
				{
					go
				});
			}
		}
		catch (Exception)
		{
		}
		return Vector3.zero;
	}

	private void UpdateStatUnits()
	{
		ResourcePackManager.HpConfig hpConfig = this.hpCfg;
		bool flag = hpConfig != null && (hpConfig.hpNumOwn || hpConfig.stamNumOwn || hpConfig.regenNumOwn);
		bool flag2 = hpConfig != null && (hpConfig.hpNumEnemy || hpConfig.stamNumEnemy || hpConfig.regenNumEnemy);
		bool flag3 = (!flag && !flag2) || this.HiddenByAlt(hpConfig, hpConfig.altNumbers);
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		bool flag9 = flag8;
		bool flag10 = flag9;
		if (flag10)
		{
			bool flag11 = this.statUnits.Count > 0;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			bool flag17 = flag16;
			bool flag18 = flag17;
			if (flag18)
			{
				this.statUnits.Clear();
			}
		}
		else
		{
			float unscaledTime = Time.unscaledTime;
			bool flag19 = unscaledTime < this.statNextScan;
			bool flag20 = !flag19;
			bool flag21 = flag20;
			bool flag22 = flag21;
			bool flag23 = flag22;
			bool flag24 = flag23;
			bool flag25 = flag24;
			bool flag26 = flag25;
			if (flag26)
			{
				this.statNextScan = unscaledTime + 0.2f;
				this.statUnits.Clear();
				this.statSeen.Clear();
				ResourcePackManager.PruneDead<Labor, ResourcePackManager.StatUnit>(this.statCache, 300);
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
					this.CollectStatUnits(UIGameObserver.bricktrons, false);
					this.CollectStatUnits(UIGameObserver.alliedBricktrons, false);
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
					this.CollectStatUnits(UIGameObserver.enemyBricktrons, true);
				}
			}
		}
	}

	private void CollectStatUnits(UIGameObserver.Bricktron observer, bool enemy)
	{
		if (enemy && ResourcePackManager.HideEnemyHud()) return;
		bool flag = observer == null;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			foreach (Labor labor in observer.CheckCreation())
			{
				bool flag9 = labor == null || !labor.gameObject.activeInHierarchy || !this.statSeen.Add(labor);
				bool flag10 = !flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					BricktronDamageReceiver bricktronDamageReceiver = labor.damageReceiver as BricktronDamageReceiver;
					bool flag17 = bricktronDamageReceiver == null || bricktronDamageReceiver.CurrentHP <= 0f;
					bool flag18 = !flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					bool flag21 = flag20;
					bool flag22 = flag21;
					bool flag23 = flag22;
					bool flag24 = flag23;
					if (flag24)
					{
						bool flag25 = enemy && (labor.vision == null || !labor.vision.IsVisibleForMe());
						bool flag26 = !flag25;
						bool flag27 = flag26;
						bool flag28 = flag27;
						bool flag29 = flag28;
						bool flag30 = flag29;
						bool flag31 = flag30;
						bool flag32 = flag31;
						if (flag32)
						{
							ResourcePackManager.StatUnit statUnit;
							bool flag33 = !this.statCache.TryGetValue(labor, out statUnit) || statUnit.dr == null;
							bool flag34 = flag33;
							bool flag35 = flag34;
							bool flag36 = flag35;
							bool flag37 = flag36;
							bool flag38 = flag37;
							bool flag39 = flag38;
							bool flag40 = flag39;
							if (flag40)
							{
								statUnit = new ResourcePackManager.StatUnit();
								statUnit.labor = labor;
								statUnit.dr = bricktronDamageReceiver;
								statUnit.localOffset = ResourcePackManager.GetRingOffset(labor.gameObject);
								this.statCache[labor] = statUnit;
							}
							statUnit.enemy = enemy;
							statUnit.knight = ResourcePackManager.IsJob(labor, Occupation.Job.Knight);
							this.statUnits.Add(statUnit);
						}
					}
				}
			}
		}
	}

	private static string FormatStat(float cur, float max, int fmt)
	{
		int num = Mathf.Max(0, Mathf.CeilToInt(cur - 0.001f));
		bool flag = fmt == 0;
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
			result = num.ToString();
		}
		else
		{
			bool flag9 = fmt == 1;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				result = num.ToString() + "/" + Mathf.RoundToInt(max).ToString();
			}
			else
			{
				result = ((max > 0f) ? Mathf.RoundToInt(cur / max * 100f) : 0).ToString() + "%";
			}
		}
		return result;
	}

	private static Color StatColor(ResourcePackManager.HpConfig cfg, bool hp, bool enemy, float ratio, Color fixedCol, bool byPalette)
	{
		Color result = fixedCol;
		if (byPalette)
		{
			bool flag = enemy && cfg.enemyPalette;
			ResourcePackManager.HpPalette hpPalette = hp ? (flag ? cfg.enemy : cfg.own) : (flag ? cfg.stamEnemy : cfg.stamOwn);
			bool flag2 = hpPalette.mins.Length != 0;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			bool flag9 = flag8;
			if (flag9)
			{
				Color color = hpPalette.main[hpPalette.Index(ratio * 100f)];
				result = new Color(color.r, color.g, color.b, fixedCol.a);
			}
		}
		result.a = fixedCol.a * cfg.statAlpha;
		return result;
	}

	// Outline quality for stat/building numbers: false = 1 shadow label (cheap, default), true = legacy 4-label outline.
	private static readonly bool BtcStatFullOutline = false;

	private void DrawStatLabel(ResourcePackManager.HpConfig cfg, Vector3 sp, float k, int fs, float ow, float offX, float offY, string text, Color col)
	{
		float num = sp.x + offX * k;
		float num2 = (float)Screen.height - sp.y - offY * k;
		Rect position = new Rect(num - (float)fs * 3f, num2 - (float)fs, (float)fs * 6f, (float)fs * 2f);
		bool statOutline = cfg.statOutline;
		bool flag = statOutline;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		if (flag7)
		{
			GUI.color = new Color(0f, 0f, 0f, col.a);
			GUI.Label(new Rect(position.x + ow, position.y + ow, position.width, position.height), text, this.statStyle);
			if (BtcStatFullOutline) GUI.Label(new Rect(position.x - ow, position.y, position.width, position.height), text, this.statStyle);
			if (BtcStatFullOutline) GUI.Label(new Rect(position.x + ow, position.y, position.width, position.height), text, this.statStyle);
			if (BtcStatFullOutline) GUI.Label(new Rect(position.x, position.y - ow, position.width, position.height), text, this.statStyle);
			if (BtcStatFullOutline) GUI.Label(new Rect(position.x, position.y + ow, position.width, position.height), text, this.statStyle);
		}
		GUI.color = col;
		GUI.Label(position, text, this.statStyle);
	}

	private void DrawStatNumbers()
	{
		ResourcePackManager.HpConfig hpConfig = this.hpCfg;
		bool flag = hpConfig == null || this.statUnits.Count == 0 || Event.current.type != EventType.Repaint;
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
				for (int i = 0; i < this.statUnits.Count; i++)
				{
					ResourcePackManager.StatUnit statUnit = this.statUnits[i];
					bool flag25 = statUnit.labor == null || statUnit.dr == null || !statUnit.labor.gameObject.activeInHierarchy;
					bool flag26 = !flag25;
					bool flag27 = flag26;
					bool flag28 = flag27;
					bool flag29 = flag28;
					bool flag30 = flag29;
					bool flag31 = flag30;
					bool flag32 = flag31;
					if (flag32)
					{
						bool flag33 = hpConfig.statHideFar && (statUnit.labor.transform.position - position).sqrMagnitude > num3;
						bool flag34 = !flag33;
						bool flag35 = flag34;
						bool flag36 = flag35;
						bool flag37 = flag36;
						bool flag38 = flag37;
						bool flag39 = flag38;
						bool flag40 = flag39;
						if (flag40)
						{
							Vector3 vector = main.WorldToScreenPoint(statUnit.labor.transform.TransformPoint(statUnit.localOffset));
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
								bool flag49 = statUnit.enemy ? hpConfig.hpNumEnemy : hpConfig.hpNumOwn;
								bool flag50 = statUnit.enemy ? hpConfig.stamNumEnemy : hpConfig.stamNumOwn;
								bool flag51 = flag50 && hpConfig.stamNumKnightOnly && !statUnit.knight;
								bool flag52 = flag51;
								bool flag53 = flag52;
								bool flag54 = flag53;
								bool flag55 = flag54;
								bool flag56 = flag55;
								bool flag57 = flag56;
								bool flag58 = flag57;
								if (flag58)
								{
									flag50 = false;
								}
								float currentHP = statUnit.dr.CurrentHP;
								float maxHP = statUnit.dr.MaxHP;
								float currentStamina = statUnit.dr.CurrentStamina;
								float maxStamina = statUnit.dr.MaxStamina;
								bool flag59 = maxHP <= 0f || (hpConfig.statHideFull && currentHP >= maxHP - 0.01f);
								bool flag60 = flag59;
								bool flag61 = flag60;
								bool flag62 = flag61;
								bool flag63 = flag62;
								bool flag64 = flag63;
								bool flag65 = flag64;
								bool flag66 = flag65;
								if (flag66)
								{
									flag49 = false;
								}
								bool flag67 = maxStamina <= 0f || (hpConfig.statHideFull && currentStamina >= maxStamina - 0.01f);
								bool flag68 = flag67;
								bool flag69 = flag68;
								bool flag70 = flag69;
								bool flag71 = flag70;
								bool flag72 = flag71;
								bool flag73 = flag72;
								bool flag74 = flag73;
								if (flag74)
								{
									flag50 = false;
								}
								bool flag75 = statUnit.enemy ? hpConfig.regenNumEnemy : hpConfig.regenNumOwn;
								bool flag76 = flag75 && !ResourcePackManager.regenReflFail && ((maxHP > 0f && currentHP < maxHP - 0.01f) || (maxStamina > 0f && currentStamina < maxStamina - 0.01f));
								bool flag77 = flag76;
								bool flag78 = flag77;
								bool flag79 = flag78;
								bool flag80 = flag79;
								bool flag81 = flag80;
								bool flag82 = flag81;
								bool flag83 = flag82;
								if (flag83)
								{
									float unscaledTime = Time.unscaledTime;
									bool flag84 = unscaledTime - statUnit.regenRead > 0.1f;
									bool flag85 = flag84;
									bool flag86 = flag85;
									bool flag87 = flag86;
									bool flag88 = flag87;
									bool flag89 = flag88;
									bool flag90 = flag89;
									bool flag91 = flag90;
									if (flag91)
									{
										statUnit.regenRead = unscaledTime;
										statUnit.regenRem = this.ReadRegenRemaining(statUnit.dr);
									}
									float num4 = statUnit.regenRem - (unscaledTime - statUnit.regenRead);
									bool flag92 = num4 > 0.05f;
									bool flag93 = flag92;
									bool flag94 = flag93;
									bool flag95 = flag94;
									bool flag96 = flag95;
									bool flag97 = flag96;
									bool flag98 = flag97;
									bool flag99 = flag98;
									if (flag99)
									{
										Color regenNumColor = hpConfig.regenNumColor;
										regenNumColor.a = hpConfig.regenNumColor.a * hpConfig.statAlpha;
										this.DrawStatLabel(hpConfig, vector, num, num2, ow, 0f, hpConfig.regenNumY, ResourcePackManager.RegenText(num4), regenNumColor);
									}
								}
								bool flag100 = flag49;
								bool flag101 = flag100;
								bool flag102 = flag101;
								bool flag103 = flag102;
								bool flag104 = flag103;
								bool flag105 = flag104;
								bool flag106 = flag105;
								bool flag107 = flag106;
								if (flag107)
								{
									Color col = ResourcePackManager.StatColor(hpConfig, true, statUnit.enemy, currentHP / maxHP, hpConfig.hpNumColor, hpConfig.hpNumByPalette);
									this.DrawStatLabel(hpConfig, vector, num, num2, ow, hpConfig.hpNumX, hpConfig.hpNumY, statUnit.HpText(currentHP, maxHP, hpConfig.statFormat), col);
								}
								bool flag108 = flag50;
								bool flag109 = flag108;
								bool flag110 = flag109;
								bool flag111 = flag110;
								bool flag112 = flag111;
								bool flag113 = flag112;
								bool flag114 = flag113;
								bool flag115 = flag114;
								if (flag115)
								{
									Color col2 = ResourcePackManager.StatColor(hpConfig, false, statUnit.enemy, currentStamina / maxStamina, hpConfig.stamNumColor, hpConfig.stamNumByPalette);
									this.DrawStatLabel(hpConfig, vector, num, num2, ow, hpConfig.stamNumX, hpConfig.stamNumY, statUnit.StamText(currentStamina, maxStamina, hpConfig.statFormat), col2);
								}
							}
						}
					}
				}
				GUI.color = color;
			}
		}
	}

	private void UpdatePathUnits()
	{
		ResourcePackManager.HpConfig hpConfig = this.hpCfg;
		bool flag = hpConfig == null || !hpConfig.pathOwn;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			bool flag9 = this.pathUnits.Count > 0;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				this.pathUnits.Clear();
			}
			bool flag17 = this.trails.Count > 0;
			bool flag18 = flag17;
			bool flag19 = flag18;
			bool flag20 = flag19;
			bool flag21 = flag20;
			bool flag22 = flag21;
			bool flag23 = flag22;
			bool flag24 = flag23;
			if (flag24)
			{
				this.trails.Clear();
			}
			this.ClearPredictions();
		}
		else
		{
			float unscaledTime = Time.unscaledTime;
			bool flag25 = unscaledTime < this.pathNextScan;
			bool flag26 = !flag25;
			bool flag27 = flag26;
			bool flag28 = flag27;
			bool flag29 = flag28;
			bool flag30 = flag29;
			bool flag31 = flag30;
			bool flag32 = flag31;
			if (flag32)
			{
				this.pathNextScan = unscaledTime + 0.1f;
				this.pathScanId++;
				this.pathUnits.Clear();
				BtcOverlays.ClearPaths();
				this.pathSeen.Clear();
				this.pathSelected.Clear();
				bool pathSelectedOnly = hpConfig.pathSelectedOnly;
				bool flag33 = pathSelectedOnly;
				bool flag34 = flag33;
				bool flag35 = flag34;
				bool flag36 = flag35;
				bool flag37 = flag36;
				bool flag38 = flag37;
				bool flag39 = flag38;
				if (flag39)
				{
					this.CollectSelectedLabors();
				}
				this.AddPathUnits(UIGameObserver.bricktrons, hpConfig);
				this.AddPathUnits(UIGameObserver.alliedBricktrons, hpConfig);
				this.trailDrop.Clear();
				foreach (KeyValuePair<Labor, ResourcePackManager.TrailData> keyValuePair in this.trails)
				{
					bool flag40 = keyValuePair.Key == null || keyValuePair.Value.scanId != this.pathScanId;
					bool flag41 = flag40;
					bool flag42 = flag41;
					bool flag43 = flag42;
					bool flag44 = flag43;
					bool flag45 = flag44;
					bool flag46 = flag45;
					bool flag47 = flag46;
					if (flag47)
					{
						this.trailDrop.Add(keyValuePair.Key);
					}
				}
				for (int i = 0; i < this.trailDrop.Count; i++)
				{
					this.trails.Remove(this.trailDrop[i]);
				}
				this.DropDeadPredictions();
			}
		}
	}

	private void CollectSelectedLabors()
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
			for (int i = 0; i < this.hpRings.Count; i++)
			{
				ResourcePackManager.HpRing hpRing = this.hpRings[i];
				bool flag9 = hpRing.owner == null || hpRing.rend == null || hpRing.anim == null || !hpRing.rend.enabled || !hpRing.rend.gameObject.activeInHierarchy;
				bool flag10 = !flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					ResourcePackManager.ResolveRingUnit(hpRing);
					bool flag17 = hpRing.labor == null || !hpRing.allied;
					bool flag18 = !flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					bool flag21 = flag20;
					bool flag22 = flag21;
					bool flag23 = flag22;
					bool flag24 = flag23;
					if (flag24)
					{
						bool @bool = hpRing.anim.GetBool(ResourcePackManager.hpSelectedId);
						bool flag25 = @bool;
						bool flag26 = flag25;
						bool flag27 = flag26;
						bool flag28 = flag27;
						bool flag29 = flag28;
						bool flag30 = flag29;
						bool flag31 = flag30;
						if (flag31)
						{
							this.pathSelected.Add(hpRing.labor);
						}
					}
				}
			}
		}
	}

	private bool EnsurePathMat()
	{
		bool flag = this.pathMat != null;
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
			result = true;
		}
		else
		{
			bool flag9 = this.pathMatFailed;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				result = false;
			}
			else
			{
				Shader shader = Shader.Find("Hidden/Internal-Colored");
				bool flag17 = shader == null;
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				if (flag24)
				{
					shader = Shader.Find("Sprites/Default");
				}
				bool flag25 = shader == null;
				bool flag26 = flag25;
				bool flag27 = flag26;
				bool flag28 = flag27;
				bool flag29 = flag28;
				bool flag30 = flag29;
				bool flag31 = flag30;
				bool flag32 = flag31;
				if (flag32)
				{
					shader = Shader.Find("UI/Default");
				}
				bool flag33 = shader == null;
				bool flag34 = flag33;
				bool flag35 = flag34;
				bool flag36 = flag35;
				bool flag37 = flag36;
				bool flag38 = flag37;
				bool flag39 = flag38;
				bool flag40 = flag39;
				if (flag40)
				{
					this.pathMatFailed = true;
					ResourcePackManager.PackWarn("[ResourcePacks] Path lines: no suitable shader found");
					result = false;
				}
				else
				{
					this.pathMat = new Material(shader);
					this.pathMat.hideFlags = HideFlags.HideAndDontSave;
					bool flag41 = this.pathMat.HasProperty("_SrcBlend");
					bool flag42 = flag41;
					bool flag43 = flag42;
					bool flag44 = flag43;
					bool flag45 = flag44;
					bool flag46 = flag45;
					bool flag47 = flag46;
					bool flag48 = flag47;
					if (flag48)
					{
						this.pathMat.SetInt("_SrcBlend", 5);
					}
					bool flag49 = this.pathMat.HasProperty("_DstBlend");
					bool flag50 = flag49;
					bool flag51 = flag50;
					bool flag52 = flag51;
					bool flag53 = flag52;
					bool flag54 = flag53;
					bool flag55 = flag54;
					bool flag56 = flag55;
					if (flag56)
					{
						this.pathMat.SetInt("_DstBlend", 10);
					}
					bool flag57 = this.pathMat.HasProperty("_Cull");
					bool flag58 = flag57;
					bool flag59 = flag58;
					bool flag60 = flag59;
					bool flag61 = flag60;
					bool flag62 = flag61;
					bool flag63 = flag62;
					bool flag64 = flag63;
					if (flag64)
					{
						this.pathMat.SetInt("_Cull", 0);
					}
					bool flag65 = this.pathMat.HasProperty("_ZWrite");
					bool flag66 = flag65;
					bool flag67 = flag66;
					bool flag68 = flag67;
					bool flag69 = flag68;
					bool flag70 = flag69;
					bool flag71 = flag70;
					bool flag72 = flag71;
					if (flag72)
					{
						this.pathMat.SetInt("_ZWrite", 0);
					}
					result = true;
				}
			}
		}
		return result;
	}

	private static float PathHalfWidth(Vector3 p, Vector3 camPos, Vector3 camFwd, float perDepth, float ortho)
	{
		bool flag = ortho > 0f;
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
			result = ortho;
		}
		else
		{
			float num = Mathf.Max(0.1f, Vector3.Dot(p - camPos, camFwd));
			result = perDepth * num;
		}
		return result;
	}

	private static void PathQuad(Vector3 c, Vector3 ax, Vector3 ay)
	{
		GL.Vertex(c + ay);
		GL.Vertex(c + ax);
		GL.Vertex(c - ay);
		GL.Vertex(c - ax);
	}
}
}
