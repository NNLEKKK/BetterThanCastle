using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Brix.Game.AI;
using Brix.Game.Components;
using Brix.Utils;
using UnityEngine;

namespace BetterThanCastle
{
public static class BtcOverlays
{
	public static bool AnyOn
	{
		get
		{
			BtcOverlays.Load();
			bool flag = !BtcOverlays.master;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool result;
			if (flag3)
			{
				result = false;
			}
			else
			{
				for (int i = 0; i < 4; i++)
				{
					bool flag4 = BtcOverlays.on[i];
					bool flag5 = flag4;
					bool flag6 = flag5;
					bool flag7 = flag6;
					if (flag7)
					{
						return true;
					}
				}
				result = false;
			}
			return result;
		}
	}

	public static bool IsOn(int f)
	{
		BtcOverlays.Load();
		return BtcOverlays.master && BtcOverlays.on[f];
	}

	private static void Load()
	{
		bool flag = BtcOverlays.loaded;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			BtcOverlays.loaded = true;
			BtcPaths.Init();
			BtcOverlays.master = BtcOverlays.RB("master.on", BtcOverlays.master);
			for (int i = 0; i < 4; i++)
			{
				string str = BtcOverlays.Keys[i];
				BtcOverlays.on[i] = BtcOverlays.RB(str + ".on", BtcOverlays.on[i]);
				BtcOverlays.alt[i] = BtcOverlays.RB(str + ".alt", BtcOverlays.alt[i]);
				BtcOverlays.width[i] = BtcOverlays.RF(str + ".width", BtcOverlays.width[i]);
				BtcOverlays.alpha[i] = BtcOverlays.RF(str + ".alpha", BtcOverlays.alpha[i]);
				BtcOverlays.colorIdx[i] = Mathf.Clamp((int)BtcOverlays.RF(str + ".color", (float)BtcOverlays.colorIdx[i]), 0, BtcOverlays.Presets.Length - 1);
			}
			BtcOverlays.mageRadius = BtcOverlays.RF("mage.radius", BtcOverlays.mageRadius);
			BtcOverlays.runSpeed = BtcOverlays.RF("path.speed", BtcOverlays.runSpeed);
			BtcOverlays.climbPenalty = BtcOverlays.RF("path.climb", BtcOverlays.climbPenalty);
			BtcOverlays.stepDownPenalty = BtcOverlays.RF("path.stepdown", BtcOverlays.stepDownPenalty);
			BtcOverlays.landingLabel = BtcOverlays.RB("landing.label", BtcOverlays.landingLabel);
		}
	}

	private static bool RB(string k, bool d)
	{
		return BtcPaths.ReadKey("overlays.txt", k, d ? "1" : "0") != "0";
	}

	private static float RF(string k, float d)
	{
		float num;
		return float.TryParse(BtcPaths.ReadKey("overlays.txt", k, d.ToString(CultureInfo.InvariantCulture)), NumberStyles.Float, CultureInfo.InvariantCulture, out num) ? num : d;
	}

	private static void Save()
	{
		try
		{
			List<string> list = new List<string>();
			list.Add("# Better Than Castle: overlays. master.on=0 turns ALL overlays off; .on=0/1 disables a feature completely; .alt=1 shows it only while LeftAlt is held");
			list.Add("master.on=" + (BtcOverlays.master ? "1" : "0"));
			for (int i = 0; i < 4; i++)
			{
				string text = BtcOverlays.Keys[i];
				list.Add(text + ".on=" + (BtcOverlays.on[i] ? "1" : "0"));
				list.Add(text + ".alt=" + (BtcOverlays.alt[i] ? "1" : "0"));
				list.Add(text + ".width=" + BtcOverlays.F(BtcOverlays.width[i]));
				list.Add(text + ".alpha=" + BtcOverlays.F(BtcOverlays.alpha[i]));
				list.Add(text + ".color=" + BtcOverlays.colorIdx[i]);
			}
			list.Add("mage.radius=" + BtcOverlays.F(BtcOverlays.mageRadius));
			list.Add("path.speed=" + BtcOverlays.F(BtcOverlays.runSpeed));
			list.Add("path.climb=" + BtcOverlays.F(BtcOverlays.climbPenalty));
			list.Add("path.stepdown=" + BtcOverlays.F(BtcOverlays.stepDownPenalty));
			list.Add("landing.label=" + (BtcOverlays.landingLabel ? "1" : "0"));
			File.WriteAllLines(BtcPaths.Config("overlays.txt"), list.ToArray());
		}
		catch (Exception)
		{
		}
	}

	private static string F(float v)
	{
		return v.ToString("0.###", CultureInfo.InvariantCulture);
	}

	private static bool Visible(int f)
	{
		return BtcOverlays.master && BtcOverlays.on[f] && (!BtcOverlays.alt[f] || Input.GetKey(KeyCode.LeftAlt));
	}

