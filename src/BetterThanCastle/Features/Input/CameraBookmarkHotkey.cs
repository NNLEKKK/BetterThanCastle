using System;
using System.Collections.Generic;
using Brix.Engine.Abstract;
using Brix.Game.AI;
using Brix.Game.Components;
using Brix.Game.Rules;
using Brix.Legacy;
using Brix.UI.GameSelector;
using Brix.UI.Icons;
using Brix.Util;
using UnityEngine;
using BetterThanCastle;

namespace Brix.Input
{
	public class CameraBookmarkHotkey : MonoBehaviour
	{
		private void Start()
		{
			// Added from UIGameObserver.Start: when that happens after the load, this is the "map ready" moment.
			if (!BtcLoadBoost.Loading)
			{
				CameraBookmarkHotkey.OnLevelReady("start");
			}
		}

		private void Update()
		{
			// End of a map load (LevelReady may fire before this component exists, so the load flag is watched too).
			bool loading = BtcLoadBoost.Loading;
			if (CameraBookmarkHotkey._wasLoading && !loading)
			{
				CameraBookmarkHotkey.OnLevelReady("load end");
			}
			CameraBookmarkHotkey._wasLoading = loading;
			CameraScript instance = CameraScript.Instance;
			bool flag = instance == null;
			bool flag2 = !flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			if (flag5)
			{
				this.HandleCycle(instance);
				this.HandleStableSlots(instance);
				this.HandleFastModes(instance);
				this.HandleXRay();
				this.HandleGroups(instance);
				this.HandleGroupAssignFirstEmpty();
				this.HandleGroupCycle(instance);
				this.HandleGroupCycleNoCamera();
				this.HandleGroupClearAll();
				this.HandleAutoGroups();
				this.HandleFocusSelection(instance);
				this.HandleCameraDive(instance);
				this.HandleJumpCursor(instance);
				this.HandleQuickTurn(instance);
				this.HandleMouseTurn(instance);
				this.HandleInstaZoom(instance);
				this.HandleWheel(instance);
				this.WheelStep(instance);
				this.HandleFollow(instance);
				this.HandleHistory(instance);
				this.HandleStartAngle(instance);
				this.HandleMultiCam();
				this.HandleDigVoxel();
				this.HandleBreakObject();
				this.HandleGoDetonate();
				this.HandleShootAt();
				this.HandleFocusMark();
				this.HandlePing();
				BtcStationRadius.Tick(instance);
				this.HandleFocusHold();
				this.HandleDropLuggage();
				this.HandleOverlayToggle();
			}
		}

		private static bool IsComboPressed(KeyCode[] keys)
		{
			bool flag = keys == null || keys.Length == 0;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool result;
			if (flag5)
			{
				result = false;
			}
			else
			{
				bool flag6 = BtcHotkeys.IsSuppressed(keys);
				bool flag7 = flag6;
				bool flag8 = flag7;
				bool flag9 = flag8;
				bool flag10 = flag9;
				if (flag10)
				{
					result = false;
				}
				else
				{
					for (int i = 0; i < keys.Length - 1; i++)
					{
						bool flag11 = !UnityEngine.Input.GetKey(keys[i]);
						bool flag12 = flag11;
						bool flag13 = flag12;
						bool flag14 = flag13;
						bool flag15 = flag14;
						if (flag15)
						{
							return false;
						}
					}
					result = (UnityEngine.Input.GetKeyDown(keys[keys.Length - 1]) && !CameraBookmarkHotkey.ExtraModifiersHeld(keys));
				}
			}
			return result;
		}

		private static bool AllHeld(KeyCode[] keys)
		{
			for (int i = 0; i < keys.Length; i++)
			{
				bool flag = !UnityEngine.Input.GetKey(keys[i]);
				bool flag2 = flag;
				bool flag3 = flag2;
				bool flag4 = flag3;
				bool flag5 = flag4;
				if (flag5)
				{
					return false;
				}
			}
			return true;
		}

		private static bool ContainsKey(KeyCode[] combo, KeyCode key)
		{
			for (int i = 0; i < combo.Length; i++)
			{
				bool flag = combo[i] == key;
				bool flag2 = flag;
				bool flag3 = flag2;
				bool flag4 = flag3;
				bool flag5 = flag4;
				if (flag5)
				{
					return true;
				}
			}
			return false;
		}

		private static bool ExtraModifiersHeld(KeyCode[] combo)
		{
			for (int i = 0; i < CameraBookmarkHotkey.ModifierKeys.Length; i++)
			{
				KeyCode key = CameraBookmarkHotkey.ModifierKeys[i];
				bool flag = UnityEngine.Input.GetKey(key) && !CameraBookmarkHotkey.ContainsKey(combo, key);
				bool flag2 = flag;
				bool flag3 = flag2;
				bool flag4 = flag3;
				bool flag5 = flag4;
				if (flag5)
				{
					return true;
				}
			}
			return false;
		}

		public CameraBookmarkHotkey()
		{
			this._stableCursor = -1;
			this._stableActiveSlot = -1;
			this._fastModeToggleState = new bool[3];
			this._groupCycleCursor = -1;
			this._cycleSelections = new List<GameObject>[2];
			for (int i = 0; i < 2; i++)
			{
				this._cycleSelections[i] = new List<GameObject>();
			}
			this._groups = new List<GameObject>[5];
			this._groupLastPressTime = new float[5];
			for (int j = 0; j < 5; j++)
			{
				this._groups[j] = new List<GameObject>();
				this._groupLastPressTime[j] = -100f;
			}
		}

		private void HandleCycle(CameraScript cam)
		{
			KeyCode[] keys = BtcHotkeys.GetKeys("camera_bookmark_record", CameraBookmarkHotkey.CycleRecordOnlyDefault);
			bool flag = CameraBookmarkHotkey.IsComboPressed(keys);
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			if (flag5)
			{
				this.RecordCycleSlot(cam, false);
			}
			else
			{
				KeyCode[] keys2 = BtcHotkeys.GetKeys("camera_bookmark_cycle", CameraBookmarkHotkey.CycleSwitchDefault);
				bool flag6 = CameraBookmarkHotkey.IsComboPressed(keys2);
				bool flag7 = flag6;
				bool flag8 = flag7;
				bool flag9 = flag8;
				bool flag10 = flag9;
				if (flag10)
				{
					this.RecordCycleSlot(cam, true);
				}
			}
		}

		private void RecordCycleSlot(CameraScript cam, bool teleportAfter)
		{
			this._cycleSlots[this._nextCycleSlot] = CameraBookmarkHotkey.CaptureState(cam);
			List<GameObject> source = new List<GameObject>();
			UIGameSelector.GetSelectedEverything(ref source);
			this._cycleSelections[this._nextCycleSlot] = CameraBookmarkHotkey.FilterToUnits(source);
			this._cycleSlotSet[this._nextCycleSlot] = true;
			int num = 1 - this._nextCycleSlot;
			bool flag = teleportAfter && this._cycleSlotSet[num];
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			if (flag5)
			{
				CameraBookmarkHotkey.ApplyState(cam, this._cycleSlots[num]);
				UIGameSelector.UnselectEverything(null);
				List<GameObject> aliveObjects = CameraBookmarkHotkey.GetAliveObjects(this._cycleSelections[num]);
				bool flag6 = aliveObjects.Count > 0;
				bool flag7 = flag6;
				bool flag8 = flag7;
				bool flag9 = flag8;
				bool flag10 = flag9;
				if (flag10)
				{
					UIGameSelector.Select(aliveObjects);
				}
			}
			this._nextCycleSlot = num;
		}

		private void HandleStableSlots(CameraScript cam)
		{
			for (int i = 0; i < 5; i++)
			{
				KeyCode[] keys = BtcHotkeys.GetKeys(BtcStr.Key("camera_slot_save_", i + 1), CameraBookmarkHotkey.StableSaveDefaults[i]);
				bool flag = CameraBookmarkHotkey.IsComboPressed(keys);
				bool flag2 = flag;
				bool flag3 = flag2;
				bool flag4 = flag3;
				bool flag5 = flag4;
				if (flag5)
				{
					this._stableSlots[i] = CameraBookmarkHotkey.CaptureState(cam);
					this._stableSlotSet[i] = true;
					bool flag6 = this._stableActiveSlot == i;
					bool flag7 = flag6;
					bool flag8 = flag7;
					bool flag9 = flag8;
					bool flag10 = flag9;
					if (flag10)
					{
						this._stableActiveSlot = -1;
					}
				}
				else
				{
					KeyCode[] keys2 = BtcHotkeys.GetKeys(BtcStr.Key("camera_slot_goto_", i + 1), CameraBookmarkHotkey.StableGotoDefaults[i]);
					bool flag11 = CameraBookmarkHotkey.IsComboPressed(keys2) && this._stableSlotSet[i];
					bool flag12 = flag11;
					bool flag13 = flag12;
					bool flag14 = flag13;
					bool flag15 = flag14;
					if (flag15)
					{
						this.GoToStableSlot(cam, i);
					}
				}
			}
			KeyCode[] keys3 = BtcHotkeys.GetKeys("camera_slot_next", CameraBookmarkHotkey.StableNextDefault);
			bool flag16 = CameraBookmarkHotkey.IsComboPressed(keys3);
			bool flag17 = flag16;
			bool flag18 = flag17;
			bool flag19 = flag18;
			bool flag20 = flag19;
			if (flag20)
			{
				this.StepStableCursor(cam, 1);
			}
			KeyCode[] keys4 = BtcHotkeys.GetKeys("camera_slot_prev", CameraBookmarkHotkey.StablePrevDefault);
			bool flag21 = CameraBookmarkHotkey.IsComboPressed(keys4);
			bool flag22 = flag21;
			bool flag23 = flag22;
			bool flag24 = flag23;
			bool flag25 = flag24;
			if (flag25)
			{
				this.StepStableCursor(cam, -1);
			}
		}

		private void GoToStableSlot(CameraScript cam, int i)
		{
			bool flag = this._stableActiveSlot == i;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			if (flag5)
			{
				CameraBookmarkHotkey.ApplyState(cam, this._stableReturnState);
				this._stableActiveSlot = -1;
			}
			else
			{
				this._stableReturnState = CameraBookmarkHotkey.CaptureState(cam);
				CameraBookmarkHotkey.ApplyState(cam, this._stableSlots[i]);
				this._stableActiveSlot = i;
			}
			this._stableCursor = i;
		}

		private void StepStableCursor(CameraScript cam, int direction)
		{
			bool flag = false;
			for (int i = 0; i < 5; i++)
			{
				bool flag2 = this._stableSlotSet[i];
				bool flag3 = flag2;
				bool flag4 = flag3;
				bool flag5 = flag4;
				bool flag6 = flag5;
				if (flag6)
				{
					flag = true;
					break;
				}
			}
			bool flag7 = !flag;
			bool flag8 = !flag7;
			bool flag9 = flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			if (flag11)
			{
				int num = this._stableCursor;
				for (int j = 0; j < 5; j++)
				{
					num = ((num + direction) % 5 + 5) % 5;
					bool flag12 = this._stableSlotSet[num];
					bool flag13 = flag12;
					bool flag14 = flag13;
					bool flag15 = flag14;
					bool flag16 = flag15;
					if (flag16)
					{
						this._stableCursor = num;
						this._stableActiveSlot = -1;
						CameraBookmarkHotkey.ApplyState(cam, this._stableSlots[num]);
						break;
					}
				}
			}
		}

		private static CameraBookmarkHotkey.CamState CaptureState(CameraScript cam)
		{
			CameraBookmarkHotkey.CamState result;
			result.position = cam.transform.position;
			result.rotation = cam.transform.rotation;
			result.distance = cam.Distance;
			return result;
		}

		private static void ApplyState(CameraScript cam, CameraBookmarkHotkey.CamState state)
		{
			cam.Positionner(state.position, state.rotation, state.distance);
		}

