using System;
using System.Collections.Generic;
using System.Reflection;
using Brix.Game;
using Brix.Game.Components;
using Brix.Network;
using UnityEngine;

namespace BetterThanCastle
{
public static class BtcPov
{
	public static bool Active
	{
		get
		{
			return BtcPov.PovFaction != null;
		}
	}

	private static void Log(string s)
	{
		Debug.Log("[BtcPov] " + s);
	}

	public static List<BtcPov.PlayerInfo> Players()
	{
		List<BtcPov.PlayerInfo> list = new List<BtcPov.PlayerInfo>();
		try
		{
			UNetManager instance = UNetManager.instance;
			bool flag = instance == null || instance.UserList == null;
			bool flag2 = flag;
			bool flag3 = flag2;
			if (flag3)
			{
				if (BtcReplay.Mode != BtcReplay.State.Playing) return list;
			}
			else
			foreach (User user in instance.UserList)
			{
				bool flag4 = user == null || user.faction == null || user.faction.IsSpectator;
				bool flag5 = !flag4;
				bool flag6 = flag5;
				if (flag6)
				{
					bool flag7 = false;
					for (int i = 0; i < list.Count; i++)
					{
						bool flag8 = list[i].faction == user.faction;
						bool flag9 = flag8;
						bool flag10 = flag9;
						if (flag10)
						{
							flag7 = true;
						}
					}
					bool flag11 = flag7;
					bool flag12 = !flag11;
					bool flag13 = flag12;
					if (flag13)
					{
						string text = user.ScreenName;
						bool flag14 = string.IsNullOrEmpty(text);
						bool flag15 = flag14;
						bool flag16 = flag15;
						if (flag16)
						{
							text = user.faction.name;
						}
						list.Add(new BtcPov.PlayerInfo
						{
							faction = user.faction,
							name = text
						});
					}
				}
			}
		}
		catch (Exception ex)
		{
			BtcPov.Log("Players: " + ex.Message);
		}
		try { BtcPov.AddReplayPlayers(list); }
		catch (Exception ex) { BtcPov.Log("Players(replay): " + ex.Message); }
		list.Sort(delegate (PlayerInfo a, PlayerInfo b) { return a.faction.ID.CompareTo(b.faction.ID); });
		foreach (PlayerInfo pi in list) pi.name = BtcStr.Plain(pi.name);
		return list;
	}

	/// <summary>Replay: the header lists only the players known at the start, so the recorded camera tracks and nicks add the rest (clients).</summary>
	private static void AddReplayPlayers(List<BtcPov.PlayerInfo> list)
	{
		if (BtcReplay.Mode != BtcReplay.State.Playing) return;
		HashSet<int> ids = new HashSet<int>();
		foreach (int id in BtcReplayNames.Ids) ids.Add(id);
		for (int i = 0; i < BtcPovRec.Play.Count; i++) ids.Add(BtcPovRec.Play[i].Fac);
		foreach (int id in ids)
		{
			Faction f = BtcMatchStats.FactionOf(id);
			if (f == null || !BtcMatchStats.Tracks(f)) continue;
			bool have = false;
			for (int i = 0; i < list.Count; i++) if (list[i].faction == f) { have = true; break; }
			if (have) continue;
			string n = BtcReplayNames.NickOf(id);
			BtcPovRec.Track t = BtcPovRec.TrackOf(id);
			if (string.IsNullOrEmpty(n) && t != null) n = t.Name;
			if (string.IsNullOrEmpty(n)) n = f.name;
			list.Add(new BtcPov.PlayerInfo { faction = f, name = n });
		}
	}

	public static void SetPov(Faction f, string name)
	{
		try
		{
			bool flag = f == null;
			bool flag2 = flag;
			bool flag3 = flag2;
			if (flag3)
			{
				BtcPov.Reset();
			}
			else
			{
				BtcPov.PovFaction = f;
				BtcPov.PovName = (name ?? "");
				LocalFactionVision.AllVisible = false;
				BtcPov.SetRendererFaction(f);
				BtcPov.nextTick = 0f;
				BtcPov.Tick(Time.unscaledTime);
				BtcPov.Log("POV: " + BtcPov.PovName);
			}
		}
		catch (Exception ex)
		{
			BtcPov.Log("SetPov: " + ex.Message);
		}
	}

