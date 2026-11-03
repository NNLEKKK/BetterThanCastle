using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Brix.Engine;
using Brix.Engine.Abstract;
using Brix.Game;
using Brix.Game.AI;
using Brix.UI.GameSelector;
using Brix.Util;
using UnityEngine;

namespace BetterThanCastle
{
public static class BtcWaypoints
{
	public static void Tick(bool master)
	{
		BtcWaypoints.Load();
		BtcWaypoints.HandleKey();
		bool flag = BtcWaypoints.tracks.Count == 0;
		if (flag)
		{
			bool flag2 = BtcWaypoints.vis.Count > 0;
			if (flag2)
			{
				BtcWaypoints.vis.Clear();
			}
		}
		else
		{
			float unscaledTime = Time.unscaledTime;
			bool flag3 = unscaledTime < BtcWaypoints.nextTick;
			if (!flag3)
			{
				BtcWaypoints.nextTick = unscaledTime + 0.05f;
				BtcWaypoints.Advance();
				BtcWaypoints.BuildVisible(master);
			}
		}
	}

	public static void OnManualOrder(Labor labor)
	{
		bool flag = BtcWaypoints.sending || BtcWaypoints.tracks.Count == 0 || labor == null;
		if (!flag)
		{
			BtcWaypoints.tracks.Remove(labor);
		}
	}

	private static void HandleKey()
	{
		bool flag = !BtcHotkeys.Enabled || BtcReplay.Active;
		if (!flag)
		{
			KeyCode[] keys = BtcHotkeys.GetKeys("waypoint_add", null);
			bool flag2 = keys == null || keys.Length == 0 || !BtcHotkeys.ComboPressed(keys);
			if (!flag2)
			{
				Camera main = Camera.main;
				bool flag3 = main == null;
				if (!flag3)
				{
					Ray ray = main.ScreenPointToRay(Input.mousePosition);
					RaycastHit raycastHit;
					bool flag4 = !Physics.Raycast(ray, out raycastHit, 500f, 132096, QueryTriggerInteraction.Collide);
					if (!flag4)
					{
						Vector3 vecteur = Calcule.RoundVecteur(raycastHit.point - raycastHit.normal * 0.0001f);
						int num = Brix.Engine.Abstract.Voxel.GetScalaire(vecteur);
						bool flag5 = !Brix.Engine.Voxel.IsOpenAndWalkable(XYZ.FromScalaire(num));
						if (flag5)
						{
							num += 1048576;
						}
						Vector3 vecteur2 = Brix.Engine.Abstract.Voxel.GetVecteur(num);
						BtcWaypoints.selBuf.Clear();
						UIGameSelector.GetSelectedEverything(ref BtcWaypoints.selBuf);
						BtcWaypoints.WP wp = null;
						for (int i = 0; i < BtcWaypoints.selBuf.Count; i++)
						{
							GameObject gameObject = BtcWaypoints.selBuf[i];
							bool flag6 = !(gameObject == null) && Affiliation.BelongsToMe(gameObject);
							if (flag6)
							{
								Labor component = gameObject.GetComponent<Labor>();
								bool flag7 = !(component == null);
								if (flag7)
								{
									bool flag8 = wp == null;
									if (flag8)
									{
										wp = new BtcWaypoints.WP();
										wp.pos = vecteur2;
									}
									BtcWaypoints.Track track = null;
									bool flag9 = BtcWaypoints.tracks.TryGetValue(component, out track) && track.q.Count > 0;
									if (flag9)
									{
										track.q.Add(wp);
									}
									else
									{
										track = new BtcWaypoints.Track();
										track.labor = component;
										track.q.Add(wp);
										BtcWaypoints.tracks[component] = track;
										BtcWaypoints.Go(track, component, wp);
									}
								}
							}
						}
						bool flag10 = wp != null;
						if (flag10)
						{
							BtcWaypoints.nextTick = 0f;
						}
					}
				}
			}
		}
	}

	private static void Go(BtcWaypoints.Track t, Labor labor, BtcWaypoints.WP wp)
	{
		t.lastProg = Time.time;
		t.sentT = Time.time;
		t.still = 0f;
		t.lastT = Time.time;
		t.lastPos = labor.transform.position;
		t.lastDist = (labor.transform.position - wp.pos).magnitude;
		BtcWaypoints.sending = true;
		try
		{
			DirectOrderProject.AddMoveToGoal(labor, wp.pos, false, false);
		}
		finally
		{
			BtcWaypoints.sending = false;
		}
	}

