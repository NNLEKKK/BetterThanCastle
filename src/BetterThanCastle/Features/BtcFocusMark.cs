using System;
using System.Collections.Generic;
using System.Reflection;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.AI.Damage;
using Brix.Network;
using Brix.UI.GameSelector;
using Brix.Game.Components;
using Brix.Game.Components.Wards;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;

namespace BetterThanCastle
{
// Artificer focus mark (Gameplay > Balance, default OFF, BetterConnection ON only). A hotkey on an enemy while an Artificer is selected
// marks it; every Sentinel Ward of that faction that can fire at it (range, vision, line of sight) shoots it instead of other targets.
// When the mark leaves a ward's reach the ward falls back to vanilla targeting. Wards run on the host: a client sends msg 106 to the host.
// Wire (BC ON only): 106 client->host "targetNetId|mageNetId" (empty target = clear). OFF: no handler, no send, WardAttack stays vanilla.
public static class BtcFocusMark
{
	internal const short MsgMark = 106;
	private static int iOn = -2;
	private static readonly Dictionary<int, GameObject> byFaction = new Dictionary<int, GameObject>();
	private static GameObject localMark;
	private static FieldInfo targetField;
	private static MethodInfo canFire;
	private static MethodInfo targetChanged;
	private static GUIStyle style;

	public static void AddRows()
	{
		BtcGameplay.Add("sent.mark", "rng", "Artificer focus mark (hotkey)", "Метка мага (бинд)", 0f, 0f, 1f, 1f, true, "With an Artificer selected, the 'Artificer focus mark' hotkey marks an enemy under the cursor: all your Sentinel Wards that can shoot it fire at it first. If it leaves a ward's reach, that ward targets as usual. Hotkey is unassigned by default (Hotkeys > Orders).", "При выделенном маге бинд «метка мага» ставит метку на врага под курсором: все ваши Sentinel Ward, которые могут по нему стрелять, бьют сначала по нему. Если цель вышла из зоны варда, тот целится как обычно. Бинд по умолчанию не назначен (Бинды > Приказы).");
	}

	public static bool On
	{
		get
		{
			if (!BtcNetworkGate.Enabled) return false;
			if (iOn == -2) iOn = BtcGameplay.Idx("sent.mark");
			float[] a = BtcGameplay.Active;
			return iOn >= 0 && a != null && iOn < a.Length && a[iOn] > 0.5f;
		}
	}

	private static bool IsMage(Labor l)
	{
		return l != null && l.toolbag != null && l.toolbag.HasTool(Brix.External.Factories.Accessories.Staff);
	}

	// Hotkey press (client side of whoever pressed it).
	public static void Place()
	{
		if (!On) return;
		try
		{
			Camera cam = Camera.main;
			if (cam == null) return;
			List<GameObject> sel = new List<GameObject>();
			UIGameSelector.GetSelectedEverything(ref sel);
			Labor mage = null;
			for (int i = 0; i < sel.Count && mage == null; i++)
			{
				Labor l = sel[i] == null ? null : sel[i].GetComponent<Labor>();
				if (IsMage(l)) mage = l;
			}
			if (mage == null || mage.faction == null) return;
			Ray ray = cam.ScreenPointToRay(UnityEngine.Input.mousePosition);
			RaycastHit[] hits = Physics.RaycastAll(ray, 500f, -1, QueryTriggerInteraction.Ignore);
			Array.Sort<RaycastHit>(hits, (RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance));
			GameObject target = null;
			for (int j = 0; j < hits.Length && target == null; j++)
			{
				Collider col = hits[j].collider;
				if (col == null) continue;
				GameComponent gc = col.GetComponentInParent<GameComponent>();
				if (gc == null || gc.GetComponent<DamageReceiver>() == null) continue;
				if (mage.faction.IsEnemyTo(gc.gameObject)) target = gc.gameObject;
			}
			if (target == null) return;
			bool clear = localMark == target; // same target again = unmark
			localMark = clear ? null : target;
			if (NetworkServer.active) Apply(mage.faction, clear ? null : target);
			else SendToHost(clear ? null : target, mage.gameObject);
		}
		catch (Exception e) { BtcLog.Error("FocusMark", "Place: " + e); }
	}

