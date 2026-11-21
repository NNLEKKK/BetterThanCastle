using System;
using System.Collections.Generic;
using System.IO;
using Brix.Engine;
using Brix.Engine.Abstract;
using Brix.Game;
using Brix.Game.AI;
using Brix.UI.GameSelector;
using Brix.Util;
using UnityEngine;

namespace BetterThanCastle
{
public static class BtcLineMove
{
	public static void Tick()
	{
		BtcBlockMarks.Tick();
		BtcLineMove.LogTick();
		bool flag = !BtcHotkeys.Enabled || BtcReplay.Active;
		bool flag2 = flag;
		if (flag2)
		{
			bool flag3 = BtcLineMove.active;
			bool flag4 = flag3;
			if (flag4)
			{
				BtcLineMove.End();
			}
		}
		else
		{
			bool flag5 = !BtcLineMove.active;
			bool flag6 = flag5;
			if (flag6)
			{
				string[] array = new string[]
				{
					"line_move",
					"line_move_diagonal"
				};
				for (int i = 0; i < array.Length; i++)
				{
					KeyCode[] keys = BtcHotkeys.GetKeys(array[i], null);
					bool flag7 = keys != null && keys.Length != 0 && BtcHotkeys.ComboPressed(keys);
					bool flag8 = flag7;
					if (flag8)
					{
						BtcLineMove.curId = array[i];
						BtcLineMove.diagMode = (i == 1);
						BtcLineMove.Begin();
						bool flag9 = BtcLineMove.active;
						bool flag10 = flag9;
						if (flag10)
						{
							BtcLineMove.Sweep();
						}
						break;
					}
				}
			}
			else
			{
				KeyCode[] keys2 = BtcHotkeys.GetKeys(BtcLineMove.curId, null);
				bool flag11 = keys2 == null || keys2.Length == 0;
				bool flag12 = flag11;
				if (flag12)
				{
					BtcLineMove.End();
				}
				else
				{
					bool flag13 = BtcHotkeys.GetString("line_move_mode", "hold").Equals("toggle", StringComparison.OrdinalIgnoreCase);
					bool flag14 = BtcHotkeys.IsSuppressed(keys2);
					bool flag15 = flag14;
					if (flag15)
					{
						BtcLineMove.End();
					}
					else
					{
						bool flag16 = flag13;
						bool flag17 = flag16;
						if (flag17)
						{
							bool flag18 = BtcHotkeys.ComboPressed(keys2);
							bool flag19 = flag18;
							if (flag19)
							{
								BtcLineMove.End();
							}
							else
							{
								BtcLineMove.Sweep();
							}
						}
						else
						{
							bool flag20 = BtcLineMove.waiting;
							bool flag21 = flag20;
							if (flag21)
							{
								bool flag22 = BtcHotkeys.ComboPressed(keys2) || BtcHotkeys.AllHeld(keys2);
								bool flag23 = flag22;
								if (flag23)
								{
									bool flag24 = BtcLineMove.free.Count == 0;
									bool flag25 = flag24;
									if (flag25)
									{
										BtcLineMove.End();
										BtcLineMove.Begin();
										bool flag26 = BtcLineMove.active;
										bool flag27 = flag26;
										if (flag27)
										{
											BtcLineMove.Sweep();
										}
									}
									else
									{
										BtcLineMove.waiting = false;
										BtcLineMove.prevValid = false;
										BtcLineMove.lastNum = int.MinValue;
										BtcLineMove.DiagReset();
										BtcLineMove.Sweep();
									}
								}
								else
								{
									bool flag28 = Time.unscaledTime >= BtcLineMove.waitUntil;
									bool flag29 = flag28;
									if (flag29)
									{
										BtcLineMove.End();
									}
								}
							}
							else
							{
								bool flag30 = !BtcHotkeys.AllHeld(keys2);
								bool flag31 = flag30;
								if (flag31)
								{
									int delayMs = BtcBlockMarks.DelayMs;
									bool flag32 = delayMs <= 0 || BtcLineMove.free.Count == 0;
									bool flag33 = flag32;
									if (flag33)
									{
										BtcLineMove.End();
									}
									else
									{
										BtcLineMove.waiting = true;
										BtcLineMove.waitUntil = Time.unscaledTime + (float)delayMs * 0.001f;
										BtcLineMove.prevValid = false;
									}
								}
								else
								{
									BtcLineMove.Sweep();
								}
							}
						}
					}
				}
			}
		}
	}