	private static void Advance()
	{
		BtcWaypoints.keyBuf.Clear();
		foreach (KeyValuePair<Labor, BtcWaypoints.Track> keyValuePair in BtcWaypoints.tracks)
		{
			BtcWaypoints.keyBuf.Add(keyValuePair.Key);
		}
		float time = Time.time;
		float @float = BtcHotkeys.GetFloat("waypoint_radius", 2f);
		float float2 = BtcHotkeys.GetFloat("waypoint_lead", 0.35f);
		float float3 = BtcHotkeys.GetFloat("waypoint_stuck", 2f);
		for (int i = 0; i < BtcWaypoints.keyBuf.Count; i++)
		{
			Labor labor = BtcWaypoints.keyBuf[i];
			BtcWaypoints.Track track = null;
			bool flag = labor == null || !BtcWaypoints.tracks.TryGetValue(labor, out track) || track.q.Count == 0;
			if (flag)
			{
				BtcWaypoints.tracks.Remove(labor);
			}
			else
			{
				BtcWaypoints.WP wp = track.q[0];
				Vector3 position = labor.transform.position;
				float num = time - track.lastT;
				bool flag2 = num > 0.0001f;
				if (flag2)
				{
					float b = Mathf.Min(Mathf.Sqrt((position.x - track.lastPos.x) * (position.x - track.lastPos.x) + (position.z - track.lastPos.z) * (position.z - track.lastPos.z)) / num, 12f);
					track.speed = Mathf.Lerp(track.speed, b, 0.4f);
					track.lastPos = position;
					track.lastT = time;
					bool flag3 = time - track.sentT > 1f && track.speed < 0.3f;
					if (flag3)
					{
						track.still += num;
					}
					else
					{
						track.still = 0f;
					}
				}
				Vector3 vector = position - wp.pos;
				float num2 = Mathf.Sqrt(vector.x * vector.x + vector.z * vector.z);
				bool flag4 = num2 <= @float + ((track.q.Count > 1) ? (track.speed * float2) : 0f) && Mathf.Abs(vector.y) <= 2.5f;
				bool flag5 = !flag4 && float3 > 0f && track.still >= float3;
				if (flag5)
				{
					flag4 = true;
				}
				bool flag6 = !flag4 && BtcWaypoints.timeout > 0f;
				if (flag6)
				{
					bool flag7 = num2 < track.lastDist - 0.5f;
					if (flag7)
					{
						track.lastDist = num2;
						track.lastProg = time;
					}
					else
					{
						bool flag8 = time - track.lastProg > BtcWaypoints.timeout;
						if (flag8)
						{
							flag4 = true;
						}
					}
				}
				bool flag9 = flag4;
				if (flag9)
				{
					track.q.RemoveAt(0);
					bool flag10 = track.q.Count == 0;
					if (flag10)
					{
						BtcWaypoints.tracks.Remove(labor);
					}
					else
					{
						BtcWaypoints.Go(track, labor, track.q[0]);
					}
				}
			}
		}
	}

	private static void BuildVisible(bool master)
	{
		BtcWaypoints.vis.Clear();
		bool flag = !master || !BtcWaypoints.on || BtcWaypoints.tracks.Count == 0;
		if (!flag)
		{
			BtcWaypoints.selSet.Clear();
			bool flag2 = BtcWaypoints.onlySel;
			if (flag2)
			{
				BtcWaypoints.selBuf.Clear();
				UIGameSelector.GetSelectedEverything(ref BtcWaypoints.selBuf);
				for (int i = 0; i < BtcWaypoints.selBuf.Count; i++)
				{
					bool flag3 = BtcWaypoints.selBuf[i] != null;
					if (flag3)
					{
						BtcWaypoints.selSet.Add(BtcWaypoints.selBuf[i]);
					}
				}
			}
			BtcWaypoints.pass++;
			foreach (KeyValuePair<Labor, BtcWaypoints.Track> keyValuePair in BtcWaypoints.tracks)
			{
				Labor key = keyValuePair.Key;
				bool flag4 = !(key == null) && (!BtcWaypoints.onlySel || BtcWaypoints.selSet.Contains(key.gameObject));
				if (flag4)
				{
					List<BtcWaypoints.WP> q = keyValuePair.Value.q;
					for (int j = 0; j < q.Count; j++)
					{
						BtcWaypoints.WP wp = q[j];
						bool flag5 = wp.stamp != BtcWaypoints.pass;
						if (flag5)
						{
							wp.stamp = BtcWaypoints.pass;
							wp.num = 0;
							BtcWaypoints.vis.Add(wp);
						}
						bool flag6 = j + 1 > wp.num;
						if (flag6)
						{
							wp.num = j + 1;
						}
					}
				}
			}
		}
	}