		private void HandleFastModes(CameraScript cam)
		{
			bool flag = !this._fastBaseCaptured;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			if (flag5)
			{
				this._fastBaseTranslationMod = cam.TranslationSpeedMod;
				this._fastBaseZoomMod = cam.ZoomSpeedMod;
				this._fastBaseRotationMod = cam.RotationSpeedMod;
				this._fastBaseCaptured = true;
			}
			float num = 1f;
			float num2 = 1f;
			float num3 = 1f;
			for (int i = 0; i < 3; i++)
			{
				int num4 = i + 1;
				KeyCode[] keys = BtcHotkeys.GetKeys(BtcStr.Key("camera_fast_move_", num4), CameraBookmarkHotkey.FastModeKeyDefaults[i]);
				bool flag6 = keys == null || keys.Length == 0;
				bool flag7 = !flag6;
				bool flag8 = flag7;
				bool flag9 = flag8;
				bool flag10 = flag9;
				if (flag10)
				{
					string @string = BtcHotkeys.GetString(BtcStr.Key("camera_fast_move_", num4, "_mode"), "hold");
					bool flag11 = @string.Equals("toggle", StringComparison.OrdinalIgnoreCase);
					bool flag12 = flag11;
					bool flag13 = flag12;
					bool flag14 = flag13;
					bool flag15 = flag14;
					bool flag21;
					if (flag15)
					{
						bool flag16 = CameraBookmarkHotkey.IsComboPressed(keys);
						bool flag17 = flag16;
						bool flag18 = flag17;
						bool flag19 = flag18;
						bool flag20 = flag19;
						if (flag20)
						{
							this._fastModeToggleState[i] = !this._fastModeToggleState[i];
						}
						flag21 = this._fastModeToggleState[i];
					}
					else
					{
						flag21 = (!BtcHotkeys.IsSuppressed(keys) && !CameraBookmarkHotkey.ExtraModifiersHeld(keys) && CameraBookmarkHotkey.AllHeld(keys));
					}
					bool flag22 = flag21;
					bool flag23 = flag22;
					bool flag24 = flag23;
					bool flag25 = flag24;
					bool flag26 = flag25;
					if (flag26)
					{
						num *= BtcHotkeys.GetFloat(BtcStr.Key("camera_fast_move_", num4, "_translation_multiplier"), 2.5f);
						num2 *= BtcHotkeys.GetFloat(BtcStr.Key("camera_fast_move_", num4, "_zoom_multiplier"), 2.5f);
						num3 *= BtcHotkeys.GetFloat(BtcStr.Key("camera_fast_move_", num4, "_orbit_multiplier"), 2.5f);
					}
				}
			}
			cam.TranslationSpeedMod = this._fastBaseTranslationMod * num;
			cam.ZoomSpeedMod = this._fastBaseZoomMod * num2;
			cam.RotationSpeedMod = this._fastBaseRotationMod * num3;
		}

		private void HandleXRay()
		{
			KeyCode[] keys = BtcHotkeys.GetKeys("xray_toggle", CameraBookmarkHotkey.XRayToggleDefault);
			bool flag = CameraBookmarkHotkey.IsComboPressed(keys) && XRayImpl.instance != null;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			if (flag5)
			{
				XRayImpl.instance.enabled = !XRayImpl.instance.enabled;
			}
		}

		private void HandleGroups(CameraScript cam)
		{
			for (int i = 0; i < 5; i++)
			{
				KeyCode[] keys = BtcHotkeys.GetKeys(BtcStr.Key("group_save_", i + 1), CameraBookmarkHotkey.GroupSaveDefaults[i]);
				bool flag = CameraBookmarkHotkey.IsComboPressed(keys);
				bool flag2 = flag;
				bool flag3 = flag2;
				bool flag4 = flag3;
				bool flag5 = flag4;
				if (flag5)
				{
					List<GameObject> source = new List<GameObject>();
					UIGameSelector.GetSelectedEverything(ref source);
					List<GameObject> list = CameraBookmarkHotkey.FilterToUnits(source);
					bool flag6 = list.Count > 0;
					bool flag7 = flag6;
					bool flag8 = flag7;
					bool flag9 = flag8;
					bool flag10 = flag9;
					if (flag10)
					{
						this._groups[i] = list;
					}
				}
				else if (!this.HandleGroupAdd(i))
				{
					KeyCode[] keys2 = BtcHotkeys.GetKeys(BtcStr.Key("group_select_", i + 1), CameraBookmarkHotkey.GroupSelectDefaults[i]);
					bool flag11 = CameraBookmarkHotkey.IsComboPressed(keys2) && this._groups[i].Count > 0;
					bool flag12 = flag11;
					bool flag13 = flag12;
					bool flag14 = flag13;
					bool flag15 = flag14;
					if (flag15)
					{
						List<GameObject> list2 = CameraBookmarkHotkey.FilterToUnits(this._groups[i]);
						bool flag16 = list2.Count == 0;
						bool flag17 = flag16;
						bool flag18 = flag17;
						bool flag19 = flag18;
						bool flag20 = flag19;
						if (flag20)
						{
							this._groups[i] = new List<GameObject>();
							this._groupLastPressTime[i] = -100f;
							bool flag21 = this._groupCycleCursor == i;
							bool flag22 = flag21;
							bool flag23 = flag22;
							bool flag24 = flag23;
							if (flag24)
							{
								this._groupCycleCursor = -1;
							}
						}
						else
						{
							UIGameSelector.UnselectEverything(null);
							UIGameSelector.Select(list2);
							this._groupCycleCursor = i;
							float time = Time.time;
							bool flag25 = time - this._groupLastPressTime[i] <= 0.35f;
							this._groupLastPressTime[i] = time;
							bool flag26 = flag25;
							bool flag27 = flag26;
							bool flag28 = flag27;
							bool flag29 = flag28;
							bool flag30 = flag29;
							if (flag30)
							{
								CameraBookmarkHotkey.JumpCameraTo(cam, list2);
							}
						}
					}
				}
			}
		}

		// Add to group: the "add" modifier held + a group's select key adds the selected units to that group (true = consumed).
		private bool HandleGroupAdd(int i)
		{
			KeyCode[] mod = BtcHotkeys.GetKeys("group_add_modifier", CameraBookmarkHotkey.NoKeys);
			if (mod.Length == 0 || !CameraBookmarkHotkey.AllHeld(mod))
			{
				return false;
			}
			KeyCode[] sel = BtcHotkeys.GetKeys(BtcStr.Key("group_select_", i + 1), CameraBookmarkHotkey.GroupSelectDefaults[i]);
			if (sel == null || sel.Length == 0)
			{
				return false;
			}
			KeyCode[] combo = new KeyCode[mod.Length + sel.Length];
			mod.CopyTo(combo, 0);
			sel.CopyTo(combo, mod.Length);
			if (!CameraBookmarkHotkey.IsComboPressed(combo))
			{
				// the modifier is held: never fall through to a plain group select
				return true;
			}
			List<GameObject> source = new List<GameObject>();
			UIGameSelector.GetSelectedEverything(ref source);
			List<GameObject> add = CameraBookmarkHotkey.FilterToUnits(source);
			List<GameObject> group = CameraBookmarkHotkey.FilterToUnits(this._groups[i]);
			for (int k = 0; k < add.Count; k++)
			{
				if (!group.Contains(add[k]))
				{
					group.Add(add[k]);
				}
			}
			this._groups[i] = group;
			return true;
		}

		private void HandleGroupAssignFirstEmpty()
		{
			KeyCode[] keys = BtcHotkeys.GetKeys("group_save_first_empty", CameraBookmarkHotkey.GroupSaveFirstEmptyDefault);
			bool flag = keys == null || keys.Length == 0;
			bool flag2 = !flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			if (flag5)
			{
				bool flag6 = CameraBookmarkHotkey.IsComboPressed(keys);
				bool flag7 = flag6;
				bool flag8 = flag7;
				bool flag9 = flag8;
				if (flag9)
				{
					List<GameObject> source = new List<GameObject>();
					UIGameSelector.GetSelectedEverything(ref source);
					List<GameObject> list = CameraBookmarkHotkey.FilterToUnits(source);
					bool flag10 = list.Count > 0;
					bool flag11 = flag10;
					bool flag12 = flag11;
					bool flag13 = flag12;
					if (flag13)
					{
						for (int i = 0; i < 5; i++)
						{
							bool flag14 = this._groups[i].Count == 0;
							bool flag15 = flag14;
							bool flag16 = flag15;
							bool flag17 = flag16;
							if (flag17)
							{
								this._groups[i] = list;
								this._groupLastPressTime[i] = -100f;
								break;
							}
						}
					}
				}
			}
		}

		private void HandleGroupCycle(CameraScript cam)
		{
			KeyCode[] keys = BtcHotkeys.GetKeys("group_cycle", CameraBookmarkHotkey.GroupCycleDefault);
			bool flag = keys == null || keys.Length == 0;
			bool flag2 = !flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			if (flag5)
			{
				bool flag6 = !CameraBookmarkHotkey.IsComboPressed(keys);
				bool flag7 = !flag6;
				bool flag8 = flag7;
				bool flag9 = flag8;
				bool flag10 = flag9;
				if (flag10)
				{
					bool flag11 = false;
					for (int i = 0; i < 5; i++)
					{
						bool flag12 = this._groups[i].Count > 0;
						bool flag13 = flag12;
						bool flag14 = flag13;
						bool flag15 = flag14;
						bool flag16 = flag15;
						if (flag16)
						{
							flag11 = true;
							break;
						}
					}
					bool flag17 = !flag11;
					bool flag18 = !flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					bool flag21 = flag20;
					if (flag21)
					{
						int num = this._groupCycleCursor;
						for (int j = 0; j < 5; j++)
						{
							num = (num + 1) % 5;
							bool flag22 = this._groups[num].Count > 0;
							bool flag23 = flag22;
							bool flag24 = flag23;
							bool flag25 = flag24;
							bool flag26 = flag25;
							if (flag26)
							{
								List<GameObject> list = CameraBookmarkHotkey.FilterToUnits(this._groups[num]);
								bool flag27 = list.Count > 0;
								bool flag28 = flag27;
								bool flag29 = flag28;
								bool flag30 = flag29;
								bool flag31 = flag30;
								if (flag31)
								{
									this._groupCycleCursor = num;
									UIGameSelector.UnselectEverything(null);
									UIGameSelector.Select(list);
									CameraBookmarkHotkey.JumpCameraTo(cam, list);
									break;
								}
								this._groups[num] = new List<GameObject>();
								bool flag32 = this._groupCycleCursor == num;
								bool flag33 = flag32;
								bool flag34 = flag33;
								bool flag35 = flag34;
								if (flag35)
								{
									this._groupCycleCursor = -1;
								}
							}
						}
					}
				}
			}
		}

		private void HandleGroupClearAll()
		{
			KeyCode[] keys = BtcHotkeys.GetKeys("group_clear_all", CameraBookmarkHotkey.GroupClearAllDefault);
			bool flag = keys == null || keys.Length == 0;
			bool flag2 = !flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			if (flag5)
			{
				bool flag6 = !CameraBookmarkHotkey.IsComboPressed(keys);
				bool flag7 = !flag6;
				bool flag8 = flag7;
				bool flag9 = flag8;
				bool flag10 = flag9;
				if (flag10)
				{
					for (int i = 0; i < 5; i++)
					{
						this._groups[i] = new List<GameObject>();
						this._groupLastPressTime[i] = -100f;
					}
					this._groupCycleCursor = -1;
				}
			}
		}

		private void HandleAutoGroups()
		{
			KeyCode[] keys = BtcHotkeys.GetKeys("group_auto_assign", CameraBookmarkHotkey.GroupAutoAssignDefault);
			bool flag = keys == null || keys.Length == 0;
			bool flag2 = !flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			if (flag5)
			{
				bool flag6 = !CameraBookmarkHotkey.IsComboPressed(keys);
				bool flag7 = !flag6;
				bool flag8 = flag7;
				bool flag9 = flag8;
				bool flag10 = flag9;
				if (flag10)
				{
					List<int> list = new List<int>();
					for (int i = 0; i < 5; i++)
					{
						bool flag11 = this._groups[i].Count == 0;
						bool flag12 = flag11;
						bool flag13 = flag12;
						bool flag14 = flag13;
						bool flag15 = flag14;
						if (flag15)
						{
							list.Add(i);
						}
					}
					bool flag16 = list.Count == 0;
					bool flag17 = !flag16;
					bool flag18 = flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					if (flag20)
					{
						List<Labor> list2 = new List<Labor>();
						foreach (Labor labor in UIGameObserver.bricktrons.CheckCreation())
						{
							bool flag21 = labor != null;
							bool flag22 = flag21;
							bool flag23 = flag22;
							bool flag24 = flag23;
							bool flag25 = flag24;
							if (flag25)
							{
								list2.Add(labor);
							}
						}
						bool flag26 = list2.Count == 0;
						bool flag27 = !flag26;
						bool flag28 = flag27;
						bool flag29 = flag28;
						bool flag30 = flag29;
						if (flag30)
						{
							float @float = BtcHotkeys.GetFloat("group_auto_cluster_radius", 6f);
							List<List<Labor>> list3 = CameraBookmarkHotkey.ClusterByProximity(list2, @float);
							list3.Sort((List<Labor> a, List<Labor> b) => b.Count.CompareTo(a.Count));
							int num = 0;
							int num2 = 0;
							while (num2 < list3.Count && num < list.Count)
							{
								List<GameObject> list4 = new List<GameObject>();
								for (int j = 0; j < list3[num2].Count; j++)
								{
									list4.Add(list3[num2][j].gameObject);
								}
								int num3 = list[num];
								this._groups[num3] = list4;
								this._groupLastPressTime[num3] = -100f;
								num++;
								num2++;
							}
						}
					}
				}
			}
		}

