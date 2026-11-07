using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Brix.Engine;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.Components.Volumes;
using Brix.Input;
using Brix.Legacy;
using Motus.Behavior;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
public static class BtcQol
{
	public static string T(string en, string ru)
	{
		return BtcLang.Russian ? ru : en;
	}

	public static void AddRows()
	{
		BtcGameplay.Add("mov.items", "mov", "Ignore items blocking paths", "Игнорировать блокировку пути предметами", 0f, 0f, 1f, 1f, true, "Bricktrons walk through free blocks (logs, bricks, planks, ingots): no pushing, no blocking.", "Бриктроны ходят сквозь свободные блоки (брёвна, кирпичи, доски, слитки): не толкают их и не блочатся.");
		BtcGameplay.Add("mov.allies", "mov", "Pass through allies", "Проходить через своих", 0f, 0f, 1f, 1f, true, "Units walk through allied units in every path search (including attack-move orders). Fixes units standing AFK when several share a block.", "Юниты проходят через союзников во всех поисках пути (включая приказы атаки). Лечит афк-стояние, когда несколько юнитов в одном блоке.");
		BtcGameplay.Add("mov.retry", "mov", "Retry orders after path failure", "Повторять приказ после сбоя пути", 0f, 0f, 1f, 1f, true, "If the path to a player's order is not found or gets blocked, the order is re-sent instead of silently dropped. Stops when the player gives a new order.", "Если путь к приказу игрока не найден или заблокирован, приказ отправляется заново, а не молча сбрасывается. Прекращается, когда игрок даёт новый приказ.");
		BtcGameplay.Add("mov.cancel", "mov", "Cancel animation on run order", "Отменять анимацию приказом бега", 0f, 0f, 1f, 1f, true, "A run order resets every action animation (shooting, fighting, working, taking...) and starts running at once. Falling, ladders, traps and stun are not touched.", "Приказ бега сбрасывает любую анимацию действия (стрельба, бой, работа, подбор...) и сразу начинает бег. Падение, лестницы, ловушки и стан не трогаются.");
		BtcGameplay.Add("mov.repath", "mov", "Smart detour around obstacles", "Умный обход препятствий", 1f, 0f, 1f, 1f, true, "A blocked unit tries farther points of its path (not only 3 steps ahead) and searches deeper, so big obstacles are bypassed without stopping and a full path recalculation.", "Заблокированный юнит пробует более дальние точки своего пути (а не только на 3 шага вперёд) и ищет глубже, поэтому большие препятствия обходятся без остановки и полного пересчёта пути.");
		BtcGameplay.Add("mov.adv", "mov", "Advanced (retry settings)", "Расширенно (настройки повтора)", 0f, 0f, 1f, 1f, true, "Shows the retry sliders.", "Показывает слайдеры повтора.");
		BtcGameplay.Add("mov.retryMax", "mov", "Retry: max attempts (0 = unlimited)", "Повтор: макс. попыток (0 = без лимита)", 0f, 0f, 1000f, 1f, false, "0 = retry until the player gives a new order.", "0 = повторять, пока игрок не даст новый приказ.");
		BtcGameplay.Add("mov.retryDelay", "mov", "Retry: pause (ms)", "Повтор: пауза (мс)", 500f, 100f, 5000f, 50f, false, "Pause before each retry. Lower = faster reaction, more pathfinding load.", "Пауза перед каждым повтором. Меньше = быстрее реакция, больше нагрузка на поиск пути.");
		BtcGameplay.Add("mov.repathSteps", "mov", "Detour search depth", "Глубина поиска обхода", 300f, 100f, 3000f, 50f, false, "Search steps per attempt (vanilla: 100). Higher = bypasses bigger obstacles; costs CPU only while a unit is blocked.", "Шагов поиска за попытку (в ванили 100). Больше = обходит крупные препятствия; нагрузка только пока юнит заблокирован.");
		BtcGameplay.Defs[BtcGameplay.Idx("mov.retryMax")].Adv = 2;
		BtcGameplay.Defs[BtcGameplay.Idx("mov.retryDelay")].Adv = 2;
		BtcGameplay.Defs[BtcGameplay.Idx("mov.repathSteps")].Adv = 2;
		BtcSimplePhys.AddRows();
		BtcJumpLine.AddRows();
		BtcAutoPick.AddRows();
		BtcChopBlock1x1.AddRows();
		BtcTimer.AddRows();
		BtcNoSkip.AddRows();
		BtcCraftQueue.AddRows();
		BtcShootDown.AddRows();
	}