	public static void Render(Camera cam, bool master)
	{
		bool flag = cam == null || BtcWaypoints.vis.Count == 0 || !master || !BtcWaypoints.on || cam != Camera.main || !BtcWaypoints.EnsureMat();
		if (!flag)
		{
			Color c = BtcWaypoints.Presets[BtcWaypoints.colorIdx];
			c.a = BtcWaypoints.alpha;
			Vector3 a = cam.transform.right * BtcWaypoints.size;
			Vector3 a2 = cam.transform.up * BtcWaypoints.size;
			BtcWaypoints.mat.SetPass(0);
			GL.PushMatrix();
			GL.Begin(7);
			GL.Color(c);
			for (int i = 0; i < BtcWaypoints.vis.Count; i++)
			{
				Vector3 vector = BtcWaypoints.vis[i].pos + Vector3.up * 0.15f;
				for (int j = 0; j < 4; j++)
				{
					float num = (float)j * 1.5707964f;
					float f = num + 0.7853982f;
					float f2 = num + 1.5707964f;
					GL.Vertex(vector);
					GL.Vertex(vector + a * Mathf.Cos(num) + a2 * Mathf.Sin(num));
					GL.Vertex(vector + a * Mathf.Cos(f) * 1.08f + a2 * Mathf.Sin(f) * 1.08f);
					GL.Vertex(vector + a * Mathf.Cos(f2) + a2 * Mathf.Sin(f2));
				}
			}
			GL.End();
			GL.PopMatrix();
		}
	}

	public static void OnGUI(bool master)
	{
		bool flag = !BtcWaypoints.labels || BtcWaypoints.vis.Count == 0 || !master || !BtcWaypoints.on || Event.current.type != EventType.Repaint;
		if (!flag)
		{
			Camera main = Camera.main;
			bool flag2 = main == null;
			if (!flag2)
			{
				bool flag3 = BtcWaypoints.style == null;
				if (flag3)
				{
					BtcWaypoints.style = new GUIStyle(GUI.skin.label);
					BtcWaypoints.style.alignment = TextAnchor.MiddleCenter;
					BtcWaypoints.style.fontStyle = FontStyle.Bold;
					BtcWaypoints.style.fontSize = 14;
				}
				Color textColor = BtcWaypoints.Presets[BtcWaypoints.colorIdx];
				textColor.a = 1f;
				BtcWaypoints.style.normal.textColor = textColor;
				for (int i = 0; i < BtcWaypoints.vis.Count; i++)
				{
					Vector3 vector = main.WorldToScreenPoint(BtcWaypoints.vis[i].pos + Vector3.up * (0.5f + BtcWaypoints.size));
					bool flag4 = vector.z > 0f;
					if (flag4)
					{
						GUI.Label(new Rect(vector.x - 30f, (float)Screen.height - vector.y - 12f, 60f, 24f), "#" + BtcWaypoints.vis[i].num, BtcWaypoints.style);
					}
				}
			}
		}
	}

