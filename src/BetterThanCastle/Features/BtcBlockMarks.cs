using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Brix.Engine;
using Brix.Engine.Abstract;
using Brix.Util;
using UnityEngine;

namespace BetterThanCastle
{
public static class BtcBlockMarks
{
	public static string Title
	{
		get
		{
			return BtcLang.Russian ? "Подсветка блоков" : "Block Highlight";
		}
	}

	public static int DelayMs
	{
		get
		{
			BtcBlockMarks.Load();
			return BtcBlockMarks.delayMs;
		}
	}

	private static bool HoverCell(Camera cam, Vector3 screen, out Vector3 pos)
	{
		pos = Vector3.zero;
		RaycastHit raycastHit;
		bool flag = !Physics.Raycast(cam.ScreenPointToRay(screen), out raycastHit, 500f, 132096, QueryTriggerInteraction.Collide);
		bool result;
		if (flag)
		{
			result = false;
		}
		else
		{
			Vector3 vecteur = Calcule.RoundVecteur(raycastHit.point - raycastHit.normal * 0.0001f);
			int num = Brix.Engine.Abstract.Voxel.GetScalaire(vecteur);
			bool flag2 = !Brix.Engine.Voxel.IsOpenAndWalkable(XYZ.FromScalaire(num));
			if (flag2)
			{
				num += 1048576;
			}
			bool flag3 = !Brix.Engine.Voxel.IsOpenAndWalkable(XYZ.FromScalaire(num));
			if (flag3)
			{
				result = false;
			}
			else
			{
				pos = Brix.Engine.Abstract.Voxel.GetVecteur(num);
				result = true;
			}
		}
		return result;
	}

