using System;
using System.Collections.Generic;
using Brix.Legacy;
using UnityEngine;

namespace BetterThanCastle
{
public static class BtcSimplePhys
{
	public static void AddRows()
	{
		BtcGameplay.Add("mov.simple", "mov", "Simple Physics (items fall straight down)", "Простая физика (предметы падают строго вниз)", 0f, 0f, 1f, 1f, true, "Needs 'Ignore items blocking paths'. Free blocks, tree logs and stones only fall vertically and touch nothing but the ground (barrels stay vanilla). Almost no desync. Every player needs the mod.", "Нужен чекбокс «Игнорировать блокировку пути предметами». Свободные блоки, брёвна и камешки падают только вертикально и касаются лишь земли (бочки как в ванили). Почти нет десинка. Мод нужен всем игрокам.");
		BtcGameplay.Add("mov.dropfix", "mov", "Fix items falling through the ground when dropped", "Починить проваливание предметов сквозь землю при дропе", 1f, 0f, 1f, 1f, true, "Vanilla bug: a dropped item (grass bag, tools) is teleported in front of the bricktron and can start inside a wall or voxel, or tunnel through thin ground. Moves the drop point out of solid blocks and uses continuous collision for the item.", "Ванильный баг: выброшенный предмет (мешок травы, инструменты) телепортируется перед бриктроном и может оказаться внутри стены или вокселя, либо пролететь сквозь тонкую землю. Вытаскивает точку дропа из твёрдых блоков и включает непрерывные коллизии предмета.");
		BtcBuildAI.AddRows();
	}

	public static void Item(GameObject go)
	{
		Rigidbody component = go.GetComponent<Rigidbody>();
		bool flag = component == null;
		if (!flag)
		{
			BtcSimplePhys._mask |= 1 << go.layer;
			bool flag2 = !BtcSimplePhys._rbs.ContainsKey(component);
			if (flag2)
			{
				BtcSimplePhys._rbs[component] = component.constraints;
			}
			bool fixOn = BtcSimplePhys.FixOn;
			if (fixOn)
			{
				component.collisionDetectionMode = CollisionDetectionMode.Continuous;
			}
			bool on = BtcSimplePhys._on;
			if (on)
			{
				BtcSimplePhys.Freeze(component, true);
			}
		}
	}

	public static void Tick()
	{
		BtcBuildAI.Sync();
		float unscaledTime = Time.unscaledTime;
		bool flag = unscaledTime < BtcSimplePhys._next;
		bool flag2 = !flag;
		if (flag2)
		{
			BtcSimplePhys._next = unscaledTime + 0.2f;
			bool flag3 = BtcSimplePhys.Flag() && BtcQol.ItemsOn;
			bool flag4 = flag3 != BtcSimplePhys._on;
			bool flag5 = flag4;
			if (flag5)
			{
				bool flag6 = flag3 && !BtcSimplePhys.SetLayers(true);
				bool flag7 = flag6;
				if (flag7)
				{
					BtcSimplePhys._next = unscaledTime + 1f;
				}
				else
				{
					BtcSimplePhys._on = flag3;
					BtcSimplePhys.Prune();
					foreach (KeyValuePair<Rigidbody, RigidbodyConstraints> keyValuePair in BtcSimplePhys._rbs)
					{
						BtcSimplePhys.Freeze(keyValuePair.Key, flag3);
					}
					bool flag8 = !flag3;
					bool flag9 = flag8;
					if (flag9)
					{
						BtcSimplePhys.SetLayers(false);
					}
				}
			}
			else
			{
				bool flag10 = !flag3;
				bool flag11 = !flag10;
				if (flag11)
				{
					foreach (KeyValuePair<Rigidbody, RigidbodyConstraints> keyValuePair2 in BtcSimplePhys._rbs)
					{
						Rigidbody key = keyValuePair2.Key;
						bool flag12 = key == null || key.isKinematic || key.IsSleeping();
						bool flag13 = !flag12;
						if (flag13)
						{
							Vector3 velocity = key.velocity;
							bool flag14 = velocity.y > 0f;
							bool flag15 = flag14;
							if (flag15)
							{
								velocity.y = 0f;
								key.velocity = velocity;
							}
						}
					}
				}
			}
		}
	}

	private static bool Flag()
	{
		bool flag = BtcSimplePhys._idx == -2;
		if (flag)
		{
			BtcSimplePhys._idx = BtcGameplay.Idx("mov.simple");
		}
		float[] active = BtcGameplay.Active;
		return BtcSimplePhys._idx >= 0 && active != null && BtcSimplePhys._idx < active.Length && active[BtcSimplePhys._idx] > 0.5f;
	}