		private static List<List<Labor>> ClusterByProximity(List<Labor> units, float radius)
		{
			int count = units.Count;
			int[] array = new int[count];
			for (int i = 0; i < count; i++)
			{
				array[i] = i;
			}
			float num = radius * radius;
			for (int j = 0; j < count; j++)
			{
				Vector3 position = units[j].transform.position;
				for (int k = j + 1; k < count; k++)
				{
					Vector3 position2 = units[k].transform.position;
					bool flag = (position - position2).sqrMagnitude <= num;
					bool flag2 = flag;
					bool flag3 = flag2;
					bool flag4 = flag3;
					bool flag5 = flag4;
					if (flag5)
					{
						CameraBookmarkHotkey.Union(array, j, k);
					}
				}
			}
			Dictionary<int, List<Labor>> dictionary = new Dictionary<int, List<Labor>>();
			for (int l = 0; l < count; l++)
			{
				int key = CameraBookmarkHotkey.Find(array, l);
				List<Labor> list;
				bool flag6 = !dictionary.TryGetValue(key, out list);
				bool flag7 = flag6;
				bool flag8 = flag7;
				bool flag9 = flag8;
				bool flag10 = flag9;
				if (flag10)
				{
					list = new List<Labor>();
					dictionary[key] = list;
				}
				list.Add(units[l]);
			}
			return new List<List<Labor>>(dictionary.Values);
		}

		private static int Find(int[] parent, int i)
		{
			while (parent[i] != i)
			{
				parent[i] = parent[parent[i]];
				i = parent[i];
			}
			return i;
		}

		private static void Union(int[] parent, int a, int b)
		{
			int num = CameraBookmarkHotkey.Find(parent, a);
			int num2 = CameraBookmarkHotkey.Find(parent, b);
			bool flag = num != num2;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			if (flag5)
			{
				parent[num] = num2;
			}
		}

		private void HandleDigVoxel()
		{
			KeyCode[] keys = BtcHotkeys.GetKeys("dig_voxel", CameraBookmarkHotkey.DigVoxelDefault);
			bool flag = keys == null || keys.Length == 0;
			bool flag2 = !flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			if (flag5)
			{
				bool flag6 = !CameraBookmarkHotkey.IsComboPressed(keys);
				bool flag7 = !flag6;
				bool flag8 = flag7;
				bool flag9 = flag8;
				bool flag10 = flag9;
				if (flag10)
				{
					Camera main = Camera.main;
					bool flag11 = main == null;
					bool flag12 = !flag11;
					bool flag13 = flag12;
					bool flag14 = flag13;
					bool flag15 = flag14;
					if (flag15)
					{
						Ray ray = main.ScreenPointToRay(UnityEngine.Input.mousePosition);
						int layerMask = 132096;
						RaycastHit raycastHit;
						bool flag16 = !CameraBookmarkHotkey.TerrainRay(ray, layerMask, out raycastHit);
						bool flag17 = !flag16;
						bool flag18 = flag17;
						bool flag19 = flag18;
						bool flag20 = flag19;
						if (flag20)
						{
							Vector3 vector = Calcule.RoundVecteur(raycastHit.point - raycastHit.normal * 0.0001f);
							bool flag21 = !TerrainRules.Main.CanDigAt(vector) || CameraBookmarkHotkey.BuildingOnRay(ray, raycastHit.distance);
							bool flag22 = !flag21;
							bool flag23 = flag22;
							bool flag24 = flag23;
							bool flag25 = flag24;
							if (flag25)
							{
								int scalaire = Voxel.GetScalaire(vector);
								List<GameObject> list = new List<GameObject>();
								UIGameSelector.GetSelectedEverything(ref list);
								for (int i = 0; i < list.Count; i++)
								{
									GameObject gameObject = list[i];
									bool flag26 = gameObject == null;
									bool flag27 = !flag26;
									bool flag28 = flag27;
									bool flag29 = flag28;
									bool flag30 = flag29;
									if (flag30)
									{
										Labor component = gameObject.GetComponent<Labor>();
										bool flag31 = component != null && !component.recepteur.RequiresBothHands();
										bool flag32 = flag31;
										bool flag33 = flag32;
										bool flag34 = flag33;
										bool flag35 = flag34;
										if (flag35)
										{
											DirectOrderProject.AddBreakVoxelGoal(component, scalaire);
										}
									}
								}
							}
						}
					}
				}
			}
		}

		private void HandleOverlayToggle()
		{
			KeyCode[] keys = BtcHotkeys.GetKeys("overlay_toggle", CameraBookmarkHotkey.OverlayToggleDefault);
			bool flag = keys == null || keys.Length == 0;
			bool flag2 = !flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			if (flag5)
			{
				bool flag6 = CameraBookmarkHotkey.IsComboPressed(keys);
				bool flag7 = flag6;
				bool flag8 = flag7;
				bool flag9 = flag8;
				bool flag10 = flag9;
				if (flag10)
				{
					this._overlayVisible = !this._overlayVisible;
				}
			}
		}

		private void OnGUI()
		{
			if (useGUILayout) useGUILayout = false; // no GUILayout here: skip Unity's per-event layout pass (garbage)
			this.EnsureOverlayStyles();
			bool flag = BtcHotkeys.Enabled && this._overlayVisible && BtcReplay.Mode != BtcReplay.State.Playing;
			bool flag2 = flag;
			if (flag2)
			{
				this.DrawOverlay();
			}
			this.DrawAttribution();
			this.DrawQuickTurnArrow();
			BtcFocusMark.DrawIcon();
			BtcFocusFx.Draw();
			BtcStationRadius.Draw();
		}

		private void EnsureOverlayStyles()
		{
			bool overlayStylesReady = this._overlayStylesReady;
			bool flag = !overlayStylesReady;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			if (flag4)
			{
				this._overlayWhiteTex = new Texture2D(1, 1);
				this._overlayWhiteTex.SetPixel(0, 0, Color.white);
				this._overlayWhiteTex.Apply();
				this._overlayCountStyle = new GUIStyle(GUI.skin.label);
				this._overlayCountStyle.alignment = TextAnchor.MiddleLeft;
				this._overlayCountStyle.fontSize = 11;
				this._overlayCountStyle.fontStyle = FontStyle.Bold;
				this._overlayCountStyle.normal.textColor = new Color(1f, 1f, 1f, 0.95f);
				this._overlaySlotTextStyle = new GUIStyle(GUI.skin.label);
				this._overlaySlotTextStyle.alignment = TextAnchor.MiddleCenter;
				this._overlaySlotTextStyle.fontSize = 10;
				this._overlaySlotTextStyle.fontStyle = FontStyle.Bold;
				this._overlaySlotTextStyle.normal.textColor = new Color(0.12f, 0.09f, 0.02f, 1f);
				this._overlayAttributionStyle = new GUIStyle(GUI.skin.label);
				this._overlayAttributionStyle.alignment = TextAnchor.LowerCenter;
				this._overlayAttributionStyle.fontSize = 9;
				this._overlayAttributionStyle.fontStyle = FontStyle.Normal;
				this._overlayAttributionStyle.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
				this._overlayStylesReady = true;
			}
		}

		private static bool _grpOpen = true;   // always expanded (user rule 2026-11-10)

		// control groups (1-5) and camera slots (1-5) in the menu style: compact strip, the groups expand into rows with unit icons on hover
		private void DrawOverlay()
		{
			CameraScript instance = CameraScript.Instance;
			BtcHud.Slot sl = BtcHud.SlotOf("groups");
			if (!sl.Enabled)
			{
				return;
			}
			Event ev = Event.current;
			int fs = Mathf.RoundToInt((float)BtcHud.Fs * sl.Scale);
			BtcReplayUI.Ensure(fs);
			float a = sl.Alpha;
			float cell = (float)fs * 1.9f, gap = (float)fs * 0.2f, pad = (float)fs * 0.4f, rowH = (float)fs * 1.9f;
			float wCol = 5f * cell + 4f * gap + pad * 2f;
			float wExp = (float)fs * 12f + pad * 2f;
			float hCol = pad * 2f + cell * 2f + gap;
			float hExp = pad * 2f + rowH * 5f + gap * 5f + cell;
			float w = _grpOpen ? wExp : wCol, h = _grpOpen ? hExp : hCol;
			Rect panel = new Rect((float)Screen.width - 45f - w + sl.Offset.x, (float)fs * 0.5f + sl.Offset.y, w, h);
			Rect trigger = new Rect((float)Screen.width - 45f - wCol + sl.Offset.x, panel.y, wCol, panel.height);
			Color pc = BtcHud.Panel;
			BtcReplayUI.Fill(panel, new Color(pc.r, pc.g, pc.b, pc.a * a));
			RefreshOverlayGroups();
			List<GameObject> list = _ovSel;
			GUIStyle num = BtcReplayUI.sVal;
			Color oc = GUI.contentColor;
			float y = panel.y + pad;
			for (int i = 0; i < 5; i++)
			{
				List<GameObject> list2 = _ovGroups[i];
				Color color = (list2.Count > 0 && this._groupCycleCursor == i && list.Count == list2.Count) ? CameraBookmarkHotkey.ColorActive : ((list2.Count > 0) ? CameraBookmarkHotkey.ColorOccupied : CameraBookmarkHotkey.ColorEmpty);
				Rect rect = _grpOpen ? new Rect(panel.x + pad, y, panel.width - pad * 2f, rowH) : new Rect(panel.x + pad + (float)i * (cell + gap), y, cell, cell);
				BtcReplayUI.Fill(rect, new Color(color.r, color.g, color.b, (list2.Count > 0 ? 0.22f : 0.07f) * a));
				BtcReplayUI.Fill(new Rect(rect.x, rect.y, 3f, rect.height), new Color(color.r, color.g, color.b, (list2.Count > 0 ? 1f : 0.35f) * a));
				GUI.contentColor = new Color(1f, 1f, 1f, (list2.Count > 0 ? 0.95f : 0.4f) * a);
				GUI.Label(_grpOpen ? new Rect(rect.x + (float)fs * 0.2f, rect.y, (float)fs * 1.4f, rect.height) : rect, BtcStr.Key("", i + 1), num);
				GUI.contentColor = oc;
				if (_grpOpen)
				{
					if (list2.Count > 0)
					{
						this.DrawGroupIcons(new Rect(rect.x + (float)fs * 1.6f, rect.y, rect.width - (float)fs * 1.6f, rect.height), list2, (float)fs * 1.3f);
					}
					y += rowH + gap;
				}
			}
			if (!_grpOpen)
			{
				y += cell + gap;
			}
			float x0 = panel.x + pad;
			for (int j = 0; j < 5; j++)
			{
				Rect rect2 = new Rect(x0 + (float)j * (cell + gap), y, cell, cell);
				bool flag7 = this._stableSlotSet[j] && CameraBookmarkHotkey.IsCameraAtState(instance, this._stableSlots[j]);
				bool flag8 = this._stableActiveSlot == j && flag7;
				Color color2 = flag8 ? CameraBookmarkHotkey.ColorReturnMode : (flag7 ? CameraBookmarkHotkey.ColorActive : (this._stableSlotSet[j] ? CameraBookmarkHotkey.ColorOccupied : CameraBookmarkHotkey.ColorEmpty));
				bool set = this._stableSlotSet[j];
				BtcReplayUI.Fill(rect2, new Color(color2.r, color2.g, color2.b, (set ? 0.22f : 0.07f) * a));
				BtcReplayUI.Fill(new Rect(rect2.x, rect2.yMax - 2f, rect2.width, 2f), new Color(color2.r, color2.g, color2.b, (set ? 1f : 0.35f) * a));
				GUI.contentColor = new Color(1f, 1f, 1f, (set ? 0.95f : 0.4f) * a);
				GUI.Label(rect2, BtcStr.Key("", j + 1), num);
				GUI.contentColor = oc;
				BtcReplayIcons.Hint(rect2, BtcStr.Key(BtcReplayUI.T("Camera slot ", "Слот камеры "), j + 1));
			}
		}

		private void DrawGroupIcons(Rect rowRect, List<GameObject> alive, float iconSize)
		{
			Dictionary<Occupation.Job, int> dictionary = new Dictionary<Occupation.Job, int>();
			List<Occupation.Job> list = new List<Occupation.Job>();
			for (int i = 0; i < alive.Count; i++)
			{
				Labor component = alive[i].GetComponent<Labor>();
				bool flag = component == null || component.Occupation == null;
				bool flag2 = !flag;
				bool flag3 = flag2;
				bool flag4 = flag3;
				bool flag5 = flag4;
				if (flag5)
				{
					Occupation.Job currentJob = component.Occupation.CurrentJob;
					int num;
					bool flag6 = dictionary.TryGetValue(currentJob, out num);
					bool flag7 = flag6;
					bool flag8 = flag7;
					bool flag9 = flag8;
					bool flag10 = flag9;
					if (flag10)
					{
						dictionary[currentJob] = num + 1;
					}
					else
					{
						dictionary[currentJob] = 1;
						list.Add(currentJob);
					}
				}
			}
			float num2 = rowRect.x + 4f;
			float y = rowRect.y + (rowRect.height - iconSize) * 0.5f;
			for (int j = 0; j < list.Count; j++)
			{
				Sprite sprite = CameraBookmarkHotkey.JobIcon(list[j]);
				Rect rect = new Rect(num2, y, iconSize, iconSize);
				bool flag11 = sprite != null;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				if (flag15)
				{
					CameraBookmarkHotkey.DrawSprite(rect, sprite);
				}
				num2 += iconSize + 2f;
				string text = dictionary[list[j]].ToString();
				float num3 = 16f;
				Rect position = new Rect(num2, rowRect.y, num3, rowRect.height);
				GUI.Label(position, text, this._overlayCountStyle);
				num2 += num3 + 4f;
			}
		}