	private static bool MasterOn
	{
		get
		{
			bool result;
			try
			{
				bool flag = BtcBlockMarks.masterField == null;
				if (flag)
				{
					BtcBlockMarks.masterField = typeof(BtcOverlays).GetField("master", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				}
				result = (BtcBlockMarks.masterField == null || (bool)BtcBlockMarks.masterField.GetValue(null));
			}
			catch (Exception)
			{
				result = true;
			}
			return result;
		}
	}

	private static string T(string en, string ru)
	{
		return BtcLang.Russian ? ru : en;
	}

	public static void BeginSession()
	{
		BtcBlockMarks.tPos.Clear();
		BtcBlockMarks.tEnded = false;
	}

	public static void AddTarget(Vector3 cell)
	{
		BtcBlockMarks.Load();
		bool flag = !BtcBlockMarks.tOn || BtcBlockMarks.tPos.Count >= 512;
		bool flag2 = !flag;
		bool flag3 = flag2;
		if (flag3)
		{
			BtcBlockMarks.tEnded = false;
			BtcBlockMarks.tPos.Add(cell);
		}
	}

	public static void EndSession()
	{
		bool flag = BtcBlockMarks.tPos.Count == 0;
		bool flag2 = !flag;
		bool flag3 = flag2;
		if (flag3)
		{
			bool flag4 = BtcBlockMarks.tLife <= 0.001f;
			bool flag5 = flag4;
			bool flag6 = flag5;
			if (flag6)
			{
				BtcBlockMarks.tPos.Clear();
				BtcBlockMarks.tEnded = false;
			}
			else
			{
				BtcBlockMarks.tEnded = true;
				BtcBlockMarks.tEndTime = Time.unscaledTime;
			}
		}
	}

	public static void Tick()
	{
		BtcBlockMarks.Load();
		float unscaledTime = Time.unscaledTime;
		bool flag = BtcBlockMarks.dirty && unscaledTime >= BtcBlockMarks.nextSave;
		bool flag2 = flag;
		bool flag3 = flag2;
		if (flag3)
		{
			BtcBlockMarks.Save();
		}
		bool flag4 = BtcBlockMarks.tEnded && unscaledTime - BtcBlockMarks.tEndTime >= BtcBlockMarks.tLife;
		bool flag5 = flag4;
		bool flag6 = flag5;
		if (flag6)
		{
			BtcBlockMarks.tPos.Clear();
			BtcBlockMarks.tEnded = false;
		}
		bool flag7 = !BtcBlockMarks.hOn || !BtcBlockMarks.MasterOn || (BtcBlockMarks.hAlt && !Input.GetKey(KeyCode.LeftAlt));
		bool flag8 = flag7;
		bool flag9 = flag8;
		if (flag9)
		{
			BtcBlockMarks.hValid = false;
		}
		else
		{
			Camera main = Camera.main;
			bool flag10 = main == null;
			bool flag11 = flag10;
			bool flag12 = flag11;
			if (flag12)
			{
				BtcBlockMarks.hValid = false;
			}
			else
			{
				Vector3 mousePosition = Input.mousePosition;
				Transform transform = main.transform;
				bool flag13 = unscaledTime < BtcBlockMarks.hNext && mousePosition == BtcBlockMarks.hMouse && transform.position == BtcBlockMarks.hCamPos && transform.rotation == BtcBlockMarks.hCamRot;
				bool flag14 = !flag13;
				bool flag15 = flag14;
				if (flag15)
				{
					BtcBlockMarks.hNext = unscaledTime + 0.2f;
					BtcBlockMarks.hMouse = mousePosition;
					BtcBlockMarks.hCamPos = transform.position;
					BtcBlockMarks.hCamRot = transform.rotation;
					bool flag16 = BtcHotkeys.PointerOverUi();
					bool flag17 = flag16;
					bool flag18 = flag17;
					if (flag18)
					{
						BtcBlockMarks.hValid = false;
					}
					else
					{
						Vector3 vector;
						bool flag19 = BtcBlockMarks.HoverCell(main, mousePosition, out vector);
						bool flag20 = flag19;
						bool flag21 = flag20;
						if (flag21)
						{
							BtcBlockMarks.hPos = vector;
							BtcBlockMarks.hValid = true;
						}
						else
						{
							BtcBlockMarks.hValid = false;
						}
					}
				}
			}
		}
	}

	public static void Render(Camera cam)
	{
		bool flag = BtcBlockMarks.tPos.Count == 0 && !BtcBlockMarks.hValid;
		bool flag2 = !flag;
		bool flag3 = flag2;
		if (flag3)
		{
			bool flag4 = cam == null || cam != Camera.main || !BtcBlockMarks.MasterOn || !BtcBlockMarks.EnsureMat();
			bool flag5 = !flag4;
			bool flag6 = flag5;
			if (flag6)
			{
				float num = 1f;
				bool flag7 = BtcBlockMarks.tEnded;
				bool flag8 = flag7;
				bool flag9 = flag8;
				if (flag9)
				{
					num = 1f - (Time.unscaledTime - BtcBlockMarks.tEndTime) / Mathf.Max(0.01f, BtcBlockMarks.tLife);
				}
				bool flag10 = BtcBlockMarks.tOn && BtcBlockMarks.tPos.Count > 0 && num > 0f;
				bool flag11 = BtcBlockMarks.hOn && BtcBlockMarks.hValid;
				bool flag12 = flag10 || flag11;
				bool flag13 = flag12;
				bool flag14 = flag13;
				if (flag14)
				{
					bool flag15 = BtcBlockMarks.mat.HasProperty("_ZTest");
					bool flag16 = flag15;
					bool flag17 = flag16;
					if (flag17)
					{
						BtcBlockMarks.mat.SetInt("_ZTest", BtcBlockMarks.through ? 8 : 4);
					}
					BtcBlockMarks.mat.SetPass(0);
					GL.PushMatrix();
					GL.Begin(7);
					bool flag18 = flag10;
					bool flag19 = flag18;
					bool flag20 = flag19;
					if (flag20)
					{
						for (int i = 0; i < BtcBlockMarks.tPos.Count; i++)
						{
							BtcBlockMarks.DrawCell(BtcBlockMarks.tPos[i], BtcBlockMarks.tStyle, BtcBlockMarks.tCol, BtcBlockMarks.tAlpha, num);
						}
					}
					bool flag21 = flag11;
					bool flag22 = flag21;
					bool flag23 = flag22;
					if (flag23)
					{
						BtcBlockMarks.DrawCell(BtcBlockMarks.hPos, BtcBlockMarks.hStyle, BtcBlockMarks.hCol, BtcBlockMarks.hAlpha, 1f);
					}
					GL.End();
					GL.PopMatrix();
				}
			}
		}
	}

	private static void DrawCell(Vector3 cell, int style, Color col, float alpha, float mul)
	{
		float num = 0.5f * BtcBlockMarks.size;
		float num2 = cell.y - 0.5f + BtcBlockMarks.yoff + 0.02f;
		float a = Mathf.Min(1f, alpha * 1.8f + 0.2f) * mul;
		bool flag = style == 3;
		bool flag2 = flag;
		bool flag3 = flag2;
		if (flag3)
		{
			float num3 = cell.y - 0.5f + BtcBlockMarks.yoff;
			float y = num3 - 2f * num;
			float y2 = num3 + 0.02f;
			col.a = alpha * mul;
			GL.Color(col);
			Vector3 vector = new Vector3(cell.x - num, y, cell.z - num);
			Vector3 vector2 = new Vector3(cell.x + num, y, cell.z - num);
			Vector3 vector3 = new Vector3(cell.x + num, y, cell.z + num);
			Vector3 vector4 = new Vector3(cell.x - num, y, cell.z + num);
			Vector3 vector5 = new Vector3(cell.x - num, y2, cell.z - num);
			Vector3 vector6 = new Vector3(cell.x + num, y2, cell.z - num);
			Vector3 vector7 = new Vector3(cell.x + num, y2, cell.z + num);
			Vector3 vector8 = new Vector3(cell.x - num, y2, cell.z + num);
			BtcBlockMarks.Quad(vector, vector2, vector3, vector4);
			BtcBlockMarks.Quad(vector5, vector6, vector7, vector8);
			BtcBlockMarks.Quad(vector, vector2, vector6, vector5);
			BtcBlockMarks.Quad(vector2, vector3, vector7, vector6);
			BtcBlockMarks.Quad(vector3, vector4, vector8, vector7);
			BtcBlockMarks.Quad(vector4, vector, vector5, vector8);
		}
		else
		{
			bool flag4 = style == 0 || style == 2;
			bool flag5 = flag4;
			bool flag6 = flag5;
			if (flag6)
			{
				col.a = alpha * mul;
				GL.Color(col);
				BtcBlockMarks.Quad(new Vector3(cell.x - num, num2, cell.z - num), new Vector3(cell.x + num, num2, cell.z - num), new Vector3(cell.x + num, num2, cell.z + num), new Vector3(cell.x - num, num2, cell.z + num));
			}
			bool flag7 = style == 1 || style == 2;
			bool flag8 = flag7;
			bool flag9 = flag8;
			if (flag9)
			{
				float num4 = Mathf.Min(BtcBlockMarks.frameW, num);
				float y3 = num2 + 0.005f;
				col.a = a;
				GL.Color(col);
				BtcBlockMarks.Quad(new Vector3(cell.x - num, y3, cell.z + num - num4), new Vector3(cell.x + num, y3, cell.z + num - num4), new Vector3(cell.x + num, y3, cell.z + num), new Vector3(cell.x - num, y3, cell.z + num));
				BtcBlockMarks.Quad(new Vector3(cell.x - num, y3, cell.z - num), new Vector3(cell.x + num, y3, cell.z - num), new Vector3(cell.x + num, y3, cell.z - num + num4), new Vector3(cell.x - num, y3, cell.z - num + num4));
				BtcBlockMarks.Quad(new Vector3(cell.x - num, y3, cell.z - num + num4), new Vector3(cell.x - num + num4, y3, cell.z - num + num4), new Vector3(cell.x - num + num4, y3, cell.z + num - num4), new Vector3(cell.x - num, y3, cell.z + num - num4));
				BtcBlockMarks.Quad(new Vector3(cell.x + num - num4, y3, cell.z - num + num4), new Vector3(cell.x + num, y3, cell.z - num + num4), new Vector3(cell.x + num, y3, cell.z + num - num4), new Vector3(cell.x + num - num4, y3, cell.z + num - num4));
			}
		}
	}

	private static void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
	{
		GL.Vertex(a);
		GL.Vertex(b);
		GL.Vertex(c);
		GL.Vertex(d);
	}

	private static bool EnsureMat()
	{
		bool flag = BtcBlockMarks.mat != null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool result;
		if (flag3)
		{
			result = true;
		}
		else
		{
			Shader shader = Shader.Find("Hidden/Internal-Colored");
			bool flag4 = shader == null;
			bool flag5 = flag4;
			bool flag6 = flag5;
			if (flag6)
			{
				shader = Shader.Find("Sprites/Default");
			}
			bool flag7 = shader == null;
			bool flag8 = flag7;
			bool flag9 = flag8;
			if (flag9)
			{
				result = false;
			}
			else
			{
				BtcBlockMarks.mat = new Material(shader);
				BtcBlockMarks.mat.hideFlags = HideFlags.HideAndDontSave;
				bool flag10 = BtcBlockMarks.mat.HasProperty("_Cull");
				bool flag11 = flag10;
				bool flag12 = flag11;
				if (flag12)
				{
					BtcBlockMarks.mat.SetInt("_Cull", 0);
				}
				bool flag13 = BtcBlockMarks.mat.HasProperty("_ZWrite");
				bool flag14 = flag13;
				bool flag15 = flag14;
				if (flag15)
				{
					BtcBlockMarks.mat.SetInt("_ZWrite", 0);
				}
				bool flag16 = BtcBlockMarks.mat.HasProperty("_SrcBlend");
				bool flag17 = flag16;
				bool flag18 = flag17;
				if (flag18)
				{
					BtcBlockMarks.mat.SetInt("_SrcBlend", 5);
				}
				bool flag19 = BtcBlockMarks.mat.HasProperty("_DstBlend");
				bool flag20 = flag19;
				bool flag21 = flag20;
				if (flag21)
				{
					BtcBlockMarks.mat.SetInt("_DstBlend", 10);
				}
				result = true;
			}
		}
		return result;
	}

	public static Color GetColor(int id)
	{
		BtcBlockMarks.Load();
		Color result = (id == 40) ? BtcBlockMarks.tCol : BtcBlockMarks.hCol;
		result.a = 1f;
		return result;
	}

	public static void SetColor(int id, Color c)
	{
		BtcBlockMarks.Load();
		c.a = 1f;
		bool flag = id == 40;
		bool flag2 = flag;
		bool flag3 = flag2;
		if (flag3)
		{
			BtcBlockMarks.tCol = c;
		}
		else
		{
			BtcBlockMarks.hCol = c;
		}
		BtcBlockMarks.MarkDirty();
	}

	private static string StyleName(int s)
	{
		bool flag = s == 0;
		bool flag2 = flag;
		bool flag3 = flag2;
		string result;
		if (flag3)
		{
			result = BtcBlockMarks.T("Fill", "Заливка");
		}
		else
		{
			bool flag4 = s == 1;
			bool flag5 = flag4;
			bool flag6 = flag5;
			if (flag6)
			{
				result = BtcBlockMarks.T("Frame", "Рамка");
			}
			else
			{
				bool flag7 = s == 2;
				bool flag8 = flag7;
				bool flag9 = flag8;
				if (flag9)
				{
					result = BtcBlockMarks.T("Fill + frame", "Заливка + рамка");
				}
				else
				{
					result = BtcBlockMarks.T("Cube", "Куб");
				}
			}
		}
		return result;
	}

	private static void MarkDirty()
	{
		BtcBlockMarks.dirty = true;
		BtcBlockMarks.nextSave = Time.unscaledTime + 0.5f;
	}

	private static bool Chg(float a, float b)
	{
		return Mathf.Abs(a - b) > 0.0005f;
	}

	public static void DrawSettings(Func<string, bool, bool> check, Func<string, string, bool> step, BtcBlockMarks.SliderFn slider, BtcBlockMarks.ColorFn color)
	{
		BtcBlockMarks.Load();
		bool flag = false;
		float num = slider(BtcBlockMarks.T("Line-Move: release delay", "Line-Move: задержка после отпускания"), 9401, (float)BtcBlockMarks.delayMs, 0f, 500f, 10f, "0", BtcBlockMarks.T(" ms", " мс"));
		bool flag2 = BtcBlockMarks.Chg(num, (float)BtcBlockMarks.delayMs);
		bool flag3 = flag2;
		bool flag4 = flag3;
		if (flag4)
		{
			BtcBlockMarks.delayMs = Mathf.Clamp(Mathf.RoundToInt(num), 0, 500);
			flag = true;
		}
		bool flag5 = check(BtcBlockMarks.T("Highlight Line-Move target blocks", "Подсвечивать блоки Line-Move"), BtcBlockMarks.tOn);
		bool flag6 = flag5;
		bool flag7 = flag6;
		if (flag7)
		{
			BtcBlockMarks.tOn = !BtcBlockMarks.tOn;
			bool flag8 = !BtcBlockMarks.tOn;
			bool flag9 = flag8;
			bool flag10 = flag9;
			if (flag10)
			{
				BtcBlockMarks.tPos.Clear();
				BtcBlockMarks.tEnded = false;
			}
			flag = true;
		}
		bool flag11 = BtcBlockMarks.tOn;
		bool flag12 = flag11;
		bool flag13 = flag12;
		if (flag13)
		{
			bool flag14 = step(BtcBlockMarks.T("   Style", "   Стиль"), BtcBlockMarks.StyleName(BtcBlockMarks.tStyle));
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				BtcBlockMarks.tStyle = (BtcBlockMarks.tStyle + 1) % 4;
				flag = true;
			}
			color(BtcBlockMarks.T("   Color", "   Цвет"), 40);
			float a = slider(BtcBlockMarks.T("   Opacity", "   Прозрачность"), 9402, BtcBlockMarks.tAlpha * 100f, 5f, 100f, 5f, "0", "%") / 100f;
			bool flag17 = BtcBlockMarks.Chg(a, BtcBlockMarks.tAlpha);
			bool flag18 = flag17;
			bool flag19 = flag18;
			if (flag19)
			{
				BtcBlockMarks.tAlpha = a;
				flag = true;
			}
			float a2 = slider(BtcBlockMarks.T("   Fade-out after end", "   Затухание после конца"), 9403, BtcBlockMarks.tLife, 0f, 10f, 0.5f, "0.0", BtcBlockMarks.T(" s", " с"));
			bool flag20 = BtcBlockMarks.Chg(a2, BtcBlockMarks.tLife);
			bool flag21 = flag20;
			bool flag22 = flag21;
			if (flag22)
			{
				BtcBlockMarks.tLife = a2;
				flag = true;
			}
		}
		bool flag23 = check(BtcBlockMarks.T("Highlight the block under the cursor", "Подсвечивать блок под курсором"), BtcBlockMarks.hOn);
		bool flag24 = flag23;
		bool flag25 = flag24;
		if (flag25)
		{
			BtcBlockMarks.hOn = !BtcBlockMarks.hOn;
			BtcBlockMarks.hValid = false;
			flag = true;
		}
		bool flag26 = BtcBlockMarks.hOn;
		bool flag27 = flag26;
		bool flag28 = flag27;
		if (flag28)
		{
			bool flag29 = check(BtcBlockMarks.T("   Only while holding LeftAlt", "   Только при зажатом LeftAlt"), BtcBlockMarks.hAlt);
			bool flag30 = flag29;
			bool flag31 = flag30;
			if (flag31)
			{
				BtcBlockMarks.hAlt = !BtcBlockMarks.hAlt;
				flag = true;
			}
			bool flag32 = step(BtcBlockMarks.T("   Style", "   Стиль"), BtcBlockMarks.StyleName(BtcBlockMarks.hStyle));
			bool flag33 = flag32;
			bool flag34 = flag33;
			if (flag34)
			{
				BtcBlockMarks.hStyle = (BtcBlockMarks.hStyle + 1) % 4;
				flag = true;
			}
			color(BtcBlockMarks.T("   Color", "   Цвет"), 41);
			float a3 = slider(BtcBlockMarks.T("   Opacity", "   Прозрачность"), 9404, BtcBlockMarks.hAlpha * 100f, 5f, 100f, 5f, "0", "%") / 100f;
			bool flag35 = BtcBlockMarks.Chg(a3, BtcBlockMarks.hAlpha);
			bool flag36 = flag35;
			bool flag37 = flag36;
			if (flag37)
			{
				BtcBlockMarks.hAlpha = a3;
				flag = true;
			}
		}
		bool flag38 = BtcBlockMarks.tOn || BtcBlockMarks.hOn;
		bool flag39 = flag38;
		bool flag40 = flag39;
		if (flag40)
		{
			float a4 = slider(BtcBlockMarks.T("Frame width", "Толщина рамки"), 9405, BtcBlockMarks.frameW, 0.02f, 0.25f, 0.01f, "0.00", string.Empty);
			bool flag41 = BtcBlockMarks.Chg(a4, BtcBlockMarks.frameW);
			bool flag42 = flag41;
			bool flag43 = flag42;
			if (flag43)
			{
				BtcBlockMarks.frameW = a4;
				flag = true;
			}
			float a5 = slider(BtcBlockMarks.T("Block size", "Размер блока"), 9406, BtcBlockMarks.size * 100f, 40f, 100f, 5f, "0", "%") / 100f;
			bool flag44 = BtcBlockMarks.Chg(a5, BtcBlockMarks.size);
			bool flag45 = flag44;
			bool flag46 = flag45;
			if (flag46)
			{
				BtcBlockMarks.size = a5;
				flag = true;
			}
			float a6 = slider(BtcBlockMarks.T("Height offset", "Смещение по высоте"), 9407, BtcBlockMarks.yoff, -1f, 1f, 0.05f, "0.00", string.Empty);
			bool flag47 = BtcBlockMarks.Chg(a6, BtcBlockMarks.yoff);
			bool flag48 = flag47;
			bool flag49 = flag48;
			if (flag49)
			{
				BtcBlockMarks.yoff = a6;
				flag = true;
			}
			bool flag50 = check(BtcBlockMarks.T("Show through walls", "Показывать сквозь стены"), BtcBlockMarks.through);
			bool flag51 = flag50;
			bool flag52 = flag51;
			if (flag52)
			{
				BtcBlockMarks.through = !BtcBlockMarks.through;
				flag = true;
			}
		}
		bool flag53 = flag;
		bool flag54 = flag53;
		bool flag55 = flag54;
		if (flag55)
		{
			BtcBlockMarks.MarkDirty();
		}
	}