	public static bool Vis(BtcGameplay.Def def, float[] shown)
	{
		bool flag = def.Id != "mov.simple";
		bool result;
		if (flag)
		{
			result = true;
		}
		else
		{
			int num = BtcGameplay.Idx("mov.items");
			result = (num >= 0 && num < shown.Length && shown[num] > 0.5f);
		}
		return result;
	}

	private static void Freeze(Rigidbody rb, bool on)
	{
		bool flag = rb == null;
		if (!flag)
		{
			if (on)
			{
				rb.constraints = (RigidbodyConstraints)122;
				bool flag2 = !rb.isKinematic;
				if (flag2)
				{
					rb.velocity = Vector3.zero;
					rb.angularVelocity = Vector3.zero;
				}
			}
			else
			{
				RigidbodyConstraints rigidbodyConstraints;
				rb.constraints = (BtcSimplePhys._rbs.TryGetValue(rb, out rigidbodyConstraints) ? rigidbodyConstraints : RigidbodyConstraints.None);
			}
		}
	}

	private static void Prune()
	{
		List<Rigidbody> list = null;
		foreach (KeyValuePair<Rigidbody, RigidbodyConstraints> keyValuePair in BtcSimplePhys._rbs)
		{
			bool flag = keyValuePair.Key == null;
			if (flag)
			{
				bool flag2 = list == null;
				if (flag2)
				{
					list = new List<Rigidbody>();
				}
				list.Add(keyValuePair.Key);
			}
		}
		bool flag3 = list != null;
		if (flag3)
		{
			for (int i = 0; i < list.Count; i++)
			{
				BtcSimplePhys._rbs.Remove(list[i]);
			}
		}
	}

	private static bool SetLayers(bool on)
	{
		if (on)
		{
			bool flag = BtcSimplePhys._mask == 0;
			if (flag)
			{
				return false;
			}
			bool flag2 = BtcSimplePhys._terrain < 0;
			if (flag2)
			{
				BtcSimplePhys._terrain = BtcSimplePhys.FindTerrain();
			}
			bool flag3 = BtcSimplePhys._terrain < 0 || (BtcSimplePhys._mask & 1 << BtcSimplePhys._terrain) != 0;
			if (flag3)
			{
				bool flag4 = BtcSimplePhys._terrain >= 0 && !BtcSimplePhys._warned;
				if (flag4)
				{
					BtcSimplePhys._warned = true;
					Debug.LogWarning("[BtcSimplePhys] terrain layer " + BtcSimplePhys._terrain + " is also an item layer, layer ignoring skipped");
				}
				return BtcSimplePhys._terrain >= 0;
			}
			for (int i = 0; i < 32; i++)
			{
				bool flag5 = (BtcSimplePhys._mask & 1 << i) == 0;
				if (!flag5)
				{
					for (int j = 0; j < 32; j++)
					{
						bool flag6 = j == BtcSimplePhys._terrain || Physics.GetIgnoreLayerCollision(i, j);
						if (!flag6)
						{
							Physics.IgnoreLayerCollision(i, j, true);
							BtcSimplePhys._changed.Add((short)(i * 32 + j));
						}
					}
				}
			}
			BarilScript barilScript = UnityEngine.Object.FindObjectOfType<BarilScript>();
			Debug.Log(string.Concat(new object[]
			{
				"[BtcSimplePhys] on: itemMask=",
				BtcSimplePhys._mask,
				" terrain=",
				BtcSimplePhys._terrain,
				" barrelLayer=",
				(barilScript != null) ? barilScript.gameObject.layer : -1
			}));
		}
		else
		{
			for (int k = 0; k < BtcSimplePhys._changed.Count; k++)
			{
				Physics.IgnoreLayerCollision((int)(BtcSimplePhys._changed[k] / 32), (int)(BtcSimplePhys._changed[k] % 32), false);
			}
			BtcSimplePhys._changed.Clear();
		}
		return true;
	}

