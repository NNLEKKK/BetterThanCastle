using System;
using System.Collections.Generic;
using System.Text;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.Components;
using Brix.Network;
using Brix.UI.GameSelector;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;

namespace BetterThanCastle
{
	// "Focus without auto-run" (Advanced Hotkeys > Orders, checkbox focus_hold + key focus_hold_key). Works like the vanilla focus order on an enemy, but
	// ranged units (anything that can fire projectiles) HOLD their position and shoot instead of running after the target; other selected combat units
	// still get the vanilla attack order. The key is either a keyboard combo (acts on the enemy under the cursor when pressed) or a combo ending in the
	// right mouse button (acts on the vanilla order click, so a plain RMB bind does not clash with vanilla Alt/Ctrl+RMB focus: modifiers must match exactly).
	// Wire (BC ON only): 114 client->host "targetNetId|unitNetId,unitNetId,..." (host applies Order.Combat.HoldPositionAndAttackDirectly). OFF: no handler, no send,
	// the feature is inactive and the vanilla order passes untouched.
	public static class BtcFocusHold
	{
		internal const short MsgHold = 114;

		public static bool On
		{
			get { return BtcNetworkGate.Enabled && BtcHotkeys.Enabled && BtcHotkeys.GetBool("focus_hold", true); }
		}

		internal static KeyCode[] Keys()
		{
			return BtcHotkeys.GetKeys("focus_hold_key", null);
		}

		internal static bool IsMouseCombo(KeyCode[] keys)
		{
			return keys != null && keys.Length > 0 && keys[keys.Length - 1] == KeyCode.Mouse1;
		}

		private static bool Ranged(Labor l)
		{
			try { return l != null && l.Occupation != null && l.Occupation.Profession != null && l.Occupation.Profession.ProjectileAttackAvailable(); }
			catch (Exception) { return false; }
		}

		private static bool ModifiersExactly(KeyCode[] keys)
		{
			KeyCode[] mods = { KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftControl, KeyCode.RightControl, KeyCode.LeftAlt, KeyCode.RightAlt };
			for (int i = 0; i < mods.Length; i++)
			{
				bool want = Array.IndexOf(keys, mods[i]) >= 0;
				if (UnityEngine.Input.GetKey(mods[i]) != want) return false;
			}
			for (int i = 0; i < keys.Length - 1; i++) if (!UnityEngine.Input.GetKey(keys[i])) return false;
			return true;
		}

		// DirectOrder(GameObject) prefix: the vanilla right-click order on an enemy. Returns true = run vanilla with the (possibly shortened) list.
		internal static bool OnVanillaOrder(List<GameObject> units, GameObject target)
		{
			try
			{
				if (!On || units == null || target == null) return true;
				KeyCode[] keys = Keys();
				if (!IsMouseCombo(keys) || !UnityEngine.Input.GetKeyUp(KeyCode.Mouse1) || !ModifiersExactly(keys)) return true;
				if (BtcHotkeys.IsSuppressed(keys)) return true;
				List<Labor> ranged = new List<Labor>();
				for (int i = units.Count - 1; i >= 0; i--)
				{
					Labor l = units[i] == null ? null : units[i].GetComponent<Labor>();
					if (l == null || !Ranged(l) || !Affiliation.IsEnemy(l.gameObject, target)) continue;
					ranged.Add(l);
					units.RemoveAt(i);
				}
				if (ranged.Count == 0) return true;
				Issue(ranged, target);
				return units.Count > 0;
			}
			catch (Exception e) { BtcLog.Error("FocusHold", "OnVanillaOrder: " + e); }
			return true;
		}