		private static Sprite JobIcon(Occupation.Job job)
		{
			Sprite result;
			switch (job)
			{
			case Occupation.Job.Builder:
				result = IconKeys._Face_Worker.Get32();
				break;
			case Occupation.Job.Halberdier:
				result = IconKeys._Face_Halberdier.Get32();
				break;
			case Occupation.Job.Knight:
				result = IconKeys._Face_Knight.Get32();
				break;
			case Occupation.Job.Archer:
				result = IconKeys._Face_Archer.Get32();
				break;
			case Occupation.Job.Arbalist:
				result = IconKeys._Face_Arbalist.Get32();
				break;
			case Occupation.Job.Alchemist:
				result = IconKeys._Face_Alchemist.Get32();
				break;
			case Occupation.Job.Artificer:
				result = IconKeys._Face_Artificer.Get32();
				break;
			default:
				result = null;
				break;
			}
			return result;
		}

		private static void DrawSprite(Rect rect, Sprite sprite)
		{
			Rect textureRect = sprite.textureRect;
			Texture2D texture = sprite.texture;
			Rect texCoords = new Rect(textureRect.x / (float)texture.width, textureRect.y / (float)texture.height, textureRect.width / (float)texture.width, textureRect.height / (float)texture.height);
			GUI.DrawTextureWithTexCoords(rect, texture, texCoords);
		}

		private void HandleFocusSelection(CameraScript cam)
		{
			KeyCode[] keys = BtcHotkeys.GetKeys("camera_focus_selection", CameraBookmarkHotkey.FocusSelectionDefault);
			bool flag = keys == null || keys.Length == 0;
			bool flag2 = !flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			if (flag5)
			{
				bool flag6 = !CameraBookmarkHotkey.IsComboPressed(keys);
				bool flag7 = !flag6;
				bool flag8 = flag7;
				bool flag9 = flag8;
				bool flag10 = flag9;
				if (flag10)
				{
					List<GameObject> source = new List<GameObject>();
					UIGameSelector.GetSelectedEverything(ref source);
					List<GameObject> aliveObjects = CameraBookmarkHotkey.GetAliveObjects(source);
					bool flag11 = aliveObjects.Count > 0;
					bool flag12 = flag11;
					bool flag13 = flag12;
					bool flag14 = flag13;
					bool flag15 = flag14;
					if (flag15)
					{
						CameraBookmarkHotkey.JumpCameraTo(cam, aliveObjects);
					}
				}
			}
		}

		// Camera in the ground: puts the camera inside the terrain block under the cursor, looking level; a second press goes back.
		private void HandleCameraDive(CameraScript cam)
		{
			KeyCode[] keys = BtcHotkeys.GetKeys("camera_dive", CameraBookmarkHotkey.NoKeys);
			// left the ground some other way (bookmark, jump to selection...): the dive is over
			if (this._diveActive)
			{
				Vector3 now = cam.transform.position;
				// any instant camera move (control group, bookmark, jump to selection, ...) ends the dive; WASD/drag moves are small per frame
				if (now.y > this._diveY + 3f || (this._diveCamId == cam.GetInstanceID() && (now - this._divePos).sqrMagnitude > 9f))
				{
					this._diveActive = false;
				}
				this._divePos = now;
			}
			if (!CameraBookmarkHotkey.IsComboPressed(keys))
			{
				return;
			}
			try
			{
				if (this._diveActive && this._diveCamId == cam.GetInstanceID())
				{
					this._diveActive = false;
					CameraBookmarkHotkey.ApplyState(cam, this._diveReturn);
					return;
				}
				this._diveActive = false;
				Camera main = Camera.main;
				if (main == null)
				{
					return;
				}
				RaycastHit hit;
				if (!CameraBookmarkHotkey.TerrainRay(main.ScreenPointToRay(UnityEngine.Input.mousePosition), 132096, out hit))
				{
					return;
				}
				Vector3 cell = Calcule.RoundVecteur(hit.point - hit.normal * 0.0001f);
				float depth = BtcHotkeys.GetFloat("camera_dive_depth", 0.5f);
				float back = BtcHotkeys.GetFloat("camera_dive_back", 0f);
				float tilt = BtcHotkeys.GetFloat("camera_dive_tilt", 0f);
				float yaw = cam.transform.eulerAngles.y;
				if (!BtcHotkeys.GetBool("camera_dive_keep_yaw", true))
				{
					yaw = Mathf.Round(yaw / 90f) * 90f;
				}
				Vector3 flat = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
				Vector3 pos = new Vector3(cell.x, cell.y + 0.5f - depth, cell.z) - flat * back;
				CameraBookmarkHotkey.RecordCam(cam);
				this._diveReturn = CameraBookmarkHotkey.CaptureState(cam);
				this._diveCamId = cam.GetInstanceID();
				this._diveActive = true;
				this._diveY = pos.y;
				this._divePos = pos;
				cam.Positionner(pos, Quaternion.Euler(tilt, yaw, 0f), 2f);
			}
			catch (Exception ex)
			{
				BtcLog.Error("CamDive", ex.Message);
			}
		}

		// Jump to the cursor: the camera moves right over the block under the cursor (or the mini map point), like a WASD move:
		// angle, zoom and the Shift+scroll level stay. Holding the bind repeats the jump.
		private void HandleJumpCursor(CameraScript cam)
		{
			KeyCode[] keys = BtcHotkeys.GetKeys("camera_jump_cursor", CameraBookmarkHotkey.NoKeys);
			float now = Time.unscaledTime;
			if (CameraBookmarkHotkey.IsComboPressed(keys))
			{
				if (this.DiveBlocks("camera_dive_block_jump"))
				{
					return;
				}
				CameraBookmarkHotkey.RecordCam(cam);
				CameraBookmarkHotkey._follow = null;
				if (BtcHotkeys.GetBool("camera_jump_follow", true) && CameraBookmarkHotkey.StartFollow(cam))
				{
					return;
				}
				CameraBookmarkHotkey.JumpToCursor(cam);
				this._jumpHeld = true;
				this._jumpNext = now + BtcHotkeys.GetFloat("camera_jump_repeat_delay", 250f) / 1000f;
				return;
			}
			if (!this._jumpHeld)
			{
				return;
			}
			if (keys.Length == 0 || !BtcHotkeys.AllHeld(keys))
			{
				this._jumpHeld = false;
				return;
			}
			float every = BtcHotkeys.GetFloat("camera_jump_repeat", 150f) / 1000f;
			if (every > 0f && now >= this._jumpNext)
			{
				CameraBookmarkHotkey.JumpToCursor(cam);
				this._jumpNext = now + every;
			}
		}

		// Follow: a tap of Jump to the cursor on a bricktron keeps the camera over it until the camera is moved some other way.
		private static bool StartFollow(CameraScript cam)
		{
			Vector3 mm;
			if (BtcPing.MinimapCursorPoint(out mm))
			{
				return false;
			}
			Camera main = Camera.main;
			RaycastHit hit;
			if (main == null || !Physics.Raycast(main.ScreenPointToRay(UnityEngine.Input.mousePosition), out hit, 1000f, -1, QueryTriggerInteraction.Ignore))
			{
				return false;
			}
			Labor unit = hit.collider.GetComponentInParent<Labor>();
			if (unit == null || hit.collider.GetComponentInParent<Corpse>() != null)
			{
				return false;
			}
			CameraBookmarkHotkey._follow = unit.transform;
			CameraBookmarkHotkey.FollowStep(cam);
			return true;
		}

		private static void FollowStep(CameraScript cam)
		{
			Vector3 camPos = cam.transform.position;
			Vector3 u = CameraBookmarkHotkey._follow.position;
			Vector3 delta = new Vector3(u.x - camPos.x, 0f, u.z - camPos.z);
			CameraScript.Target += delta;
			cam.transform.position = camPos + delta;
			CameraBookmarkHotkey._followSet = CameraScript.Target;
		}

		private void HandleFollow(CameraScript cam)
		{
			if (CameraBookmarkHotkey._follow == null)
			{
				return;
			}
			Vector3 d = CameraScript.Target - CameraBookmarkHotkey._followSet;
			// moved by hand (WASD, drag, another bind) or Esc: stop following
			if (d.x * d.x + d.z * d.z > 0.0025f || UnityEngine.Input.GetKeyDown(KeyCode.Escape) || !CameraBookmarkHotkey._follow.gameObject.activeInHierarchy)
			{
				CameraBookmarkHotkey._follow = null;
				return;
			}
			CameraBookmarkHotkey.FollowStep(cam);
		}

		// Camera history (back / forward like a browser) over the jumps, zooms and turns of the camera binds.
		internal static void RecordCam(CameraScript cam)
		{
			if (cam == null)
			{
				return;
			}
			List<CameraBookmarkHotkey.CamState> back = CameraBookmarkHotkey._histBack;
			if (back.Count > 0 && CameraBookmarkHotkey.IsCameraAtState(cam, back[back.Count - 1]))
			{
				return;
			}
			back.Add(CameraBookmarkHotkey.CaptureState(cam));
			if (back.Count > Mathf.Max(1, Mathf.RoundToInt(BtcHotkeys.GetFloat("camera_history_size", 50f))))
			{
				back.RemoveAt(0);
			}
			CameraBookmarkHotkey._histFwd.Clear();
		}

		private void HandleHistory(CameraScript cam)
		{
			List<CameraBookmarkHotkey.CamState> from;
			List<CameraBookmarkHotkey.CamState> to;
			if (CameraBookmarkHotkey.IsComboPressed(BtcHotkeys.GetKeys("camera_history_back", CameraBookmarkHotkey.NoKeys)))
			{
				from = CameraBookmarkHotkey._histBack;
				to = CameraBookmarkHotkey._histFwd;
			}
			else if (CameraBookmarkHotkey.IsComboPressed(BtcHotkeys.GetKeys("camera_history_forward", CameraBookmarkHotkey.NoKeys)))
			{
				from = CameraBookmarkHotkey._histFwd;
				to = CameraBookmarkHotkey._histBack;
			}
			else
			{
				return;
			}
			if (from.Count == 0)
			{
				return;
			}
			to.Add(CameraBookmarkHotkey.CaptureState(cam));
			CameraBookmarkHotkey.CamState s = from[from.Count - 1];
			from.RemoveAt(from.Count - 1);
			this._zoomOn = false;
			this._zoomAnim = false;
			CameraBookmarkHotkey._follow = null;
			CameraBookmarkHotkey.ApplyState(cam, s);
		}

		// Honest wheel zoom: every notch zooms by the same step at once, no speed-up and no inertia (the game's wheel zoom is muted).
		private void HandleWheel(CameraScript cam)
		{
			if (!BtcHotkeys.GetBool("camera_wheel", false))
			{
				return;
			}
			float fast = cam.ZoomSpeedMod / Mathf.Max(0.0001f, this._fastBaseZoomMod);
			cam.ZoomSpeedMod = 0f;
			float w = UnityEngine.Input.mouseScrollDelta.y;
			if (w == 0f || BtcStationRadius.WheelTaken || UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift)
				|| (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()))
			{
				return;
			}
			try
			{
				if (BtcHotkeys.GetBool("camera_wheel_invert", false))
				{
					w = -w;
				}
				// one step per notch whatever the device reports per notch
				w = Mathf.Sign(w) * Mathf.Max(1f, Mathf.Round(Mathf.Abs(w))) * fast;
				float d0 = this._wheelAnim ? this._wheelTo : cam.Distance;
				float d1 = BtcHotkeys.GetBool("camera_wheel_prop", true)
					? d0 * Mathf.Pow(1f - Mathf.Clamp(BtcHotkeys.GetFloat("camera_wheel_percent", 12f) / 100f, 0.01f, 0.9f), w)
					: d0 - w * BtcHotkeys.GetFloat("camera_wheel_blocks", 4f);
				d1 = Mathf.Clamp(d1, BtcHotkeys.GetFloat("camera_wheel_min", 2f), Mathf.Max(BtcHotkeys.GetFloat("camera_wheel_min", 2f), BtcHotkeys.GetFloat("camera_wheel_max", 300f)));
				Vector3 point;
				if (BtcHotkeys.GetBool("camera_wheel_cursor", false) && d0 > 0.01f && CameraBookmarkHotkey.CursorWorldPoint(out point))
				{
					Vector3 shift = (point - CameraScript.Target) * (1f - d1 / d0);
					shift.y = 0f;
					CameraScript.Target += shift;
				}
				this._wheelFrom = cam.Distance;
				this._wheelTo = d1;
				this._wheelT0 = Time.unscaledTime;
				this._wheelAnim = true;
			}
			catch (Exception ex)
			{
				BtcLog.Error("CamWheel", ex.Message);
			}
		}