	private static void Begin()
	{
		BtcLineMove.free.Clear();
		BtcLineMove.used.Clear();
		BtcLineMove.selBuf.Clear();
		UIGameSelector.GetSelectedEverything(ref BtcLineMove.selBuf);
		for (int i = 0; i < BtcLineMove.selBuf.Count; i++)
		{
			GameObject gameObject = BtcLineMove.selBuf[i];
			bool flag = !(gameObject == null) && Affiliation.BelongsToMe(gameObject);
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			if (flag4)
			{
				Labor component = gameObject.GetComponent<Labor>();
				bool flag5 = !(component == null) && !BtcLineMove.free.Contains(component);
				bool flag6 = flag5;
				bool flag7 = flag6;
				bool flag8 = flag7;
				if (flag8)
				{
					BtcLineMove.free.Add(component);
				}
			}
		}
		BtcLineMove.selBuf.Clear();
		BtcLineMove.nearest = (BtcHotkeys.GetFloat("line_move_nearest", 1f) >= 0.5f);
		BtcLineMove.DiagReset();
		BtcLineMove.lastNum = int.MinValue;
		BtcLineMove.prevValid = false;
		BtcLineMove.waiting = false;
		BtcLineMove.active = (BtcLineMove.free.Count > 0);
		bool flag9 = BtcLineMove.active;
		bool flag10 = flag9;
		bool flag11 = flag10;
		bool flag12 = flag11;
		if (flag12)
		{
			BtcBlockMarks.BeginSession();
		}
	}

	private static void End()
	{
		bool flag = BtcLineMove.active;
		BtcLineMove.active = false;
		BtcLineMove.waiting = false;
		BtcLineMove.free.Clear();
		BtcLineMove.used.Clear();
		BtcLineMove.prevValid = false;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		if (flag5)
		{
			BtcBlockMarks.EndSession();
		}
	}

