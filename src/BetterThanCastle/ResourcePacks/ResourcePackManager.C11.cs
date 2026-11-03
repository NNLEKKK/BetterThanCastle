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

	private void UpdateHitCounters()
	{
		ResourcePackManager.HpConfig hpConfig = this.hpCfg;
		bool flag = !ResourcePackManager.HitAnyOn(hpConfig);
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			bool flag9 = this.hitTracks.Count > 0 || this.hitFloats.Count > 0;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				this.ClearHitTracks();
			}
		}
		else
		{
			float unscaledTime = Time.unscaledTime;
			bool flag17 = unscaledTime >= this.hitNextScan;
			bool flag18 = flag17;
			bool flag19 = flag18;
			bool flag20 = flag19;
			bool flag21 = flag20;
			bool flag22 = flag21;
			bool flag23 = flag22;
			bool flag24 = flag23;
			if (flag24)
			{
				this.hitNextScan = unscaledTime + (hpConfig.perfMode ? 0.5f : 0.25f);
				this.hitSeen.Clear();
				ResourcePackManager.PruneDead<Transform, ResourcePackManager.BldUnit>(this.bldCache, 600);
				bool flag25 = hpConfig.hitOwn || hpConfig.hitStamOwn;
				bool flag26 = flag25;
				bool flag27 = flag26;
				bool flag28 = flag27;
				bool flag29 = flag28;
				bool flag30 = flag29;
				bool flag31 = flag30;
				bool flag32 = flag31;
				if (flag32)
				{
					this.CollectHitUnits(UIGameObserver.bricktrons, false, unscaledTime);
					this.CollectHitUnits(UIGameObserver.alliedBricktrons, false, unscaledTime);
				}
				bool flag33 = hpConfig.hitEnemy || hpConfig.hitStamEnemy;
				bool flag34 = flag33;
				bool flag35 = flag34;
				bool flag36 = flag35;
				bool flag37 = flag36;
				bool flag38 = flag37;
				bool flag39 = flag38;
				bool flag40 = flag39;
				if (flag40)
				{
					this.CollectHitUnits(UIGameObserver.enemyBricktrons, true, unscaledTime);
				}
				bool hitBldOwn = hpConfig.hitBldOwn;
				bool flag41 = hitBldOwn;
				bool flag42 = flag41;
				bool flag43 = flag42;
				bool flag44 = flag43;
				bool flag45 = flag44;
				bool flag46 = flag45;
				bool flag47 = flag46;
				if (flag47)
				{
					this.CollectHitBuildings(UIGameObserver.dynamicObjects, false, unscaledTime);
				}
				bool hitBldEnemy = hpConfig.hitBldEnemy;
				bool flag48 = hitBldEnemy;
				bool flag49 = flag48;
				bool flag50 = flag49;
				bool flag51 = flag50;
				bool flag52 = flag51;
				bool flag53 = flag52;
				bool flag54 = flag53;
				if (flag54)
				{
					this.CollectHitBuildings(UIGameObserver.ennemyDynamicObjects, true, unscaledTime);
				}
				this.hitDrop.Clear();
				foreach (KeyValuePair<Transform, ResourcePackManager.HitTrack> keyValuePair in this.hitTracks)
				{
					bool flag55 = keyValuePair.Key == null || unscaledTime - keyValuePair.Value.lastSeen > 1.5f;
					bool flag56 = flag55;
					bool flag57 = flag56;
					bool flag58 = flag57;
					bool flag59 = flag58;
					bool flag60 = flag59;
					bool flag61 = flag60;
					bool flag62 = flag61;
					if (flag62)
					{
						this.hitDrop.Add(keyValuePair.Key);
					}
				}
				for (int i = 0; i < this.hitDrop.Count; i++)
				{
					ResourcePackManager.HitTrack ht;
					bool flag63 = this.hitTracks.TryGetValue(this.hitDrop[i], out ht);
					bool flag64 = flag63;
					bool flag65 = flag64;
					bool flag66 = flag65;
					bool flag67 = flag66;
					bool flag68 = flag67;
					bool flag69 = flag68;
					bool flag70 = flag69;
					if (flag70)
					{
						this.HandleHitGone(hpConfig, ht, unscaledTime, Time.fixedTime);
						this.FlushBurst(ht, unscaledTime);
						this.DetachHitTrack(ht);
						this.hitTracks.Remove(this.hitDrop[i]);
					}
				}
			}
			for (int j = this.hitFloats.Count - 1; j >= 0; j--)
			{
				ResourcePackManager.HitFloat hitFloat = this.hitFloats[j];
				float num = hitFloat.dps ? hpConfig.dpsShow : hpConfig.hitTime;
				bool flag71 = unscaledTime - hitFloat.t0 > num;
				bool flag72 = flag71;
				bool flag73 = flag72;
				bool flag74 = flag73;
				bool flag75 = flag74;
				bool flag76 = flag75;
				bool flag77 = flag76;
				bool flag78 = flag77;
				if (flag78)
				{
					this.hitFloats.RemoveAt(j);
				}
			}
		}
	}

	private void CollectHitUnits(UIGameObserver.Bricktron observer, bool enemy, float now)
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
				bool flag9 = labor == null || !labor.gameObject.activeInHierarchy || !this.hitSeen.Add(labor.transform);
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
							string victimKey = (labor.Occupation != null) ? labor.Occupation.CurrentJob.ToString() : "?";
							ResourcePackManager.HitTrack hitTrack = this.GetHitTrack(labor.transform, bricktronDamageReceiver, false, victimKey);
							hitTrack.enemy = enemy;
							hitTrack.lastSeen = now;
							hitTrack.lastWorld = labor.transform.TransformPoint(hitTrack.localOffset);
						}
					}
				}
			}
		}
	}

	private void FlushBurst(ResourcePackManager.HitTrack ht, float now)
	{
		ResourcePackManager.HpConfig hpConfig = this.hpCfg;
		bool flag = hpConfig != null && ht.burstHits >= 2 && ResourcePackManager.HitDpsOn(hpConfig, ht);
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			float num = ht.burstLast - ht.burstStart;
			float num2 = num * (float)ht.burstHits / (float)(ht.burstHits - 1);
			float b = ht.building ? 1f : Mathf.Max(0.001f, Time.fixedDeltaTime);
			num2 = Mathf.Max(num2, b);
			float num3 = ht.burstTotal / num2;
			ResourcePackManager.HitFloat hitFloat = new ResourcePackManager.HitFloat();
			hitFloat.tr = ht.tr;
			hitFloat.localOffset = ht.localOffset;
			hitFloat.world = ht.lastWorld;
			hitFloat.enemy = ht.enemy;
			hitFloat.amount = num3;
			hitFloat.t0 = now;
			hitFloat.lastHit = now;
			hitFloat.dps = true;
			hitFloat.text = "DPS " + (hpConfig.dpsPrecise ? num3.ToString("0.000", CultureInfo.InvariantCulture) : Mathf.RoundToInt(num3).ToString());
			this.hitFloats.Add(hitFloat);
		}
		ht.burstHits = 0;
		ht.burstTotal = 0f;
	}

	private GUIStyle GetOverlayStyle()
	{
		bool flag = this.overlayStyle == null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			this.overlayStyle = new GUIStyle(GUI.skin.label);
			this.overlayStyle.alignment = TextAnchor.MiddleCenter;
			this.overlayStyle.fontStyle = FontStyle.Bold;
			this.overlayStyle.wordWrap = false;
			this.overlayStyle.clipping = TextClipping.Overflow;
		}
		return this.overlayStyle;
	}

	private static void DrawOutlinedText(Rect r, string text, GUIStyle st, Color col, float ow)
	{
		GUI.color = new Color(0f, 0f, 0f, col.a);
		GUI.Label(new Rect(r.x - ow, r.y, r.width, r.height), text, st);
		GUI.Label(new Rect(r.x + ow, r.y, r.width, r.height), text, st);
		GUI.Label(new Rect(r.x, r.y - ow, r.width, r.height), text, st);
		GUI.Label(new Rect(r.x, r.y + ow, r.width, r.height), text, st);
		GUI.color = col;
		GUI.Label(r, text, st);
	}

	private void DrawHitCounters()
	{
		ResourcePackManager.HpConfig hpConfig = this.hpCfg;
		bool flag = hpConfig == null || this.hitFloats.Count == 0 || Event.current.type != EventType.Repaint;
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
				int num2 = Mathf.Max(8, Mathf.RoundToInt(26f * hpConfig.hitSize * num));
				GUIStyle guistyle = this.GetOverlayStyle();
				guistyle.fontSize = num2;
				GUI.depth = 5;
				Color color = GUI.color;
				float ow = Mathf.Max(1f, (float)num2 * 0.07f);
				for (int i = 0; i < this.hitFloats.Count; i++)
				{
					ResourcePackManager.HitFloat hitFloat = this.hitFloats[i];
					float num3 = hitFloat.dps ? hpConfig.dpsShow : hpConfig.hitTime;
					float num4 = unscaledTime - hitFloat.t0;
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
						Vector3 vector = hitFloat.world;
						bool flag25 = hitFloat.tr != null && hitFloat.tr.gameObject.activeInHierarchy;
						bool flag26 = flag25;
						bool flag27 = flag26;
						bool flag28 = flag27;
						bool flag29 = flag28;
						bool flag30 = flag29;
						bool flag31 = flag30;
						bool flag32 = flag31;
						if (flag32)
						{
							vector = hitFloat.tr.TransformPoint(hitFloat.localOffset);
							hitFloat.world = vector;
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
							float num5 = Mathf.Clamp01(num4 / num3) * 28f * num;
							Color col = hitFloat.dps ? hpConfig.dpsColor : (hitFloat.stam ? hpConfig.hitStamColor : (hitFloat.enemy ? hpConfig.hitEnemyColor : hpConfig.hitOwnColor));
							col.a = a;
							float num6 = vector2.x + (hitFloat.stam ? (-hpConfig.hitX) : hpConfig.hitX) * num;
							float num7 = (float)Screen.height - vector2.y - (hpConfig.hitY * num + num5) - (hitFloat.dps ? ((float)num2 * 1.15f) : 0f);
							Rect r = new Rect(num6 - (float)num2 * 4f, num7 - (float)num2, (float)num2 * 8f, (float)num2 * 2f);
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
								ResourcePackManager.DrawShadowText(r, hitFloat.text, guistyle, col, ow);
							}
							else
							{
								ResourcePackManager.DrawOutlinedText(r, hitFloat.text, guistyle, col, ow);
							}
						}
					}
				}
				GUI.color = color;
			}
		}
	}

	private IsCapturable[] scanCache;

	private int scanIdx;

	private bool scanActive;

	private int scanLive;

	private float crystalFindNext;

	private List<ResourcePackManager.CrystalTrack> scanList;

	// Starts a time-sliced crystal scan: the expensive FindObjectsOfType runs rarely, the array is processed in small chunks per frame.
	private void ScanCrystals()
	{
		float now = Time.unscaledTime;
		if (this.scanCache == null || now >= this.crystalFindNext)
		{
			this.scanCache = UnityEngine.Object.FindObjectsOfType<IsCapturable>();
			this.crystalFindNext = now + 30f;
		}
		if (this.scanList == null)
		{
			this.scanList = new List<ResourcePackManager.CrystalTrack>();
		}
		this.scanList.Clear();
		this.scanIdx = 0;
		this.scanLive = 0;
		this.scanActive = true;
		this.ScanCrystalsStep();
	}

	private void ScanCrystalsStep()
	{
		IsCapturable[] array = this.scanCache;
		bool flag = ResourcePackManager.fCapClock == null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			ResourcePackManager.fCapClock = typeof(IsCapturable).GetField("CaptureClock", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		}
		bool flag9 = ResourcePackManager.fClockProg == null;
		bool flag10 = flag9;
		bool flag11 = flag10;
		bool flag12 = flag11;
		bool flag13 = flag12;
		bool flag14 = flag13;
		bool flag15 = flag14;
		bool flag16 = flag15;
		if (flag16)
		{
			ResourcePackManager.fClockProg = typeof(CaptureClock).GetField("capture", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		}
		List<ResourcePackManager.CrystalTrack> list = this.scanList;
		int scanEnd = Math.Min(this.scanIdx + 12, array.Length);
		for (; this.scanIdx < scanEnd; this.scanIdx++)
		{
			IsCapturable isCapturable = array[this.scanIdx];
			if (isCapturable != null)
			{
				this.scanLive++;
			}
			bool flag17 = isCapturable == null || !isCapturable.gameObject.activeInHierarchy;
			bool flag18 = !flag17;
			bool flag19 = flag18;
			bool flag20 = flag19;
			bool flag21 = flag20;
			bool flag22 = flag21;
			bool flag23 = flag22;
			bool flag24 = flag23;
			if (flag24)
			{
				ResourcePackManager.CrystalTrack crystalTrack = null;
				for (int j = 0; j < this.crystalTracks.Count; j++)
				{
					bool flag25 = this.crystalTracks[j].cap == isCapturable;
					bool flag26 = flag25;
					bool flag27 = flag26;
					bool flag28 = flag27;
					bool flag29 = flag28;
					bool flag30 = flag29;
					bool flag31 = flag30;
					bool flag32 = flag31;
					if (flag32)
					{
						crystalTrack = this.crystalTracks[j];
						break;
					}
				}
				bool flag33 = crystalTrack == null;
				bool flag34 = flag33;
				bool flag35 = flag34;
				bool flag36 = flag35;
				bool flag37 = flag36;
				bool flag38 = flag37;
				bool flag39 = flag38;
				bool flag40 = flag39;
				if (flag40)
				{
					crystalTrack = new ResourcePackManager.CrystalTrack();
					crystalTrack.cap = isCapturable;
					crystalTrack.clock = ((ResourcePackManager.fCapClock != null) ? (ResourcePackManager.fCapClock.GetValue(isCapturable) as CaptureClock) : null);
					bool flag41 = crystalTrack.clock == null;
					bool flag42 = flag41;
					bool flag43 = flag42;
					bool flag44 = flag43;
					bool flag45 = flag44;
					bool flag46 = flag45;
					bool flag47 = flag46;
					bool flag48 = flag47;
					if (flag48)
					{
						crystalTrack.clock = isCapturable.GetComponentInChildren<CaptureClock>();
					}
					crystalTrack.prog = ((crystalTrack.clock != null && ResourcePackManager.fClockProg != null) ? (ResourcePackManager.fClockProg.GetValue(crystalTrack.clock) as CaptureProgress) : null);
					bool flag49 = crystalTrack.prog == null && crystalTrack.clock != null;
					bool flag50 = flag49;
					bool flag51 = flag50;
					bool flag52 = flag51;
					bool flag53 = flag52;
					bool flag54 = flag53;
					bool flag55 = flag54;
					bool flag56 = flag55;
					if (flag56)
					{
						crystalTrack.prog = crystalTrack.clock.GetComponentInChildren<CaptureProgress>();
					}
					bool flag57 = crystalTrack.clock == null || crystalTrack.prog == null;
					bool flag58 = flag57;
					bool flag59 = flag58;
					bool flag60 = flag59;
					bool flag61 = flag60;
					bool flag62 = flag61;
					bool flag63 = flag62;
					bool flag64 = flag63;
					if (flag64)
					{
						bool flag65 = !this.crystalWarned;
						bool flag66 = flag65;
						bool flag67 = flag66;
						bool flag68 = flag67;
						bool flag69 = flag68;
						bool flag70 = flag69;
						bool flag71 = flag70;
						bool flag72 = flag71;
						if (flag72)
						{
							this.crystalWarned = true;
							ResourcePackManager.PackWarn("[ResourcePacks] Crystal timer: CaptureClock/CaptureProgress not found on '" + isCapturable.name + "'");
						}
						goto IL_329;
					}
				}
				list.Add(crystalTrack);
			}
			IL_329:;
		}
		if (this.scanIdx >= array.Length)
		{
			this.scanActive = false;
			if (this.scanLive == 0)
			{
				this.crystalFindNext = 0f;
			}
			this.crystalTracks.Clear();
			this.crystalTracks.AddRange(list);
		}
	}

	private void UpdateCrystalTimers()
	{
		ResourcePackManager.HpConfig hpConfig = this.hpCfg;
		bool flag = hpConfig == null || !hpConfig.crystalTimer;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			bool flag9 = this.crystalTracks.Count > 0;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				this.crystalTracks.Clear();
			}
		}
		else
		{
			float unscaledTime = Time.unscaledTime;
			if (this.scanActive)
			{
				try
				{
					this.ScanCrystalsStep();
				}
				catch (Exception)
				{
					this.scanActive = false;
				}
			}
			bool flag17 = !this.scanActive && unscaledTime >= this.crystalNextScan;
			bool flag18 = flag17;
			bool flag19 = flag18;
			bool flag20 = flag19;
			bool flag21 = flag20;
			bool flag22 = flag21;
			bool flag23 = flag22;
			bool flag24 = flag23;
			if (flag24)
			{
				this.crystalNextScan = unscaledTime + (this.hpCfg.perfMode ? 8f : 4f);
				try
				{
					this.ScanCrystals();
				}
				catch (Exception ex)
				{
					bool flag25 = !this.crystalWarned;
					bool flag26 = flag25;
					bool flag27 = flag26;
					bool flag28 = flag27;
					bool flag29 = flag28;
					bool flag30 = flag29;
					bool flag31 = flag30;
					bool flag32 = flag31;
					if (flag32)
					{
						this.crystalWarned = true;
						ResourcePackManager.PackWarn("[ResourcePacks] Crystal timer: scan failed: " + ex.Message);
					}
				}
			}
			float time = Time.time;
			for (int i = 0; i < this.crystalTracks.Count; i++)
			{
				ResourcePackManager.CrystalTrack crystalTrack = this.crystalTracks[i];
				bool flag33 = crystalTrack.cap == null || crystalTrack.prog == null || crystalTrack.clock == null;
				bool flag34 = !flag33;
				bool flag35 = flag34;
				bool flag36 = flag35;
				bool flag37 = flag36;
				bool flag38 = flag37;
				bool flag39 = flag38;
				bool flag40 = flag39;
				if (flag40)
				{
					float current = crystalTrack.prog.current;
					bool flag41 = crystalTrack.lastCur < 0f;
					bool flag42 = flag41;
					bool flag43 = flag42;
					bool flag44 = flag43;
					bool flag45 = flag44;
					bool flag46 = flag45;
					bool flag47 = flag46;
					bool flag48 = flag47;
					if (flag48)
					{
						crystalTrack.lastCur = current;
						crystalTrack.lastChange = time;
						crystalTrack.dir = 0;
					}
					else
					{
						bool flag49 = current > crystalTrack.lastCur + 0.0001f;
						bool flag50 = flag49;
						bool flag51 = flag50;
						bool flag52 = flag51;
						bool flag53 = flag52;
						bool flag54 = flag53;
						bool flag55 = flag54;
						bool flag56 = flag55;
						if (flag56)
						{
							crystalTrack.dir = 1;
							crystalTrack.lastCur = current;
							crystalTrack.lastChange = time;
						}
						else
						{
							bool flag57 = current < crystalTrack.lastCur - 0.0001f;
							bool flag58 = flag57;
							bool flag59 = flag58;
							bool flag60 = flag59;
							bool flag61 = flag60;
							bool flag62 = flag61;
							bool flag63 = flag62;
							bool flag64 = flag63;
							if (flag64)
							{
								crystalTrack.dir = -1;
								crystalTrack.lastCur = current;
								crystalTrack.lastChange = time;
							}
							else
							{
								bool flag65 = crystalTrack.dir != 0 && time - crystalTrack.lastChange > Mathf.Max(1f, crystalTrack.clock.IncrementTime * 2.5f);
								bool flag66 = flag65;
								bool flag67 = flag66;
								bool flag68 = flag67;
								bool flag69 = flag68;
								bool flag70 = flag69;
								bool flag71 = flag70;
								bool flag72 = flag71;
								if (flag72)
								{
									crystalTrack.dir = 0;
								}
							}
						}
					}
					bool flag73 = crystalTrack.dir == 0;
					bool flag74 = !flag73;
					bool flag75 = flag74;
					bool flag76 = flag75;
					bool flag77 = flag76;
					bool flag78 = flag77;
					bool flag79 = flag78;
					bool flag80 = flag79;
					if (flag80)
					{
						crystalTrack.good = this.CrystalGoodness(crystalTrack);
						float num = Mathf.Max(0.0001f, crystalTrack.prog.smooth);
						float num2 = (crystalTrack.dir > 0) ? ((float)crystalTrack.prog.max - crystalTrack.prog.current) : crystalTrack.prog.current;
						float incrementTime = crystalTrack.clock.IncrementTime;
						float num3 = num2 * num * incrementTime - Mathf.Min(incrementTime, time - crystalTrack.lastChange);
						int num4 = Mathf.Max(1, Mathf.CeilToInt(num3 - 0.001f));
						bool flag81 = num2 <= 0.0001f;
						bool flag82 = flag81;
						bool flag83 = flag82;
						bool flag84 = flag83;
						bool flag85 = flag84;
						bool flag86 = flag85;
						bool flag87 = flag86;
						bool flag88 = flag87;
						if (flag88)
						{
							num4 = 0;
						}
						bool flag89 = num4 != crystalTrack.shownSecs;
						bool flag90 = flag89;
						bool flag91 = flag90;
						bool flag92 = flag91;
						bool flag93 = flag92;
						bool flag94 = flag93;
						bool flag95 = flag94;
						bool flag96 = flag95;
						if (flag96)
						{
							crystalTrack.shownSecs = num4;
							crystalTrack.text = num4.ToString() + "s";
						}
					}
				}
			}
		}
	}

	private void DrawCrystalTimers()
	{
		ResourcePackManager.HpConfig hpConfig = this.hpCfg;
		bool flag = hpConfig == null || !hpConfig.crystalTimer || this.crystalTracks.Count == 0 || Event.current.type != EventType.Repaint;
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
				int num2 = Mathf.Max(8, Mathf.RoundToInt(30f * hpConfig.crystalSize * num));
				GUIStyle guistyle = this.GetOverlayStyle();
				guistyle.fontSize = num2;
				GUI.depth = 5;
				Color color = GUI.color;
				float ow = Mathf.Max(1f, (float)num2 * 0.07f);
				for (int i = 0; i < this.crystalTracks.Count; i++)
				{
					ResourcePackManager.CrystalTrack crystalTrack = this.crystalTracks[i];
					bool flag17 = crystalTrack.cap == null || crystalTrack.prog == null || crystalTrack.clock == null || crystalTrack.dir == 0 || crystalTrack.shownSecs <= 0;
					bool flag18 = !flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					bool flag21 = flag20;
					bool flag22 = flag21;
					bool flag23 = flag22;
					bool flag24 = flag23;
					if (flag24)
					{
						Vector3 vector = main.WorldToScreenPoint(crystalTrack.cap.transform.position + Vector3.up * hpConfig.crystalLift);
						bool flag25 = vector.z <= 0f || vector.x < -100f || vector.x > (float)Screen.width + 100f || vector.y < -100f || vector.y > (float)Screen.height + 100f;
						bool flag26 = !flag25;
						bool flag27 = flag26;
						bool flag28 = flag27;
						bool flag29 = flag28;
						bool flag30 = flag29;
						bool flag31 = flag30;
						bool flag32 = flag31;
						if (flag32)
						{
							Color col = (crystalTrack.good > 0) ? hpConfig.crystalUpColor : hpConfig.crystalDownColor;
							float num3 = vector.x + hpConfig.crystalX * num;
							float num4 = (float)Screen.height - vector.y - hpConfig.crystalY * num;
							Rect r = new Rect(num3 - (float)num2 * 3f, num4 - (float)num2, (float)num2 * 6f, (float)num2 * 2f);
							bool perfMode = hpConfig.perfMode;
							bool flag33 = perfMode;
							bool flag34 = flag33;
							bool flag35 = flag34;
							bool flag36 = flag35;
							bool flag37 = flag36;
							bool flag38 = flag37;
							bool flag39 = flag38;
							if (flag39)
							{
								ResourcePackManager.DrawShadowText(r, crystalTrack.text, guistyle, col, ow);
							}
							else
							{
								ResourcePackManager.DrawOutlinedText(r, crystalTrack.text, guistyle, col, ow);
							}
						}
					}
				}
				GUI.color = color;
			}
		}
	}

	private void FixedUpdate()
	{
		this.PollHitHp();
	}

	private void PollHitHp()
	{
		ResourcePackManager.HpConfig hpConfig = this.hpCfg;
		bool flag = !ResourcePackManager.HitAnyOn(hpConfig) || this.hitTracks.Count == 0;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			float fixedTime = Time.fixedTime;
			float unscaledTime = Time.unscaledTime;
			foreach (KeyValuePair<Transform, ResourcePackManager.HitTrack> keyValuePair in this.hitTracks)
			{
				ResourcePackManager.HitTrack value = keyValuePair.Value;
				bool flag9 = value.dr == null;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					this.HandleHitGone(hpConfig, value, unscaledTime, fixedTime);
					bool flag17 = value.deadAt > 0f && unscaledTime - value.deadAt >= 0.35f;
					bool flag18 = flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					bool flag21 = flag20;
					bool flag22 = flag21;
					bool flag23 = flag22;
					bool flag24 = flag23;
					if (flag24)
					{
						this.FlushBurst(value, unscaledTime);
						value.deadAt = 0f;
					}
				}
				else
				{
					float currentHP = value.dr.CurrentHP;
					float num = (value.bdr != null) ? value.bdr.CurrentStamina : 0f;
					float num2 = (value.lastHp >= 0f) ? (value.lastHp - currentHP) : 0f;
					float num3 = (value.bdr != null && value.lastStam >= 0f) ? (value.lastStam - num) : 0f;
					value.lastHp = currentHP;
					value.lastStam = num;
					bool flag25 = num2 > 0.001f;
					bool flag26 = num3 > 0.001f;
					bool flag27 = flag25 || flag26;
					bool flag28 = flag27;
					bool flag29 = flag28;
					bool flag30 = flag29;
					bool flag31 = flag30;
					bool flag32 = flag31;
					bool flag33 = flag32;
					bool flag34 = flag33;
					if (flag34)
					{
						ResourcePackManager.HitLoss hitLoss = new ResourcePackManager.HitLoss();
						hitLoss.t = unscaledTime;
						hitLoss.gt = fixedTime;
						hitLoss.hp = (flag25 ? num2 : 0f);
						hitLoss.stam = (flag26 ? num3 : 0f);
						hitLoss.lethal = (flag25 && currentHP <= 0.001f);
						bool flag35 = flag25;
						bool flag36 = flag35;
						bool flag37 = flag36;
						bool flag38 = flag37;
						bool flag39 = flag38;
						bool flag40 = flag39;
						bool flag41 = flag40;
						bool flag42 = flag41;
						if (flag42)
						{
							this.RegisterDamage(hpConfig, value, num2, false, unscaledTime, fixedTime, false);
						}
						bool flag43 = flag26 && flag25;
						bool flag44 = flag43;
						bool flag45 = flag44;
						bool flag46 = flag45;
						bool flag47 = flag46;
						bool flag48 = flag47;
						bool flag49 = flag48;
						bool flag50 = flag49;
						if (flag50)
						{
							this.RegisterDamage(hpConfig, value, num3, true, unscaledTime, fixedTime, false);
							hitLoss.stamDone = true;
						}
						value.losses.Add(hitLoss);
						bool flag51 = hitLoss.lethal && value.deadAt <= 0f;
						bool flag52 = flag51;
						bool flag53 = flag52;
						bool flag54 = flag53;
						bool flag55 = flag54;
						bool flag56 = flag55;
						bool flag57 = flag56;
						bool flag58 = flag57;
						if (flag58)
						{
							value.deadAt = unscaledTime;
						}
						this.MatchHits(hpConfig, value, unscaledTime);
					}
					else
					{
						bool flag59 = value.evts.Count > 0 || value.losses.Count > 0;
						bool flag60 = flag59;
						bool flag61 = flag60;
						bool flag62 = flag61;
						bool flag63 = flag62;
						bool flag64 = flag63;
						bool flag65 = flag64;
						bool flag66 = flag65;
						if (flag66)
						{
							this.MatchHits(hpConfig, value, unscaledTime);
						}
					}
					bool flag67 = value.deadAt > 0f && unscaledTime - value.deadAt >= 0.35f;
					bool flag68 = flag67;
					bool flag69 = flag68;
					bool flag70 = flag69;
					bool flag71 = flag70;
					bool flag72 = flag71;
					bool flag73 = flag72;
					bool flag74 = flag73;
					if (flag74)
					{
						this.FlushBurst(value, unscaledTime);
						value.deadAt = 0f;
					}
					else
					{
						bool flag75 = value.burstHits > 0 && ResourcePackManager.HitDpsOn(hpConfig, value) && fixedTime - value.burstLast >= hpConfig.dpsSilence;
						bool flag76 = flag75;
						bool flag77 = flag76;
						bool flag78 = flag77;
						bool flag79 = flag78;
						bool flag80 = flag79;
						bool flag81 = flag80;
						bool flag82 = flag81;
						if (flag82)
						{
							this.FlushBurst(value, unscaledTime);
						}
					}
				}
			}
		}
	}

	private static void DrawShadowText(Rect r, string text, GUIStyle st, Color col, float ow)
	{
		GUI.color = new Color(0f, 0f, 0f, col.a);
		GUI.Label(new Rect(r.x + ow, r.y + ow, r.width, r.height), text, st);
		GUI.color = col;
		GUI.Label(r, text, st);
	}

	private int CrystalGoodness(ResourcePackManager.CrystalTrack t)
	{
		User localUser = User.LocalUser;
		Faction faction = (localUser != null) ? localUser.faction : null;
		bool flag = faction == null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		int result;
		if (flag8)
		{
			result = ((t.dir > 0) ? 1 : -1);
		}
		else
		{
			Faction faction2 = Faction.GetFaction(t.cap.gameObject);
			Faction defaultNeutral = Faction.DefaultNeutral;
			bool flag9 = faction2 != null && faction2 != defaultNeutral;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				bool flag17 = Affiliation.GetRelation(faction, faction2) == Affiliation.Relation.Allied;
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				bool flag25 = flag24;
				if (flag25)
				{
					result = ((t.dir > 0) ? -1 : 1);
				}
				else
				{
					result = ((t.dir > 0) ? 1 : -1);
				}
			}
			else
			{
				bool flag26 = t.dir > 0;
				bool flag27 = flag26;
				bool flag28 = flag27;
				bool flag29 = flag28;
				bool flag30 = flag29;
				bool flag31 = flag30;
				bool flag32 = flag31;
				bool flag33 = flag32;
				if (flag33)
				{
					Faction currentCapturers = t.clock.CurrentCapturers;
					bool flag34 = currentCapturers != null && currentCapturers != defaultNeutral;
					bool flag35 = flag34;
					bool flag36 = flag35;
					bool flag37 = flag36;
					bool flag38 = flag37;
					bool flag39 = flag38;
					bool flag40 = flag39;
					bool flag41 = flag40;
					if (flag41)
					{
						t.lastRel = ((Affiliation.GetRelation(faction, currentCapturers) == Affiliation.Relation.Allied) ? 1 : -1);
					}
					result = ((t.lastRel != 0) ? t.lastRel : 1);
				}
				else
				{
					result = ((t.lastRel != 0) ? (-t.lastRel) : -1);
				}
			}
		}
		return result;
	}

	private int CompareBombDist(ResourcePackManager.BombBarrel a, ResourcePackManager.BombBarrel b)
	{
		float num = (a.ex != null) ? (a.ex.transform.position - this.bombSortPos).sqrMagnitude : float.MaxValue;
		float value = (b.ex != null) ? (b.ex.transform.position - this.bombSortPos).sqrMagnitude : float.MaxValue;
		return num.CompareTo(value);
	}

	private static int BombEffLayers(ResourcePackManager.HpConfig cfg)
	{
		return cfg.perfMode ? Mathf.Min(cfg.bombMapLayers, 3) : cfg.bombMapLayers;
	}

	private void FreeAllBombMeshes()
	{
		this.FreeBombGroupList(this.bombShown, null);
		this.FreeBombGroupList(this.bombBuilding, null);
		this.bombShown = new List<ResourcePackManager.BombGroup>();
		this.bombBuilding = null;
		this.bombBarrels.Clear();
		this.bombGlobalSig = int.MinValue;
	}

	private void ScanBarrels(ResourcePackManager.HpConfig cfg)
	{
		Explosifs[] array = UnityEngine.Object.FindObjectsOfType<Explosifs>();
		bool flag = ResourcePackManager.fBombTemplate == null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			ResourcePackManager.fBombTemplate = typeof(Explosifs).GetField("effectTemplate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		}
		List<ResourcePackManager.BombBarrel> list = new List<ResourcePackManager.BombBarrel>();
		foreach (Explosifs explosifs in array)
		{
			bool flag9 = explosifs == null || !explosifs.gameObject.activeInHierarchy || explosifs.GetComponentInParent<Labor>() != null;
			bool flag10 = !flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				string text = (ResourcePackManager.fBombTemplate != null) ? (ResourcePackManager.fBombTemplate.GetValue(explosifs) as string) : "Explosion";
				bool flag17 = text != "Explosion" && text != "Petite";
				bool flag18 = !flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				if (flag24)
				{
					bool flag25 = text == "Explosion";
					ExplosionGameplay explosionGameplay = flag25 ? null : this.GetBombPrefab(text);
					bool flag26 = !flag25 && (explosionGameplay == null || explosionGameplay.unitDamageCurve == null);
					bool flag27 = !flag26;
					bool flag28 = flag27;
					bool flag29 = flag28;
					bool flag30 = flag29;
					bool flag31 = flag30;
					bool flag32 = flag31;
					bool flag33 = flag32;
					if (flag33)
					{
						list.Add(new ResourcePackManager.BombBarrel
						{
							ex = explosifs,
							id = explosifs.GetInstanceID(),
							useTable = flag25,
							curve = (flag25 ? null : explosionGameplay.unitDamageCurve),
							maxV = (flag25 ? 1601f : Mathf.Max(1f, explosionGameplay.unitDamageCurve.intensityFactor))
						});
					}
				}
			}
		}
		int num = cfg.perfMode ? 8 : 24;
		bool flag34 = list.Count > num;
		bool flag35 = flag34;
		bool flag36 = flag35;
		bool flag37 = flag36;
		bool flag38 = flag37;
		bool flag39 = flag38;
		bool flag40 = flag39;
		bool flag41 = flag40;
		if (flag41)
		{
			Camera main = Camera.main;
			bool flag42 = main != null;
			bool flag43 = flag42;
			bool flag44 = flag43;
			bool flag45 = flag44;
			bool flag46 = flag45;
			bool flag47 = flag46;
			bool flag48 = flag47;
			bool flag49 = flag48;
			if (flag49)
			{
				this.bombSortPos = main.transform.position;
				list.Sort(new Comparison<ResourcePackManager.BombBarrel>(this.CompareBombDist));
			}
			list.RemoveRange(num, list.Count - num);
		}
		this.bombBarrels = list;
	}

	private static int BombMeshSig(ResourcePackManager.HpConfig cfg)
	{
		int num = 17;
		num = num * 31 + (cfg.bombMap3D ? 1 : 0);
		num = num * 31 + Mathf.RoundToInt(cfg.bombMapAlpha * 1000f);
		num = num * 31 + Mathf.RoundToInt(cfg.bombMapYOffset * 1000f);
		num = num * 31 + cfg.bombBandMask;
		num = num * 31 + (cfg.bombByDamage ? 1 : 0);
		for (int i = 0; i < cfg.bombDmg.Length; i++)
		{
			num = num * 31 + Mathf.RoundToInt(cfg.bombDmg[i]);
		}
		for (int j = 0; j < cfg.bombColors.Length; j++)
		{
			Color color = cfg.bombColors[j];
			num = num * 31 + (Mathf.RoundToInt(color.r * 255f) << 16 | Mathf.RoundToInt(color.g * 255f) << 8 | Mathf.RoundToInt(color.b * 255f));
		}
		for (int k = 0; k < cfg.bombThr.Length; k++)
		{
			num = num * 31 + Mathf.RoundToInt(cfg.bombThr[k] * 100f);
		}
		return num;
	}

	private static int BombEffRange(ResourcePackManager.HpConfig cfg)
	{
		return Mathf.Clamp(cfg.bombMapRange, 1, 8);
	}

	private static int BombBarrelR(ResourcePackManager.BombBarrel b, int range)
	{
		int a = b.useTable ? 8 : Mathf.CeilToInt(b.curve.maxRange);
		return Mathf.Max(1, Mathf.Min(a, range));
	}

	private static long BombKey(int x, int y, int z)
	{
		return (long)(x + 1048576) << 42 | (long)(y + 1048576) << 21 | (long)(z + 1048576);
	}

	private void FreeBombGroupMeshes(ResourcePackManager.BombGroup g)
	{
		for (int i = 0; i < g.meshes.Count; i++)
		{
			bool flag = g.meshes[i] != null;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				UnityEngine.Object.Destroy(g.meshes[i]);
			}
		}
		g.meshes.Clear();
		g.meshSig = int.MinValue;
	}

	private void FreeBombGroupList(List<ResourcePackManager.BombGroup> list, List<ResourcePackManager.BombGroup> keep)
	{
		bool flag = list == null;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			for (int i = 0; i < list.Count; i++)
			{
				bool flag9 = keep == null || !keep.Contains(list[i]);
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					this.FreeBombGroupMeshes(list[i]);
				}
			}
		}
	}

	private void RefreshBarrelVoxels()
	{
		for (int i = 0; i < this.bombBarrels.Count; i++)
		{
			ResourcePackManager.BombBarrel bombBarrel = this.bombBarrels[i];
			bool flag = bombBarrel.ex == null;
			bool flag2 = !flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				Vector3 vector = XYZ.FromVector3(bombBarrel.ex.transform.position).ToVector3();
				bombBarrel.cc = vector;
				bombBarrel.vx = Mathf.RoundToInt(vector.x);
				bombBarrel.vy = Mathf.RoundToInt(vector.y);
				bombBarrel.vz = Mathf.RoundToInt(vector.z);
			}
		}
	}

	private static int BombBarrelHash(ResourcePackManager.BombBarrel b)
	{
		return b.id * 397 ^ b.vx * 73856093 ^ b.vy * 19349663 ^ b.vz * 83492791 ^ (b.useTable ? 5 : 0);
	}

	private static int BombShapeSig(ResourcePackManager.HpConfig cfg)
	{
		int num = 17;
		num = num * 31 + ResourcePackManager.BombEffRange(cfg);
		num = num * 31 + ResourcePackManager.BombEffLayers(cfg);
		return num * 31 + (cfg.bombMap3D ? 1 : 0);
	}

	private int BombFindParent(int[] parent, int i)
	{
		while (parent[i] != i)
		{
			parent[i] = parent[parent[i]];
			i = parent[i];
		}
		return i;
	}

	private void RegroupBombs(ResourcePackManager.HpConfig cfg)
	{
		int range = ResourcePackManager.BombEffRange(cfg);
		int num = ResourcePackManager.BombShapeSig(cfg);
		int count = this.bombBarrels.Count;
		int[] array = new int[count];
		for (int i = 0; i < count; i++)
		{
			array[i] = i;
		}
		for (int j = 0; j < count; j++)
		{
			bool flag = this.bombBarrels[j].ex == null;
			bool flag2 = !flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				int num2 = ResourcePackManager.BombBarrelR(this.bombBarrels[j], range);
				for (int k = j + 1; k < count; k++)
				{
					ResourcePackManager.BombBarrel bombBarrel = this.bombBarrels[j];
					ResourcePackManager.BombBarrel bombBarrel2 = this.bombBarrels[k];
					bool flag9 = bombBarrel2.ex == null;
					bool flag10 = !flag9;
					bool flag11 = flag10;
					bool flag12 = flag11;
					bool flag13 = flag12;
					bool flag14 = flag13;
					bool flag15 = flag14;
					bool flag16 = flag15;
					if (flag16)
					{
						int num3 = num2 + ResourcePackManager.BombBarrelR(bombBarrel2, range) + 1;
						int num4 = bombBarrel.vx - bombBarrel2.vx;
						int num5 = bombBarrel.vy - bombBarrel2.vy;
						int num6 = bombBarrel.vz - bombBarrel2.vz;
						bool flag17 = num4 * num4 + num5 * num5 + num6 * num6 <= num3 * num3;
						bool flag18 = flag17;
						bool flag19 = flag18;
						bool flag20 = flag19;
						bool flag21 = flag20;
						bool flag22 = flag21;
						bool flag23 = flag22;
						bool flag24 = flag23;
						if (flag24)
						{
							int num7 = this.BombFindParent(array, j);
							int num8 = this.BombFindParent(array, k);
							bool flag25 = num7 != num8;
							bool flag26 = flag25;
							bool flag27 = flag26;
							bool flag28 = flag27;
							bool flag29 = flag28;
							bool flag30 = flag29;
							bool flag31 = flag30;
							bool flag32 = flag31;
							if (flag32)
							{
								array[num8] = num7;
							}
						}
					}
				}
			}
		}
		Dictionary<int, ResourcePackManager.BombGroup> dictionary = new Dictionary<int, ResourcePackManager.BombGroup>();
		List<ResourcePackManager.BombGroup> list = new List<ResourcePackManager.BombGroup>();
		for (int l = 0; l < count; l++)
		{
			bool flag33 = this.bombBarrels[l].ex == null;
			bool flag34 = !flag33;
			bool flag35 = flag34;
			bool flag36 = flag35;
			bool flag37 = flag36;
			bool flag38 = flag37;
			bool flag39 = flag38;
			bool flag40 = flag39;
			if (flag40)
			{
				int key = this.BombFindParent(array, l);
				ResourcePackManager.BombGroup bombGroup;
				bool flag41 = !dictionary.TryGetValue(key, out bombGroup);
				bool flag42 = flag41;
				bool flag43 = flag42;
				bool flag44 = flag43;
				bool flag45 = flag44;
				bool flag46 = flag45;
				bool flag47 = flag46;
				bool flag48 = flag47;
				if (flag48)
				{
					bombGroup = new ResourcePackManager.BombGroup();
					dictionary[key] = bombGroup;
					list.Add(bombGroup);
				}
				bombGroup.members.Add(this.bombBarrels[l]);
			}
		}
		List<ResourcePackManager.BombGroup> list2 = new List<ResourcePackManager.BombGroup>();
		for (int m = 0; m < list.Count; m++)
		{
			ResourcePackManager.BombGroup bombGroup2 = list[m];
			int num9 = num;
			Vector3 vector = Vector3.zero;
			for (int n = 0; n < bombGroup2.members.Count; n++)
			{
				num9 += ResourcePackManager.BombBarrelHash(bombGroup2.members[n]);
				vector += bombGroup2.members[n].cc;
			}
			vector /= (float)bombGroup2.members.Count;
			float num10 = 0f;
			for (int num11 = 0; num11 < bombGroup2.members.Count; num11++)
			{
				num10 = Mathf.Max(num10, (bombGroup2.members[num11].cc - vector).magnitude + (float)ResourcePackManager.BombBarrelR(bombGroup2.members[num11], range) + 1f);
			}
			bombGroup2.sig = num9;
			bombGroup2.center = vector;
			bombGroup2.reach = num10;
			ResourcePackManager.BombGroup bombGroup3 = ResourcePackManager.FindBombGroup(this.bombBuilding, bombGroup2.sig) ?? ResourcePackManager.FindBombGroup(this.bombShown, bombGroup2.sig);
			list2.Add((bombGroup3 != null) ? bombGroup3 : bombGroup2);
		}
		bool flag49 = this.bombBuilding != null;
		bool flag50 = flag49;
		bool flag51 = flag50;
		bool flag52 = flag51;
		bool flag53 = flag52;
		bool flag54 = flag53;
		bool flag55 = flag54;
		bool flag56 = flag55;
		if (flag56)
		{
			for (int num12 = 0; num12 < this.bombBuilding.Count; num12++)
			{
				ResourcePackManager.BombGroup bombGroup4 = this.bombBuilding[num12];
				bool flag57 = !list2.Contains(bombGroup4) && !this.bombShown.Contains(bombGroup4);
				bool flag58 = flag57;
				bool flag59 = flag58;
				bool flag60 = flag59;
				bool flag61 = flag60;
				bool flag62 = flag61;
				bool flag63 = flag62;
				bool flag64 = flag63;
				if (flag64)
				{
					this.FreeBombGroupMeshes(bombGroup4);
				}
			}
		}
		this.bombBuilding = list2;
	}

	private static ResourcePackManager.BombGroup FindBombGroup(List<ResourcePackManager.BombGroup> list, int sig)
	{
		bool flag = list == null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		ResourcePackManager.BombGroup result;
		if (flag8)
		{
			result = null;
		}
		else
		{
			for (int i = 0; i < list.Count; i++)
			{
				bool flag9 = list[i].sig == sig && list[i].built;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					return list[i];
				}
			}
			result = null;
		}
		return result;
	}

	private void MergeBombGroup(ResourcePackManager.BombGroup g, ResourcePackManager.HpConfig cfg)
	{
		int num = ResourcePackManager.BombEffRange(cfg);
		int num2 = num * num;
		int value = ResourcePackManager.BombEffLayers(cfg);
		this.bombMerge.Clear();
		g.cells.Clear();
		g.numCells.Clear();
		float num3 = 1f;
		for (int i = 0; i < g.members.Count; i++)
		{
			ResourcePackManager.BombBarrel bombBarrel = g.members[i];
			num3 = Mathf.Max(num3, bombBarrel.maxV);
			int num4 = ResourcePackManager.BombBarrelR(bombBarrel, num);
			int num5 = cfg.bombMap3D ? Mathf.Clamp(value, 1, num4) : 0;
			for (int j = -num5; j <= num5; j++)
			{
				for (int k = -num4; k <= num4; k++)
				{
					for (int l = -num4; l <= num4; l++)
					{
						int num6 = k * k + j * j + l * l;
						bool flag = num6 > num2;
						bool flag2 = !flag;
						bool flag3 = flag2;
						bool flag4 = flag3;
						bool flag5 = flag4;
						bool flag6 = flag5;
						bool flag7 = flag6;
						bool flag8 = flag7;
						if (flag8)
						{
							bool useTable = bombBarrel.useTable;
							bool flag9 = useTable;
							bool flag10 = flag9;
							bool flag11 = flag10;
							bool flag12 = flag11;
							bool flag13 = flag12;
							bool flag14 = flag13;
							bool flag15 = flag14;
							int num7;
							if (flag15)
							{
								num7 = ResourcePackManager.BarrelDamageAt(num6);
							}
							else
							{
								float num8 = Mathf.Sqrt((float)num6);
								num7 = ((num8 > bombBarrel.curve.maxRange) ? 0 : Mathf.RoundToInt(bombBarrel.curve.Evaluate(num8)));
							}
							bool flag16 = num7 <= 0;
							bool flag17 = !flag16;
							bool flag18 = flag17;
							bool flag19 = flag18;
							bool flag20 = flag19;
							bool flag21 = flag20;
							bool flag22 = flag21;
							bool flag23 = flag22;
							if (flag23)
							{
								long key = ResourcePackManager.BombKey(bombBarrel.vx + k, bombBarrel.vy + j, bombBarrel.vz + l);
								ResourcePackManager.BombCell bombCell;
								bool flag24 = this.bombMerge.TryGetValue(key, out bombCell);
								bool flag25 = flag24;
								bool flag26 = flag25;
								bool flag27 = flag26;
								bool flag28 = flag27;
								bool flag29 = flag28;
								bool flag30 = flag29;
								bool flag31 = flag30;
								if (flag31)
								{
									bombCell.value += num7;
									bool flag32 = j == 0;
									bool flag33 = flag32;
									bool flag34 = flag33;
									bool flag35 = flag34;
									bool flag36 = flag35;
									bool flag37 = flag36;
									bool flag38 = flag37;
									bool flag39 = flag38;
									if (flag39)
									{
										bombCell.layer = true;
									}
								}
								else
								{
									bombCell = new ResourcePackManager.BombCell();
									bombCell.center = bombBarrel.cc + new Vector3((float)k, (float)j, (float)l);
									bombCell.value = num7;
									bombCell.layer = (j == 0);
									this.bombMerge[key] = bombCell;
									g.cells.Add(bombCell);
								}
							}
						}
					}
				}
			}
		}
		this.bombMerge.Clear();
		for (int m = 0; m < g.cells.Count; m++)
		{
			ResourcePackManager.BombCell bombCell2 = g.cells[m];
			bombCell2.t = (float)bombCell2.value / num3;
			bombCell2.text = bombCell2.value.ToString();
			bool layer = bombCell2.layer;
			bool flag40 = layer;
			bool flag41 = flag40;
			bool flag42 = flag41;
			bool flag43 = flag42;
			bool flag44 = flag43;
			bool flag45 = flag44;
			bool flag46 = flag45;
			if (flag46)
			{
				g.numCells.Add(bombCell2);
			}
		}
		g.built = true;
	}
}
}
