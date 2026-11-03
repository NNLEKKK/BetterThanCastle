using System;
using System.Collections.Generic;
using System.Reflection;
using Brix.Engine;
using Brix.Game.AI;
using Brix.Game.AI.Pathfinding;
using Brix.Pathfinding.Test;
using UnityEngine;

namespace BetterThanCastle
{
// Group jump: each selected unit gets its own block in a line through the clicked target.
// Client-side only: changes the Vector3 in vanilla CmdGoAndJumpTo (wire format unchanged).
public static class BtcJumpLine
{
	private static int iOn = -2;
	private static int frame = -1;
	private static readonly Dictionary<int, Vector3> slots = new Dictionary<int, Vector3>();
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

	// Called from the GoAndJumpTo prefix for every labor of one click (same frame).
	public static void Adjust(Labor labor, ref Vector3 position)
	{
		if (labor == null) return;
		if (Time.frameCount != frame)
		{
			frame = Time.frameCount;
			slots.Clear();
			Build(position);
		}
		Vector3 v;
		if (slots.TryGetValue(labor.GetInstanceID(), out v)) position = v;
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

	private static void Build(Vector3 target)
	{
		List<Labor> group = Group();
		if (group == null || group.Count < 2) return;
		int n = group.Count;
		Vector3 c = Vector3.zero;
		for (int i = 0; i < n; i++) c += group[i].transform.position;
		c /= n;
		Vector3 d = target - c;
		bool alongZ = Mathf.Abs(d.x) >= Mathf.Abs(d.z);
		XYZ t = XYZ.FromVector3(target);
		List<float> lat = new List<float>();
		List<Vector3> pos = new List<Vector3>();
		int limit = n + 6;
		for (int k = 0; k <= limit * 2 && pos.Count < n; k++)
		{
			int off = (k % 2 == 1) ? (k + 1) / 2 : -(k / 2);
			// order 0, +1, -1, +2, -2 ...
			off = (k == 0) ? 0 : ((k % 2 == 1) ? (k + 1) / 2 : -(k / 2));
			XYZ p = alongZ ? new XYZ(t.x, t.y, t.z + off) : new XYZ(t.x + off, t.y, t.z);
			WalkablePathNode node = PathNodeRegistry.WalkableAt(p);
			if (node == null || node.IsEmpty) continue;
			lat.Add(off);
			pos.Add(node.RepresentativePosition.ToVector3());
		}
		if (pos.Count == 0) return;
		int m = pos.Count;
		// sort slots by lateral coordinate
		int[] si = new int[m];
		for (int i = 0; i < m; i++) si[i] = i;
		Array.Sort(si, (a, b) => lat[a].CompareTo(lat[b]));
		// sort units by lateral coordinate of their position
		float[] ul = new float[n];
		int[] ui = new int[n];
		for (int i = 0; i < n; i++)
		{
			Vector3 up = group[i].transform.position;
			ul[i] = alongZ ? up.z : up.x;
			ui[i] = i;
		}
		Array.Sort(ui, (a, b) => ul[a].CompareTo(ul[b]));
		for (int i = 0; i < n; i++)
		{
			// spread units across the available slots; extra units share slots proportionally
			int s = m >= n ? i : (int)((long)i * m / n);
			if (m >= n)
			{
				// center the chosen slots: take the n lowest-|offset| ones, already sorted by lateral
				s = i;
			}
			slots[group[ui[i]].GetInstanceID()] = pos[si[s]];
		}
	}
}
}