	private static void Apply(Faction faction, GameObject target)
	{
		int key = faction.GetInstanceID();
		if (target == null) byFaction.Remove(key);
		else byFaction[key] = target;
	}

	private static void SendToHost(GameObject target, GameObject mage)
	{
		UNetManager m = NetworkManager.singleton as UNetManager;
		if (m == null || m.LocalNetworkClient == null || !m.LocalNetworkClient.isConnected) return;
		NetworkIdentity tid = target == null ? null : target.GetComponent<NetworkIdentity>();
		NetworkIdentity mid = mage.GetComponent<NetworkIdentity>();
		if (mid == null) return;
		string t = tid == null ? string.Empty : tid.netId.Value.ToString();
		m.LocalNetworkClient.Send(MsgMark, new StringMessage(t + "|" + mid.netId.Value));
	}

	internal static void RegisterHandlers(NetworkClient clientOverride)
	{
		if (!BtcNetworkGate.Enabled || !NetworkServer.active) return;
		try
		{
			NetworkServer.UnregisterHandler(MsgMark);
			NetworkServer.RegisterHandler(MsgMark, OnMarkAtHost);
		}
		catch (Exception e) { BtcLog.Error("FocusMark", "RegisterHandlers: " + e); }
	}

	private static GameObject Find(string id)
	{
		uint v;
		if (!uint.TryParse(id, out v)) return null;
		NetworkIdentity ni;
		return NetworkServer.objects.TryGetValue(new NetworkInstanceId(v), out ni) && ni != null ? ni.gameObject : null;
	}

	private static void OnMarkAtHost(NetworkMessage nm)
	{
		try
		{
			string[] a = nm.ReadMessage<StringMessage>().value.Split('|');
			if (a.Length < 2 || !On) return;
			GameObject mageGo = Find(a[1]);
			Labor mage = mageGo == null ? null : mageGo.GetComponent<Labor>();
			if (mage == null || mage.faction == null || !IsMage(mage)) return;
			GameObject target = a[0].Length == 0 ? null : Find(a[0]);
			if (target != null && !mage.faction.IsEnemyTo(target)) return;
			Apply(mage.faction, target);
		}
		catch (Exception e) { BtcLog.Error("FocusMark", "OnMarkAtHost: " + e); }
	}

	// WardAttack.UnitsRefresh prefix (host). true = run vanilla.
	internal static bool RefreshWard(WardAttack ward, HashSet<Trackable> units)
	{
		if (byFaction.Count == 0 || !On || ward.faction == null) return true;
		GameObject mark;
		if (!byFaction.TryGetValue(ward.faction.GetInstanceID(), out mark)) return true;
		if (mark == null) { byFaction.Remove(ward.faction.GetInstanceID()); return true; }
		if (targetField == null)
		{
			targetField = typeof(WardAttack).GetField("myTarget", BindingFlags.Instance | BindingFlags.NonPublic);
			canFire = typeof(WardAttack).GetMethod("CanFireAt", BindingFlags.Instance | BindingFlags.NonPublic);
			targetChanged = typeof(WardAttack).GetMethod("TargetChanged", BindingFlags.Instance | BindingFlags.NonPublic);
		}
		Trackable found = null;
		foreach (Trackable u in units)
		{
			if (u != null && u.gameObject == mark) { found = u; break; }
		}
		if (found == null || !(bool)canFire.Invoke(ward, new object[] { mark })) return true;
		if ((Trackable)targetField.GetValue(ward) != found)
		{
			targetField.SetValue(ward, found);
			targetChanged.Invoke(ward, null);
		}
		return false;
	}

	// Local marker icon above the marked unit (marker's own screen only).
	public static void DrawIcon()
	{
		if (localMark == null) return;
		Camera cam = Camera.main;
		if (cam == null) return;
		Vector3 p = cam.WorldToScreenPoint(localMark.transform.position + Vector3.up * 3.2f);
		if (p.z <= 0f) return;
		if (style == null)
		{
			style = new GUIStyle(GUI.skin.label);
			style.fontSize = 22;
			style.fontStyle = FontStyle.Bold;
			style.alignment = TextAnchor.MiddleCenter;
			style.normal.textColor = new Color(1f, 0.15f, 0.15f, 1f);
		}
		GUI.Label(new Rect(p.x - 20f, Screen.height - p.y - 14f, 40f, 28f), "▼", style);
	}
}
}
