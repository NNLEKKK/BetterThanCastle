using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Brix.Engine;
using Brix.Game.AI;
using Brix.Game.AI.Pathfinding;
using Brix.Pathfinding.Test;
using UnityEngine;

namespace BetterThanCastle
{
// Group jump: each selected unit gets its own block in a line through the clicked target.
// Client-side only: changes the Vector3 in vanilla CmdGoAndJumpTo (wire format unchanged).
// Host side (vanilla): CmdGoAndJumpTo -> TurnAndJumpToward -> GoJumpOff(XYZ.FromVector3(position)) uses the voxel of the
// position as the jump destination as is (no re-snap), so distinct voxels = distinct landing blocks.
public static class BtcJumpLine
{
	private static int iOn = -2;
	private static int frame = -1;
	private static int targetFrame = -1;
	private static Vector3 rawTarget;
	private static readonly Dictionary<int, Vector3> slots = new Dictionary<int, Vector3>();
	private static readonly List<Vector3> spare = new List<Vector3>();
	private static int spareUsed;
	private static MethodInfo subjects;

	public static void AddRows()
	{
		BtcGameplay.Add("mov.jumpLine", "mov", "Group jump: each unit to its own block in a line", "Групповой прыжок: каждый на свой блок в линию", 1f, 0f, 1f, 1f, true, "When several units are ordered to jump, each one jumps to its own block; the blocks form a line through the target, across the direction of the group.", "Когда прыжок приказан нескольким юнитам, каждый прыгает на свой блок; блоки образуют линию через цель поперёк направления группы.");
	}

	public static bool On
	{
		get
		{
			if (!BtcNetworkGate.Enabled) return false;
			if (iOn == -2) iOn = BtcGameplay.Idx("mov.jumpLine");
			float[] a = BtcGameplay.Active;
			return iOn >= 0 && a != null && iOn < a.Length && a[iOn] > 0.5f;
		}
	}

	// Formation.Init postfix: remembers the real click target (GoAndJumpTo only gets Formation.Spot(i)).
	public static void NoteTarget(Vector3 target)
	{
		rawTarget = target;
		targetFrame = Time.frameCount;
	}

	// Called from the GoAndJumpTo prefix for every labor of one click (same frame).
	public static void Adjust(Labor labor, ref Vector3 position)
	{
		if (labor == null) return;
		if (Time.frameCount != frame)
		{
			frame = Time.frameCount;
			slots.Clear();
			spare.Clear();
			spareUsed = 0;
			Build(labor, position);
		}
		int id = labor.GetInstanceID();
		Vector3 v;
		if (slots.TryGetValue(id, out v)) { position = v; return; }
		// labor not in the collected set: next free unique slot
		if (spare.Count == 0) return;
		if (spareUsed < spare.Count) v = spare[spareUsed++];
		else
		{
			v = spare[spare.Count - 1];
			BtcLog.Warn("JumpLine", "no free slot left, reusing a block for an extra unit");
		}
		slots[id] = v;
		position = v;
	}

	private static List<Labor> Group()
	{
		if (subjects == null)
		{
			MethodInfo m = typeof(Brix.Legacy.Instructions).GetMethod("Subjects", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
			if (m == null) return null;
			subjects = m.MakeGenericMethod(typeof(Labor));
		}
		return subjects.Invoke(null, null) as List<Labor>;
	}

	private static long Key(XYZ p)
	{
		return ((long)(p.x + 32768) << 32) | ((long)(p.z + 32768) << 16) | (long)((p.y + 32768) & 0xFFFF);
	}

	private struct Slot
	{
		public Vector3 pos;
		public float lat; // lateral coordinate across the jump direction
	}

	private static void Build(Labor first, Vector3 fallbackTarget)
	{
		List<Labor> group = Group();
		if (group == null) group = new List<Labor>();
		if (group.Count == 0) group.Add(first);
		int n = group.Count;
		Vector3 target = (targetFrame == Time.frameCount) ? rawTarget : fallbackTarget;
		Vector3 c = Vector3.zero;
		for (int i = 0; i < n; i++) c += group[i].transform.position;
		c /= n;
		Vector3 d = target - c;
		d.y = 0f;
		if (d.sqrMagnitude < 0.01f) d = new Vector3(0f, 0f, 1f);
		d.Normalize();
		Vector3 perp = new Vector3(-d.z, 0f, d.x);
		XYZ t = XYZ.FromVector3(target);

		int want = n + 8; // spare slots for labors outside the collected set
		int maxOff = want + 4;
		List<Slot> pool = new List<Slot>();
		HashSet<long> seen = new HashSet<long>();
		int rows = 0;
		for (int r = 0; r < 12 && pool.Count < want; r++)
		{
			// row order: 0, behind (toward the group), in front, 2 behind, 2 in front ...
			int row = (r == 0) ? 0 : ((r % 2 == 1) ? -((r + 1) / 2) : (r / 2));
			Vector3 rowShift = d * row;
			rows++;
			for (int k = 0; k <= maxOff * 2 && pool.Count < want; k++)
			{
				int off = (k == 0) ? 0 : ((k % 2 == 1) ? (k + 1) / 2 : -(k / 2));
				Vector3 q = new Vector3(t.x, t.y, t.z) + perp * off + rowShift;
				XYZ p = new XYZ(Mathf.RoundToInt(q.x), t.y, Mathf.RoundToInt(q.z));
				WalkablePathNode node = PathNodeRegistry.WalkableAt(p);
				if (node == null || node.IsEmpty) continue;
				XYZ rp = node.RepresentativePosition;
				if (!seen.Add(Key(rp))) continue;
				Slot s;
				s.pos = rp.ToVector3();
				s.lat = off;
				pool.Add(s);
			}
		}
		if (pool.Count == 0) return;

		int take = Math.Min(n, pool.Count);
		// the first `take` pool entries are the most central ones; order them across the direction
		Slot[] chosen = new Slot[take];
		for (int i = 0; i < take; i++) chosen[i] = pool[i];
		Array.Sort(chosen, (a, b) => a.lat.CompareTo(b.lat));
		float[] ul = new float[n];
		int[] ui = new int[n];
		for (int i = 0; i < n; i++)
		{
			ul[i] = Vector3.Dot(group[i].transform.position - c, perp);
			ui[i] = i;
		}
		Array.Sort(ui, (a, b) => ul[a].CompareTo(ul[b]));
		StringBuilder sb = new StringBuilder();
		int reused = 0;
		for (int i = 0; i < n; i++)
		{
			Slot s;
			if (take >= n) s = chosen[i];
			else { s = chosen[(int)((long)i * take / n)]; reused++; }
			slots[group[ui[i]].GetInstanceID()] = s.pos;
			XYZ v = XYZ.FromVector3(s.pos);
			if (i > 0) sb.Append(' ');
			sb.Append(v.x).Append(',').Append(v.y).Append(',').Append(v.z);
		}
		for (int i = take; i < pool.Count; i++) spare.Add(pool[i].pos);
		BtcLog.Info("JumpLine", "units=" + n + " slots=" + pool.Count + " rows=" + rows + " dir=" + d.x.ToString("0.00") + "," + d.z.ToString("0.00") + " target=" + t.x + "," + t.y + "," + t.z + " voxels=" + sb);
		if (reused > 0) BtcLog.Warn("JumpLine", "not enough walkable blocks: " + reused + " unit(s) share a block");
	}
}
}
