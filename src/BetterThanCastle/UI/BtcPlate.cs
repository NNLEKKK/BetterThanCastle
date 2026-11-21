using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Brix.Engine;
using Brix.Network;
using Brix.UI.Builder.Panel;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace BetterThanCastle
{
public static partial class BtcPlate
{
	public static void Bind(Image bg, Image av, LobbyPlayer p, bool local)
	{
		bool flag = bg == null || p == null;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			BtcPlate.Slot slot = null;
			for (int i = 0; i < BtcPlate._binds.Count; i++)
			{
				bool flag5 = BtcPlate._binds[i].bg == bg;
				bool flag6 = flag5;
				bool flag7 = flag6;
				bool flag8 = flag7;
				if (flag8)
				{
					slot = BtcPlate._binds[i];
					break;
				}
			}
			bool flag9 = slot == null;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			if (flag12)
			{
				slot = new BtcPlate.Slot();
				slot.bg = bg;
				BtcPlate._binds.Add(slot);
				Text text = null;
				try
				{
					ThumbnailPanelComponent componentInParent = bg.GetComponentInParent<ThumbnailPanelComponent>();
					bool flag13 = componentInParent != null;
					bool flag14 = flag13;
					bool flag15 = flag14;
					bool flag16 = flag15;
					if (flag16)
					{
						text = componentInParent._playerNameText;
						slot.root = (componentInParent.transform as RectTransform);
						bool flag17 = componentInParent._avatarGo != null;
						bool flag18 = flag17;
						bool flag19 = flag18;
						bool flag20 = flag19;
						if (flag20)
						{
							slot.avGo = componentInParent._avatarGo;
							slot.defAvScale = componentInParent._avatarGo.transform.localScale;
							RectTransform rectTransform = componentInParent._avatarGo.transform as RectTransform;
							bool flag21 = rectTransform != null;
							if (flag21)
							{
								slot.defAvPos = rectTransform.anchoredPosition;
							}
						}
					}
				}
				catch (Exception)
				{
				}
				slot.nick = text;
				bool flag22 = text != null;
				bool flag23 = flag22;
				bool flag24 = flag23;
				bool flag25 = flag24;
				if (flag25)
				{
					slot.defFont = text.font;
					slot.defStyle = text.fontStyle;
					slot.defAlign = text.alignment;
					slot.defSize = text.fontSize;
					slot.defPos = text.rectTransform.anchoredPosition;
					slot.defHO = text.horizontalOverflow;
					slot.defVO = text.verticalOverflow;
					slot.defBest = text.resizeTextForBestFit;
				}
			}
			slot.av = av;
			slot.nid = p.netId.Value;
			slot.local = local;
			slot.baseCol = bg.color;
			slot.avBase = ((av != null) ? av.color : Color.white);
			BtcPlate.EnsureHook();
			BtcPlate.Apply(slot, Time.unscaledTime);
		}
	}

	public static void Tick()
	{
		BtcPlate.EnsureHook();
		bool flag = Time.frameCount == BtcPlate._lastFrame;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			BtcPlate._lastFrame = Time.frameCount;
			try
			{
				BtcPlate.Load();
				bool flag5 = BtcPlate._pend && Time.unscaledTime >= BtcPlate._pendAt;
				bool flag6 = flag5;
				bool flag7 = flag6;
				bool flag8 = flag7;
				if (flag8)
				{
					BtcPlate._pend = false;
					BtcPlate._ver++;
					BtcPlate.WriteCfg();
				}
				bool flag9 = NetworkServer.active || NetworkClient.active;
				bool flag10 = !flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				if (flag13)
				{
					bool hadNet = BtcPlate._hadNet;
					bool flag14 = hadNet;
					bool flag15 = flag14;
					bool flag16 = flag15;
					if (flag16)
					{
						BtcPlate.ResetNet();
					}
				}
				else
				{
					BtcPlate._hadNet = true;
					UNetManager instance = UNetManager.instance;
					bool flag17 = instance != null;
					bool flag18 = flag17;
					bool flag19 = flag18;
					bool flag20 = flag19;
					if (flag20)
					{
						BtcPlate.Register();
						LobbyPlayer localLobbyPlayer = instance.LocalLobbyPlayer;
						bool active = NetworkServer.active;
						bool flag21 = active;
						bool flag22 = flag21;
						bool flag23 = flag22;
						if (flag23)
						{
							BtcPlate.ServerTick(localLobbyPlayer);
						}
						else
						{
							BtcPlate.ClientTick(localLobbyPlayer);
						}
						bool flag24 = Time.unscaledTime >= BtcPlate._pruneAt;
						bool flag25 = flag24;
						bool flag26 = flag25;
						bool flag27 = flag26;
						if (flag27)
						{
							BtcPlate._pruneAt = Time.unscaledTime + 2f;
							BtcPlate.Prune(instance);
						}
					}
				}
				BtcPlateImg.Tick();
				BtcPlate.Animate();
			}
			catch (Exception)
			{
			}
		}
	}

	private static void Animate()
	{
		float unscaledTime = Time.unscaledTime;
		bool flag = BtcPlate._binds.Count == 0 || unscaledTime < BtcPlate._nextAnim;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			BtcPlate._nextAnim = unscaledTime + 0.033f;
			for (int i = BtcPlate._binds.Count - 1; i >= 0; i--)
			{
				BtcPlate.Slot slot = BtcPlate._binds[i];
				bool flag5 = slot.bg == null;
				bool flag6 = flag5;
				bool flag7 = flag6;
				bool flag8 = flag7;
				if (flag8)
				{
					BtcPlate._binds.RemoveAt(i);
				}
				else
				{
					bool activeInHierarchy = slot.bg.gameObject.activeInHierarchy;
					bool flag9 = activeInHierarchy;
					bool flag10 = flag9;
					bool flag11 = flag10;
					if (flag11)
					{
						BtcPlate.Apply(slot, unscaledTime);
					}
				}
			}
		}
	}

	private static Color ToColor(int c)
	{
		return new Color((float)(c >> 16 & 255) / 255f, (float)(c >> 8 & 255) / 255f, (float)(c & 255) / 255f, 1f);
	}

	private static Color ColorFor(BtcPlate.Data d, float t)
	{
		Color a = BtcPlate.ToColor(d.a);
		Color b = BtcPlate.ToColor(d.b);
		float num = Mathf.Max(0.1f, (float)d.speed / 10f);
		float num2 = t * num * 0.35f;
		float t2 = 0.5f - 0.5f * Mathf.Cos(num2 * 6.2831855f);
		bool flag = d.style == 1;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		Color result;
		if (flag4)
		{
			result = Color.Lerp(a, b, t2);
		}
		else
		{
			bool flag5 = d.style == 2;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				result = Color.HSVToRGB(num2 - Mathf.Floor(num2), 0.55f, 1f);
			}
			else
			{
				bool flag9 = d.style == 3;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				if (flag12)
				{
					result = Color.Lerp(Color.Lerp(a, Color.black, 0.55f), Color.Lerp(a, Color.white, 0.25f), t2);
				}
				else
				{
					float num3 = Mathf.Sin(num2 * 6.2831855f);
					result = Color.Lerp(a, b, Mathf.Clamp01(num3 * 3f + 0.5f));
				}
			}
		}
		result.a = 1f;
		return result;
	}

	internal static void Resend()
	{
		BtcPlate.Changed();
	}

	private static readonly BtcPlate.Data _ldOff = new BtcPlate.Data();

	private static BtcPlate.Data LocalData()
	{
		return BtcNick.StyleOn ? BtcPlate.FieldsData() : BtcPlate._ldOff;
	}

	private static BtcPlate.Data FieldsData()
	{
		BtcPlate._ld.style = (byte)BtcPlate._style;
		BtcPlate._ld.a = BtcPlate._a;
		BtcPlate._ld.b = BtcPlate._b;
		BtcPlate._ld.speed = (byte)Mathf.RoundToInt(BtcPlate._speed * 10f);
		BtcPlate._ld.grad = (byte)BtcPlate._grad;
		BtcPlate._ld.g = BtcPlate._g;
		BtcPlate._ld.fm = (byte)BtcPlate._fm;
		BtcPlate._ld.fc = BtcPlate._fc;
		BtcPlate._ld.fw = (byte)BtcPlate._fw;
		BtcPlate._ld.nm = (byte)BtcPlate._nm;
		BtcPlate._ld.nc = BtcPlate._nc;
		BtcPlate._ld.nw = (byte)BtcPlate._nw;
		BtcPlate._ld.fs = (byte)BtcPlate._fs;
		BtcPlate._ld.nf = BtcPlate._nf;
		BtcPlate._ld.na = (byte)BtcPlate._na;
		BtcPlate._ld.ox = (byte)((BtcPlate._ox + 200) / 2);
		BtcPlate._ld.oy = (byte)((BtcPlate._oy + 200) / 2);
		BtcPlate._ld.nsz = (byte)BtcPlate._nsz;
		BtcPlate._ld.asz = (byte)BtcPlate._asz;
		BtcPlate._ld.nx = BtcPlate._nx;
		BtcPlate._ld.nxs = (byte)BtcPlate._nxs;
		BtcPlate._ld.nxi = (byte)BtcPlate._nxi;
		BtcPlate._ld.ph = (byte)BtcPlate._ph;
		BtcPlate._ld.sc = (byte)BtcPlate._sc;
		BtcPlate._ld.ax = (byte)((BtcPlate._ax + 120) / 2);
		BtcPlate._ld.ay = (byte)((BtcPlate._ay + 120) / 2);
		BtcPlate._ld.im = (byte)BtcPlate._im;
		BtcPlate._ld.io = (byte)BtcPlate._io;
		BtcPlate._ld.idm = (byte)BtcPlate._idm;
		return BtcPlate._ld;
	}

	private static BtcPlate.PlateMsg ToMsg(uint nid, BtcPlate.Data d)
	{
		return new BtcPlate.PlateMsg
		{
			nid = nid,
			style = d.style,
			a = d.a,
			b = d.b,
			speed = d.speed,
			grad = d.grad,
			g = d.g,
			fm = d.fm,
			fc = d.fc,
			fw = d.fw,
			nm = d.nm,
			nc = d.nc,
			nw = d.nw,
			fs = d.fs,
			nf = d.nf,
			na = d.na,
			ox = d.ox,
			oy = d.oy,
			nsz = d.nsz,
			asz = d.asz,
			nx = d.nx,
			nxs = d.nxs,
			nxi = d.nxi,
			ph = d.ph,
			sc = d.sc,
			ax = d.ax,
			ay = d.ay,
			im = d.im,
			io = d.io,
			idm = d.idm
		};
	}

	private static BtcPlate.Data FromMsg(BtcPlate.PlateMsg m)
	{
		return new BtcPlate.Data
		{
			style = (byte)Mathf.Clamp((int)m.style, 0, 4),
			a = (m.a & 16777215),
			b = (m.b & 16777215),
			speed = (byte)Mathf.Clamp((int)m.speed, 1, 30),
			grad = (byte)Mathf.Clamp((int)m.grad, 0, 2),
			g = (m.g & 16777215),
			fm = (byte)Mathf.Clamp((int)m.fm, 0, 3),
			fc = (m.fc & 16777215),
			fw = (byte)Mathf.Clamp((int)m.fw, 1, 6),
			nm = (byte)Mathf.Clamp((int)m.nm, 0, 3),
			nc = (m.nc & 16777215),
			nw = (byte)Mathf.Clamp((int)m.nw, 1, 4),
			fs = (byte)Mathf.Clamp((int)m.fs, 0, 4),
			nf = BtcPlate.CleanFont(m.nf),
			na = (byte)Mathf.Clamp((int)m.na, 0, 9),
			ox = (byte)Mathf.Clamp((int)m.ox, 0, 255),
			oy = (byte)Mathf.Clamp((int)m.oy, 0, 200),
			nsz = (byte)Mathf.Clamp((int)m.nsz, 10, 60),
			asz = (byte)Mathf.Clamp((int)m.asz, 10, 60),
			nx = (m.nx & 4095),
			nxs = (byte)Mathf.Clamp((int)m.nxs, 1, 30),
			nxi = (byte)Mathf.Clamp((int)m.nxi, 1, 10),
			ph = (byte)Mathf.Clamp((int)m.ph, 10, 30),
			sc = (byte)Mathf.Clamp((int)m.sc, 0, 1),
			ax = (byte)Mathf.Clamp((int)m.ax, 0, 255),
			ay = (byte)Mathf.Clamp((int)m.ay, 0, 255),
			im = (byte)Mathf.Clamp((int)m.im, 0, 2),
			io = (byte)Mathf.Clamp((int)m.io, 1, 20),
			idm = (byte)Mathf.Clamp((int)m.idm, 0, 16)
		};
	}

	private static bool Same(BtcPlate.Data x, BtcPlate.Data y)
	{
		return x != null && y != null && x.style == y.style && x.a == y.a && x.b == y.b && x.speed == y.speed && x.grad == y.grad && x.g == y.g && x.fm == y.fm && x.fc == y.fc && x.fw == y.fw && x.nm == y.nm && x.nc == y.nc && x.nw == y.nw && x.fs == y.fs && x.nf == y.nf && x.na == y.na && x.ox == y.ox && x.oy == y.oy && x.nsz == y.nsz && x.asz == y.asz && x.nx == y.nx && x.nxs == y.nxs && x.nxi == y.nxi && x.ph == y.ph && x.sc == y.sc && x.ax == y.ax && x.ay == y.ay && x.im == y.im && x.io == y.io && x.idm == y.idm;
	}

	private static void Load()
	{
		bool cfgLoaded = BtcPlate._cfgLoaded;
		bool flag = !cfgLoaded;
		bool flag2 = flag;
		bool flag3 = flag2;
		if (flag3)
		{
			BtcPlate._cfgLoaded = true;
			try
			{
				BtcPaths.Init();
				BtcPlate._style = BtcPlate.IntKey("style", 0, 0, 4);
				BtcPlate._a = BtcPlate.HexKey("a", "FFAA00", BtcPlate._a);
				BtcPlate._b = BtcPlate.HexKey("b", "FF00CC", BtcPlate._b);
				float value;
				bool flag4 = float.TryParse(BtcPaths.ReadKey("btc_plate.txt", "speed", "1"), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
				bool flag5 = flag4;
				bool flag6 = flag5;
				bool flag7 = flag6;
				if (flag7)
				{
					BtcPlate._speed = Mathf.Clamp(value, 0.1f, 3f);
				}
				BtcPlate._grad = BtcPlate.IntKey("grad", 0, 0, 2);
				BtcPlate._g = BtcPlate.HexKey("g", "00CCFF", BtcPlate._g);
				BtcPlate._fm = BtcPlate.IntKey("fm", 0, 0, 3);
				BtcPlate._fc = BtcPlate.HexKey("fc", "FFFFFF", BtcPlate._fc);
				BtcPlate._fw = BtcPlate.IntKey("fw", 2, 1, 6);
				BtcPlate._nm = BtcPlate.IntKey("nm", 0, 0, 3);
				BtcPlate._nc = BtcPlate.HexKey("nc", "000000", BtcPlate._nc);
				BtcPlate._nw = BtcPlate.IntKey("nw", 1, 1, 4);
				BtcPlate._fs = BtcPlate.IntKey("fs", 0, 0, 4);
				BtcPlate._nf = BtcPlate.CleanFont(BtcPaths.ReadKey("btc_plate.txt", "nf", ""));
				BtcPlate._na = BtcPlate.IntKey("na", 0, 0, 9);
				BtcPlate._ox = BtcPlate.IntKey("ox", 0, -200, 310);
				BtcPlate._oy = BtcPlate.IntKey("oy", 0, -200, 200);
				BtcPlate._nsz = BtcPlate.IntKey("nsz", 20, 10, 60);
				BtcPlate._asz = BtcPlate.IntKey("asz", 20, 10, 60);
				BtcPlate._nx = BtcPlate.IntKey("nx", 0, 0, 4095);
				BtcPlate._nxs = BtcPlate.IntKey("nxs", 10, 1, 30);
				BtcPlate._nxi = BtcPlate.IntKey("nxi", 5, 1, 10);
				BtcPlate._ph = BtcPlate.IntKey("ph", 10, 10, 30);
				BtcPlate._sc = BtcPlate.IntKey("sc", 1, 0, 1);
				BtcPlate._ax = BtcPlate.IntKey("ax", 0, -120, 390);
				BtcPlate._ay = BtcPlate.IntKey("ay", 0, -120, 390);
				BtcPlate._im = BtcPlate.IntKey("im", 0, 0, 2);
				BtcPlate._io = BtcPlate.IntKey("io", 20, 1, 20);
				BtcPlate._idm = BtcPlate.IntKey("idm", 0, 0, 16);
			}
			catch (Exception)
			{
			}
			BtcPlate._hexA = BtcPlate._a.ToString("X6");
			BtcPlate._hexB = BtcPlate._b.ToString("X6");
			BtcPlate._hexG = BtcPlate._g.ToString("X6");
			BtcPlate._hexF = BtcPlate._fc.ToString("X6");
			BtcPlate._hexN = BtcPlate._nc.ToString("X6");
			BtcPlate._nfEdit = BtcPlate._nf;
		}
	}

	private static void Changed()
	{
		BtcPlate._pend = true;
		BtcPlate._pendAt = Time.unscaledTime + 0.4f;
	}

	private static string CfgFile
	{
		get
		{
			return BtcPaths.Config("btc_plate.txt");
		}
	}

	private static void Register()
	{
		bool active = NetworkServer.active;
		bool flag = active;
		bool flag2 = flag;
		bool flag3 = flag2;
		if (flag3)
		{
			bool flag4 = !BtcPlate._srvReg;
			bool flag5 = flag4;
			bool flag6 = flag5;
			bool flag7 = flag6;
			if (flag7)
			{
				BtcPlate._srvReg = true;
				NetworkServer.RegisterHandler(93, new NetworkMessageDelegate(BtcPlate.OnUp));
			}
		}
		else
		{
			BtcPlate._srvReg = false;
		}
		NetworkClient networkClient = (NetworkClient.allClients.Count > 0) ? NetworkClient.allClients[0] : null;
		bool flag8 = networkClient != BtcPlate._cl;
		bool flag9 = flag8;
		bool flag10 = flag9;
		bool flag11 = flag10;
		if (flag11)
		{
			BtcPlate._cl = networkClient;
			BtcPlate._annVer = -1;
			bool flag12 = networkClient != null;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			if (flag15)
			{
				networkClient.RegisterHandler(94, new NetworkMessageDelegate(BtcPlate.OnDown));
			}
		}
	}

	private static void ClientTick(LobbyPlayer me)
	{
		bool flag = BtcPlate._cl == null || !BtcPlate._cl.isConnected || me == null;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			bool flag5 = BtcPlate._annVer == BtcPlate._ver && Time.unscaledTime < BtcPlate._reAnn;
			bool flag6 = !flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				BtcPlate._annVer = BtcPlate._ver;
				BtcPlate._reAnn = Time.unscaledTime + 8f;
				try
				{
					BtcPlate._cl.Send(93, BtcPlate.ToMsg(0U, BtcPlate.LocalData()));
				}
				catch (Exception)
				{
				}
			}
		}
	}

	private static void ServerTick(LobbyPlayer me)
	{
		bool flag = me == null;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			uint value = me.netId.Value;
			bool flag5 = BtcPlate._hostVer != BtcPlate._ver;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				BtcPlate._hostVer = BtcPlate._ver;
				BtcPlate.Data data = BtcPlate.Clone(BtcPlate.LocalData());
				BtcPlate._data[value] = data;
				BtcPlate.PlateMsg m = BtcPlate.ToMsg(value, data);
				for (int i = 0; i < BtcPlate._mods.Count; i++)
				{
					BtcPlate.SendTo(BtcPlate._mods[i], m);
				}
			}
			else
			{
				bool flag9 = BtcPlate._mods.Count > 0 && Time.unscaledTime >= BtcPlate._resyncAt;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				if (flag12)
				{
					BtcPlate._resyncAt = Time.unscaledTime + 10f;
					foreach (KeyValuePair<uint, BtcPlate.Data> keyValuePair in BtcPlate._data)
					{
						BtcPlate.PlateMsg m2 = BtcPlate.ToMsg(keyValuePair.Key, keyValuePair.Value);
						for (int j = 0; j < BtcPlate._mods.Count; j++)
						{
							BtcPlate.SendTo(BtcPlate._mods[j], m2);
						}
					}
				}
			}
		}
	}

	private static void SendTo(int conn, BtcPlate.PlateMsg m)
	{
		try
		{
			NetworkServer.SendToClient(conn, 94, m);
		}
		catch (Exception)
		{
		}
	}

	private static uint OwnerOf(int connId)
	{
		UNetManager instance = UNetManager.instance;
		bool flag = instance == null || instance.lobbySlots == null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		uint result;
		if (flag4)
		{
			result = 0U;
		}
		else
		{
			for (int i = 0; i < instance.lobbySlots.Length; i++)
			{
				LobbyPlayer lobbyPlayer = instance.lobbySlots[i] as LobbyPlayer;
				bool flag5 = lobbyPlayer != null && lobbyPlayer.connectionToClient != null && lobbyPlayer.connectionToClient.connectionId == connId;
				bool flag6 = flag5;
				bool flag7 = flag6;
				bool flag8 = flag7;
				if (flag8)
				{
					return lobbyPlayer.netId.Value;
				}
			}
			result = 0U;
		}
		return result;
	}

	private static void OnUp(NetworkMessage netMsg)
	{
		try
		{
			BtcPlate.PlateMsg m = netMsg.ReadMessage<BtcPlate.PlateMsg>();
			int connectionId = netMsg.conn.connectionId;
			uint num = BtcPlate.OwnerOf(connectionId);
			bool flag = num > 0U;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			if (flag4)
			{
				bool flag5 = !BtcPlate._mods.Contains(connectionId);
				bool flag6 = flag5;
				bool flag7 = flag6;
				bool flag8 = flag7;
				bool flag9 = flag8;
				if (flag9)
				{
					BtcPlate._mods.Add(connectionId);
				}
				BtcPlate.Data data = BtcPlate.FromMsg(m);
				BtcPlate.Data x;
				bool flag10 = !BtcPlate._data.TryGetValue(num, out x) || !BtcPlate.Same(x, data);
				BtcPlate._data[num] = data;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				if (flag14)
				{
					BtcPlate.PlateMsg m2 = BtcPlate.ToMsg(num, data);
					for (int i = 0; i < BtcPlate._mods.Count; i++)
					{
						bool flag15 = BtcPlate._mods[i] != connectionId;
						bool flag16 = flag15;
						bool flag17 = flag16;
						bool flag18 = flag17;
						if (flag18)
						{
							BtcPlate.SendTo(BtcPlate._mods[i], m2);
						}
					}
				}
				bool flag19 = flag5;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				if (flag22)
				{
					foreach (KeyValuePair<uint, BtcPlate.Data> keyValuePair in BtcPlate._data)
					{
						bool flag23 = keyValuePair.Key != num;
						bool flag24 = flag23;
						bool flag25 = flag24;
						bool flag26 = flag25;
						if (flag26)
						{
							BtcPlate.SendTo(connectionId, BtcPlate.ToMsg(keyValuePair.Key, keyValuePair.Value));
						}
					}
				}
			}
		}
		catch (Exception)
		{
		}
	}

	private static void OnDown(NetworkMessage netMsg)
	{
		try
		{
			bool flag = !NetworkServer.active;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			if (flag4)
			{
				BtcPlate.PlateMsg plateMsg = netMsg.ReadMessage<BtcPlate.PlateMsg>();
				BtcPlate._data[plateMsg.nid] = BtcPlate.FromMsg(plateMsg);
			}
		}
		catch (Exception)
		{
		}
	}

	private static void Prune(UNetManager um)
	{
		HashSet<uint> hashSet = new HashSet<uint>();
		bool flag = um.lobbySlots != null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			for (int i = 0; i < um.lobbySlots.Length; i++)
			{
				LobbyPlayer lobbyPlayer = um.lobbySlots[i] as LobbyPlayer;
				bool flag5 = lobbyPlayer != null;
				bool flag6 = flag5;
				bool flag7 = flag6;
				bool flag8 = flag7;
				if (flag8)
				{
					hashSet.Add(lobbyPlayer.netId.Value);
				}
			}
		}
		bool flag9 = hashSet.Count == 0;
		bool flag10 = flag9;
		bool flag11 = flag10;
		bool flag12 = flag11;
		if (flag12)
		{
			BtcPlate._deadPrev.Clear();
		}
		else
		{
			HashSet<uint> hashSet2 = new HashSet<uint>();
			foreach (uint item in BtcPlate._data.Keys)
			{
				bool flag13 = !hashSet.Contains(item);
				bool flag14 = flag13;
				bool flag15 = flag14;
				bool flag16 = flag15;
				if (flag16)
				{
					hashSet2.Add(item);
				}
			}
			List<uint> list = new List<uint>();
			foreach (uint item2 in hashSet2)
			{
				bool flag17 = BtcPlate._deadPrev.Contains(item2);
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				if (flag20)
				{
					list.Add(item2);
				}
			}
			BtcPlate._deadPrev = hashSet2;
			for (int j = 0; j < list.Count; j++)
			{
				BtcPlate._data.Remove(list[j]);
			}
			bool active = NetworkServer.active;
			bool flag21 = active;
			bool flag22 = flag21;
			bool flag23 = flag22;
			if (flag23)
			{
				for (int k = BtcPlate._mods.Count - 1; k >= 0; k--)
				{
					bool flag24 = false;
					for (int l = 0; l < NetworkServer.connections.Count; l++)
					{
						NetworkConnection networkConnection = NetworkServer.connections[l];
						bool flag25 = networkConnection != null && networkConnection.connectionId == BtcPlate._mods[k];
						bool flag26 = flag25;
						bool flag27 = flag26;
						bool flag28 = flag27;
						if (flag28)
						{
							flag24 = true;
							break;
						}
					}
					bool flag29 = !flag24;
					bool flag30 = flag29;
					bool flag31 = flag30;
					bool flag32 = flag31;
					if (flag32)
					{
						BtcPlate._mods.RemoveAt(k);
					}
				}
			}
		}
	}

	private static void ResetNet()
	{
		BtcPlate._hadNet = false;
		BtcPlate._data.Clear();
		BtcPlate._mods.Clear();
		BtcPlate._deadPrev.Clear();
		BtcPlate._srvReg = false;
		BtcPlate._cl = null;
		BtcPlate._annVer = -1;
		BtcPlate._hostVer = -1;
	}

	private static void Box(Rect r, Color c, Texture2D px)
	{
		Color color = GUI.color;
		GUI.color = c;
		GUI.DrawTexture(r, px);
		GUI.color = color;
	}

	private static float ColorRow(float x, float y, float w, float row, float gap, string title, int cur, ref string hex, GUIStyle label, Texture2D px, out int result)
	{
		result = cur;
		GUI.Label(new Rect(x, y, w, row), title, label);
		y += row;
		float num = w / (float)BtcPlate.Pal.Length;
		for (int i = 0; i < BtcPlate.Pal.Length; i++)
		{
			Rect rect = new Rect(x + num * (float)i, y, num - 2f, row * 0.8f);
			bool flag = BtcPlate.Pal[i] == cur;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			if (flag4)
			{
				BtcPlate.Box(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 4f), Color.white, px);
			}
			BtcPlate.Box(rect, BtcPlate.ToColor(BtcPlate.Pal[i]), px);
			bool flag5 = GUI.Button(rect, GUIContent.none, GUIStyle.none);
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				result = BtcPlate.Pal[i];
				hex = result.ToString("X6");
			}
		}
		y += row * 0.8f + gap;
		string text = GUI.TextField(new Rect(x, y, w * 0.4f, row), hex, 6);
		bool flag9 = text != hex;
		bool flag10 = flag9;
		bool flag11 = flag10;
		bool flag12 = flag11;
		if (flag12)
		{
			hex = text;
			int num2 = 0;
			bool flag13 = text.Length == 6 && int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out num2);
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				result = (num2 & 16777215);
			}
		}
		BtcPlate.Box(new Rect(x + w * 0.4f + 6f, y + 2f, row * 1.5f, row - 4f), BtcPlate.ToColor(result), px);
		return y + row + gap;
	}

	public static float DrawUI(float x, float y, float w, float row, float gap, GUIStyle btn, GUIStyle label, GUIStyle tog, Texture2D px)
	{
		BtcPlate.Load();
		BtcPlate.EnsureHook();
		BtcPlate._tog = tog;
		bool flag = false;
		GUI.Label(new Rect(x, y, w, row * 2f), BtcLang.Tr("Player list plate (visible to players with the mod)"), label);
		y += row * 2f + gap;
		y += BtcPlate.DrawPreview(x, y, w, row, label, px) + gap;
		bool flag2 = BtcPlate._style > 0 || BtcPlate._fm >= 2 || BtcPlate._nm >= 2;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		if (flag5)
		{
			GUI.Label(new Rect(x, y, w * 0.3f, row), BtcLang.Tr("Speed"), label);
			float num = GUI.HorizontalSlider(new Rect(x + w * 0.32f, y + row * 0.3f, w * 0.5f, row), BtcPlate._speed, 0.1f, 3f);
			GUI.Label(new Rect(x + w * 0.85f, y, w * 0.15f, row), BtcPlate._speed.ToString("0.0"), label);
			bool flag6 = Mathf.Abs(num - BtcPlate._speed) > 0.01f;
			bool flag7 = flag6;
			bool flag8 = flag7;
			bool flag9 = flag8;
			if (flag9)
			{
				BtcPlate._speed = Mathf.Round(num * 10f) / 10f;
				flag = true;
			}
			y += row + gap;
		}
		string[] names = new string[]
		{
			BtcPlate.T("Background", "Фон"),
			BtcPlate.T("Frame", "Рамка"),
			BtcPlate.T("Nick", "Ник"),
			BtcPlate.T("Nick FX", "Эффекты"),
			BtcPlate.T("Size", "Размер"),
			BtcPlate.T("Image", "Картинка")
		};
		y = BtcPlate.Grid(x, y, w, row, gap, ref BtcPlate._sec, names, 3, btn, ref flag);
		bool flag10 = BtcPlate._sec == 0;
		bool flag11 = flag10;
		bool flag12 = flag11;
		bool flag13 = flag12;
		if (flag13)
		{
			y = BtcPlate.DrawBg(x, y, w, row, gap, btn, label, px, ref flag);
		}
		else
		{
			bool flag14 = BtcPlate._sec == 1;
			bool flag15 = flag14;
			bool flag16 = flag15;
			bool flag17 = flag16;
			if (flag17)
			{
				y = BtcPlate.DrawFrame(x, y, w, row, gap, btn, label, px, ref flag);
			}
			else
			{
				y = BtcPlate.DrawSec(x, y, w, row, gap, btn, label, px, ref flag);
			}
		}
		GUI.Label(new Rect(x, y, w, row * 3f), BtcLang.Tr("Players with the mod see your plate in the lobby player list. Others see the default one."), label);
		y += row * 3f + gap;
		bool flag18 = flag;
		bool flag19 = flag18;
		bool flag20 = flag19;
		bool flag21 = flag20;
		if (flag21)
		{
			BtcPlate.Changed();
		}
		return y;
	}

	static BtcPlate()
	{
		BtcPlate._binds = new List<BtcPlate.Slot>();
		BtcPlate._data = new Dictionary<uint, BtcPlate.Data>();
		BtcPlate._fonts = new Dictionary<string, Font>(StringComparer.OrdinalIgnoreCase);
		BtcPlate._mods = new List<int>();
		BtcPlate._ld = new BtcPlate.Data();
		BtcPlate._deadPrev = new HashSet<uint>();
		BtcPlate._a = 16755200;
		BtcPlate._b = 16711884;
		BtcPlate._speed = 1f;
		BtcPlate._g = 52479;
		BtcPlate._fc = 16777215;
		BtcPlate._fw = 2;
		BtcPlate._nw = 1;
		BtcPlate._nf = "";
		BtcPlate._nsz = 20;
		BtcPlate._asz = 20;
		BtcPlate._nxs = 10;
		BtcPlate._nxi = 5;
		BtcPlate._ph = 10;
		BtcPlate._sc = 1;
		BtcPlate._io = 20;
		BtcPlate._nfEdit = "";
		BtcPlate._hexA = "FFAA00";
		BtcPlate._hexB = "FF00CC";
		BtcPlate._hexG = "00CCFF";
		BtcPlate._hexF = "FFFFFF";
		BtcPlate._hexN = "000000";
		BtcPlate._annVer = -1;
		BtcPlate._hostVer = -1;
		BtcPlate._lastFrame = -1;
	}

	private static void EnsureHook()
	{
		bool hooked = BtcPlate._hooked;
		bool flag = !hooked;
		bool flag2 = flag;
		bool flag3 = flag2;
		if (flag3)
		{
			BtcPlate._hooked = true;
			Canvas.willRenderCanvases += BtcPlate.OnWillRender;
		}
	}

	private static void OnWillRender()
	{
		BtcPlate.Tick();
	}

	private static void Apply(BtcPlate.Slot s, float t)
	{
		bool flag = s.bg == null;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			BtcPlate.Data data = null;
			bool local = s.local;
			bool flag5 = local;
			bool flag6 = flag5;
			bool flag7 = flag6;
			if (flag7)
			{
				data = BtcPlate.LocalData();
			}
			else
			{
				BtcPlate._data.TryGetValue(s.nid, out data);
			}
			Color color = s.baseCol;
			bool flag8 = data != null;
			bool flag9 = flag8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			if (flag11)
			{
				bool flag12 = data.style > 0;
				bool flag13 = flag12;
				bool flag14 = flag13;
				bool flag15 = flag14;
				if (flag15)
				{
					color = BtcPlate.ColorFor(data, t);
					color.a = s.baseCol.a;
				}
				else
				{
					bool flag16 = data.grad > 0;
					bool flag17 = flag16;
					bool flag18 = flag17;
					bool flag19 = flag18;
					if (flag19)
					{
						color = BtcPlate.ToColor(data.a);
						color.a = s.baseCol.a;
					}
				}
			}
			bool flag20 = s.bg.color != color;
			bool flag21 = flag20;
			bool flag22 = flag21;
			bool flag23 = flag22;
			if (flag23)
			{
				s.bg.color = color;
			}
			Color color2 = (data != null && (data.style > 0 || data.grad > 0 || data.asz != 20 || data.ph != 10 || data.im > 0)) ? new Color(s.avBase.r, s.avBase.g, s.avBase.b, 0f) : s.avBase;
			bool flag24 = s.av != null && s.av.color != color2;
			bool flag25 = flag24;
			bool flag26 = flag25;
			bool flag27 = flag26;
			if (flag27)
			{
				s.av.color = color2;
			}
			BtcPlate.ApplyFx(s, data, t);
			BtcPlate.ApplyNick(s, data, t);
			BtcPlate.ApplyHeight(s, data);
			BtcPlate.ApplyImage(s, data);
			BtcPlate.ApplyAvatar(s, data);
			if (local)
			{
				BtcPlate.GeoCapture(s, data);
			}
		}
	}

	private static BtcPlate.Data Clone(BtcPlate.Data d)
	{
		return new BtcPlate.Data
		{
			style = d.style,
			a = d.a,
			b = d.b,
			speed = d.speed,
			grad = d.grad,
			g = d.g,
			fm = d.fm,
			fc = d.fc,
			fw = d.fw,
			nm = d.nm,
			nc = d.nc,
			nw = d.nw,
			fs = d.fs,
			nf = d.nf,
			na = d.na,
			ox = d.ox,
			oy = d.oy,
			nsz = d.nsz,
			asz = d.asz,
			nx = d.nx,
			nxs = d.nxs,
			nxi = d.nxi,
			ph = d.ph,
			sc = d.sc,
			ax = d.ax,
			ay = d.ay,
			im = d.im,
			io = d.io,
			idm = d.idm
		};
	}

	private static string T(string en, string ru)
	{
		return BtcLang.Russian ? ru : en;
	}

	private static void ApplyFx(BtcPlate.Slot s, BtcPlate.Data d, float t)
	{
		int num = (int)((d != null) ? d.grad : 0);
		float num2 = (d != null && d.fm > 0) ? ((float)d.fw) : 0f;
		bool flag = s.fx == null && num == 0 && num2 <= 0f;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			BtcPlate.Fx fx = s.fx;
			bool flag5 = fx == null;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				fx = s.bg.gameObject.GetComponent<BtcPlate.Fx>();
				bool flag9 = fx == null;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				if (flag12)
				{
					fx = s.bg.gameObject.AddComponent<BtcPlate.Fx>();
				}
				s.fx = fx;
			}
			Color color = (num > 0) ? BtcPlate.ToColor(d.g) : Color.white;
			Color color2 = (num2 > 0f) ? BtcPlate.EffCol((int)d.fm, d.fc, (int)d.speed, t) : Color.white;
			bool flag13 = fx.grad != num || fx.fw != num2 || (num > 0 && fx.g != color) || (num2 > 0f && fx.fcol != color2);
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				fx.grad = num;
				fx.g = color;
				fx.fw = num2;
				fx.fcol = color2;
				fx.Dirty();
			}
		}
	}

	private static bool NickCustom(BtcPlate.Data d)
	{
		return d != null && (d.nf.Length > 0 || d.fs > 0 || d.nm > 0 || d.na > 0 || d.ox != 100 || d.oy != 100 || d.nsz != 20 || d.nx != 0 || (d.sc != 0 && d.ph != 10));
	}

	private static void ApplyNick(BtcPlate.Slot s, BtcPlate.Data d, float t)
	{
		Text nick = s.nick;
		bool flag = nick == null;
		bool flag2 = !flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			bool flag5 = BtcPlate.NickCustom(d);
			bool flag6 = !flag5 && !s.nickMod;
			bool flag7 = !flag6;
			bool flag8 = flag7;
			bool flag9 = flag8;
			if (flag9)
			{
				s.nickMod = flag5;
				Font font = flag5 ? BtcPlate.GetFont(d.nf) : null;
				Font font2 = (font != null) ? font : s.defFont;
				bool flag10 = nick.font != font2;
				bool flag11 = flag10;
				bool flag12 = flag11;
				bool flag13 = flag12;
				if (flag13)
				{
					nick.font = font2;
				}
				FontStyle fontStyle = (flag5 && d.fs > 0) ? BtcPlate.StyleOf((int)d.fs) : s.defStyle;
				bool flag14 = nick.fontStyle != fontStyle;
				bool flag15 = flag14;
				bool flag16 = flag15;
				bool flag17 = flag16;
				if (flag17)
				{
					nick.fontStyle = fontStyle;
				}
				int num = flag5 ? Mathf.Max(1, Mathf.RoundToInt((float)s.defSize * (float)d.nsz / 20f * BtcPlate.PlateK(d))) : s.defSize;
				bool flag18 = nick.fontSize != num;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				if (flag21)
				{
					nick.fontSize = num;
				}
				TextAnchor textAnchor = (flag5 && d.na > 0) ? ((TextAnchor)(d.na - 1)) : s.defAlign;
				bool flag22 = nick.alignment != textAnchor;
				bool flag23 = flag22;
				bool flag24 = flag23;
				bool flag25 = flag24;
				if (flag25)
				{
					nick.alignment = textAnchor;
				}
				bool flag26 = flag5 && num > s.defSize;
				HorizontalWrapMode horizontalWrapMode = flag26 ? HorizontalWrapMode.Overflow : s.defHO;
				VerticalWrapMode verticalWrapMode = flag26 ? VerticalWrapMode.Overflow : s.defVO;
				bool flag27 = !flag26 && s.defBest;
				bool flag28 = nick.horizontalOverflow != horizontalWrapMode;
				if (flag28)
				{
					nick.horizontalOverflow = horizontalWrapMode;
				}
				bool flag29 = nick.verticalOverflow != verticalWrapMode;
				if (flag29)
				{
					nick.verticalOverflow = verticalWrapMode;
				}
				bool flag30 = nick.resizeTextForBestFit != flag27;
				if (flag30)
				{
					nick.resizeTextForBestFit = flag27;
				}
				Vector2 vector = flag5 ? new Vector2(s.defPos.x + (float)((d.ox - 100) * 2), s.defPos.y + (float)((d.oy - 100) * 2)) : s.defPos;
				RectTransform rectTransform = nick.rectTransform;
				bool flag31 = rectTransform.anchoredPosition != vector;
				bool flag32 = flag31;
				bool flag33 = flag32;
				bool flag34 = flag33;
				if (flag34)
				{
					rectTransform.anchoredPosition = vector;
				}
				bool flag35 = flag5 && d.nx != 0;
				bool flag36 = flag35 || s.nFx != null;
				bool flag37 = flag36;
				if (flag37)
				{
					bool flag38 = s.nFx == null;
					bool flag39 = flag38;
					if (flag39)
					{
						s.nFx = nick.gameObject.GetComponent<BtcPlate.NickFx>();
						bool flag40 = s.nFx == null;
						bool flag41 = flag40;
						if (flag41)
						{
							bool flag42 = s.nOut != null;
							bool flag43 = flag42;
							if (flag43)
							{
								s.nOut.enabled = false;
								UnityEngine.Object.Destroy(s.nOut);
								s.nOut = null;
							}
							s.nFx = nick.gameObject.AddComponent<BtcPlate.NickFx>();
						}
					}
					s.nFx.Set(flag35 ? d.nx : 0, (int)((d != null) ? d.nxs : 10), (int)((d != null) ? d.nxi : 5), t);
				}
				bool flag44 = flag5 && d.nm > 0;
				bool flag45 = flag44;
				bool flag46 = flag45;
				bool flag47 = flag46;
				if (flag47)
				{
					bool flag48 = s.nOut == null;
					bool flag49 = flag48;
					bool flag50 = flag49;
					bool flag51 = flag50;
					if (flag51)
					{
						s.nOut = nick.gameObject.AddComponent<Outline>();
					}
					bool flag52 = !s.nOut.enabled;
					bool flag53 = flag52;
					bool flag54 = flag53;
					bool flag55 = flag54;
					if (flag55)
					{
						s.nOut.enabled = true;
					}
					Color color = BtcPlate.EffCol((int)d.nm, d.nc, (int)d.speed, t);
					bool flag56 = s.nOut.effectColor != color;
					bool flag57 = flag56;
					bool flag58 = flag57;
					bool flag59 = flag58;
					if (flag59)
					{
						s.nOut.effectColor = color;
					}
					Vector2 vector2 = new Vector2((float)d.nw, -(float)d.nw);
					bool flag60 = s.nOut.effectDistance != vector2;
					bool flag61 = flag60;
					bool flag62 = flag61;
					bool flag63 = flag62;
					if (flag63)
					{
						s.nOut.effectDistance = vector2;
					}
				}
				else
				{
					bool flag64 = s.nOut != null && s.nOut.enabled;
					bool flag65 = flag64;
					bool flag66 = flag65;
					bool flag67 = flag66;
					if (flag67)
					{
						s.nOut.enabled = false;
					}
				}
			}
		}
	}

	private static FontStyle StyleOf(int fs)
	{
		bool flag = fs == 2;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		FontStyle result;
		if (flag4)
		{
			result = FontStyle.Bold;
		}
		else
		{
			bool flag5 = fs == 3;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				result = FontStyle.Italic;
			}
			else
			{
				bool flag9 = fs == 4;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				if (flag12)
				{
					result = FontStyle.BoldAndItalic;
				}
				else
				{
					result = FontStyle.Normal;
				}
			}
		}
		return result;
	}

	private static HashSet<string> Installed()
	{
		bool instTried = BtcPlate._instTried;
		bool flag = !instTried;
		bool flag2 = flag;
		bool flag3 = flag2;
		if (flag3)
		{
			BtcPlate._instTried = true;
			try
			{
				MethodInfo method = typeof(Font).GetMethod("GetOSInstalledFontNames", BindingFlags.Static | BindingFlags.Public);
				bool flag4 = method != null;
				bool flag5 = flag4;
				bool flag6 = flag5;
				bool flag7 = flag6;
				if (flag7)
				{
					string[] array = method.Invoke(null, null) as string[];
					bool flag8 = array != null && array.Length != 0;
					bool flag9 = flag8;
					bool flag10 = flag9;
					bool flag11 = flag10;
					if (flag11)
					{
						BtcPlate._inst = new HashSet<string>(array, StringComparer.OrdinalIgnoreCase);
					}
				}
			}
			catch (Exception)
			{
			}
		}
		return BtcPlate._inst;
	}

	private static Font GetFont(string name)
	{
		bool flag = string.IsNullOrEmpty(name);
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		Font result;
		if (flag4)
		{
			result = null;
		}
		else
		{
			Font font;
			bool flag5 = BtcPlate._fonts.TryGetValue(name, out font);
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				result = font;
			}
			else
			{
				font = null;
				bool flag9 = BtcPlate._fonts.Count < 24;
				bool flag10 = flag9;
				bool flag11 = flag10;
				bool flag12 = flag11;
				if (flag12)
				{
					HashSet<string> hashSet = BtcPlate.Installed();
					bool flag13 = hashSet == null || hashSet.Contains(name);
					bool flag14 = flag13;
					bool flag15 = flag14;
					bool flag16 = flag15;
					if (flag16)
					{
						try
						{
							font = Font.CreateDynamicFontFromOSFont(name, 16);
						}
						catch (Exception)
						{
							font = null;
						}
					}
					BtcPlate._fonts[name] = font;
				}
				result = font;
			}
		}
		return result;
	}

	private static string[] FontList()
	{
		bool flag = BtcPlate._fontList != null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		string[] fontList;
		if (flag4)
		{
			fontList = BtcPlate._fontList;
		}
		else
		{
			List<string> list = new List<string>();
			list.Add("");
			HashSet<string> hashSet = BtcPlate.Installed();
			for (int i = 0; i < BtcPlate.Curated.Length; i++)
			{
				bool flag5 = hashSet == null || hashSet.Contains(BtcPlate.Curated[i]);
				bool flag6 = flag5;
				bool flag7 = flag6;
				bool flag8 = flag7;
				if (flag8)
				{
					list.Add(BtcPlate.Curated[i]);
				}
			}
			BtcPlate._fontList = list.ToArray();
			BtcPlate._fontNames = new string[BtcPlate._fontList.Length];
			for (int j = 0; j < BtcPlate._fontList.Length; j++)
			{
				BtcPlate._fontNames[j] = ((j == 0) ? BtcPlate.T("Default", "Обычный") : BtcPlate._fontList[j]);
			}
			fontList = BtcPlate._fontList;
		}
		return fontList;
	}

	private static Color EffCol(int mode, int c, int speed, float t)
	{
		float num = Mathf.Max(0.1f, (float)speed / 10f);
		float num2 = t * num * 0.35f;
		bool flag = mode == 2;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		Color result;
		if (flag4)
		{
			result = Color.HSVToRGB(num2 - Mathf.Floor(num2), 0.7f, 1f);
		}
		else
		{
			Color color = BtcPlate.ToColor(c);
			bool flag5 = mode == 3;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				float t2 = 0.5f - 0.5f * Mathf.Cos(num2 * 6.2831855f);
				result = Color.Lerp(Color.Lerp(color, Color.black, 0.7f), color, t2);
			}
			else
			{
				result = color;
			}
		}
		result.a = 1f;
		return result;
	}

	private static string CleanFont(string s)
	{
		bool flag = string.IsNullOrEmpty(s);
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		string result;
		if (flag4)
		{
			result = "";
		}
		else
		{
			char[] array = new char[Math.Min(s.Length, 32)];
			int num = 0;
			int num2 = 0;
			while (num2 < s.Length && num < array.Length)
			{
				bool flag5 = !char.IsControl(s[num2]);
				bool flag6 = flag5;
				bool flag7 = flag6;
				bool flag8 = flag7;
				if (flag8)
				{
					array[num++] = s[num2];
				}
				num2++;
			}
			result = new string(array, 0, num).Trim();
		}
		return result;
	}

	private static int HexKey(string key, string def, int cur)
	{
		int num;
		bool flag = int.TryParse(BtcPaths.ReadKey("btc_plate.txt", key, def), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out num);
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		int result;
		if (flag4)
		{
			result = (num & 16777215);
		}
		else
		{
			result = cur;
		}
		return result;
	}

	private static int IntKey(string key, int def, int lo, int hi)
	{
		int value;
		bool flag = int.TryParse(BtcPaths.ReadKey("btc_plate.txt", key, def.ToString()), out value);
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		int result;
		if (flag4)
		{
			result = Mathf.Clamp(value, lo, hi);
		}
		else
		{
			result = def;
		}
		return result;
	}

	private static void WriteCfg()
	{
		try
		{
			File.WriteAllText(BtcPlate.CfgFile, string.Concat(new string[]
			{
				"style=",
				BtcPlate._style.ToString(),
				"\r\na=",
				BtcPlate._a.ToString("X6"),
				"\r\nb=",
				BtcPlate._b.ToString("X6"),
				"\r\nspeed=",
				BtcPlate._speed.ToString("0.0", CultureInfo.InvariantCulture),
				"\r\ngrad=",
				BtcPlate._grad.ToString(),
				"\r\ng=",
				BtcPlate._g.ToString("X6"),
				"\r\nfm=",
				BtcPlate._fm.ToString(),
				"\r\nfc=",
				BtcPlate._fc.ToString("X6"),
				"\r\nfw=",
				BtcPlate._fw.ToString(),
				"\r\nnm=",
				BtcPlate._nm.ToString(),
				"\r\nnc=",
				BtcPlate._nc.ToString("X6"),
				"\r\nnw=",
				BtcPlate._nw.ToString(),
				"\r\nfs=",
				BtcPlate._fs.ToString(),
				"\r\nnf=",
				BtcPlate._nf,
				"\r\nna=",
				BtcPlate._na.ToString(),
				"\r\nox=",
				BtcPlate._ox.ToString(),
				"\r\noy=",
				BtcPlate._oy.ToString(),
				"\r\nnsz=",
				BtcPlate._nsz.ToString(),
				"\r\nasz=",
				BtcPlate._asz.ToString(),
				"\r\nnx=",
				BtcPlate._nx.ToString(),
				"\r\nnxs=",
				BtcPlate._nxs.ToString(),
				"\r\nnxi=",
				BtcPlate._nxi.ToString(),
				"\r\nph=",
				BtcPlate._ph.ToString(),
				"\r\nsc=",
				BtcPlate._sc.ToString(),
				"\r\nax=",
				BtcPlate._ax.ToString(),
				"\r\nay=",
				BtcPlate._ay.ToString(),
				"\r\nim=",
				BtcPlate._im.ToString(),
				"\r\nio=",
				BtcPlate._io.ToString(),
				"\r\nidm=",
				BtcPlate._idm.ToString(),
				"\r\n"
			}));
		}
		catch (Exception)
		{
		}
	}

	private static void Fill(Rect r, Color c1, Color c2, int dir, Texture2D px)
	{
		bool flag = dir == 0;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			BtcPlate.Box(r, c1, px);
		}
		else
		{
			int num = 24;
			for (int i = 0; i < num; i++)
			{
				float num2 = (float)i / (float)num;
				float num3 = (float)(i + 1) / (float)num;
				Color c3 = Color.Lerp(c1, c2, (num2 + num3) * 0.5f);
				Rect r2 = (dir == 1) ? new Rect(r.x + r.width * num2, r.y, r.width * (num3 - num2) + 1f, r.height) : new Rect(r.x, r.y + r.height * num2, r.width, r.height * (num3 - num2) + 1f);
				BtcPlate.Box(r2, c3, px);
			}
		}
	}

	private static string StripTags(string s)
	{
		bool flag = s == BtcPlate._stripSrc;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		string stripRes;
		if (flag4)
		{
			stripRes = BtcPlate._stripRes;
		}
		else
		{
			BtcPlate._stripSrc = s;
			BtcPlate._stripRes = Regex.Replace(s, "<[^>]*>", "");
			stripRes = BtcPlate._stripRes;
		}
		return stripRes;
	}

	internal static float DrawPreview(float x, float y, float w, float row, GUIStyle label, Texture2D px)
	{
		float num = 8f;
		BtcPlate.Data data = BtcPlate.LocalData();
		float num2 = (float)data.ph / 10f;
		float num3 = row * 1.8f;
		float num4 = num3 * num2;
		float unscaledTime = Time.unscaledTime;
		Color c = new Color(1f, 0.9f, 0.2f, 1f);
		bool flag = data.style > 0;
		if (flag)
		{
			c = BtcPlate.ColorFor(data, unscaledTime);
		}
		else
		{
			bool flag2 = data.grad > 0;
			if (flag2)
			{
				c = BtcPlate.ToColor(data.a);
			}
		}
		Rect rect = new Rect(x + num, y + num, w - num * 2f, num4);
		bool flag3 = data.fm > 0;
		if (flag3)
		{
			float num5 = (float)data.fw;
			BtcPlate.Box(new Rect(rect.x - num5, rect.y - num5, rect.width + num5 * 2f, rect.height + num5 * 2f), BtcPlate.EffCol((int)data.fm, data.fc, (int)data.speed, unscaledTime), px);
		}
		BtcPlate.Fill(rect, c, BtcPlate.ToColor(data.g), (int)data.grad, px);
		BtcPlateImg.Anim local = BtcPlateImg.Local;
		bool flag4 = data.im > 0 && local != null && local.tex != null;
		if (flag4)
		{
			local.used = Time.unscaledTime;
			float num6 = 1f - (float)data.idm * 0.05f;
			Color color = GUI.color;
			GUI.color = new Color(num6, num6, num6, (float)data.io / 20f);
			GUI.DrawTextureWithTexCoords(rect, local.tex, BtcPlate.CoverUv(local.w, local.h, rect.width / Mathf.Max(1f, rect.height), data.im == 2));
			GUI.color = color;
		}
		string text = "Nick";
		try
		{
			bool flag5 = !string.IsNullOrEmpty(Neo.multiplayerName);
			if (flag5)
			{
				text = Neo.multiplayerName;
			}
		}
		catch (Exception)
		{
		}
		bool flag6 = BtcPlate._pvStyle == null;
		if (flag6)
		{
			BtcPlate._pvStyle = new GUIStyle(label);
			BtcPlate._pvStyle.richText = true;
			BtcPlate._pvStyle.alignment = TextAnchor.MiddleLeft;
			BtcPlate._pvOut = new GUIStyle(BtcPlate._pvStyle);
		}
		Font font = BtcPlate.GetFont(data.nf);
		BtcPlate._pvStyle.font = font;
		BtcPlate._pvOut.font = font;
		FontStyle fontStyle = (data.fs > 0) ? BtcPlate.StyleOf((int)data.fs) : FontStyle.Normal;
		BtcPlate._pvStyle.fontStyle = fontStyle;
		BtcPlate._pvOut.fontStyle = fontStyle;
		int num7 = (label.fontSize > 0) ? label.fontSize : 14;
		int num8 = Mathf.Max(1, Mathf.RoundToInt((float)num7 * (float)data.nsz / 20f * BtcPlate.PlateK(data)));
		num8 = Mathf.Min(num8, Mathf.Max(8, Mathf.RoundToInt(rect.height * 0.85f)));
		BtcPlate._pvStyle.fontSize = num8;
		BtcPlate._pvOut.fontSize = num8;
		float num9 = Mathf.Min(num3 * (float)data.asz / 20f * BtcPlate.PlateK(data), rect.height * 1.5f);
		BtcPlate.Box(new Rect(rect.xMax - num9 - 6f + (float)(data.ax * 2 - 120), rect.y + (rect.height - num9) * 0.5f - (float)(data.ay * 2 - 120), num9, num9), new Color(0.15f, 0.15f, 0.15f, 1f), px);
		TextAnchor alignment = (TextAnchor)((data.na > 0) ? (data.na - 1) : 3);
		BtcPlate._pvStyle.alignment = alignment;
		BtcPlate._pvOut.alignment = alignment;
		Rect rect2 = new Rect(rect.x + 8f + (float)((data.ox - 100) * 2), rect.y - (float)((data.oy - 100) * 2), rect.width - 16f, rect.height);
		bool flag7 = data.nx != 0;
		float result;
		if (flag7)
		{
			BtcPlate.PvFx(rect2, BtcPlate.StripTags(text), data, unscaledTime, num8);
			result = num4 + num * 2f;
		}
		else
		{
			bool flag8 = data.nm > 0;
			if (flag8)
			{
				Color textColor = BtcPlate.EffCol((int)data.nm, data.nc, (int)data.speed, unscaledTime);
				BtcPlate._pvOut.normal.textColor = textColor;
				BtcPlate._pvOut.richText = false;
				string text2 = BtcPlate.StripTags(text);
				float num10 = (float)data.nw;
				bool flag9 = Event.current.type == EventType.Repaint;
				int num11 = 0;
				while (num11 < 8 && flag9)
				{
					BtcPlate._pvOut.Draw(new Rect(rect2.x + BtcPlate.Dx[num11] * num10, rect2.y + BtcPlate.Dy[num11] * num10, rect2.width, rect2.height), text2, false, false, false, false);
					num11++;
				}
			}
			GUI.Label(rect2, text, BtcPlate._pvStyle);
			result = num4 + num * 2f;
		}
		return result;
	}

	private static float Grid(float x, float y, float w, float row, float gap, ref int sel, string[] names, int cols, GUIStyle btn, ref bool dirty)
	{
		int num = (names.Length + cols - 1) / cols;
		float num2 = (float)num * row + (float)(num - 1) * 2f + gap;
		int num3 = GUI.SelectionGrid(new Rect(x, y, w, num2), sel, names, cols, btn);
		bool flag = num3 != sel;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			sel = num3;
			dirty = true;
		}
		return y + num2 + gap;
	}

	private static float Slide(float x, float y, float w, float row, float gap, string title, ref int val, int lo, int hi, GUIStyle label, ref bool dirty)
	{
		GUI.Label(new Rect(x, y, w * 0.3f, row), title, label);
		float f = GUI.HorizontalSlider(new Rect(x + w * 0.32f, y + row * 0.3f, w * 0.5f, row), (float)val, (float)lo, (float)hi);
		GUI.Label(new Rect(x + w * 0.85f, y, w * 0.15f, row), val.ToString(), label);
		int num = Mathf.Clamp(Mathf.RoundToInt(f), lo, hi);
		bool flag = num != val;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			val = num;
			dirty = true;
		}
		return y + row + gap;
	}

	private static float DrawBg(float x, float y, float w, float row, float gap, GUIStyle btn, GUIStyle label, Texture2D px, ref bool dirty)
	{
		string[] names = new string[]
		{
			BtcLang.Tr("Off"),
			BtcLang.Tr("Cycle"),
			BtcLang.Tr("Rainbow"),
			BtcLang.Tr("Pulse"),
			BtcLang.Tr("Neon")
		};
		y = BtcPlate.Grid(x, y, w, row, gap, ref BtcPlate._style, names, 3, btn, ref dirty);
		bool flag = BtcPlate._style == 1 || BtcPlate._style == 3 || BtcPlate._style == 4 || BtcPlate._grad > 0;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			int num;
			y = BtcPlate.ColorRow(x, y, w, row, gap, BtcLang.Tr("Color A"), BtcPlate._a, ref BtcPlate._hexA, label, px, out num);
			bool flag5 = num != BtcPlate._a;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				BtcPlate._a = num;
				dirty = true;
			}
		}
		bool flag9 = BtcPlate._style == 1 || BtcPlate._style == 4;
		bool flag10 = flag9;
		bool flag11 = flag10;
		bool flag12 = flag11;
		if (flag12)
		{
			int num2;
			y = BtcPlate.ColorRow(x, y, w, row, gap, BtcLang.Tr("Color B"), BtcPlate._b, ref BtcPlate._hexB, label, px, out num2);
			bool flag13 = num2 != BtcPlate._b;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				BtcPlate._b = num2;
				dirty = true;
			}
		}
		GUI.Label(new Rect(x, y, w, row), BtcPlate.T("Gradient", "Градиент"), label);
		y += row;
		string[] names2 = new string[]
		{
			BtcPlate.T("None", "Нет"),
			BtcPlate.T("Horizontal", "Горизонт."),
			BtcPlate.T("Vertical", "Вертикал.")
		};
		y = BtcPlate.Grid(x, y, w, row, gap, ref BtcPlate._grad, names2, 3, btn, ref dirty);
		bool flag17 = BtcPlate._grad > 0;
		bool flag18 = flag17;
		bool flag19 = flag18;
		bool flag20 = flag19;
		if (flag20)
		{
			int num3;
			y = BtcPlate.ColorRow(x, y, w, row, gap, BtcPlate.T("Gradient end color", "Конечный цвет градиента"), BtcPlate._g, ref BtcPlate._hexG, label, px, out num3);
			bool flag21 = num3 != BtcPlate._g;
			bool flag22 = flag21;
			bool flag23 = flag22;
			bool flag24 = flag23;
			if (flag24)
			{
				BtcPlate._g = num3;
				dirty = true;
			}
		}
		return y;
	}

	private static float DrawFrame(float x, float y, float w, float row, float gap, GUIStyle btn, GUIStyle label, Texture2D px, ref bool dirty)
	{
		string[] names = new string[]
		{
			BtcLang.Tr("Off"),
			BtcPlate.T("Solid", "Сплошная"),
			BtcLang.Tr("Rainbow"),
			BtcLang.Tr("Pulse")
		};
		y = BtcPlate.Grid(x, y, w, row, gap, ref BtcPlate._fm, names, 2, btn, ref dirty);
		bool flag = BtcPlate._fm == 1 || BtcPlate._fm == 3;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			int num;
			y = BtcPlate.ColorRow(x, y, w, row, gap, BtcPlate.T("Frame color", "Цвет рамки"), BtcPlate._fc, ref BtcPlate._hexF, label, px, out num);
			bool flag5 = num != BtcPlate._fc;
			bool flag6 = flag5;
			bool flag7 = flag6;
			bool flag8 = flag7;
			if (flag8)
			{
				BtcPlate._fc = num;
				dirty = true;
			}
		}
		bool flag9 = BtcPlate._fm > 0;
		bool flag10 = flag9;
		bool flag11 = flag10;
		bool flag12 = flag11;
		if (flag12)
		{
			y = BtcPlate.Slide(x, y, w, row, gap, BtcPlate.T("Width", "Толщина"), ref BtcPlate._fw, 1, 6, label, ref dirty);
		}
		return y;
	}

	private static float DrawNick(float x, float y, float w, float row, float gap, GUIStyle btn, GUIStyle label, Texture2D px, ref bool dirty)
	{
		string[] array = BtcPlate.FontList();
		GUI.Label(new Rect(x, y, w, row), BtcPlate.T("Font", "Шрифт"), label);
		y += row;
		int num = Array.IndexOf<string>(array, BtcPlate._nf);
		int num2 = num;
		bool flag = false;
		y = BtcPlate.Grid(x, y, w, row, gap, ref num, BtcPlate._fontNames, 3, btn, ref flag);
		bool flag2 = num != num2 && num >= 0;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		if (flag5)
		{
			BtcPlate._nf = array[num];
			BtcPlate._nfEdit = BtcPlate._nf;
			dirty = true;
		}
		GUI.Label(new Rect(x, y, w, row), BtcPlate.T("Any installed font (exact name)", "Любой установленный шрифт (точное имя)"), label);
		y += row;
		BtcPlate._nfEdit = GUI.TextField(new Rect(x, y, w * 0.6f, row), BtcPlate._nfEdit, 32);
		bool flag6 = GUI.Button(new Rect(x + w * 0.62f, y, w * 0.38f, row), BtcLang.Tr("Apply"), btn);
		bool flag7 = flag6;
		bool flag8 = flag7;
		bool flag9 = flag8;
		if (flag9)
		{
			string text = BtcPlate.CleanFont(BtcPlate._nfEdit);
			bool flag10 = text != BtcPlate._nf;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			if (flag13)
			{
				BtcPlate._nf = text;
				dirty = true;
			}
		}
		y += row + gap;
		GUI.Label(new Rect(x, y, w, row), BtcPlate.T("Font style", "Начертание"), label);
		y += row;
		string[] names = new string[]
		{
			BtcPlate.T("Default", "Обычный"),
			BtcPlate.T("Normal", "Тонкий"),
			BtcPlate.T("Bold", "Жирный"),
			BtcPlate.T("Italic", "Курсив"),
			BtcPlate.T("Bold italic", "Жирный курсив")
		};
		y = BtcPlate.Grid(x, y, w, row, gap, ref BtcPlate._fs, names, 3, btn, ref dirty);
		GUI.Label(new Rect(x, y, w, row), BtcPlate.T("Nick position in the plate", "Положение ника в плашке"), label);
		y += row;
		string[] names2 = new string[]
		{
			"TL",
			"T",
			"TR",
			"L",
			"C",
			"R",
			"BL",
			"B",
			"BR"
		};
		int num3 = BtcPlate._na - 1;
		int num4 = num3;
		bool flag14 = false;
		y = BtcPlate.Grid(x, y, w, row, gap, ref num3, names2, 3, btn, ref flag14);
		bool flag15 = num3 != num4 && num3 >= 0;
		bool flag16 = flag15;
		bool flag17 = flag16;
		bool flag18 = flag17;
		if (flag18)
		{
			BtcPlate._na = num3 + 1;
			dirty = true;
		}
		y = BtcPlate.Slide(x, y, w, row, gap, BtcPlate.T("Shift X", "Сдвиг X"), ref BtcPlate._ox, -200, 200, label, ref dirty);
		y = BtcPlate.Slide(x, y, w, row, gap, BtcPlate.T("Shift Y", "Сдвиг Y"), ref BtcPlate._oy, -200, 200, label, ref dirty);
		bool flag19 = GUI.Button(new Rect(x, y, w * 0.5f, row), BtcPlate.T("Reset position", "Сбросить положение"), btn);
		bool flag20 = flag19;
		bool flag21 = flag20;
		bool flag22 = flag21;
		if (flag22)
		{
			BtcPlate._na = 0;
			BtcPlate._ox = 0;
			BtcPlate._oy = 0;
			dirty = true;
		}
		y += row + gap;
		y = BtcPlate.SlideP(x, y, w, row, gap, BtcPlate.T("Nick size", "Размер ника"), ref BtcPlate._nsz, 10, 60, label, ref dirty);
		GUI.Label(new Rect(x, y, w, row), BtcPlate.T("Nick outline", "Обводка ника"), label);
		y += row;
		string[] names3 = new string[]
		{
			BtcLang.Tr("Off"),
			BtcPlate.T("Solid", "Сплошная"),
			BtcLang.Tr("Rainbow"),
			BtcLang.Tr("Pulse")
		};
		y = BtcPlate.Grid(x, y, w, row, gap, ref BtcPlate._nm, names3, 2, btn, ref dirty);
		bool flag23 = BtcPlate._nm == 1 || BtcPlate._nm == 3;
		bool flag24 = flag23;
		bool flag25 = flag24;
		bool flag26 = flag25;
		if (flag26)
		{
			int num5;
			y = BtcPlate.ColorRow(x, y, w, row, gap, BtcPlate.T("Outline color", "Цвет обводки"), BtcPlate._nc, ref BtcPlate._hexN, label, px, out num5);
			bool flag27 = num5 != BtcPlate._nc;
			bool flag28 = flag27;
			bool flag29 = flag28;
			bool flag30 = flag29;
			if (flag30)
			{
				BtcPlate._nc = num5;
				dirty = true;
			}
		}
		bool flag31 = BtcPlate._nm > 0;
		bool flag32 = flag31;
		bool flag33 = flag32;
		bool flag34 = flag33;
		if (flag34)
		{
			y = BtcPlate.Slide(x, y, w, row, gap, BtcPlate.T("Width", "Толщина"), ref BtcPlate._nw, 1, 4, label, ref dirty);
		}
		return y;
	}

	private static void ApplyAvatar(BtcPlate.Slot s, BtcPlate.Data d)
	{
		bool flag = s.avGo == null;
		if (!flag)
		{
			bool flag2 = d != null && (d.asz != 20 || (d.sc != 0 && d.ph != 10) || d.ax != 60 || d.ay != 60);
			bool flag3 = !flag2 && !s.avMod;
			if (!flag3)
			{
				s.avMod = flag2;
				Vector3 vector = flag2 ? (s.defAvScale * ((float)d.asz / 20f * BtcPlate.PlateK(d))) : s.defAvScale;
				Transform transform = s.avGo.transform;
				bool flag4 = transform.localScale != vector;
				if (flag4)
				{
					transform.localScale = vector;
				}
				RectTransform x = transform as RectTransform;
				bool flag5 = x == null;
				if (!flag5)
				{
					bool flag6 = s.avOff == null;
					if (flag6)
					{
						s.avOff = s.avGo.GetComponent<BtcPlate.AvOff>();
						bool flag7 = s.avOff == null;
						if (flag7)
						{
							bool flag8 = !flag2;
							if (flag8)
							{
								return;
							}
							s.avOff = s.avGo.AddComponent<BtcPlate.AvOff>();
						}
					}
					Vector2 offset = flag2 ? new Vector2((float)(d.ax * 2 - 120), (float)(d.ay * 2 - 120)) : Vector2.zero;
					s.avOff.SetOffset(offset);
				}
			}
		}
	}

	private static float SlideP(float x, float y, float w, float row, float gap, string title, ref int val, int lo, int hi, GUIStyle label, ref bool dirty)
	{
		GUI.Label(new Rect(x, y, w * 0.3f, row), title, label);
		float f = GUI.HorizontalSlider(new Rect(x + w * 0.32f, y + row * 0.3f, w * 0.5f, row), (float)val, (float)lo, (float)hi);
		GUI.Label(new Rect(x + w * 0.85f, y, w * 0.15f, row), (val * 5).ToString() + "%", label);
		int num = Mathf.Clamp(Mathf.RoundToInt(f), lo, hi);
		bool flag = num != val;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			val = num;
			dirty = true;
		}
		return y + row + gap;
	}

	private static float H(int a, int b)
	{
		uint num = (uint)(a * 73856093 ^ b * 19349663);
		num ^= num >> 13;
		num *= 1274126177U;
		num ^= num >> 16;
		return (num & 16777215U) / 16777216f;
	}

	private static BtcPlate.LS LetterState(int m, int i, int n, float tt, float k, float us)
	{
		BtcPlate.LS ls = default(BtcPlate.LS);
		ls.sx = 1f;
		ls.sy = 1f;
		ls.alpha = 1f;
		float num = 0f;
		float num2 = 0f;
		bool flag = (m & 1) != 0;
		bool flag2 = flag;
		if (flag2)
		{
			float num3 = 1.1f + BtcPlate.H(i, 11) * 1.5f;
			float num4 = (tt + BtcPlate.H(i, 12) * 10f) / num3;
			int num5 = Mathf.FloorToInt(num4);
			float num6 = num4 - (float)num5;
			bool flag3 = num6 < 0.3f;
			bool flag4 = flag3;
			bool flag5 = flag4;
			if (flag5)
			{
				float num7 = num6 / 0.3f;
				float num8 = 0.6f + 0.8f * BtcPlate.H(i, num5 + 13);
				num2 += 4f * num7 * (1f - num7) * 9f * k * num8;
			}
		}
		bool flag6 = (m & 2) != 0;
		bool flag7 = flag6;
		if (flag7)
		{
			float num9 = tt * 1.6f - (float)i * 0.4f;
			float num10 = num9 - Mathf.Floor(num9);
			bool flag8 = num10 < 0.5f;
			bool flag9 = flag8;
			bool flag10 = flag9;
			if (flag10)
			{
				num2 += Mathf.Sin(num10 / 0.5f * 3.1415927f) * 10f * k;
			}
		}
		bool flag11 = (m & 4) != 0;
		bool flag12 = flag11;
		if (flag12)
		{
			num2 += Mathf.Sin(tt * 3f - (float)i * 0.65f) * 4f * k;
		}
		bool flag13 = (m & 8) != 0;
		bool flag14 = flag13;
		if (flag14)
		{
			int num11 = Mathf.FloorToInt(tt * 22f);
			num += (BtcPlate.H(i, num11) - 0.5f) * 3f * k;
			num2 += (BtcPlate.H(i, num11 + 4001) - 0.5f) * 3f * k;
		}
		bool flag15 = (m & 16) != 0;
		bool flag16 = flag15;
		if (flag16)
		{
			int num12 = Mathf.FloorToInt(tt * 9f);
			bool flag17 = BtcPlate.H(num12, 777) < 0.38f;
			bool flag18 = flag17;
			bool flag19 = flag18;
			if (flag19)
			{
				bool flag20 = BtcPlate.H(i, num12) < 0.55f;
				bool flag21 = flag20;
				if (flag21)
				{
					num += (BtcPlate.H(i, num12 + 31) - 0.5f) * 2f * 7f * k;
					ls.shear = (BtcPlate.H(i, num12 + 57) - 0.5f) * 2f * 5f * k;
					ls.ghost = Mathf.Max(ls.ghost, 2.5f * k);
				}
				bool flag22 = BtcPlate.H(i, num12 + 93) < 0.18f;
				bool flag23 = flag22;
				if (flag23)
				{
					ls.alpha *= 0.25f;
				}
				bool flag24 = BtcPlate.H(i, num12 + 17) < 0.12f;
				bool flag25 = flag24;
				if (flag25)
				{
					num2 += (BtcPlate.H(i, num12 + 5) - 0.5f) * 8f * k;
				}
			}
		}
		bool flag26 = (m & 32) != 0;
		bool flag27 = flag26;
		if (flag27)
		{
			ls.ghost = Mathf.Max(ls.ghost, (1.6f + Mathf.Sin(tt * 2.2f) * 0.9f) * k);
		}
		bool flag28 = (m & 64) != 0;
		bool flag29 = flag28;
		if (flag29)
		{
			float num13 = tt * 0.25f - (float)i * 0.09f;
			num13 -= Mathf.Floor(num13);
			ls.hue = true;
			ls.hueCol = Color.HSVToRGB(num13, 0.75f, 1f);
		}
		bool flag30 = (m & 128) != 0;
		bool flag31 = flag30;
		if (flag31)
		{
			ls.rot = Mathf.Sin(tt * 3f + (float)i * 0.8f) * 14f * k;
		}
		bool flag32 = (m & 256) != 0;
		bool flag33 = flag32;
		if (flag33)
		{
			float num14 = Mathf.Max(0.2f, 1f + Mathf.Sin(tt * 3.2f + (float)i * 0.55f) * 0.2f * k);
			ls.sx = num14;
			ls.sy = num14;
		}
		bool flag34 = (m & 512) != 0;
		bool flag35 = flag34;
		if (flag35)
		{
			int num15 = Mathf.FloorToInt(tt * 14f);
			bool flag36 = BtcPlate.H(i, num15 + 211) < 0.1f * k;
			bool flag37 = flag36;
			if (flag37)
			{
				ls.alpha *= 0.12f;
			}
			else
			{
				bool flag38 = BtcPlate.H(num15, 5) < 0.05f * k;
				bool flag39 = flag38;
				if (flag39)
				{
					ls.alpha *= 0.5f;
				}
			}
		}
		bool flag40 = (m & 1024) != 0;
		bool flag41 = flag40;
		if (flag41)
		{
			float num16 = ((float)i + 0.5f) / (float)Mathf.Max(1, n);
			float num17 = tt * 0.45f % 1.6f - 0.3f;
			float num18 = num16 - num17;
			ls.white = Mathf.Clamp01(Mathf.Exp(-num18 * num18 / 0.012f)) * 0.95f;
		}
		bool flag42 = (m & 2048) != 0;
		bool flag43 = flag42;
		if (flag43)
		{
			float num19 = 2.2f + BtcPlate.H(i, 41) * 2.5f;
			float num20 = (tt + BtcPlate.H(i, 42) * 9f) / num19;
			float num21 = num20 - Mathf.Floor(num20);
			bool flag44 = num21 < 0.22f;
			bool flag45 = flag44;
			if (flag45)
			{
				ls.sx = Mathf.Cos(num21 / 0.22f * 6.2831855f);
			}
		}
		ls.dx = num * us;
		ls.dy = num2 * us;
		ls.shear *= us;
		ls.ghost *= us;
		return ls;
	}

	private static string[] FxNames()
	{
		return new string[]
		{
			BtcPlate.T("Hop (random)", "Скачки (случайные)"),
			BtcPlate.T("Jump wave", "Волна прыжков"),
			BtcPlate.T("Wave", "Волна"),
			BtcPlate.T("Shake", "Дрожь"),
			BtcPlate.T("Glitch", "Глитч"),
			BtcPlate.T("RGB split", "RGB-сдвиг"),
			BtcPlate.T("Rainbow letters", "Радужные буквы"),
			BtcPlate.T("Rock", "Качание"),
			BtcPlate.T("Pulse", "Пульс букв"),
			BtcPlate.T("Neon flicker", "Мерцание"),
			BtcPlate.T("Shine", "Блик"),
			BtcPlate.T("Coin flip", "Монетка")
		};
	}

	private static float DrawNickFx(float x, float y, float w, float row, float gap, GUIStyle btn, GUIStyle label, Texture2D px, ref bool dirty)
	{
		bool flag = BtcPlate._fxLbl == null;
		bool flag2 = flag;
		bool flag3 = flag2;
		if (flag3)
		{
			BtcPlate._fxLbl = new GUIStyle(label);
			BtcPlate._fxLbl.alignment = TextAnchor.MiddleCenter;
		}
		GUI.Label(new Rect(x, y, w, row), BtcPlate.T("Letter effects (combine any)", "Эффекты букв (можно сочетать)"), label);
		y += row;
		float num = w / 4f;
		string[] array = new string[]
		{
			BtcPlate.T("Off", "Выкл"),
			BtcPlate.T("Glitch pack", "Глитч-пак"),
			BtcPlate.T("Playful", "Весёлый"),
			BtcPlate.T("Random", "Случайно")
		};
		for (int i = 0; i < 4; i++)
		{
			bool flag4 = GUI.Button(new Rect(x + num * (float)i, y, num - 3f, row), array[i], btn);
			bool flag5 = flag4;
			bool flag6 = flag5;
			if (flag6)
			{
				int num2 = BtcPlate._nx;
				bool flag7 = i == 0;
				bool flag8 = flag7;
				if (flag8)
				{
					num2 = 0;
				}
				else
				{
					bool flag9 = i == 1;
					bool flag10 = flag9;
					if (flag10)
					{
						num2 = 560;
					}
					else
					{
						bool flag11 = i == 2;
						bool flag12 = flag11;
						if (flag12)
						{
							num2 = 321;
						}
						else
						{
							num2 = 0;
							int num3 = 2 + UnityEngine.Random.Range(0, 3);
							for (int j = 0; j < num3; j++)
							{
								num2 |= 1 << UnityEngine.Random.Range(0, 12);
							}
						}
					}
				}
				BtcPlate._nx = num2;
				dirty = true;
			}
		}
		y += row + gap;
		string[] array2 = BtcPlate.FxNames();
		float num4 = w / 2f;
		for (int k = 0; k < array2.Length; k++)
		{
			Rect rect = new Rect(x + (float)(k % 2) * num4, y + (float)(k / 2) * (row + 2f), num4 - 3f, row);
			bool flag13 = (BtcPlate._nx & 1 << k) != 0;
			BtcPlate.Box(rect, flag13 ? new Color(0.2f, 0.55f, 0.25f, 1f) : new Color(0.18f, 0.18f, 0.18f, 1f), px);
			GUI.Label(rect, array2[k], BtcPlate._fxLbl);
			bool flag14 = GUI.Button(rect, GUIContent.none, GUIStyle.none);
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				BtcPlate._nx ^= 1 << k;
				dirty = true;
			}
		}
		y += (float)((array2.Length + 1) / 2) * (row + 2f) + gap;
		y = BtcPlate.Slide(x, y, w, row, gap, BtcPlate.T("Strength", "Сила"), ref BtcPlate._nxi, 1, 10, label, ref dirty);
		y = BtcPlate.Slide(x, y, w, row, gap, BtcPlate.T("Speed", "Скорость"), ref BtcPlate._nxs, 1, 30, label, ref dirty);
		GUI.Label(new Rect(x, y, w, row * 2f), BtcPlate.T("Effects move each letter separately. Players with the mod see them in the lobby.", "Эффекты двигают каждую букву отдельно. Игроки с модом видят их в лобби."), label);
		return y + row * 2f + gap;
	}

	private static void PvFx(Rect tr, string plain, BtcPlate.Data ld, float t, int psz)
	{
		bool flag = Event.current.type == EventType.Repaint;
		bool flag2 = !flag || string.IsNullOrEmpty(plain);
		bool flag3 = !flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			GUIStyle pvOut = BtcPlate._pvOut;
			pvOut.richText = false;
			TextAnchor alignment = BtcPlate._pvStyle.alignment;
			int num = (int)alignment % (int)TextAnchor.MiddleLeft;
			int num2 = (int)alignment / (int)TextAnchor.MiddleLeft;
			pvOut.alignment = (TextAnchor)(num2 * 3);
			int length = plain.Length;
			float x = pvOut.CalcSize(new GUIContent(plain)).x;
			float num3 = (num == 0) ? tr.x : ((num == 1) ? (tr.x + (tr.width - x) * 0.5f) : (tr.xMax - x));
			float tt = t * (float)ld.nxs / 10f;
			float k = (float)ld.nxi / 5f;
			float us = Mathf.Clamp((float)psz / 18f, 0.5f, 3f);
			Color color = GUI.color;
			Color textColor = BtcPlate._pvStyle.normal.textColor;
			for (int i = 0; i < length; i++)
			{
				char c = plain[i];
				bool flag5 = c != ' ';
				bool flag6 = flag5;
				if (flag6)
				{
					BtcPlate.LS ls = BtcPlate.LetterState(ld.nx, i, length, tt, k, us);
					float num4 = (i == 0) ? 0f : pvOut.CalcSize(new GUIContent(plain.Substring(0, i))).x;
					float num5 = num3 + num4 + ls.dx;
					float num6 = tr.y - ls.dy;
					string text = c.ToString();
					GUI.color = new Color(1f, 1f, 1f, ls.alpha);
					bool flag7 = ls.ghost > 0.01f;
					bool flag8 = flag7;
					if (flag8)
					{
						pvOut.normal.textColor = new Color(1f, 0.2f, 0.3f, 0.8f);
						GUI.Label(new Rect(num5 - ls.ghost, num6, tr.width, tr.height), text, pvOut);
						pvOut.normal.textColor = new Color(0.2f, 0.85f, 1f, 0.8f);
						GUI.Label(new Rect(num5 + ls.ghost, num6, tr.width, tr.height), text, pvOut);
					}
					bool flag9 = ld.nm > 0;
					bool flag10 = flag9;
					if (flag10)
					{
						pvOut.normal.textColor = BtcPlate.EffCol((int)ld.nm, ld.nc, (int)ld.speed, t);
						float num7 = (float)ld.nw;
						for (int j = 0; j < 8; j++)
						{
							GUI.Label(new Rect(num5 + BtcPlate.Dx[j] * num7, num6 + BtcPlate.Dy[j] * num7, tr.width, tr.height), text, pvOut);
						}
					}
					Color color2 = ls.hue ? ls.hueCol : textColor;
					color2.a = 1f;
					bool flag11 = ls.white > 0f;
					bool flag12 = flag11;
					if (flag12)
					{
						color2 = Color.Lerp(color2, Color.white, ls.white);
					}
					pvOut.normal.textColor = color2;
					GUI.Label(new Rect(num5, num6, tr.width, tr.height), text, pvOut);
				}
			}
			GUI.color = color;
		}
	}

	private static float PlateK(BtcPlate.Data d)
	{
		return (d != null && d.sc != 0) ? ((float)d.ph / 10f) : 1f;
	}

	private static Rect CoverUv(int iw, int ih, float plateAspect, bool stretch)
	{
		bool flag = stretch || iw < 1 || ih < 1 || plateAspect <= 0.01f;
		Rect result;
		if (flag)
		{
			result = new Rect(0f, 0f, 1f, 1f);
		}
		else
		{
			float num = (float)iw / (float)ih;
			bool flag2 = plateAspect > num;
			if (flag2)
			{
				float num2 = num / plateAspect;
				result = new Rect(0f, (1f - num2) * 0.5f, 1f, num2);
			}
			else
			{
				float num3 = plateAspect / num;
				result = new Rect((1f - num3) * 0.5f, 0f, num3, 1f);
			}
		}
		return result;
	}

	private static void ApplyHeight(BtcPlate.Slot s, BtcPlate.Data d)
	{
		RectTransform rectTransform = BtcPlate.LayoutChild(s);
		bool flag = rectTransform == null;
		if (!flag)
		{
			float num = (d != null) ? ((float)d.ph / 10f) : 1f;
			bool flag2 = num > 1.001f || s.hMod;
			if (flag2)
			{
				BtcPlate.DumpTree(s);
			}
			BtcPlate.ApplyChain(s, num);
			bool flag3 = num > 1.001f;
			bool flag4 = !flag3 && !s.hMod;
			if (!flag4)
			{
				bool flag5 = s.defH <= 0f;
				if (flag5)
				{
					s.defH = Mathf.Max(rectTransform.rect.height, LayoutUtility.GetPreferredHeight(rectTransform));
					bool flag6 = s.defH <= 0f;
					if (flag6)
					{
						return;
					}
				}
				bool flag7 = rectTransform.anchorMin.y == rectTransform.anchorMax.y;
				bool flag8 = flag3;
				if (flag8)
				{
					float num2 = Mathf.Round(s.defH * num);
					bool flag9 = s.hMod && Mathf.Abs(s.curH - num2) < 0.5f;
					if (flag9)
					{
						return;
					}
					bool flag10 = s.le == null;
					if (flag10)
					{
						s.le = rectTransform.GetComponent<LayoutElement>();
						s.leAdded = false;
						bool flag11 = s.le == null;
						if (flag11)
						{
							s.le = rectTransform.gameObject.AddComponent<LayoutElement>();
							s.leAdded = true;
						}
						s.leMin = s.le.minHeight;
						s.lePref = s.le.preferredHeight;
					}
					s.le.minHeight = num2;
					s.le.preferredHeight = num2;
					bool flag12 = flag7;
					if (flag12)
					{
						rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, num2);
					}
					s.hMod = true;
					s.curH = num2;
				}
				else
				{
					bool flag13 = s.le != null;
					if (flag13)
					{
						bool leAdded = s.leAdded;
						if (leAdded)
						{
							UnityEngine.Object.Destroy(s.le);
						}
						else
						{
							s.le.minHeight = s.leMin;
							s.le.preferredHeight = s.lePref;
						}
						s.le = null;
						s.leAdded = false;
					}
					bool flag14 = flag7;
					if (flag14)
					{
						rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, s.defH);
					}
					s.hMod = false;
					s.curH = 0f;
				}
				RectTransform rectTransform2 = rectTransform.parent as RectTransform;
				LayoutRebuilder.MarkLayoutForRebuild((rectTransform2 != null) ? rectTransform2 : rectTransform);
			}
		}
	}

	private static void ApplyImage(BtcPlate.Slot s, BtcPlate.Data d)
	{
		bool local = s.local;
		if (local)
		{
			Rect rect = s.bg.rectTransform.rect;
			bool flag = rect.width > 1f && rect.height > 1f;
			if (flag)
			{
				BtcPlate._plateW = rect.width;
				BtcPlate._plateH = rect.height;
				BtcPlate._plateAsp = rect.width / rect.height;
			}
		}
		BtcPlateImg.Anim anim = (d != null && d.im > 0) ? BtcPlateImg.For(s.nid, s.local) : null;
		bool flag2 = anim == null || anim.tex == null;
		if (flag2)
		{
			bool flag3 = s.img != null && s.img.enabled;
			if (flag3)
			{
				s.img.enabled = false;
			}
		}
		else
		{
			bool flag4 = s.img == null;
			if (flag4)
			{
				GameObject gameObject = new GameObject("BtcPlateImg", new Type[]
				{
					typeof(RectTransform)
				});
				RectTransform component = gameObject.GetComponent<RectTransform>();
				component.SetParent(s.bg.transform, false);
				component.anchorMin = Vector2.zero;
				component.anchorMax = Vector2.one;
				component.offsetMin = Vector2.zero;
				component.offsetMax = Vector2.zero;
				component.SetAsFirstSibling();
				LayoutElement layoutElement = gameObject.AddComponent<LayoutElement>();
				layoutElement.ignoreLayout = true;
				RawImage rawImage = gameObject.AddComponent<RawImage>();
				rawImage.raycastTarget = false;
				s.img = rawImage;
			}
			RawImage img = s.img;
			bool flag5 = !img.enabled;
			if (flag5)
			{
				img.enabled = true;
			}
			bool flag6 = img.texture != anim.tex;
			if (flag6)
			{
				img.texture = anim.tex;
			}
			Rect rect2 = img.rectTransform.rect;
			bool flag7 = rect2.width > 1f && rect2.height > 1f;
			if (flag7)
			{
				Rect rect3 = BtcPlate.CoverUv(anim.w, anim.h, rect2.width / rect2.height, d.im == 2);
				bool flag8 = Mathf.Abs(rect3.x - s.uvLast.x) > 0.0005f || Mathf.Abs(rect3.y - s.uvLast.y) > 0.0005f || Mathf.Abs(rect3.width - s.uvLast.width) > 0.0005f || Mathf.Abs(rect3.height - s.uvLast.height) > 0.0005f;
				if (flag8)
				{
					s.uvLast = rect3;
					img.uvRect = rect3;
				}
			}
			float num = 1f - (float)d.idm * 0.05f;
			Color color = new Color(num, num, num, (float)d.io / 20f);
			bool flag9 = s.imgCol != color;
			if (flag9)
			{
				s.imgCol = color;
				img.color = color;
			}
		}
	}

	private static float SlideM(float x, float y, float w, float row, float gap, string title, ref int val, int lo, int hi, int mul, GUIStyle label, ref bool dirty)
	{
		GUI.Label(new Rect(x, y, w * 0.3f, row), title, label);
		float f = GUI.HorizontalSlider(new Rect(x + w * 0.32f, y + row * 0.3f, w * 0.5f, row), (float)val, (float)lo, (float)hi);
		GUI.Label(new Rect(x + w * 0.85f, y, w * 0.15f, row), (val * mul).ToString() + "%", label);
		int num = Mathf.Clamp(Mathf.RoundToInt(f), lo, hi);
		bool flag = num != val;
		if (flag)
		{
			val = num;
			dirty = true;
		}
		return y + row + gap;
	}

	private static float DrawSec(float x, float y, float w, float row, float gap, GUIStyle btn, GUIStyle label, Texture2D px, ref bool dirty)
	{
		bool flag = BtcPlate._sec == 2;
		float result;
		if (flag)
		{
			result = BtcPlate.DrawNick(x, y, w, row, gap, btn, label, px, ref dirty);
		}
		else
		{
			bool flag2 = BtcPlate._sec == 4;
			if (flag2)
			{
				result = BtcPlate.DrawSize(x, y, w, row, gap, btn, label, px, ref dirty);
			}
			else
			{
				bool flag3 = BtcPlate._sec == 5;
				if (flag3)
				{
					result = BtcPlate.DrawImg(x, y, w, row, gap, btn, label, px, ref dirty);
				}
				else
				{
					result = BtcPlate.DrawNickFx(x, y, w, row, gap, btn, label, px, ref dirty);
				}
			}
		}
		return result;
	}

	private static float DrawSize(float x, float y, float w, float row, float gap, GUIStyle btn, GUIStyle label, Texture2D px, ref bool dirty)
	{
		y = BtcPlate.SlideM(x, y, w, row, gap, BtcPlate.T("Plate height", "Высота плашки"), ref BtcPlate._ph, 10, 30, 10, label, ref dirty);
		GUIStyle style = (BtcPlate._tog != null) ? BtcPlate._tog : GUI.skin.toggle;
		bool flag = GUI.Toggle(new Rect(x, y, w, row), BtcPlate._sc != 0, BtcPlate.T("Scale avatar and nick with the height", "Увеличивать аватарку и ник вместе с высотой"), style);
		bool flag2 = flag != (BtcPlate._sc != 0);
		if (flag2)
		{
			BtcPlate._sc = (flag ? 1 : 0);
			dirty = true;
		}
		y += row + gap;
		y = BtcPlate.SlideP(x, y, w, row, gap, BtcPlate.T("Avatar size", "Размер аватарки"), ref BtcPlate._asz, 10, 60, label, ref dirty);
		y = BtcPlate.Slide(x, y, w, row, gap, BtcPlate.T("Avatar shift X", "Сдвиг аватарки X"), ref BtcPlate._ax, -60, 60, label, ref dirty);
		y = BtcPlate.Slide(x, y, w, row, gap, BtcPlate.T("Avatar shift Y", "Сдвиг аватарки Y"), ref BtcPlate._ay, -60, 60, label, ref dirty);
		bool flag3 = GUI.Button(new Rect(x, y, w * 0.5f, row), BtcPlate.T("Reset size", "Сбросить размер"), btn);
		if (flag3)
		{
			BtcPlate._ph = 10;
			BtcPlate._sc = 1;
			BtcPlate._asz = 20;
			BtcPlate._ax = 0;
			BtcPlate._ay = 0;
			dirty = true;
		}
		y += row + gap;
		GUI.Label(new Rect(x, y, w, row * 3f), BtcPlate.T("Height is up to 300%. Nick size and position are in the Nick tab (up to 300%, shift up to 200 px).", "Высота до 300%. Размер и положение ника - во вкладке Ник (размер до 300%, сдвиг до 200 px)."), label);
		y += row * 3f + gap;
		return y;
	}

	private static float DrawImg(float x, float y, float w, float row, float gap, GUIStyle btn, GUIStyle label, Texture2D px, ref bool dirty)
	{
		y = BtcPlateImg.DrawUI(x, y, w, row, gap, btn, label, px);
		bool flag = BtcPlateImg.TakeSelected() && BtcPlate._im == 0;
		if (flag)
		{
			BtcPlate._im = 1;
			dirty = true;
		}
		GUI.Label(new Rect(x, y, w, row), BtcPlate.T("Show image", "Показ картинки"), label);
		y += row;
		string[] names = new string[]
		{
			BtcLang.Tr("Off"),
			BtcPlate.T("Fill (crop)", "Заполнить (обрезка)"),
			BtcPlate.T("Stretch", "Растянуть")
		};
		y = BtcPlate.Grid(x, y, w, row, gap, ref BtcPlate._im, names, 3, btn, ref dirty);
		y = BtcPlate.SlideM(x, y, w, row, gap, BtcPlate.T("Opacity", "Непрозрачность"), ref BtcPlate._io, 1, 20, 5, label, ref dirty);
		y = BtcPlate.SlideM(x, y, w, row, gap, BtcPlate.T("Darken", "Затемнение"), ref BtcPlate._idm, 0, 16, 5, label, ref dirty);
		bool flag2 = BtcPlate._plateAsp > 0.5f;
		string text;
		if (flag2)
		{
			int num = Mathf.Clamp(Mathf.RoundToInt(256f / BtcPlate._plateAsp), 16, 128);
			text = string.Concat(new object[]
			{
				BtcPlate.T("Your plate right now is about ", "Ваша плашка сейчас примерно "),
				Mathf.RoundToInt(BtcPlate._plateW),
				"x",
				Mathf.RoundToInt(BtcPlate._plateH),
				BtcPlate.T(". Best picture size for it: 256x", ". Лучший размер картинки для неё: 256x"),
				num,
				". "
			});
		}
		else
		{
			text = BtcPlate.T("Open the lobby once to measure the plate. ", "Откройте лобби, чтобы измерить плашку. ");
		}
		text += BtcPlate.T("Rule of thumb: 256x64 for 100%, 256x96 for 200%, 256x128 for 300% height. Bigger files are shrunk to fit 256x128. Up to 32 frames, 255 colors, about 400 KB packed.", "Ориентир: 256x64 для высоты 100%, 256x96 для 200%, 256x128 для 300%. Больше - уменьшится до 256x128. До 32 кадров, 255 цветов, около 400 КБ в сжатом виде.");
		GUI.Label(new Rect(x, y, w, row * 6f), text, label);
		y += row * 6f + gap;
		return y;
	}

	private static RectTransform LayoutChild(BtcPlate.Slot s)
	{
		bool flag = s.lay != null;
		RectTransform result;
		if (flag)
		{
			result = s.lay;
		}
		else
		{
			RectTransform root = s.root;
			bool flag2 = root == null;
			if (flag2)
			{
				result = null;
			}
			else
			{
				RectTransform rectTransform = null;
				RectTransform rectTransform2 = root;
				int num = 0;
				while (num < 6 && rectTransform2 != null)
				{
					RectTransform rectTransform3 = rectTransform2.parent as RectTransform;
					bool flag3 = rectTransform3 == null;
					if (flag3)
					{
						break;
					}
					bool flag4 = rectTransform3.GetComponent<LayoutGroup>() != null;
					if (flag4)
					{
						rectTransform = rectTransform2;
						break;
					}
					rectTransform2 = rectTransform3;
					num++;
				}
				s.lay = ((rectTransform != null) ? rectTransform : root);
				List<RectTransform> list = new List<RectTransform>();
				Transform transform = s.bg.transform;
				while (transform != null && transform != s.lay)
				{
					RectTransform rectTransform4 = transform as RectTransform;
					bool flag5 = rectTransform4 != null;
					if (flag5)
					{
						list.Add(rectTransform4);
					}
					transform = transform.parent;
				}
				s.chain = list.ToArray();
				result = s.lay;
			}
		}
		return result;
	}

	private static void DumpTree(BtcPlate.Slot s)
	{
		bool dumped = s.dumped;
		if (!dumped)
		{
			s.dumped = true;
			try
			{
				StringBuilder stringBuilder = new StringBuilder();
				stringBuilder.AppendLine("BtcPlate hierarchy dump (bg -> up)");
				Transform transform = s.bg.transform;
				int num = 0;
				while (num < 9 && transform != null)
				{
					RectTransform rectTransform = transform as RectTransform;
					stringBuilder.Append(new string(' ', num * 2)).Append(transform.name);
					bool flag = rectTransform != null;
					if (flag)
					{
						stringBuilder.Append("  rect=").Append(rectTransform.rect.width.ToString("0.#")).Append("x").Append(rectTransform.rect.height.ToString("0.#"));
						StringBuilder stringBuilder2 = stringBuilder.Append(" anchorMin=");
						Vector2 vector = rectTransform.anchorMin;
						StringBuilder stringBuilder3 = stringBuilder2.Append(vector.x.ToString("0.##")).Append(",");
						vector = rectTransform.anchorMin;
						stringBuilder3.Append(vector.y.ToString("0.##"));
						StringBuilder stringBuilder4 = stringBuilder.Append(" anchorMax=");
						vector = rectTransform.anchorMax;
						StringBuilder stringBuilder5 = stringBuilder4.Append(vector.x.ToString("0.##")).Append(",");
						vector = rectTransform.anchorMax;
						stringBuilder5.Append(vector.y.ToString("0.##"));
					}
					Component[] components = transform.GetComponents<Component>();
					stringBuilder.Append("  [");
					for (int i = 0; i < components.Length; i++)
					{
						bool flag2 = components[i] == null;
						if (!flag2)
						{
							stringBuilder.Append(components[i].GetType().Name);
							LayoutElement layoutElement = components[i] as LayoutElement;
							bool flag3 = layoutElement != null;
							if (flag3)
							{
								stringBuilder.Append("(min=").Append(layoutElement.minHeight).Append(",pref=").Append(layoutElement.preferredHeight).Append(",ignore=").Append(layoutElement.ignoreLayout).Append(")");
							}
							HorizontalOrVerticalLayoutGroup horizontalOrVerticalLayoutGroup = components[i] as HorizontalOrVerticalLayoutGroup;
							bool flag4 = horizontalOrVerticalLayoutGroup != null;
							if (flag4)
							{
								stringBuilder.Append("(ctrlH=").Append(horizontalOrVerticalLayoutGroup.childControlHeight).Append(",expandH=").Append(horizontalOrVerticalLayoutGroup.childForceExpandHeight).Append(")");
							}
							bool flag5 = i + 1 < components.Length;
							if (flag5)
							{
								stringBuilder.Append(", ");
							}
						}
					}
					stringBuilder.AppendLine("]");
					transform = transform.parent;
					num++;
				}
				stringBuilder.AppendLine(string.Concat(new object[]
				{
					"root=",
					(s.root != null) ? s.root.name : "null",
					" layoutChild=",
					(s.lay != null) ? s.lay.name : "null",
					" defH=",
					s.defH
				}));
				bool flag6 = s.avGo != null;
				if (flag6)
				{
					RectTransform rectTransform2 = s.avGo.transform as RectTransform;
					StringBuilder stringBuilder6 = stringBuilder;
					string[] array = new string[6];
					array[0] = "avatar ";
					array[1] = s.avGo.name;
					array[2] = " parent=";
					array[3] = ((s.avGo.transform.parent != null) ? s.avGo.transform.parent.name : "null");
					array[4] = " pos=";
					int num2 = 5;
					string text;
					if (!(rectTransform2 != null))
					{
						text = "-";
					}
					else
					{
						Vector2 vector = rectTransform2.anchoredPosition;
						text = vector.ToString();
					}
					array[num2] = text;
					stringBuilder6.AppendLine(string.Concat(array));
				}
				Directory.CreateDirectory(BtcPaths.Debug);
				File.WriteAllText(BtcPaths.DebugFile("btc_plate_dump.txt"), stringBuilder.ToString());
			}
			catch (Exception)
			{
			}
		}
	}

	private static void ApplyChain(BtcPlate.Slot s, float k)
	{
		bool flag = s.chain == null || s.chain.Length == 0;
		if (!flag)
		{
			bool flag2 = k > 1.001f;
			bool flag3 = s.chainH == null;
			if (flag3)
			{
				bool flag4 = !flag2;
				if (flag4)
				{
					return;
				}
				float[] array = new float[s.chain.Length];
				bool flag5 = false;
				for (int i = 0; i < array.Length; i++)
				{
					array[i] = s.chain[i].rect.height;
					bool flag6 = array[i] > 1f;
					if (flag6)
					{
						flag5 = true;
					}
				}
				bool flag7 = !flag5;
				if (flag7)
				{
					return;
				}
				s.chainH = array;
				s.cLe = new LayoutElement[array.Length];
				s.cAdd = new bool[array.Length];
				s.cMin = new float[array.Length];
				s.cPref = new float[array.Length];
			}
			bool flag8 = false;
			for (int j = 0; j < s.chain.Length; j++)
			{
				RectTransform rectTransform = s.chain[j];
				bool flag9 = rectTransform == null || s.chainH[j] <= 1f;
				if (!flag9)
				{
					float num = Mathf.Round(s.chainH[j] * (flag2 ? k : 1f));
					RectTransform rectTransform2 = rectTransform.parent as RectTransform;
					bool flag10 = rectTransform2 != null && rectTransform2.GetComponent<LayoutGroup>() != null;
					bool flag11 = flag10;
					if (flag11)
					{
						bool flag12 = flag2;
						if (flag12)
						{
							bool flag13 = s.cLe[j] == null;
							if (flag13)
							{
								LayoutElement layoutElement = rectTransform.GetComponent<LayoutElement>();
								s.cAdd[j] = false;
								bool flag14 = layoutElement == null;
								if (flag14)
								{
									layoutElement = rectTransform.gameObject.AddComponent<LayoutElement>();
									s.cAdd[j] = true;
								}
								s.cMin[j] = layoutElement.minHeight;
								s.cPref[j] = layoutElement.preferredHeight;
								s.cLe[j] = layoutElement;
							}
							LayoutElement layoutElement2 = s.cLe[j];
							bool flag15 = Mathf.Abs(layoutElement2.minHeight - num) > 0.5f || Mathf.Abs(layoutElement2.preferredHeight - num) > 0.5f;
							if (flag15)
							{
								layoutElement2.minHeight = num;
								layoutElement2.preferredHeight = num;
								flag8 = true;
							}
						}
						else
						{
							bool flag16 = s.cLe[j] != null;
							if (flag16)
							{
								bool flag17 = s.cAdd[j];
								if (flag17)
								{
									UnityEngine.Object.Destroy(s.cLe[j]);
								}
								else
								{
									s.cLe[j].minHeight = s.cMin[j];
									s.cLe[j].preferredHeight = s.cPref[j];
								}
								s.cLe[j] = null;
								flag8 = true;
							}
						}
					}
					else
					{
						bool flag18 = rectTransform.anchorMin.y == rectTransform.anchorMax.y && Mathf.Abs(rectTransform.rect.height - num) > 0.5f;
						if (flag18)
						{
							rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, num);
						}
					}
				}
			}
			bool flag19 = flag8 && s.lay != null;
			if (flag19)
			{
				LayoutRebuilder.MarkLayoutForRebuild(s.lay);
			}
		}
	}

	private static readonly int[] Pal = new int[]
	{
		16711680,
		16737792,
		16763904,
		3407667,
		65484,
		43775,
		3368703,
		11154431,
		16724940,
		16777215,
		8947848,
		0
	};

	private static readonly List<BtcPlate.Slot> _binds;

	private static readonly Dictionary<uint, BtcPlate.Data> _data;

	private static readonly List<int> _mods;

	private static HashSet<uint> _deadPrev;

	private static NetworkClient _cl;

	private static bool _cfgLoaded;

	private static bool _srvReg;

	private static bool _hadNet;

	private static int _style;

	private static int _a;

	private static int _b;

	private static float _speed;

	private static string _hexA;

	private static string _hexB;

	private static int _ver;

	private static int _annVer;

	private static int _hostVer;

	private static int _lastFrame;

	private static float _pruneAt;

	private static float _reAnn;

	private static float _nextAnim;

	private static float _resyncAt;

	private static bool _hooked;

	private static readonly BtcPlate.Data _ld;

	private static readonly string[] Curated = new string[]
	{
		"Arial",
		"Verdana",
		"Tahoma",
		"Trebuchet MS",
		"Georgia",
		"Times New Roman",
		"Courier New",
		"Consolas",
		"Comic Sans MS",
		"Impact",
		"Segoe UI",
		"Segoe Script",
		"Lucida Console",
		"Palatino Linotype"
	};

	private static readonly float[] Dx = new float[]
	{
		1f,
		-1f,
		0f,
		0f,
		0.7f,
		0.7f,
		-0.7f,
		-0.7f
	};

	private static readonly float[] Dy = new float[]
	{
		0f,
		0f,
		1f,
		-1f,
		0.7f,
		-0.7f,
		0.7f,
		-0.7f
	};

	private static readonly Dictionary<string, Font> _fonts;

	private static HashSet<string> _inst;

	private static string[] _fontList;

	private static string[] _fontNames;

	private static GUIStyle _pvStyle;

	private static GUIStyle _pvOut;

	private static string _stripSrc;

	private static string _stripRes;

	private static bool _instTried;

	private static bool _pend;

	private static int _sec;

	private static int _grad;

	private static int _g;

	private static int _fm;

	private static int _fc;

	private static int _fw;

	private static int _nm;

	private static int _nc;

	private static int _nw;

	private static int _fs;

	private static string _nf;

	private static string _nfEdit;

	private static string _hexG;

	private static string _hexF;

	private static string _hexN;

	private static float _pendAt;

	private static int _na;

	private static int _ox;

	private static int _oy;

	private static int _nsz;

	private static int _asz;

	private static GUIStyle _fxLbl;

	private static int _nx;

	private static int _nxs;

	private static int _nxi;

	private static int _ph;

	private static int _sc;

	private static int _ax;

	private static int _ay;

	private static int _im;

	private static int _io;

	private static int _idm;

	private static GUIStyle _tog;

	private static float _plateAsp;

	private static float _plateW;

	private static float _plateH;

	private sealed class Slot
	{
		public Image bg;

		public Image av;

		public uint nid;

		public bool local;

		public Color baseCol;

		public BtcPlate.Fx fx;

		public Text nick;

		public Font defFont;

		public FontStyle defStyle;

		public Outline nOut;

		public bool nickMod;

		public TextAnchor defAlign;

		public Vector2 defPos;

		public int defSize;

		public GameObject avGo;

		public Vector3 defAvScale;

		public bool avMod;

		public Color avBase;

		public BtcPlate.NickFx nFx;

		public RectTransform root;

		public LayoutElement le;

		public bool leAdded;

		public float leMin;

		public float lePref;

		public float defH;

		public float curH;

		public bool hMod;

		public RawImage img;

		public Rect uvLast;

		public Color imgCol;

		public Vector2 defAvPos;

		public HorizontalWrapMode defHO;

		public VerticalWrapMode defVO;

		public bool defBest;

		public RectTransform lay;

		public BtcPlate.AvOff avOff;

		public bool dumped;

		public RectTransform[] chain;

		public float[] chainH;

		public LayoutElement[] cLe;

		public bool[] cAdd;

		public float[] cMin;

		public float[] cPref;
	}

	private sealed class Data
	{
		public byte style;

		public int a;

		public int b;

		public byte speed;

		public byte grad;

		public int g;

		public byte fm;

		public int fc;

		public byte fw;

		public byte nm;

		public int nc;

		public byte nw;

		public byte fs;

		public string nf = "";

		public byte na;

		public byte ox = 100;

		public byte oy = 100;

		public byte nsz = 20;

		public byte asz = 20;

		public int nx;

		public byte nxs = 10;

		public byte nxi = 5;

		public byte ph = 10;

		public byte sc = 1;

		public byte ax = 60;

		public byte ay = 60;

		public byte im;

		public byte io = 20;

		public byte idm;
	}

	public class PlateMsg : MessageBase
	{
		public override void Serialize(NetworkWriter w)
		{
			w.WritePackedUInt32(this.nid);
			w.Write(this.style);
			w.Write(this.a);
			w.Write(this.b);
			w.Write(this.speed);
			w.Write(this.grad);
			w.Write(this.g);
			w.Write(this.fm);
			w.Write(this.fc);
			w.Write(this.fw);
			w.Write(this.nm);
			w.Write(this.nc);
			w.Write(this.nw);
			w.Write(this.fs);
			w.Write(this.nf ?? "");
			w.Write(this.na);
			w.Write(this.ox);
			w.Write(this.oy);
			w.Write(this.nsz);
			w.Write(this.asz);
			w.Write(this.nx);
			w.Write(this.nxs);
			w.Write(this.nxi);
			w.Write(this.ph);
			w.Write(this.sc);
			w.Write(this.ax);
			w.Write(this.ay);
			w.Write(this.im);
			w.Write(this.io);
			w.Write(this.idm);
		}

		public override void Deserialize(NetworkReader r)
		{
			this.nid = r.ReadPackedUInt32();
			this.style = r.ReadByte();
			this.a = r.ReadInt32();
			this.b = r.ReadInt32();
			this.speed = r.ReadByte();
			this.grad = r.ReadByte();
			this.g = r.ReadInt32();
			this.fm = r.ReadByte();
			this.fc = r.ReadInt32();
			this.fw = r.ReadByte();
			this.nm = r.ReadByte();
			this.nc = r.ReadInt32();
			this.nw = r.ReadByte();
			this.fs = r.ReadByte();
			this.nf = r.ReadString();
			this.na = r.ReadByte();
			this.ox = r.ReadByte();
			this.oy = r.ReadByte();
			this.nsz = r.ReadByte();
			this.asz = r.ReadByte();
			this.nx = r.ReadInt32();
			this.nxs = r.ReadByte();
			this.nxi = r.ReadByte();
			this.ph = r.ReadByte();
			this.sc = r.ReadByte();
			this.ax = r.ReadByte();
			this.ay = r.ReadByte();
			this.im = r.ReadByte();
			this.io = r.ReadByte();
			this.idm = r.ReadByte();
		}

		public uint nid;

		public byte style;

		public int a;

		public int b;

		public byte speed;

		public byte grad;

		public int g;

		public byte fm;

		public int fc;

		public byte fw;

		public byte nm;

		public int nc;

		public byte nw;

		public byte fs;

		public string nf;

		public byte na;

		public byte ox;

		public byte oy;

		public byte nsz;

		public byte asz;

		public int nx;

		public byte nxs;

		public byte nxi;

		public byte ph;

		public byte sc;

		public byte ax;

		public byte ay;

		public byte im;

		public byte io;

		public byte idm;
	}

	public sealed class Fx : BaseMeshEffect
	{
		public void Dirty()
		{
			bool flag = base.graphic != null;
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			if (flag4)
			{
				base.graphic.SetVerticesDirty();
			}
		}

		public override void ModifyMesh(VertexHelper vh)
		{
			bool flag = !this.IsActive() || (this.grad == 0 && this.fw <= 0f);
			bool flag2 = !flag;
			bool flag3 = flag2;
			bool flag4 = flag3;
			if (flag4)
			{
				vh.GetUIVertexStream(BtcPlate.Fx._src);
				int count = BtcPlate.Fx._src.Count;
				bool flag5 = count != 0;
				bool flag6 = flag5;
				bool flag7 = flag6;
				bool flag8 = flag7;
				if (flag8)
				{
					BtcPlate.Fx._buf.Clear();
					bool flag9 = this.fw > 0f;
					bool flag10 = flag9;
					bool flag11 = flag10;
					bool flag12 = flag11;
					if (flag12)
					{
						Color32 color = this.fcol;
						for (int i = 0; i < 8; i++)
						{
							float num = BtcPlate.Dx[i] * this.fw;
							float num2 = BtcPlate.Dy[i] * this.fw;
							for (int j = 0; j < count; j++)
							{
								UIVertex uivertex = BtcPlate.Fx._src[j];
								Vector3 position = uivertex.position;
								position.x += num;
								position.y += num2;
								uivertex.position = position;
								uivertex.color = new Color32(color.r, color.g, color.b, (byte)(color.a * uivertex.color.a / byte.MaxValue));
								BtcPlate.Fx._buf.Add(uivertex);
							}
						}
					}
					bool flag13 = this.grad > 0;
					Rect rect = base.graphic.rectTransform.rect;
					Color32 b = this.g;
					for (int k = 0; k < count; k++)
					{
						UIVertex uivertex2 = BtcPlate.Fx._src[k];
						bool flag14 = flag13;
						bool flag15 = flag14;
						bool flag16 = flag15;
						bool flag17 = flag16;
						if (flag17)
						{
							float value = (this.grad == 1) ? ((uivertex2.position.x - rect.xMin) / Mathf.Max(1f, rect.width)) : ((uivertex2.position.y - rect.yMin) / Mathf.Max(1f, rect.height));
							Color32 color2 = Color32.Lerp(uivertex2.color, b, Mathf.Clamp01(value));
							color2.a = uivertex2.color.a;
							uivertex2.color = color2;
						}
						BtcPlate.Fx._buf.Add(uivertex2);
					}
					vh.Clear();
					vh.AddUIVertexTriangleStream(BtcPlate.Fx._buf);
				}
			}
		}

		public int grad;

		public Color g;

		public float fw;

		public Color fcol;

		private static readonly List<UIVertex> _src = new List<UIVertex>();

		private static readonly List<UIVertex> _buf = new List<UIVertex>();
	}

	private struct LS
	{
		public float dx;

		public float dy;

		public float rot;

		public float sx;

		public float sy;

		public float alpha;

		public float shear;

		public float ghost;

		public float white;

		public bool hue;

		public Color32 hueCol;
	}

	public sealed class NickFx : BaseMeshEffect
	{
		public void Set(int mask, int speed, int inten, float t)
		{
			bool flag = this._mask != 0;
			this._mask = mask;
			this._sp = (float)Mathf.Clamp(speed, 1, 30) / 10f;
			this._k = (float)Mathf.Clamp(inten, 1, 10) / 5f;
			this._t = t;
			bool flag2 = (mask != 0 || flag) && base.graphic != null;
			bool flag3 = flag2;
			bool flag4 = flag3;
			if (flag4)
			{
				base.graphic.SetVerticesDirty();
			}
		}

		private int CountCopies()
		{
			int num = 1;
			try
			{
				foreach (Component component in base.GetComponents<Component>())
				{
					bool flag = component == this;
					bool flag2 = flag;
					if (flag2)
					{
						break;
					}
					Shadow shadow = component as Shadow;
					bool flag3 = shadow != null && shadow.enabled;
					bool flag4 = flag3;
					if (flag4)
					{
						num *= ((shadow is Outline) ? 5 : 2);
					}
				}
			}
			catch (Exception)
			{
			}
			return num;
		}

		private void AddQuad(int q, ref BtcPlate.LS st, float offX, bool ghost, Color32 gcol, bool main)
		{
			int num = q * 6;
			float num2 = float.MaxValue;
			float num3 = float.MaxValue;
			float num4 = float.MinValue;
			float num5 = float.MinValue;
			for (int i = 0; i < 6; i++)
			{
				Vector3 position = BtcPlate.NickFx._src[num + i].position;
				bool flag = position.x < num2;
				bool flag2 = flag;
				if (flag2)
				{
					num2 = position.x;
				}
				bool flag3 = position.x > num4;
				bool flag4 = flag3;
				if (flag4)
				{
					num4 = position.x;
				}
				bool flag5 = position.y < num3;
				bool flag6 = flag5;
				if (flag6)
				{
					num3 = position.y;
				}
				bool flag7 = position.y > num5;
				bool flag8 = flag7;
				if (flag8)
				{
					num5 = position.y;
				}
			}
			float num6 = (num2 + num4) * 0.5f;
			float num7 = (num3 + num5) * 0.5f;
			float num8 = num5 - num3;
			bool flag9 = num4 - num2 < 0.01f || num8 < 0.01f;
			float num9 = 1f;
			float num10 = 0f;
			bool flag10 = st.rot != 0f;
			bool flag11 = flag10;
			if (flag11)
			{
				float f = st.rot * 0.017453292f;
				num9 = Mathf.Cos(f);
				num10 = Mathf.Sin(f);
			}
			for (int j = 0; j < 6; j++)
			{
				UIVertex uivertex = BtcPlate.NickFx._src[num + j];
				bool flag12 = !flag9;
				bool flag13 = flag12;
				if (flag13)
				{
					Vector3 position2 = uivertex.position;
					float num11 = (position2.x - num6) * st.sx;
					float num12 = (position2.y - num7) * st.sy;
					float num13 = st.shear * (position2.y - num7) / (num8 * 0.5f);
					position2.x = num6 + (num11 * num9 - num12 * num10) + st.dx + num13 + offX;
					position2.y = num7 + (num11 * num10 + num12 * num9) + st.dy;
					uivertex.position = position2;
				}
				Color32 color = uivertex.color;
				if (ghost)
				{
					color = new Color32(gcol.r, gcol.g, gcol.b, (byte)Mathf.Clamp((float)color.a * st.alpha * (float)gcol.a / 255f, 0f, 255f));
				}
				else
				{
					if (main)
					{
						bool hue = st.hue;
						bool flag14 = hue;
						if (flag14)
						{
							color = new Color32(st.hueCol.r, st.hueCol.g, st.hueCol.b, color.a);
						}
						bool flag15 = st.white > 0f;
						bool flag16 = flag15;
						if (flag16)
						{
							byte a = color.a;
							color = Color32.Lerp(color, new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, a), st.white);
							color.a = a;
						}
					}
					color.a = (byte)Mathf.Clamp((float)color.a * st.alpha, 0f, 255f);
				}
				uivertex.color = color;
				BtcPlate.NickFx._buf.Add(uivertex);
			}
		}

		public override void ModifyMesh(VertexHelper vh)
		{
			bool flag = !this.IsActive() || this._mask == 0;
			bool flag2 = !flag;
			bool flag3 = flag2;
			if (flag3)
			{
				vh.GetUIVertexStream(BtcPlate.NickFx._src);
				int count = BtcPlate.NickFx._src.Count;
				int num = count / 6;
				bool flag4 = num != 0;
				bool flag5 = flag4;
				if (flag5)
				{
					bool flag6 = this._copies < 1;
					bool flag7 = flag6;
					if (flag7)
					{
						this._copies = this.CountCopies();
					}
					int num2 = Mathf.Max(1, num / this._copies);
					float us = 1f;
					Text text = base.graphic as Text;
					bool flag8 = text != null;
					bool flag9 = flag8;
					if (flag9)
					{
						us = Mathf.Clamp((float)text.fontSize / 18f, 0.5f, 3f);
					}
					float tt = this._t * this._sp;
					bool flag10 = BtcPlate.NickFx._st.Length < num2;
					bool flag11 = flag10;
					if (flag11)
					{
						BtcPlate.NickFx._st = new BtcPlate.LS[num2 + 16];
					}
					bool flag12 = false;
					for (int i = 0; i < num2; i++)
					{
						BtcPlate.NickFx._st[i] = BtcPlate.LetterState(this._mask, i, num2, tt, this._k, us);
						bool flag13 = BtcPlate.NickFx._st[i].ghost > 0.01f;
						bool flag14 = flag13;
						if (flag14)
						{
							flag12 = true;
						}
					}
					BtcPlate.NickFx._buf.Clear();
					int num3 = (this._copies - 1) * num2;
					bool flag15 = flag12;
					bool flag16 = flag15;
					if (flag16)
					{
						Color32 gcol = new Color32(byte.MaxValue, 50, 70, 200);
						Color32 gcol2 = new Color32(50, 220, byte.MaxValue, 200);
						for (int j = num3; j < num; j++)
						{
							int num4 = j % num2;
							float ghost = BtcPlate.NickFx._st[num4].ghost;
							bool flag17 = ghost > 0.01f;
							bool flag18 = flag17;
							if (flag18)
							{
								this.AddQuad(j, ref BtcPlate.NickFx._st[num4], -ghost, true, gcol, false);
								this.AddQuad(j, ref BtcPlate.NickFx._st[num4], ghost, true, gcol2, false);
							}
						}
					}
					for (int k = 0; k < num; k++)
					{
						int num5 = k % num2;
						bool main = k >= num3;
						this.AddQuad(k, ref BtcPlate.NickFx._st[num5], 0f, false, default(Color32), main);
					}
					for (int l = num * 6; l < count; l++)
					{
						BtcPlate.NickFx._buf.Add(BtcPlate.NickFx._src[l]);
					}
					vh.Clear();
					vh.AddUIVertexTriangleStream(BtcPlate.NickFx._buf);
				}
			}
		}

		private int _mask;

		private float _sp = 1f;

		private float _k = 1f;

		private float _t;

		private int _copies = -1;

		private static readonly List<UIVertex> _src = new List<UIVertex>();

		private static readonly List<UIVertex> _buf = new List<UIVertex>();

		private static BtcPlate.LS[] _st = new BtcPlate.LS[64];
	}

	public sealed class AvOff : MonoBehaviour, ILayoutSelfController, ILayoutController
	{
		public void SetOffset(Vector2 off)
		{
			bool flag = off != this._off;
			this._off = off;
			this.Fix();
			bool flag2 = flag;
			if (flag2)
			{
				RectTransform rectTransform = base.transform as RectTransform;
				RectTransform rectTransform2 = (rectTransform != null) ? (rectTransform.parent as RectTransform) : null;
				bool flag3 = rectTransform2 != null;
				if (flag3)
				{
					LayoutRebuilder.MarkLayoutForRebuild(rectTransform2);
				}
			}
		}

		public void SetLayoutHorizontal()
		{
			this.Fix();
		}

		public void SetLayoutVertical()
		{
			this.Fix();
		}

		private void Fix()
		{
			RectTransform rectTransform = base.transform as RectTransform;
			bool flag = rectTransform == null;
			if (!flag)
			{
				Vector2 anchoredPosition = rectTransform.anchoredPosition;
				Vector2 a = (this._has && anchoredPosition == this._out) ? (anchoredPosition - this._lastOff) : anchoredPosition;
				Vector2 vector = a + this._off;
				bool flag2 = anchoredPosition != vector;
				if (flag2)
				{
					rectTransform.anchoredPosition = vector;
				}
				this._out = vector;
				this._lastOff = this._off;
				this._has = true;
			}
		}

		private Vector2 _off;

		private Vector2 _out;

		private Vector2 _lastOff;

		private bool _has;
	}
}
}