	private static void Load()
	{
		bool flag = BtcBlockMarks.loaded;
		bool flag2 = !flag;
		bool flag3 = flag2;
		if (flag3)
		{
			BtcBlockMarks.loaded = true;
			Dictionary<string, string> dictionary = new Dictionary<string, string>();
			try
			{
				string path = BtcPaths.Config("blockmarks.txt");
				bool flag4 = File.Exists(path);
				bool flag5 = flag4;
				bool flag6 = flag5;
				if (flag6)
				{
					string[] array = File.ReadAllLines(path);
					for (int i = 0; i < array.Length; i++)
					{
						string text = array[i].Trim();
						bool flag7 = text.Length == 0 || text.StartsWith("#");
						bool flag8 = !flag7;
						bool flag9 = flag8;
						if (flag9)
						{
							int num = text.IndexOf('=');
							bool flag10 = num > 0;
							bool flag11 = flag10;
							bool flag12 = flag11;
							if (flag12)
							{
								dictionary[text.Substring(0, num).Trim().ToLowerInvariant()] = text.Substring(num + 1).Trim();
							}
						}
					}
				}
			}
			catch (Exception)
			{
			}
			BtcBlockMarks.delayMs = Mathf.Clamp(Mathf.RoundToInt(BtcBlockMarks.RF(dictionary, "delay", (float)BtcBlockMarks.delayMs)), 0, 500);
			BtcBlockMarks.tOn = (BtcBlockMarks.RF(dictionary, "t.on", BtcBlockMarks.tOn ? 1f : 0f) > 0.5f);
			BtcBlockMarks.tStyle = Mathf.Clamp(Mathf.RoundToInt(BtcBlockMarks.RF(dictionary, "t.style", (float)BtcBlockMarks.tStyle)), 0, 3);
			BtcBlockMarks.tAlpha = Mathf.Clamp(BtcBlockMarks.RF(dictionary, "t.alpha", BtcBlockMarks.tAlpha), 0.05f, 1f);
			BtcBlockMarks.tLife = Mathf.Clamp(BtcBlockMarks.RF(dictionary, "t.life", BtcBlockMarks.tLife), 0f, 10f);
			BtcBlockMarks.tCol = BtcBlockMarks.RC(dictionary, "t.color", BtcBlockMarks.tCol);
			BtcBlockMarks.hOn = (BtcBlockMarks.RF(dictionary, "h.on", BtcBlockMarks.hOn ? 1f : 0f) > 0.5f);
			BtcBlockMarks.hAlt = (BtcBlockMarks.RF(dictionary, "h.alt", BtcBlockMarks.hAlt ? 1f : 0f) > 0.5f);
			BtcBlockMarks.hStyle = Mathf.Clamp(Mathf.RoundToInt(BtcBlockMarks.RF(dictionary, "h.style", (float)BtcBlockMarks.hStyle)), 0, 3);
			BtcBlockMarks.hAlpha = Mathf.Clamp(BtcBlockMarks.RF(dictionary, "h.alpha", BtcBlockMarks.hAlpha), 0.05f, 1f);
			BtcBlockMarks.hCol = BtcBlockMarks.RC(dictionary, "h.color", BtcBlockMarks.hCol);
			BtcBlockMarks.frameW = Mathf.Clamp(BtcBlockMarks.RF(dictionary, "frame", BtcBlockMarks.frameW), 0.02f, 0.25f);
			BtcBlockMarks.size = Mathf.Clamp(BtcBlockMarks.RF(dictionary, "size", BtcBlockMarks.size), 0.4f, 1f);
			BtcBlockMarks.yoff = Mathf.Clamp(BtcBlockMarks.RF(dictionary, "yoff", BtcBlockMarks.yoff), -1f, 1f);
			BtcBlockMarks.through = (BtcBlockMarks.RF(dictionary, "through", BtcBlockMarks.through ? 1f : 0f) > 0.5f);
		}
	}