	public static void DrawSettings(Func<string, bool, bool> check, Func<string, string, bool> step)
	{
		BtcWaypoints.Load();
		bool flag = false;
		bool flag2 = check(BtcLang.Tr("Waypoints: show on map"), BtcWaypoints.on);
		if (flag2)
		{
			BtcWaypoints.on = !BtcWaypoints.on;
			flag = true;
		}
		bool flag3 = BtcWaypoints.on;
		if (flag3)
		{
			bool flag4 = check(BtcLang.Tr("Waypoints: only for selected units"), BtcWaypoints.onlySel);
			if (flag4)
			{
				BtcWaypoints.onlySel = !BtcWaypoints.onlySel;
				flag = true;
			}
			bool flag5 = check(BtcLang.Tr("Waypoints: show numbers"), BtcWaypoints.labels);
			if (flag5)
			{
				BtcWaypoints.labels = !BtcWaypoints.labels;
				flag = true;
			}
			bool flag6 = step(BtcLang.Tr("Waypoint size"), BtcWaypoints.F(BtcWaypoints.size));
			if (flag6)
			{
				BtcWaypoints.size = BtcWaypoints.Next(BtcWaypoints.size, new float[]
				{
					0.15f,
					0.25f,
					0.4f,
					0.6f
				});
				flag = true;
			}
			bool flag7 = step(BtcLang.Tr("Waypoint opacity"), BtcWaypoints.F(BtcWaypoints.alpha));
			if (flag7)
			{
				BtcWaypoints.alpha = BtcWaypoints.Next(BtcWaypoints.alpha, new float[]
				{
					0.25f,
					0.5f,
					0.7f,
					0.9f,
					1f
				});
				flag = true;
			}
			bool flag8 = step(BtcLang.Tr("Waypoint color"), "#" + (BtcWaypoints.colorIdx + 1));
			if (flag8)
			{
				BtcWaypoints.colorIdx = (BtcWaypoints.colorIdx + 1) % BtcWaypoints.Presets.Length;
				flag = true;
			}
		}
		bool flag9 = step(BtcLang.Tr("Skip unreachable waypoint after (sec, 0 = never)"), BtcWaypoints.F(BtcWaypoints.timeout));
		if (flag9)
		{
			BtcWaypoints.timeout = BtcWaypoints.Next(BtcWaypoints.timeout, new float[]
			{
				0f,
				10f,
				20f,
				30f,
				60f
			});
			flag = true;
		}
		bool flag10 = flag;
		if (flag10)
		{
			BtcWaypoints.Save();
			BtcWaypoints.nextTick = 0f;
		}
	}

	private static void Load()
	{
		bool flag = BtcWaypoints.loaded;
		if (!flag)
		{
			BtcWaypoints.loaded = true;
			BtcPaths.Init();
			BtcWaypoints.on = BtcWaypoints.RB("show.on", BtcWaypoints.on);
			BtcWaypoints.onlySel = BtcWaypoints.RB("show.selected_only", BtcWaypoints.onlySel);
			BtcWaypoints.labels = BtcWaypoints.RB("show.numbers", BtcWaypoints.labels);
			BtcWaypoints.size = BtcWaypoints.RF("size", BtcWaypoints.size);
			BtcWaypoints.alpha = BtcWaypoints.RF("alpha", BtcWaypoints.alpha);
			BtcWaypoints.colorIdx = Mathf.Clamp((int)BtcWaypoints.RF("color", (float)BtcWaypoints.colorIdx), 0, BtcWaypoints.Presets.Length - 1);
			BtcWaypoints.timeout = BtcWaypoints.RF("timeout", BtcWaypoints.timeout);
		}
	}

	private static void Save()
	{
		try
		{
			List<string> list = new List<string>();
			list.Add("# Better Than Castle: waypoints. show.on=0 hides the dots (units still follow the queue); show.selected_only=1 draws only waypoints of selected units; timeout=0 never skips");
			list.Add("show.on=" + (BtcWaypoints.on ? "1" : "0"));
			list.Add("show.selected_only=" + (BtcWaypoints.onlySel ? "1" : "0"));
			list.Add("show.numbers=" + (BtcWaypoints.labels ? "1" : "0"));
			list.Add("size=" + BtcWaypoints.F(BtcWaypoints.size));
			list.Add("alpha=" + BtcWaypoints.F(BtcWaypoints.alpha));
			list.Add("color=" + BtcWaypoints.colorIdx);
			list.Add("timeout=" + BtcWaypoints.F(BtcWaypoints.timeout));
			File.WriteAllLines(BtcPaths.Config("waypoints.txt"), list.ToArray());
		}
		catch (Exception)
		{
		}
	}

	private static bool RB(string k, bool d)
	{
		return BtcPaths.ReadKey("waypoints.txt", k, d ? "1" : "0") != "0";
	}

	private static float RF(string k, float d)
	{
		float num = 0f;
		return float.TryParse(BtcPaths.ReadKey("waypoints.txt", k, d.ToString(CultureInfo.InvariantCulture)), NumberStyles.Float, CultureInfo.InvariantCulture, out num) ? num : d;
	}