	private static Color Col(int f)
	{
		Color result = BtcOverlays.Presets[BtcOverlays.colorIdx[f]];
		result.a = BtcOverlays.alpha[f];
		return result;
	}

	private static float Next(float cur, float[] vals)
	{
		for (int i = 0; i < vals.Length; i++)
		{
			bool flag = Mathf.Abs(vals[i] - cur) < 0.0005f;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			if (flag4)
			{
				return vals[(i + 1) % vals.Length];
			}
		}
		return vals[0];
	}

	private static void ClearAll()
	{
		BtcOverlays.mages.Clear();
		BtcOverlays.bombLines.Clear();
		BtcOverlays.smashLines.Clear();
		BtcOverlays.pathTimes.Clear();
	}

	public static void Tick()
	{
		BtcOverlays.Load();
		BtcWaypoints.Tick(BtcOverlays.master);
		BtcLineMove.Tick();
		bool flag = !BtcOverlays.AnyOn;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		if (flag5)
		{
			bool flag6 = BtcOverlays.mages.Count + BtcOverlays.bombLines.Count + BtcOverlays.smashLines.Count + BtcOverlays.pathTimes.Count > 0;
			bool flag7 = flag6;
			bool flag8 = flag7;
			bool flag9 = flag8;
			bool flag10 = flag9;
			if (flag10)
			{
				BtcOverlays.ClearAll();
			}
		}
		else
		{
			bool flag11 = !BtcOverlays.on[2] && BtcOverlays.pathTimes.Count > 0;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			if (flag15)
			{
				BtcOverlays.pathTimes.Clear();
			}
			float unscaledTime = Time.unscaledTime;
			bool flag16 = unscaledTime < BtcOverlays.nextScan;
			bool flag17 = !flag16;
			bool flag18 = flag17;
			bool flag19 = flag18;
			bool flag20 = flag19;
			if (flag20)
			{
				BtcOverlays.nextScan = unscaledTime + 0.3f;
				bool flag21 = BtcOverlays.Visible(0);
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				bool flag25 = flag24;
				if (flag25)
				{
					BtcOverlays.ScanMages();
				}
				else
				{
					BtcOverlays.mages.Clear();
				}
				bool flag26 = BtcOverlays.Visible(1);
				bool flag27 = flag26;
				bool flag28 = flag27;
				bool flag29 = flag28;
				bool flag30 = flag29;
				if (flag30)
				{
					BtcOverlays.ScanBombs();
				}
				else
				{
					BtcOverlays.bombLines.Clear();
				}
				bool flag31 = BtcOverlays.Visible(3);
				bool flag32 = flag31;
				bool flag33 = flag32;
				bool flag34 = flag33;
				bool flag35 = flag34;
				if (flag35)
				{
					BtcOverlays.ScanSmash();
				}
				else
				{
					BtcOverlays.smashLines.Clear();
				}
			}
		}
	}