	private static float RF(Dictionary<string, string> d, string k, float def)
	{
		string s;
		float num;
		return (d.TryGetValue(k, out s) && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out num)) ? num : def;
	}

	private static Color RC(Dictionary<string, string> d, string k, Color def)
	{
		string text;
		bool flag = !d.TryGetValue(k, out text);
		bool flag2 = flag;
		bool flag3 = flag2;
		Color result;
		if (flag3)
		{
			result = def;
		}
		else
		{
			string[] array = text.Split(new char[]
			{
				','
			});
			float value = 0f;
			float value2 = 0f;
			float value3 = 0f;
			bool flag4 = array.Length >= 3 && float.TryParse(array[0], NumberStyles.Float, CultureInfo.InvariantCulture, out value) && float.TryParse(array[1], NumberStyles.Float, CultureInfo.InvariantCulture, out value2) && float.TryParse(array[2], NumberStyles.Float, CultureInfo.InvariantCulture, out value3);
			bool flag5 = flag4;
			bool flag6 = flag5;
			if (flag6)
			{
				result = new Color(Mathf.Clamp01(value), Mathf.Clamp01(value2), Mathf.Clamp01(value3), 1f);
			}
			else
			{
				result = def;
			}
		}
		return result;
	}

	private static string F(float v)
	{
		return v.ToString("0.###", CultureInfo.InvariantCulture);
	}

