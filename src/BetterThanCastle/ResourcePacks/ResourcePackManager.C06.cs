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

	private void ForceShowHp()
	{
		ResourcePackManager.HpConfig hpConfig = this.hpCfg;
		bool flag = hpConfig == null || this.hpRings.Count == 0 || (!this.AlwaysOwnNow(hpConfig) && !this.AlwaysEnemyNow(hpConfig) && this.hpForcedCount <= 0);
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			int num = 0;
			for (int i = 0; i < this.hpRings.Count; i++)
			{
				ResourcePackManager.HpRing hpRing = this.hpRings[i];
				bool flag9 = hpRing.owner == null || hpRing.rend == null || hpRing.anim == null;
				bool flag10 = !flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					bool flag17 = !hpRing.rend.enabled || !hpRing.rend.gameObject.activeInHierarchy;
					bool flag18 = flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					bool flag21 = flag20;
					bool flag22 = flag21;
					bool flag23 = flag22;
					bool flag24 = flag23;
					if (flag24)
					{
						hpRing.forcedShow = false;
						hpRing.forcedStam = false;
					}
					else
					{
						bool flag25 = this.IsEnemyRing(hpRing) ? this.AlwaysEnemyNow(hpConfig) : this.AlwaysOwnNow(hpConfig);
						bool flag26 = flag25;
						bool flag27 = flag26;
						bool flag28 = flag27;
						bool flag29 = flag28;
						bool flag30 = flag29;
						bool flag31 = flag30;
						bool flag32 = flag31;
						bool flag33 = flag32;
						if (flag33)
						{
							hpRing.anim.SetBool(ResourcePackManager.hpShowHpId, true);
							hpRing.forcedShow = true;
							bool flag34 = !hpRing.anim.GetBool(ResourcePackManager.hpShowStamId) && hpRing.anim.GetFloat(ResourcePackManager.hpStamId) > 0.001f;
							bool flag35 = flag34;
							bool flag36 = flag35;
							bool flag37 = flag36;
							bool flag38 = flag37;
							bool flag39 = flag38;
							bool flag40 = flag39;
							bool flag41 = flag40;
							if (flag41)
							{
								hpRing.anim.SetBool(ResourcePackManager.hpShowStamId, true);
								hpRing.forcedStam = true;
							}
							num++;
						}
						else
						{
							bool forcedShow = hpRing.forcedShow;
							bool flag42 = forcedShow;
							bool flag43 = flag42;
							bool flag44 = flag43;
							bool flag45 = flag44;
							bool flag46 = flag45;
							bool flag47 = flag46;
							bool flag48 = flag47;
							if (flag48)
							{
								hpRing.forcedShow = false;
								bool flag49 = hpRing.anim.GetFloat(ResourcePackManager.hpParamId) >= 0.999f;
								bool flag50 = flag49;
								bool flag51 = flag50;
								bool flag52 = flag51;
								bool flag53 = flag52;
								bool flag54 = flag53;
								bool flag55 = flag54;
								bool flag56 = flag55;
								if (flag56)
								{
									hpRing.anim.SetBool(ResourcePackManager.hpShowHpId, false);
								}
							}
							bool forcedStam = hpRing.forcedStam;
							bool flag57 = forcedStam;
							bool flag58 = flag57;
							bool flag59 = flag58;
							bool flag60 = flag59;
							bool flag61 = flag60;
							bool flag62 = flag61;
							bool flag63 = flag62;
							if (flag63)
							{
								hpRing.forcedStam = false;
								bool flag64 = hpRing.anim.GetFloat(ResourcePackManager.hpStamId) >= 0.999f;
								bool flag65 = flag64;
								bool flag66 = flag65;
								bool flag67 = flag66;
								bool flag68 = flag67;
								bool flag69 = flag68;
								bool flag70 = flag69;
								bool flag71 = flag70;
								if (flag71)
								{
									hpRing.anim.SetBool(ResourcePackManager.hpShowStamId, false);
								}
							}
						}
					}
				}
			}
			this.hpForcedCount = num;
		}
	}

	private bool TryRegisterRing(SelectionCircle sc)
	{
		int instanceID = sc.GetInstanceID();
		bool flag = this.hpRingIds.Contains(instanceID);
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
			Renderer renderer = ResourcePackManager.circleRendField.GetValue(sc) as Renderer;
			Animator animator = ResourcePackManager.circleAnimField.GetValue(sc) as Animator;
			bool flag9 = renderer == null || animator == null || renderer.sharedMaterial == null || !renderer.sharedMaterial.HasProperty(ResourcePackManager.ringPropIds[0]);
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
				ResourcePackManager.HpRing hpRing = new ResourcePackManager.HpRing();
				hpRing.owner = sc;
				hpRing.rend = renderer;
				hpRing.anim = animator;
				hpRing.block = new MaterialPropertyBlock();
				hpRing.originals = new Color[ResourcePackManager.ringPropIds.Length];
				for (int i = 0; i < ResourcePackManager.ringPropIds.Length; i++)
				{
					hpRing.originals[i] = (renderer.sharedMaterial.HasProperty(ResourcePackManager.ringPropIds[i]) ? renderer.sharedMaterial.GetColor(ResourcePackManager.ringPropIds[i]) : Color.white);
				}
				this.hpRings.Add(hpRing);
				this.hpRingIds.Add(instanceID);
				result = true;
			}
		}
		return result;
	}

	private void UpdateAlwaysRings()
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
			bool flag9 = !this.AlwaysOwnNow(hpConfig) && !this.AlwaysEnemyNow(hpConfig) && this.autoRings.Count == 0;
			bool flag10 = !flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				bool flag17 = !ResourcePackManager.InitHpReflection();
				bool flag18 = !flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				if (flag24)
				{
					float unscaledTime = Time.unscaledTime;
					Dictionary<GameObject, SelectionCircle> dictionary = null;
					SelectionCircleDispenser selectionCircleDispenser = SelectionCircleDispenser.Instance;
					bool flag25 = selectionCircleDispenser != null;
					bool flag26 = flag25;
					bool flag27 = flag26;
					bool flag28 = flag27;
					bool flag29 = flag28;
					bool flag30 = flag29;
					bool flag31 = flag30;
					bool flag32 = flag31;
					if (flag32)
					{
						bool flag33 = ResourcePackManager.dispenserMapField == null;
						bool flag34 = flag33;
						bool flag35 = flag34;
						bool flag36 = flag35;
						bool flag37 = flag36;
						bool flag38 = flag37;
						bool flag39 = flag38;
						bool flag40 = flag39;
						if (flag40)
						{
							ResourcePackManager.dispenserMapField = typeof(SelectionCircleDispenser).GetField("highlightByGameObject", BindingFlags.Instance | BindingFlags.NonPublic);
						}
						bool flag41 = ResourcePackManager.dispenserMapField != null;
						bool flag42 = flag41;
						bool flag43 = flag42;
						bool flag44 = flag43;
						bool flag45 = flag44;
						bool flag46 = flag45;
						bool flag47 = flag46;
						bool flag48 = flag47;
						if (flag48)
						{
							dictionary = (ResourcePackManager.dispenserMapField.GetValue(selectionCircleDispenser) as Dictionary<GameObject, SelectionCircle>);
						}
					}
					bool flag49 = this.autoRings.Count > 0;
					bool flag50 = flag49;
					bool flag51 = flag50;
					bool flag52 = flag51;
					bool flag53 = flag52;
					bool flag54 = flag53;
					bool flag55 = flag54;
					bool flag56 = flag55;
					if (flag56)
					{
						this.autoTmp.Clear();
						foreach (KeyValuePair<Labor, ResourcePackManager.AutoRing> keyValuePair in this.autoRings)
						{
							this.autoTmp.Add(keyValuePair.Key);
						}
						for (int i = 0; i < this.autoTmp.Count; i++)
						{
							Labor labor = this.autoTmp[i];
							ResourcePackManager.AutoRing autoRing = this.autoRings[labor];
							bool flag57 = labor != null && autoRing.circle != null && autoRing.rend != null && autoRing.go != null && autoRing.go.activeInHierarchy && (autoRing.enemy ? this.AlwaysEnemyNow(hpConfig) : this.AlwaysOwnNow(hpConfig));
							bool flag58 = flag57 && autoRing.enemy;
							bool flag59 = flag58;
							bool flag60 = flag59;
							bool flag61 = flag60;
							bool flag62 = flag61;
							bool flag63 = flag62;
							bool flag64 = flag63;
							bool flag65 = flag64;
							if (flag65)
							{
								flag57 = (labor.vision != null && labor.vision.IsVisibleForMe());
							}
							bool flag66 = !flag57;
							bool flag67 = flag66;
							bool flag68 = flag67;
							bool flag69 = flag68;
							bool flag70 = flag69;
							bool flag71 = flag70;
							bool flag72 = flag71;
							bool flag73 = flag72;
							if (flag73)
							{
								ResourcePackManager.ReleaseAutoRing(autoRing);
								this.autoRings.Remove(labor);
							}
							else
							{
								SelectionCircle x;
								bool flag74 = dictionary != null && dictionary.TryGetValue(autoRing.go, out x) && x != null && x != autoRing.circle;
								bool flag75 = autoRing.rend.enabled == flag74;
								bool flag76 = flag75;
								bool flag77 = flag76;
								bool flag78 = flag77;
								bool flag79 = flag78;
								bool flag80 = flag79;
								bool flag81 = flag80;
								bool flag82 = flag81;
								if (flag82)
								{
									autoRing.rend.enabled = !flag74;
								}
								bool flag83 = !flag74 && unscaledTime >= autoRing.nextAdapt;
								bool flag84 = flag83;
								bool flag85 = flag84;
								bool flag86 = flag85;
								bool flag87 = flag86;
								bool flag88 = flag87;
								bool flag89 = flag88;
								bool flag90 = flag89;
								if (flag90)
								{
									autoRing.nextAdapt = unscaledTime + 0.15f;
									try
									{
										autoRing.circle.AdaptToBricktron(labor);
									}
									catch (Exception)
									{
									}
								}
							}
						}
					}
					bool flag91 = unscaledTime >= this.autoNextScan;
					bool flag92 = flag91;
					bool flag93 = flag92;
					bool flag94 = flag93;
					bool flag95 = flag94;
					bool flag96 = flag95;
					bool flag97 = flag96;
					bool flag98 = flag97;
					if (flag98)
					{
						this.autoNextScan = unscaledTime + 0.25f;
						bool flag99 = this.AlwaysOwnNow(hpConfig);
						bool flag100 = flag99;
						bool flag101 = flag100;
						bool flag102 = flag101;
						bool flag103 = flag102;
						bool flag104 = flag103;
						bool flag105 = flag104;
						bool flag106 = flag105;
						if (flag106)
						{
							this.ScanAutoRings(UIGameObserver.bricktrons, SelectionCircle.MainColors.Local, false);
							this.ScanAutoRings(UIGameObserver.alliedBricktrons, SelectionCircle.MainColors.Allies, false);
						}
						bool flag107 = this.AlwaysEnemyNow(hpConfig);
						bool flag108 = flag107;
						bool flag109 = flag108;
						bool flag110 = flag109;
						bool flag111 = flag110;
						bool flag112 = flag111;
						bool flag113 = flag112;
						bool flag114 = flag113;
						if (flag114)
						{
							this.ScanAutoRings(UIGameObserver.enemyBricktrons, SelectionCircle.MainColors.Enemies, true);
						}
					}
				}
			}
		}
	}

	private void ScanAutoRings(UIGameObserver.Bricktron observer, SelectionCircle.MainColors color, bool enemy)
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
			List<Labor> list = new List<Labor>(observer.CheckCreation());
			for (int i = 0; i < list.Count; i++)
			{
				Labor labor = list[i];
				bool flag9 = labor == null || this.autoRings.ContainsKey(labor) || !labor.gameObject.activeInHierarchy;
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
							try
							{
								SelectionCircle selectionCircle = SelectionCircle.New(labor.gameObject);
								bool flag33 = selectionCircle == null;
								bool flag34 = !flag33;
								bool flag35 = flag34;
								bool flag36 = flag35;
								bool flag37 = flag36;
								bool flag38 = flag37;
								bool flag39 = flag38;
								bool flag40 = flag39;
								if (flag40)
								{
									ResourcePackManager.AutoRing autoRing = new ResourcePackManager.AutoRing();
									autoRing.labor = labor;
									autoRing.go = labor.gameObject;
									autoRing.circle = selectionCircle;
									autoRing.rend = (ResourcePackManager.circleRendField.GetValue(selectionCircle) as Renderer);
									autoRing.enemy = enemy;
									this.autoRings[labor] = autoRing;
									selectionCircle.MainColor(color);
									selectionCircle.AdaptToBricktron(labor);
									this.TryRegisterRing(selectionCircle);
								}
							}
							catch (Exception ex)
							{
								ResourcePackManager.PackWarn("[ResourcePacks] Always show: cannot create ring: " + ex.Message);
								break;
							}
						}
					}
				}
			}
		}
	}

	private static void ReleaseAutoRing(ResourcePackManager.AutoRing a)
	{
		bool flag = a.circle == null;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			try
			{
				ObjectPoolSingleton.Release(a.circle.gameObject);
			}
			catch (Exception)
			{
			}
		}
	}

	private void SetEnemyTex(bool on)
	{
		bool flag = this.enemyTexOn == on;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			this.enemyTexOn = on;
			this.SaveOptions();
			this.enemyTexNext = 0f;
			if (on)
			{
				try
				{
					bool flag9 = this.currentPack != null;
					bool flag10 = flag9;
					bool flag11 = flag10;
					bool flag12 = flag11;
					bool flag13 = flag12;
					bool flag14 = flag13;
					bool flag15 = flag14;
					bool flag16 = flag15;
					if (flag16)
					{
						Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.Combine(ResourcePackManager.RootDir, this.currentPack), "Enemy"));
					}
				}
				catch (Exception)
				{
				}
			}
			else
			{
				this.RevertEnemyTextures();
			}
		}
	}

	private void LoadEnemyTexFiles()
	{
		bool flag = this.currentPack == null;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			string path = System.IO.Path.Combine(System.IO.Path.Combine(ResourcePackManager.RootDir, this.currentPack), "Enemy");
			bool flag9 = !Directory.Exists(path);
			bool flag10 = !flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				string[] files = Directory.GetFiles(path, "*.png");
				for (int i = 0; i < files.Length; i++)
				{
					string text = System.IO.Path.GetFileNameWithoutExtension(files[i]);
					int num = text.IndexOf("__", StringComparison.Ordinal);
					bool flag17 = num > 0;
					bool flag18 = flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					bool flag21 = flag20;
					bool flag22 = flag21;
					bool flag23 = flag22;
					bool flag24 = flag23;
					if (flag24)
					{
						text = text.Substring(0, num);
					}
					DateTime lastWriteTimeUtc = File.GetLastWriteTimeUtc(files[i]);
					DateTime d;
					bool flag25 = this.enemyTexStamp.TryGetValue(text, out d) && d == lastWriteTimeUtc;
					bool flag26 = !flag25;
					bool flag27 = flag26;
					bool flag28 = flag27;
					bool flag29 = flag28;
					bool flag30 = flag29;
					bool flag31 = flag30;
					bool flag32 = flag31;
					if (flag32)
					{
						try
						{
							Texture2D texture2D;
							bool flag33 = !this.enemyTexByName.TryGetValue(text, out texture2D) || texture2D == null;
							bool flag34 = flag33;
							bool flag35 = flag34;
							bool flag36 = flag35;
							bool flag37 = flag36;
							bool flag38 = flag37;
							bool flag39 = flag38;
							bool flag40 = flag39;
							if (flag40)
							{
								texture2D = new Texture2D(2, 2, TextureFormat.RGBA32, true);
								texture2D.hideFlags = HideFlags.HideAndDontSave;
								this.enemyTexByName[text] = texture2D;
							}
							bool flag41 = texture2D.LoadImage(File.ReadAllBytes(files[i]));
							bool flag42 = flag41;
							bool flag43 = flag42;
							bool flag44 = flag43;
							bool flag45 = flag44;
							bool flag46 = flag45;
							bool flag47 = flag46;
							bool flag48 = flag47;
							if (flag48)
							{
								this.enemyTexStamp[text] = lastWriteTimeUtc;
								ResourcePackManager.PackLog(string.Concat(new object[]
								{
									"[ResourcePacks] Enemy texture loaded: ",
									text,
									" (",
									texture2D.width,
									"x",
									texture2D.height,
									")"
								}));
							}
						}
						catch (Exception ex)
						{
							this.enemyTexStamp[text] = lastWriteTimeUtc;
							ResourcePackManager.PackWarn("[ResourcePacks] Cannot load enemy texture " + files[i] + ": " + ex.Message);
						}
					}
				}
			}
		}
	}

	private void UpdateEnemyTextures()
	{
		bool flag = !this.enemyTexOn && this.enemyMats.Count == 0;
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
			bool flag9 = unscaledTime < this.enemyTexNext;
			bool flag10 = !flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				this.enemyTexNext = unscaledTime + 1f;
				bool flag17 = this.enemyTexPack != this.currentPack;
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				if (flag24)
				{
					this.RevertEnemyTextures();
					foreach (KeyValuePair<string, Texture2D> keyValuePair in this.enemyTexByName)
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
							UnityEngine.Object.Destroy(keyValuePair.Value);
						}
					}
					this.enemyTexByName.Clear();
					this.enemyTexStamp.Clear();
					this.enemyTexPack = this.currentPack;
					this.enemyTexTick = 0;
				}
				bool flag33 = !this.enemyTexOn;
				bool flag34 = !flag33;
				bool flag35 = flag34;
				bool flag36 = flag35;
				bool flag37 = flag36;
				bool flag38 = flag37;
				bool flag39 = flag38;
				bool flag40 = flag39;
				if (flag40)
				{
					this.enemyTexTick++;
					try
					{
						bool flag41 = this.enemyTexTick % 2 == 1;
						bool flag42 = flag41;
						bool flag43 = flag42;
						bool flag44 = flag43;
						bool flag45 = flag44;
						bool flag46 = flag45;
						bool flag47 = flag46;
						bool flag48 = flag47;
						if (flag48)
						{
							this.LoadEnemyTexFiles();
						}
						bool flag49 = this.enemyTexByName.Count == 0;
						bool flag50 = !flag49;
						bool flag51 = flag50;
						bool flag52 = flag51;
						bool flag53 = flag52;
						bool flag54 = flag53;
						bool flag55 = flag54;
						bool flag56 = flag55;
						if (flag56)
						{
							bool flag57 = this.enemyTexTick % 10 == 0;
							bool flag58 = flag57;
							bool flag59 = flag58;
							bool flag60 = flag59;
							bool flag61 = flag60;
							bool flag62 = flag61;
							bool flag63 = flag62;
							bool flag64 = flag63;
							if (flag64)
							{
								this.PruneEnemyMats();
							}
							UIGameObserver.Bricktron enemyBricktrons = UIGameObserver.enemyBricktrons;
							bool flag65 = enemyBricktrons != null;
							bool flag66 = flag65;
							bool flag67 = flag66;
							bool flag68 = flag67;
							bool flag69 = flag68;
							bool flag70 = flag69;
							bool flag71 = flag70;
							bool flag72 = flag71;
							if (flag72)
							{
								List<Labor> list = new List<Labor>(enemyBricktrons.CheckCreation());
								for (int i = 0; i < list.Count; i++)
								{
									bool flag73 = list[i] != null;
									bool flag74 = flag73;
									bool flag75 = flag74;
									bool flag76 = flag75;
									bool flag77 = flag76;
									bool flag78 = flag77;
									bool flag79 = flag78;
									bool flag80 = flag79;
									if (flag80)
									{
										this.ApplyEnemyTextures(list[i]);
									}
								}
							}
							bool flag81 = this.enemyMatIds.Count > 0;
							bool flag82 = flag81;
							bool flag83 = flag82;
							bool flag84 = flag83;
							bool flag85 = flag84;
							bool flag86 = flag85;
							bool flag87 = flag86;
							bool flag88 = flag87;
							if (flag88)
							{
								UIGameObserver.Bricktron bricktrons = UIGameObserver.bricktrons;
								bool flag89 = bricktrons != null;
								bool flag90 = flag89;
								bool flag91 = flag90;
								bool flag92 = flag91;
								bool flag93 = flag92;
								bool flag94 = flag93;
								bool flag95 = flag94;
								bool flag96 = flag95;
								if (flag96)
								{
									List<Labor> list2 = new List<Labor>(bricktrons.CheckCreation());
									for (int j = 0; j < list2.Count; j++)
									{
										bool flag97 = list2[j] != null;
										bool flag98 = flag97;
										bool flag99 = flag98;
										bool flag100 = flag99;
										bool flag101 = flag100;
										bool flag102 = flag101;
										bool flag103 = flag102;
										bool flag104 = flag103;
										if (flag104)
										{
											this.RevertUnitTextures(list2[j]);
										}
									}
								}
							}
						}
					}
					catch (Exception ex)
					{
						ResourcePackManager.PackWarn("[ResourcePacks] Enemy textures: " + ex.Message);
					}
				}
			}
		}
	}

	private static Texture GetTexSafe(Material m, string prop)
	{
		return m.HasProperty(prop) ? m.GetTexture(prop) : null;
	}

	private Texture2D EnemyTexFor(Texture t)
	{
		bool flag = t == null || string.IsNullOrEmpty(t.name);
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		Texture2D result;
		if (flag8)
		{
			result = null;
		}
		else
		{
			Texture2D texture2D;
			bool flag9 = this.enemyTexByName.TryGetValue(t.name, out texture2D) && texture2D != null && texture2D != t;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				result = texture2D;
			}
			else
			{
				result = null;
			}
		}
		return result;
	}

	private void ApplyEnemyTextures(Labor labor)
	{
		this.enemyRendTmp.Clear();
		labor.GetComponentsInChildren<Renderer>(true, this.enemyRendTmp);
		for (int i = 0; i < this.enemyRendTmp.Count; i++)
		{
			Renderer renderer = this.enemyRendTmp[i];
			bool flag = renderer == null;
			bool flag2 = !flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				Material[] sharedMaterials = renderer.sharedMaterials;
				bool flag9 = false;
				for (int j = 0; j < sharedMaterials.Length; j++)
				{
					Material material = sharedMaterials[j];
					bool flag10 = material == null;
					bool flag11 = !flag10;
					bool flag12 = flag11;
					bool flag13 = flag12;
					bool flag14 = flag13;
					bool flag15 = flag14;
					bool flag16 = flag15;
					bool flag17 = flag16;
					if (flag17)
					{
						int instanceID = material.GetInstanceID();
						bool flag18 = this.enemyMatIds.Contains(instanceID) || this.enemyMatSeen.Contains(instanceID);
						bool flag19 = !flag18;
						bool flag20 = flag19;
						bool flag21 = flag20;
						bool flag22 = flag21;
						bool flag23 = flag22;
						bool flag24 = flag23;
						bool flag25 = flag24;
						if (flag25)
						{
							bool flag26 = false;
							int num = 0;
							while (num < ResourcePackManager.EnemyTexProps.Length && !flag26)
							{
								flag26 = (this.EnemyTexFor(ResourcePackManager.GetTexSafe(material, ResourcePackManager.EnemyTexProps[num])) != null);
								num++;
							}
							bool flag27 = !flag26;
							bool flag28 = flag27;
							bool flag29 = flag28;
							bool flag30 = flag29;
							bool flag31 = flag30;
							bool flag32 = flag31;
							bool flag33 = flag32;
							bool flag34 = flag33;
							if (flag34)
							{
								this.enemyMatSeen.Add(instanceID);
							}
							else
							{
								Material material2 = material;
								Material shared = null;
								bool flag35 = !material.name.EndsWith("(Instance)", StringComparison.Ordinal);
								bool flag36 = flag35;
								bool flag37 = flag36;
								bool flag38 = flag37;
								bool flag39 = flag38;
								bool flag40 = flag39;
								bool flag41 = flag40;
								bool flag42 = flag41;
								if (flag42)
								{
									material2 = new Material(material);
									material2.name = material.name + " (Instance)";
									shared = material;
									sharedMaterials[j] = material2;
									flag9 = true;
								}
								int instanceID2 = material2.GetInstanceID();
								for (int k = 0; k < ResourcePackManager.EnemyTexProps.Length; k++)
								{
									Texture texSafe = ResourcePackManager.GetTexSafe(material2, ResourcePackManager.EnemyTexProps[k]);
									Texture2D texture2D = this.EnemyTexFor(texSafe);
									bool flag43 = texture2D == null;
									bool flag44 = !flag43;
									bool flag45 = flag44;
									bool flag46 = flag45;
									bool flag47 = flag46;
									bool flag48 = flag47;
									bool flag49 = flag48;
									bool flag50 = flag49;
									if (flag50)
									{
										texture2D.wrapMode = texSafe.wrapMode;
										texture2D.filterMode = texSafe.filterMode;
										texture2D.anisoLevel = texSafe.anisoLevel;
										ResourcePackManager.EnemyMat enemyMat = new ResourcePackManager.EnemyMat();
										enemyMat.mat = material2;
										enemyMat.prop = ResourcePackManager.EnemyTexProps[k];
										enemyMat.orig = texSafe;
										enemyMat.id = instanceID2;
										enemyMat.rend = renderer;
										enemyMat.slot = j;
										enemyMat.shared = shared;
										this.enemyMats.Add(enemyMat);
										material2.SetTexture(ResourcePackManager.EnemyTexProps[k], texture2D);
									}
								}
								this.enemyMatIds.Add(instanceID2);
							}
						}
					}
				}
				bool flag51 = flag9;
				bool flag52 = flag51;
				bool flag53 = flag52;
				bool flag54 = flag53;
				bool flag55 = flag54;
				bool flag56 = flag55;
				bool flag57 = flag56;
				bool flag58 = flag57;
				if (flag58)
				{
					renderer.sharedMaterials = sharedMaterials;
				}
			}
		}
	}

	private void RevertUnitTextures(Labor labor)
	{
		this.enemyRendTmp.Clear();
		labor.GetComponentsInChildren<Renderer>(true, this.enemyRendTmp);
		for (int i = 0; i < this.enemyRendTmp.Count; i++)
		{
			Renderer renderer = this.enemyRendTmp[i];
			bool flag = renderer == null;
			bool flag2 = !flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				Material[] sharedMaterials = renderer.sharedMaterials;
				for (int j = 0; j < sharedMaterials.Length; j++)
				{
					bool flag9 = sharedMaterials[j] != null && this.enemyMatIds.Contains(sharedMaterials[j].GetInstanceID());
					bool flag10 = flag9;
					bool flag11 = flag10;
					bool flag12 = flag11;
					bool flag13 = flag12;
					bool flag14 = flag13;
					bool flag15 = flag14;
					bool flag16 = flag15;
					if (flag16)
					{
						this.RevertMaterial(sharedMaterials[j].GetInstanceID());
					}
				}
			}
		}
	}

	private void RevertMaterial(int id)
	{
		for (int i = this.enemyMats.Count - 1; i >= 0; i--)
		{
			bool flag = this.enemyMats[i].id != id;
			bool flag2 = !flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				this.RestoreRec(this.enemyMats[i]);
				this.enemyMats.RemoveAt(i);
			}
		}
		this.enemyMatIds.Remove(id);
	}

	private void RevertEnemyTextures()
	{
		for (int i = 0; i < this.enemyMats.Count; i++)
		{
			this.RestoreRec(this.enemyMats[i]);
		}
		this.enemyMats.Clear();
		this.enemyMatIds.Clear();
		this.enemyMatSeen.Clear();
	}

	private void PruneEnemyMats()
	{
		for (int i = this.enemyMats.Count - 1; i >= 0; i--)
		{
			ResourcePackManager.EnemyMat enemyMat = this.enemyMats[i];
			bool flag = enemyMat.mat == null || (enemyMat.shared != null && enemyMat.rend == null);
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				bool flag9 = enemyMat.mat != null;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					UnityEngine.Object.Destroy(enemyMat.mat);
				}
				this.enemyMatIds.Remove(enemyMat.id);
				this.enemyMats.RemoveAt(i);
			}
		}
		bool flag17 = this.enemyMatSeen.Count > 5000;
		bool flag18 = flag17;
		bool flag19 = flag18;
		bool flag20 = flag19;
		bool flag21 = flag20;
		bool flag22 = flag21;
		bool flag23 = flag22;
		bool flag24 = flag23;
		if (flag24)
		{
			this.enemyMatSeen.Clear();
		}
	}

	private void RestoreRec(ResourcePackManager.EnemyMat rec)
	{
		bool flag = rec.mat == null;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			bool flag9 = rec.shared != null;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				bool flag17 = rec.rend != null;
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				if (flag24)
				{
					Material[] sharedMaterials = rec.rend.sharedMaterials;
					bool flag25 = rec.slot < sharedMaterials.Length && sharedMaterials[rec.slot] == rec.mat;
					bool flag26 = flag25;
					bool flag27 = flag26;
					bool flag28 = flag27;
					bool flag29 = flag28;
					bool flag30 = flag29;
					bool flag31 = flag30;
					bool flag32 = flag31;
					if (flag32)
					{
						sharedMaterials[rec.slot] = rec.shared;
						rec.rend.sharedMaterials = sharedMaterials;
					}
				}
				UnityEngine.Object.Destroy(rec.mat);
			}
			else
			{
				rec.mat.SetTexture(rec.prop, rec.orig);
			}
		}
	}

	private static bool IsInEnemyFolder(string packDir, string file)
	{
		string text = (file.Length > packDir.Length) ? file.Substring(packDir.Length) : file;
		string[] array = text.Split(new char[]
		{
			'/',
			'\\'
		}, StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i < array.Length - 1; i++)
		{
			bool flag = array[i].Equals("Enemy", StringComparison.OrdinalIgnoreCase);
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				return true;
			}
		}
		return false;
	}

	private static float ParseBright(string line)
	{
		string s = line.Substring(line.IndexOf('=') + 1).Trim();
		float value;
		bool flag = float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
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
			result = Mathf.Clamp(value, 0.3f, 5f);
		}
		else
		{
			result = 1f;
		}
		return result;
	}

	private static float NightFactor()
	{
		bool flag = Journee.instance == null;
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
			result = 0f;
		}
		else
		{
			float currentTime = Journee.CurrentTime;
			bool flag9 = currentTime >= 12f;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				result = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(17.5f, 20.5f, currentTime));
			}
			else
			{
				result = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(4f, 7f, currentTime));
			}
		}
		return result;
	}

	private void UpdateLightCache()
	{
		bool flag = Mathf.Approximately(this.dayBright, 1f) && Mathf.Approximately(this.nightBright, 1f);
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
			bool flag9 = unscaledTime < this.dirLightsNext;
			bool flag10 = !flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				this.dirLightsNext = unscaledTime + 3f;
				Light[] array = UnityEngine.Object.FindObjectsOfType<Light>();
				List<Light> list = new List<Light>();
				for (int i = 0; i < array.Length; i++)
				{
					bool flag17 = array[i] != null && array[i].type == LightType.Directional;
					bool flag18 = flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					bool flag21 = flag20;
					bool flag22 = flag21;
					bool flag23 = flag22;
					bool flag24 = flag23;
					if (flag24)
					{
						list.Add(array[i]);
					}
				}
				this.dirLights = list.ToArray();
			}
		}
	}

	private void OnCamPreCull(Camera cam)
	{
		bool flag = this.lightApplied || (Mathf.Approximately(this.dayBright, 1f) && Mathf.Approximately(this.nightBright, 1f));
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			bool flag9 = false;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			float num;
			if (flag16)
			{
				bool flag17 = cam == null || !cam.CompareTag("MainCamera");
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				if (flag24)
				{
					return;
				}
				bool flag25 = this.brightFrame != (float)Time.frameCount;
				bool flag26 = flag25;
				bool flag27 = flag26;
				bool flag28 = flag27;
				bool flag29 = flag28;
				bool flag30 = flag29;
				bool flag31 = flag30;
				bool flag32 = flag31;
				if (flag32)
				{
					this.brightFrame = (float)Time.frameCount;
					this.brightMul = Mathf.Lerp(this.dayBright, this.nightBright, ResourcePackManager.NightFactor());
				}
				num = this.brightMul;
			}
			else
			{
				num = Mathf.Lerp(this.dayBright, this.nightBright, ResourcePackManager.NightFactor());
			}
			this.ambientSaved = RenderSettings.ambientLight;
			Color color = this.ambientSaved;
			RenderSettings.ambientLight = new Color(color.r * num, color.g * num, color.b * num, color.a);
			bool flag33 = this.dirLights != null;
			bool flag34 = flag33;
			bool flag35 = flag34;
			bool flag36 = flag35;
			bool flag37 = flag36;
			bool flag38 = flag37;
			bool flag39 = flag38;
			bool flag40 = flag39;
			if (flag40)
			{
				bool flag41 = this.lightSaved.Length != this.dirLights.Length;
				bool flag42 = flag41;
				bool flag43 = flag42;
				bool flag44 = flag43;
				bool flag45 = flag44;
				bool flag46 = flag45;
				bool flag47 = flag46;
				bool flag48 = flag47;
				if (flag48)
				{
					this.lightSaved = new float[this.dirLights.Length];
				}
				for (int i = 0; i < this.dirLights.Length; i++)
				{
					bool flag49 = this.dirLights[i] != null;
					bool flag50 = flag49;
					bool flag51 = flag50;
					bool flag52 = flag51;
					bool flag53 = flag52;
					bool flag54 = flag53;
					bool flag55 = flag54;
					bool flag56 = flag55;
					if (flag56)
					{
						this.lightSaved[i] = this.dirLights[i].intensity;
						this.dirLights[i].intensity = this.lightSaved[i] * num;
					}
				}
			}
			this.lightApplied = true;
		}
	}

	private void OnCamPostRender(Camera cam)
	{
		this.RestoreLighting();
		ResourcePackManager.HpConfig hpConfig = this.hpCfg;
		bool flag = this.AltShow(hpConfig, hpConfig != null && hpConfig.altPaths);
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			this.DrawPaths(cam);
		}
		bool flag9 = this.AltShow(hpConfig, hpConfig != null && hpConfig.altBombMap);
		bool flag10 = flag9;
		bool flag11 = flag10;
		bool flag12 = flag11;
		bool flag13 = flag12;
		bool flag14 = flag13;
		bool flag15 = flag14;
		bool flag16 = flag15;
		if (flag16)
		{
			this.DrawBombMap(cam);
		}
		BtcOverlays.Render(cam);
	}

	private void RestoreLighting()
	{
		bool flag = !this.lightApplied;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			this.lightApplied = false;
			RenderSettings.ambientLight = this.ambientSaved;
			bool flag9 = this.dirLights != null && this.lightSaved.Length == this.dirLights.Length;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				for (int i = 0; i < this.dirLights.Length; i++)
				{
					bool flag17 = this.dirLights[i] != null;
					bool flag18 = flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					bool flag21 = flag20;
					bool flag22 = flag21;
					bool flag23 = flag22;
					bool flag24 = flag23;
					if (flag24)
					{
						this.dirLights[i].intensity = this.lightSaved[i];
					}
				}
			}
		}
	}

	private float BrightRow(Event e, ref float y, float w, float rowH, int fs, Color old, string label, int id, float value)
	{
		label = BtcLang.Tr(label);
		Rect row = new Rect(0f, y, w, rowH);
		this.DrawRowBackground(e, row, false, old);
		GUI.Label(new Rect((float)fs, y, w * 0.42f, rowH), label, this.menuLabelStyle);
		Rect hit = new Rect(w * 0.47f, y, w * 0.53f - (float)fs * 4.5f, rowH);
		float result = this.SliderCore(e, hit, id, value, 0.3f, 4f, 0.05f, old);
		GUI.Label(new Rect(hit.xMax + (float)fs * 0.3f, y, (float)fs * 4f, rowH), "x" + result.ToString("0.00", CultureInfo.InvariantCulture), this.menuValueStyle);
		y += rowH;
		return result;
	}

	private static int HpPaletteIndex(string section)
	{
		uint num = ResourcePackManager.ComputeStringHash(section);
		bool flag = num <= 1776971179U;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		if (flag7)
		{
			bool flag8 = num != 1381875722U;
			bool flag9 = flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			if (flag14)
			{
				bool flag15 = num != 1536888239U;
				bool flag16 = flag15;
				bool flag17 = flag16;
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				if (flag21)
				{
					bool flag22 = num == 1776971179U;
					bool flag23 = flag22;
					bool flag24 = flag23;
					bool flag25 = flag24;
					bool flag26 = flag25;
					bool flag27 = flag26;
					bool flag28 = flag27;
					if (flag28)
					{
						bool flag29 = section == "enemy";
						bool flag30 = flag29;
						bool flag31 = flag30;
						bool flag32 = flag31;
						bool flag33 = flag32;
						bool flag34 = flag33;
						bool flag35 = flag34;
						if (flag35)
						{
							return 1;
						}
					}
				}
				else
				{
					bool flag36 = section == "ammo";
					bool flag37 = flag36;
					bool flag38 = flag37;
					bool flag39 = flag38;
					bool flag40 = flag39;
					bool flag41 = flag40;
					bool flag42 = flag41;
					if (flag42)
					{
						return 4;
					}
				}
			}
			else
			{
				bool flag43 = section == "staminaenemy";
				bool flag44 = flag43;
				bool flag45 = flag44;
				bool flag46 = flag45;
				bool flag47 = flag46;
				bool flag48 = flag47;
				bool flag49 = flag48;
				if (flag49)
				{
					return 3;
				}
			}
		}
		else
		{
			bool flag50 = num <= 2769661887U;
			bool flag51 = flag50;
			bool flag52 = flag51;
			bool flag53 = flag52;
			bool flag54 = flag53;
			bool flag55 = flag54;
			bool flag56 = flag55;
			if (flag56)
			{
				bool flag57 = num != 2712300123U;
				bool flag58 = flag57;
				bool flag59 = flag58;
				bool flag60 = flag59;
				bool flag61 = flag60;
				bool flag62 = flag61;
				bool flag63 = flag62;
				if (flag63)
				{
					bool flag64 = num == 2769661887U;
					bool flag65 = flag64;
					bool flag66 = flag65;
					bool flag67 = flag66;
					bool flag68 = flag67;
					bool flag69 = flag68;
					bool flag70 = flag69;
					if (flag70)
					{
						bool flag71 = section == "own";
						bool flag72 = flag71;
						bool flag73 = flag72;
						bool flag74 = flag73;
						bool flag75 = flag74;
						bool flag76 = flag75;
						bool flag77 = flag76;
						if (flag77)
						{
							return 0;
						}
					}
				}
				else
				{
					bool flag78 = section == "buildingenemy";
					bool flag79 = flag78;
					bool flag80 = flag79;
					bool flag81 = flag80;
					bool flag82 = flag81;
					bool flag83 = flag82;
					bool flag84 = flag83;
					if (flag84)
					{
						return 6;
					}
				}
			}
			else
			{
				bool flag85 = num != 3542249438U;
				bool flag86 = flag85;
				bool flag87 = flag86;
				bool flag88 = flag87;
				bool flag89 = flag88;
				bool flag90 = flag89;
				bool flag91 = flag90;
				if (flag91)
				{
					bool flag92 = num == 3909760495U;
					bool flag93 = flag92;
					bool flag94 = flag93;
					bool flag95 = flag94;
					bool flag96 = flag95;
					bool flag97 = flag96;
					bool flag98 = flag97;
					if (flag98)
					{
						bool flag99 = section == "buildingown";
						bool flag100 = flag99;
						bool flag101 = flag100;
						bool flag102 = flag101;
						bool flag103 = flag102;
						bool flag104 = flag103;
						bool flag105 = flag104;
						if (flag105)
						{
							return 5;
						}
					}
				}
				else
				{
					bool flag106 = section == "staminaown";
					bool flag107 = flag106;
					bool flag108 = flag107;
					bool flag109 = flag108;
					bool flag110 = flag109;
					bool flag111 = flag110;
					bool flag112 = flag111;
					if (flag112)
					{
						return 2;
					}
				}
			}
		}
		return -1;
	}
}
}
