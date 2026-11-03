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

	private void DrawPaths(Camera cam)
	{
		ResourcePackManager.HpConfig hpConfig = this.hpCfg;
		bool flag = hpConfig == null || this.pathUnits.Count == 0 || cam == null;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			bool flag9 = this.pathCamFrame != Time.frameCount;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				this.pathCamCache = Camera.main;
				this.pathCamFrame = Time.frameCount;
			}
			bool flag17 = cam != this.pathCamCache || !this.EnsurePathMat();
			bool flag18 = !flag17;
			bool flag19 = flag18;
			bool flag20 = flag19;
			bool flag21 = flag20;
			bool flag22 = flag21;
			bool flag23 = flag22;
			bool flag24 = flag23;
			if (flag24)
			{
				try
				{
					bool flag25 = this.pathMat.HasProperty("_ZTest");
					bool flag26 = flag25;
					bool flag27 = flag26;
					bool flag28 = flag27;
					bool flag29 = flag28;
					bool flag30 = flag29;
					bool flag31 = flag30;
					bool flag32 = flag31;
					if (flag32)
					{
						this.pathMat.SetInt("_ZTest", hpConfig.pathOnTop ? 8 : 4);
					}
					Transform transform = cam.transform;
					Vector3 position = transform.position;
					Vector3 forward = transform.forward;
					Vector3 right = transform.right;
					Vector3 up = transform.up;
					float num = Mathf.Max(0.5f, hpConfig.pathWidth * 0.5f * (float)cam.pixelHeight / 1080f);
					float perDepth = 0f;
					float ortho = 0f;
					bool orthographic = cam.orthographic;
					bool flag33 = orthographic;
					bool flag34 = flag33;
					bool flag35 = flag34;
					bool flag36 = flag35;
					bool flag37 = flag36;
					bool flag38 = flag37;
					bool flag39 = flag38;
					if (flag39)
					{
						ortho = num * 2f * cam.orthographicSize / Mathf.Max(1f, (float)cam.pixelHeight);
					}
					else
					{
						perDepth = num * 2f * Mathf.Tan(cam.fieldOfView * 0.5f * 0.017453292f) / Mathf.Max(1f, (float)cam.pixelHeight);
					}
					Vector3 b = Vector3.up * hpConfig.pathLift;
					this.pathMat.SetPass(0);
					GL.PushMatrix();
					GL.Begin(7);
					for (int i = 0; i < this.pathUnits.Count; i++)
					{
						ResourcePackManager.PathUnit pathUnit = this.pathUnits[i];
						List<Vector3> pts = pathUnit.pts;
						bool flag40 = pts.Count < 2;
						bool flag41 = !flag40;
						bool flag42 = flag41;
						bool flag43 = flag42;
						bool flag44 = flag43;
						bool flag45 = flag44;
						bool flag46 = flag45;
						bool flag47 = flag46;
						if (flag47)
						{
							Color pathOwnColor = hpConfig.pathOwnColor;
							pathOwnColor.a *= hpConfig.pathAlpha;
							bool trail = pathUnit.trail;
							bool flag48 = trail;
							bool flag49 = flag48;
							bool flag50 = flag49;
							bool flag51 = flag50;
							bool flag52 = flag51;
							bool flag53 = flag52;
							bool flag54 = flag53;
							if (flag54)
							{
								pathOwnColor.a *= 0.6f;
							}
							GL.Color(pathOwnColor);
							for (int j = 0; j < pts.Count - 1; j++)
							{
								Vector3 vector = pts[j] + b;
								Vector3 vector2 = pts[j + 1] + b;
								Vector3 a = Vector3.Cross(vector2 - vector, (vector + vector2) * 0.5f - position);
								float magnitude = a.magnitude;
								bool flag55 = magnitude < 1E-05f;
								bool flag56 = !flag55;
								bool flag57 = flag56;
								bool flag58 = flag57;
								bool flag59 = flag58;
								bool flag60 = flag59;
								bool flag61 = flag60;
								bool flag62 = flag61;
								if (flag62)
								{
									a /= magnitude;
									float d = ResourcePackManager.PathHalfWidth(vector, position, forward, perDepth, ortho);
									float d2 = ResourcePackManager.PathHalfWidth(vector2, position, forward, perDepth, ortho);
									GL.Vertex(vector - a * d);
									GL.Vertex(vector + a * d);
									GL.Vertex(vector2 + a * d2);
									GL.Vertex(vector2 - a * d2);
									bool flag63 = j > 0;
									bool flag64 = flag63;
									bool flag65 = flag64;
									bool flag66 = flag65;
									bool flag67 = flag66;
									bool flag68 = flag67;
									bool flag69 = flag68;
									bool flag70 = flag69;
									if (flag70)
									{
										ResourcePackManager.PathQuad(vector, right * d, up * d);
									}
								}
							}
							bool flag71 = hpConfig.pathMarker && !pathUnit.trail;
							bool flag72 = flag71;
							bool flag73 = flag72;
							bool flag74 = flag73;
							bool flag75 = flag74;
							bool flag76 = flag75;
							bool flag77 = flag76;
							bool flag78 = flag77;
							if (flag78)
							{
								Vector3 vector3 = pts[pts.Count - 1] + b;
								float d3 = ResourcePackManager.PathHalfWidth(vector3, position, forward, perDepth, ortho) * 3f;
								Color c = pathOwnColor;
								c.a = Mathf.Clamp01(pathOwnColor.a * 1.15f);
								GL.Color(c);
								ResourcePackManager.PathQuad(vector3, right * d3, up * d3);
							}
						}
					}
					GL.End();
					GL.PopMatrix();
				}
				catch (Exception ex)
				{
					bool flag79 = !this.pathDrawWarned;
					bool flag80 = flag79;
					bool flag81 = flag80;
					bool flag82 = flag81;
					bool flag83 = flag82;
					bool flag84 = flag83;
					bool flag85 = flag84;
					bool flag86 = flag85;
					if (flag86)
					{
						this.pathDrawWarned = true;
						ResourcePackManager.PackWarn("[ResourcePacks] Path lines: draw failed: " + ex.Message);
					}
				}
			}
		}
	}

	private Color GetExtraColor(int id)
	{
		bool flag = id >= 40;
		Color result;
		if (flag)
		{
			result = BtcBlockMarks.GetColor(id);
		}
		else
		{
			ResourcePackManager.HpConfig hpConfig = this.hpCfg;
			bool flag2 = hpConfig == null;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			bool flag9 = flag8;
			bool flag10 = flag9;
			Color color;
			if (flag10)
			{
				color = Color.white;
			}
			else
			{
				switch (id)
				{
				case 1:
					color = hpConfig.hpNumColor;
					break;
				case 2:
					color = hpConfig.stamNumColor;
					break;
				case 3:
					color = hpConfig.pathOwnColor;
					break;
				default:
					switch (id)
					{
					case 20:
						color = hpConfig.hitOwnColor;
						break;
					case 21:
						color = hpConfig.hitEnemyColor;
						break;
					case 22:
						color = hpConfig.dpsColor;
						break;
					case 23:
						color = hpConfig.crystalUpColor;
						break;
					case 24:
						color = hpConfig.crystalDownColor;
						break;
					case 25:
						color = hpConfig.regenNumColor;
						break;
					case 26:
						color = hpConfig.hitStamColor;
						break;
					case 27:
						color = hpConfig.centerOwnColor;
						break;
					case 28:
						color = hpConfig.centerEnemyColor;
						break;
					default:
						color = hpConfig.bombColors[Mathf.Clamp(id - 4, 0, 7)];
						break;
					}
					break;
				}
			}
			result = color;
		}
		return result;
	}

	private void SetExtraColor(int id, Color col)
	{
		bool flag = id >= 40;
		if (flag)
		{
			BtcBlockMarks.SetColor(id, col);
		}
		else
		{
			ResourcePackManager.HpConfig hpConfig = this.hpCfg;
			bool flag2 = hpConfig == null;
			bool flag3 = !flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			bool flag9 = flag8;
			bool flag10 = flag9;
			if (flag10)
			{
				col.a = this.GetExtraColor(id).a;
				switch (id)
				{
				case 1:
					hpConfig.hpNumColor = col;
					break;
				case 2:
					hpConfig.stamNumColor = col;
					break;
				case 3:
					hpConfig.pathOwnColor = col;
					break;
				default:
					switch (id)
					{
					case 20:
						col.a = 1f;
						hpConfig.hitOwnColor = col;
						break;
					case 21:
						col.a = 1f;
						hpConfig.hitEnemyColor = col;
						break;
					case 22:
						col.a = 1f;
						hpConfig.dpsColor = col;
						break;
					case 23:
						col.a = 1f;
						hpConfig.crystalUpColor = col;
						break;
					case 24:
						col.a = 1f;
						hpConfig.crystalDownColor = col;
						break;
					case 25:
						hpConfig.regenNumColor = col;
						break;
					case 26:
						col.a = 1f;
						hpConfig.hitStamColor = col;
						break;
					case 27:
						col.a = 1f;
						hpConfig.centerOwnColor = col;
						break;
					case 28:
						col.a = 1f;
						hpConfig.centerEnemyColor = col;
						break;
					default:
						col.a = 1f;
						hpConfig.bombColors[Mathf.Clamp(id - 4, 0, 7)] = col;
						break;
					}
					break;
				}
				this.MarkHpDirty();
			}
		}
	}

	private void TogglePickerExtra(int id)
	{
		bool flag = this.pickExtra == id;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			this.pickExtra = 0;
		}
		else
		{
			this.pickExtra = id;
			this.pickNum = false;
			this.pickStop = null;
			this.pickOv = null;
			Color.RGBToHSV(this.PickedColor(), out this.pickH, out this.pickS, out this.pickV);
			this.svTexHue = -1f;
		}
	}

	private void ColorRow(Event e, ref float y, float w, float rowH, int fs, Color old, string label, int id)
	{
		label = BtcLang.Tr(label);
		Color extraColor = this.GetExtraColor(id);
		Rect row = new Rect(0f, y, w, rowH);
		this.DrawRowBackground(e, row, false, old);
		GUI.Label(new Rect((float)fs, y, w * 0.42f, rowH), label, this.menuLabelStyle);
		float num = (float)fs * 1.5f;
		Rect rect = new Rect(w * 0.47f, y + (rowH - num * 0.75f) * 0.5f, num, num * 0.75f);
		this.SwatchBox(rect, extraColor, this.pickExtra == id);
		bool flag = GUI.Button(rect, GUIContent.none, GUIStyle.none);
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			this.TogglePickerExtra(id);
		}
		GUI.Label(new Rect(rect.xMax + (float)fs * 0.6f, y, (float)fs * 6f, rowH), ResourcePackManager.HpHex(extraColor), this.menuLabelStyle);
		y += rowH;
		bool flag9 = this.pickExtra == id;
		bool flag10 = flag9;
		bool flag11 = flag10;
		bool flag12 = flag11;
		bool flag13 = flag12;
		bool flag14 = flag13;
		bool flag15 = flag14;
		bool flag16 = flag15;
		if (flag16)
		{
			y = this.DrawPicker(e, y, w, fs, old);
		}
	}

	private void AddPathUnits(UIGameObserver.Bricktron observer, ResourcePackManager.HpConfig cfg)
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
				bool flag9 = this.pathUnits.Count >= 64;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					break;
				}
				bool flag17 = labor == null || !labor.gameObject.activeInHierarchy || !this.pathSeen.Add(labor);
				bool flag18 = !flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				if (flag24)
				{
					bool flag25 = cfg.pathSelectedOnly && !this.pathSelected.Contains(labor);
					bool flag26 = !flag25;
					bool flag27 = flag26;
					bool flag28 = flag27;
					bool flag29 = flag28;
					bool flag30 = flag29;
					bool flag31 = flag30;
					bool flag32 = flag31;
					if (flag32)
					{
						BricktronDamageReceiver bricktronDamageReceiver = labor.damageReceiver as BricktronDamageReceiver;
						bool flag33 = bricktronDamageReceiver != null && bricktronDamageReceiver.CurrentHP <= 0f;
						bool flag34 = !flag33;
						bool flag35 = flag34;
						bool flag36 = flag35;
						bool flag37 = flag36;
						bool flag38 = flag37;
						bool flag39 = flag38;
						bool flag40 = flag39;
						if (flag40)
						{
							this.BuildPath(labor, cfg);
						}
					}
				}
			}
		}
	}

	private ResourcePackManager.PathUnit NewPathUnit()
	{
		bool flag = this.pathUnits.Count < this.pathPool.Count;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		ResourcePackManager.PathUnit pathUnit;
		if (flag8)
		{
			pathUnit = this.pathPool[this.pathUnits.Count];
		}
		else
		{
			pathUnit = new ResourcePackManager.PathUnit();
			this.pathPool.Add(pathUnit);
		}
		pathUnit.trail = false;
		return pathUnit;
	}

	private void BuildPath(Labor labor, ResourcePackManager.HpConfig cfg)
	{
		bool flag = false;
		try
		{
			Navigation navigation = labor.navigation;
			Locomotion4 locomotion = (navigation != null) ? navigation.Locomotion : null;
			DestinationIterator destinationIterator = (locomotion != null) ? locomotion.PathIterator : null;
			bool flag2 = destinationIterator != null;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			bool flag9 = flag8;
			if (flag9)
			{
				this.pathTmp.Clear();
				this.pathTmp.Add(labor.transform.position);
				int num = Mathf.Max(0, destinationIterator.CurrentIndex);
				for (int i = num; i < num + 400; i++)
				{
					WalkNode nth = destinationIterator.GetNth(i);
					bool flag10 = nth == null;
					bool flag11 = flag10;
					bool flag12 = flag11;
					bool flag13 = flag12;
					bool flag14 = flag13;
					bool flag15 = flag14;
					bool flag16 = flag15;
					bool flag17 = flag16;
					if (flag17)
					{
						break;
					}
					this.pathTmp.Add(nth.Position);
				}
				bool flag18 = this.pathTmp.Count >= 2;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				bool flag25 = flag24;
				if (flag25)
				{
					ResourcePackManager.PathUnit pathUnit = this.NewPathUnit();
					pathUnit.labor = labor;
					pathUnit.pts.Clear();
					pathUnit.pts.Add(this.pathTmp[0]);
					for (int j = 1; j < this.pathTmp.Count - 1; j++)
					{
						Vector3 vector = this.pathTmp[j] - pathUnit.pts[pathUnit.pts.Count - 1];
						Vector3 vector2 = this.pathTmp[j + 1] - this.pathTmp[j];
						bool flag26 = vector.sqrMagnitude < 1E-06f;
						bool flag27 = !flag26;
						bool flag28 = flag27;
						bool flag29 = flag28;
						bool flag30 = flag29;
						bool flag31 = flag30;
						bool flag32 = flag31;
						bool flag33 = flag32;
						if (flag33)
						{
							bool flag34 = Vector3.Dot(vector.normalized, vector2.normalized) > 0.999f;
							bool flag35 = !flag34;
							bool flag36 = flag35;
							bool flag37 = flag36;
							bool flag38 = flag37;
							bool flag39 = flag38;
							bool flag40 = flag39;
							bool flag41 = flag40;
							if (flag41)
							{
								pathUnit.pts.Add(this.pathTmp[j]);
							}
						}
					}
					pathUnit.pts.Add(this.pathTmp[this.pathTmp.Count - 1]);
					this.pathUnits.Add(pathUnit);
					BtcOverlays.AddPath(pathUnit.pts);
					flag = true;
				}
			}
		}
		catch (Exception)
		{
		}
		bool flag42 = flag || NetworkServer.active;
		bool flag43 = !flag42;
		bool flag44 = flag43;
		bool flag45 = flag44;
		bool flag46 = flag45;
		bool flag47 = flag46;
		bool flag48 = flag47;
		bool flag49 = flag48;
		if (flag49)
		{
			bool flag50 = cfg.pathPredict && this.TryAddPredicted(labor);
			bool flag51 = !flag50;
			bool flag52 = flag51;
			bool flag53 = flag52;
			bool flag54 = flag53;
			bool flag55 = flag54;
			bool flag56 = flag55;
			bool flag57 = flag56;
			if (flag57)
			{
				bool flag58 = !cfg.pathTrail;
				bool flag59 = !flag58;
				bool flag60 = flag59;
				bool flag61 = flag60;
				bool flag62 = flag61;
				bool flag63 = flag62;
				bool flag64 = flag63;
				bool flag65 = flag64;
				if (flag65)
				{
					this.AddTrail(labor);
				}
			}
		}
	}

	private void AddTrail(Labor labor)
	{
		Vector3 position = labor.transform.position;
		float unscaledTime = Time.unscaledTime;
		ResourcePackManager.TrailData trailData;
		bool flag = !this.trails.TryGetValue(labor, out trailData);
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			trailData = new ResourcePackManager.TrailData();
			trailData.lastMove = unscaledTime;
			trailData.pts.Add(position);
			this.trails[labor] = trailData;
		}
		trailData.scanId = this.pathScanId;
		Vector3 b = trailData.pts[trailData.pts.Count - 1];
		bool flag9 = (position - b).sqrMagnitude >= 0.0625f;
		bool flag10 = flag9;
		bool flag11 = flag10;
		bool flag12 = flag11;
		bool flag13 = flag12;
		bool flag14 = flag13;
		bool flag15 = flag14;
		bool flag16 = flag15;
		if (flag16)
		{
			trailData.pts.Add(position);
			trailData.lastMove = unscaledTime;
			bool flag17 = trailData.pts.Count > 60;
			bool flag18 = flag17;
			bool flag19 = flag18;
			bool flag20 = flag19;
			bool flag21 = flag20;
			bool flag22 = flag21;
			bool flag23 = flag22;
			bool flag24 = flag23;
			if (flag24)
			{
				trailData.pts.RemoveAt(0);
			}
		}
		else
		{
			bool flag25 = unscaledTime - trailData.lastMove > 2f && trailData.pts.Count > 1;
			bool flag26 = flag25;
			bool flag27 = flag26;
			bool flag28 = flag27;
			bool flag29 = flag28;
			bool flag30 = flag29;
			bool flag31 = flag30;
			bool flag32 = flag31;
			if (flag32)
			{
				trailData.pts.Clear();
				trailData.pts.Add(position);
			}
		}
		bool flag33 = trailData.pts.Count < 2;
		bool flag34 = !flag33;
		bool flag35 = flag34;
		bool flag36 = flag35;
		bool flag37 = flag36;
		bool flag38 = flag37;
		bool flag39 = flag38;
		bool flag40 = flag39;
		if (flag40)
		{
			ResourcePackManager.PathUnit pathUnit = this.NewPathUnit();
			pathUnit.labor = labor;
			pathUnit.trail = true;
			pathUnit.pts.Clear();
			pathUnit.pts.AddRange(trailData.pts);
			pathUnit.pts.Add(position);
			this.pathUnits.Add(pathUnit);
		}
	}

	private static bool PredReflection()
	{
		bool flag = ResourcePackManager.predReflTried;
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
			result = ResourcePackManager.predReflOk;
		}
		else
		{
			ResourcePackManager.predReflTried = true;
			try
			{
				BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
				Type typeFromHandle = typeof(SearchPathRequest);
				ResourcePackManager.mPredInitOrigin = typeFromHandle.GetMethod("InitOrginAndDestination", bindingAttr);
				ResourcePackManager.mPredInitSearch = typeFromHandle.GetMethod("InitSearch", bindingAttr);
				ResourcePackManager.mPredWork = typeFromHandle.GetMethod("Work", bindingAttr);
				ResourcePackManager.mPredInitImprove = typeFromHandle.GetMethod("InitImprove", bindingAttr);
				ResourcePackManager.mPredLoopImprove = typeFromHandle.GetMethod("LoopImprovePath", bindingAttr);
				ResourcePackManager.mPredWrapUp = typeFromHandle.GetMethod("WrapUpSearch", bindingAttr);
				ResourcePackManager.predReflOk = (ResourcePackManager.mPredInitOrigin != null && ResourcePackManager.mPredInitSearch != null && ResourcePackManager.mPredWork != null && ResourcePackManager.mPredInitImprove != null && ResourcePackManager.mPredLoopImprove != null && ResourcePackManager.mPredWrapUp != null);
			}
			catch (Exception)
			{
				ResourcePackManager.predReflOk = false;
			}
			bool flag9 = !ResourcePackManager.predReflOk;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				ResourcePackManager.PackWarn("[ResourcePacks] Path prediction: SearchPathRequest methods not found, using movement trail only");
			}
			result = ResourcePackManager.predReflOk;
		}
		return result;
	}

	private void ClearPredictions()
	{
		for (int i = 0; i < this.predQueue.Count; i++)
		{
			this.DisposePred(this.predQueue[i]);
		}
		this.predQueue.Clear();
		this.predRoutes.Clear();
	}

	private void DisposePred(ResourcePackManager.PredReq r)
	{
		bool flag = r == null || r.req == null;
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
				bool flag9 = r.req.Search != null;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					r.req.Search.Clear();
				}
				r.req.Dispose();
			}
			catch (Exception)
			{
			}
			r.req = null;
		}
	}

	private void DropDeadPredictions()
	{
		bool flag = this.predRoutes.Count == 0;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			this.predDrop.Clear();
			foreach (KeyValuePair<Labor, ResourcePackManager.PredRoute> keyValuePair in this.predRoutes)
			{
				bool flag9 = keyValuePair.Key == null || !keyValuePair.Key.gameObject.activeInHierarchy;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					this.predDrop.Add(keyValuePair.Key);
				}
			}
			for (int i = 0; i < this.predDrop.Count; i++)
			{
				this.predRoutes.Remove(this.predDrop[i]);
			}
		}
	}

	private void PollClientOrders()
	{
		ResourcePackManager.HpConfig hpConfig = this.hpCfg;
		bool flag = hpConfig == null || !hpConfig.pathOwn || !hpConfig.pathPredict || NetworkServer.active;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			bool flag9 = !ResourcePackManager.pollReflTried;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				ResourcePackManager.pollReflTried = true;
				try
				{
					BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
					FieldInfo field = typeof(PickerComponent).GetField("OrderGiver", bindingAttr);
					Type typeFromHandle = typeof(PickerOrderGiver);
					bool flag17 = field != null;
					bool flag18 = flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					bool flag21 = flag20;
					bool flag22 = flag21;
					bool flag23 = flag22;
					bool flag24 = flag23;
					if (flag24)
					{
						ResourcePackManager.pollGiver = field.GetValue(null);
					}
					ResourcePackManager.fPollActors = typeFromHandle.GetField("_actors", bindingAttr);
					ResourcePackManager.fPollPos = typeFromHandle.GetField("_targetPosition", bindingAttr);
					ResourcePackManager.fPollObj = typeFromHandle.GetField("_targetObject", bindingAttr);
					ResourcePackManager.fPollType = typeFromHandle.GetField("_actionType", bindingAttr);
					ResourcePackManager.fPollTime = typeFromHandle.GetField("_timestamp", bindingAttr);
				}
				catch (Exception)
				{
					ResourcePackManager.pollGiver = null;
				}
				bool flag25 = ResourcePackManager.pollGiver == null || ResourcePackManager.fPollActors == null || ResourcePackManager.fPollPos == null || ResourcePackManager.fPollObj == null || ResourcePackManager.fPollType == null || ResourcePackManager.fPollTime == null;
				bool flag26 = flag25;
				bool flag27 = flag26;
				bool flag28 = flag27;
				bool flag29 = flag28;
				bool flag30 = flag29;
				bool flag31 = flag30;
				bool flag32 = flag31;
				if (flag32)
				{
					ResourcePackManager.pollGiver = null;
					this.PredNote("cannot read PickerOrderGiver (giver/fields not found)");
					ResourcePackManager.PackWarn("[ResourcePacks] Path prediction: PickerOrderGiver fields not found, prediction disabled");
				}
			}
			bool flag33 = ResourcePackManager.pollGiver == null;
			bool flag34 = !flag33;
			bool flag35 = flag34;
			bool flag36 = flag35;
			bool flag37 = flag36;
			bool flag38 = flag37;
			bool flag39 = flag38;
			bool flag40 = flag39;
			if (flag40)
			{
				try
				{
					float num = (float)ResourcePackManager.fPollTime.GetValue(ResourcePackManager.pollGiver);
					bool flag41 = num <= 0f || num == this.pollLastStamp;
					bool flag42 = !flag41;
					bool flag43 = flag42;
					bool flag44 = flag43;
					bool flag45 = flag44;
					bool flag46 = flag45;
					bool flag47 = flag46;
					bool flag48 = flag47;
					if (flag48)
					{
						this.pollLastStamp = num;
						List<GameObject> list = (List<GameObject>)ResourcePackManager.fPollActors.GetValue(ResourcePackManager.pollGiver);
						bool flag49 = list == null || list.Count == 0;
						bool flag50 = !flag49;
						bool flag51 = flag50;
						bool flag52 = flag51;
						bool flag53 = flag52;
						bool flag54 = flag53;
						bool flag55 = flag54;
						bool flag56 = flag55;
						if (flag56)
						{
							this.OnClientOrder(new List<GameObject>(list), (Vector3)ResourcePackManager.fPollPos.GetValue(ResourcePackManager.pollGiver), (GameObject)ResourcePackManager.fPollObj.GetValue(ResourcePackManager.pollGiver), (ActionType)ResourcePackManager.fPollType.GetValue(ResourcePackManager.pollGiver));
						}
					}
				}
				catch (Exception)
				{
				}
			}
		}
	}

	private void OnClientOrder(List<GameObject> actors, Vector3 target, GameObject targetObject, ActionType type)
	{
		ResourcePackManager.HpConfig hpConfig = this.hpCfg;
		bool flag = hpConfig == null || !hpConfig.pathOwn || !hpConfig.pathPredict || NetworkServer.active;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			bool flag9 = actors == null || actors.Count == 0;
			bool flag10 = !flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				ContextualActionGroupData contextualActionGroupData = null;
				try
				{
					bool flag17 = PickingUtility.isInQueueMode();
					bool flag18 = flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					bool flag21 = flag20;
					bool flag22 = flag21;
					bool flag23 = flag22;
					bool flag24 = flag23;
					if (flag24)
					{
						this.PredNote("skipped: queue mode (Shift)");
					}
					else
					{
						bool flag25 = targetObject == null;
						this.PredNote(string.Concat(new object[]
						{
							"order seen: ",
							actors.Count,
							" unit(s), ",
							type,
							flag25 ? " -> point" : " -> object"
						}));
						XYZ xyz = XYZ.FromVector3(target);
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
							bool flag34 = type == ActionType.Secondary;
							bool flag35 = flag34;
							bool flag36 = flag35;
							bool flag37 = flag36;
							bool flag38 = flag37;
							bool flag39 = flag38;
							bool flag40 = flag39;
							bool flag41 = flag40;
							if (flag41)
							{
								this.PredNote("skipped: secondary action (not a move order)");
								return;
							}
							bool flag42 = TreeRegistry.Instance.HasTree(xyz) || NatureRegistry.HasNatureAt(xyz);
							bool flag43 = flag42;
							bool flag44 = flag43;
							bool flag45 = flag44;
							bool flag46 = flag45;
							bool flag47 = flag46;
							bool flag48 = flag47;
							bool flag49 = flag48;
							if (flag49)
							{
								this.PredNote("skipped: click on tree/nature");
								return;
							}
							contextualActionGroupData = new ContextualActionGroupData();
							contextualActionGroupData.Init(new List<GameObject>(actors));
						}
						for (int i = 0; i < actors.Count; i++)
						{
							GameObject gameObject = actors[i];
							bool flag50 = gameObject == null;
							bool flag51 = !flag50;
							bool flag52 = flag51;
							bool flag53 = flag52;
							bool flag54 = flag53;
							bool flag55 = flag54;
							bool flag56 = flag55;
							bool flag57 = flag56;
							if (flag57)
							{
								Labor component = gameObject.GetComponent<Labor>();
								bool flag58 = component == null;
								bool flag59 = !flag58;
								bool flag60 = flag59;
								bool flag61 = flag60;
								bool flag62 = flag61;
								bool flag63 = flag62;
								bool flag64 = flag63;
								bool flag65 = flag64;
								if (flag65)
								{
									bool flag66 = flag25;
									bool flag67 = flag66;
									bool flag68 = flag67;
									bool flag69 = flag68;
									bool flag70 = flag69;
									bool flag71 = flag70;
									bool flag72 = flag71;
									bool flag73 = flag72;
									Vector3 dest;
									if (flag73)
									{
										try
										{
											dest = contextualActionGroupData.GetFormationDestination(xyz.ToVector3(), gameObject);
										}
										catch (Exception)
										{
											dest = xyz.ToVector3();
										}
									}
									else
									{
										dest = targetObject.transform.position;
									}
									this.EnqueuePrediction(component, dest, flag25);
								}
							}
						}
					}
				}
				catch (Exception ex)
				{
					this.PredNote("order failed: " + ex.GetType().Name + ": " + ex.Message);
					bool flag74 = !this.predWarned;
					bool flag75 = flag74;
					bool flag76 = flag75;
					bool flag77 = flag76;
					bool flag78 = flag77;
					bool flag79 = flag78;
					bool flag80 = flag79;
					bool flag81 = flag80;
					if (flag81)
					{
						this.predWarned = true;
						ResourcePackManager.PackWarn("[ResourcePacks] Path prediction: order failed: " + ex.Message);
					}
				}
				finally
				{
					bool flag82 = contextualActionGroupData != null;
					bool flag83 = flag82;
					bool flag84 = flag83;
					bool flag85 = flag84;
					bool flag86 = flag85;
					bool flag87 = flag86;
					bool flag88 = flag87;
					bool flag89 = flag88;
					if (flag89)
					{
						try
						{
							contextualActionGroupData.ResetObject();
						}
						catch (Exception)
						{
						}
					}
				}
			}
		}
	}

	private void EnqueuePrediction(Labor labor, Vector3 dest, bool exact)
	{
		for (int i = this.predQueue.Count - 1; i >= 0; i--)
		{
			bool flag = this.predQueue[i].labor == labor;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				this.DisposePred(this.predQueue[i]);
				this.predQueue.RemoveAt(i);
			}
		}
		this.predRoutes.Remove(labor);
		bool flag9 = this.predQueue.Count >= 48;
		bool flag10 = !flag9;
		bool flag11 = flag10;
		bool flag12 = flag11;
		bool flag13 = flag12;
		bool flag14 = flag13;
		bool flag15 = flag14;
		bool flag16 = flag15;
		if (flag16)
		{
			ResourcePackManager.PredReq predReq = new ResourcePackManager.PredReq();
			predReq.labor = labor;
			predReq.dest = dest;
			predReq.exact = exact;
			predReq.created = Time.unscaledTime;
			this.predQueue.Add(predReq);
		}
	}

	private void ProcessPredictions()
	{
		bool flag = this.predQueue.Count == 0;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			ResourcePackManager.HpConfig hpConfig = this.hpCfg;
			bool flag9 = hpConfig == null || !hpConfig.pathOwn || !hpConfig.pathPredict || NetworkServer.active || !ResourcePackManager.PredReflection();
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				this.ClearPredictions();
			}
			else
			{
				int num = 0;
				int num2 = 0;
				while (num < 4 && this.predQueue.Count > 0)
				{
					bool flag17 = num2 >= this.predQueue.Count;
					bool flag18 = flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					bool flag21 = flag20;
					bool flag22 = flag21;
					bool flag23 = flag22;
					bool flag24 = flag23;
					if (flag24)
					{
						num2 = 0;
					}
					ResourcePackManager.PredReq r = this.predQueue[num2];
					bool flag25;
					try
					{
						flag25 = this.StepPrediction(r);
					}
					catch (Exception ex)
					{
						this.predWarned = true;
						Exception ex2 = (ex.InnerException != null) ? ex.InnerException : ex;
						this.PredNote("search failed: " + ex2.GetType().Name + ": " + ex2.Message);
						ResourcePackManager.PackWarn("[ResourcePacks] Path prediction failed: " + ex2.GetType().Name + ": " + ex2.Message);
						flag25 = true;
					}
					num++;
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
						this.DisposePred(r);
						this.predQueue.RemoveAt(num2);
					}
					else
					{
						num2++;
					}
				}
			}
		}
	}

	private bool StepPrediction(ResourcePackManager.PredReq r)
	{
		Labor labor = r.labor;
		bool flag = labor == null || !labor.gameObject.activeInHierarchy || Time.unscaledTime - r.created > 20f;
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
			this.PredNote("unit gone or search timed out");
			result = true;
		}
		else
		{
			bool flag9 = r.stage == 0;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				Navigation navigation = labor.navigation;
				TestPathfinding testPathfinding = TestPathfinding.Instance;
				bool flag17 = navigation == null || navigation.Presence == null || navigation.Interaction == null || testPathfinding == null || !testPathfinding.Initialized;
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				if (flag24)
				{
					this.PredNote(string.Concat(new string[]
					{
						"client cannot search: nav=",
						(navigation != null).ToString(),
						" presence=",
						(navigation != null && navigation.Presence != null).ToString(),
						" interaction=",
						(navigation != null && navigation.Interaction != null).ToString(),
						" pathfinding=",
						(testPathfinding != null).ToString(),
						" initialized=",
						(testPathfinding != null && testPathfinding.Initialized).ToString()
					}));
					result = true;
				}
				else
				{
					SearchPathRequest searchPathRequest = SearchPathRequest.Make();
					r.req = searchPathRequest;
					searchPathRequest.InitNavigation(navigation);
					searchPathRequest.From = navigation.Presence.CurrentVoxel;
					searchPathRequest.Location = Location.Of(r.dest);
					searchPathRequest.Exact = r.exact;
					searchPathRequest.Precise = true;
					searchPathRequest.PresenceTest = navigation.Presence.BuildPresenceTest(labor.Profession.CanPassThrough(), false);
					searchPathRequest.CanClimb = navigation.Interaction.CanClimb();
					searchPathRequest.CanWalkInForest = labor.Profession.CanWalkInForest();
					searchPathRequest.CanBreakVoxels = labor.Profession.CanMakeWay();
					searchPathRequest.SearchAway = false;
					searchPathRequest.SearchAwayDistance = 15;
					searchPathRequest.LodCount = searchPathRequest.Pathfinding.lodCount;
					searchPathRequest.SearchLimit = -1;
					searchPathRequest.SearchPerUpdate = 150;
					bool flag25 = !(bool)ResourcePackManager.mPredInitOrigin.Invoke(searchPathRequest, null);
					bool flag26 = flag25;
					bool flag27 = flag26;
					bool flag28 = flag27;
					bool flag29 = flag28;
					bool flag30 = flag29;
					bool flag31 = flag30;
					bool flag32 = flag31;
					if (flag32)
					{
						result = this.PredRetryOrFail(r);
					}
					else
					{
						ResourcePackManager.mPredInitSearch.Invoke(searchPathRequest, null);
						r.stage = 1;
						result = false;
					}
				}
			}
			else
			{
				bool flag33 = r.stage == 1;
				bool flag34 = flag33;
				bool flag35 = flag34;
				bool flag36 = flag35;
				bool flag37 = flag36;
				bool flag38 = flag37;
				bool flag39 = flag38;
				bool flag40 = flag39;
				if (flag40)
				{
					bool flag41 = (bool)ResourcePackManager.mPredWork.Invoke(r.req, new object[]
					{
						150
					});
					r.calls++;
					bool flag42 = flag41 || r.calls > 600;
					bool flag43 = flag42;
					bool flag44 = flag43;
					bool flag45 = flag44;
					bool flag46 = flag45;
					bool flag47 = flag46;
					bool flag48 = flag47;
					bool flag49 = flag48;
					if (flag49)
					{
						ResourcePackManager.mPredInitImprove.Invoke(r.req, null);
						bool flag50 = r.req.SuccessLink == null;
						bool flag51 = flag50;
						bool flag52 = flag51;
						bool flag53 = flag52;
						bool flag54 = flag53;
						bool flag55 = flag54;
						bool flag56 = flag55;
						bool flag57 = flag56;
						if (flag57)
						{
							return this.PredRetryOrFail(r);
						}
						r.stage = 2;
						r.calls = 0;
					}
					result = false;
				}
				else
				{
					bool flag58 = r.stage == 2;
					bool flag59 = flag58;
					bool flag60 = flag59;
					bool flag61 = flag60;
					bool flag62 = flag61;
					bool flag63 = flag62;
					bool flag64 = flag63;
					bool flag65 = flag64;
					if (flag65)
					{
						bool flag66 = (bool)ResourcePackManager.mPredLoopImprove.Invoke(r.req, new object[]
						{
							150
						});
						r.calls++;
						bool flag67 = flag66 || r.calls > 600;
						bool flag68 = flag67;
						bool flag69 = flag68;
						bool flag70 = flag69;
						bool flag71 = flag70;
						bool flag72 = flag71;
						bool flag73 = flag72;
						bool flag74 = flag73;
						if (flag74)
						{
							r.stage = 3;
						}
						result = false;
					}
					else
					{
						bool flag75 = !(bool)ResourcePackManager.mPredWrapUp.Invoke(r.req, null) || r.req.ResultPath == null;
						bool flag76 = flag75;
						bool flag77 = flag76;
						bool flag78 = flag77;
						bool flag79 = flag78;
						bool flag80 = flag79;
						bool flag81 = flag80;
						bool flag82 = flag81;
						if (flag82)
						{
							result = this.PredRetryOrFail(r);
						}
						else
						{
							List<WalkNode> list = new List<WalkNode>();
							r.req.ResultPath.GetPathLocations(list);
							this.predTmp.Clear();
							this.predTmp.Add(labor.transform.position);
							for (int i = 0; i < list.Count; i++)
							{
								bool flag83 = list[i] != null;
								bool flag84 = flag83;
								bool flag85 = flag84;
								bool flag86 = flag85;
								bool flag87 = flag86;
								bool flag88 = flag87;
								bool flag89 = flag88;
								bool flag90 = flag89;
								if (flag90)
								{
									this.predTmp.Add(list[i].Position);
								}
							}
							bool flag91 = this.predTmp.Count < 2;
							bool flag92 = flag91;
							bool flag93 = flag92;
							bool flag94 = flag93;
							bool flag95 = flag94;
							bool flag96 = flag95;
							bool flag97 = flag96;
							bool flag98 = flag97;
							if (flag98)
							{
								this.PredNote("search finished but path is empty");
								result = true;
							}
							else
							{
								ResourcePackManager.PredRoute predRoute = new ResourcePackManager.PredRoute();
								ResourcePackManager.SimplifyPoints(this.predTmp, predRoute.pts);
								bool flag99 = predRoute.pts.Count < 2;
								bool flag100 = flag99;
								bool flag101 = flag100;
								bool flag102 = flag101;
								bool flag103 = flag102;
								bool flag104 = flag103;
								bool flag105 = flag104;
								bool flag106 = flag105;
								if (flag106)
								{
									result = true;
								}
								else
								{
									predRoute.created = Time.unscaledTime;
									predRoute.lastMove = predRoute.created;
									predRoute.lastPos = labor.transform.position;
									this.predRoutes[labor] = predRoute;
									this.PredNote("ok: route of " + predRoute.pts.Count + " points");
									result = true;
								}
							}
						}
					}
				}
			}
		}
		return result;
	}
}
}