	private static int FindTerrain()
	{
		foreach (KeyValuePair<Rigidbody, RigidbodyConstraints> keyValuePair in BtcSimplePhys._rbs)
		{
			Rigidbody key = keyValuePair.Key;
			bool flag = key == null || !key.gameObject.activeInHierarchy;
			if (!flag)
			{
				RaycastHit[] array = Physics.RaycastAll(key.position + Vector3.up * 0.5f, Vector3.down, 64f, -1, QueryTriggerInteraction.Ignore);
				float num = float.MaxValue;
				int num2 = -1;
				for (int i = 0; i < array.Length; i++)
				{
					Collider collider = array[i].collider;
					bool flag2 = collider == null || collider.attachedRigidbody != null || (BtcSimplePhys._mask & 1 << collider.gameObject.layer) != 0;
					if (!flag2)
					{
						bool flag3 = array[i].distance < num;
						if (flag3)
						{
							num = array[i].distance;
							num2 = collider.gameObject.layer;
						}
					}
				}
				bool flag4 = num2 >= 0;
				if (flag4)
				{
					return num2;
				}
			}
		}
		return -1;
	}

	static BtcSimplePhys()
	{
		BtcSimplePhys._idx = -2;
		BtcSimplePhys._terrain = -1;
	}

	public static void LogFix(GameObject go)
	{
		bool flag = !BtcSimplePhys._on || go == null;
		if (!flag)
		{
			Vector3 forward = go.transform.forward;
			forward.y = 0f;
			Quaternion rotation = Quaternion.LookRotation((forward.sqrMagnitude > 0.01f) ? forward.normalized : Vector3.right);
			go.transform.rotation = rotation;
			Rigidbody component = go.GetComponent<Rigidbody>();
			bool flag2 = component == null;
			if (!flag2)
			{
				component.rotation = rotation;
				bool flag3 = !component.isKinematic;
				if (flag3)
				{
					component.velocity = Vector3.zero;
					component.angularVelocity = Vector3.zero;
				}
			}
		}
	}

	public static void Drop(GameObject go, Transform user)
	{
		bool flag = !BtcSimplePhys.FixOn || go == null || user == null;
		if (!flag)
		{
			Vector3 vector = user.position + user.up * 1.5f;
			Vector3 vector2 = go.transform.position;
			Vector3 a = vector2 - vector;
			float magnitude = a.magnitude;
			bool flag2 = magnitude > 0.001f;
			if (flag2)
			{
				Vector3 vector3 = a / magnitude;
				int num = Physics.RaycastNonAlloc(vector, vector3, BtcSimplePhys._hits, magnitude, -1, QueryTriggerInteraction.Ignore);
				float num2 = magnitude;
				bool flag3 = false;
				for (int i = 0; i < num; i++)
				{
					Collider collider = BtcSimplePhys._hits[i].collider;
					bool flag4 = collider != null && collider.attachedRigidbody == null && BtcSimplePhys._hits[i].distance < num2;
					if (flag4)
					{
						num2 = BtcSimplePhys._hits[i].distance;
						flag3 = true;
					}
				}
				bool flag5 = flag3;
				if (flag5)
				{
					vector2 = vector + vector3 * Mathf.Max(0f, num2 - 0.3f);
				}
			}
			bool flag6 = BtcSimplePhys.Solid(vector2);
			if (flag6)
			{
				vector2 = vector;
			}
			go.transform.position = vector2;
			Rigidbody component = go.GetComponent<Rigidbody>();
			bool flag7 = component != null;
			if (flag7)
			{
				component.position = vector2;
				component.collisionDetectionMode = CollisionDetectionMode.Continuous;
			}
		}
	}

	private static bool Solid(Vector3 p)
	{
		int num = Physics.OverlapSphereNonAlloc(p, 0.2f, BtcSimplePhys._cols, -1, QueryTriggerInteraction.Ignore);
		for (int i = 0; i < num; i++)
		{
			bool flag = BtcSimplePhys._cols[i] != null && BtcSimplePhys._cols[i].attachedRigidbody == null;
			if (flag)
			{
				return true;
			}
		}
		return false;
	}

	public static bool FixOn
	{
		get
		{
			bool flag = BtcSimplePhys._fixIdx == -2;
			if (flag)
			{
				BtcSimplePhys._fixIdx = BtcGameplay.Idx("mov.dropfix");
			}
			float[] active = BtcGameplay.Active;
			return BtcSimplePhys._fixIdx >= 0 && active != null && BtcSimplePhys._fixIdx < active.Length && active[BtcSimplePhys._fixIdx] > 0.5f;
		}
	}

	private static readonly Dictionary<Rigidbody, RigidbodyConstraints> _rbs = new Dictionary<Rigidbody, RigidbodyConstraints>();

	private static readonly List<short> _changed = new List<short>();

	private static int _idx;

	private static int _mask;

	private static int _terrain;

	private static float _next;

	private static bool _on;

	private static bool _warned;

	private static readonly RaycastHit[] _hits = new RaycastHit[16];

	private static readonly Collider[] _cols = new Collider[8];

	private static int _fixIdx = -2;
}
}