	private static void Sweep()
	{
		bool flag = BtcLineMove.free.Count == 0;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			bool flag5 = BtcHotkeys.PointerOverUi();
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				BtcLineMove.prevValid = false;
			}
			else
			{
				Camera main = Camera.main;
				bool flag9 = main == null;
				bool flag10 = !flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				if (flag12)
				{
					Vector3 mousePosition = Input.mousePosition;
					bool flag13 = !BtcLineMove.prevValid;
					bool flag14 = flag13;
					bool flag15 = flag14;
					bool flag16 = flag15;
					if (flag16)
					{
						BtcLineMove.Probe(main, mousePosition);
					}
					else
					{
						int num = Mathf.Clamp(Mathf.CeilToInt((mousePosition - BtcLineMove.prev).magnitude / 8f), 1, 40);
						int num2 = 1;
						while (num2 <= num && BtcLineMove.free.Count > 0)
						{
							BtcLineMove.Probe(main, Vector3.Lerp(BtcLineMove.prev, mousePosition, (float)num2 / (float)num));
							num2++;
						}
					}
					BtcLineMove.prev = mousePosition;
					BtcLineMove.prevValid = true;
				}
			}
		}
	}

	private static void Probe(Camera cam, Vector3 screen)
	{
		int num;
		Vector3 vector;
		bool flag2;
		bool flag = !BtcLineMove.TryCell(cam, screen, out num, out vector, out flag2);
		bool flag3 = !flag;
		if (flag3)
		{
			bool flag4 = BtcLineMove.diagMode;
			bool flag5 = flag4;
			if (flag5)
			{
				BtcLineMove.ProbeDiag(num, vector, flag2);
			}
			else
			{
				bool flag6 = num == BtcLineMove.lastNum;
				bool flag7 = !flag6;
				if (flag7)
				{
					BtcLineMove.lastNum = num;
					bool flag8 = flag2;
					bool flag9 = flag8;
					if (flag9)
					{
						BtcLineMove.Assign(num, vector);
					}
				}
			}
		}
	}

	private static Labor TakeUnit(Vector3 target)
	{
		int num = -1;
		float num2 = float.MaxValue;
		for (int i = BtcLineMove.free.Count - 1; i >= 0; i--)
		{
			Labor labor = BtcLineMove.free[i];
			bool flag = labor == null;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			if (flag4)
			{
				BtcLineMove.free.RemoveAt(i);
			}
			else
			{
				float sqrMagnitude = (labor.transform.position - target).sqrMagnitude;
				bool flag5 = !BtcLineMove.nearest || sqrMagnitude <= num2;
				bool flag6 = flag5;
				bool flag7 = flag6;
				bool flag8 = flag7;
				if (flag8)
				{
					num2 = sqrMagnitude;
					num = i;
					bool flag9 = !BtcLineMove.nearest;
					bool flag10 = flag9;
					bool flag11 = flag10;
					bool flag12 = flag11;
					if (flag12)
					{
						num2 = float.MaxValue;
					}
				}
			}
		}
		bool flag13 = num < 0;
		bool flag14 = flag13;
		bool flag15 = flag14;
		bool flag16 = flag15;
		Labor result;
		if (flag16)
		{
			result = null;
		}
		else
		{
			Labor labor2 = BtcLineMove.free[num];
			BtcLineMove.free.RemoveAt(num);
			result = labor2;
		}
		return result;
	}

	static BtcLineMove()
	{
		BtcLineMove.free = new List<Labor>();
		BtcLineMove.used = new HashSet<int>();
		BtcLineMove.selBuf = new List<GameObject>();
	}

	public static bool TryCell(Camera cam, Vector3 screen, out int num, out Vector3 pos, out bool walkable)
	{
		num = 0;
		pos = Vector3.zero;
		walkable = false;
		Ray ray = cam.ScreenPointToRay(screen);
		RaycastHit raycastHit;
		bool flag = !Physics.Raycast(ray, out raycastHit, 500f, 132096, QueryTriggerInteraction.Collide);
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool result;
		if (flag4)
		{
			result = false;
		}
		else
		{
			Vector3 vecteur = Calcule.RoundVecteur(raycastHit.point - raycastHit.normal * 0.0001f);
			num = Brix.Engine.Abstract.Voxel.GetScalaire(vecteur);
			bool flag5 = !Brix.Engine.Voxel.IsOpenAndWalkable(XYZ.FromScalaire(num));
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				num += 1048576;
			}
			walkable = Brix.Engine.Voxel.IsOpenAndWalkable(XYZ.FromScalaire(num));
			pos = Brix.Engine.Abstract.Voxel.GetVecteur(num);
			BtcLineMove.hitX = Mathf.Clamp(raycastHit.point.x, pos.x - 0.5f, pos.x + 0.5f);
			BtcLineMove.hitZ = Mathf.Clamp(raycastHit.point.z, pos.z - 0.5f, pos.z + 0.5f);
			result = true;
		}
		return result;
	}

	private static bool Assign(int num, Vector3 vector)
	{
		bool flag = BtcLineMove.used.Contains(num);
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool result;
		if (flag4)
		{
			result = false;
		}
		else
		{
			Labor labor = BtcLineMove.TakeUnit(vector);
			bool flag5 = labor == null;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				result = false;
			}
			else
			{
				BtcLineMove.used.Add(num);
				BtcBlockMarks.AddTarget(vector);
				BtcLineMove.LogAdd(labor, vector);
				try
				{
					BtcWaypoints.OnManualOrder(labor);
					DirectOrderProject.AddMoveToGoal(labor, vector, false, false);
				}
				catch (Exception)
				{
				}
				result = true;
			}
		}
		return result;
	}

	private static void DiagReset()
	{
		BtcLineMove.hasLast = false;
		BtcLineMove.lastY = 0f;
	}

	private static void ProbeDiag(int num, Vector3 pos, bool walkable)
	{
		bool flag = !BtcLineMove.hasLast;
		bool flag2 = flag;
		if (flag2)
		{
			bool flag3 = walkable && BtcLineMove.Assign(num, pos);
			bool flag4 = flag3;
			if (flag4)
			{
				BtcLineMove.hasLast = true;
				BtcLineMove.lx = Mathf.RoundToInt(pos.x);
				BtcLineMove.lz = Mathf.RoundToInt(pos.z);
				BtcLineMove.lastY = pos.y;
			}
		}
		else
		{
			float num2 = BtcLineMove.hitX - (float)BtcLineMove.lx;
			float num3 = BtcLineMove.hitZ - (float)BtcLineMove.lz;
			float num4 = Mathf.Abs(num2);
			float num5 = Mathf.Abs(num3);
			float num6 = Mathf.Min(num4, num5);
			float num7 = Mathf.Max(num4, num5);
			bool flag5 = num6 < 0.5f || num6 < num7 * 0.4f;
			bool flag6 = !flag5;
			if (flag6)
			{
				int num8 = (num2 < 0f) ? -1 : 1;
				int num9 = (num3 < 0f) ? -1 : 1;
				int num10 = Mathf.Max(1, Mathf.FloorToInt((num4 + num5) * 0.5f + 0.5f));
				int num11 = 0;
				while (num11 < num10 && BtcLineMove.free.Count > 0)
				{
					num11++;
					BtcLineMove.lx += num8;
					BtcLineMove.lz += num9;
					int num12;
					Vector3 vector;
					bool flag7 = BtcLineMove.FindColumn(BtcLineMove.lx, BtcLineMove.lz, BtcLineMove.lastY, out num12, out vector);
					bool flag8 = flag7;
					if (flag8)
					{
						BtcLineMove.lastY = vector.y;
						BtcLineMove.Assign(num12, vector);
					}
				}
			}
		}
	}

	private static bool FindColumn(int x, int z, float yRef, out int num, out Vector3 pos)
	{
		RaycastHit raycastHit;
		bool flag = Physics.Raycast(new Vector3((float)x, yRef + 2.5f, (float)z), Vector3.down, out raycastHit, 8f, 132096, QueryTriggerInteraction.Collide);
		bool flag2 = flag;
		if (flag2)
		{
			Vector3 vector = Calcule.RoundVecteur(raycastHit.point - raycastHit.normal * 0.0001f);
			bool flag3 = Mathf.RoundToInt(vector.x) == x && Mathf.RoundToInt(vector.z) == z;
			bool flag4 = flag3;
			if (flag4)
			{
				int num2 = Brix.Engine.Abstract.Voxel.GetScalaire(vector);
				bool flag5 = !Brix.Engine.Voxel.IsOpenAndWalkable(XYZ.FromScalaire(num2));
				bool flag6 = flag5;
				if (flag6)
				{
					num2 += 1048576;
				}
				bool flag7 = Brix.Engine.Voxel.IsOpenAndWalkable(XYZ.FromScalaire(num2));
				bool flag8 = flag7;
				if (flag8)
				{
					num = num2;
					pos = Brix.Engine.Abstract.Voxel.GetVecteur(num2);
					return true;
				}
			}
		}
		int num3 = Mathf.RoundToInt(yRef);
		for (int i = 0; i < 9; i++)
		{
			int num4 = (i == 0) ? 0 : (((i & 1) == 1) ? ((i + 1) / 2) : (-(i / 2)));
			int scalaire = Brix.Engine.Abstract.Voxel.GetScalaire(new Vector3((float)x, (float)(num3 + num4), (float)z));
			bool flag9 = Brix.Engine.Voxel.IsOpenAndWalkable(XYZ.FromScalaire(scalaire));
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			if (flag12)
			{
				num = scalaire;
				pos = Brix.Engine.Abstract.Voxel.GetVecteur(scalaire);
				return true;
			}
		}
		num = 0;
		pos = Vector3.zero;
		return false;
	}

	private static void LogAdd(Labor labor, Vector3 target)
	{
		bool flag = BtcLineMove.dgL.Count >= 80;
		bool flag2 = !flag;
		if (flag2)
		{
			BtcLineMove.dgL.Add(labor);
			BtcLineMove.dgT.Add(target);
			BtcLineMove.dgW.Add(Time.unscaledTime + 4f);
			BtcLineMove.dgS.Add(string.Concat(new object[]
			{
				BtcLineMove.diagMode ? "DIAG" : "NORM",
				" target=",
				target.ToString("F2"),
				" start=",
				labor.transform.position.ToString("F2"),
				" hit=(",
				BtcLineMove.hitX.ToString("F2"),
				",",
				BtcLineMove.hitZ.ToString("F2"),
				") last=(",
				BtcLineMove.lx,
				",",
				BtcLineMove.lz,
				") lastY=",
				BtcLineMove.lastY.ToString("F2")
			}));
		}
	}

	private static void LogTick()
	{
		for (int i = BtcLineMove.dgL.Count - 1; i >= 0; i--)
		{
			bool flag = Time.unscaledTime < BtcLineMove.dgW[i];
			bool flag2 = !flag;
			if (flag2)
			{
				try
				{
					Labor labor = BtcLineMove.dgL[i];
					string text = BtcLineMove.dgS[i];
					bool flag3 = labor != null;
					bool flag4 = flag3;
					if (flag4)
					{
						Vector3 position = labor.transform.position;
						text = string.Concat(new string[]
						{
							text,
							" END=",
							position.ToString("F2"),
							" delta=",
							(position - BtcLineMove.dgT[i]).ToString("F2")
						});
					}
					BtcPaths.Init();
					File.AppendAllText(BtcPaths.DebugFile("btc_linemove_log.txt"), text + "\r\n");
				}
				catch (Exception)
				{
				}
				BtcLineMove.dgL.RemoveAt(i);
				BtcLineMove.dgT.RemoveAt(i);
				BtcLineMove.dgW.RemoveAt(i);
				BtcLineMove.dgS.RemoveAt(i);
			}
		}
	}

	private static bool active;

	private static bool nearest = true;

	private static bool prevValid;

	private static int lastNum = int.MinValue;

	private static Vector3 prev;

	private static readonly List<Labor> free;

	private static readonly HashSet<int> used;

	private static List<GameObject> selBuf;

	private static bool waiting;

	private static float waitUntil;

	private static float lastY;

	private static bool hasLast;

	private static int lx;

	private static int lz;

	private static float hitX;

	private static float hitZ;

	private static string curId = "line_move";

	private static bool diagMode;

	private static List<Labor> dgL = new List<Labor>();

	private static List<Vector3> dgT = new List<Vector3>();

	private static List<float> dgW = new List<float>();

	private static List<string> dgS = new List<string>();
}
}