		private void WheelStep(CameraScript cam)
		{
			if (!this._wheelAnim)
			{
				return;
			}
			float dur = BtcHotkeys.GetFloat("camera_wheel_time", 0f);
			float t = dur <= 0f ? 1f : Mathf.Clamp01((Time.unscaledTime - this._wheelT0) / dur);
			cam.Distance = Mathf.Lerp(this._wheelFrom, this._wheelTo, t);
			if (t >= 1f)
			{
				this._wheelAnim = false;
			}
		}

		private static Transform _follow;

		private static Vector3 _followSet;

		private static readonly List<CameraBookmarkHotkey.CamState> _histBack = new List<CameraBookmarkHotkey.CamState>();

		private static readonly List<CameraBookmarkHotkey.CamState> _histFwd = new List<CameraBookmarkHotkey.CamState>();

		private bool _wheelAnim;

		private float _wheelFrom;

		private float _wheelTo;

		private float _wheelT0;

		// True while the camera is in the ground (dive) and the given "blocked while in the ground" option is on.
		private bool DiveBlocks(string option)
		{
			return this._diveActive && BtcHotkeys.GetBool(option, true);
		}

		// World point under the cursor: the mini map point when hovering it, else the first terrain hit.
		private static bool CursorWorldPoint(out Vector3 p)
		{
			if (BtcPing.MinimapCursorPoint(out p))
			{
				return true;
			}
			Camera main = Camera.main;
			RaycastHit hit;
			if (main == null || !CameraBookmarkHotkey.TerrainRay(main.ScreenPointToRay(UnityEngine.Input.mousePosition), 132096, out hit))
			{
				return false;
			}
			p = hit.point;
			return true;
		}

		private static void JumpToCursor(CameraScript cam)
		{
			try
			{
				Vector3 cell;
				Vector3 p;
				if (BtcPing.MinimapCursorPoint(out p))
				{
					cell = Calcule.RoundVecteur(p - Vector3.up * 0.01f);
				}
				else
				{
					Camera main = Camera.main;
					RaycastHit hit;
					if (main == null || !CameraBookmarkHotkey.TerrainRay(main.ScreenPointToRay(UnityEngine.Input.mousePosition), 132096, out hit))
					{
						return;
					}
					cell = Calcule.RoundVecteur(hit.point - hit.normal * 0.0001f);
				}
				Vector3 camPos = cam.transform.position;
				Vector3 delta = new Vector3(cell.x - camPos.x, 0f, cell.z - camPos.z);
				if (BtcHotkeys.GetBool("camera_jump_keep_height", false))
				{
					RaycastHit ground;
					float top = cell.y + 0.5f;
					float height = Physics.Raycast(camPos, Vector3.down, out ground, 1000f, 1024, QueryTriggerInteraction.Ignore) ? camPos.y - ground.point.y : camPos.y - top;
					delta.y = top + height - camPos.y;
				}
				CameraScript.Target += delta;
				cam.transform.position = camPos + delta;
			}
			catch (Exception ex)
			{
				BtcLog.Error("CamJump", ex.Message);
			}
		}

		// Mouse turn: while held (or toggled on) mouse movement turns / tilts the camera; 1x = 360 deg per screen width, 180 per height.
		private void HandleMouseTurn(CameraScript cam)
		{
			KeyCode[] keys = BtcHotkeys.GetKeys("camera_mouse_turn", CameraBookmarkHotkey.NoKeys);
			bool toggle = BtcHotkeys.GetString("camera_mouse_turn_mode", "hold").Equals("toggle", StringComparison.OrdinalIgnoreCase);
			bool pressed = CameraBookmarkHotkey.IsComboPressed(keys);
			Vector2 mouse = UnityEngine.Input.mousePosition;
			if (!this._mtHeld)
			{
				if (pressed)
				{
					this._mtHeld = true;
					this._mtLast = mouse;
					if (!this._mtRun)
					{
						this._mtYaw = cam.CamOrientation;
						this._mtPitch = cam.AngleOfView;
					}
					this._mtRun = true;
					return;
				}
			}
			else if (UnityEngine.Input.GetKeyDown(KeyCode.Escape) || (toggle ? pressed : (keys.Length == 0 || !BtcHotkeys.AllHeld(keys))))
			{
				this._mtHeld = false;
			}
			if (!this._mtRun)
			{
				return;
			}
			try
			{
				if (this._mtHeld)
				{
					Vector2 d = mouse - this._mtLast;
					this._mtLast = mouse;
					float sx = BtcHotkeys.GetFloat("camera_mouse_turn_sens_x", 1f);
					float sy = BtcHotkeys.GetBool("camera_mouse_turn_same", true) ? sx : BtcHotkeys.GetFloat("camera_mouse_turn_sens_y", 1f);
					if (BtcHotkeys.GetBool("camera_mouse_turn_x", true))
					{
						this._mtYaw += d.x / Mathf.Max(1f, (float)Screen.width) * 360f * sx * (BtcHotkeys.GetBool("camera_mouse_turn_inv_x", false) ? -1f : 1f);
					}
					if (BtcHotkeys.GetBool("camera_mouse_turn_y", true))
					{
						this._mtPitch -= d.y / Mathf.Max(1f, (float)Screen.height) * 180f * sy * (BtcHotkeys.GetBool("camera_mouse_turn_inv_y", false) ? -1f : 1f);
					}
					float lo = BtcHotkeys.GetFloat("camera_mouse_turn_min", -80f);
					float hi = BtcHotkeys.GetFloat("camera_mouse_turn_max", 89f);
					this._mtPitch = Mathf.Clamp(this._mtPitch, Mathf.Min(lo, hi), Mathf.Max(lo, hi));
				}
				float smooth = Mathf.Clamp(BtcHotkeys.GetFloat("camera_mouse_turn_smooth", 0f), 0f, 0.95f);
				float k = smooth <= 0f ? 1f : 1f - Mathf.Pow(smooth, Time.unscaledDeltaTime * 60f);
				float yaw = Mathf.LerpAngle(cam.CamOrientation, this._mtYaw, k);
				float pitch = Mathf.Lerp(cam.AngleOfView, this._mtPitch, k);
				CameraBookmarkHotkey.SetAngles(cam, yaw, pitch, Mathf.RoundToInt(BtcHotkeys.GetFloat("camera_mouse_turn_pivot", 0f)) == 1);
				if (!this._mtHeld && Mathf.Abs(Mathf.DeltaAngle(yaw, this._mtYaw)) < 0.05f && Mathf.Abs(pitch - this._mtPitch) < 0.05f)
				{
					this._mtRun = false;
				}
			}
			catch (Exception ex)
			{
				this._mtRun = this._mtHeld = false;
				BtcLog.Error("CamMouseTurn", ex.Message);
			}
		}

		// Sets the orbit angles; aroundCamera keeps the camera in place (moves the pivot) instead of orbiting the view centre.
		private static void SetAngles(CameraScript cam, float yaw, float pitch, bool aroundCamera)
		{
			if (aroundCamera)
			{
				Vector3 camPos = CameraScript.Target - Quaternion.Euler(cam.AngleOfView, cam.CamOrientation, 0f) * Vector3.forward * cam.Distance;
				CameraScript.Target = camPos + Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward * cam.Distance;
			}
			cam.CamOrientation = yaw;
			cam.AngleOfView = pitch;
		}

		// Insta zoom: one press zooms by a set amount, the next press (or the release in hold mode) zooms back.
		private void HandleInstaZoom(CameraScript cam)
		{
			KeyCode[] keys = BtcHotkeys.GetKeys("camera_insta_zoom", CameraBookmarkHotkey.NoKeys);
			bool pressed = CameraBookmarkHotkey.IsComboPressed(keys);
			if (pressed && !this._zoomOn && this.DiveBlocks("camera_dive_block_zoom"))
			{
				pressed = false;
			}
			bool hold = BtcHotkeys.GetString("camera_insta_zoom_mode", "toggle").Equals("hold", StringComparison.OrdinalIgnoreCase);
			try
			{
				// the player zoomed back out himself (wheel or anything else): no way back, the next press zooms in again
				if (this._zoomOn && !this._zoomAnim && BtcHotkeys.GetBool("camera_insta_zoom_reset", true))
				{
					float dd = cam.Distance - this._zoomSet;
					if (this._zoomOutward ? dd < -0.3f : dd > 0.3f)
					{
						this._zoomOn = false;
					}
				}
				if (pressed && !this._zoomOn)
				{
					float d0 = cam.Distance;
					float d1;
					bool outward = BtcHotkeys.GetBool("camera_insta_zoom_out", false);
					if (BtcHotkeys.GetBool("camera_insta_zoom_prop", true))
					{
						float f = BtcHotkeys.GetFloat("camera_insta_zoom_percent", 60f) / 100f;
						d1 = outward ? d0 / Mathf.Max(0.05f, 1f - f) : d0 * (1f - f);
					}
					else
					{
						float b = BtcHotkeys.GetFloat("camera_insta_zoom_blocks", 30f);
						d1 = outward ? d0 + b : d0 - b;
					}
					d1 = Mathf.Max(d1, BtcHotkeys.GetFloat("camera_insta_zoom_limit", 3f));
					Vector3 shift = Vector3.zero;
					Vector3 point;
					if (!outward && d0 > 0.01f && BtcHotkeys.GetBool("camera_insta_zoom_cursor", true) && CameraBookmarkHotkey.CursorWorldPoint(out point))
					{
						// centre the view on the point (like Jump to the cursor), or keep the point under the cursor
						shift = (point - CameraScript.Target) * (BtcHotkeys.GetBool("camera_insta_zoom_center", true) ? 1f : 1f - d1 / d0);
						shift.y = 0f;
					}
					this._zoomOn = true;
					this._zoomOutward = d1 > d0;
					this._zoomBack = d0;
					this._zoomShift = shift;
					this.StartZoom(d0, d1, Vector3.zero, shift);
					this.RememberZoomPoint(CameraScript.Target + shift, d1);
					this._zoomInTime = Time.unscaledTime;
					CameraBookmarkHotkey.RecordCam(cam);
				}
				else if (this._zoomOn && pressed && !hold && this._hasPrevZoom
					&& Time.unscaledTime - this._zoomInTime <= BtcHotkeys.GetFloat("camera_insta_zoom_double", 300f) / 1000f)
				{
					// double tap: back into the previous zoom point instead of zooming out
					Vector3 delta = this._prevZoomPt - this._lastZoomPt;
					delta.y = 0f;
					CameraScript.Target += delta;
					this._zoomShift += delta;
					Vector3 swap = this._lastZoomPt;
					this._lastZoomPt = this._prevZoomPt;
					this._prevZoomPt = swap;
					this._zoomInTime = -100f;
				}
				else if (this._zoomOn && (hold ? (keys.Length == 0 || !BtcHotkeys.AllHeld(keys)) : pressed))
				{
					CameraBookmarkHotkey.RecordCam(cam);
					this._zoomOn = false;
					this.StartZoom(cam.Distance, this._zoomBack, Vector3.zero, -this._zoomShift);
				}
				else if (this._hasLastZoom && CameraBookmarkHotkey.IsComboPressed(BtcHotkeys.GetKeys("camera_insta_rezoom", CameraBookmarkHotkey.NoKeys)) && !this.DiveBlocks("camera_dive_block_zoom"))
				{
					// re-zoom: into the last zoom point, wherever the cursor is
					CameraBookmarkHotkey.RecordCam(cam);
					Vector3 shift = this._lastZoomPt - CameraScript.Target;
					shift.y = 0f;
					if (!this._zoomOn)
					{
						this._zoomOn = true;
						this._zoomBack = cam.Distance;
						this._zoomShift = shift;
					}
					else
					{
						this._zoomShift += shift;
					}
					this._zoomOutward = this._lastZoomDist > cam.Distance;
					this.StartZoom(cam.Distance, this._lastZoomDist, Vector3.zero, shift);
				}
				if (this._zoomAnim)
				{
					float t = this._zoomDur <= 0f ? 1f : Mathf.Clamp01((Time.unscaledTime - this._zoomT0) / this._zoomDur);
					float e = t * t * (3f - 2f * t);
					cam.Distance = Mathf.Lerp(this._zoomFrom, this._zoomTo, e);
					Vector3 s = this._zoomShiftTo * e;
					CameraScript.Target += s - this._zoomShiftDone;
					this._zoomShiftDone = s;
					if (t >= 1f)
					{
						this._zoomAnim = false;
						this._zoomSet = cam.Distance;
					}
				}
			}
			catch (Exception ex)
			{
				this._zoomAnim = false;
				BtcLog.Error("CamZoom", ex.Message);
			}
		}