	public static void Cycle(int dir)
	{
		List<BtcPov.PlayerInfo> list = BtcPov.Players();
		int num = 0;
		for (int i = 0; i < list.Count; i++)
		{
			bool flag = list[i].faction == BtcPov.PovFaction;
			bool flag2 = flag;
			bool flag3 = flag2;
			if (flag3)
			{
				num = i + 1;
			}
		}
		int num2 = list.Count + 1;
		num = ((num + dir) % num2 + num2) % num2;
		bool flag4 = num == 0;
		bool flag5 = flag4;
		bool flag6 = flag5;
		if (flag6)
		{
			BtcPov.Reset();
		}
		else
		{
			BtcPov.SetPov(list[num - 1].faction, list[num - 1].name);
		}
	}

	private static void SetRendererFaction(Faction f)
	{
		FogOfWarRenderer fogOfWarRenderer = UnityEngine.Object.FindObjectOfType<FogOfWarRenderer>();
		bool flag = fogOfWarRenderer == null;
		bool flag2 = !flag;
		bool flag3 = flag2;
		if (flag3)
		{
			bool flag4 = !BtcPov.rendererTouched;
			bool flag5 = flag4;
			bool flag6 = flag5;
			if (flag6)
			{
				BtcPov.rendererFactionBackup = fogOfWarRenderer.faction;
				BtcPov.rendererTouched = true;
			}
			fogOfWarRenderer.faction = f;
			FieldInfo field = typeof(FogOfWarRenderer).GetField("_allVisions", BindingFlags.Instance | BindingFlags.NonPublic);
			bool flag7 = field == null;
			bool flag8 = !flag7;
			bool flag9 = flag8;
			if (flag9)
			{
				HashSet<LocalFactionVision> hashSet = field.GetValue(fogOfWarRenderer) as HashSet<LocalFactionVision>;
				bool flag10 = hashSet == null;
				bool flag11 = !flag10;
				bool flag12 = flag11;
				if (flag12)
				{
					hashSet.Clear();
					bool flag13 = f != null;
					bool flag14 = flag13;
					bool flag15 = flag14;
					if (flag15)
					{
						foreach (LocalFactionVision localFactionVision in UIGameObserver.fogObjects.CheckCreated())
						{
							bool flag16 = localFactionVision != null && localFactionVision.LineOfSightRange > 0 && f.IsAlliedTo(localFactionVision.gameObject);
							bool flag17 = flag16;
							bool flag18 = flag17;
							if (flag18)
							{
								hashSet.Add(localFactionVision);
							}
						}
					}
					FieldInfo field2 = typeof(FogOfWarRenderer).GetField("allVisionCount", BindingFlags.Instance | BindingFlags.Public);
					bool flag19 = field2 != null;
					bool flag20 = flag19;
					bool flag21 = flag20;
					if (flag21)
					{
						field2.SetValue(fogOfWarRenderer, hashSet.Count);
					}
				}
			}
		}
	}

	public static void Tick(float now)
	{
		try
		{
			BtcPov.TickUi(now);
			BtcPov.TryResume(now);
			bool flag = BtcPov.PovFaction == null;
			bool flag2 = !flag;
			bool flag3 = flag2;
			if (flag3)
			{
				bool flag4 = now < BtcPov.nextTick;
				bool flag5 = !flag4;
				bool flag6 = flag5;
				if (flag6)
				{
					BtcPov.nextTick = now + 0.04f;
					LocalFactionVision.AllVisible = false;
					BtcPov.ApplyFog();
					bool flag7 = now >= BtcPov.fogSyncAt;
					bool flag8 = flag7;
					if (flag8)
					{
						BtcPov.fogSyncAt = now + 1f;
						BtcPov.SetRendererFaction(BtcPov.PovFaction);
					}
				}
			}
		}
		catch (Exception ex)
		{
			BtcPov.Log("Tick: " + ex.Message);
		}
	}

