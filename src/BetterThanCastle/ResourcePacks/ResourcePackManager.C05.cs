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

	private static ResourcePackManager.HpOverride FindHpOverride(List<ResourcePackManager.HpOverride> list, string name)
	{
		for (int i = 0; i < list.Count; i++)
		{
			bool flag = list[i].name == name;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				return list[i];
			}
		}
		return null;
	}

	private void DrawOverrideRows(Event e, ref float y, float w, float rowH, int fs, Color old, List<ResourcePackManager.HpOverride> list, int team)
	{
		for (int i = 0; i < ResourcePackManager.HpKnownFloatProps.Length + ResourcePackManager.HpKnownColorProps.Length; i++)
		{
			bool flag = i >= ResourcePackManager.HpKnownFloatProps.Length;
			string text = flag ? ResourcePackManager.HpKnownColorProps[i - ResourcePackManager.HpKnownFloatProps.Length] : ResourcePackManager.HpKnownFloatProps[i];
			ResourcePackManager.HpOverride hpOverride = ResourcePackManager.FindHpOverride(list, text);
			Rect row = new Rect(0f, y, w, rowH);
			this.DrawRowBackground(e, row, false, old);
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
				float num = (float)fs * 1.1f;
				this.DrawCheckbox(new Rect((float)fs, y + (rowH - num) * 0.5f, num, num), hpOverride != null);
			}
			bool flag10 = GUI.Button(new Rect(0f, y, w * 0.5f, rowH), text, this.menuHatItemStyle);
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			bool flag17 = flag16;
			if (flag17)
			{
				bool flag18 = hpOverride == null;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				bool flag25 = flag24;
				if (flag25)
				{
					hpOverride = new ResourcePackManager.HpOverride();
					hpOverride.name = text;
					hpOverride.id = Shader.PropertyToID(text);
					hpOverride.color = flag;
					hpOverride.mult = !flag;
					hpOverride.f = 1f;
					hpOverride.c = new Color(0.35f, 0.05f, 0.05f, 1f);
					list.Add(hpOverride);
				}
				else
				{
					bool flag26 = this.pickOv == hpOverride;
					bool flag27 = flag26;
					bool flag28 = flag27;
					bool flag29 = flag28;
					bool flag30 = flag29;
					bool flag31 = flag30;
					bool flag32 = flag31;
					bool flag33 = flag32;
					if (flag33)
					{
						this.pickOv = null;
					}
					list.Remove(hpOverride);
					hpOverride = null;
				}
				this.HpStructureEdited();
			}
			bool flag34 = hpOverride != null;
			bool flag35 = flag34;
			bool flag36 = flag35;
			bool flag37 = flag36;
			bool flag38 = flag37;
			bool flag39 = flag38;
			bool flag40 = flag39;
			bool flag41 = flag40;
			if (flag41)
			{
				bool color = hpOverride.color;
				bool flag42 = color;
				bool flag43 = flag42;
				bool flag44 = flag43;
				bool flag45 = flag44;
				bool flag46 = flag45;
				bool flag47 = flag46;
				bool flag48 = flag47;
				if (flag48)
				{
					float num2 = (float)fs * 1.5f;
					Rect rect = new Rect(w * 0.52f, y + (rowH - num2 * 0.75f) * 0.5f, num2, num2 * 0.75f);
					this.SwatchBox(rect, hpOverride.c, this.pickOv == hpOverride);
					bool flag49 = GUI.Button(rect, GUIContent.none, GUIStyle.none);
					bool flag50 = flag49;
					bool flag51 = flag50;
					bool flag52 = flag51;
					bool flag53 = flag52;
					bool flag54 = flag53;
					bool flag55 = flag54;
					bool flag56 = flag55;
					if (flag56)
					{
						this.TogglePicker(null, hpOverride);
					}
					GUI.Label(new Rect(rect.xMax + (float)fs * 0.6f, y, (float)fs * 6f, rowH), ResourcePackManager.HpHex(hpOverride.c), this.menuLabelStyle);
				}
				else
				{
					Rect hit = new Rect(w * 0.52f, y, w * 0.48f - (float)fs * 5.5f, rowH);
					float min = hpOverride.mult ? 0.1f : 0f;
					float max = hpOverride.mult ? 4f : Mathf.Max(1f, hpOverride.f * 2f);
					float num3 = this.SliderCore(e, hit, 300 + team * 50 + i, hpOverride.f, min, max, hpOverride.mult ? 0.05f : 0.01f, old);
					bool flag57 = num3 != hpOverride.f;
					bool flag58 = flag57;
					bool flag59 = flag58;
					bool flag60 = flag59;
					bool flag61 = flag60;
					bool flag62 = flag61;
					bool flag63 = flag62;
					bool flag64 = flag63;
					if (flag64)
					{
						hpOverride.f = num3;
						this.MarkHpDirty();
					}
					GUI.Label(new Rect(hit.xMax + (float)fs * 0.3f, y, (float)fs * 5f, rowH), (hpOverride.mult ? "x" : "=") + hpOverride.f.ToString("0.00", CultureInfo.InvariantCulture), this.menuValueStyle);
				}
			}
			y += rowH;
			bool flag65 = hpOverride != null && hpOverride.color && this.pickOv == hpOverride;
			bool flag66 = flag65;
			bool flag67 = flag66;
			bool flag68 = flag67;
			bool flag69 = flag68;
			bool flag70 = flag69;
			bool flag71 = flag70;
			bool flag72 = flag71;
			if (flag72)
			{
				y = this.DrawPicker(e, y, w, fs, old);
			}
		}
	}
}
}