	private static void ScanMages()
	{
		BtcOverlays.mages.Clear();
		ArtificerStaff[] array = UnityEngine.Object.FindObjectsOfType<ArtificerStaff>();
		FieldInfo field = typeof(ArtificerStaff).GetField("wielder", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		foreach (ArtificerStaff artificerStaff in array)
		{
			bool flag = artificerStaff == null || !artificerStaff.gameObject.activeInHierarchy;
			bool flag2 = !flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			if (flag4)
			{
				Labor labor = (field != null) ? (field.GetValue(artificerStaff) as Labor) : null;
				bool flag5 = labor == null;
				bool flag6 = flag5;
				bool flag7 = flag6;
				bool flag8 = flag7;
				if (flag8)
				{
					labor = artificerStaff.GetComponentInParent<Labor>();
				}
				bool flag9 = labor == null || !labor.gameObject.activeInHierarchy;
				bool flag10 = !flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				if (flag12)
				{
					try
					{
						bool flag13 = labor.Autonomy != null && labor.Autonomy.IsDefensive;
						bool flag14 = flag13;
						bool flag15 = flag14;
						bool flag16 = flag15;
						if (flag16)
						{
							goto IL_124;
						}
					}
					catch (Exception)
					{
					}
					BtcOverlays.mages.Add(labor.transform);
				}
			}
			IL_124:;
		}
	}

	private static void ScanBombs()
	{
		BtcOverlays.bombLines.Clear();
		KinematicProjectile[] array = UnityEngine.Object.FindObjectsOfType<KinematicProjectile>();
		bool flag = !BtcOverlays.acceptLooked;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			BtcOverlays.acceptLooked = true;
			BtcOverlays.acceptMi = typeof(KinematicProjectile).GetMethod("AcceptCollision", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		}
		int num = 0;
		int num2 = 0;
		while (num2 < array.Length && num < 8)
		{
			KinematicProjectile kinematicProjectile = array[num2];
			bool flag5 = kinematicProjectile == null || kinematicProjectile.Archetype != KinematicProjectile.AttackArchetype.AlchemistBomb || !kinematicProjectile.gameObject.activeInHierarchy;
			bool flag6 = !flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				Vector3 vector = kinematicProjectile.transform.position;
				Vector3 velocity;
				try
				{
					velocity = kinematicProjectile.GetVelocity();
				}
				catch (Exception)
				{
					goto IL_286;
				}
				bool flag9 = velocity.sqrMagnitude < 0.01f;
				bool flag10 = !flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				if (flag12)
				{
					BtcOverlays.Line line = new BtcOverlays.Line();
					line.pts.Add(vector);
					int num3 = 0;
					Vector3 vector2 = vector;
					for (int i = 0; i < 250; i++)
					{
						Vector3 vector3 = ProjectilePhysics.ComputeNextPosition(vector, ref velocity, 0.04f);
						Vector3 a = vector3 - vector;
						float magnitude = a.magnitude;
						RaycastHit raycastHit = default(RaycastHit);
						bool flag13 = magnitude > 1E-05f && BtcOverlays.FirstAccepted(kinematicProjectile, vector, a / magnitude, magnitude, out raycastHit);
						bool flag14 = flag13;
						bool flag15 = flag14;
						bool flag16 = flag15;
						if (flag16)
						{
							vector2 = raycastHit.point;
							line.pts.Add(vector2);
							bool flag17 = raycastHit.collider.GetComponentInParent<Labor>() != null;
							bool flag18 = flag17;
							bool flag19 = flag18;
							bool flag20 = flag19;
							if (flag20)
							{
								num3 = 4;
							}
							else
							{
								num3 = ((raycastHit.normal.y > 0.7f) ? 2 : 1);
							}
							break;
						}
						vector = vector3;
						line.pts.Add(vector);
						vector2 = vector;
						bool flag21 = vector.y <= -16f;
						bool flag22 = flag21;
						bool flag23 = flag22;
						bool flag24 = flag23;
						if (flag24)
						{
							num3 = 3;
							break;
						}
					}
					line.kind = num3;
					line.end = vector2;
					line.text = ((num3 == 1) ? "Hit block" : ((num3 == 2) ? "Floor" : ((num3 == 3) ? "Abyss" : ((num3 == 4) ? "Unit" : ""))));
					BtcOverlays.bombLines.Add(line);
					num++;
				}
			}
			IL_286:
			num2++;
		}
	}

	private static bool FirstAccepted(KinematicProjectile p, Vector3 origin, Vector3 dir, float dist, out RaycastHit result)
	{
		RaycastHit[] array = Physics.RaycastAll(origin, dir, dist, p.LayerCollisionMask, QueryTriggerInteraction.Ignore);
		result = default(RaycastHit);
		bool flag = array.Length == 0;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool result2;
		if (flag4)
		{
			result2 = false;
		}
		else
		{
			Array.Sort<RaycastHit>(array, (RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance));
			for (int i = 0; i < array.Length; i++)
			{
				bool flag5 = true;
				bool flag6 = BtcOverlays.acceptMi != null;
				bool flag7 = flag6;
				bool flag8 = flag7;
				bool flag9 = flag8;
				if (flag9)
				{
					try
					{
						flag5 = (bool)BtcOverlays.acceptMi.Invoke(p, new object[]
						{
							array[i]
						});
					}
					catch (Exception)
					{
						flag5 = true;
					}
				}
				bool flag10 = flag5;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				if (flag13)
				{
					result = array[i];
					return true;
				}
			}
			result2 = false;
		}
		return result2;
	}