	private static string F(float v)
	{
		return v.ToString("0.###", CultureInfo.InvariantCulture);
	}

	private static float Next(float cur, float[] vals)
	{
		for (int i = 0; i < vals.Length; i++)
		{
			bool flag = Mathf.Abs(vals[i] - cur) < 0.0005f;
			if (flag)
			{
				return vals[(i + 1) % vals.Length];
			}
		}
		return vals[0];
	}

	private static bool EnsureMat()
	{
		bool flag = BtcWaypoints.mat != null;
		bool result;
		if (flag)
		{
			result = true;
		}
		else
		{
			Shader shader = Shader.Find("Hidden/Internal-Colored");
			bool flag2 = shader == null;
			if (flag2)
			{
				shader = Shader.Find("Sprites/Default");
			}
			bool flag3 = shader == null;
			if (flag3)
			{
				result = false;
			}
			else
			{
				BtcWaypoints.mat = new Material(shader);
				BtcWaypoints.mat.hideFlags = HideFlags.HideAndDontSave;
				bool flag4 = BtcWaypoints.mat.HasProperty("_Cull");
				if (flag4)
				{
					BtcWaypoints.mat.SetInt("_Cull", 0);
				}
				bool flag5 = BtcWaypoints.mat.HasProperty("_ZWrite");
				if (flag5)
				{
					BtcWaypoints.mat.SetInt("_ZWrite", 0);
				}
				bool flag6 = BtcWaypoints.mat.HasProperty("_ZTest");
				if (flag6)
				{
					BtcWaypoints.mat.SetInt("_ZTest", 8);
				}
				bool flag7 = BtcWaypoints.mat.HasProperty("_SrcBlend");
				if (flag7)
				{
					BtcWaypoints.mat.SetInt("_SrcBlend", 5);
				}
				bool flag8 = BtcWaypoints.mat.HasProperty("_DstBlend");
				if (flag8)
				{
					BtcWaypoints.mat.SetInt("_DstBlend", 10);
				}
				result = true;
			}
		}
		return result;
	}

	public static void OnClientOrder(List<GameObject> actors)
	{
		bool flag = BtcWaypoints.tracks.Count == 0 || actors == null;
		if (!flag)
		{
			for (int i = 0; i < actors.Count; i++)
			{
				bool flag2 = !(actors[i] == null);
				if (flag2)
				{
					Labor component = actors[i].GetComponent<Labor>();
					bool flag3 = !(component == null);
					if (flag3)
					{
						BtcWaypoints.tracks.Remove(component);
					}
				}
			}
		}
	}

	private static bool on = true;

	private static bool onlySel = false;

	private static bool labels = true;

	private static float size = 0.25f;

	private static float alpha = 0.9f;

	private static int colorIdx = 0;

	private static float timeout = 30f;

	private static bool loaded = false;

	private static bool sending = false;

	private static float nextTick = 0f;

	private static int pass = 0;

	private static Material mat;

	private static GUIStyle style;

	private static readonly Color[] Presets = new Color[]
	{
		new Color(1f, 0.85f, 0.1f),
		new Color(0.2f, 0.9f, 1f),
		new Color(0.3f, 1f, 0.3f),
		new Color(1f, 0.55f, 0.1f),
		new Color(1f, 0.3f, 0.9f),
		new Color(1f, 1f, 1f)
	};

	private static readonly Dictionary<Labor, BtcWaypoints.Track> tracks = new Dictionary<Labor, BtcWaypoints.Track>();

	private static readonly List<BtcWaypoints.WP> vis = new List<BtcWaypoints.WP>();

	private static readonly List<Labor> keyBuf = new List<Labor>();

	private static List<GameObject> selBuf = new List<GameObject>();

	private static readonly HashSet<GameObject> selSet = new HashSet<GameObject>();

	private class WP
	{
		public Vector3 pos;

		public int num;

		public int stamp;
	}

	private class Track
	{
		public Labor labor;

		public List<BtcWaypoints.WP> q = new List<BtcWaypoints.WP>();

		public float lastDist;

		public float lastProg;

		public float lastT;

		public float speed;

		public Vector3 lastPos;

		public float sentT;

		public float still;
	}
}
}