	private static void ApplyFog()
	{
		Faction povFaction = BtcPov.PovFaction;
		HashSet<LocalFactionVision> hashSet = UIGameObserver.fogObjects.CheckCreated();
		List<Vector3> list = BtcPov.posBuf;
		List<float> list2 = BtcPov.rngBuf;
		list.Clear();
		list2.Clear();
		foreach (LocalFactionVision localFactionVision in hashSet)
		{
			bool flag = localFactionVision == null || localFactionVision.LineOfSightRange <= 0;
			bool flag2 = !flag;
			bool flag3 = flag2;
			if (flag3)
			{
				bool flag4 = povFaction.IsAlliedTo(localFactionVision.gameObject);
				bool flag5 = flag4;
				bool flag6 = flag5;
				if (flag6)
				{
					list.Add(localFactionVision.transform.position);
					float num = (float)localFactionVision.LineOfSightRange;
					list2.Add(num * num);
				}
			}
		}
		foreach (LocalFactionVision localFactionVision2 in hashSet)
		{
			bool flag7 = localFactionVision2 == null;
			bool flag8 = !flag7;
			bool flag9 = flag8;
			if (flag9)
			{
				bool flag10 = BtcPov.IsVisible(localFactionVision2, povFaction, list, list2);
				bool flag12;
				bool flag11 = BtcPov.hidden.TryGetValue(localFactionVision2, out flag12) && flag12 == !flag10;
				bool flag13 = !flag11;
				bool flag14 = flag13;
				if (flag14)
				{
					bool flag15 = !BtcPov.hidden.ContainsKey(localFactionVision2) && flag10;
					bool flag16 = !flag15;
					bool flag17 = flag16;
					if (flag17)
					{
						BtcPov.SetRenderers(localFactionVision2, flag10);
						BtcPov.hidden[localFactionVision2] = !flag10;
					}
				}
			}
		}
		List<LocalFactionVision> list3 = null;
		foreach (KeyValuePair<LocalFactionVision, bool> keyValuePair in BtcPov.hidden)
		{
			bool flag18 = keyValuePair.Key == null;
			bool flag19 = flag18;
			bool flag20 = flag19;
			if (flag20)
			{
				bool flag21 = list3 == null;
				bool flag22 = flag21;
				bool flag23 = flag22;
				if (flag23)
				{
					list3 = new List<LocalFactionVision>();
				}
				list3.Add(keyValuePair.Key);
			}
		}
		bool flag24 = list3 != null;
		bool flag25 = flag24;
		bool flag26 = flag25;
		if (flag26)
		{
			for (int i = 0; i < list3.Count; i++)
			{
				BtcPov.hidden.Remove(list3[i]);
			}
		}
	}

	private static bool IsVisible(LocalFactionVision o, Faction pov, List<Vector3> pos, List<float> rng)
	{
		bool flag = o.AlwaysVisible && !o.AlwaysInvisibleForNonAllied;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool result;
		if (flag3)
		{
			result = true;
		}
		else
		{
			GameObject gameObject = o.gameObject;
			bool flag4 = pov.IsAlliedTo(gameObject);
			bool flag5 = flag4;
			bool flag6 = flag5;
			if (flag6)
			{
				result = true;
			}
			else
			{
				bool alwaysInvisibleForNonAllied = o.AlwaysInvisibleForNonAllied;
				bool flag7 = alwaysInvisibleForNonAllied;
				bool flag8 = flag7;
				if (flag8)
				{
					result = false;
				}
				else
				{
					Vector3 position = o.transform.position;
					for (int i = 0; i < pos.Count; i++)
					{
						bool flag9 = (pos[i] - position).sqrMagnitude <= rng[i];
						bool flag10 = flag9;
						bool flag11 = flag10;
						if (flag11)
						{
							return true;
						}
					}
					result = false;
				}
			}
		}
		return result;
	}

	private static void SetRenderers(LocalFactionVision o, bool vis)
	{
		foreach (Renderer renderer in o.GetComponentsInChildren<Renderer>(true))
		{
			bool flag = !(renderer is LineRenderer);
			bool flag2 = flag;
			bool flag3 = flag2;
			if (flag3)
			{
				renderer.enabled = vis;
			}
		}
	}

	public static void Reset()
	{
		try
		{
			bool flag = BtcPov.PovFaction != null;
			BtcPov.PovFaction = null;
			BtcPov.PovName = "";
			BtcPov.resumeName = null;
			bool flag2 = flag || BtcPov.hidden.Count > 0;
			bool flag3 = flag2;
			bool flag4 = flag3;
			if (flag4)
			{
				foreach (KeyValuePair<LocalFactionVision, bool> keyValuePair in BtcPov.hidden)
				{
					bool flag5 = keyValuePair.Key != null && keyValuePair.Value;
					bool flag6 = flag5;
					bool flag7 = flag6;
					if (flag7)
					{
						BtcPov.SetRenderers(keyValuePair.Key, true);
					}
				}
				BtcPov.hidden.Clear();
				bool flag8 = flag;
				bool flag9 = flag8;
				bool flag10 = flag9;
				if (flag10)
				{
					LocalFactionVision.AllVisible = true;
					bool flag11 = BtcPov.rendererTouched;
					bool flag12 = flag11;
					bool flag13 = flag12;
					if (flag13)
					{
						BtcPov.SetRendererFaction(BtcPov.rendererFactionBackup);
						BtcPov.rendererTouched = false;
					}
				}
			}
		}
		catch (Exception ex)
		{
			BtcPov.Log("Reset: " + ex.Message);
		}
	}