	private static string C(Color c)
	{
		return string.Concat(new string[]
		{
			BtcBlockMarks.F(c.r),
			",",
			BtcBlockMarks.F(c.g),
			",",
			BtcBlockMarks.F(c.b)
		});
	}

	private static void Save()
	{
		BtcBlockMarks.dirty = false;
		try
		{
			List<string> list = new List<string>();
			list.Add("# Better Than Castle: block highlight. delay = Line-Move release delay in ms (0..500); t.* = Line-Move target blocks; h.* = block under the cursor; style 0 fill, 1 frame, 2 fill+frame, 3 cube");
			list.Add("delay=" + BtcBlockMarks.delayMs);
			list.Add("t.on=" + (BtcBlockMarks.tOn ? "1" : "0"));
			list.Add("t.style=" + BtcBlockMarks.tStyle);
			list.Add("t.alpha=" + BtcBlockMarks.F(BtcBlockMarks.tAlpha));
			list.Add("t.life=" + BtcBlockMarks.F(BtcBlockMarks.tLife));
			list.Add("t.color=" + BtcBlockMarks.C(BtcBlockMarks.tCol));
			list.Add("h.on=" + (BtcBlockMarks.hOn ? "1" : "0"));
			list.Add("h.alt=" + (BtcBlockMarks.hAlt ? "1" : "0"));
			list.Add("h.style=" + BtcBlockMarks.hStyle);
			list.Add("h.alpha=" + BtcBlockMarks.F(BtcBlockMarks.hAlpha));
			list.Add("h.color=" + BtcBlockMarks.C(BtcBlockMarks.hCol));
			list.Add("frame=" + BtcBlockMarks.F(BtcBlockMarks.frameW));
			list.Add("size=" + BtcBlockMarks.F(BtcBlockMarks.size));
			list.Add("yoff=" + BtcBlockMarks.F(BtcBlockMarks.yoff));
			list.Add("through=" + (BtcBlockMarks.through ? "1" : "0"));
			File.WriteAllLines(BtcPaths.Config("blockmarks.txt"), list.ToArray());
		}
		catch (Exception)
		{
		}
	}

	private static FieldInfo masterField;

	public const int TargetColorId = 40;

	public const int HoverColorId = 41;

	private static bool loaded;

	private static bool dirty;

	private static float nextSave;

	private static int delayMs = 150;

	private static bool tOn = true;

	private static int tStyle = 2;

	private static float tAlpha = 0.45f;

	private static float tLife = 2f;

	private static Color tCol = new Color(0.2f, 0.5f, 1f, 1f);

	private static bool hOn;

	private static bool hAlt;

	private static int hStyle = 2;

	private static float hAlpha = 0.35f;

	private static Color hCol = new Color(1f, 0.9f, 0.2f, 1f);

	private static float frameW = 0.06f;

	private static float size = 0.92f;

	private static float yoff;

	private static bool through = true;

	private static readonly List<Vector3> tPos = new List<Vector3>();

	private static bool tEnded;

	private static float tEndTime;

	private static bool hValid;

	private static Vector3 hPos;

	private static Vector3 hMouse;

	private static Vector3 hCamPos;

	private static Quaternion hCamRot;

	private static float hNext;

	private static Material mat;

	public delegate float SliderFn(string label, int id, float value, float min, float max, float step, string format, string suffix);

	public delegate void ColorFn(string label, int id);
}
}