		// Keyboard / non-RMB key press (called from the hotkey poll).
		public static void OnKey()
		{
			if (!On) return;
			KeyCode[] keys = Keys();
			if (keys == null || keys.Length == 0 || IsMouseCombo(keys)) return;
			try
			{
				Camera cam = Camera.main;
				if (cam == null) return;
				List<GameObject> sel = new List<GameObject>();
				UIGameSelector.GetSelectedEverything(ref sel);
				List<Labor> ranged = new List<Labor>();
				List<Labor> melee = new List<Labor>();
				for (int i = 0; i < sel.Count; i++)
				{
					Labor l = sel[i] == null ? null : sel[i].GetComponent<Labor>();
					if (l == null) continue;
					if (Ranged(l)) ranged.Add(l); else melee.Add(l);
				}
				if (ranged.Count == 0) return;
				Ray ray = cam.ScreenPointToRay(UnityEngine.Input.mousePosition);
				RaycastHit[] hits = Physics.RaycastAll(ray, 500f, -1, QueryTriggerInteraction.Ignore);
				Array.Sort<RaycastHit>(hits, (RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance));
				GameObject target = null;
				for (int j = 0; j < hits.Length && target == null; j++)
				{
					Collider col = hits[j].collider;
					if (col == null) continue;
					GameComponent gc = col.GetComponentInParent<GameComponent>();
					if (gc == null || gc.GetComponent<Brix.Game.AI.Damage.DamageReceiver>() == null) continue;
					if (Affiliation.IsEnemy(ranged[0].gameObject, gc.gameObject)) target = gc.gameObject;
				}
				if (target == null) return;
				Issue(ranged, target);
			}
			catch (Exception e) { BtcLog.Error("FocusHold", "OnKey: " + e); }
		}

		private static void Issue(List<Labor> units, GameObject target)
		{
			BtcFocusFx.OnOrder(units, target);
			if (NetworkServer.active)
			{
				for (int i = 0; i < units.Count; i++) ApplyOne(units[i], target);
				return;
			}
			UNetManager m = NetworkManager.singleton as UNetManager;
			if (m == null || m.LocalNetworkClient == null || !m.LocalNetworkClient.isConnected) return;
			NetworkIdentity tid = target.GetComponent<NetworkIdentity>();
			if (tid == null) return;
			StringBuilder sb = new StringBuilder();
			sb.Append(tid.netId.Value).Append('|');
			int n = 0;
			for (int i = 0; i < units.Count; i++)
			{
				NetworkIdentity ni = units[i].GetComponent<NetworkIdentity>();
				if (ni == null) continue;
				if (n++ > 0) sb.Append(',');
				sb.Append(ni.netId.Value);
				units[i].AssignToIdle();
				Brix.Audio.SoundEngine.Play(units[i].Sounds.Go, units[i].gameObject);
			}
			if (n > 0) m.LocalNetworkClient.Send(MsgHold, new StringMessage(sb.ToString()));
		}

		private static void ApplyOne(Labor l, GameObject target)
		{
			l.AssignToIdle();
			Brix.Audio.SoundEngine.Play(l.Sounds.Go, l.gameObject);
			Order.Combat.HoldPositionAndAttackDirectly.Send(l, target, Labor.DecisionSource.User);
		}

		internal static void RegisterHandlers(NetworkClient clientOverride)
		{
			if (!BtcNetworkGate.Enabled || !NetworkServer.active) return;
			try
			{
				NetworkServer.UnregisterHandler(MsgHold);
				NetworkServer.RegisterHandler(MsgHold, OnHoldAtHost);
			}
			catch (Exception e) { BtcLog.Error("FocusHold", "RegisterHandlers: " + e); }
		}

		private static GameObject Find(string id)
		{
			uint v;
			if (!uint.TryParse(id, out v)) return null;
			NetworkIdentity ni;
			return NetworkServer.objects.TryGetValue(new NetworkInstanceId(v), out ni) && ni != null ? ni.gameObject : null;
		}

		private static void OnHoldAtHost(NetworkMessage nm)
		{
			try
			{
				if (!BtcNetworkGate.Enabled) return;
				string[] a = nm.ReadMessage<StringMessage>().value.Split('|');
				if (a.Length < 2) return;
				GameObject target = Find(a[0]);
				User u = UNetManager.instance != null ? UNetManager.instance.GetUserFromConnection(nm.conn) : null;
				if (target == null || u == null) return;
				string[] ids = a[1].Split(',');
				for (int i = 0; i < ids.Length && i < 200; i++)
				{
					GameObject go = Find(ids[i]);
					Labor l = go == null ? null : go.GetComponent<Labor>();
					if (l == null || !Affiliation.IsOwnedBy(go, u.gameObject) || !Affiliation.IsEnemy(go, target)) continue;
					Order.Combat.HoldPositionAndAttackDirectly.Send(l, target, Labor.DecisionSource.User);
				}
			}
			catch (Exception e) { BtcLog.Error("FocusHold", "OnHoldAtHost: " + e); }
		}
	}
}