	public static void Shutdown()
	{
		BtcPov.Reset();
		BtcPov.SetUiHidden(false);
	}

	public static void ToggleUi()
	{
		BtcPov.SetUiHidden(!BtcPov.UiHidden);
	}

	public static void SetUiHidden(bool hide)
	{
		BtcPov.UiHidden = hide;
		BtcPov.ApplyUi();
	}

	private static void ApplyUi()
	{
		try
		{
			GameObject gameObject = GameObject.Find("Canvas");
			bool flag = gameObject == null;
			bool flag2 = !flag;
			bool flag3 = flag2;
			if (flag3)
			{
				Canvas component = gameObject.GetComponent<Canvas>();
				bool flag4 = component != null && component.enabled == BtcPov.UiHidden;
				bool flag5 = flag4;
				bool flag6 = flag5;
				if (flag6)
				{
					component.enabled = !BtcPov.UiHidden;
				}
			}
		}
		catch (Exception ex)
		{
			BtcPov.Log("ApplyUi: " + ex.Message);
		}
	}

	private static void TickUi(float now)
	{
		bool keyDown = Input.GetKeyDown(KeyCode.F8);
		bool flag = keyDown;
		bool flag2 = flag;
		if (flag2)
		{
			BtcReplayUI.ToggleHidden(); // F8 hides the replay panel, the game UI stays
		}
		if (Input.GetKeyDown(KeyCode.F7) && BtcReplay.Mode == BtcReplay.State.Playing) BtcReplayLayout.ToggleEdit();
		bool flag3 = now < BtcPov.nextUi;
		bool flag4 = !flag3;
		bool flag5 = flag4;
		if (flag5)
		{
			BtcPov.nextUi = now + 0.5f;
			bool uiHidden = BtcPov.UiHidden;
			bool flag6 = uiHidden;
			bool flag7 = flag6;
			if (flag7)
			{
				BtcPov.ApplyUi();
			}
		}
	}

	public static void OnRewind()
	{
		try
		{
			bool flag = !string.IsNullOrEmpty(BtcPov.PovName);
			bool flag2 = flag;
			if (flag2)
			{
				BtcPov.resumeName = BtcPov.PovName;
				BtcPov.resumeAt = Time.unscaledTime + 1f;
			}
			BtcPov.PovFaction = null;
			BtcPov.hidden.Clear();
			BtcPov.rendererTouched = false;
			BtcPov.rendererFactionBackup = null;
			LocalFactionVision.AllVisible = true;
		}
		catch (Exception ex)
		{
			BtcPov.Log("OnRewind: " + ex.Message);
		}
	}

	private static void TryResume(float now)
	{
		bool flag = BtcPov.resumeName == null || now < BtcPov.resumeAt;
		bool flag2 = !flag;
		if (flag2)
		{
			BtcPov.resumeAt = now + 0.5f;
			bool flag3 = UnityEngine.Object.FindObjectOfType<FogOfWarRenderer>() == null;
			bool flag4 = !flag3;
			if (flag4)
			{
				List<BtcPov.PlayerInfo> list = BtcPov.Players();
				for (int i = 0; i < list.Count; i++)
				{
					bool flag5 = list[i].faction != null && list[i].name == BtcPov.resumeName;
					bool flag6 = flag5;
					if (flag6)
					{
						string name = BtcPov.resumeName;
						BtcPov.resumeName = null;
						BtcPov.SetPov(list[i].faction, name);
						break;
					}
				}
			}
		}
	}

	public static Faction PovFaction;

	public static string PovName = "";

	public static bool UiHidden;

	private static float nextTick;

	private static float nextUi;

	private static Faction rendererFactionBackup;

	private static bool rendererTouched;

	private static readonly Dictionary<LocalFactionVision, bool> hidden = new Dictionary<LocalFactionVision, bool>();

	private static readonly List<Vector3> posBuf = new List<Vector3>();

	private static readonly List<float> rngBuf = new List<float>();

	private static string resumeName;

	private static float resumeAt;

	private static float fogSyncAt;

	public class PlayerInfo
	{
		public Faction faction;

		public string name;
	}
}
}
