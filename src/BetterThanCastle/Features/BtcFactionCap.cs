using System;
using System.Collections.Generic;
using Brix.Game;
using Brix.Game.Components;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
public static class BtcFactionCap
{
	public static bool SeeAdd(LocalFactionVision v, int id)
	{
		bool flag = id < 8;
		bool result;
		if (flag)
		{
			result = v.FactionsSeeingMe.Add(id);
		}
		else
		{
			bool flag2 = v == null || id > 39;
			if (flag2)
			{
				result = false;
			}
			else
			{
				uint num = 1U << id - 8;
				uint num2;
				BtcFactionCap._ext.TryGetValue(v, out num2);
				bool flag3 = (num2 & num) > 0U;
				if (flag3)
				{
					result = false;
				}
				else
				{
					BtcFactionCap._ext[v] = (num2 | num);
					result = true;
				}
			}
		}
		return result;
	}

	public static bool SeeRemove(LocalFactionVision v, int id)
	{
		bool flag = id < 8;
		bool result;
		if (flag)
		{
			result = v.FactionsSeeingMe.Remove(id);
		}
		else
		{
			bool flag2 = v == null || id > 39;
			if (flag2)
			{
				result = false;
			}
			else
			{
				uint num = 1U << id - 8;
				uint num2;
				bool flag3 = !BtcFactionCap._ext.TryGetValue(v, out num2) || (num2 & num) == 0U;
				if (flag3)
				{
					result = false;
				}
				else
				{
					num2 &= ~num;
					bool flag4 = num2 == 0U;
					if (flag4)
					{
						BtcFactionCap._ext.Remove(v);
					}
					else
					{
						BtcFactionCap._ext[v] = num2;
					}
					result = true;
				}
			}
		}
		return result;
	}

	public static bool SeeHas(LocalFactionVision v, int id)
	{
		bool flag = id < 8;
		bool result;
		if (flag)
		{
			result = v.FactionsSeeingMe.Contains(id);
		}
		else
		{
			bool flag2 = v == null || id > 39;
			uint num;
			result = (!flag2 && BtcFactionCap._ext.TryGetValue(v, out num) && (num & 1U << id - 8) > 0U);
		}
		return result;
	}

	public static void SeeClear(LocalFactionVision v)
	{
		bool flag = v != null;
		if (flag)
		{
			BtcFactionCap._ext.Remove(v);
		}
	}

	public static void OnReturn(User u, Faction f)
	{
		try
		{
			bool flag = !NetworkServer.active || u == null || f == null;
			if (!flag)
			{
				for (int i = f.OwnerSyncList.Count - 1; i >= 0; i--)
				{
					bool flag2 = f.OwnerSyncList[i] == null;
					if (flag2)
					{
						f.OwnerSyncList.RemoveAt(i);
					}
				}
				f.AddOwner(u);
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning("BtcFactionCap.OnReturn: " + ex.Message);
		}
	}

	public const int Slots = 16;

	private static readonly Dictionary<LocalFactionVision, uint> _ext = new Dictionary<LocalFactionVision, uint>();
}
}