	private static void ScanSmash()
	{
		BtcOverlays.smashLines.Clear();
		Labor[] array = UnityEngine.Object.FindObjectsOfType<Labor>();
		int num = 0;
		Vector3 gravity = Physics.gravity;
		int num2 = 0;
		while (num2 < array.Length && num < 24)
		{
			Labor labor = array[num2];
			bool flag = labor == null || !labor.gameObject.activeInHierarchy;
			bool flag2 = !flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			if (flag4)
			{
				Rigidbody rigidbody = null;
				try
				{
					Navigation navigation = labor.navigation;
					Locomotion4 locomotion = (navigation != null) ? navigation.Locomotion : null;
					rigidbody = ((locomotion != null) ? locomotion.FlailingColliderRigidbody : null);
				}
				catch (Exception)
				{
				}
				bool flag5 = rigidbody == null || rigidbody.isKinematic || !rigidbody.gameObject.activeInHierarchy;
				bool flag6 = !flag5;
				bool flag7 = flag6;
				bool flag8 = flag7;
				if (flag8)
				{
					Vector3 a = rigidbody.velocity;
					bool flag9 = a.sqrMagnitude < 1f;
					bool flag10 = !flag9;
					bool flag11 = flag10;
					bool flag12 = flag11;
					if (flag12)
					{
						Vector3 vector = rigidbody.position;
						float drag = rigidbody.drag;
						BtcOverlays.Line line = new BtcOverlays.Line();
						line.pts.Add(vector);
						int num3 = 0;
						Vector3 vector2 = vector;
						for (int i = 0; i < 150; i++)
						{
							a += gravity * 0.04f;
							a *= Mathf.Clamp01(1f - drag * 0.04f);
							Vector3 vector3 = vector + a * 0.04f;
							Vector3 a2 = vector3 - vector;
							float magnitude = a2.magnitude;
							RaycastHit raycastHit = default(RaycastHit);
							bool flag13 = magnitude > 1E-05f && BtcOverlays.SelfFilteredCast(labor.transform, vector, a2 / magnitude, magnitude, out raycastHit);
							bool flag14 = flag13;
							bool flag15 = flag14;
							bool flag16 = flag15;
							if (flag16)
							{
								vector2 = raycastHit.point;
								line.pts.Add(vector2);
								num3 = ((raycastHit.normal.y > 0.7f) ? 2 : 1);
								break;
							}
							vector = vector3;
							line.pts.Add(vector);
							vector2 = vector;
							bool flag17 = vector.y <= -16f;
							bool flag18 = flag17;
							bool flag19 = flag18;
							bool flag20 = flag19;
							if (flag20)
							{
								num3 = 3;
								break;
							}
						}
						line.kind = num3;
						line.end = vector2;
						line.text = ((num3 == 1) ? "Hit block" : ((num3 == 2) ? "Floor" : ((num3 == 3) ? "Abyss" : "")));
						BtcOverlays.smashLines.Add(line);
						num++;
					}
				}
			}
			num2++;
		}
	}

	private static bool SelfFilteredCast(Transform self, Vector3 origin, Vector3 dir, float dist, out RaycastHit result)
	{
		RaycastHit[] array = Physics.SphereCastAll(origin, 0.3f, dir, dist, -5, QueryTriggerInteraction.Ignore);
		result = default(RaycastHit);
		bool flag = array.Length == 0;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool result2;
		if (flag4)
		{
			result2 = false;
		}
		else
		{
			Array.Sort<RaycastHit>(array, (RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance));
			for (int i = 0; i < array.Length; i++)
			{
				bool flag5 = array[i].distance <= 0f;
				bool flag6 = !flag5;
				bool flag7 = flag6;
				bool flag8 = flag7;
				if (flag8)
				{
					bool flag9 = array[i].collider.transform.IsChildOf(self) || array[i].collider.GetComponentInParent<Labor>() != null;
					bool flag10 = !flag9;
					bool flag11 = flag10;
					bool flag12 = flag11;
					if (flag12)
					{
						result = array[i];
						return true;
					}
				}
			}
			result2 = false;
		}
		return result2;
	}

	public static void ClearPaths()
	{
		BtcOverlays.pathTimes.Clear();
	}

