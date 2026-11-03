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
		private void Update()
		{
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
				this.HandleDigVoxel();
				this.HandleBreakObject();
				this.HandleGoDetonate();
				this.HandleShootAt();
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
				KeyCode[] keys = BtcHotkeys.GetKeys("camera_slot_save_" + (i + 1), CameraBookmarkHotkey.StableSaveDefaults[i]);
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
					KeyCode[] keys2 = BtcHotkeys.GetKeys("camera_slot_goto_" + (i + 1), CameraBookmarkHotkey.StableGotoDefaults[i]);
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
				KeyCode[] keys = BtcHotkeys.GetKeys("camera_fast_move_" + num4, CameraBookmarkHotkey.FastModeKeyDefaults[i]);
				bool flag6 = keys == null || keys.Length == 0;
				bool flag7 = !flag6;
				bool flag8 = flag7;
				bool flag9 = flag8;
				bool flag10 = flag9;
				if (flag10)
				{
					string @string = BtcHotkeys.GetString("camera_fast_move_" + num4 + "_mode", "hold");
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
						num *= BtcHotkeys.GetFloat("camera_fast_move_" + num4 + "_translation_multiplier", 2.5f);
						num2 *= BtcHotkeys.GetFloat("camera_fast_move_" + num4 + "_zoom_multiplier", 2.5f);
						num3 *= BtcHotkeys.GetFloat("camera_fast_move_" + num4 + "_orbit_multiplier", 2.5f);
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
				KeyCode[] keys = BtcHotkeys.GetKeys("group_save_" + (i + 1), CameraBookmarkHotkey.GroupSaveDefaults[i]);
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
				else
				{
					KeyCode[] keys2 = BtcHotkeys.GetKeys("group_select_" + (i + 1), CameraBookmarkHotkey.GroupSelectDefaults[i]);
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
						bool flag16 = !Physics.Raycast(ray, out raycastHit, 500f, layerMask, QueryTriggerInteraction.Collide);
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
			this.EnsureOverlayStyles();
			bool flag = BtcHotkeys.Enabled && this._overlayVisible;
			bool flag2 = flag;
			if (flag2)
			{
				this.DrawOverlay();
			}
			this.DrawAttribution();
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
				this._overlayCountStyle.normal.textColor = new Color(0.12f, 0.09f, 0.02f, 1f);
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

		private void DrawOverlay()
		{
			CameraScript instance = CameraScript.Instance;
			float num = 45f;
			float num2 = 166f;
			float num3 = 136f;
			float num4 = (float)Screen.width - num2 - num;
			float num5 = 0f;
			float num6 = num4 + 8f;
			float num7 = num5 + 8f;
			List<GameObject> source = new List<GameObject>();
			UIGameSelector.GetSelectedEverything(ref source);
			List<GameObject> list = CameraBookmarkHotkey.FilterToUnits(source);
			for (int i = 0; i < 5; i++)
			{
				Rect rect = new Rect(num6, num7 + (float)i * 28f, 150f, 24f);
				List<GameObject> list2 = CameraBookmarkHotkey.FilterToUnits(this._groups[i]);
				Color color = (list2.Count > 0 && this._groupCycleCursor == i && list.Count == list2.Count) ? CameraBookmarkHotkey.ColorActive : ((list2.Count > 0) ? CameraBookmarkHotkey.ColorOccupied : CameraBookmarkHotkey.ColorEmpty);
				this.DrawRect(rect, color);
				bool flag = list2.Count > 0;
				bool flag2 = flag;
				bool flag3 = flag2;
				bool flag4 = flag3;
				bool flag5 = flag4;
				bool flag6 = flag5;
				if (flag6)
				{
					this.DrawGroupIcons(rect, list2, 18f);
				}
			}
			num7 += num3 + 10f;
			float num8 = 116f;
			float num9 = num6 + (150f - num8) * 0.5f;
			for (int j = 0; j < 5; j++)
			{
				Rect rect2 = new Rect(num9 + (float)j * 24f, num7, 20f, 20f);
				bool flag7 = this._stableSlotSet[j] && CameraBookmarkHotkey.IsCameraAtState(instance, this._stableSlots[j]);
				bool flag8 = this._stableActiveSlot == j && flag7;
				bool flag9 = flag8;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				Color color2;
				if (flag12)
				{
					color2 = CameraBookmarkHotkey.ColorReturnMode;
				}
				else
				{
					bool flag13 = flag7;
					bool flag14 = flag13;
					bool flag15 = flag14;
					bool flag16 = flag15;
					bool flag17 = flag16;
					if (flag17)
					{
						color2 = CameraBookmarkHotkey.ColorActive;
					}
					else
					{
						bool flag18 = this._stableSlotSet[j];
						bool flag19 = flag18;
						bool flag20 = flag19;
						bool flag21 = flag20;
						bool flag22 = flag21;
						if (flag22)
						{
							color2 = CameraBookmarkHotkey.ColorOccupied;
						}
						else
						{
							color2 = CameraBookmarkHotkey.ColorEmpty;
						}
					}
				}
				this.DrawRect(rect2, color2);
				GUI.Label(rect2, (j + 1).ToString(), this._overlaySlotTextStyle);
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
			float num = 420f;
			float num2 = 16f;
			Rect position = new Rect(((float)Screen.width - num) * 0.5f, (float)Screen.height - num2 - 4f, num, num2);
			GUI.Label(position, "Better Than Castle by NotNonlokill (v" + BtcPlugin.Version + ")", this._overlayAttributionStyle);
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

		// True when a non-terrain, non-unit object (a building) is hit by the cursor ray before the terrain voxel.
		private static bool BuildingOnRay(Ray ray, float terrainDist)
		{
			try
			{
				RaycastHit[] hits = Physics.RaycastAll(ray, terrainDist, ~132096, QueryTriggerInteraction.Ignore);
				for (int i = 0; i < hits.Length; i++)
				{
					Collider c = hits[i].collider;
					if (c == null)
					{
						continue;
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
							if (array[i].collider.isTrigger)
							{
								continue;
							}
							GameComponent componentInParent2 = array[i].collider.GetComponentInParent<GameComponent>();
							if (componentInParent2 != null)
							{
								return CameraBookmarkHotkey.IsUnit(componentInParent2.gameObject) ? null : componentInParent2.gameObject;
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
					if (col == null)
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