		private void RememberZoomPoint(Vector3 pt, float dist)
		{
			if (this._hasLastZoom)
			{
				this._prevZoomPt = this._lastZoomPt;
				this._hasPrevZoom = true;
			}
			this._lastZoomPt = pt;
			this._lastZoomDist = dist;
			this._hasLastZoom = true;
		}

		private bool _hasLastZoom;

		private bool _hasPrevZoom;

		private Vector3 _lastZoomPt;

		private Vector3 _prevZoomPt;

		private float _lastZoomDist;

		private float _zoomInTime = -100f;

		private float _zoomSet;

		private bool _zoomOutward;

		private void StartZoom(float from, float to, Vector3 shiftFrom, Vector3 shiftTo)
		{
			this._zoomFrom = from;
			this._zoomTo = to;
			this._zoomShiftDone = shiftFrom;
			this._zoomShiftTo = shiftTo;
			this._zoomT0 = Time.unscaledTime;
			this._zoomDur = BtcHotkeys.GetFloat("camera_insta_zoom_time", 0f);
			this._zoomAnim = true;
		}

		// Camera when a map loads: after LevelReady (+ delay) the camera gets the set tilt / turn / zoom around its view centre.
		private void HandleStartAngle(CameraScript cam)
		{
			if (!CameraBookmarkHotkey._levelReadyHooked)
			{
				CameraBookmarkHotkey._levelReadyHooked = true;
				Brix.Engine.GameSignals.Connect(Brix.Engine.GameSignals.LevelReady, new Action(CameraBookmarkHotkey.OnLevelReadySignal), null);
			}
			if (CameraBookmarkHotkey._startCheckAt > 0f && Time.unscaledTime >= CameraBookmarkHotkey._startCheckAt)
			{
				CameraBookmarkHotkey._startCheckAt = -1f;
				BtcLog.Info("CamStart", "3 s later: yaw " + cam.CamOrientation.ToString("0") + " pitch " + cam.AngleOfView.ToString("0") + " dist " + cam.Distance.ToString("0"));
			}
			if (CameraBookmarkHotkey._startSerial == CameraBookmarkHotkey._levelReadySerial || Time.unscaledTime < CameraBookmarkHotkey._levelReadyAt + BtcHotkeys.GetFloat("camera_start_delay", 0.5f))
			{
				return;
			}
			CameraBookmarkHotkey._startSerial = CameraBookmarkHotkey._levelReadySerial;
			if (!BtcHotkeys.GetBool("camera_start", false))
			{
				return;
			}
			try
			{
				float yaw = BtcHotkeys.GetBool("camera_start_yaw_on", false) ? BtcHotkeys.GetFloat("camera_start_yaw", 0f) : cam.CamOrientation;
				CameraBookmarkHotkey.SetAngles(cam, yaw, BtcHotkeys.GetFloat("camera_start_pitch", 45f), false);
				float dist = BtcHotkeys.GetFloat("camera_start_dist", 0f);
				if (dist > 0f)
				{
					cam.Distance = dist;
				}
				BtcLog.Info("CamStart", "yaw " + yaw.ToString("0") + " pitch " + cam.AngleOfView.ToString("0") + " dist " + cam.Distance.ToString("0") + " via " + CameraBookmarkHotkey._readySrc);
				CameraBookmarkHotkey._startCheckAt = Time.unscaledTime + 3f;
			}
			catch (Exception ex)
			{
				BtcLog.Error("CamStart", ex.Message);
			}
		}

		private static void OnLevelReadySignal()
		{
			CameraBookmarkHotkey.OnLevelReady("signal");
		}

		private static void OnLevelReady(string src)
		{
			CameraBookmarkHotkey._readySrc = src;
			CameraBookmarkHotkey._levelReadyAt = Time.unscaledTime;
			CameraBookmarkHotkey._levelReadySerial++;
		}

		private bool _jumpHeld;

		private float _jumpNext;

		private bool _mtHeld;

		private bool _mtRun;

		private Vector2 _mtLast;

		private float _mtYaw;

		private float _mtPitch;

		private bool _zoomOn;

		private bool _zoomAnim;

		private float _zoomBack;

		private Vector3 _zoomShift;

		private float _zoomFrom;

		private float _zoomTo;

		private Vector3 _zoomShiftDone;

		private Vector3 _zoomShiftTo;

		private float _zoomT0;

		private float _zoomDur;

		private static int _startSerial;

		private static bool _levelReadyHooked;

		private static bool _wasLoading;

		private static int _levelReadySerial;

		private static float _levelReadyAt = -1f;

		private static string _readySrc = "";

		private static float _startCheckAt = -1f;

		// Quick turn: hold the bind, draw an arrow, release -> the camera turns to face the arrow direction on the ground.
		private void HandleQuickTurn(CameraScript cam)
		{
			KeyCode[] keys = BtcHotkeys.GetKeys("camera_quick_turn", CameraBookmarkHotkey.NoKeys);
			if (!this._turnActive)
			{
				if (!CameraBookmarkHotkey.IsComboPressed(keys))
				{
					return;
				}
				this._turnActive = true;
				this._turnStart = UnityEngine.Input.mousePosition;
				Camera main = Camera.main;
				RaycastHit hit = default(RaycastHit);
				this._turnHasWorld = main != null && CameraBookmarkHotkey.TerrainRay(main.ScreenPointToRay(this._turnStart), 132096, out hit);
				this._turnStartWorld = this._turnHasWorld ? hit.point : CameraScript.Target;
				return;
			}
			if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
			{
				this._turnActive = false;
				return;
			}
			if (keys.Length > 0 && BtcHotkeys.AllHeld(keys))
			{
				return;
			}
			this._turnActive = false;
			try
			{
				Vector2 end = UnityEngine.Input.mousePosition;
				Vector2 drag = end - this._turnStart;
				if (drag.magnitude < 12f)
				{
					return;
				}
				float curYaw = cam.transform.eulerAngles.y;
				// Screen angle as a fallback, the arrow projected on the ground plane when both ends hit it.
				float yaw = curYaw + Mathf.Atan2(drag.x, drag.y) * Mathf.Rad2Deg;
				Camera main = Camera.main;
				if (main != null)
				{
					Plane plane = new Plane(Vector3.up, this._turnStartWorld);
					Ray r0 = main.ScreenPointToRay(this._turnStart);
					Ray r1 = main.ScreenPointToRay(end);
					float d0, d1;
					if (plane.Raycast(r0, out d0) && plane.Raycast(r1, out d1))
					{
						Vector3 dir = r1.GetPoint(d1) - r0.GetPoint(d0);
						dir.y = 0f;
						if (dir.sqrMagnitude > 0.01f)
						{
							yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
						}
					}
				}
				float snap = BtcHotkeys.GetFloat("camera_quick_turn_snap", 0f);
				if (snap >= 1f)
				{
					yaw = Mathf.Round(yaw / snap) * snap;
				}
				float delta = Mathf.DeltaAngle(curYaw, yaw);
				int mode = Mathf.RoundToInt(BtcHotkeys.GetFloat("camera_quick_turn_pivot", 0f));
				Vector3 camPos = cam.transform.position;
				Vector3 pivot = mode == 2 ? camPos : (mode == 1 ? this._turnStartWorld : CameraScript.Target);
				Vector3 pos = pivot + Quaternion.Euler(0f, delta, 0f) * (camPos - pivot);
				Quaternion rot = Quaternion.Euler(cam.transform.eulerAngles.x, curYaw + delta, 0f);
				CameraBookmarkHotkey.RecordCam(cam);
				cam.Positionner(pos, rot, cam.Distance);
			}
			catch (Exception ex)
			{
				BtcLog.Error("CamTurn", ex.Message);
			}
		}

		private void DrawQuickTurnArrow()
		{
			if (!this._turnActive || Event.current.type != EventType.Repaint || this._overlayWhiteTex == null)
			{
				return;
			}
			Vector2 a = new Vector2(this._turnStart.x, Screen.height - this._turnStart.y);
			Vector3 m = UnityEngine.Input.mousePosition;
			Vector2 b = new Vector2(m.x, Screen.height - m.y);
			Vector2 d = b - a;
			float len = d.magnitude;
			Color col = GUI.color;
			Matrix4x4 mat = GUI.matrix;
			GUI.color = BtcMenu.Accent;
			GUI.DrawTexture(new Rect(a.x - 4f, a.y - 4f, 8f, 8f), this._overlayWhiteTex);
			if (len >= 12f)
			{
				float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
				float head = Mathf.Min(16f, len * 0.4f);
				for (int pass = 0; pass < 2; pass++)
				{
					float th = pass == 0 ? 6f : 3f;
					GUI.color = pass == 0 ? new Color(0f, 0f, 0f, 0.6f) : BtcMenu.Accent;
					GUI.matrix = mat;
					GUIUtility.RotateAroundPivot(ang, a);
					GUI.DrawTexture(new Rect(a.x, a.y - th * 0.5f, len, th), this._overlayWhiteTex);
					for (int s = -1; s <= 1; s += 2)
					{
						GUI.matrix = mat;
						GUIUtility.RotateAroundPivot(ang + 180f + 35f * s, b);
						GUI.DrawTexture(new Rect(b.x, b.y - th * 0.5f, head, th), this._overlayWhiteTex);
					}
				}
			}
			GUI.matrix = mat;
			GUI.color = col;
		}

		private bool _turnActive;

		private Vector2 _turnStart;

		private bool _turnHasWorld;

		private Vector3 _turnStartWorld;

		private static readonly KeyCode[] NoKeys = new KeyCode[0];

		private bool _diveActive;

		private int _diveCamId;

		private float _diveY;

		private Vector3 _divePos;

		private CameraBookmarkHotkey.CamState _diveReturn;

		private static List<GameObject> GetAliveObjects(List<GameObject> source)
		{
			List<GameObject> list = new List<GameObject>();
			for (int i = 0; i < source.Count; i++)
			{
				GameObject gameObject = source[i];
				bool flag = gameObject != null;
				bool flag2 = flag;
				bool flag3 = flag2;
				bool flag4 = flag3;
				bool flag5 = flag4;
				if (flag5)
				{
					list.Add(gameObject);
				}
			}
			return list;
		}

		// The groups overlay is drawn on every OnGUI event (layout, repaint, input): its selection and group lists are worked
		// out once per frame into reused lists (was 6 new lists + a selection query per event).
		private int _ovFrame = -1;
		private List<GameObject> _ovSource = new List<GameObject>();
		private readonly List<GameObject> _ovSel = new List<GameObject>();
		private readonly List<GameObject>[] _ovGroups = { new List<GameObject>(), new List<GameObject>(), new List<GameObject>(), new List<GameObject>(), new List<GameObject>() };

		private void RefreshOverlayGroups()
		{
			if (_ovFrame == Time.frameCount) return;
			_ovFrame = Time.frameCount;
			_ovSource.Clear();
			UIGameSelector.GetSelectedEverything(ref _ovSource);
			FilterInto(_ovSource, _ovSel);
			for (int i = 0; i < 5; i++) FilterInto(this._groups[i], _ovGroups[i]);
		}

		private static void FilterInto(List<GameObject> source, List<GameObject> dest)
		{
			dest.Clear();
			if (source == null) return;
			for (int i = 0; i < source.Count; i++)
			{
				GameObject go = source[i];
				if (go != null && go.GetComponent<Labor>() != null) dest.Add(go);
			}
		}

		private static List<GameObject> FilterToUnits(List<GameObject> source)
		{
			List<GameObject> list = new List<GameObject>();
			for (int i = 0; i < source.Count; i++)
			{
				GameObject gameObject = source[i];
				bool flag = gameObject != null && gameObject.GetComponent<Labor>() != null;
				bool flag2 = flag;
				bool flag3 = flag2;
				bool flag4 = flag3;
				bool flag5 = flag4;
				if (flag5)
				{
					list.Add(gameObject);
				}
			}
			return list;
		}

		private static void JumpCameraTo(CameraScript cam, List<GameObject> objs)
		{
			CameraBookmarkHotkey.RecordCam(cam);
			Vector3 position = cam.transform.position;
			Vector3 target = Vector3.zero;
			float num = float.MaxValue;
			bool flag = false;
			for (int i = 0; i < objs.Count; i++)
			{
				Vector3 position2 = objs[i].transform.position;
				float sqrMagnitude = (position2 - position).sqrMagnitude;
				bool flag2 = !flag || sqrMagnitude < num;
				bool flag3 = flag2;
				bool flag4 = flag3;
				bool flag5 = flag4;
				bool flag6 = flag5;
				if (flag6)
				{
					num = sqrMagnitude;
					target = position2;
					flag = true;
				}
			}
			bool flag7 = !flag;
			bool flag8 = !flag7;
			bool flag9 = flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			if (flag11)
			{
				cam.LookAt(target);
			}
		}