	public static void AddPath(List<Vector3> pts)
	{
		BtcOverlays.Load();
		bool flag = !BtcOverlays.Visible(2) || pts == null || pts.Count < 2;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			float num = 0f;
			float num2 = Mathf.Max(0.5f, BtcOverlays.runSpeed);
			for (int i = 0; i < pts.Count - 1; i++)
			{
				Vector3 vector = pts[i];
				Vector3 vector2 = pts[i + 1];
				float num3 = vector2.x - vector.x;
				float num4 = vector2.z - vector.z;
				num += Mathf.Sqrt(num3 * num3 + num4 * num4) / num2;
				float num5 = vector2.y - vector.y;
				bool flag5 = num5 > 0.5f;
				bool flag6 = flag5;
				bool flag7 = flag6;
				bool flag8 = flag7;
				if (flag8)
				{
					num += BtcOverlays.climbPenalty * Mathf.Round(num5);
				}
				else
				{
					bool flag9 = num5 < -0.5f;
					bool flag10 = flag9;
					bool flag11 = flag10;
					bool flag12 = flag11;
					if (flag12)
					{
						num += BtcOverlays.stepDownPenalty * Mathf.Round(-num5);
					}
				}
			}
			BtcOverlays.pathTimes.Add(new KeyValuePair<Vector3, float>(pts[pts.Count - 1], num));
		}
	}

	private static bool EnsureMat()
	{
		bool flag = BtcOverlays.mat != null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool result;
		if (flag4)
		{
			result = true;
		}
		else
		{
			Shader shader = Shader.Find("Hidden/Internal-Colored");
			bool flag5 = shader == null;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				shader = Shader.Find("Sprites/Default");
			}
			bool flag9 = shader == null;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			if (flag12)
			{
				result = false;
			}
			else
			{
				BtcOverlays.mat = new Material(shader);
				BtcOverlays.mat.hideFlags = HideFlags.HideAndDontSave;
				bool flag13 = BtcOverlays.mat.HasProperty("_Cull");
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					BtcOverlays.mat.SetInt("_Cull", 0);
				}
				bool flag17 = BtcOverlays.mat.HasProperty("_ZWrite");
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				if (flag20)
				{
					BtcOverlays.mat.SetInt("_ZWrite", 0);
				}
				bool flag21 = BtcOverlays.mat.HasProperty("_ZTest");
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				if (flag24)
				{
					BtcOverlays.mat.SetInt("_ZTest", 8);
				}
				bool flag25 = BtcOverlays.mat.HasProperty("_SrcBlend");
				bool flag26 = flag25;
				bool flag27 = flag26;
				bool flag28 = flag27;
				if (flag28)
				{
					BtcOverlays.mat.SetInt("_SrcBlend", 5);
				}
				bool flag29 = BtcOverlays.mat.HasProperty("_DstBlend");
				bool flag30 = flag29;
				bool flag31 = flag30;
				bool flag32 = flag31;
				if (flag32)
				{
					BtcOverlays.mat.SetInt("_DstBlend", 10);
				}
				result = true;
			}
		}
		return result;
	}

	public static void Render(Camera cam)
	{
		BtcWaypoints.Render(cam, BtcOverlays.master);
		BtcBlockMarks.Render(cam);
		bool flag = cam == null || !BtcOverlays.AnyOn;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		if (flag5)
		{
			bool flag6 = cam != Camera.main;
			bool flag7 = !flag6;
			bool flag8 = flag7;
			bool flag9 = flag8;
			bool flag10 = flag9;
			if (flag10)
			{
				bool flag11 = BtcOverlays.Visible(0) && BtcOverlays.mages.Count > 0;
				bool flag12 = BtcOverlays.Visible(1) && BtcOverlays.bombLines.Count > 0;
				bool flag13 = BtcOverlays.Visible(3) && BtcOverlays.smashLines.Count > 0;
				bool flag14 = !flag11 && !flag12 && !flag13;
				bool flag15 = !flag14;
				bool flag16 = flag15;
				bool flag17 = flag16;
				bool flag18 = flag17;
				if (flag18)
				{
					bool flag19 = !BtcOverlays.EnsureMat();
					bool flag20 = !flag19;
					bool flag21 = flag20;
					bool flag22 = flag21;
					bool flag23 = flag22;
					if (flag23)
					{
						BtcOverlays.mat.SetPass(0);
						GL.PushMatrix();
						GL.Begin(7);
						Vector3 position = cam.transform.position;
						bool flag24 = flag11;
						bool flag25 = flag24;
						bool flag26 = flag25;
						bool flag27 = flag26;
						bool flag28 = flag27;
						if (flag28)
						{
							GL.Color(BtcOverlays.Col(0));
							for (int i = 0; i < BtcOverlays.mages.Count; i++)
							{
								bool flag29 = BtcOverlays.mages[i] == null;
								bool flag30 = !flag29;
								bool flag31 = flag30;
								bool flag32 = flag31;
								bool flag33 = flag32;
								if (flag33)
								{
									BtcOverlays.BuildCircle(BtcOverlays.mages[i].position + Vector3.up * 0.08f, BtcOverlays.mageRadius, 56);
									BtcOverlays.Ribbon(BtcOverlays.circle, position, BtcOverlays.width[0], true);
								}
							}
						}
						bool flag34 = flag12;
						bool flag35 = flag34;
						bool flag36 = flag35;
						bool flag37 = flag36;
						bool flag38 = flag37;
						if (flag38)
						{
							for (int j = 0; j < BtcOverlays.bombLines.Count; j++)
							{
								GL.Color(BtcOverlays.Col(1));
								BtcOverlays.Ribbon(BtcOverlays.bombLines[j].pts, position, BtcOverlays.width[1], false);
								BtcOverlays.Marker(BtcOverlays.bombLines[j].end, position, 0.35f, BtcOverlays.EndColor(BtcOverlays.bombLines[j].kind, 1));
							}
						}
						bool flag39 = flag13;
						bool flag40 = flag39;
						bool flag41 = flag40;
						bool flag42 = flag41;
						bool flag43 = flag42;
						if (flag43)
						{
							for (int k = 0; k < BtcOverlays.smashLines.Count; k++)
							{
								GL.Color(BtcOverlays.Col(3));
								BtcOverlays.Ribbon(BtcOverlays.smashLines[k].pts, position, BtcOverlays.width[3], false);
								BtcOverlays.Marker(BtcOverlays.smashLines[k].end, position, 0.35f, BtcOverlays.EndColor(BtcOverlays.smashLines[k].kind, 3));
							}
						}
						GL.End();
						GL.PopMatrix();
					}
				}
			}
		}
	}

	private static Color EndColor(int kind, int f)
	{
		Color result = (kind == 3) ? new Color(0.7f, 0.2f, 1f) : ((kind == 2) ? new Color(0.3f, 1f, 0.3f) : ((kind == 4) ? new Color(1f, 0.2f, 0.2f) : new Color(1f, 0.85f, 0.2f)));
		result.a = Mathf.Max(0.6f, BtcOverlays.alpha[f]);
		return result;
	}

	private static void BuildCircle(Vector3 center, float r, int seg)
	{
		BtcOverlays.circle.Clear();
		for (int i = 0; i <= seg; i++)
		{
			float f = (float)i / (float)seg * 6.2831855f;
			BtcOverlays.circle.Add(center + new Vector3(Mathf.Cos(f) * r, 0f, Mathf.Sin(f) * r));
		}
	}

	private static void Ribbon(List<Vector3> pts, Vector3 camPos, float w, bool flat)
	{
		float d = w * 0.5f;
		for (int i = 0; i < pts.Count - 1; i++)
		{
			Vector3 vector = pts[i];
			Vector3 vector2 = pts[i + 1];
			Vector3 vector3 = flat ? Vector3.Cross(vector2 - vector, Vector3.up) : Vector3.Cross(vector2 - vector, (vector + vector2) * 0.5f - camPos);
			float magnitude = vector3.magnitude;
			bool flag = magnitude < 1E-06f;
			bool flag2 = !flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			if (flag4)
			{
				vector3 = vector3 / magnitude * d;
				GL.Vertex(vector - vector3);
				GL.Vertex(vector + vector3);
				GL.Vertex(vector2 + vector3);
				GL.Vertex(vector2 - vector3);
			}
		}
	}

	private static void Marker(Vector3 p, Vector3 camPos, float size, Color c)
	{
		Vector3 normalized = (camPos - p).normalized;
		Vector3 vector = Vector3.Cross(Vector3.up, normalized);
		bool flag = vector.sqrMagnitude < 1E-06f;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			vector = Vector3.right;
		}
		vector = vector.normalized * size;
		Vector3 b = Vector3.Cross(normalized, vector).normalized * size;
		GL.Color(c);
		GL.Vertex(p - vector);
		GL.Vertex(p + b);
		GL.Vertex(p + vector);
		GL.Vertex(p - b);
	}

	public static void OnGUI()
	{
		BtcWaypoints.OnGUI(BtcOverlays.master);
		bool flag = !BtcOverlays.AnyOn || Event.current.type != EventType.Repaint;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			Camera main = Camera.main;
			bool flag5 = main == null;
			bool flag6 = !flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				bool flag9 = BtcOverlays.labelStyle == null;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				if (flag12)
				{
					BtcOverlays.labelStyle = new GUIStyle(GUI.skin.label);
					BtcOverlays.labelStyle.alignment = TextAnchor.MiddleCenter;
					BtcOverlays.labelStyle.fontStyle = FontStyle.Bold;
					BtcOverlays.labelStyle.fontSize = 14;
				}
				bool flag13 = BtcOverlays.Visible(2);
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					for (int i = 0; i < BtcOverlays.pathTimes.Count; i++)
					{
						BtcOverlays.Label(main, BtcOverlays.pathTimes[i].Key + Vector3.up * 0.9f, BtcOverlays.pathTimes[i].Value.ToString("0.0") + " s", BtcOverlays.Col(2));
					}
				}
				bool flag17 = BtcOverlays.landingLabel;
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				if (flag20)
				{
					bool flag21 = BtcOverlays.Visible(1);
					bool flag22 = flag21;
					bool flag23 = flag22;
					bool flag24 = flag23;
					if (flag24)
					{
						for (int j = 0; j < BtcOverlays.bombLines.Count; j++)
						{
							bool flag25 = BtcOverlays.bombLines[j].text.Length > 0;
							bool flag26 = flag25;
							bool flag27 = flag26;
							bool flag28 = flag27;
							if (flag28)
							{
								BtcOverlays.Label(main, BtcOverlays.bombLines[j].end + Vector3.up * 0.7f, BtcLang.Tr(BtcOverlays.bombLines[j].text), BtcOverlays.EndColor(BtcOverlays.bombLines[j].kind, 1));
							}
						}
					}
					bool flag29 = BtcOverlays.Visible(3);
					bool flag30 = flag29;
					bool flag31 = flag30;
					bool flag32 = flag31;
					if (flag32)
					{
						for (int k = 0; k < BtcOverlays.smashLines.Count; k++)
						{
							bool flag33 = BtcOverlays.smashLines[k].text.Length > 0;
							bool flag34 = flag33;
							bool flag35 = flag34;
							bool flag36 = flag35;
							if (flag36)
							{
								BtcOverlays.Label(main, BtcOverlays.smashLines[k].end + Vector3.up * 0.7f, BtcLang.Tr(BtcOverlays.smashLines[k].text), BtcOverlays.EndColor(BtcOverlays.smashLines[k].kind, 3));
							}
						}
					}
				}
			}
		}
	}

	private static void Label(Camera cam, Vector3 world, string text, Color c)
	{
		Vector3 vector = cam.WorldToScreenPoint(world);
		bool flag = vector.z <= 0f;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			Rect position = new Rect(vector.x - 60f, (float)Screen.height - vector.y - 12f, 120f, 24f);
			Color color = GUI.color;
			BtcOverlays.labelStyle.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
			GUI.Label(new Rect(position.x + 1f, position.y + 1f, position.width, position.height), text, BtcOverlays.labelStyle);
			BtcOverlays.labelStyle.normal.textColor = c;
			GUI.Label(position, text, BtcOverlays.labelStyle);
			GUI.color = color;
		}
	}

	static BtcOverlays()
	{
		BtcOverlays.Names = new string[]
		{
			"Mage Passive Radius",
			"Bomb Trajectory",
			"Path Timer",
			"Smash Trajectory",
			"Block Highlight"
		};
		BtcOverlays.Keys = new string[]
		{
			"mage",
			"bomb",
			"path",
			"smash"
		};
		BtcOverlays.on = new bool[4];
		BtcOverlays.alt = new bool[4];
		BtcOverlays.width = new float[]
		{
			0.08f,
			0.08f,
			0.08f,
			0.08f
		};
		BtcOverlays.alpha = new float[]
		{
			0.7f,
			0.9f,
			1f,
			0.9f
		};
		BtcOverlays.colorIdx = new int[]
		{
			0,
			1,
			3,
			2
		};
		BtcOverlays.Presets = new Color[]
		{
			new Color(0.35f, 1f, 0.45f),
			new Color(1f, 0.6f, 0.1f),
			new Color(1f, 0.25f, 0.85f),
			new Color(1f, 1f, 1f),
			new Color(0.3f, 0.8f, 1f),
			new Color(1f, 0.9f, 0.2f)
		};
		BtcOverlays.mageRadius = 8f;
		BtcOverlays.runSpeed = 6f;
		BtcOverlays.climbPenalty = 0.4f;
		BtcOverlays.stepDownPenalty = 0.2f;
		BtcOverlays.landingLabel = true;
		BtcOverlays.mages = new List<Transform>();
		BtcOverlays.bombLines = new List<BtcOverlays.Line>();
		BtcOverlays.smashLines = new List<BtcOverlays.Line>();
		BtcOverlays.pathTimes = new List<KeyValuePair<Vector3, float>>();
		BtcOverlays.circle = new List<Vector3>();
	}

	public static void DrawOne(int i, Func<string, bool, bool> check, Func<string, string, bool> step)
	{
		BtcOverlays.Load();
		bool flag = false;
		bool flag2 = i == 0 && check(BtcLang.Russian ? "ВСЕ оверлеи (мастер-выключатель)" : "ALL overlays (master switch)", BtcOverlays.master);
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			BtcOverlays.master = !BtcOverlays.master;
			flag = true;
		}
		bool flag5 = check(BtcLang.Tr("Enabled"), BtcOverlays.on[i]);
		bool flag6 = flag5;
		bool flag7 = flag6;
		if (flag7)
		{
			BtcOverlays.on[i] = !BtcOverlays.on[i];
			flag = true;
		}
		bool flag8 = BtcOverlays.on[i];
		bool flag9 = flag8;
		bool flag10 = flag9;
		if (flag10)
		{
			bool flag11 = check(BtcLang.Tr("Only while holding LeftAlt"), BtcOverlays.alt[i]);
			bool flag12 = flag11;
			bool flag13 = flag12;
			if (flag13)
			{
				BtcOverlays.alt[i] = !BtcOverlays.alt[i];
				flag = true;
			}
			bool flag14 = i != 2 && step(BtcLang.Tr("Line width"), BtcOverlays.F(BtcOverlays.width[i]));
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				BtcOverlays.width[i] = BtcOverlays.Next(BtcOverlays.width[i], new float[]
				{
					0.03f,
					0.06f,
					0.08f,
					0.12f,
					0.2f
				});
				flag = true;
			}
			bool flag17 = step(BtcLang.Tr("Opacity"), BtcOverlays.F(BtcOverlays.alpha[i]));
			bool flag18 = flag17;
			bool flag19 = flag18;
			if (flag19)
			{
				BtcOverlays.alpha[i] = BtcOverlays.Next(BtcOverlays.alpha[i], new float[]
				{
					0.25f,
					0.5f,
					0.7f,
					0.9f,
					1f
				});
				flag = true;
			}
			bool flag20 = step(BtcLang.Tr("Color"), "#" + (BtcOverlays.colorIdx[i] + 1));
			bool flag21 = flag20;
			bool flag22 = flag21;
			if (flag22)
			{
				BtcOverlays.colorIdx[i] = (BtcOverlays.colorIdx[i] + 1) % BtcOverlays.Presets.Length;
				flag = true;
			}
			bool flag23 = i == 0 && step(BtcLang.Tr("Radius (blocks)"), BtcOverlays.F(BtcOverlays.mageRadius));
			bool flag24 = flag23;
			bool flag25 = flag24;
			if (flag25)
			{
				BtcOverlays.mageRadius = BtcOverlays.Next(BtcOverlays.mageRadius, new float[]
				{
					4f,
					6f,
					8f,
					10f,
					12f,
					16f
				});
				flag = true;
			}
			bool flag26 = i == 2;
			bool flag27 = flag26;
			bool flag28 = flag27;
			if (flag28)
			{
				bool flag29 = step(BtcLang.Tr("Run speed (m/s)"), BtcOverlays.F(BtcOverlays.runSpeed));
				bool flag30 = flag29;
				bool flag31 = flag30;
				if (flag31)
				{
					BtcOverlays.runSpeed = BtcOverlays.Next(BtcOverlays.runSpeed, new float[]
					{
						3f,
						4f,
						5f,
						6f,
						7f
					});
					flag = true;
				}
				bool flag32 = step(BtcLang.Tr("Climb penalty (sec)"), BtcOverlays.F(BtcOverlays.climbPenalty));
				bool flag33 = flag32;
				bool flag34 = flag33;
				if (flag34)
				{
					BtcOverlays.climbPenalty = BtcOverlays.Next(BtcOverlays.climbPenalty, new float[]
					{
						0f,
						0.2f,
						0.4f,
						0.6f,
						0.8f,
						1f
					});
					flag = true;
				}
				bool flag35 = step(BtcLang.Tr("Step-down penalty (sec)"), BtcOverlays.F(BtcOverlays.stepDownPenalty));
				bool flag36 = flag35;
				bool flag37 = flag36;
				if (flag37)
				{
					BtcOverlays.stepDownPenalty = BtcOverlays.Next(BtcOverlays.stepDownPenalty, new float[]
					{
						0f,
						0.1f,
						0.2f,
						0.4f,
						0.6f
					});
					flag = true;
				}
			}
			bool flag38 = (i == 1 || i == 3) && check(BtcLang.Tr("Show landing label"), BtcOverlays.landingLabel);
			bool flag39 = flag38;
			bool flag40 = flag39;
			if (flag40)
			{
				BtcOverlays.landingLabel = !BtcOverlays.landingLabel;
				flag = true;
			}
		}
		bool flag41 = i == 2;
		if (flag41)
		{
			BtcWaypoints.DrawSettings(check, step);
		}
		bool flag42 = flag;
		bool flag43 = flag42;
		bool flag44 = flag43;
		if (flag44)
		{
			BtcOverlays.Save();
			BtcOverlays.ClearAll();
		}
	}

	public const int Mage = 0;

	public const int Bomb = 1;

	public const int PathT = 2;

	public const int Smash = 3;

	private const int N = 4;

	private const float AbyssY = -16f;

	private static readonly string[] Keys;

	private static readonly bool[] on;

	private static readonly bool[] alt;

	private static readonly float[] width;

	private static readonly float[] alpha;

	private static readonly int[] colorIdx;

	private static readonly Color[] Presets;

	private static float mageRadius;

	private static float runSpeed;

	private static float climbPenalty;

	private static float stepDownPenalty;

	private static bool landingLabel;

	private static bool loaded;

	private static float nextScan;

	private static Material mat;

	private static GUIStyle labelStyle;

	private static readonly List<Transform> mages;

	private static readonly List<BtcOverlays.Line> bombLines;

	private static readonly List<BtcOverlays.Line> smashLines;

	private static readonly List<KeyValuePair<Vector3, float>> pathTimes;

	private static readonly List<Vector3> circle;

	private static MethodInfo acceptMi;

	private static bool acceptLooked;

	public const int Count = 4;

	public static readonly string[] Names;

	private static bool master = true;

	private class Line
	{
		public List<Vector3> pts = new List<Vector3>();

		public int kind;

		public Vector3 end;

		public string text;
	}
}
}
