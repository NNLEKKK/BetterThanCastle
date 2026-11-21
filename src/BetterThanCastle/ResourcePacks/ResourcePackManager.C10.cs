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

	private bool PredRetryOrFail(ResourcePackManager.PredReq r)
	{
		bool flag = !r.retried && r.exact;
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
			r.retried = true;
			r.exact = false;
			this.DisposePred(r);
			r.stage = 0;
			r.calls = 0;
			result = false;
		}
		else
		{
			this.PredNote(string.Concat(new object[]
			{
				"no path found (stage ",
				r.stage,
				r.retried ? ", inexact retry too" : "",
				"; 0=origin/destination, 1=search, 3=wrap-up)"
			}));
			result = true;
		}
		return result;
	}

	private static void SimplifyPoints(List<Vector3> src, List<Vector3> dst)
	{
		dst.Clear();
		dst.Add(src[0]);
		for (int i = 1; i < src.Count - 1; i++)
		{
			Vector3 vector = src[i] - dst[dst.Count - 1];
			Vector3 vector2 = src[i + 1] - src[i];
			bool flag = vector.sqrMagnitude < 1E-06f;
			bool flag2 = !flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				bool flag9 = vector2.sqrMagnitude > 1E-06f && Vector3.Dot(vector.normalized, vector2.normalized) > 0.999f;
				bool flag10 = !flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					dst.Add(src[i]);
				}
			}
		}
		dst.Add(src[src.Count - 1]);
	}

	private static float DistToSegSqr(Vector3 p, Vector3 a, Vector3 b)
	{
		Vector3 vector = b - a;
		float sqrMagnitude = vector.sqrMagnitude;
		float d = (sqrMagnitude < 1E-06f) ? 0f : Mathf.Clamp01(Vector3.Dot(p - a, vector) / sqrMagnitude);
		return (a + vector * d - p).sqrMagnitude;
	}

	private bool TryAddPredicted(Labor labor)
	{
		ResourcePackManager.PredRoute predRoute;
		bool flag = !this.predRoutes.TryGetValue(labor, out predRoute);
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
			float unscaledTime = Time.unscaledTime;
			Vector3 position = labor.transform.position;
			bool flag9 = (position - predRoute.lastPos).sqrMagnitude > 0.01f;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				predRoute.lastPos = position;
				predRoute.lastMove = unscaledTime;
				predRoute.moved = true;
			}
			Vector3 vector = predRoute.pts[predRoute.pts.Count - 1] - position;
			vector.y = 0f;
			bool flag17 = vector.sqrMagnitude < 2.25f;
			bool flag18 = predRoute.moved ? (unscaledTime - predRoute.lastMove > 1.5f) : (unscaledTime - predRoute.created > 4f);
			bool flag19 = flag17 || flag18;
			bool flag20 = flag19;
			bool flag21 = flag20;
			bool flag22 = flag21;
			bool flag23 = flag22;
			bool flag24 = flag23;
			bool flag25 = flag24;
			bool flag26 = flag25;
			if (flag26)
			{
				this.predRoutes.Remove(labor);
				result = false;
			}
			else
			{
				int num = predRoute.progress;
				float num2 = float.MaxValue;
				int num3 = Mathf.Min(predRoute.pts.Count - 1, predRoute.progress + 6);
				for (int i = predRoute.progress; i < num3; i++)
				{
					float num4 = ResourcePackManager.DistToSegSqr(position, predRoute.pts[i], predRoute.pts[i + 1]);
					bool flag27 = num4 < num2;
					bool flag28 = flag27;
					bool flag29 = flag28;
					bool flag30 = flag29;
					bool flag31 = flag30;
					bool flag32 = flag31;
					bool flag33 = flag32;
					bool flag34 = flag33;
					if (flag34)
					{
						num2 = num4;
						num = i;
					}
				}
				predRoute.progress = num;
				ResourcePackManager.PathUnit pathUnit = this.NewPathUnit();
				pathUnit.labor = labor;
				pathUnit.trail = false;
				pathUnit.pts.Clear();
				pathUnit.pts.Add(position);
				for (int j = num + 1; j < predRoute.pts.Count; j++)
				{
					pathUnit.pts.Add(predRoute.pts[j]);
				}
				bool flag35 = pathUnit.pts.Count < 2;
				bool flag36 = flag35;
				bool flag37 = flag36;
				bool flag38 = flag37;
				bool flag39 = flag38;
				bool flag40 = flag39;
				bool flag41 = flag40;
				bool flag42 = flag41;
				if (flag42)
				{
					this.predRoutes.Remove(labor);
					result = false;
				}
				else
				{
					this.pathUnits.Add(pathUnit);
					result = true;
				}
			}
		}
		return result;
	}

	private void PredNote(string text)
	{
		bool flag = text != this.predDiag;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			this.predDiag = text;
			ResourcePackManager.PackLog("[ResourcePacks] Path predict: " + text);
		}
	}

	private ExplosionGameplay GetBombPrefab(string template)
	{
		ExplosionGameplay explosionGameplay;
		bool flag = this.bombPrefabs.TryGetValue(template, out explosionGameplay);
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		ExplosionGameplay result;
		if (flag8)
		{
			result = explosionGameplay;
		}
		else
		{
			explosionGameplay = null;
			try
			{
				Factory.AssetKey key = (template == "Petite") ? EffetsParticules.ExplosionBarilVide : EffetsParticules.Explosion;
				GameObject gameObject = Factory.Peek(key);
				bool flag9 = gameObject != null;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					explosionGameplay = gameObject.GetComponentInChildren<ExplosionGameplay>(true);
				}
			}
			catch (Exception ex)
			{
				bool flag17 = !this.bombWarned;
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				if (flag24)
				{
					this.bombWarned = true;
					ResourcePackManager.PackWarn("[ResourcePacks] Explosion map: cannot read explosion prefab: " + ex.Message);
				}
			}
			bool flag25 = explosionGameplay == null && !this.bombWarned;
			bool flag26 = flag25;
			bool flag27 = flag26;
			bool flag28 = flag27;
			bool flag29 = flag28;
			bool flag30 = flag29;
			bool flag31 = flag30;
			bool flag32 = flag31;
			if (flag32)
			{
				this.bombWarned = true;
				ResourcePackManager.PackWarn("[ResourcePacks] Explosion map: ExplosionGameplay not found on prefab for '" + template + "'");
			}
			this.bombPrefabs[template] = explosionGameplay;
			result = explosionGameplay;
		}
		return result;
	}

	private void UpdateBombMap()
	{
		ResourcePackManager.HpConfig hpConfig = this.hpCfg;
		bool flag = hpConfig == null || !hpConfig.bombMap || this.HiddenByAlt(hpConfig, hpConfig.altBombMap);
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			bool flag9 = this.bombBarrels.Count > 0 || this.bombShown.Count > 0 || this.bombBuilding != null;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				this.FreeAllBombMeshes();
			}
		}
		else
		{
			float unscaledTime = Time.unscaledTime;
			bool flag17 = unscaledTime >= this.bombNextScan;
			bool flag18 = flag17;
			bool flag19 = flag18;
			bool flag20 = flag19;
			bool flag21 = flag20;
			bool flag22 = flag21;
			bool flag23 = flag22;
			bool flag24 = flag23;
			if (flag24)
			{
				this.bombNextScan = unscaledTime + (hpConfig.perfMode ? 1f : 0.5f);
				try
				{
					this.ScanBarrels(hpConfig);
				}
				catch (Exception ex)
				{
					bool flag25 = !this.bombWarned;
					bool flag26 = flag25;
					bool flag27 = flag26;
					bool flag28 = flag27;
					bool flag29 = flag28;
					bool flag30 = flag29;
					bool flag31 = flag30;
					bool flag32 = flag31;
					if (flag32)
					{
						this.bombWarned = true;
						ResourcePackManager.PackWarn("[ResourcePacks] Explosion map: scan failed: " + ex.Message);
					}
				}
			}
			try
			{
				this.RefreshBarrelVoxels();
				int num = ResourcePackManager.BombShapeSig(hpConfig) * 31 + this.bombBarrels.Count;
				for (int i = 0; i < this.bombBarrels.Count; i++)
				{
					bool flag33 = this.bombBarrels[i].ex != null;
					bool flag34 = flag33;
					bool flag35 = flag34;
					bool flag36 = flag35;
					bool flag37 = flag36;
					bool flag38 = flag37;
					bool flag39 = flag38;
					bool flag40 = flag39;
					if (flag40)
					{
						num += ResourcePackManager.BombBarrelHash(this.bombBarrels[i]);
					}
				}
				bool flag41 = num != this.bombGlobalSig;
				bool flag42 = flag41;
				bool flag43 = flag42;
				bool flag44 = flag43;
				bool flag45 = flag44;
				bool flag46 = flag45;
				bool flag47 = flag46;
				bool flag48 = flag47;
				if (flag48)
				{
					this.bombGlobalSig = num;
					this.RegroupBombs(hpConfig);
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
					float realtimeSinceStartup = Time.realtimeSinceStartup;
					bool flag57 = true;
					for (int j = 0; j < this.bombBuilding.Count; j++)
					{
						ResourcePackManager.BombGroup bombGroup = this.bombBuilding[j];
						bool flag58 = bombGroup.built && bombGroup.meshes.Count > 0;
						bool flag59 = !flag58;
						bool flag60 = flag59;
						bool flag61 = flag60;
						bool flag62 = flag61;
						bool flag63 = flag62;
						bool flag64 = flag63;
						bool flag65 = flag64;
						if (flag65)
						{
							bool flag66 = j > 0 && Time.realtimeSinceStartup - realtimeSinceStartup > 0.006f;
							bool flag67 = flag66;
							bool flag68 = flag67;
							bool flag69 = flag68;
							bool flag70 = flag69;
							bool flag71 = flag70;
							bool flag72 = flag71;
							bool flag73 = flag72;
							if (flag73)
							{
								flag57 = false;
								break;
							}
							bool flag74 = !bombGroup.built;
							bool flag75 = flag74;
							bool flag76 = flag75;
							bool flag77 = flag76;
							bool flag78 = flag77;
							bool flag79 = flag78;
							bool flag80 = flag79;
							bool flag81 = flag80;
							if (flag81)
							{
								this.MergeBombGroup(bombGroup, hpConfig);
							}
							this.RebuildBombMeshes(bombGroup, hpConfig);
							bombGroup.meshSig = ResourcePackManager.BombMeshSig(hpConfig);
						}
					}
					bool flag82 = flag57;
					bool flag83 = flag82;
					bool flag84 = flag83;
					bool flag85 = flag84;
					bool flag86 = flag85;
					bool flag87 = flag86;
					bool flag88 = flag87;
					bool flag89 = flag88;
					if (flag89)
					{
						this.FreeBombGroupList(this.bombShown, this.bombBuilding);
						this.bombShown = this.bombBuilding;
						this.bombBuilding = null;
					}
				}
				int num2 = ResourcePackManager.BombMeshSig(hpConfig);
				float realtimeSinceStartup2 = Time.realtimeSinceStartup;
				for (int k = 0; k < this.bombShown.Count; k++)
				{
					ResourcePackManager.BombGroup bombGroup2 = this.bombShown[k];
					bool flag90 = bombGroup2.built && bombGroup2.meshSig != num2;
					bool flag91 = flag90;
					bool flag92 = flag91;
					bool flag93 = flag92;
					bool flag94 = flag93;
					bool flag95 = flag94;
					bool flag96 = flag95;
					bool flag97 = flag96;
					if (flag97)
					{
						bool flag98 = k > 0 && Time.realtimeSinceStartup - realtimeSinceStartup2 > 0.006f;
						bool flag99 = flag98;
						bool flag100 = flag99;
						bool flag101 = flag100;
						bool flag102 = flag101;
						bool flag103 = flag102;
						bool flag104 = flag103;
						bool flag105 = flag104;
						if (flag105)
						{
							break;
						}
						this.RebuildBombMeshes(bombGroup2, hpConfig);
						bombGroup2.meshSig = num2;
					}
				}
			}
			catch (Exception ex2)
			{
				bool flag106 = !this.bombWarned;
				bool flag107 = flag106;
				bool flag108 = flag107;
				bool flag109 = flag108;
				bool flag110 = flag109;
				bool flag111 = flag110;
				bool flag112 = flag111;
				bool flag113 = flag112;
				if (flag113)
				{
					this.bombWarned = true;
					ResourcePackManager.PackWarn("[ResourcePacks] Explosion map: build failed: " + ex2.Message);
				}
			}
		}
	}

	private void DrawBombMap(Camera cam)
	{
		ResourcePackManager.HpConfig hpConfig = this.hpCfg;
		bool flag = hpConfig == null || !hpConfig.bombMap || this.bombShown.Count == 0 || cam == null;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			bool flag9 = cam != Camera.main || !this.EnsurePathMat();
			bool flag10 = !flag9;
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
					bool flag17 = this.pathMat.HasProperty("_ZTest");
					bool flag18 = flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					bool flag21 = flag20;
					bool flag22 = flag21;
					bool flag23 = flag22;
					bool flag24 = flag23;
					if (flag24)
					{
						this.pathMat.SetInt("_ZTest", 4);
					}
					Vector3 position = cam.transform.position;
					float num = hpConfig.perfMode ? 60f : 90f;
					bool flag25 = false;
					for (int i = 0; i < this.bombShown.Count; i++)
					{
						ResourcePackManager.BombGroup bombGroup = this.bombShown[i];
						float num2 = num + bombGroup.reach;
						bool flag26 = bombGroup.meshes.Count == 0 || (bombGroup.center - position).sqrMagnitude > num2 * num2;
						bool flag27 = !flag26;
						bool flag28 = flag27;
						bool flag29 = flag28;
						bool flag30 = flag29;
						bool flag31 = flag30;
						bool flag32 = flag31;
						bool flag33 = flag32;
						if (flag33)
						{
							bool flag34 = !flag25;
							bool flag35 = flag34;
							bool flag36 = flag35;
							bool flag37 = flag36;
							bool flag38 = flag37;
							bool flag39 = flag38;
							bool flag40 = flag39;
							bool flag41 = flag40;
							if (flag41)
							{
								this.pathMat.SetPass(0);
								flag25 = true;
							}
							for (int j = 0; j < bombGroup.meshes.Count; j++)
							{
								bool flag42 = bombGroup.meshes[j] != null;
								bool flag43 = flag42;
								bool flag44 = flag43;
								bool flag45 = flag44;
								bool flag46 = flag45;
								bool flag47 = flag46;
								bool flag48 = flag47;
								bool flag49 = flag48;
								if (flag49)
								{
									Graphics.DrawMeshNow(bombGroup.meshes[j], Matrix4x4.identity);
								}
							}
						}
					}
				}
				catch (Exception ex)
				{
					bool flag50 = !this.bombWarned;
					bool flag51 = flag50;
					bool flag52 = flag51;
					bool flag53 = flag52;
					bool flag54 = flag53;
					bool flag55 = flag54;
					bool flag56 = flag55;
					bool flag57 = flag56;
					if (flag57)
					{
						this.bombWarned = true;
						ResourcePackManager.PackWarn("[ResourcePacks] Explosion map: draw failed: " + ex.Message);
					}
				}
			}
		}
	}

	private void DrawBombNumbers()
	{
		ResourcePackManager.HpConfig hpConfig = this.hpCfg;
		bool flag = hpConfig == null || !hpConfig.bombMap || !hpConfig.bombMapNumbers || hpConfig.bombNumAlpha <= 0.004f || this.bombShown.Count == 0 || Event.current.type != EventType.Repaint;
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
				bool flag17 = this.bombStyle == null;
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				if (flag24)
				{
					this.bombStyle = new GUIStyle(GUI.skin.label);
					this.bombStyle.alignment = TextAnchor.MiddleCenter;
					this.bombStyle.fontStyle = FontStyle.Bold;
					this.bombStyle.wordWrap = false;
					this.bombStyle.clipping = TextClipping.Overflow;
				}
				GUI.depth = 7;
				Color color = GUI.color;
				Vector3 position = main.transform.position;
				float num = main.orthographic ? 0f : Mathf.Tan(main.fieldOfView * 0.5f * 0.017453292f);
				float num2 = hpConfig.bombNumDist;
				bool perfMode = hpConfig.perfMode;
				bool flag25 = perfMode;
				bool flag26 = flag25;
				bool flag27 = flag26;
				bool flag28 = flag27;
				bool flag29 = flag28;
				bool flag30 = flag29;
				bool flag31 = flag30;
				if (flag31)
				{
					num2 = Mathf.Min(num2, 60f);
				}
				float num3 = num2 * num2;
				int num4 = hpConfig.perfMode ? 300 : 2000;
				float num5 = (float)Screen.width;
				float num6 = (float)Screen.height;
				float bombMapYOffset = hpConfig.bombMapYOffset;
				float num7 = hpConfig.bombMapYOffset - 0.5f + 0.03f;
				bool bombMap3D = hpConfig.bombMap3D;
				Color color2 = new Color(0f, 0f, 0f, 0.85f * hpConfig.bombNumAlpha);
				Color color3 = new Color(1f, 1f, 1f, hpConfig.bombNumAlpha);
				int num8 = 0;
				int num9 = 0;
				while (num9 < this.bombShown.Count && num8 < num4)
				{
					ResourcePackManager.BombGroup bombGroup = this.bombShown[num9];
					float num10 = num2 + bombGroup.reach;
					bool flag32 = (bombGroup.center - position).sqrMagnitude > num10 * num10;
					bool flag33 = !flag32;
					bool flag34 = flag33;
					bool flag35 = flag34;
					bool flag36 = flag35;
					bool flag37 = flag36;
					bool flag38 = flag37;
					bool flag39 = flag38;
					if (flag39)
					{
						List<ResourcePackManager.BombCell> list = hpConfig.bombMapAllNumbers ? bombGroup.cells : bombGroup.numCells;
						int num11 = 0;
						while (num11 < list.Count && num8 < num4)
						{
							ResourcePackManager.BombCell bombCell = list[num11];
							bool flag40 = (hpConfig.bombBandMask >> bombCell.band & 1) == 0;
							bool flag41 = !flag40;
							bool flag42 = flag41;
							bool flag43 = flag42;
							bool flag44 = flag43;
							bool flag45 = flag44;
							bool flag46 = flag45;
							bool flag47 = flag46;
							if (flag47)
							{
								Vector3 center = bombCell.center;
								bool flag48 = (center - position).sqrMagnitude > num3;
								bool flag49 = !flag48;
								bool flag50 = flag49;
								bool flag51 = flag50;
								bool flag52 = flag51;
								bool flag53 = flag52;
								bool flag54 = flag53;
								bool flag55 = flag54;
								if (flag55)
								{
									Vector3 vector = main.WorldToScreenPoint(new Vector3(center.x, center.y + (bombMap3D ? bombMapYOffset : num7), center.z));
									bool flag56 = vector.z <= 0f || vector.x < -50f || vector.x > num5 + 50f || vector.y < -50f || vector.y > num6 + 50f;
									bool flag57 = !flag56;
									bool flag58 = flag57;
									bool flag59 = flag58;
									bool flag60 = flag59;
									bool flag61 = flag60;
									bool flag62 = flag61;
									bool flag63 = flag62;
									if (flag63)
									{
										float num12 = main.orthographic ? (num6 / (2f * Mathf.Max(0.01f, main.orthographicSize))) : (num6 / (2f * Mathf.Max(0.1f, vector.z) * num));
										int num13 = Mathf.Min(30, Mathf.RoundToInt(num12 * 0.34f));
										bool flag64 = num13 < 9;
										bool flag65 = !flag64;
										bool flag66 = flag65;
										bool flag67 = flag66;
										bool flag68 = flag67;
										bool flag69 = flag68;
										bool flag70 = flag69;
										bool flag71 = flag70;
										if (flag71)
										{
											num13 &= -2;
											this.bombStyle.fontSize = num13;
											Rect position2 = new Rect(vector.x - (float)num13 * 2f, num6 - vector.y - (float)num13, (float)num13 * 4f, (float)num13 * 2f);
											bool flag72 = num13 >= 12 && !hpConfig.perfMode;
											bool flag73 = flag72;
											bool flag74 = flag73;
											bool flag75 = flag74;
											bool flag76 = flag75;
											bool flag77 = flag76;
											bool flag78 = flag77;
											bool flag79 = flag78;
											if (flag79)
											{
												GUI.color = color2;
												GUI.Label(new Rect(position2.x + 1f, position2.y + 1f, position2.width, position2.height), bombCell.text, this.bombStyle);
											}
											GUI.color = color3;
											GUI.Label(position2, bombCell.text, this.bombStyle);
											num8++;
										}
									}
								}
							}
							num11++;
						}
					}
					num9++;
				}
				GUI.color = color;
			}
		}
	}

	private static int BarrelDamageAt(int sqr)
	{
		int length = ResourcePackManager.BarrelDamageTable.GetLength(0);
		bool flag = sqr >= ResourcePackManager.BarrelDamageTable[length - 1, 0];
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
			result = 0;
		}
		else
		{
			for (int i = 0; i < length; i++)
			{
				bool flag9 = ResourcePackManager.BarrelDamageTable[i, 0] == sqr;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					return ResourcePackManager.BarrelDamageTable[i, 1];
				}
			}
			float num = Mathf.Sqrt((float)sqr);
			for (int j = 0; j < length - 1; j++)
			{
				float num2 = Mathf.Sqrt((float)ResourcePackManager.BarrelDamageTable[j, 0]);
				float num3 = Mathf.Sqrt((float)ResourcePackManager.BarrelDamageTable[j + 1, 0]);
				bool flag17 = num >= num2 && num <= num3;
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				if (flag24)
				{
					float t = (num - num2) / Mathf.Max(0.0001f, num3 - num2);
					int num4 = Mathf.RoundToInt(Mathf.Lerp((float)ResourcePackManager.BarrelDamageTable[j, 1], (float)ResourcePackManager.BarrelDamageTable[j + 1, 1], t));
					return (num4 < 10) ? 0 : num4;
				}
			}
			result = 0;
		}
		return result;
	}

	private static bool ApplyCombatKey(string k, string val, ResourcePackManager.HpConfig cfg, int lineIdx, List<string> problems)
	{
		uint num = ResourcePackManager.ComputeStringHash(k);
		bool flag = num <= 1962948445U;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		if (flag7)
		{
			bool flag8 = num <= 996757603U;
			bool flag9 = flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			if (flag14)
			{
				bool flag15 = num <= 306939824U;
				bool flag16 = flag15;
				bool flag17 = flag16;
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				if (flag21)
				{
					bool flag22 = num <= 108686930U;
					bool flag23 = flag22;
					bool flag24 = flag23;
					bool flag25 = flag24;
					bool flag26 = flag25;
					bool flag27 = flag26;
					bool flag28 = flag27;
					if (flag28)
					{
						bool flag29 = num != 22793305U;
						bool flag30 = flag29;
						bool flag31 = flag30;
						bool flag32 = flag31;
						bool flag33 = flag32;
						bool flag34 = flag33;
						bool flag35 = flag34;
						if (flag35)
						{
							bool flag36 = num != 105010770U;
							bool flag37 = flag36;
							bool flag38 = flag37;
							bool flag39 = flag38;
							bool flag40 = flag39;
							bool flag41 = flag40;
							bool flag42 = flag41;
							if (flag42)
							{
								bool flag43 = num != 108686930U;
								bool flag44 = flag43;
								bool flag45 = flag44;
								bool flag46 = flag45;
								bool flag47 = flag46;
								bool flag48 = flag47;
								bool flag49 = flag48;
								if (flag49)
								{
									goto IL_4C4A;
								}
								bool flag50 = !(k == "altammo");
								bool flag51 = flag50;
								bool flag52 = flag51;
								bool flag53 = flag52;
								bool flag54 = flag53;
								bool flag55 = flag54;
								bool flag56 = flag55;
								if (flag56)
								{
									goto IL_4C4A;
								}
							}
							else
							{
								bool flag57 = !(k == "altbombmap");
								bool flag58 = flag57;
								bool flag59 = flag58;
								bool flag60 = flag59;
								bool flag61 = flag60;
								bool flag62 = flag61;
								bool flag63 = flag62;
								if (flag63)
								{
									goto IL_4C4A;
								}
							}
						}
						else
						{
							bool flag64 = !(k == "hitenemycolor");
							bool flag65 = flag64;
							bool flag66 = flag65;
							bool flag67 = flag66;
							bool flag68 = flag67;
							bool flag69 = flag68;
							bool flag70 = flag69;
							if (flag70)
							{
								goto IL_4C4A;
							}
							goto IL_4881;
						}
					}
					else
					{
						bool flag71 = num <= 154776637U;
						bool flag72 = flag71;
						bool flag73 = flag72;
						bool flag74 = flag73;
						bool flag75 = flag74;
						bool flag76 = flag75;
						bool flag77 = flag76;
						if (flag77)
						{
							bool flag78 = num != 145307514U;
							bool flag79 = flag78;
							bool flag80 = flag79;
							bool flag81 = flag80;
							bool flag82 = flag81;
							bool flag83 = flag82;
							bool flag84 = flag83;
							if (flag84)
							{
								bool flag85 = num != 154776637U;
								bool flag86 = flag85;
								bool flag87 = flag86;
								bool flag88 = flag87;
								bool flag89 = flag88;
								bool flag90 = flag89;
								bool flag91 = flag90;
								if (flag91)
								{
									goto IL_4C4A;
								}
								bool flag92 = !(k == "centersize");
								bool flag93 = flag92;
								bool flag94 = flag93;
								bool flag95 = flag94;
								bool flag96 = flag95;
								bool flag97 = flag96;
								bool flag98 = flag97;
								if (flag98)
								{
									goto IL_4C4A;
								}
								cfg.centerSize = ResourcePackManager.ParseHpFloat(val, cfg.centerSize, 0.3f, 4f);
								return true;
							}
							else
							{
								bool flag99 = !(k == "bombmaprange");
								bool flag100 = flag99;
								bool flag101 = flag100;
								bool flag102 = flag101;
								bool flag103 = flag102;
								bool flag104 = flag103;
								bool flag105 = flag104;
								if (flag105)
								{
									goto IL_4C4A;
								}
								cfg.bombMapRange = Mathf.Clamp(Mathf.RoundToInt(ResourcePackManager.ParseHpFloat(val, (float)cfg.bombMapRange, 1f, 8f)), 1, 8);
								return true;
							}
						}
						else
						{
							bool flag106 = num != 259882152U;
							bool flag107 = flag106;
							bool flag108 = flag107;
							bool flag109 = flag108;
							bool flag110 = flag109;
							bool flag111 = flag110;
							bool flag112 = flag111;
							if (flag112)
							{
								bool flag113 = num != 306939824U;
								bool flag114 = flag113;
								bool flag115 = flag114;
								bool flag116 = flag115;
								bool flag117 = flag116;
								bool flag118 = flag117;
								bool flag119 = flag118;
								if (flag119)
								{
									goto IL_4C4A;
								}
								bool flag120 = !(k == "centerownobjects");
								bool flag121 = flag120;
								bool flag122 = flag121;
								bool flag123 = flag122;
								bool flag124 = flag123;
								bool flag125 = flag124;
								bool flag126 = flag125;
								if (flag126)
								{
									goto IL_4C4A;
								}
							}
							else
							{
								bool flag127 = !(k == "crystallift");
								bool flag128 = flag127;
								bool flag129 = flag128;
								bool flag130 = flag129;
								bool flag131 = flag130;
								bool flag132 = flag131;
								bool flag133 = flag132;
								if (flag133)
								{
									goto IL_4C4A;
								}
								cfg.crystalLift = ResourcePackManager.ParseHpFloat(val, cfg.crystalLift, 0f, 8f);
								return true;
							}
						}
					}
				}
				else
				{
					bool flag134 = num <= 540935103U;
					bool flag135 = flag134;
					bool flag136 = flag135;
					bool flag137 = flag136;
					bool flag138 = flag137;
					bool flag139 = flag138;
					bool flag140 = flag139;
					if (flag140)
					{
						bool flag141 = num <= 457042545U;
						bool flag142 = flag141;
						bool flag143 = flag142;
						bool flag144 = flag143;
						bool flag145 = flag144;
						bool flag146 = flag145;
						bool flag147 = flag146;
						if (flag147)
						{
							bool flag148 = num != 351786008U;
							bool flag149 = flag148;
							bool flag150 = flag149;
							bool flag151 = flag150;
							bool flag152 = flag151;
							bool flag153 = flag152;
							bool flag154 = flag153;
							if (flag154)
							{
								bool flag155 = num != 457042545U;
								bool flag156 = flag155;
								bool flag157 = flag156;
								bool flag158 = flag157;
								bool flag159 = flag158;
								bool flag160 = flag159;
								bool flag161 = flag160;
								if (flag161)
								{
									goto IL_4C4A;
								}
								bool flag162 = !(k == "centeropacity");
								bool flag163 = flag162;
								bool flag164 = flag163;
								bool flag165 = flag164;
								bool flag166 = flag165;
								bool flag167 = flag166;
								bool flag168 = flag167;
								if (flag168)
								{
									goto IL_4C4A;
								}
								cfg.centerAlpha = ResourcePackManager.ParseHpFloat(val, cfg.centerAlpha, 0.1f, 1f);
								return true;
							}
							else
							{
								bool flag169 = !(k == "bombmapcolormask");
								bool flag170 = flag169;
								bool flag171 = flag170;
								bool flag172 = flag171;
								bool flag173 = flag172;
								bool flag174 = flag173;
								bool flag175 = flag174;
								if (flag175)
								{
									goto IL_4C4A;
								}
								cfg.bombBandMask = Mathf.Clamp(Mathf.RoundToInt(ResourcePackManager.ParseHpFloat(val, (float)cfg.bombBandMask, 0f, 255f)), 0, 255);
								return true;
							}
						}
						else
						{
							bool flag176 = num != 482030934U;
							bool flag177 = flag176;
							bool flag178 = flag177;
							bool flag179 = flag178;
							bool flag180 = flag179;
							bool flag181 = flag180;
							bool flag182 = flag181;
							if (flag182)
							{
								bool flag183 = num != 540935103U;
								bool flag184 = flag183;
								bool flag185 = flag184;
								bool flag186 = flag185;
								bool flag187 = flag186;
								bool flag188 = flag187;
								bool flag189 = flag188;
								if (flag189)
								{
									goto IL_4C4A;
								}
								bool flag190 = !(k == "dpsincludestamina");
								bool flag191 = flag190;
								bool flag192 = flag191;
								bool flag193 = flag192;
								bool flag194 = flag193;
								bool flag195 = flag194;
								bool flag196 = flag195;
								if (flag196)
								{
									goto IL_4C4A;
								}
							}
							else
							{
								bool flag197 = !(k == "crystaltimer");
								bool flag198 = flag197;
								bool flag199 = flag198;
								bool flag200 = flag199;
								bool flag201 = flag200;
								bool flag202 = flag201;
								bool flag203 = flag202;
								if (flag203)
								{
									goto IL_4C4A;
								}
							}
						}
					}
					else
					{
						bool flag204 = num <= 959824205U;
						bool flag205 = flag204;
						bool flag206 = flag205;
						bool flag207 = flag206;
						bool flag208 = flag207;
						bool flag209 = flag208;
						bool flag210 = flag209;
						if (flag210)
						{
							bool flag211 = num != 765571211U;
							bool flag212 = flag211;
							bool flag213 = flag212;
							bool flag214 = flag213;
							bool flag215 = flag214;
							bool flag216 = flag215;
							bool flag217 = flag216;
							if (flag217)
							{
								bool flag218 = num != 959824205U;
								bool flag219 = flag218;
								bool flag220 = flag219;
								bool flag221 = flag220;
								bool flag222 = flag221;
								bool flag223 = flag222;
								bool flag224 = flag223;
								if (flag224)
								{
									goto IL_4C4A;
								}
								bool flag225 = !(k == "centeron");
								bool flag226 = flag225;
								bool flag227 = flag226;
								bool flag228 = flag227;
								bool flag229 = flag228;
								bool flag230 = flag229;
								bool flag231 = flag230;
								if (flag231)
								{
									goto IL_4C4A;
								}
							}
							else
							{
								bool flag232 = !(k == "centerownunits");
								bool flag233 = flag232;
								bool flag234 = flag233;
								bool flag235 = flag234;
								bool flag236 = flag235;
								bool flag237 = flag236;
								bool flag238 = flag237;
								if (flag238)
								{
									goto IL_4C4A;
								}
							}
						}
						else
						{
							bool flag239 = num != 980146198U;
							bool flag240 = flag239;
							bool flag241 = flag240;
							bool flag242 = flag241;
							bool flag243 = flag242;
							bool flag244 = flag243;
							bool flag245 = flag244;
							if (flag245)
							{
								bool flag246 = num != 996757603U;
								bool flag247 = flag246;
								bool flag248 = flag247;
								bool flag249 = flag248;
								bool flag250 = flag249;
								bool flag251 = flag250;
								bool flag252 = flag251;
								if (flag252)
								{
									goto IL_4C4A;
								}
								bool flag253 = !(k == "dpsshow");
								bool flag254 = flag253;
								bool flag255 = flag254;
								bool flag256 = flag255;
								bool flag257 = flag256;
								bool flag258 = flag257;
								bool flag259 = flag258;
								if (flag259)
								{
									goto IL_4C4A;
								}
								cfg.dpsShow = ResourcePackManager.ParseHpFloat(val, cfg.dpsShow, 1f, 10f);
								return true;
							}
							else
							{
								bool flag260 = !(k == "altonly");
								bool flag261 = flag260;
								bool flag262 = flag261;
								bool flag263 = flag262;
								bool flag264 = flag263;
								bool flag265 = flag264;
								bool flag266 = flag265;
								if (flag266)
								{
									goto IL_4C4A;
								}
							}
						}
					}
				}
			}
			else
			{
				bool flag267 = num <= 1398915826U;
				bool flag268 = flag267;
				bool flag269 = flag268;
				bool flag270 = flag269;
				bool flag271 = flag270;
				bool flag272 = flag271;
				bool flag273 = flag272;
				if (flag273)
				{
					bool flag274 = num <= 1224116499U;
					bool flag275 = flag274;
					bool flag276 = flag275;
					bool flag277 = flag276;
					bool flag278 = flag277;
					bool flag279 = flag278;
					bool flag280 = flag279;
					if (flag280)
					{
						bool flag281 = num != 1076287810U;
						bool flag282 = flag281;
						bool flag283 = flag282;
						bool flag284 = flag283;
						bool flag285 = flag284;
						bool flag286 = flag285;
						bool flag287 = flag286;
						if (flag287)
						{
							bool flag288 = num != 1199176212U;
							bool flag289 = flag288;
							bool flag290 = flag289;
							bool flag291 = flag290;
							bool flag292 = flag291;
							bool flag293 = flag292;
							bool flag294 = flag293;
							if (flag294)
							{
								bool flag295 = num != 1224116499U;
								bool flag296 = flag295;
								bool flag297 = flag296;
								bool flag298 = flag297;
								bool flag299 = flag298;
								bool flag300 = flag299;
								bool flag301 = flag300;
								if (flag301)
								{
									goto IL_4C4A;
								}
								bool flag302 = !(k == "crystalupcolor");
								bool flag303 = flag302;
								bool flag304 = flag303;
								bool flag305 = flag304;
								bool flag306 = flag305;
								bool flag307 = flag306;
								bool flag308 = flag307;
								if (flag308)
								{
									goto IL_4C4A;
								}
								goto IL_4881;
							}
							else
							{
								bool flag309 = !(k == "centerenemyobjects");
								bool flag310 = flag309;
								bool flag311 = flag310;
								bool flag312 = flag311;
								bool flag313 = flag312;
								bool flag314 = flag313;
								bool flag315 = flag314;
								if (flag315)
								{
									goto IL_4C4A;
								}
							}
						}
						else
						{
							bool flag316 = !(k == "hitbuildingenemy");
							bool flag317 = flag316;
							bool flag318 = flag317;
							bool flag319 = flag318;
							bool flag320 = flag319;
							bool flag321 = flag320;
							bool flag322 = flag321;
							if (flag322)
							{
								goto IL_4C4A;
							}
						}
					}
					else
					{
						bool flag323 = num <= 1266184123U;
						bool flag324 = flag323;
						bool flag325 = flag324;
						bool flag326 = flag325;
						bool flag327 = flag326;
						bool flag328 = flag327;
						bool flag329 = flag328;
						if (flag329)
						{
							bool flag330 = num != 1259504835U;
							bool flag331 = flag330;
							bool flag332 = flag331;
							bool flag333 = flag332;
							bool flag334 = flag333;
							bool flag335 = flag334;
							bool flag336 = flag335;
							if (flag336)
							{
								bool flag337 = num != 1266184123U;
								bool flag338 = flag337;
								bool flag339 = flag338;
								bool flag340 = flag339;
								bool flag341 = flag340;
								bool flag342 = flag341;
								bool flag343 = flag342;
								if (flag343)
								{
									goto IL_4C4A;
								}
								bool flag344 = !(k == "centerowncolor");
								bool flag345 = flag344;
								bool flag346 = flag345;
								bool flag347 = flag346;
								bool flag348 = flag347;
								bool flag349 = flag348;
								bool flag350 = flag349;
								if (flag350)
								{
									goto IL_4C4A;
								}
								goto IL_4881;
							}
							else
							{
								bool flag351 = !(k == "bombmapnumberdistance");
								bool flag352 = flag351;
								bool flag353 = flag352;
								bool flag354 = flag353;
								bool flag355 = flag354;
								bool flag356 = flag355;
								bool flag357 = flag356;
								if (flag357)
								{
									goto IL_4C4A;
								}
								cfg.bombNumDist = ResourcePackManager.ParseHpFloat(val, cfg.bombNumDist, 5f, 150f);
								return true;
							}
						}
						else
						{
							bool flag358 = num != 1341422310U;
							bool flag359 = flag358;
							bool flag360 = flag359;
							bool flag361 = flag360;
							bool flag362 = flag361;
							bool flag363 = flag362;
							bool flag364 = flag363;
							if (flag364)
							{
								bool flag365 = num != 1398915826U;
								bool flag366 = flag365;
								bool flag367 = flag366;
								bool flag368 = flag367;
								bool flag369 = flag368;
								bool flag370 = flag369;
								bool flag371 = flag370;
								if (flag371)
								{
									goto IL_4C4A;
								}
								bool flag372 = !(k == "hitfulldamage");
								bool flag373 = flag372;
								bool flag374 = flag373;
								bool flag375 = flag374;
								bool flag376 = flag375;
								bool flag377 = flag376;
								bool flag378 = flag377;
								if (flag378)
								{
									goto IL_4C4A;
								}
							}
							else
							{
								bool flag379 = !(k == "centerdistancesize");
								bool flag380 = flag379;
								bool flag381 = flag380;
								bool flag382 = flag381;
								bool flag383 = flag382;
								bool flag384 = flag383;
								bool flag385 = flag384;
								if (flag385)
								{
									goto IL_4C4A;
								}
								cfg.centerDistSize = ResourcePackManager.ParseHpFloat(val, cfg.centerDistSize, 0.3f, 3f);
								return true;
							}
						}
					}
				}
				else
				{
					bool flag386 = num <= 1508992415U;
					bool flag387 = flag386;
					bool flag388 = flag387;
					bool flag389 = flag388;
					bool flag390 = flag389;
					bool flag391 = flag390;
					bool flag392 = flag391;
					if (flag392)
					{
						bool flag393 = num <= 1492214796U;
						bool flag394 = flag393;
						bool flag395 = flag394;
						bool flag396 = flag395;
						bool flag397 = flag396;
						bool flag398 = flag397;
						bool flag399 = flag398;
						if (flag399)
						{
							bool flag400 = num != 1463578989U;
							bool flag401 = flag400;
							bool flag402 = flag401;
							bool flag403 = flag402;
							bool flag404 = flag403;
							bool flag405 = flag404;
							bool flag406 = flag405;
							if (flag406)
							{
								bool flag407 = num != 1492214796U;
								bool flag408 = flag407;
								bool flag409 = flag408;
								bool flag410 = flag409;
								bool flag411 = flag410;
								bool flag412 = flag411;
								bool flag413 = flag412;
								if (flag413)
								{
									goto IL_4C4A;
								}
								bool flag414 = !(k == "hitoffsety");
								bool flag415 = flag414;
								bool flag416 = flag415;
								bool flag417 = flag416;
								bool flag418 = flag417;
								bool flag419 = flag418;
								bool flag420 = flag419;
								if (flag420)
								{
									goto IL_4C4A;
								}
								cfg.hitY = ResourcePackManager.ParseHpFloat(val, cfg.hitY, -200f, 200f);
								return true;
							}
							else
							{
								bool flag421 = !(k == "menublockinput");
								bool flag422 = flag421;
								bool flag423 = flag422;
								bool flag424 = flag423;
								bool flag425 = flag424;
								bool flag426 = flag425;
								bool flag427 = flag426;
								if (flag427)
								{
									goto IL_4C4A;
								}
							}
						}
						else
						{
							bool flag428 = num != 1503799230U;
							bool flag429 = flag428;
							bool flag430 = flag429;
							bool flag431 = flag430;
							bool flag432 = flag431;
							bool flag433 = flag432;
							bool flag434 = flag433;
							if (flag434)
							{
								bool flag435 = num != 1508992415U;
								bool flag436 = flag435;
								bool flag437 = flag436;
								bool flag438 = flag437;
								bool flag439 = flag438;
								bool flag440 = flag439;
								bool flag441 = flag440;
								if (flag441)
								{
									goto IL_4C4A;
								}
								bool flag442 = !(k == "hitoffsetx");
								bool flag443 = flag442;
								bool flag444 = flag443;
								bool flag445 = flag444;
								bool flag446 = flag445;
								bool flag447 = flag446;
								bool flag448 = flag447;
								if (flag448)
								{
									goto IL_4C4A;
								}
								cfg.hitX = ResourcePackManager.ParseHpFloat(val, cfg.hitX, -200f, 200f);
								return true;
							}
							else
							{
								bool flag449 = !(k == "buildinghpnumberoffsety");
								bool flag450 = flag449;
								bool flag451 = flag450;
								bool flag452 = flag451;
								bool flag453 = flag452;
								bool flag454 = flag453;
								bool flag455 = flag454;
								if (flag455)
								{
									goto IL_4C4A;
								}
								cfg.bldHpNumY = ResourcePackManager.ParseHpFloat(val, cfg.bldHpNumY, -400f, 400f);
								return true;
							}
						}
					}
					else
					{
						bool flag456 = num <= 1620011983U;
						bool flag457 = flag456;
						bool flag458 = flag457;
						bool flag459 = flag458;
						bool flag460 = flag459;
						bool flag461 = flag460;
						bool flag462 = flag461;
						if (flag462)
						{
							bool flag463 = num != 1546579007U;
							bool flag464 = flag463;
							bool flag465 = flag464;
							bool flag466 = flag465;
							bool flag467 = flag466;
							bool flag468 = flag467;
							bool flag469 = flag468;
							if (flag469)
							{
								bool flag470 = num != 1620011983U;
								bool flag471 = flag470;
								bool flag472 = flag471;
								bool flag473 = flag472;
								bool flag474 = flag473;
								bool flag475 = flag474;
								bool flag476 = flag475;
								if (flag476)
								{
									goto IL_4C4A;
								}
								bool flag477 = !(k == "centerenemycolor");
								bool flag478 = flag477;
								bool flag479 = flag478;
								bool flag480 = flag479;
								bool flag481 = flag480;
								bool flag482 = flag481;
								bool flag483 = flag482;
								if (flag483)
								{
									goto IL_4C4A;
								}
								goto IL_4881;
							}
							else
							{
								bool flag484 = !(k == "altrings");
								bool flag485 = flag484;
								bool flag486 = flag485;
								bool flag487 = flag486;
								bool flag488 = flag487;
								bool flag489 = flag488;
								bool flag490 = flag489;
								if (flag490)
								{
									goto IL_4C4A;
								}
							}
						}
						else
						{
							bool flag491 = num != 1821647799U;
							bool flag492 = flag491;
							bool flag493 = flag492;
							bool flag494 = flag493;
							bool flag495 = flag494;
							bool flag496 = flag495;
							bool flag497 = flag496;
							if (flag497)
							{
								bool flag498 = num != 1962948445U;
								bool flag499 = flag498;
								bool flag500 = flag499;
								bool flag501 = flag500;
								bool flag502 = flag501;
								bool flag503 = flag502;
								bool flag504 = flag503;
								if (flag504)
								{
									goto IL_4C4A;
								}
								bool flag505 = !(k == "hitstaminaown");
								bool flag506 = flag505;
								bool flag507 = flag506;
								bool flag508 = flag507;
								bool flag509 = flag508;
								bool flag510 = flag509;
								bool flag511 = flag510;
								if (flag511)
								{
									goto IL_4C4A;
								}
							}
							else
							{
								bool flag512 = !(k == "buildingsecondcolorbrightness");
								bool flag513 = flag512;
								bool flag514 = flag513;
								bool flag515 = flag514;
								bool flag516 = flag515;
								bool flag517 = flag516;
								bool flag518 = flag517;
								if (flag518)
								{
									goto IL_4C4A;
								}
								cfg.bldSecondBrightness = ResourcePackManager.ParseHpFloat(val, cfg.bldSecondBrightness, 0f, 4f);
								return true;
							}
						}
					}
				}
			}
		}
		else
		{
			bool flag519 = num <= 3592433196U;
			bool flag520 = flag519;
			bool flag521 = flag520;
			bool flag522 = flag521;
			bool flag523 = flag522;
			bool flag524 = flag523;
			bool flag525 = flag524;
			if (flag525)
			{
				bool flag526 = num <= 2938864219U;
				bool flag527 = flag526;
				bool flag528 = flag527;
				bool flag529 = flag528;
				bool flag530 = flag529;
				bool flag531 = flag530;
				bool flag532 = flag531;
				if (flag532)
				{
					bool flag533 = num <= 2401854863U;
					bool flag534 = flag533;
					bool flag535 = flag534;
					bool flag536 = flag535;
					bool flag537 = flag536;
					bool flag538 = flag537;
					bool flag539 = flag538;
					if (flag539)
					{
						bool flag540 = num != 1976218703U;
						bool flag541 = flag540;
						bool flag542 = flag541;
						bool flag543 = flag542;
						bool flag544 = flag543;
						bool flag545 = flag544;
						bool flag546 = flag545;
						if (flag546)
						{
							bool flag547 = num != 1989152662U;
							bool flag548 = flag547;
							bool flag549 = flag548;
							bool flag550 = flag549;
							bool flag551 = flag550;
							bool flag552 = flag551;
							bool flag553 = flag552;
							if (flag553)
							{
								bool flag554 = num != 2401854863U;
								bool flag555 = flag554;
								bool flag556 = flag555;
								bool flag557 = flag556;
								bool flag558 = flag557;
								bool flag559 = flag558;
								bool flag560 = flag559;
								if (flag560)
								{
									goto IL_4C4A;
								}
								bool flag561 = !(k == "buildingregennumberoffsety");
								bool flag562 = flag561;
								bool flag563 = flag562;
								bool flag564 = flag563;
								bool flag565 = flag564;
								bool flag566 = flag565;
								bool flag567 = flag566;
								if (flag567)
								{
									goto IL_4C4A;
								}
								cfg.bldRegenY = ResourcePackManager.ParseHpFloat(val, cfg.bldRegenY, -400f, 400f);
								return true;
							}
							else
							{
								bool flag568 = !(k == "altcrystal");
								bool flag569 = flag568;
								bool flag570 = flag569;
								bool flag571 = flag570;
								bool flag572 = flag571;
								bool flag573 = flag572;
								bool flag574 = flag573;
								if (flag574)
								{
									goto IL_4C4A;
								}
							}
						}
						else
						{
							bool flag575 = !(k == "centerenemyunits");
							bool flag576 = flag575;
							bool flag577 = flag576;
							bool flag578 = flag577;
							bool flag579 = flag578;
							bool flag580 = flag579;
							bool flag581 = flag580;
							if (flag581)
							{
								goto IL_4C4A;
							}
						}
					}
					else
					{
						bool flag582 = num <= 2849487305U;
						bool flag583 = flag582;
						bool flag584 = flag583;
						bool flag585 = flag584;
						bool flag586 = flag585;
						bool flag587 = flag586;
						bool flag588 = flag587;
						if (flag588)
						{
							bool flag589 = num != 2799725831U;
							bool flag590 = flag589;
							bool flag591 = flag590;
							bool flag592 = flag591;
							bool flag593 = flag592;
							bool flag594 = flag593;
							bool flag595 = flag594;
							if (flag595)
							{
								bool flag596 = num != 2849487305U;
								bool flag597 = flag596;
								bool flag598 = flag597;
								bool flag599 = flag598;
								bool flag600 = flag599;
								bool flag601 = flag600;
								bool flag602 = flag601;
								if (flag602)
								{
									goto IL_4C4A;
								}
								bool flag603 = !(k == "dpssilence");
								bool flag604 = flag603;
								bool flag605 = flag604;
								bool flag606 = flag605;
								bool flag607 = flag606;
								bool flag608 = flag607;
								bool flag609 = flag608;
								if (flag609)
								{
									goto IL_4C4A;
								}
								cfg.dpsSilence = ResourcePackManager.ParseHpFloat(val, cfg.dpsSilence, 1f, 15f);
								return true;
							}
							else
							{
								bool flag610 = !(k == "hitdps");
								bool flag611 = flag610;
								bool flag612 = flag611;
								bool flag613 = flag612;
								bool flag614 = flag613;
								bool flag615 = flag614;
								bool flag616 = flag615;
								if (flag616)
								{
									goto IL_4C4A;
								}
							}
						}
						else
						{
							bool flag617 = num != 2903785309U;
							bool flag618 = flag617;
							bool flag619 = flag618;
							bool flag620 = flag619;
							bool flag621 = flag620;
							bool flag622 = flag621;
							bool flag623 = flag622;
							if (flag623)
							{
								bool flag624 = num != 2938864219U;
								bool flag625 = flag624;
								bool flag626 = flag625;
								bool flag627 = flag626;
								bool flag628 = flag627;
								bool flag629 = flag628;
								bool flag630 = flag629;
								if (flag630)
								{
									goto IL_4C4A;
								}
								bool flag631 = !(k == "hitbuildingdps");
								bool flag632 = flag631;
								bool flag633 = flag632;
								bool flag634 = flag633;
								bool flag635 = flag634;
								bool flag636 = flag635;
								bool flag637 = flag636;
								if (flag637)
								{
									goto IL_4C4A;
								}
							}
							else
							{
								bool flag638 = !(k == "bombmapnumberopacity");
								bool flag639 = flag638;
								bool flag640 = flag639;
								bool flag641 = flag640;
								bool flag642 = flag641;
								bool flag643 = flag642;
								bool flag644 = flag643;
								if (flag644)
								{
									goto IL_4C4A;
								}
								cfg.bombNumAlpha = ResourcePackManager.ParseHpFloat(val, cfg.bombNumAlpha, 0f, 1f);
								return true;
							}
						}
					}
				}
				else
				{
					bool flag645 = num <= 3333509190U;
					bool flag646 = flag645;
					bool flag647 = flag646;
					bool flag648 = flag647;
					bool flag649 = flag648;
					bool flag650 = flag649;
					bool flag651 = flag650;
					if (flag651)
					{
						bool flag652 = num <= 2999003293U;
						bool flag653 = flag652;
						bool flag654 = flag653;
						bool flag655 = flag654;
						bool flag656 = flag655;
						bool flag657 = flag656;
						bool flag658 = flag657;
						if (flag658)
						{
							bool flag659 = num != 2944041146U;
							bool flag660 = flag659;
							bool flag661 = flag660;
							bool flag662 = flag661;
							bool flag663 = flag662;
							bool flag664 = flag663;
							bool flag665 = flag664;
							if (flag665)
							{
								bool flag666 = num != 2999003293U;
								bool flag667 = flag666;
								bool flag668 = flag667;
								bool flag669 = flag668;
								bool flag670 = flag669;
								bool flag671 = flag670;
								bool flag672 = flag671;
								if (flag672)
								{
									goto IL_4C4A;
								}
								bool flag673 = !(k == "statnumberhidedistance");
								bool flag674 = flag673;
								bool flag675 = flag674;
								bool flag676 = flag675;
								bool flag677 = flag676;
								bool flag678 = flag677;
								bool flag679 = flag678;
								if (flag679)
								{
									goto IL_4C4A;
								}
								cfg.statHideDist = ResourcePackManager.ParseHpFloat(val, cfg.statHideDist, 10f, 300f);
								return true;
							}
							else
							{
								bool flag680 = !(k == "altnumbers");
								bool flag681 = flag680;
								bool flag682 = flag681;
								bool flag683 = flag682;
								bool flag684 = flag683;
								bool flag685 = flag684;
								bool flag686 = flag685;
								if (flag686)
								{
									goto IL_4C4A;
								}
							}
						}
						else
						{
							bool flag687 = num != 3235398411U;
							bool flag688 = flag687;
							bool flag689 = flag688;
							bool flag690 = flag689;
							bool flag691 = flag690;
							bool flag692 = flag691;
							bool flag693 = flag692;
							if (flag693)
							{
								bool flag694 = num != 3333509190U;
								bool flag695 = flag694;
								bool flag696 = flag695;
								bool flag697 = flag696;
								bool flag698 = flag697;
								bool flag699 = flag698;
								bool flag700 = flag699;
								if (flag700)
								{
									goto IL_4C4A;
								}
								bool flag701 = !(k == "hitbuildingown");
								bool flag702 = flag701;
								bool flag703 = flag702;
								bool flag704 = flag703;
								bool flag705 = flag704;
								bool flag706 = flag705;
								bool flag707 = flag706;
								if (flag707)
								{
									goto IL_4C4A;
								}
							}
							else
							{
								bool flag708 = !(k == "hittime");
								bool flag709 = flag708;
								bool flag710 = flag709;
								bool flag711 = flag710;
								bool flag712 = flag711;
								bool flag713 = flag712;
								bool flag714 = flag713;
								if (flag714)
								{
									goto IL_4C4A;
								}
								cfg.hitTime = ResourcePackManager.ParseHpFloat(val, cfg.hitTime, 0.3f, 3f);
								return true;
							}
						}
					}
					else
					{
						bool flag715 = num <= 3428495806U;
						bool flag716 = flag715;
						bool flag717 = flag716;
						bool flag718 = flag717;
						bool flag719 = flag718;
						bool flag720 = flag719;
						bool flag721 = flag720;
						if (flag721)
						{
							bool flag722 = num != 3347690925U;
							bool flag723 = flag722;
							bool flag724 = flag723;
							bool flag725 = flag724;
							bool flag726 = flag725;
							bool flag727 = flag726;
							bool flag728 = flag727;
							if (flag728)
							{
								bool flag729 = num != 3428495806U;
								bool flag730 = flag729;
								bool flag731 = flag730;
								bool flag732 = flag731;
								bool flag733 = flag732;
								bool flag734 = flag733;
								bool flag735 = flag734;
								if (flag735)
								{
									goto IL_4C4A;
								}
								bool flag736 = !(k == "centerdistancetime");
								bool flag737 = flag736;
								bool flag738 = flag737;
								bool flag739 = flag738;
								bool flag740 = flag739;
								bool flag741 = flag740;
								bool flag742 = flag741;
								if (flag742)
								{
									goto IL_4C4A;
								}
								cfg.centerDistTime = ResourcePackManager.ParseHpFloat(val, cfg.centerDistTime, 0.5f, 6f);
								return true;
							}
							else
							{
								bool flag743 = !(k == "perfmode");
								bool flag744 = flag743;
								bool flag745 = flag744;
								bool flag746 = flag745;
								bool flag747 = flag746;
								bool flag748 = flag747;
								bool flag749 = flag748;
								if (flag749)
								{
									goto IL_4C4A;
								}
							}
						}
						else
						{
							bool flag750 = num != 3507496856U;
							bool flag751 = flag750;
							bool flag752 = flag751;
							bool flag753 = flag752;
							bool flag754 = flag753;
							bool flag755 = flag754;
							bool flag756 = flag755;
							if (flag756)
							{
								bool flag757 = num != 3592433196U;
								bool flag758 = flag757;
								bool flag759 = flag758;
								bool flag760 = flag759;
								bool flag761 = flag760;
								bool flag762 = flag761;
								bool flag763 = flag762;
								if (flag763)
								{
									goto IL_4C4A;
								}
								bool flag764 = !(k == "menupause");
								bool flag765 = flag764;
								bool flag766 = flag765;
								bool flag767 = flag766;
								bool flag768 = flag767;
								bool flag769 = flag768;
								bool flag770 = flag769;
								if (flag770)
								{
									goto IL_4C4A;
								}
							}
							else
							{
								bool flag771 = !(k == "altpaths");
								bool flag772 = flag771;
								bool flag773 = flag772;
								bool flag774 = flag773;
								bool flag775 = flag774;
								bool flag776 = flag775;
								bool flag777 = flag776;
								if (flag777)
								{
									goto IL_4C4A;
								}
							}
						}
					}
				}
			}
			else
			{
				bool flag778 = num <= 3777450131U;
				bool flag779 = flag778;
				bool flag780 = flag779;
				bool flag781 = flag780;
				bool flag782 = flag781;
				bool flag783 = flag782;
				bool flag784 = flag783;
				if (flag784)
				{
					bool flag785 = num <= 3640498079U;
					bool flag786 = flag785;
					bool flag787 = flag786;
					bool flag788 = flag787;
					bool flag789 = flag788;
					bool flag790 = flag789;
					bool flag791 = flag790;
					if (flag791)
					{
						bool flag792 = num <= 3608732291U;
						bool flag793 = flag792;
						bool flag794 = flag793;
						bool flag795 = flag794;
						bool flag796 = flag795;
						bool flag797 = flag796;
						bool flag798 = flag797;
						if (flag798)
						{
							bool flag799 = num != 3600873442U;
							bool flag800 = flag799;
							bool flag801 = flag800;
							bool flag802 = flag801;
							bool flag803 = flag802;
							bool flag804 = flag803;
							bool flag805 = flag804;
							if (flag805)
							{
								bool flag806 = num != 3608732291U;
								bool flag807 = flag806;
								bool flag808 = flag807;
								bool flag809 = flag808;
								bool flag810 = flag809;
								bool flag811 = flag810;
								bool flag812 = flag811;
								if (flag812)
								{
									goto IL_4C4A;
								}
								bool flag813 = !(k == "dpsprecise");
								bool flag814 = flag813;
								bool flag815 = flag814;
								bool flag816 = flag815;
								bool flag817 = flag816;
								bool flag818 = flag817;
								bool flag819 = flag818;
								if (flag819)
								{
									goto IL_4C4A;
								}
							}
							else
							{
								bool flag820 = !(k == "hitown");
								bool flag821 = flag820;
								bool flag822 = flag821;
								bool flag823 = flag822;
								bool flag824 = flag823;
								bool flag825 = flag824;
								bool flag826 = flag825;
								if (flag826)
								{
									goto IL_4C4A;
								}
							}
						}
						else
						{
							bool flag827 = num != 3627710351U;
							bool flag828 = flag827;
							bool flag829 = flag828;
							bool flag830 = flag829;
							bool flag831 = flag830;
							bool flag832 = flag831;
							bool flag833 = flag832;
							if (flag833)
							{
								bool flag834 = num != 3640498079U;
								bool flag835 = flag834;
								bool flag836 = flag835;
								bool flag837 = flag836;
								bool flag838 = flag837;
								bool flag839 = flag838;
								bool flag840 = flag839;
								if (flag840)
								{
									goto IL_4C4A;
								}
								bool flag841 = !(k == "regennumberoffsety");
								bool flag842 = flag841;
								bool flag843 = flag842;
								bool flag844 = flag843;
								bool flag845 = flag844;
								bool flag846 = flag845;
								bool flag847 = flag846;
								if (flag847)
								{
									goto IL_4C4A;
								}
								cfg.regenNumY = ResourcePackManager.ParseHpFloat(val, cfg.regenNumY, -400f, 400f);
								return true;
							}
							else
							{
								bool flag848 = !(k == "centerdistance");
								bool flag849 = flag848;
								bool flag850 = flag849;
								bool flag851 = flag850;
								bool flag852 = flag851;
								bool flag853 = flag852;
								bool flag854 = flag853;
								if (flag854)
								{
									goto IL_4C4A;
								}
							}
						}
					}
					else
					{
						bool flag855 = num <= 3727048566U;
						bool flag856 = flag855;
						bool flag857 = flag856;
						bool flag858 = flag857;
						bool flag859 = flag858;
						bool flag860 = flag859;
						bool flag861 = flag860;
						if (flag861)
						{
							bool flag862 = num != 3652078161U;
							bool flag863 = flag862;
							bool flag864 = flag863;
							bool flag865 = flag864;
							bool flag866 = flag865;
							bool flag867 = flag866;
							bool flag868 = flag867;
							if (flag868)
							{
								bool flag869 = num != 3727048566U;
								bool flag870 = flag869;
								bool flag871 = flag870;
								bool flag872 = flag871;
								bool flag873 = flag872;
								bool flag874 = flag873;
								bool flag875 = flag874;
								if (flag875)
								{
									goto IL_4C4A;
								}
								bool flag876 = !(k == "hitenemy");
								bool flag877 = flag876;
								bool flag878 = flag877;
								bool flag879 = flag878;
								bool flag880 = flag879;
								bool flag881 = flag880;
								bool flag882 = flag881;
								if (flag882)
								{
									goto IL_4C4A;
								}
							}
							else
							{
								bool flag883 = !(k == "hitstaminaenemy");
								bool flag884 = flag883;
								bool flag885 = flag884;
								bool flag886 = flag885;
								bool flag887 = flag886;
								bool flag888 = flag887;
								bool flag889 = flag888;
								if (flag889)
								{
									goto IL_4C4A;
								}
							}
						}
						else
						{
							bool flag890 = num != 3735472061U;
							bool flag891 = flag890;
							bool flag892 = flag891;
							bool flag893 = flag892;
							bool flag894 = flag893;
							bool flag895 = flag894;
							bool flag896 = flag895;
							if (flag896)
							{
								bool flag897 = num != 3777450131U;
								bool flag898 = flag897;
								bool flag899 = flag898;
								bool flag900 = flag899;
								bool flag901 = flag900;
								bool flag902 = flag901;
								bool flag903 = flag902;
								if (flag903)
								{
									goto IL_4C4A;
								}
								bool flag904 = !(k == "hitsize");
								bool flag905 = flag904;
								bool flag906 = flag905;
								bool flag907 = flag906;
								bool flag908 = flag907;
								bool flag909 = flag908;
								bool flag910 = flag909;
								if (flag910)
								{
									goto IL_4C4A;
								}
								cfg.hitSize = ResourcePackManager.ParseHpFloat(val, cfg.hitSize, 0.3f, 3f);
								return true;
							}
							else
							{
								bool flag911 = !(k == "altdamage");
								bool flag912 = flag911;
								bool flag913 = flag912;
								bool flag914 = flag913;
								bool flag915 = flag914;
								bool flag916 = flag915;
								bool flag917 = flag916;
								if (flag917)
								{
									goto IL_4C4A;
								}
							}
						}
					}
				}
				else
				{
					bool flag918 = num <= 3866795000U;
					bool flag919 = flag918;
					bool flag920 = flag919;
					bool flag921 = flag920;
					bool flag922 = flag921;
					bool flag923 = flag922;
					bool flag924 = flag923;
					if (flag924)
					{
						bool flag925 = num <= 3823836789U;
						bool flag926 = flag925;
						bool flag927 = flag926;
						bool flag928 = flag927;
						bool flag929 = flag928;
						bool flag930 = flag929;
						bool flag931 = flag930;
						if (flag931)
						{
							bool flag932 = num != 3784741130U;
							bool flag933 = flag932;
							bool flag934 = flag933;
							bool flag935 = flag934;
							bool flag936 = flag935;
							bool flag937 = flag936;
							bool flag938 = flag937;
							if (flag938)
							{
								bool flag939 = num != 3823836789U;
								bool flag940 = flag939;
								bool flag941 = flag940;
								bool flag942 = flag941;
								bool flag943 = flag942;
								bool flag944 = flag943;
								bool flag945 = flag944;
								if (flag945)
								{
									goto IL_4C4A;
								}
								bool flag946 = !(k == "hitowncolor");
								bool flag947 = flag946;
								bool flag948 = flag947;
								bool flag949 = flag948;
								bool flag950 = flag949;
								bool flag951 = flag950;
								bool flag952 = flag951;
								if (flag952)
								{
									goto IL_4C4A;
								}
								goto IL_4881;
							}
							else
							{
								bool flag953 = !(k == "hitstaminacolor");
								bool flag954 = flag953;
								bool flag955 = flag954;
								bool flag956 = flag955;
								bool flag957 = flag956;
								bool flag958 = flag957;
								bool flag959 = flag958;
								if (flag959)
								{
									goto IL_4C4A;
								}
								goto IL_4881;
							}
						}
						else
						{
							bool flag960 = num != 3844348771U;
							bool flag961 = flag960;
							bool flag962 = flag961;
							bool flag963 = flag962;
							bool flag964 = flag963;
							bool flag965 = flag964;
							bool flag966 = flag965;
							if (flag966)
							{
								bool flag967 = num != 3866795000U;
								bool flag968 = flag967;
								bool flag969 = flag968;
								bool flag970 = flag969;
								bool flag971 = flag970;
								bool flag972 = flag971;
								bool flag973 = flag972;
								if (flag973)
								{
									goto IL_4C4A;
								}
								bool flag974 = !(k == "crystalsize");
								bool flag975 = flag974;
								bool flag976 = flag975;
								bool flag977 = flag976;
								bool flag978 = flag977;
								bool flag979 = flag978;
								bool flag980 = flag979;
								if (flag980)
								{
									goto IL_4C4A;
								}
								cfg.crystalSize = ResourcePackManager.ParseHpFloat(val, cfg.crystalSize, 0.3f, 4f);
								return true;
							}
							else
							{
								bool flag981 = !(k == "bombmaplayersv2");
								bool flag982 = flag981;
								bool flag983 = flag982;
								bool flag984 = flag983;
								bool flag985 = flag984;
								bool flag986 = flag985;
								bool flag987 = flag986;
								if (flag987)
								{
									goto IL_4C4A;
								}
								cfg.bombMapLayers = Mathf.Clamp(Mathf.RoundToInt(ResourcePackManager.ParseHpFloat(val, (float)cfg.bombMapLayers, 1f, 8f)), 1, 8);
								return true;
							}
						}
					}
					else
					{
						bool flag988 = num <= 3908035530U;
						bool flag989 = flag988;
						bool flag990 = flag989;
						bool flag991 = flag990;
						bool flag992 = flag991;
						bool flag993 = flag992;
						bool flag994 = flag993;
						if (flag994)
						{
							bool flag995 = num != 3869167644U;
							bool flag996 = flag995;
							bool flag997 = flag996;
							bool flag998 = flag997;
							bool flag999 = flag998;
							bool flag1000 = flag999;
							bool flag1001 = flag1000;
							if (flag1001)
							{
								bool flag1002 = num != 3908035530U;
								bool flag1003 = flag1002;
								bool flag1004 = flag1003;
								bool flag1005 = flag1004;
								bool flag1006 = flag1005;
								bool flag1007 = flag1006;
								bool flag1008 = flag1007;
								if (flag1008)
								{
									goto IL_4C4A;
								}
								bool flag1009 = !(k == "crystaloffsetx");
								bool flag1010 = flag1009;
								bool flag1011 = flag1010;
								bool flag1012 = flag1011;
								bool flag1013 = flag1012;
								bool flag1014 = flag1013;
								bool flag1015 = flag1014;
								if (flag1015)
								{
									goto IL_4C4A;
								}
								cfg.crystalX = ResourcePackManager.ParseHpFloat(val, cfg.crystalX, -200f, 200f);
								return true;
							}
							else
							{
								bool flag1016 = !(k == "crystaldowncolor");
								bool flag1017 = flag1016;
								bool flag1018 = flag1017;
								bool flag1019 = flag1018;
								bool flag1020 = flag1019;
								bool flag1021 = flag1020;
								bool flag1022 = flag1021;
								if (flag1022)
								{
									goto IL_4C4A;
								}
								goto IL_4881;
							}
						}
						else
						{
							bool flag1023 = num != 3924813149U;
							bool flag1024 = flag1023;
							bool flag1025 = flag1024;
							bool flag1026 = flag1025;
							bool flag1027 = flag1026;
							bool flag1028 = flag1027;
							bool flag1029 = flag1028;
							if (flag1029)
							{
								bool flag1030 = num != 3926385505U;
								bool flag1031 = flag1030;
								bool flag1032 = flag1031;
								bool flag1033 = flag1032;
								bool flag1034 = flag1033;
								bool flag1035 = flag1034;
								bool flag1036 = flag1035;
								if (flag1036)
								{
									goto IL_4C4A;
								}
								bool flag1037 = !(k == "dpscolor");
								bool flag1038 = flag1037;
								bool flag1039 = flag1038;
								bool flag1040 = flag1039;
								bool flag1041 = flag1040;
								bool flag1042 = flag1041;
								bool flag1043 = flag1042;
								if (flag1043)
								{
									goto IL_4C4A;
								}
								goto IL_4881;
							}
							else
							{
								bool flag1044 = !(k == "crystaloffsety");
								bool flag1045 = flag1044;
								bool flag1046 = flag1045;
								bool flag1047 = flag1046;
								bool flag1048 = flag1047;
								bool flag1049 = flag1048;
								bool flag1050 = flag1049;
								if (flag1050)
								{
									goto IL_4C4A;
								}
								cfg.crystalY = ResourcePackManager.ParseHpFloat(val, cfg.crystalY, -200f, 200f);
								return true;
							}
						}
					}
				}
			}
		}
		bool flag1052;
		bool flag1051 = !ResourcePackManager.TryParseHpBool(val, out flag1052);
		bool flag1053 = flag1051;
		bool flag1054 = flag1053;
		bool flag1055 = flag1054;
		bool flag1056 = flag1055;
		bool flag1057 = flag1056;
		bool flag1058 = flag1057;
		bool flag1059 = flag1058;
		if (flag1059)
		{
			problems.Add(string.Concat(new object[]
			{
				"line ",
				lineIdx + 1,
				": ",
				k,
				" must be true/false"
			}));
			return true;
		}
		bool flag1060 = k == "hitown";
		bool flag1061 = flag1060;
		bool flag1062 = flag1061;
		bool flag1063 = flag1062;
		bool flag1064 = flag1063;
		bool flag1065 = flag1064;
		bool flag1066 = flag1065;
		bool flag1067 = flag1066;
		if (flag1067)
		{
			cfg.hitOwn = flag1052;
		}
		else
		{
			bool flag1068 = k == "hitenemy";
			bool flag1069 = flag1068;
			bool flag1070 = flag1069;
			bool flag1071 = flag1070;
			bool flag1072 = flag1071;
			bool flag1073 = flag1072;
			bool flag1074 = flag1073;
			bool flag1075 = flag1074;
			if (flag1075)
			{
				cfg.hitEnemy = flag1052;
			}
			else
			{
				bool flag1076 = k == "hitdps";
				bool flag1077 = flag1076;
				bool flag1078 = flag1077;
				bool flag1079 = flag1078;
				bool flag1080 = flag1079;
				bool flag1081 = flag1080;
				bool flag1082 = flag1081;
				bool flag1083 = flag1082;
				if (flag1083)
				{
					cfg.hitDps = flag1052;
				}
				else
				{
					bool flag1084 = k == "hitstaminaown";
					bool flag1085 = flag1084;
					bool flag1086 = flag1085;
					bool flag1087 = flag1086;
					bool flag1088 = flag1087;
					bool flag1089 = flag1088;
					bool flag1090 = flag1089;
					bool flag1091 = flag1090;
					if (flag1091)
					{
						cfg.hitStamOwn = flag1052;
					}
					else
					{
						bool flag1092 = k == "hitstaminaenemy";
						bool flag1093 = flag1092;
						bool flag1094 = flag1093;
						bool flag1095 = flag1094;
						bool flag1096 = flag1095;
						bool flag1097 = flag1096;
						bool flag1098 = flag1097;
						bool flag1099 = flag1098;
						if (flag1099)
						{
							cfg.hitStamEnemy = flag1052;
						}
						else
						{
							bool flag1100 = k == "dpsincludestamina";
							bool flag1101 = flag1100;
							bool flag1102 = flag1101;
							bool flag1103 = flag1102;
							bool flag1104 = flag1103;
							bool flag1105 = flag1104;
							bool flag1106 = flag1105;
							bool flag1107 = flag1106;
							if (flag1107)
							{
								cfg.dpsStamina = flag1052;
							}
							else
							{
								bool flag1108 = k == "hitbuildingown";
								bool flag1109 = flag1108;
								bool flag1110 = flag1109;
								bool flag1111 = flag1110;
								bool flag1112 = flag1111;
								bool flag1113 = flag1112;
								bool flag1114 = flag1113;
								bool flag1115 = flag1114;
								if (flag1115)
								{
									cfg.hitBldOwn = flag1052;
								}
								else
								{
									bool flag1116 = k == "hitbuildingenemy";
									bool flag1117 = flag1116;
									bool flag1118 = flag1117;
									bool flag1119 = flag1118;
									bool flag1120 = flag1119;
									bool flag1121 = flag1120;
									bool flag1122 = flag1121;
									bool flag1123 = flag1122;
									if (flag1123)
									{
										cfg.hitBldEnemy = flag1052;
									}
									else
									{
										bool flag1124 = k == "hitbuildingdps";
										bool flag1125 = flag1124;
										bool flag1126 = flag1125;
										bool flag1127 = flag1126;
										bool flag1128 = flag1127;
										bool flag1129 = flag1128;
										bool flag1130 = flag1129;
										bool flag1131 = flag1130;
										if (flag1131)
										{
											cfg.hitBldDps = flag1052;
										}
										else
										{
											bool flag1132 = k == "hitfulldamage";
											bool flag1133 = flag1132;
											bool flag1134 = flag1133;
											bool flag1135 = flag1134;
											bool flag1136 = flag1135;
											bool flag1137 = flag1136;
											bool flag1138 = flag1137;
											bool flag1139 = flag1138;
											if (flag1139)
											{
												cfg.hitFullDamage = flag1052;
											}
											else
											{
												bool flag1140 = k == "menupause";
												bool flag1141 = flag1140;
												bool flag1142 = flag1141;
												bool flag1143 = flag1142;
												bool flag1144 = flag1143;
												bool flag1145 = flag1144;
												bool flag1146 = flag1145;
												bool flag1147 = flag1146;
												if (flag1147)
												{
													cfg.menuPause = flag1052;
												}
												else
												{
													bool flag1148 = k == "menublockinput";
													bool flag1149 = flag1148;
													bool flag1150 = flag1149;
													bool flag1151 = flag1150;
													bool flag1152 = flag1151;
													bool flag1153 = flag1152;
													bool flag1154 = flag1153;
													bool flag1155 = flag1154;
													if (flag1155)
													{
														cfg.menuBlockInput = flag1052;
													}
													else
													{
														bool flag1156 = k == "centeron";
														bool flag1157 = flag1156;
														bool flag1158 = flag1157;
														bool flag1159 = flag1158;
														bool flag1160 = flag1159;
														bool flag1161 = flag1160;
														bool flag1162 = flag1161;
														bool flag1163 = flag1162;
														if (flag1163)
														{
															cfg.centerOn = flag1052;
														}
														else
														{
															bool flag1164 = k == "centerdistance";
															bool flag1165 = flag1164;
															bool flag1166 = flag1165;
															bool flag1167 = flag1166;
															bool flag1168 = flag1167;
															bool flag1169 = flag1168;
															bool flag1170 = flag1169;
															bool flag1171 = flag1170;
															if (flag1171)
															{
																cfg.centerDist = flag1052;
															}
															else
															{
																bool flag1172 = k == "centerownunits";
																bool flag1173 = flag1172;
																bool flag1174 = flag1173;
																bool flag1175 = flag1174;
																bool flag1176 = flag1175;
																bool flag1177 = flag1176;
																bool flag1178 = flag1177;
																bool flag1179 = flag1178;
																if (flag1179)
																{
																	cfg.centerOwnUnits = flag1052;
																}
																else
																{
																	bool flag1180 = k == "centerenemyunits";
																	bool flag1181 = flag1180;
																	bool flag1182 = flag1181;
																	bool flag1183 = flag1182;
																	bool flag1184 = flag1183;
																	bool flag1185 = flag1184;
																	bool flag1186 = flag1185;
																	bool flag1187 = flag1186;
																	if (flag1187)
																	{
																		cfg.centerEnemyUnits = flag1052;
																	}
																	else
																	{
																		bool flag1188 = k == "centerownobjects";
																		bool flag1189 = flag1188;
																		bool flag1190 = flag1189;
																		bool flag1191 = flag1190;
																		bool flag1192 = flag1191;
																		bool flag1193 = flag1192;
																		bool flag1194 = flag1193;
																		bool flag1195 = flag1194;
																		if (flag1195)
																		{
																			cfg.centerOwnObjects = flag1052;
																		}
																		else
																		{
																			bool flag1196 = k == "centerenemyobjects";
																			bool flag1197 = flag1196;
																			bool flag1198 = flag1197;
																			bool flag1199 = flag1198;
																			bool flag1200 = flag1199;
																			bool flag1201 = flag1200;
																			bool flag1202 = flag1201;
																			bool flag1203 = flag1202;
																			if (flag1203)
																			{
																				cfg.centerEnemyObjects = flag1052;
																			}
																			else
																			{
																				bool flag1204 = k == "altonly";
																				bool flag1205 = flag1204;
																				bool flag1206 = flag1205;
																				bool flag1207 = flag1206;
																				bool flag1208 = flag1207;
																				bool flag1209 = flag1208;
																				bool flag1210 = flag1209;
																				bool flag1211 = flag1210;
																				if (flag1211)
																				{
																					cfg.altOnly = flag1052;
																				}
																				else
																				{
																					bool flag1212 = k == "altnumbers";
																					bool flag1213 = flag1212;
																					bool flag1214 = flag1213;
																					bool flag1215 = flag1214;
																					bool flag1216 = flag1215;
																					bool flag1217 = flag1216;
																					bool flag1218 = flag1217;
																					bool flag1219 = flag1218;
																					if (flag1219)
																					{
																						cfg.altNumbers = flag1052;
																					}
																					else
																					{
																						bool flag1220 = k == "altammo";
																						bool flag1221 = flag1220;
																						bool flag1222 = flag1221;
																						bool flag1223 = flag1222;
																						bool flag1224 = flag1223;
																						bool flag1225 = flag1224;
																						bool flag1226 = flag1225;
																						bool flag1227 = flag1226;
																						if (flag1227)
																						{
																							cfg.altAmmo = flag1052;
																						}
																						else
																						{
																							bool flag1228 = k == "altdamage";
																							bool flag1229 = flag1228;
																							bool flag1230 = flag1229;
																							bool flag1231 = flag1230;
																							bool flag1232 = flag1231;
																							bool flag1233 = flag1232;
																							bool flag1234 = flag1233;
																							bool flag1235 = flag1234;
																							if (flag1235)
																							{
																								cfg.altDamage = flag1052;
																							}
																							else
																							{
																								bool flag1236 = k == "altcrystal";
																								bool flag1237 = flag1236;
																								bool flag1238 = flag1237;
																								bool flag1239 = flag1238;
																								bool flag1240 = flag1239;
																								bool flag1241 = flag1240;
																								bool flag1242 = flag1241;
																								bool flag1243 = flag1242;
																								if (flag1243)
																								{
																									cfg.altCrystal = flag1052;
																								}
																								else
																								{
																									bool flag1244 = k == "altbombmap";
																									bool flag1245 = flag1244;
																									bool flag1246 = flag1245;
																									bool flag1247 = flag1246;
																									bool flag1248 = flag1247;
																									bool flag1249 = flag1248;
																									bool flag1250 = flag1249;
																									bool flag1251 = flag1250;
																									if (flag1251)
																									{
																										cfg.altBombMap = flag1052;
																									}
																									else
																									{
																										bool flag1252 = k == "altrings";
																										bool flag1253 = flag1252;
																										bool flag1254 = flag1253;
																										bool flag1255 = flag1254;
																										bool flag1256 = flag1255;
																										bool flag1257 = flag1256;
																										bool flag1258 = flag1257;
																										bool flag1259 = flag1258;
																										if (flag1259)
																										{
																											cfg.altRings = flag1052;
																										}
																										else
																										{
																											bool flag1260 = k == "altpaths";
																											bool flag1261 = flag1260;
																											bool flag1262 = flag1261;
																											bool flag1263 = flag1262;
																											bool flag1264 = flag1263;
																											bool flag1265 = flag1264;
																											bool flag1266 = flag1265;
																											bool flag1267 = flag1266;
																											if (flag1267)
																											{
																												cfg.altPaths = flag1052;
																											}
																											else
																											{
																												bool flag1268 = k == "dpsprecise";
																												bool flag1269 = flag1268;
																												bool flag1270 = flag1269;
																												bool flag1271 = flag1270;
																												bool flag1272 = flag1271;
																												bool flag1273 = flag1272;
																												bool flag1274 = flag1273;
																												bool flag1275 = flag1274;
																												if (flag1275)
																												{
																													cfg.dpsPrecise = flag1052;
																												}
																												else
																												{
																													bool flag1276 = k == "perfmode";
																													bool flag1277 = flag1276;
																													bool flag1278 = flag1277;
																													bool flag1279 = flag1278;
																													bool flag1280 = flag1279;
																													bool flag1281 = flag1280;
																													bool flag1282 = flag1281;
																													bool flag1283 = flag1282;
																													if (flag1283)
																													{
																														cfg.perfMode = flag1052;
																													}
																													else
																													{
																														cfg.crystalTimer = flag1052;
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
		return true;
		IL_4881:
		Color color;
		bool flag1284 = !ResourcePackManager.TryParseHpColor(val, out color);
		bool flag1285 = flag1284;
		bool flag1286 = flag1285;
		bool flag1287 = flag1286;
		bool flag1288 = flag1287;
		bool flag1289 = flag1288;
		bool flag1290 = flag1289;
		bool flag1291 = flag1290;
		if (flag1291)
		{
			problems.Add(string.Concat(new object[]
			{
				"line ",
				lineIdx + 1,
				": bad color '",
				val,
				"'"
			}));
			return true;
		}
		color.a = 1f;
		bool flag1292 = k == "hitowncolor";
		bool flag1293 = flag1292;
		bool flag1294 = flag1293;
		bool flag1295 = flag1294;
		bool flag1296 = flag1295;
		bool flag1297 = flag1296;
		bool flag1298 = flag1297;
		bool flag1299 = flag1298;
		if (flag1299)
		{
			cfg.hitOwnColor = color;
		}
		else
		{
			bool flag1300 = k == "hitenemycolor";
			bool flag1301 = flag1300;
			bool flag1302 = flag1301;
			bool flag1303 = flag1302;
			bool flag1304 = flag1303;
			bool flag1305 = flag1304;
			bool flag1306 = flag1305;
			bool flag1307 = flag1306;
			if (flag1307)
			{
				cfg.hitEnemyColor = color;
			}
			else
			{
				bool flag1308 = k == "dpscolor";
				bool flag1309 = flag1308;
				bool flag1310 = flag1309;
				bool flag1311 = flag1310;
				bool flag1312 = flag1311;
				bool flag1313 = flag1312;
				bool flag1314 = flag1313;
				bool flag1315 = flag1314;
				if (flag1315)
				{
					cfg.dpsColor = color;
				}
				else
				{
					bool flag1316 = k == "hitstaminacolor";
					bool flag1317 = flag1316;
					bool flag1318 = flag1317;
					bool flag1319 = flag1318;
					bool flag1320 = flag1319;
					bool flag1321 = flag1320;
					bool flag1322 = flag1321;
					bool flag1323 = flag1322;
					if (flag1323)
					{
						cfg.hitStamColor = color;
					}
					else
					{
						bool flag1324 = k == "centerowncolor";
						bool flag1325 = flag1324;
						bool flag1326 = flag1325;
						bool flag1327 = flag1326;
						bool flag1328 = flag1327;
						bool flag1329 = flag1328;
						bool flag1330 = flag1329;
						bool flag1331 = flag1330;
						if (flag1331)
						{
							cfg.centerOwnColor = color;
						}
						else
						{
							bool flag1332 = k == "centerenemycolor";
							bool flag1333 = flag1332;
							bool flag1334 = flag1333;
							bool flag1335 = flag1334;
							bool flag1336 = flag1335;
							bool flag1337 = flag1336;
							bool flag1338 = flag1337;
							bool flag1339 = flag1338;
							if (flag1339)
							{
								cfg.centerEnemyColor = color;
							}
							else
							{
								bool flag1340 = k == "crystalupcolor";
								bool flag1341 = flag1340;
								bool flag1342 = flag1341;
								bool flag1343 = flag1342;
								bool flag1344 = flag1343;
								bool flag1345 = flag1344;
								bool flag1346 = flag1345;
								bool flag1347 = flag1346;
								if (flag1347)
								{
									cfg.crystalUpColor = color;
								}
								else
								{
									cfg.crystalDownColor = color;
								}
							}
						}
					}
				}
			}
		}
		return true;
		IL_4C4A:
		return false;
	}
}
}
