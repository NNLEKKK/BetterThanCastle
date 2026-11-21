using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BetterThanCastle
{
public static class BtcSaveFormat
{
	public static void LoadSettings()
	{
		bool flag = BtcSaveFormat.settingsLoaded;
		if (!flag)
		{
			BtcSaveFormat.settingsLoaded = true;
			try
			{
				string path = BtcPaths.Config("share.txt");
				bool flag2 = !File.Exists(path);
				if (!flag2)
				{
					string[] array = File.ReadAllLines(path);
					for (int i = 0; i < array.Length; i++)
					{
						int num = array[i].IndexOf('=');
						bool flag3 = num <= 0;
						if (!flag3)
						{
							string a = array[i].Substring(0, num).Trim();
							string text = array[i].Substring(num + 1).Trim();
							if (a == "shareOn") { BtcSaveFormat.ShareOn = (text == "1"); continue; }
							if (a == "sendBtcs") { BtcSaveFormat.SendBtcs = (text == "1"); continue; }
							if (a == "sendThumb") { BtcSaveFormat.SendThumb = (text == "1"); continue; }
							bool flag4 = a == "includeThumb";
							if (flag4)
							{
								BtcSaveFormat.IncludeThumb = (text == "1");
							}
							else
							{
								bool flag5 = a == "thumbFactor";
								if (flag5)
								{
									int val;
									bool flag6 = int.TryParse(text, out val);
									if (flag6)
									{
										BtcSaveFormat.ThumbFactor = Math.Max(1, Math.Min(15, val));
									}
								}
							}
						}
					}
				}
			}
			catch (Exception)
			{
			}
		}
	}

	public static void SaveSettings()
	{
		try
		{
			Directory.CreateDirectory(BtcPaths.Configs);
			File.WriteAllLines(BtcPaths.Config("share.txt"), new string[]
			{
				"includeThumb=" + (BtcSaveFormat.IncludeThumb ? "1" : "0"),
				"shareOn=" + (BtcSaveFormat.ShareOn ? "1" : "0"),
				"sendBtcs=" + (BtcSaveFormat.SendBtcs ? "1" : "0"),
				"sendThumb=" + (BtcSaveFormat.SendThumb ? "1" : "0"),
				"thumbFactor=" + Math.Max(1, Math.Min(15, BtcSaveFormat.ThumbFactor)).ToString()
			});
		}
		catch (Exception)
		{
		}
	}

	public static bool IsBtcs(string path)
	{
		return !string.IsNullOrEmpty(path) && path.EndsWith(".btcs", StringComparison.OrdinalIgnoreCase);
	}

	public static bool ReadInfo(string file, out BtcSaveFormat.Info info, out string err)
	{
		info = null;
		err = null;
		bool result;
		try
		{
			using (FileStream fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				result = BtcSaveFormat.ReadHeader(fileStream, out info, out err);
			}
		}
		catch (Exception ex)
		{
			info = null;
			err = ex.Message;
			result = false;
		}
		return result;
	}

	private static bool ReadHeader(Stream s, out BtcSaveFormat.Info info, out string err)
	{
		info = null;
		err = null;
		MemoryStream memoryStream = new MemoryStream();
		byte[] array = new byte[4];
		bool flag = BtcSaveFormat.ReadExact(s, array, 4) < 4 || array[0] != BtcSaveFormat.Magic[0] || array[1] != BtcSaveFormat.Magic[1] || array[2] != BtcSaveFormat.Magic[2] || array[3] != BtcSaveFormat.Magic[3];
		bool result;
		if (flag)
		{
			err = "not a .btcs file";
			result = false;
		}
		else
		{
			memoryStream.Write(array, 0, 4);
			byte[] array2 = new byte[4];
			bool flag2 = BtcSaveFormat.ReadExact(s, array2, 4) < 4;
			if (flag2)
			{
				err = "file is truncated";
				result = false;
			}
			else
			{
				memoryStream.Write(array2, 0, 4);
				BtcSaveFormat.Info info2 = new BtcSaveFormat.Info();
				info2.version = (int)array2[0];
				bool flag3 = info2.version != 1;
				if (flag3)
				{
					err = "unsupported .btcs version " + info2.version.ToString();
					result = false;
				}
				else
				{
					info2.isMap = (array2[1] == 1);
					info2.thumbEmbedded = ((array2[2] & 1) > 0);
					info2.thumbDownscaled = ((array2[2] & 2) > 0);
					info2.thumbFactor = (int)array2[3];
					long num;
					bool flag4 = !BtcSaveFormat.ReadVarint(s, memoryStream, out num) || num < 0L || num > 4096L;
					if (flag4)
					{
						err = "bad header";
						result = false;
					}
					else
					{
						byte[] array3 = new byte[(int)num];
						bool flag5 = BtcSaveFormat.ReadExact(s, array3, array3.Length) < array3.Length;
						if (flag5)
						{
							err = "file is truncated";
							result = false;
						}
						else
						{
							memoryStream.Write(array3, 0, array3.Length);
							info2.name = Encoding.UTF8.GetString(array3);
							long num2;
							bool flag6 = !BtcSaveFormat.ReadVarint(s, memoryStream, out num2) || num2 < 0L || num2 > 67108864L;
							if (flag6)
							{
								err = "bad header";
								result = false;
							}
							else
							{
								byte[] array4 = new byte[(int)num2];
								bool flag7 = BtcSaveFormat.ReadExact(s, array4, array4.Length) < array4.Length;
								if (flag7)
								{
									err = "file is truncated";
									result = false;
								}
								else
								{
									memoryStream.Write(array4, 0, array4.Length);
									info2.thumb = ((array4.Length != 0) ? array4 : null);
									long num3 = 0L;
									bool flag8 = !BtcSaveFormat.ReadVarint(s, memoryStream, out num3) || num3 < 0L || num3 > 1000000L;
									if (flag8)
									{
										err = "bad header";
										result = false;
									}
									else
									{
										info2.fileCount = (int)num3;
										byte[] array5 = new byte[4];
										bool flag9 = BtcSaveFormat.ReadExact(s, array5, 4) < 4;
										if (flag9)
										{
											err = "file is truncated";
											result = false;
										}
										else
										{
											byte[] array6 = memoryStream.ToArray();
											uint num4 = (uint)((int)array5[0] | (int)array5[1] << 8 | (int)array5[2] << 16 | (int)array5[3] << 24);
											bool flag10 = BtcSaveFormat.Crc32(array6, 0, array6.Length) != num4;
											if (flag10)
											{
												err = "header is corrupted";
												result = false;
											}
											else
											{
												info2.bodyOffset = (long)(array6.Length + 4);
												info = info2;
												result = true;
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
		return result;
	}

	public static byte[] ReadThumbnail(string file)
	{
		try
		{
			BtcSaveFormat.Info info;
			string text;
			bool flag = BtcSaveFormat.ReadInfo(file, out info, out text) && info.thumb != null;
			if (flag)
			{
				return info.thumb;
			}
			string dir = Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file));
			string text2 = BtcSaveFormat.FindThumb(dir);
			bool flag2 = text2 != null;
			if (flag2)
			{
				return File.ReadAllBytes(text2);
			}
		}
		catch (Exception)
		{
		}
		return null;
	}

	private static string FindThumb(string dir)
	{
		bool flag = !Directory.Exists(dir);
		string result;
		if (flag)
		{
			result = null;
		}
		else
		{
			string[] files = Directory.GetFiles(dir, "*.png", SearchOption.TopDirectoryOnly);
			for (int i = 0; i < files.Length; i++)
			{
				bool flag2 = string.Equals(Path.GetFileName(files[i]), "thumbnail.png", StringComparison.OrdinalIgnoreCase);
				if (flag2)
				{
					return files[i];
				}
			}
			result = null;
		}
		return result;
	}

	public static bool Pack(string folder, string outFile, bool isMap, out string err)
	{
		err = null;
		BtcSaveFormat.LoadSettings();
		string text = outFile + ".tmp";
		bool result;
		try
		{
			bool flag = !Directory.Exists(folder);
			if (flag)
			{
				err = "folder not found";
				result = false;
			}
			else
			{
				string text2 = Path.GetFullPath(folder).TrimEnd(new char[]
				{
					Path.DirectorySeparatorChar,
					Path.AltDirectorySeparatorChar
				});
				string[] files = Directory.GetFiles(text2, "*", SearchOption.AllDirectories);
				List<string> list = new List<string>();
				string text3 = null;
				for (int i = 0; i < files.Length; i++)
				{
					string text4 = files[i].Substring(text2.Length + 1).Replace('\\', '/');
					bool flag2 = text4.EndsWith(".btcs", StringComparison.OrdinalIgnoreCase) || text4.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase);
					if (!flag2)
					{
						bool flag3 = string.Equals(text4, "thumbnail.png", StringComparison.OrdinalIgnoreCase);
						if (flag3)
						{
							text3 = files[i];
						}
						else
						{
							list.Add(files[i]);
						}
					}
				}
				list.Sort(StringComparer.Ordinal);
				byte[] array = null;
				bool flag4 = false;
				int num = Math.Max(1, Math.Min(15, BtcSaveFormat.ThumbFactor));
				bool flag5 = BtcSaveFormat.IncludeThumb && text3 != null;
				if (flag5)
				{
					byte[] array2 = File.ReadAllBytes(text3);
					array = array2;
					bool flag6 = num > 1;
					if (flag6)
					{
						byte[] array3;
						bool flag7 = BtcSaveFormat.Downscale(array2, num, out array3) && array3.Length < array2.Length;
						if (flag7)
						{
							array = array3;
							flag4 = true;
						}
					}
				}
				MemoryStream memoryStream = new MemoryStream();
				memoryStream.Write(BtcSaveFormat.Magic, 0, 4);
				memoryStream.WriteByte(1);
				memoryStream.WriteByte((byte)(isMap ? 1 : 0));
				memoryStream.WriteByte((byte)(((array != null) ? 1 : 0) | (flag4 ? 2 : 0)));
				memoryStream.WriteByte((byte)(flag4 ? num : 1));
				byte[] bytes = Encoding.UTF8.GetBytes(Path.GetFileName(text2));
				BtcSaveFormat.WriteVarint(memoryStream, (ulong)((long)bytes.Length));
				memoryStream.Write(bytes, 0, bytes.Length);
				BtcSaveFormat.WriteVarint(memoryStream, (ulong)((long)((array != null) ? array.Length : 0)));
				bool flag8 = array != null;
				if (flag8)
				{
					memoryStream.Write(array, 0, array.Length);
				}
				BtcSaveFormat.WriteVarint(memoryStream, (ulong)((long)list.Count));
				byte[] array4 = memoryStream.ToArray();
				uint num2 = BtcSaveFormat.Crc32(array4, 0, array4.Length);
				string directoryName = Path.GetDirectoryName(Path.GetFullPath(outFile));
				bool flag9 = !string.IsNullOrEmpty(directoryName);
				if (flag9)
				{
					Directory.CreateDirectory(directoryName);
				}
				using (FileStream fileStream = new FileStream(text, FileMode.Create, FileAccess.Write, FileShare.None))
				{
					fileStream.Write(array4, 0, array4.Length);
					fileStream.WriteByte((byte)num2);
					fileStream.WriteByte((byte)(num2 >> 8));
					fileStream.WriteByte((byte)(num2 >> 16));
					fileStream.WriteByte((byte)(num2 >> 24));
					using (DeflateStream deflateStream = new DeflateStream(fileStream, CompressionMode.Compress, true))
					{
						for (int j = 0; j < list.Count; j++)
						{
							string text5 = list[j].Substring(text2.Length + 1).Replace('\\', '/');
							byte[] array5 = File.ReadAllBytes(list[j]);
							int num3 = 0;
							bool flag10 = text5.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && array5.Length != 0 && array5.Length <= 67108864;
							if (flag10)
							{
								byte[] array6 = BtcSaveFormat.JsonPack(array5);
								bool flag11 = array6 != null && array6.Length < array5.Length;
								if (flag11)
								{
									array5 = array6;
									num3 = 1;
								}
							}
							byte[] bytes2 = Encoding.UTF8.GetBytes(text5);
							BtcSaveFormat.WriteVarint(deflateStream, (ulong)((long)bytes2.Length));
							deflateStream.Write(bytes2, 0, bytes2.Length);
							deflateStream.WriteByte((byte)num3);
							BtcSaveFormat.WriteVarint(deflateStream, (ulong)((long)array5.Length));
							uint num4 = BtcSaveFormat.Crc32(array5, 0, array5.Length);
							deflateStream.WriteByte((byte)num4);
							deflateStream.WriteByte((byte)(num4 >> 8));
							deflateStream.WriteByte((byte)(num4 >> 16));
							deflateStream.WriteByte((byte)(num4 >> 24));
							deflateStream.Write(array5, 0, array5.Length);
						}
					}
				}
				bool flag12 = File.Exists(outFile);
				if (flag12)
				{
					File.Delete(outFile);
				}
				File.Move(text, outFile);
				result = true;
			}
		}
		catch (Exception ex)
		{
			err = ex.Message;
			try
			{
				bool flag13 = File.Exists(text);
				if (flag13)
				{
					File.Delete(text);
				}
			}
			catch (Exception)
			{
			}
			result = false;
		}
		return result;
	}

	public static bool Unpack(string file, string destFolder, byte[] keepThumb, out string err)
	{
		err = null;
		string text = destFolder.TrimEnd(new char[]
		{
			Path.DirectorySeparatorChar,
			Path.AltDirectorySeparatorChar
		}) + ".btcs_unpack";
		bool result;
		try
		{
			BtcSaveFormat.Info info;
			using (FileStream fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				bool flag = !BtcSaveFormat.ReadHeader(fileStream, out info, out err);
				if (flag)
				{
					return false;
				}
				bool flag2 = Directory.Exists(text);
				if (flag2)
				{
					Directory.Delete(text, true);
				}
				Directory.CreateDirectory(text);
				string fullPath = Path.GetFullPath(text);
				fileStream.Position = info.bodyOffset;
				using (DeflateStream deflateStream = new DeflateStream(fileStream, CompressionMode.Decompress, true))
				{
					for (int i = 0; i < info.fileCount; i++)
					{
						long num;
						bool flag3 = !BtcSaveFormat.ReadVarint(deflateStream, null, out num) || num <= 0L || num > 4096L;
						if (flag3)
						{
							err = "file is corrupted (entry " + i.ToString() + ")";
							BtcSaveFormat.Cleanup(text);
							return false;
						}
						byte[] array = new byte[(int)num];
						bool flag4 = BtcSaveFormat.ReadExact(deflateStream, array, array.Length) < array.Length;
						if (flag4)
						{
							err = "file is truncated";
							BtcSaveFormat.Cleanup(text);
							return false;
						}
						string @string = Encoding.UTF8.GetString(array);
						int num2 = deflateStream.ReadByte();
						long num3 = 0L;
						bool flag5 = num2 < 0 || num2 > 1 || !BtcSaveFormat.ReadVarint(deflateStream, null, out num3) || num3 < 0L || num3 > 1073741824L;
						if (flag5)
						{
							err = "file is corrupted (entry " + i.ToString() + ")";
							BtcSaveFormat.Cleanup(text);
							return false;
						}
						byte[] array2 = new byte[4];
						bool flag6 = BtcSaveFormat.ReadExact(deflateStream, array2, 4) < 4;
						if (flag6)
						{
							err = "file is truncated";
							BtcSaveFormat.Cleanup(text);
							return false;
						}
						uint num4 = (uint)((int)array2[0] | (int)array2[1] << 8 | (int)array2[2] << 16 | (int)array2[3] << 24);
						byte[] array3 = new byte[(int)num3];
						bool flag7 = BtcSaveFormat.ReadExact(deflateStream, array3, array3.Length) < array3.Length;
						if (flag7)
						{
							err = "file is truncated";
							BtcSaveFormat.Cleanup(text);
							return false;
						}
						bool flag8 = BtcSaveFormat.Crc32(array3, 0, array3.Length) != num4;
						if (flag8)
						{
							err = "checksum mismatch in " + @string;
							BtcSaveFormat.Cleanup(text);
							return false;
						}
						bool flag9 = num2 == 1;
						if (flag9)
						{
							array3 = BtcSaveFormat.JsonUnpack(array3);
							bool flag10 = array3 == null;
							if (flag10)
							{
								err = "bad packed json in " + @string;
								BtcSaveFormat.Cleanup(text);
								return false;
							}
						}
						string text2 = BtcSaveFormat.SafeTarget(fullPath, @string);
						bool flag11 = text2 == null;
						if (flag11)
						{
							err = "unsafe path in file: " + @string;
							BtcSaveFormat.Cleanup(text);
							return false;
						}
						string directoryName = Path.GetDirectoryName(text2);
						bool flag12 = !Directory.Exists(directoryName);
						if (flag12)
						{
							Directory.CreateDirectory(directoryName);
						}
						File.WriteAllBytes(text2, array3);
					}
				}
			}
			byte[] array4 = (info.thumbEmbedded && !info.thumbDownscaled) ? info.thumb : ((keepThumb != null) ? keepThumb : info.thumb);
			bool flag13 = array4 != null;
			if (flag13)
			{
				File.WriteAllBytes(Path.Combine(text, "thumbnail.png"), array4);
			}
			bool flag14 = Directory.Exists(destFolder);
			if (flag14)
			{
				Directory.Delete(destFolder, true);
			}
			Directory.Move(text, destFolder);
			result = true;
		}
		catch (Exception ex)
		{
			err = ex.Message;
			BtcSaveFormat.Cleanup(text);
			result = false;
		}
		return result;
	}

	public static bool ToNormal(string file, out string folder, out string err)
	{
		folder = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(file)), Path.GetFileNameWithoutExtension(file));
		byte[] keepThumb = null;
		try
		{
			string text = BtcSaveFormat.FindThumb(folder);
			bool flag = text != null;
			if (flag)
			{
				keepThumb = File.ReadAllBytes(text);
			}
		}
		catch (Exception)
		{
		}
		return BtcSaveFormat.Unpack(file, folder, keepThumb, out err);
	}

	public static string Cache(string file, out string err)
	{
		err = null;
		string result;
		try
		{
			FileInfo fileInfo = new FileInfo(file);
			string text = Path.Combine(Path.Combine(BtcPaths.Root, "cache"), string.Concat(new string[]
			{
				Path.GetFileNameWithoutExtension(file),
				"_",
				fileInfo.Length.ToString(),
				"_",
				fileInfo.LastWriteTimeUtc.Ticks.ToString("x")
			}));
			bool flag = Directory.Exists(text);
			if (flag)
			{
				result = text;
			}
			else
			{
				string path = Path.Combine(BtcPaths.Root, "cache");
				bool flag2 = Directory.Exists(path);
				if (flag2)
				{
					string value = Path.GetFileNameWithoutExtension(file) + "_";
					string[] directories = Directory.GetDirectories(path);
					for (int i = 0; i < directories.Length; i++)
					{
						bool flag3 = Path.GetFileName(directories[i]).StartsWith(value, StringComparison.Ordinal);
						if (flag3)
						{
							try
							{
								Directory.Delete(directories[i], true);
							}
							catch (Exception)
							{
							}
						}
					}
				}
				Directory.CreateDirectory(path);
				result = (BtcSaveFormat.Unpack(file, text, null, out err) ? text : null);
			}
		}
		catch (Exception ex)
		{
			err = ex.Message;
			result = null;
		}
		return result;
	}

	private static void Cleanup(string stage)
	{
		try
		{
			bool flag = Directory.Exists(stage);
			if (flag)
			{
				Directory.Delete(stage, true);
			}
		}
		catch (Exception)
		{
		}
	}

	private static string SafeTarget(string rootFull, string rel)
	{
		bool flag = string.IsNullOrEmpty(rel) || rel[0] == '/' || rel[0] == '\\' || rel.IndexOf(':') >= 0;
		string result;
		if (flag)
		{
			result = null;
		}
		else
		{
			string[] array = rel.Split(new char[]
			{
				'/'
			});
			for (int i = 0; i < array.Length; i++)
			{
				bool flag2 = array[i].Length == 0 || array[i] == "." || array[i] == "..";
				if (flag2)
				{
					return null;
				}
			}
			string fullPath = Path.GetFullPath(Path.Combine(rootFull, rel.Replace('/', Path.DirectorySeparatorChar)));
			bool flag3 = !fullPath.StartsWith(rootFull + Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal);
			if (flag3)
			{
				result = null;
			}
			else
			{
				result = fullPath;
			}
		}
		return result;
	}

	internal static bool Downscale(byte[] png, int f, out byte[] result)
	{
		result = null;
		int num;
		int num2;
		byte[] array;
		bool flag = !BtcPng.Decode(png, out num, out num2, out array);
		bool result2;
		if (flag)
		{
			result2 = false;
		}
		else
		{
			int num3 = Math.Max(1, num / f);
			int num4 = Math.Max(1, num2 / f);
			byte[] array2 = new byte[num3 * num4 * 4];
			bool hasAlpha = false;
			for (int i = 0; i < num4; i++)
			{
				int num5 = i * num2 / num4;
				int num6 = Math.Max(num5 + 1, (i + 1) * num2 / num4);
				for (int j = 0; j < num3; j++)
				{
					int num7 = j * num / num3;
					int num8 = Math.Max(num7 + 1, (j + 1) * num / num3);
					int num9 = 0;
					int num10 = 0;
					int num11 = 0;
					int num12 = 0;
					int num13 = 0;
					for (int k = num5; k < num6; k++)
					{
						int num14 = (k * num + num7) * 4;
						for (int l = num7; l < num8; l++)
						{
							num9 += (int)array[num14];
							num10 += (int)array[num14 + 1];
							num11 += (int)array[num14 + 2];
							num12 += (int)array[num14 + 3];
							num13++;
							num14 += 4;
						}
					}
					int num15 = (i * num3 + j) * 4;
					array2[num15] = (byte)((num9 + num13 / 2) / num13);
					array2[num15 + 1] = (byte)((num10 + num13 / 2) / num13);
					array2[num15 + 2] = (byte)((num11 + num13 / 2) / num13);
					byte b = (byte)((num12 + num13 / 2) / num13);
					array2[num15 + 3] = b;
					bool flag2 = b != byte.MaxValue;
					if (flag2)
					{
						hasAlpha = true;
					}
				}
			}
			result = BtcPng.Encode(num3, num4, array2, hasAlpha);
			result2 = true;
		}
		return result2;
	}

	private static byte[] JsonPack(byte[] utf8)
	{
		byte[] result;
		try
		{
			JToken jtoken;
			using (StringReader stringReader = new StringReader(Encoding.UTF8.GetString(utf8)))
			{
				using (JsonTextReader jsonTextReader = new JsonTextReader(stringReader))
				{
					jsonTextReader.DateParseHandling = DateParseHandling.None;
					jsonTextReader.FloatParseHandling = FloatParseHandling.Double;
					jtoken = JToken.ReadFrom(jsonTextReader);
				}
			}
			MemoryStream memoryStream = new MemoryStream();
			Dictionary<string, int> table = new Dictionary<string, int>();
			BtcSaveFormat.PackTok(memoryStream, jtoken, table, 0);
			byte[] array = memoryStream.ToArray();
			JToken jtoken2 = BtcSaveFormat.JsonUnpackTok(array);
			bool flag = jtoken2 == null || !JToken.DeepEquals(jtoken, jtoken2);
			if (flag)
			{
				result = null;
			}
			else
			{
				result = array;
			}
		}
		catch (Exception)
		{
			result = null;
		}
		return result;
	}

	private static void PackStr(Stream s, string v, Dictionary<string, int> table)
	{
		int num;
		bool flag = table.TryGetValue(v, out num);
		if (flag)
		{
			s.WriteByte(8);
			BtcSaveFormat.WriteVarint(s, (ulong)((long)num));
		}
		else
		{
			table[v] = table.Count;
			byte[] bytes = Encoding.UTF8.GetBytes(v);
			s.WriteByte(7);
			BtcSaveFormat.WriteVarint(s, (ulong)((long)bytes.Length));
			s.Write(bytes, 0, bytes.Length);
		}
	}

	private static void PackTok(Stream s, JToken t, Dictionary<string, int> table, int depth)
	{
		bool flag = depth > 256;
		if (flag)
		{
			throw new InvalidDataException("too deep");
		}
		switch (t.Type)
		{
		case JTokenType.Object:
		{
			JObject jobject = (JObject)t;
			s.WriteByte(10);
			BtcSaveFormat.WriteVarint(s, (ulong)((long)jobject.Count));
			foreach (JProperty jproperty in jobject.Properties())
			{
				BtcSaveFormat.PackStr(s, jproperty.Name, table);
				BtcSaveFormat.PackTok(s, jproperty.Value, table, depth + 1);
			}
			return;
		}
		case JTokenType.Array:
		{
			JArray jarray = (JArray)t;
			s.WriteByte(9);
			BtcSaveFormat.WriteVarint(s, (ulong)((long)jarray.Count));
			for (int i = 0; i < jarray.Count; i++)
			{
				BtcSaveFormat.PackTok(s, jarray[i], table, depth + 1);
			}
			return;
		}
		case JTokenType.Integer:
		{
			object value = ((JValue)t).Value;
			bool flag2 = !(value is long) && !(value is int);
			if (flag2)
			{
				throw new InvalidDataException("big int");
			}
			s.WriteByte(3);
			BtcSaveFormat.WriteVarint(s, BtcSaveFormat.ZigZag(Convert.ToInt64(value)));
			return;
		}
		case JTokenType.Float:
		{
			double d = (double)t;
			BtcSaveFormat.PackDouble(s, d);
			return;
		}
		case JTokenType.String:
			BtcSaveFormat.PackStr(s, (string)t, table);
			return;
		case JTokenType.Boolean:
			s.WriteByte((byte)(((bool)t) ? 2 : 1));
			return;
		case JTokenType.Null:
			s.WriteByte(0);
			return;
		}
		throw new InvalidDataException("unsupported token " + t.Type.ToString());
	}

	private static void PackDouble(Stream s, double d)
	{
		bool flag = BitConverter.DoubleToInt64Bits(d) == long.MinValue;
		bool flag2 = !flag && !double.IsNaN(d) && !double.IsInfinity(d) && Math.Abs(d) < 1000000000.0;
		if (flag2)
		{
			for (int i = 1; i <= 7; i++)
			{
				double num = Math.Round(d * BtcSaveFormat.Pow10[i]);
				bool flag3 = Math.Abs(num) < 9007199254740992.0 && num / BtcSaveFormat.Pow10[i] == d;
				if (flag3)
				{
					s.WriteByte(4);
					s.WriteByte((byte)i);
					BtcSaveFormat.WriteVarint(s, BtcSaveFormat.ZigZag((long)num));
					return;
				}
			}
		}
		float num2 = (float)d;
		bool flag4 = !flag && (double)num2 == d;
		if (flag4)
		{
			s.WriteByte(5);
			byte[] bytes = BitConverter.GetBytes(num2);
			s.Write(bytes, 0, 4);
		}
		else
		{
			s.WriteByte(6);
			byte[] bytes2 = BitConverter.GetBytes(d);
			s.Write(bytes2, 0, 8);
		}
	}

	private static byte[] JsonUnpack(byte[] packed)
	{
		byte[] result;
		try
		{
			JToken jtoken = BtcSaveFormat.JsonUnpackTok(packed);
			bool flag = jtoken == null;
			if (flag)
			{
				result = null;
			}
			else
			{
				StringBuilder stringBuilder = new StringBuilder(packed.Length * 2);
				using (StringWriter stringWriter = new StringWriter(stringBuilder))
				{
					using (JsonTextWriter jsonTextWriter = new JsonTextWriter(stringWriter))
					{
						jsonTextWriter.Formatting = Formatting.None;
						jtoken.WriteTo(jsonTextWriter, new JsonConverter[0]);
					}
				}
				result = new UTF8Encoding(false).GetBytes(stringBuilder.ToString());
			}
		}
		catch (Exception)
		{
			result = null;
		}
		return result;
	}

	private static JToken JsonUnpackTok(byte[] packed)
	{
		JToken result;
		try
		{
			BtcSaveFormat.Rd rd = new BtcSaveFormat.Rd();
			rd.b = packed;
			JToken jtoken = BtcSaveFormat.ReadTok(rd, 0);
			result = ((rd.p == packed.Length) ? jtoken : null);
		}
		catch (Exception)
		{
			result = null;
		}
		return result;
	}

	private static ulong RVar(BtcSaveFormat.Rd r)
	{
		ulong num = 0UL;
		int num2 = 0;
		for (;;)
		{
			bool flag = r.p >= r.b.Length || num2 > 63;
			if (flag)
			{
				break;
			}
			byte[] b = r.b;
			int p = r.p;
			r.p = p + 1;
			byte b2 = b[p];
			num |= (ulong)((ulong)((long)(b2 & 127)) << num2);
			bool flag2 = (b2 & 128) == 0;
			if (flag2)
			{
				return num;
			}
			num2 += 7;
		}
		throw new InvalidDataException("varint");
	}

	private static string RStr(BtcSaveFormat.Rd r, int tag)
	{
		bool flag = tag == 8;
		string result;
		if (flag)
		{
			ulong num = BtcSaveFormat.RVar(r);
			bool flag2 = num >= (ulong)((long)r.table.Count);
			if (flag2)
			{
				throw new InvalidDataException("str ref");
			}
			result = r.table[(int)num];
		}
		else
		{
			ulong num2 = BtcSaveFormat.RVar(r);
			bool flag3 = num2 > (ulong)((long)(r.b.Length - r.p));
			if (flag3)
			{
				throw new InvalidDataException("str len");
			}
			string @string = Encoding.UTF8.GetString(r.b, r.p, (int)num2);
			r.p += (int)num2;
			r.table.Add(@string);
			result = @string;
		}
		return result;
	}

	private static JToken ReadTok(BtcSaveFormat.Rd r, int depth)
	{
		bool flag = depth > 256 || r.p >= r.b.Length;
		if (flag)
		{
			throw new InvalidDataException("bad data");
		}
		byte[] b = r.b;
		int p = r.p;
		r.p = p + 1;
		int tag = b[p];
		JToken result;
		switch (tag)
		{
		case 0:
			result = JValue.CreateNull();
			break;
		case 1:
			result = new JValue(false);
			break;
		case 2:
			result = new JValue(true);
			break;
		case 3:
			result = new JValue(BtcSaveFormat.UnZigZag(BtcSaveFormat.RVar(r)));
			break;
		case 4:
		{
			bool flag2 = r.p >= r.b.Length;
			if (flag2)
			{
				throw new InvalidDataException("dec");
			}
			byte[] b2 = r.b;
			p = r.p;
			r.p = p + 1;
			int num = b2[p];
			bool flag3 = num < 1 || num > 7;
			if (flag3)
			{
				throw new InvalidDataException("dec scale");
			}
			long num2 = BtcSaveFormat.UnZigZag(BtcSaveFormat.RVar(r));
			result = new JValue((double)num2 / BtcSaveFormat.Pow10[num]);
			break;
		}
		case 5:
		{
			bool flag4 = r.p + 4 > r.b.Length;
			if (flag4)
			{
				throw new InvalidDataException("f32");
			}
			float num3 = BitConverter.ToSingle(r.b, r.p);
			r.p += 4;
			result = new JValue((double)num3);
			break;
		}
		case 6:
		{
			bool flag5 = r.p + 8 > r.b.Length;
			if (flag5)
			{
				throw new InvalidDataException("f64");
			}
			double value = BitConverter.ToDouble(r.b, r.p);
			r.p += 8;
			result = new JValue(value);
			break;
		}
		case 7:
		case 8:
			result = new JValue(BtcSaveFormat.RStr(r, tag));
			break;
		case 9:
		{
			ulong num4 = BtcSaveFormat.RVar(r);
			bool flag6 = num4 > (ulong)((long)(r.b.Length - r.p));
			if (flag6)
			{
				throw new InvalidDataException("array");
			}
			JArray jarray = new JArray();
			for (ulong num5 = 0UL; num5 < num4; num5 += 1UL)
			{
				jarray.Add(BtcSaveFormat.ReadTok(r, depth + 1));
			}
			result = jarray;
			break;
		}
		case 10:
		{
			ulong num6 = BtcSaveFormat.RVar(r);
			bool flag7 = num6 > (ulong)((long)(r.b.Length - r.p));
			if (flag7)
			{
				throw new InvalidDataException("object");
			}
			JObject jobject = new JObject();
			for (ulong num7 = 0UL; num7 < num6; num7 += 1UL)
			{
				bool flag8 = r.p >= r.b.Length;
				if (flag8)
				{
					throw new InvalidDataException("object");
				}
				byte[] b3 = r.b;
				p = r.p;
				r.p = p + 1;
				int num8 = b3[p];
				bool flag9 = num8 != 7 && num8 != 8;
				if (flag9)
				{
					throw new InvalidDataException("key");
				}
				string propertyName = BtcSaveFormat.RStr(r, num8);
				jobject[propertyName] = BtcSaveFormat.ReadTok(r, depth + 1);
			}
			result = jobject;
			break;
		}
		default:
			throw new InvalidDataException("tag " + tag.ToString());
		}
		return result;
	}

	private static ulong ZigZag(long v)
	{
		return (ulong)(v << 1 ^ v >> 63);
	}

	private static long UnZigZag(ulong v)
	{
		return (long)(v >> 1 ^ unchecked((ulong)(-(long)(v & 1UL))));
	}

	private static void WriteVarint(Stream s, ulong v)
	{
		while (v >= 128UL)
		{
			s.WriteByte((byte)(v | 128UL));
			v >>= 7;
		}
		s.WriteByte((byte)v);
	}

	private static bool ReadVarint(Stream s, Stream copy, out long value)
	{
		ulong num = 0UL;
		int i = 0;
		value = 0L;
		while (i <= 56)
		{
			int num2 = s.ReadByte();
			bool flag = num2 < 0;
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				bool flag2 = copy != null;
				if (flag2)
				{
					copy.WriteByte((byte)num2);
				}
				num |= (ulong)((ulong)((long)(num2 & 127)) << i);
				bool flag3 = (num2 & 128) == 0;
				if (!flag3)
				{
					i += 7;
					continue;
				}
				bool flag4 = num > 9223372036854775807UL;
				if (flag4)
				{
					result = false;
				}
				else
				{
					value = (long)num;
					result = true;
				}
			}
			return result;
		}
		return false;
	}

	private static int ReadExact(Stream s, byte[] buf, int count)
	{
		int i;
		int num;
		for (i = 0; i < count; i += num)
		{
			num = s.Read(buf, i, count - i);
			bool flag = num <= 0;
			if (flag)
			{
				break;
			}
		}
		return i;
	}

	private static uint Crc32(byte[] b, int off, int len)
	{
		bool flag = BtcSaveFormat.crcTable == null;
		if (flag)
		{
			uint[] array = new uint[256];
			for (uint num = 0U; num < 256U; num += 1U)
			{
				uint num2 = num;
				for (int i = 0; i < 8; i++)
				{
					num2 = (((num2 & 1U) != 0U) ? (3988292384U ^ num2 >> 1) : (num2 >> 1));
				}
				array[(int)num] = num2;
			}
			BtcSaveFormat.crcTable = array;
		}
		uint num3 = uint.MaxValue;
		for (int j = 0; j < len; j++)
		{
			num3 = (BtcSaveFormat.crcTable[(int)((num3 ^ (uint)b[off + j]) & 255U)] ^ num3 >> 8);
		}
		return num3 ^ uint.MaxValue;
	}

	public static bool IncludeThumb = true;

	/// <summary>Host sends the map to BTC clients as a .btcs instead of the vanilla lzip zip (BetterConnection ON only).</summary>
	public static bool SendBtcs = true;

	/// <summary>Master switch of the Share tab: off = the host uses the plain vanilla map transfer (zip with thumbnail).</summary>
	public static bool ShareOn = true;

	/// <summary>Host also sends thumbnail.png to clients (off by default: clients do not need it).</summary>
	public static bool SendThumb = false;

	public static bool IsBtcsMagic(byte[] h)
	{
		return h != null && h.Length >= 4 && h[0] == BtcSaveFormat.Magic[0] && h[1] == BtcSaveFormat.Magic[1] && h[2] == BtcSaveFormat.Magic[2] && h[3] == BtcSaveFormat.Magic[3];
	}

	/// <summary>Pack a map folder for the network (thumbnail optional, never touches the user's Share settings).</summary>
	public static bool PackForSend(string folder, string outFile, bool withThumb, out string err)
	{
		bool old = BtcSaveFormat.IncludeThumb;
		BtcSaveFormat.IncludeThumb = withThumb;
		try { return BtcSaveFormat.Pack(folder, outFile, true, out err); }
		finally { BtcSaveFormat.IncludeThumb = old; }
	}

	public static int ThumbFactor = 8;

	private static bool settingsLoaded;

	private const byte Ver = 1;

	private const int MaxEntry = 1073741824;

	private const int MaxJsonBytes = 67108864;

	private static readonly byte[] Magic = new byte[]
	{
		66,
		84,
		67,
		83
	};

	private static readonly double[] Pow10 = new double[]
	{
		1.0,
		10.0,
		100.0,
		1000.0,
		10000.0,
		100000.0,
		1000000.0,
		10000000.0
	};

	private static uint[] crcTable;

	public sealed class Info
	{
		public int version;

		public bool isMap;

		public bool thumbEmbedded;

		public bool thumbDownscaled;

		public int thumbFactor;

		public string name;

		public byte[] thumb;

		public int fileCount;

		public long bodyOffset;
	}

	private sealed class Rd
	{
		public byte[] b;

		public int p;

		public List<string> table = new List<string>();
	}
}
}
