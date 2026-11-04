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

	private void UpdateCenters()
	{
		ResourcePackManager.HpConfig hpConfig = this.hpCfg;
		bool flag = hpConfig == null || (!hpConfig.centerOn && !hpConfig.centerDist);
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			bool flag9 = this.centerShown.Count > 0;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				this.centerShown.Clear();
			}
		}
		else
		{
			float unscaledTime = Time.unscaledTime;
			bool flag17 = unscaledTime < this.centerNextScan;
			bool flag18 = !flag17;
			bool flag19 = flag18;
			bool flag20 = flag19;
			bool flag21 = flag20;
			bool flag22 = flag21;
			bool flag23 = flag22;
			bool flag24 = flag23;
			if (flag24)
			{
				this.centerNextScan = unscaledTime + (hpConfig.perfMode ? 0.5f : 0.25f);
				this.centerShown.Clear();
				this.centerSeen.Clear();
				ResourcePackManager.PruneDead<Transform, ResourcePackManager.CenterEnt>(this.centerCache, 1200);
				bool centerOwnUnits = hpConfig.centerOwnUnits;
				bool flag25 = centerOwnUnits;
				bool flag26 = flag25;
				bool flag27 = flag26;
				bool flag28 = flag27;
				bool flag29 = flag28;
				bool flag30 = flag29;
				bool flag31 = flag30;
				if (flag31)
				{
					this.CollectCenterUnits(UIGameObserver.bricktrons, false);
					this.CollectCenterUnits(UIGameObserver.alliedBricktrons, false);
				}
				bool centerEnemyUnits = hpConfig.centerEnemyUnits;
				bool flag32 = centerEnemyUnits;
				bool flag33 = flag32;
				bool flag34 = flag33;
				bool flag35 = flag34;
				bool flag36 = flag35;
				bool flag37 = flag36;
				bool flag38 = flag37;
				if (flag38)
				{
					this.CollectCenterUnits(UIGameObserver.enemyBricktrons, true);
				}
				bool centerOwnObjects = hpConfig.centerOwnObjects;
				bool flag39 = centerOwnObjects;
				bool flag40 = flag39;
				bool flag41 = flag40;
				bool flag42 = flag41;
				bool flag43 = flag42;
				bool flag44 = flag43;
				bool flag45 = flag44;
				if (flag45)
				{
					this.CollectCenterObjects(UIGameObserver.dynamicObjects, false);
				}
				bool centerEnemyObjects = hpConfig.centerEnemyObjects;
				bool flag46 = centerEnemyObjects;
				bool flag47 = flag46;
				bool flag48 = flag47;
				bool flag49 = flag48;
				bool flag50 = flag49;
				bool flag51 = flag50;
				bool flag52 = flag51;
				if (flag52)
				{
					this.CollectCenterObjects(UIGameObserver.ennemyDynamicObjects, true);
				}
			}
		}
	}

	private ResourcePackManager.CenterEnt GetCenterEnt(Transform t)
	{
		ResourcePackManager.CenterEnt centerEnt;
		bool flag = this.centerCache.TryGetValue(t, out centerEnt) && centerEnt.tr != null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		ResourcePackManager.CenterEnt result;
		if (flag8)
		{
			result = centerEnt;
		}
		else
		{
			centerEnt = new ResourcePackManager.CenterEnt();
			centerEnt.tr = t;
			centerEnt.skip = (t.GetComponent<BricktronDamageReceiver>() != null);
			foreach (Collider collider in t.GetComponentsInChildren<Collider>(true))
			{
				bool flag9 = collider == null || (1 << collider.gameObject.layer & 69632) == 0;
				bool flag10 = !flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					bool flag17 = !centerEnt.pts.Contains(collider.transform);
					bool flag18 = flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					bool flag21 = flag20;
					bool flag22 = flag21;
					bool flag23 = flag22;
					bool flag24 = flag23;
					if (flag24)
					{
						centerEnt.pts.Add(collider.transform);
					}
				}
			}
			this.centerCache[t] = centerEnt;
			result = centerEnt;
		}
		return result;
	}

	private void CollectCenterUnits(UIGameObserver.Bricktron observer, bool enemy)
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
				bool flag9 = labor == null || !labor.gameObject.activeInHierarchy || !this.centerSeen.Add(labor.transform);
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
					bool flag17 = bricktronDamageReceiver != null && bricktronDamageReceiver.CurrentHP <= 0f;
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
							ResourcePackManager.CenterEnt centerEnt = this.GetCenterEnt(labor.transform);
							centerEnt.skip = false;
							centerEnt.enemy = enemy;
							bool flag33 = centerEnt.pts.Count > 0;
							bool flag34 = flag33;
							bool flag35 = flag34;
							bool flag36 = flag35;
							bool flag37 = flag36;
							bool flag38 = flag37;
							bool flag39 = flag38;
							bool flag40 = flag39;
							if (flag40)
							{
								this.centerShown.Add(centerEnt);
							}
						}
					}
				}
			}
		}
	}

	private void CollectCenterObjects(UIGameObserver.DynamicObjects obs, bool enemy)
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
				bool flag9 = transform == null || !transform.gameObject.activeInHierarchy || !this.centerSeen.Add(transform);
				bool flag10 = !flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					ResourcePackManager.CenterEnt centerEnt = this.GetCenterEnt(transform);
					bool flag17 = centerEnt.skip || centerEnt.pts.Count == 0;
					bool flag18 = !flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					bool flag21 = flag20;
					bool flag22 = flag21;
					bool flag23 = flag22;
					bool flag24 = flag23;
					if (flag24)
					{
						if (enemy)
						{
							bool flag25 = !centerEnt.lvDone;
							bool flag26 = flag25;
							bool flag27 = flag26;
							bool flag28 = flag27;
							bool flag29 = flag28;
							bool flag30 = flag29;
							bool flag31 = flag30;
							bool flag32 = flag31;
							if (flag32)
							{
								centerEnt.lvDone = true;
								centerEnt.lv = transform.GetComponent<LocalFactionVision>();
							}
							bool flag33 = centerEnt.lv != null && !centerEnt.lv.IsVisibleForMe();
							bool flag34 = flag33;
							bool flag35 = flag34;
							bool flag36 = flag35;
							bool flag37 = flag36;
							bool flag38 = flag37;
							bool flag39 = flag38;
							bool flag40 = flag39;
							if (flag40)
							{
								continue;
							}
						}
						centerEnt.enemy = enemy;
						this.centerShown.Add(centerEnt);
					}
				}
			}
		}
	}

	private Texture2D GetDotTex()
	{
		bool flag = this.dotTex == null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			this.dotTex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
			this.dotTex.filterMode = FilterMode.Bilinear;
			this.dotTex.wrapMode = TextureWrapMode.Clamp;
			this.dotTex.hideFlags = HideFlags.HideAndDontSave;
			float num = 16f;
			for (int i = 0; i < 32; i++)
			{
				for (int j = 0; j < 32; j++)
				{
					float num2 = (float)j + 0.5f - num;
					float num3 = (float)i + 0.5f - num;
					float a = Mathf.Clamp01(num - Mathf.Sqrt(num2 * num2 + num3 * num3));
					this.dotTex.SetPixel(j, i, new Color(1f, 1f, 1f, a));
				}
			}
			this.dotTex.Apply();
		}
		return this.dotTex;
	}

	private void DrawCenters()
	{
		ResourcePackManager.HpConfig hpConfig = this.hpCfg;
		bool flag = hpConfig == null || !hpConfig.centerOn || this.centerShown.Count == 0 || Event.current.type != EventType.Repaint;
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
				float num2 = Mathf.Max(3f, 8f * hpConfig.centerSize * num);
				float num3 = Mathf.Max(1f, num2 * 0.22f);
				Texture2D image = this.GetDotTex();
				Color color = GUI.color;
				GUI.depth = 6;
				for (int i = 0; i < this.centerShown.Count; i++)
				{
					ResourcePackManager.CenterEnt centerEnt = this.centerShown[i];
					bool flag17 = centerEnt.tr == null;
					bool flag18 = !flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					bool flag21 = flag20;
					bool flag22 = flag21;
					bool flag23 = flag22;
					bool flag24 = flag23;
					if (flag24)
					{
						Color color2 = centerEnt.enemy ? hpConfig.centerEnemyColor : hpConfig.centerOwnColor;
						for (int j = 0; j < centerEnt.pts.Count; j++)
						{
							Transform transform = centerEnt.pts[j];
							bool flag25 = transform == null || !transform.gameObject.activeInHierarchy;
							bool flag26 = !flag25;
							bool flag27 = flag26;
							bool flag28 = flag27;
							bool flag29 = flag28;
							bool flag30 = flag29;
							bool flag31 = flag30;
							bool flag32 = flag31;
							if (flag32)
							{
								Vector3 vector = main.WorldToScreenPoint(transform.position);
								bool flag33 = vector.z <= 0f || vector.x < -20f || vector.x > (float)Screen.width + 20f || vector.y < -20f || vector.y > (float)Screen.height + 20f;
								bool flag34 = !flag33;
								bool flag35 = flag34;
								bool flag36 = flag35;
								bool flag37 = flag36;
								bool flag38 = flag37;
								bool flag39 = flag38;
								bool flag40 = flag39;
								if (flag40)
								{
									float x = vector.x;
									float num4 = (float)Screen.height - vector.y;
									GUI.color = new Color(0f, 0f, 0f, hpConfig.centerAlpha);
									GUI.DrawTexture(new Rect(x - num2 * 0.5f - num3, num4 - num2 * 0.5f - num3, num2 + num3 * 2f, num2 + num3 * 2f), image);
									color2.a = hpConfig.centerAlpha;
									GUI.color = color2;
									GUI.DrawTexture(new Rect(x - num2 * 0.5f, num4 - num2 * 0.5f, num2, num2), image);
								}
							}
						}
					}
				}
				GUI.color = color;
			}
		}
	}

	private void UpdateExplosionDist()
	{
		ResourcePackManager.HpConfig hpConfig = this.hpCfg;
		bool flag = hpConfig == null || !hpConfig.centerDist;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			bool flag9 = this.distFloats.Count > 0;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				this.distFloats.Clear();
			}
			this.expSeen.Clear();
			this.expPrimed = false;
		}
		else
		{
			float unscaledTime = Time.unscaledTime;
			for (int i = this.distFloats.Count - 1; i >= 0; i--)
			{
				bool flag17 = unscaledTime - this.distFloats[i].t0 > hpConfig.centerDistTime;
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				if (flag24)
				{
					this.distFloats.RemoveAt(i);
				}
			}
			bool flag25 = unscaledTime < this.expNextScan;
			bool flag26 = !flag25;
			bool flag27 = flag26;
			bool flag28 = flag27;
			bool flag29 = flag28;
			bool flag30 = flag29;
			bool flag31 = flag30;
			bool flag32 = flag31;
			if (flag32)
			{
				this.expNextScan = unscaledTime + (hpConfig.perfMode ? 0.3f : 0.15f);
				ExplosionGameplay[] array = UnityEngine.Object.FindObjectsOfType<ExplosionGameplay>();
				this.expNow.Clear();
				foreach (ExplosionGameplay explosionGameplay in array)
				{
					bool flag33 = explosionGameplay == null;
					bool flag34 = !flag33;
					bool flag35 = flag34;
					bool flag36 = flag35;
					bool flag37 = flag36;
					bool flag38 = flag37;
					bool flag39 = flag38;
					bool flag40 = flag39;
					if (flag40)
					{
						int instanceID = explosionGameplay.GetInstanceID();
						this.expNow.Add(instanceID);
						bool flag41 = this.expSeen.Add(instanceID) && this.expPrimed;
						bool flag42 = flag41;
						bool flag43 = flag42;
						bool flag44 = flag43;
						bool flag45 = flag44;
						bool flag46 = flag45;
						bool flag47 = flag46;
						bool flag48 = flag47;
						if (flag48)
						{
							this.ShowExplosionDist(hpConfig, explosionGameplay, unscaledTime);
						}
					}
				}
				this.expSeen.IntersectWith(this.expNow);
				this.expPrimed = true;
			}
		}
	}

	private void ShowExplosionDist(ResourcePackManager.HpConfig cfg, ExplosionGameplay gp, float now)
	{
		Vector3 position = gp.transform.position;
		float num = (gp.unitDamageCurve != null) ? gp.unitDamageCurve.maxRange : 5f;
		for (int i = 0; i < this.centerShown.Count; i++)
		{
			ResourcePackManager.CenterEnt centerEnt = this.centerShown[i];
			bool flag = centerEnt.tr == null;
			bool flag2 = !flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				Transform transform = null;
				float num2 = float.MaxValue;
				for (int j = 0; j < centerEnt.pts.Count; j++)
				{
					Transform transform2 = centerEnt.pts[j];
					bool flag9 = transform2 == null;
					bool flag10 = !flag9;
					bool flag11 = flag10;
					bool flag12 = flag11;
					bool flag13 = flag12;
					bool flag14 = flag13;
					bool flag15 = flag14;
					bool flag16 = flag15;
					if (flag16)
					{
						float magnitude = (transform2.position - position).magnitude;
						bool flag17 = magnitude < num2;
						bool flag18 = flag17;
						bool flag19 = flag18;
						bool flag20 = flag19;
						bool flag21 = flag20;
						bool flag22 = flag21;
						bool flag23 = flag22;
						bool flag24 = flag23;
						if (flag24)
						{
							num2 = magnitude;
							transform = transform2;
						}
					}
				}
				bool flag25 = transform == null || num2 > num;
				bool flag26 = !flag25;
				bool flag27 = flag26;
				bool flag28 = flag27;
				bool flag29 = flag28;
				bool flag30 = flag29;
				bool flag31 = flag30;
				bool flag32 = flag31;
				if (flag32)
				{
					ResourcePackManager.DistFloat distFloat = new ResourcePackManager.DistFloat();
					distFloat.pt = transform;
					distFloat.world = transform.position;
					distFloat.enemy = centerEnt.enemy;
					distFloat.amount = num2;
					distFloat.t0 = now;
					distFloat.text = num2.ToString("0.00", CultureInfo.InvariantCulture);
					this.distFloats.Add(distFloat);
				}
			}
		}
		while (this.distFloats.Count > 300)
		{
			this.distFloats.RemoveAt(0);
		}
	}

	private void DrawExplosionDist()
	{
		ResourcePackManager.HpConfig hpConfig = this.hpCfg;
		bool flag = hpConfig == null || !hpConfig.centerDist || this.distFloats.Count == 0 || Event.current.type != EventType.Repaint;
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
				float unscaledTime = Time.unscaledTime;
				float num = (float)Screen.height / 1080f;
				int num2 = Mathf.Max(8, Mathf.RoundToInt(22f * hpConfig.centerDistSize * num));
				GUIStyle guistyle = this.GetOverlayStyle();
				guistyle.fontSize = num2;
				GUI.depth = 5;
				Color color = GUI.color;
				float ow = Mathf.Max(1f, (float)num2 * 0.07f);
				for (int i = 0; i < this.distFloats.Count; i++)
				{
					ResourcePackManager.DistFloat distFloat = this.distFloats[i];
					float num3 = Mathf.Max(0.1f, hpConfig.centerDistTime);
					float num4 = unscaledTime - distFloat.t0;
					bool flag17 = num4 > num3;
					bool flag18 = !flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					bool flag21 = flag20;
					bool flag22 = flag21;
					bool flag23 = flag22;
					bool flag24 = flag23;
					if (flag24)
					{
						Vector3 vector = distFloat.world;
						bool flag25 = distFloat.pt != null;
						bool flag26 = flag25;
						bool flag27 = flag26;
						bool flag28 = flag27;
						bool flag29 = flag28;
						bool flag30 = flag29;
						bool flag31 = flag30;
						bool flag32 = flag31;
						if (flag32)
						{
							vector = distFloat.pt.position;
							distFloat.world = vector;
						}
						Vector3 vector2 = main.WorldToScreenPoint(vector);
						bool flag33 = vector2.z <= 0f || vector2.x < -100f || vector2.x > (float)Screen.width + 100f || vector2.y < -100f || vector2.y > (float)Screen.height + 100f;
						bool flag34 = !flag33;
						bool flag35 = flag34;
						bool flag36 = flag35;
						bool flag37 = flag36;
						bool flag38 = flag37;
						bool flag39 = flag38;
						bool flag40 = flag39;
						if (flag40)
						{
							float a = Mathf.Clamp01((num3 - num4) / Mathf.Max(0.05f, num3 * 0.35f));
							Color col = distFloat.enemy ? hpConfig.centerEnemyColor : hpConfig.centerOwnColor;
							col.a = a;
							float x = vector2.x;
							float num5 = (float)Screen.height - vector2.y - (float)num2 * 0.9f;
							Rect r = new Rect(x - (float)num2 * 4f, num5 - (float)num2, (float)num2 * 8f, (float)num2 * 2f);
							bool perfMode = hpConfig.perfMode;
							bool flag41 = perfMode;
							bool flag42 = flag41;
							bool flag43 = flag42;
							bool flag44 = flag43;
							bool flag45 = flag44;
							bool flag46 = flag45;
							bool flag47 = flag46;
							if (flag47)
							{
								ResourcePackManager.DrawShadowText(r, distFloat.text, guistyle, col, ow);
							}
							else
							{
								ResourcePackManager.DrawOutlinedText(r, distFloat.text, guistyle, col, ow);
							}
						}
					}
				}
				GUI.color = color;
			}
		}
	}

	private void UpdateMenuGameLock()
	{
		ResourcePackManager.HpConfig hpConfig = this.hpCfg;
		try
		{
			this.ApplyMenuInputLock((this.menuOpen && hpConfig != null && hpConfig.menuBlockInput) || BtcMatchStatsUI.PanelOpen || BtcTimer.MouseOver);
			this.ApplyMenuPause(this.menuOpen && hpConfig != null && hpConfig.menuPause);
		}
		catch (Exception ex)
		{
			bool flag = !this.menuLockWarned;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				this.menuLockWarned = true;
				ResourcePackManager.PackWarn("[ResourcePacks] Menu pause/input lock failed: " + ex.Message); BtcLog.Error("Menu", "input lock failed: " + ex);
			}
		}
	}

	private void ReleaseMenuGameLock()
	{
		try
		{
			this.ApplyMenuInputLock(false);
			this.ApplyMenuPause(false);
		}
		catch (Exception)
		{
		}
	}

	private void ApplyMenuInputLock(bool want)
	{
		float unscaledTime = Time.unscaledTime;
		InputModeController instance = InputModeController.Instance;
		if (want)
		{
			if (!this.menuInputLocked)
			{
				if (instance != null)
				{
					bool flag = ControlContext.ContextIsActive("Chat");
					if (!flag)
					{
						instance.ChangeInputToMenu();
					}
					this.menuInputLocked = !flag;
					this.SetEventSystemEnabled(false);
					this.menuInputCheckAt = unscaledTime + 0.25f;
				}
				else if (this.esBlocked == null || unscaledTime >= this.menuInputCheckAt)
				{
					// main menu / lobby (no InputModeController): only the uGUI EventSystem can be neutralized
					this.SetEventSystemEnabled(false);
					this.menuInputCheckAt = unscaledTime + 0.25f;
				}
			}
			else if (unscaledTime >= this.menuInputCheckAt)
			{
				this.menuInputCheckAt = unscaledTime + 0.25f;
				if (!ControlContext.ContextIsActive("Chat") && instance != null)
				{
					instance.ChangeInputToMenu();
				}
				this.SetEventSystemEnabled(false);
			}
		}
		else if (this.menuInputLocked || this.esBlocked != null)
		{
			if (this.menuInputLocked)
			{
				if (instance != null)
				{
					instance.ChangeInputToGame();
				}
				else
				{
					ControlContext.SetContext("Chat", false);
				}
			}
			this.menuInputLocked = false;
			this.SetEventSystemEnabled(true);
		}
	}

	private static bool IsDead(object o)
	{
		UnityEngine.Object @object = o as UnityEngine.Object;
		return o == null || (@object != null ? false : true);
	}

	private void SetEventSystemEnabled(bool enable)
	{
		Type type = Type.GetType("UnityEngine.EventSystems.EventSystem, UnityEngine.UI");
		bool flag = type == null;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			bool flag9 = !enable;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				bool flag17 = !ResourcePackManager.IsDead(this.esBlocked);
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				if (flag24)
				{
					Behaviour behaviour = this.esBlocked as Behaviour;
					bool flag25 = behaviour != null;
					bool flag26 = flag25;
					bool flag27 = flag26;
					bool flag28 = flag27;
					bool flag29 = flag28;
					bool flag30 = flag29;
					bool flag31 = flag30;
					bool flag32 = flag31;
					if (flag32)
					{
						behaviour.enabled = false;
					}
				}
				else
				{
					PropertyInfo property = type.GetProperty("current", BindingFlags.Static | BindingFlags.Public);
					Behaviour behaviour2 = (property != null) ? (property.GetValue(null, null) as Behaviour) : null;
					bool flag33 = behaviour2 == null;
					bool flag34 = !flag33;
					bool flag35 = flag34;
					bool flag36 = flag35;
					bool flag37 = flag36;
					bool flag38 = flag37;
					bool flag39 = flag38;
					bool flag40 = flag39;
					if (flag40)
					{
						this.esWasEnabled = behaviour2.enabled;
						this.esBlocked = behaviour2;
						behaviour2.enabled = false;
					}
				}
			}
			else
			{
				bool flag41 = this.esBlocked != null;
				bool flag42 = flag41;
				bool flag43 = flag42;
				bool flag44 = flag43;
				bool flag45 = flag44;
				bool flag46 = flag45;
				bool flag47 = flag46;
				bool flag48 = flag47;
				if (flag48)
				{
					Behaviour behaviour3 = this.esBlocked as Behaviour;
					bool flag49 = behaviour3 != null;
					bool flag50 = flag49;
					bool flag51 = flag50;
					bool flag52 = flag51;
					bool flag53 = flag52;
					bool flag54 = flag53;
					bool flag55 = flag54;
					bool flag56 = flag55;
					if (flag56)
					{
						behaviour3.enabled = this.esWasEnabled;
					}
					this.esBlocked = null;
				}
			}
		}
	}

	private static bool ReadUnetPause(UNetManager un)
	{
		return un != null && BtcBc.BcVoluntaryPauseActive;
	}

	private static void ToggleUnetPause(UNetManager un)
	{
		bool flag = un != null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			BtcBc.BcTogglePause();
		}
	}

	private void ApplyMenuPause(bool want)
	{
		float unscaledTime = Time.unscaledTime;
		bool isMultiplayer = Neo.IsMultiplayer;
		UNetManager unetManager = (isMultiplayer && BtcBc.BcEnabled) ? UNetManager.instance : null;
		if (want)
		{
			bool flag = this.menuPauseTried;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				bool flag9 = this.menuPausePending;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					bool flag17 = unetManager != null && ResourcePackManager.ReadUnetPause(unetManager);
					bool flag18 = flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					bool flag21 = flag20;
					bool flag22 = flag21;
					bool flag23 = flag22;
					bool flag24 = flag23;
					if (flag24)
					{
						this.menuPauseOwned = true;
						this.menuPausePending = false;
					}
					else
					{
						bool flag25 = unscaledTime - this.menuPauseReqAt > 3f;
						bool flag26 = flag25;
						bool flag27 = flag26;
						bool flag28 = flag27;
						bool flag29 = flag28;
						bool flag30 = flag29;
						bool flag31 = flag30;
						bool flag32 = flag31;
						if (flag32)
						{
							this.menuPausePending = false;
						}
					}
				}
			}
			else
			{
				this.menuPauseTried = true;
				bool flag33 = !isMultiplayer;
				bool flag34 = flag33;
				bool flag35 = flag34;
				bool flag36 = flag35;
				bool flag37 = flag36;
				bool flag38 = flag37;
				bool flag39 = flag38;
				bool flag40 = flag39;
				if (flag40)
				{
					bool flag41 = !InputModeController.GamePlayPaused();
					bool flag42 = flag41;
					bool flag43 = flag42;
					bool flag44 = flag43;
					bool flag45 = flag44;
					bool flag46 = flag45;
					bool flag47 = flag46;
					bool flag48 = flag47;
					if (flag48)
					{
						InputModeController.TogglePause();
						this.menuPauseOwned = InputModeController.IsPaused();
						this.menuPauseSolo = true;
					}
				}
				else
				{
					bool flag49 = unetManager == null || ResourcePackManager.ReadUnetPause(unetManager);
					bool flag50 = !flag49;
					bool flag51 = flag50;
					bool flag52 = flag51;
					bool flag53 = flag52;
					bool flag54 = flag53;
					bool flag55 = flag54;
					bool flag56 = flag55;
					if (flag56)
					{
						ResourcePackManager.ToggleUnetPause(unetManager);
						this.menuPausePending = true;
						this.menuPauseReqAt = unscaledTime;
					}
				}
			}
		}
		else
		{
			this.menuPauseTried = false;
			bool flag57 = this.menuPausePending;
			bool flag58 = flag57;
			bool flag59 = flag58;
			bool flag60 = flag59;
			bool flag61 = flag60;
			bool flag62 = flag61;
			bool flag63 = flag62;
			bool flag64 = flag63;
			if (flag64)
			{
				this.menuPausePending = false;
				this.menuPauseRelease = true;
			}
			bool flag65 = this.menuPauseOwned;
			bool flag66 = flag65;
			bool flag67 = flag66;
			bool flag68 = flag67;
			bool flag69 = flag68;
			bool flag70 = flag69;
			bool flag71 = flag70;
			bool flag72 = flag71;
			if (flag72)
			{
				this.menuPauseOwned = false;
				bool flag73 = this.menuPauseSolo;
				bool flag74 = flag73;
				bool flag75 = flag74;
				bool flag76 = flag75;
				bool flag77 = flag76;
				bool flag78 = flag77;
				bool flag79 = flag78;
				bool flag80 = flag79;
				if (flag80)
				{
					bool flag81 = InputModeController.IsPaused();
					bool flag82 = flag81;
					bool flag83 = flag82;
					bool flag84 = flag83;
					bool flag85 = flag84;
					bool flag86 = flag85;
					bool flag87 = flag86;
					bool flag88 = flag87;
					if (flag88)
					{
						InputModeController.TogglePause();
					}
				}
				else
				{
					bool flag89 = unetManager != null && ResourcePackManager.ReadUnetPause(unetManager);
					bool flag90 = flag89;
					bool flag91 = flag90;
					bool flag92 = flag91;
					bool flag93 = flag92;
					bool flag94 = flag93;
					bool flag95 = flag94;
					bool flag96 = flag95;
					if (flag96)
					{
						ResourcePackManager.ToggleUnetPause(unetManager);
					}
				}
				this.menuPauseSolo = false;
			}
			bool flag97 = this.menuPauseRelease;
			bool flag98 = flag97;
			bool flag99 = flag98;
			bool flag100 = flag99;
			bool flag101 = flag100;
			bool flag102 = flag101;
			bool flag103 = flag102;
			bool flag104 = flag103;
			if (flag104)
			{
				bool flag105 = unetManager == null;
				bool flag106 = flag105;
				bool flag107 = flag106;
				bool flag108 = flag107;
				bool flag109 = flag108;
				bool flag110 = flag109;
				bool flag111 = flag110;
				bool flag112 = flag111;
				if (flag112)
				{
					this.menuPauseRelease = false;
				}
				else
				{
					bool flag113 = ResourcePackManager.ReadUnetPause(unetManager);
					bool flag114 = flag113;
					bool flag115 = flag114;
					bool flag116 = flag115;
					bool flag117 = flag116;
					bool flag118 = flag117;
					bool flag119 = flag118;
					bool flag120 = flag119;
					if (flag120)
					{
						this.menuPauseRelease = false;
						ResourcePackManager.ToggleUnetPause(unetManager);
					}
					else
					{
						bool flag121 = unscaledTime - this.menuPauseReqAt > 3f;
						bool flag122 = flag121;
						bool flag123 = flag122;
						bool flag124 = flag123;
						bool flag125 = flag124;
						bool flag126 = flag125;
						bool flag127 = flag126;
						bool flag128 = flag127;
						if (flag128)
						{
							this.menuPauseRelease = false;
						}
					}
				}
			}
		}
	}

	private static void PruneDead<K, V>(Dictionary<K, V> cache, int limit) where K : UnityEngine.Object
	{
		bool flag = cache.Count <= limit;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			List<K> list = null;
			foreach (KeyValuePair<K, V> keyValuePair in cache)
			{
				bool flag9 = keyValuePair.Key == null;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					bool flag17 = list == null;
					bool flag18 = flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					bool flag21 = flag20;
					bool flag22 = flag21;
					bool flag23 = flag22;
					bool flag24 = flag23;
					if (flag24)
					{
						list = new List<K>();
					}
					list.Add(keyValuePair.Key);
				}
			}
			bool flag25 = list != null;
			bool flag26 = flag25;
			bool flag27 = flag26;
			bool flag28 = flag27;
			bool flag29 = flag28;
			bool flag30 = flag29;
			bool flag31 = flag30;
			bool flag32 = flag31;
			if (flag32)
			{
				for (int i = 0; i < list.Count; i++)
				{
					cache.Remove(list[i]);
				}
			}
			bool flag33 = cache.Count > limit * 4;
			bool flag34 = flag33;
			bool flag35 = flag34;
			bool flag36 = flag35;
			bool flag37 = flag36;
			bool flag38 = flag37;
			bool flag39 = flag38;
			bool flag40 = flag39;
			if (flag40)
			{
				cache.Clear();
			}
		}
	}

	private static bool BldVisibleForMe(ResourcePackManager.BldUnit bu)
	{
		bool flag = !bu.lvDone;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			bu.lvDone = true;
			bu.lv = bu.tr.GetComponent<LocalFactionVision>();
		}
		return bu.lv == null || bu.lv.IsVisibleForMe();
	}

}
}