		private void DrawRect(Rect rect, Color color)
		{
			Color color2 = GUI.color;
			float overlayOutlineThickness = CameraBookmarkHotkey.OverlayOutlineThickness;
			bool flag = overlayOutlineThickness > 0f;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			if (flag4)
			{
				Rect position = new Rect(rect.x - overlayOutlineThickness, rect.y - overlayOutlineThickness, rect.width + overlayOutlineThickness * 2f, rect.height + overlayOutlineThickness * 2f);
				GUI.color = Color.black;
				GUI.DrawTexture(position, this._overlayWhiteTex);
			}
			GUI.color = color;
			GUI.DrawTexture(rect, this._overlayWhiteTex);
			GUI.color = color2;
		}

		private void DrawAttribution()
		{
		}

		private static bool IsCameraAtState(CameraScript cam, CameraBookmarkHotkey.CamState state)
		{
			bool flag = cam == null;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			bool flag5 = flag4;
			bool result;
			if (flag5)
			{
				result = false;
			}
			else
			{
				bool flag6 = (cam.transform.position - state.position).sqrMagnitude <= 0.25f;
				bool flag7 = Quaternion.Angle(cam.transform.rotation, state.rotation) <= 2f;
				bool flag8 = Mathf.Abs(cam.Distance - state.distance) <= 0.5f;
				result = (flag6 && flag7 && flag8);
			}
			return result;
		}

		static CameraBookmarkHotkey()
		{
			CameraBookmarkHotkey.GroupClearAllDefault = new KeyCode[]
			{
				KeyCode.CapsLock,
				KeyCode.Backspace
			};
			CameraBookmarkHotkey.GroupAutoAssignDefault = new KeyCode[]
			{
				KeyCode.B
			};
			CameraBookmarkHotkey.DigVoxelDefault = new KeyCode[]
			{
				KeyCode.G
			};
			CameraBookmarkHotkey.FocusSelectionDefault = new KeyCode[]
			{
				KeyCode.Home
			};
			CameraBookmarkHotkey.OverlayToggleDefault = new KeyCode[]
			{
				KeyCode.O
			};
		}

		private static bool IsUnit(GameObject go)
		{
			return go != null && (go.GetComponentInParent<BaseLabor>() != null || go.GetComponentInParent<Profession>() != null);
		}

		private static int _rayLogs;

		// Range/aura volumes of wards (healing, lantern, sentinel...): a big collider on a ward that is not the ward's body.
		// Hotkey rays must pass through them.
		private static bool IsAuraCollider(Collider c)
		{
			if (c == null)
			{
				return false;
			}
			GameComponent root = c.GetComponentInParent<GameComponent>();
			if (root == null)
			{
				return false;
			}
			Vector3 size = c.bounds.size;
			bool big = size.x > 5f || size.z > 5f;
			bool wardLike = root.GetComponentInChildren<RegenAura>() != null || root.GetComponentInParent<RegenAura>() != null || root.GetComponent<Ward>() != null
				|| root.name.IndexOf("Ward", StringComparison.OrdinalIgnoreCase) >= 0 || c.name.IndexOf("aura", StringComparison.OrdinalIgnoreCase) >= 0
				|| c.name.IndexOf("range", StringComparison.OrdinalIgnoreCase) >= 0 || c.name.IndexOf("radius", StringComparison.OrdinalIgnoreCase) >= 0;
			return (wardLike && (big || c.isTrigger)) || (c.isTrigger && big);
		}

		// A loose item stored on a stockpile (not in a unit's bag): counts as part of the stockpile.
		private static GameObject StockpileOf(GameObject item)
		{
			GameComponent gc = item != null ? item.GetComponent<GameComponent>() : null;
			if (gc == null)
			{
				return null;
			}
			Brix.Game.Semantique.Recepteur r = gc.ParentRecepteur;
			if (r == null || CameraBookmarkHotkey.IsUnit(r.gameObject))
			{
				return null;
			}
			return r.gameObject;
		}

		private static void LogRay(string what, RaycastHit[] hits)
		{
			if (_rayLogs++ > 12)
			{
				return;
			}
			System.Text.StringBuilder sb = new System.Text.StringBuilder(what + " ray:");
			for (int i = 0; i < hits.Length && i < 8; i++)
			{
				Collider c = hits[i].collider;
				GameComponent gc = c != null ? c.GetComponentInParent<GameComponent>() : null;
				sb.Append(" [").Append(c != null ? c.name : "null").Append(" root=").Append(gc != null ? gc.name : "-").Append(" trig=").Append(c != null && c.isTrigger)
					.Append(" layer=").Append(c != null ? c.gameObject.layer : -1).Append(" size=").Append(c != null ? c.bounds.size.x.ToString("0.0") : "-")
					.Append(" aura=").Append(IsAuraCollider(c)).Append(" d=").Append(hits[i].distance.ToString("0.0")).Append("]");
			}
			BtcLog.Info("Hotkey", sb.ToString());
		}

		// Nearest terrain hit on the hotkey layers, skipping ward auras.
		private static bool TerrainRay(Ray ray, int layerMask, out RaycastHit hit)
		{
			RaycastHit[] hits = Physics.RaycastAll(ray, 500f, layerMask, QueryTriggerInteraction.Collide);
			Array.Sort<RaycastHit>(hits, (RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance));
			LogRay("dig", hits);
			for (int i = 0; i < hits.Length; i++)
			{
				if (!CameraBookmarkHotkey.IsAuraCollider(hits[i].collider))
				{
					hit = hits[i];
					return true;
				}
			}
			hit = default(RaycastHit);
			return false;
		}

		// True when a non-terrain, non-unit object (a building) is hit by the cursor ray before the terrain voxel.
		private static bool BuildingOnRay(Ray ray, float terrainDist)
		{
			try
			{
				RaycastHit[] hits = Physics.RaycastAll(ray, terrainDist, ~132096, QueryTriggerInteraction.Ignore);
				for (int i = 0; i < hits.Length; i++)
				{
					Collider c = hits[i].collider;
					if (c == null || CameraBookmarkHotkey.IsAuraCollider(c))
					{
						continue;
					}
					if (CameraBookmarkHotkey.StockpileOf(c.gameObject) != null || (c.GetComponentInParent<Garnotte>() != null && CameraBookmarkHotkey.StockpileOf(c.GetComponentInParent<Garnotte>().gameObject) != null))
					{
						return true;
					}
					GameComponent gc = c.GetComponentInParent<GameComponent>();
					if (gc != null && !(gc is ISoulevable) && !CameraBookmarkHotkey.IsUnit(gc.gameObject)
						&& c.GetComponentInParent<Corpse>() == null && c.GetComponentInParent<Garnotte>() == null
						&& c.GetComponentInParent<DamagedTree>() == null)
					{
						return true;
					}
				}
			}
			catch (Exception ex)
			{
				BtcLog.Error("Hotkey", ex.ToString());
			}
			return false;
		}

		private static GameObject FindObjectUnderCursor(bool needExplosive)
		{
			Camera main = Camera.main;
			bool flag = main == null;
			bool flag2 = flag;
			GameObject result;
			if (flag2)
			{
				result = null;
			}
			else
			{
				Ray ray = main.ScreenPointToRay(UnityEngine.Input.mousePosition);
				RaycastHit[] array = Physics.RaycastAll(ray, 500f, -1, needExplosive ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore);
				Array.Sort<RaycastHit>(array, (RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance));
				if (!needExplosive)
				{
					CameraBookmarkHotkey.LogRay("break", array);
				}
				for (int i = 0; i < array.Length; i++)
				{
					bool flag3 = array[i].collider == null;
					bool flag4 = !flag3;
					if (flag4)
					{
						if (needExplosive)
						{
							Explosifs componentInParent = array[i].collider.GetComponentInParent<Explosifs>();
							bool flag5 = componentInParent != null;
							bool flag6 = flag5;
							if (flag6)
							{
								return componentInParent.gameObject;
							}
						}
						else
						{
							if (array[i].collider.isTrigger || CameraBookmarkHotkey.IsAuraCollider(array[i].collider))
							{
								continue;
							}
							GameComponent componentInParent2 = array[i].collider.GetComponentInParent<GameComponent>();
							if (componentInParent2 != null)
							{
								if (CameraBookmarkHotkey.IsUnit(componentInParent2.gameObject))
								{
									return null;
								}
								GameObject stock = CameraBookmarkHotkey.StockpileOf(componentInParent2.gameObject);
								return stock != null ? stock : componentInParent2.gameObject;
							}
							if (((1 << array[i].collider.gameObject.layer) & 132096) != 0)
							{
								return null;
							}
						}
					}
				}
				result = null;
			}
			return result;
		}

		private void HandleBreakObject()
		{
			KeyCode[] keys = BtcHotkeys.GetKeys("break_object", null);
			bool flag = keys == null || keys.Length == 0 || !CameraBookmarkHotkey.IsComboPressed(keys);
			bool flag2 = !flag;
			if (flag2)
			{
				GameObject gameObject = CameraBookmarkHotkey.FindObjectUnderCursor(false);
				bool flag3 = gameObject == null;
				bool flag4 = !flag3;
				if (flag4)
				{
					List<GameObject> list = new List<GameObject>();
					UIGameSelector.GetSelectedEverything(ref list);
					for (int i = 0; i < list.Count; i++)
					{
						GameObject gameObject2 = list[i];
						bool flag5 = gameObject2 == null || gameObject2 == gameObject;
						bool flag6 = !flag5;
						if (flag6)
						{
							Labor component = gameObject2.GetComponent<Labor>();
							bool flag7 = component != null;
							bool flag8 = flag7;
							if (flag8)
							{
								DirectOrderProject.AddBreakObjectGoal(component, gameObject);
							}
						}
					}
				}
			}
		}

		private void HandleGoDetonate()
		{
			KeyCode[] keys = BtcHotkeys.GetKeys("go_detonate", null);
			bool flag = keys == null || keys.Length == 0 || !CameraBookmarkHotkey.IsComboPressed(keys);
			bool flag2 = !flag;
			if (flag2)
			{
				GameObject gameObject = CameraBookmarkHotkey.FindObjectUnderCursor(true);
				bool flag3 = gameObject == null;
				bool flag4 = !flag3;
				if (flag4)
				{
					List<GameObject> list = new List<GameObject>();
					UIGameSelector.GetSelectedEverything(ref list);
					for (int i = 0; i < list.Count; i++)
					{
						GameObject gameObject2 = list[i];
						bool flag5 = gameObject2 == null;
						bool flag6 = !flag5;
						if (flag6)
						{
							Labor component = gameObject2.GetComponent<Labor>();
							bool flag7 = component != null;
							bool flag8 = flag7;
							if (flag8)
							{
								DirectOrderProject.GoDetonate(component, gameObject);
							}
						}
					}
				}
			}
		}

		private void HandleFocusHold()
		{
			KeyCode[] keys = BtcFocusHold.Keys();
			if (keys != null && keys.Length != 0 && CameraBookmarkHotkey.IsComboPressed(keys))
			{
				BtcFocusHold.OnKey();
			}
		}

		private void HandleFocusMark()
		{
			KeyCode[] keys = BtcHotkeys.GetKeys("focus_mark", null);
			if (keys != null && keys.Length != 0 && CameraBookmarkHotkey.IsComboPressed(keys))
			{
				BtcFocusMark.Place();
			}
		}

		private static bool McPressed(string id)
		{
			KeyCode[] keys = BtcHotkeys.GetKeys(id, null);
			return keys != null && keys.Length != 0 && CameraBookmarkHotkey.IsComboPressed(keys);
		}

		// Multi-camera binds (Features/BtcMultiCam.cs).
		private void HandleMultiCam()
		{
			try
			{
				if (McPressed("multicam_split")) BtcMultiCam.Split();
				if (McPressed("multicam_toggle")) BtcMultiCam.Toggle();
				if (McPressed("multicam_close")) BtcMultiCam.CloseActive();
				if (McPressed("multicam_max")) BtcMultiCam.ToggleMax(BtcMultiCam.Active);
				if (McPressed("multicam_float")) BtcMultiCam.ToggleFloat(BtcMultiCam.Active);
				if (McPressed("multicam_dock_all")) BtcMultiCam.DockAll();
				if (McPressed("multicam_next")) BtcMultiCam.FocusNext(1);
				if (McPressed("multicam_prev")) BtcMultiCam.FocusNext(-1);
				if (McPressed("multicam_sync")) BtcMultiCam.SyncAll();
				for (int i = 1; i <= 8; i++)
				{
					if (McPressed(BtcStr.Key("multicam_focus_", i))) BtcMultiCam.Focus(i - 1);
				}
			}
			catch (Exception e) { BtcLog.Error("MultiCam", "binds: " + e.Message); }
		}

