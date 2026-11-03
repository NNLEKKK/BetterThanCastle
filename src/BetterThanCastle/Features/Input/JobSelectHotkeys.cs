using System;
using System.Collections.Generic;
using Brix.Game.AI;
using Brix.UI.GameSelector;
using UnityEngine;
using BetterThanCastle;

namespace Brix.Input
{
	public class JobSelectHotkeys : MonoBehaviour
	{
		private void Update()
		{
			for (int i = 0; i < JobSelectHotkeys.AllJobs.Length; i++)
			{
				Occupation.Job job = JobSelectHotkeys.AllJobs[i];
				KeyCode[] jobKeys = BtcHotkeys.GetJobKeys(job, JobSelectHotkeys.Defaults[job]);
				bool flag = !BtcHotkeys.IsSuppressed(jobKeys) && JobSelectHotkeys.IsComboPressed(jobKeys);
				bool flag2 = flag;
				bool flag3 = flag2;
				bool flag4 = flag3;
				if (flag4)
				{
					JobSelectHotkeys.SelectAllOfJob(job);
				}
			}
		}

		private static bool IsComboPressed(KeyCode[] keys)
		{
			bool flag = keys == null || keys.Length == 0;
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
				for (int i = 0; i < keys.Length - 1; i++)
				{
					bool flag5 = !UnityEngine.Input.GetKey(keys[i]);
					bool flag6 = flag5;
					bool flag7 = flag6;
					bool flag8 = flag7;
					if (flag8)
					{
						return false;
					}
				}
				result = (UnityEngine.Input.GetKeyDown(keys[keys.Length - 1]) && !BtcHotkeys.ExtraModifiersHeld(keys));
			}
			return result;
		}

		private static void SelectAllOfJob(Occupation.Job job)
		{
			List<GameObject> list = new List<GameObject>();
			foreach (Labor labor in UIGameObserver.bricktrons.CheckCreation())
			{
				bool flag = labor != null && labor.Occupation != null && labor.Occupation.CurrentJob == job;
				bool flag2 = flag;
				bool flag3 = flag2;
				bool flag4 = flag3;
				if (flag4)
				{
					list.Add(labor.gameObject);
				}
			}
			bool flag5 = list.Count > 0;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				UIGameSelector.Select(list);
			}
		}

		private static readonly Occupation.Job[] AllJobs = new Occupation.Job[]
		{
			Occupation.Job.Builder,
			Occupation.Job.Halberdier,
			Occupation.Job.Knight,
			Occupation.Job.Archer,
			Occupation.Job.Arbalist,
			Occupation.Job.Alchemist,
			Occupation.Job.Artificer
		};

		private static readonly Dictionary<Occupation.Job, KeyCode[]> Defaults = new Dictionary<Occupation.Job, KeyCode[]>
		{
			{
				Occupation.Job.Builder,
				new KeyCode[]
				{
					KeyCode.G
				}
			},
			{
				Occupation.Job.Halberdier,
				new KeyCode[]
				{
					KeyCode.Z
				}
			},
			{
				Occupation.Job.Knight,
				new KeyCode[]
				{
					KeyCode.Space
				}
			},
			{
				Occupation.Job.Archer,
				new KeyCode[]
				{
					KeyCode.X
				}
			},
			{
				Occupation.Job.Arbalist,
				new KeyCode[]
				{
					KeyCode.F
				}
			},
			{
				Occupation.Job.Alchemist,
				new KeyCode[]
				{
					KeyCode.R
				}
			},
			{
				Occupation.Job.Artificer,
				new KeyCode[]
				{
					KeyCode.CapsLock
				}
			}
		};
	}
}