	private static bool On(ref int idx, string id)
	{
		bool flag = idx == -2;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			idx = BtcGameplay.Idx(id);
		}
		float[] active = BtcGameplay.Active;
		return idx >= 0 && active != null && idx < active.Length && active[idx] > 0.5f;
	}

	private static float Val(ref int idx, string id, float def)
	{
		bool flag = idx == -2;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			idx = BtcGameplay.Idx(id);
		}
		float[] active = BtcGameplay.Active;
		return (idx >= 0 && active != null && idx < active.Length) ? active[idx] : def;
	}

	public static bool AdvShown(float[] shown)
	{
		bool flag = BtcQol.iAdv == -2;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			BtcQol.iAdv = BtcGameplay.Idx("mov.adv");
		}
		return BtcQol.iAdv >= 0 && shown != null && BtcQol.iAdv < shown.Length && shown[BtcQol.iAdv] > 0.5f;
	}

	public static bool CamLock
	{
		get
		{
			bool flag = !BtcQol._camInit;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			if (flag4)
			{
				BtcQol._camInit = true;
				BtcQol._cam = (PlayerPrefs.GetInt("btc.camlock", 1) != 0);
			}
			return BtcQol._cam;
		}
	}

	public static void SetCamLock(bool v)
	{
		BtcQol._camInit = true;
		BtcQol._cam = v;
		PlayerPrefs.SetInt("btc.camlock", v ? 1 : 0);
		CameraScript instance = CameraScript.Instance;
		bool flag = instance != null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			instance.ToggleEditorCamera(v);
		}
	}

	public static bool AlliesPass
	{
		get
		{
			return BtcQol.On(ref BtcQol.iAllies, "mov.allies");
		}
	}

	public static bool ItemsOn
	{
		get
		{
			bool flag = BtcQol.On(ref BtcQol.iItems, "mov.items");
			bool flag2 = flag != BtcQol._applied && Time.unscaledTime >= BtcQol._nextTry;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			if (flag5)
			{
				BtcQol.ApplyPhysics(flag);
			}
			return flag;
		}
	}

	private static void ApplyPhysics(bool v)
	{
		BtcQol._nextTry = Time.unscaledTime + 2f;
		bool flag = BtcQol._unitLayer < 0;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			Locomotion4 locomotion = UnityEngine.Object.FindObjectOfType<Locomotion4>();
			bool flag5 = locomotion != null && locomotion.RegularCollider != null;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				BtcQol._unitLayer = locomotion.RegularCollider.layer;
			}
		}
		bool flag9 = BtcQol._unitLayer < 0 || BtcQol._itemMask == 0;
		bool flag10 = !flag9;
		bool flag11 = flag10;
		bool flag12 = flag11;
		if (flag12)
		{
			for (int i = 0; i < 32; i++)
			{
				bool flag13 = (BtcQol._itemMask & 1 << i) != 0 && i != BtcQol._unitLayer;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					Physics.IgnoreLayerCollision(BtcQol._unitLayer, i, v);
				}
			}
			BtcQol._applied = v;
		}
	}

	public static Node PathRetry(Task t, BaseLabor worker)
	{
		bool flag = !BtcQol.On(ref BtcQol.iRetry, "mov.retry") || worker == null || t.InitialPackage == null || t.InitialPackage.DecisionSource != Labor.DecisionSource.User;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		Node result;
		if (flag4)
		{
			result = null;
		}
		else
		{
			int ms = (int)BtcQol.Val(ref BtcQol.iDelay, "mov.retryDelay", 500f);
			result = Node.Sequence.Do(Node.WaitForMilliseconds(ms)).Do(delegate(Task self)
			{
				BtcQol.Resend(self);
			}, t);
		}
		return result;
	}

	private static void Resend(Task t)
	{
		Labor labor = t.Worker as Labor;
		bool flag = labor == null || t.InitialPackage == null || labor.PendingTask != null || labor.CurrentTask != t;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			int num = (int)BtcQol.Val(ref BtcQol.iMax, "mov.retryMax", 0f);
			bool flag5 = num > 0;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				int instanceID = labor.GetInstanceID();
				float[] array;
				bool flag9 = !BtcQol.Retries.TryGetValue(instanceID, out array);
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				if (flag12)
				{
					array = new float[2];
					BtcQol.Retries[instanceID] = array;
				}
				float num2 = BtcQol.Val(ref BtcQol.iDelay, "mov.retryDelay", 500f) / 1000f;
				bool flag13 = Time.time - array[1] > 10f + num2 * 3f;
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					array[0] = 0f;
				}
				array[1] = Time.time;
				array[0] += 1f;
				bool flag17 = array[0] > (float)num;
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				if (flag20)
				{
					return;
				}
			}
			t.InitialPackage.Send(labor);
		}
	}

	public static void OnRunOrder(Labor l)
	{
		bool flag = !BtcQol.On(ref BtcQol.iCancel, "mov.cancel") || l == null;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			Task currentTask = l.CurrentTask;
			bool flag5 = currentTask == null || currentTask.InitialPackage == null || currentTask.InitialPackage.DecisionSource != Labor.DecisionSource.User;
			bool flag6 = !flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				string description = currentTask.Description;
				bool flag9 = description != "RunToward" && description != "RunAggressivelyToward";
				bool flag10 = !flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				if (flag12)
				{
					Locomotion4 locomotion = l.navigation.Locomotion;
					bool flag13 = locomotion == null || locomotion.animator == null || locomotion.controller == null;
					bool flag14 = !flag13;
					bool flag15 = flag14;
					bool flag16 = flag15;
					if (flag16)
					{
						GridLocomotionControllerInterface controller = locomotion.controller;
						bool flag17 = BtcQol.Keep(controller.InPlace) || BtcQol.Keep(controller.RunCycle) || BtcQol.Keep(controller.WalkCycle) || BtcQol.Keep(controller.Halting);
						bool flag18 = !flag17;
						bool flag19 = flag18;
						bool flag20 = flag19;
						if (flag20)
						{
							bool flag21 = BtcQol.Keep(controller.Flailing) || BtcQol.Keep(controller.Stabilizing) || BtcQol.Keep(controller.StandingUp) || BtcQol.Keep(controller.Hurting) || BtcQol.Keep(controller.Stunned) || BtcQol.Keep(controller.Unburrow) || BtcQol.Keep(controller.ClimbUp) || BtcQol.Keep(controller.StepDown);
							bool flag22 = !flag21;
							bool flag23 = flag22;
							bool flag24 = flag23;
							if (flag24)
							{
								bool flag25 = BtcQol.Keep(controller.Trap) || BtcQol.Keep(controller.GettingTrapped) || BtcQol.Keep(controller.Trapped) || BtcQol.Keep(controller.LeavingTrap);
								bool flag26 = !flag25;
								bool flag27 = flag26;
								bool flag28 = flag27;
								if (flag28)
								{
									bool flag29 = BtcQol.Keep(controller.JumpingOff) || BtcQol.Keep(controller.JumpingOffJumping) || BtcQol.Keep(controller.JumpingOffFalling) || BtcQol.Keep(controller.JumpingOffLanding);
									bool flag30 = !flag29;
									bool flag31 = flag30;
									bool flag32 = flag31;
									if (flag32)
									{
										bool flag33 = BtcQol.Keep(controller.Ladder) || BtcQol.Keep(controller.LadderGoingDownStart) || BtcQol.Keep(controller.LadderGoingDownCycle) || BtcQol.Keep(controller.LadderGoingDownEnd) || BtcQol.Keep(controller.LadderGoingUpStart) || BtcQol.Keep(controller.LadderGoingUpCycle) || BtcQol.Keep(controller.LadderGoingUpEnd);
										bool flag34 = !flag33;
										bool flag35 = flag34;
										bool flag36 = flag35;
										if (flag36)
										{
											locomotion.GetReadyToMove();
											controller.IsTakingArcheryStance.Set(false);
											controller.IsAiming.Set(false);
											controller.IsShooting.Set(false);
											controller.IsAbortingStrike.Set(false);
											controller.IsTurningOnSpotArchery.Set(false);
											controller.IsTakingCombatStance.Set(false);
											controller.IsAttacking.Set(false);
											controller.IsRunStriking.Set(false);
											controller.IsShieldBlocking.Set(false);
											controller.IsTurningOnSpotCombat.Set(false);
											controller.IsCombatStepping.Set(false);
											controller.IsSitting.Set(false);
											controller.IsTaking.Set(false);
											controller.IsTakingOneHand.Set(false);
											controller.HittingBrick.Set(false);
											controller.IsClosingStepStart.Set(false);
											controller.IsClosingStepEnd.Set(false);
											controller.IsTurningOnSpot.Set(false);
											bool flag37 = locomotion.layerActivator != null;
											bool flag38 = flag37;
											bool flag39 = flag38;
											bool flag40 = flag39;
											if (flag40)
											{
												locomotion.layerActivator.Deactivate(1);
												locomotion.layerActivator.Deactivate(2);
											}
											int stateHash = controller.InPlace.StateHash;
											locomotion.animator.Play(stateHash, 0, 0f);
											locomotion.animator.Update(0f);
											UNetAnimator component = l.GetComponent<UNetAnimator>();
											bool flag41 = component != null && NetworkServer.active;
											bool flag42 = flag41;
											bool flag43 = flag42;
											bool flag44 = flag43;
											if (flag44)
											{
												component.Play(stateHash);
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

	static BtcQol()
	{
		BtcQol.RepathAhead = 3;
		BtcQol.AheadVanilla = new int[]
		{
			3
		};
		BtcQol.AheadFar = new int[]
		{
			3,
			10,
			25
		};
		BtcQol.iItems = -2;
		BtcQol.iAllies = -2;
		BtcQol.iRetry = -2;
		BtcQol.iCancel = -2;
		BtcQol.iAdv = -2;
		BtcQol.iMax = -2;
		BtcQol.iDelay = -2;
		BtcQol.iRepathOn = -2;
		BtcQol.iRepathSteps = -2;
		BtcQol._cam = true;
		BtcQol._unitLayer = -1;
		BtcQol.ItemPT = new BtcQol.ItemPassThrough();
		BtcQol.Retries = new Dictionary<int, float[]>();
	}

	public static IPassThrough ItemPass(Volume v)
	{
		bool flag = v.GetComponent<BlocLibre>() == null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		IPassThrough result;
		if (flag4)
		{
			result = PassThroughObstacle.Null;
		}
		else
		{
			BtcQol._itemMask |= 1 << v.gameObject.layer;
			result = BtcQol.ItemPT;
		}
		return result;
	}

	private static bool Keep(AnimatorStateProgress s)
	{
		return s != null && s.IsActive();
	}

	public static void NoteItem(GameObject go)
	{
		BtcQol._itemMask |= 1 << go.layer;
		BtcSimplePhys.Item(go);
	}

	public static bool RepathOn
	{
		get
		{
			return BtcQol.On(ref BtcQol.iRepathOn, "mov.repath");
		}
	}

	public static int RepathSteps
	{
		get
		{
			return BtcQol.RepathOn ? ((int)BtcQol.Val(ref BtcQol.iRepathSteps, "mov.repathSteps", 300f)) : 100;
		}
	}

	public static void LogBlocked(string unit, Vector3 pos)
	{
		bool flag = !BtcConfig.LogMove.Value;
		bool flag2 = !flag;
		bool flag3 = flag2;
		if (flag3)
		{
			try
			{
				StackTrace stackTrace = new StackTrace(2, false);
				StringBuilder stringBuilder = new StringBuilder();
				stringBuilder.Append(DateTime.Now.ToString("HH:mm:ss.fff")).Append(' ').Append(unit).Append(' ').Append(pos.ToString("F1")).Append(" <-");
				int num = Math.Min(stackTrace.FrameCount, 4);
				for (int i = 0; i < num; i++)
				{
					MethodBase method = stackTrace.GetFrame(i).GetMethod();
					stringBuilder.Append(' ').Append((method.DeclaringType != null) ? method.DeclaringType.Name : "?").Append('.').Append(method.Name);
				}
				stringBuilder.AppendLine();
				Directory.CreateDirectory(BtcPaths.Root);
				File.AppendAllText(System.IO.Path.Combine(BtcPaths.Root, "qol_blocked.log"), stringBuilder.ToString());
			}
			catch
			{
			}
		}
	}

	public static bool LogOn
	{
		get { return BtcConfig.LogMove.Value; }
	}

	public static void LogNote(string s)
	{
		bool flag = !BtcConfig.LogMove.Value;
		bool flag2 = !flag;
		if (flag2)
		{
			try
			{
				Directory.CreateDirectory(BtcPaths.Root);
				File.AppendAllText(System.IO.Path.Combine(BtcPaths.Root, "qol_blocked.log"), DateTime.Now.ToString("HH:mm:ss.fff") + " " + s + "\r\n");
			}
			catch
			{
			}
		}
	}

	public static void LogWhy(Locomotion4 l)
	{
		bool flag = !BtcConfig.LogMove.Value;
		if (!flag)
		{
			try
			{
				string text = l.name + " why:";
				bool hasCurrent = l.PathIterator.HasCurrent;
				if (hasCurrent)
				{
					WalkNode walkNode = l.PathIterator.Current;
					text = string.Concat(new object[]
					{
						text,
						" cur=",
						walkNode.Position,
						" idx=",
						l.PathIterator.CurrentIndex,
						" mustFail=",
						walkNode.MustFail().ToString(),
						" pathNode=",
						(walkNode.PathNode == null) ? "null" : (walkNode.PathNode.IsEmpty ? "EMPTY" : "ok")
					});
				}
				else
				{
					text = text + " noCurrent dest=" + ((l.PathIterator.Destination == null) ? "null" : l.PathIterator.Destination.Position.ToString());
				}
				XYZ positionEntering = l.PositionEntering;
				VoxelPresence voxelPresence = (positionEntering == XYZ.zero) ? null : VoxelPresence.GetPresenceAt(positionEntering).FirstOrDefault();
				text = text + " entering=" + positionEntering;
				bool flag2 = voxelPresence == null;
				if (flag2)
				{
					text += " blocker=none";
				}
				else
				{
					text = string.Concat(new string[]
					{
						text,
						" blocker=",
						voxelPresence.name,
						(voxelPresence == l.presence) ? "(self)" : "",
						" allied=",
						Affiliation.IsAllied(l.faction, voxelPresence.faction).ToString(),
						" idle=",
						(!voxelPresence.Locomotion.PathIterator.HasCurrent).ToString()
					});
				}
				bool flag3;
				if (positionEntering != XYZ.zero)
				{
					flag3 = VolumeRegistry.HasVolumesWithPropertyAt<GameObject>(positionEntering, VProperty.obstacle, (IVolume v, GameObject g) => !v.PassThrough.CanPassThrough(g), l.gameObject);
				}
				else
				{
					flag3 = false;
				}
				bool flag4 = flag3;
				text = text + " obstacle=" + flag4.ToString();
				BtcQol.LogNote(text);
			}
			catch (Exception ex)
			{
				BtcQol.LogNote("LogWhy error: " + ex.Message);
			}
		}
	}

	private static int iItems;

	private static int iAllies;

	private static int iRetry;

	private static int iCancel;

	private static int iAdv;

	private static int iMax;

	private static int iDelay;

	private static bool _camInit;

	private static bool _cam;

	private static bool _applied;

	private static float _nextTry;

	private static int _unitLayer;

	private static int _itemMask;

	private static readonly Dictionary<int, float[]> Retries;

	private static readonly BtcQol.ItemPassThrough ItemPT;

	public static int RepathAhead;

	public static readonly int[] AheadVanilla;

	public static readonly int[] AheadFar;

	private static int iRepathOn;

	private static int iRepathSteps;

	private static int iLog = -2;

	public sealed class ItemPassThrough : IPassThrough
	{
		public bool IsWalkable()
		{
			return false;
		}

		public bool CanSearchThrough(GameObject character)
		{
			return BtcQol.ItemsOn;
		}

		public bool CanPassThrough(GameObject character)
		{
			return BtcQol.ItemsOn;
		}

		public int GetNiveauxAt(XYZ voxel)
		{
			return 0;
		}

		public float GetWalkHeight(XYZ voxel, float dx, float dz)
		{
			return 0f;
		}
	}
}
}