		private void HandlePing()
		{
			KeyCode[] keys = BtcHotkeys.GetKeys("team_ping", null);
			if (keys != null && keys.Length != 0 && CameraBookmarkHotkey.IsComboPressed(keys))
			{
				BtcPing.Place();
			}
		}

		private void HandleShootAt()
		{
			KeyCode[] keys = BtcHotkeys.GetKeys("shoot_at", null);
			if (keys == null || keys.Length == 0 || !CameraBookmarkHotkey.IsComboPressed(keys))
			{
				return;
			}
			try
			{
				Camera main = Camera.main;
				if (main == null)
				{
					return;
				}
				List<GameObject> list = new List<GameObject>();
				UIGameSelector.GetSelectedEverything(ref list);
				List<Labor> shooters = new List<Labor>();
				for (int i = 0; i < list.Count; i++)
				{
					GameObject go = list[i];
					Labor labor = (go == null) ? null : go.GetComponent<Labor>();
					if (labor != null && labor.Occupation != null && labor.Occupation.CurrentOccupation == Occupation.Type.Archer && !labor.toolbag.HasTool(Brix.External.Factories.Accessories.Staff))
					{
						shooters.Add(labor);
					}
				}
				if (shooters.Count == 0)
				{
					return;
				}
				Ray ray = main.ScreenPointToRay(UnityEngine.Input.mousePosition);
				RaycastHit[] hits = Physics.RaycastAll(ray, 500f, -1, QueryTriggerInteraction.Ignore);
				Array.Sort<RaycastHit>(hits, (RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance));
				Location target = null;
				for (int j = 0; j < hits.Length && target == null; j++)
				{
					Collider col = hits[j].collider;
					if (col == null || CameraBookmarkHotkey.IsAuraCollider(col))
					{
						continue;
					}
					if (col.GetComponentInParent<Labor>() != null)
					{
						return;
					}
					if (((1 << col.gameObject.layer) & 132096) != 0)
					{
						// terrain first: terrain colliders may sit under a GameComponent, which made the target an object ("cannot shoot there")
						Vector3 voxel = Calcule.RoundVecteur(hits[j].point - hits[j].normal * 0.0001f);
						target = new SpaceLocation(Brix.Engine.Architecte.SurfaceRaycast(Voxel.GetVecteur(Voxel.GetScalaire(voxel)), 1, false), Quaternion.identity, true);
					}
					else
					{
						GameComponent gc = col.GetComponentInParent<GameComponent>();
						if (gc != null)
						{
							target = new ObjectLocation(gc.gameObject);
						}
					}
				}
				if (target == null)
				{
					return;
				}
				for (int k = 0; k < shooters.Count; k++)
				{
					DirectOrderProject.ShootLocation(shooters[k], target, false);
				}
			}
			catch (Exception ex)
			{
				BtcLog.Error("ShootAt", ex.ToString());
			}
		}

		private void HandleDropLuggage()
		{
			KeyCode[] keys = BtcHotkeys.GetKeys("drop_luggage", null);
			bool flag = keys == null || keys.Length == 0 || !CameraBookmarkHotkey.IsComboPressed(keys);
			bool flag2 = !flag;
			if (flag2)
			{
				Camera main = Camera.main;
				bool flag3 = main == null;
				bool flag4 = !flag3;
				if (flag4)
				{
					Ray ray = main.ScreenPointToRay(UnityEngine.Input.mousePosition);
					RaycastHit raycastHit;
					bool flag5 = !Physics.Raycast(ray, out raycastHit, 500f, 132096, QueryTriggerInteraction.Collide);
					bool flag6 = !flag5;
					if (flag6)
					{
						Vector3 vecteur = Calcule.RoundVecteur(raycastHit.point - raycastHit.normal * 0.0001f);
						Vector3 vecteur2 = Voxel.GetVecteur(Voxel.GetScalaire(vecteur));
						List<GameObject> list = new List<GameObject>();
						UIGameSelector.GetSelectedEverything(ref list);
						for (int i = 0; i < list.Count; i++)
						{
							GameObject gameObject = list[i];
							bool flag7 = gameObject == null;
							bool flag8 = !flag7;
							if (flag8)
							{
								Labor component = gameObject.GetComponent<Labor>();
								bool flag9 = component != null && !component.recepteur.IsEmpty();
								bool flag10 = flag9;
								if (flag10)
								{
									DirectOrderProject.AddGoDropAt(component, vecteur2);
								}
							}
						}
					}
				}
			}
		}

		private void HandleGroupCycleNoCamera()
		{
			KeyCode[] keys = BtcHotkeys.GetKeys("group_cycle_nocam", CameraBookmarkHotkey.GroupCycleNoCameraDefault);
			bool flag = keys == null || keys.Length == 0;
			if (!flag)
			{
				bool flag2 = !CameraBookmarkHotkey.IsComboPressed(keys);
				if (!flag2)
				{
					bool flag3 = false;
					for (int i = 0; i < 5; i++)
					{
						bool flag4 = this._groups[i].Count > 0;
						if (flag4)
						{
							flag3 = true;
							break;
						}
					}
					bool flag5 = !flag3;
					if (!flag5)
					{
						int num = this._groupCycleCursor;
						for (int j = 0; j < 5; j++)
						{
							num = (num + 1) % 5;
							bool flag6 = this._groups[num].Count > 0;
							if (flag6)
							{
								List<GameObject> list = CameraBookmarkHotkey.FilterToUnits(this._groups[num]);
								bool flag7 = list.Count > 0;
								if (flag7)
								{
									this._groupCycleCursor = num;
									UIGameSelector.UnselectEverything(null);
									UIGameSelector.Select(list);
									break;
								}
								this._groups[num] = new List<GameObject>();
								bool flag8 = this._groupCycleCursor == num;
								if (flag8)
								{
									this._groupCycleCursor = -1;
								}
							}
						}
					}
				}
			}
		}

		private static readonly KeyCode[] ModifierKeys = new KeyCode[]
		{
			KeyCode.LeftControl,
			KeyCode.RightControl,
			KeyCode.LeftShift,
			KeyCode.RightShift,
			KeyCode.LeftAlt,
			KeyCode.RightAlt,
			KeyCode.CapsLock
		};

		private static readonly Color ColorEmpty = new Color(0.45f, 0.4f, 0.15f, 0.62f);

		private static readonly Color ColorOccupied = new Color(0.95f, 0.78f, 0.1f, 0.92f);

		private static readonly Color ColorActive = new Color(0.15f, 0.85f, 0.25f, 0.97f);

		private static readonly Color ColorReturnMode = new Color(1f, 0.55f, 0.1f, 0.92f);

		private static float OverlayOutlineThickness = 1.5f;

		private readonly CameraBookmarkHotkey.CamState[] _cycleSlots = new CameraBookmarkHotkey.CamState[2];

		private readonly bool[] _cycleSlotSet = new bool[2];

		private readonly List<GameObject>[] _cycleSelections;

		private int _nextCycleSlot;

		private static readonly KeyCode[] CycleSwitchDefault = new KeyCode[]
		{
			KeyCode.Mouse4
		};

		private static readonly KeyCode[] CycleRecordOnlyDefault = new KeyCode[]
		{
			KeyCode.LeftControl,
			KeyCode.Mouse4
		};

		private readonly CameraBookmarkHotkey.CamState[] _stableSlots = new CameraBookmarkHotkey.CamState[5];

		private readonly bool[] _stableSlotSet = new bool[5];

		private int _stableCursor;

		private int _stableActiveSlot;

		private CameraBookmarkHotkey.CamState _stableReturnState;

		private static readonly KeyCode[][] StableSaveDefaults = new KeyCode[][]
		{
			new KeyCode[]
			{
				KeyCode.LeftAlt,
				KeyCode.Alpha1
			},
			new KeyCode[]
			{
				KeyCode.LeftAlt,
				KeyCode.Alpha2
			},
			new KeyCode[]
			{
				KeyCode.LeftAlt,
				KeyCode.Alpha3
			},
			new KeyCode[]
			{
				KeyCode.LeftAlt,
				KeyCode.Alpha4
			},
			new KeyCode[]
			{
				KeyCode.LeftAlt,
				KeyCode.Alpha5
			}
		};

		private static readonly KeyCode[][] StableGotoDefaults = new KeyCode[][]
		{
			new KeyCode[]
			{
				KeyCode.Alpha1
			},
			new KeyCode[]
			{
				KeyCode.Alpha2
			},
			new KeyCode[]
			{
				KeyCode.Alpha3
			},
			new KeyCode[]
			{
				KeyCode.Alpha4
			},
			new KeyCode[]
			{
				KeyCode.Alpha5
			}
		};

		private static readonly KeyCode[] StableNextDefault = new KeyCode[]
		{
			KeyCode.Period
		};

		private static readonly KeyCode[] StablePrevDefault = new KeyCode[]
		{
			KeyCode.Comma
		};

		private static readonly KeyCode[][] FastModeKeyDefaults = new KeyCode[][]
		{
			new KeyCode[]
			{
				KeyCode.Mouse2
			},
			new KeyCode[0],
			new KeyCode[0]
		};

		private const float FastModeMultiplierDefault = 2.5f;

		private const string FastModeTypeDefault = "hold";

		private bool _fastBaseCaptured;

		private Vector3 _fastBaseTranslationMod;

		private float _fastBaseZoomMod;

		private Vector2 _fastBaseRotationMod;

		private readonly bool[] _fastModeToggleState;

		private static readonly KeyCode[] XRayToggleDefault = new KeyCode[]
		{
			KeyCode.Backslash
		};

		private readonly List<GameObject>[] _groups;

		private readonly float[] _groupLastPressTime;

		private int _groupCycleCursor;

		private const float DoubleTapThreshold = 0.35f;

		private static readonly KeyCode[][] GroupSaveDefaults = new KeyCode[][]
		{
			new KeyCode[]
			{
				KeyCode.LeftShift,
				KeyCode.CapsLock,
				KeyCode.Alpha1
			},
			new KeyCode[]
			{
				KeyCode.LeftShift,
				KeyCode.CapsLock,
				KeyCode.Alpha2
			},
			new KeyCode[]
			{
				KeyCode.LeftShift,
				KeyCode.CapsLock,
				KeyCode.Alpha3
			},
			new KeyCode[]
			{
				KeyCode.LeftShift,
				KeyCode.CapsLock,
				KeyCode.Alpha4
			},
			new KeyCode[]
			{
				KeyCode.LeftShift,
				KeyCode.CapsLock,
				KeyCode.Alpha5
			}
		};

		private static readonly KeyCode[][] GroupSelectDefaults = new KeyCode[][]
		{
			new KeyCode[]
			{
				KeyCode.CapsLock,
				KeyCode.Alpha1
			},
			new KeyCode[]
			{
				KeyCode.CapsLock,
				KeyCode.Alpha2
			},
			new KeyCode[]
			{
				KeyCode.CapsLock,
				KeyCode.Alpha3
			},
			new KeyCode[]
			{
				KeyCode.CapsLock,
				KeyCode.Alpha4
			},
			new KeyCode[]
			{
				KeyCode.CapsLock,
				KeyCode.Alpha5
			}
		};

		private static readonly KeyCode[] GroupSaveFirstEmptyDefault = new KeyCode[]
		{
			KeyCode.LeftControl,
			KeyCode.LeftShift,
			KeyCode.E
		};

		private static readonly KeyCode[] GroupCycleDefault = new KeyCode[]
		{
			KeyCode.Semicolon
		};

		private static readonly KeyCode[] GroupClearAllDefault;

		private static readonly KeyCode[] GroupAutoAssignDefault;

		private const float AutoClusterRadiusDefault = 6f;

		private static readonly KeyCode[] DigVoxelDefault;

		private static readonly KeyCode[] FocusSelectionDefault;

		private bool _overlayVisible = true;

		private bool _overlayStylesReady;

		private GUIStyle _overlayCountStyle;

		private GUIStyle _overlaySlotTextStyle;

		private GUIStyle _overlayAttributionStyle;

		private const string AttributionText = "Better Than Castle by NotNonlokill (a0.0.1)";

		private static readonly KeyCode[] OverlayToggleDefault;

		private Texture2D _overlayWhiteTex;

		private const float PanelRightMargin = 260f;

		private const float SlotPositionTolerance = 0.5f;

		private const float SlotDistanceTolerance = 0.5f;

		private const float SlotAngleTolerance = 2f;

		private static readonly KeyCode[] GroupCycleNoCameraDefault = new KeyCode[]
		{
			KeyCode.LeftShift,
			KeyCode.Space
		};

		private struct CamState
		{
			public Vector3 position;

			public Quaternion rotation;

			public float distance;
		}
	}
}
