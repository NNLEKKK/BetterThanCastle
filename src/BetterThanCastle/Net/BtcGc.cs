using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace BetterThanCastle
{
public static class BtcGc
{
	[DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
	private static extern IntPtr GetModuleHandle(string name);

	private static bool Resolve()
	{
		bool resolved = BtcGc._resolved;
		bool flag = resolved;
		bool result;
		if (flag)
		{
			result = (BtcGc._disable != null && BtcGc._enable != null);
		}
		else
		{
			BtcGc._resolved = true;
			result = (BtcGc.ResolveBoehm() || BtcGc.ResolveUnity());
		}
		return result;
	}

	public static void Pause(string why)
	{
		object @lock = BtcGc._lock;
		object obj = @lock;
		lock (obj)
		{
			bool flag = !BtcGc._resolved;
			bool flag2 = flag;
			if (flag2)
			{
				BtcGc.DumpEnv();
			}
			bool flag3 = !BtcGc.Enabled || BtcGc._paused || !BtcGc.Resolve();
			bool flag4 = !flag3;
			if (flag4)
			{
				try
				{
					BtcGc._disable();
				}
				catch (Exception ex)
				{
					BtcLoadBoost.Note("BtcGc: disable failed: " + ex.Message);
					return;
				}
				BtcGc._paused = true;
				BtcGc._since = Stopwatch.GetTimestamp();
				BtcGc._timer = new Timer(new TimerCallback(BtcGc.Watchdog), null, 250, 250);
				BtcLoadBoost.Note(string.Concat(new string[]
				{
					"BtcGc: GC paused (",
					why,
					") dont_gc=",
					BtcGc.DontGc(),
					BtcLoadBoost.GcInfo()
				}));
			}
		}
	}

	public static void Resume(string why)
	{
		object @lock = BtcGc._lock;
		object obj = @lock;
		lock (obj)
		{
			bool flag = !BtcGc._paused;
			bool flag2 = !flag;
			if (flag2)
			{
				BtcGc._paused = false;
				bool flag3 = BtcGc._timer != null;
				bool flag4 = flag3;
				if (flag4)
				{
					BtcGc._timer.Dispose();
					BtcGc._timer = null;
				}
				try
				{
					BtcGc._enable();
				}
				catch (Exception ex)
				{
					BtcLoadBoost.Note("BtcGc: enable failed: " + ex.Message);
				}
				BtcLoadBoost.Note(string.Concat(new string[]
				{
					"BtcGc: GC resumed (",
					why,
					") dont_gc=",
					BtcGc.DontGc(),
					BtcLoadBoost.GcInfo()
				}));
				bool collect = why != null && why.StartsWith("StartStep");
				if (collect)
				{
					// The heap ballooned while GC was paused; collect once now (loading screen still up) instead of mid-match.
					long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
					try { GC.Collect(); }
					catch (Exception ex2) { BtcLoadBoost.Note("BtcGc: post-load collect failed: " + ex2.Message); }
					long ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000L / System.Diagnostics.Stopwatch.Frequency;
					BtcLoadBoost.Note("BtcGc: post-load full collect " + ms + " ms" + BtcLoadBoost.GcInfo());
				}
			}
		}
	}

	private static void Watchdog(object state)
	{
		object @lock = BtcGc._lock;
		object obj = @lock;
		lock (obj)
		{
			bool flag = !BtcGc._paused;
			bool flag2 = !flag;
			if (flag2)
			{
				long num = GC.GetTotalMemory(false) / 1048576L;
				double num2 = (double)(Stopwatch.GetTimestamp() - BtcGc._since) / (double)Stopwatch.Frequency;
				bool flag3 = num > (long)BtcGc.MaxHeapMB;
				bool flag4 = flag3;
				if (flag4)
				{
					BtcGc.Resume("watchdog: heap limit");
				}
				else
				{
					bool flag5 = num2 > (double)BtcGc.MaxSeconds;
					bool flag6 = flag5;
					if (flag6)
					{
						BtcGc.Resume("watchdog: time limit");
					}
				}
			}
		}
	}

	private static bool ResolveUnity()
	{
		bool result;
		try
		{
			Type type = null;
			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				type = assembly.GetType("UnityEngine.Scripting.GarbageCollector");
				bool flag = type != null;
				bool flag2 = flag;
				if (flag2)
				{
					break;
				}
			}
			bool flag3 = type == null;
			bool flag4 = flag3;
			if (flag4)
			{
				BtcLoadBoost.Note("BtcGc: UnityEngine.Scripting.GarbageCollector not found (Unity too old)");
				result = false;
			}
			else
			{
				PropertyInfo prop = type.GetProperty("GCMode", BindingFlags.Static | BindingFlags.Public);
				Type nestedType = type.GetNestedType("Mode");
				bool flag5 = prop == null || nestedType == null || !prop.CanWrite;
				bool flag6 = flag5;
				if (flag6)
				{
					BtcLoadBoost.Note("BtcGc: GarbageCollector.GCMode not available");
					result = false;
				}
				else
				{
					object off = Enum.Parse(nestedType, "Disabled");
					object on = Enum.Parse(nestedType, "Enabled");
					BtcLoadBoost.Note("BtcGc: using Unity GCMode, current=" + prop.GetValue(null, null));
					BtcGc._disable = delegate()
					{
						prop.SetValue(null, off, null);
					};
					BtcGc._enable = delegate()
					{
						prop.SetValue(null, on, null);
					};
					result = true;
				}
			}
		}
		catch (Exception ex)
		{
			BtcLoadBoost.Note("BtcGc: Unity GCMode failed: " + ex.Message);
			result = false;
		}
		return result;
	}

	public static void DumpEnv()
	{
		try
		{
			string environmentVariable = Environment.GetEnvironmentVariable("GC_DONT_GC");
			BtcLoadBoost.Note("BtcGc: env GC_DONT_GC=" + ((environmentVariable == null) ? "<null>" : environmentVariable));
		}
		catch (Exception ex)
		{
			BtcLoadBoost.Note("BtcGc: env dump failed: " + ex.Message);
		}
	}

	private static bool Match(IntPtr b, int rva, byte[] sig)
	{
		for (int i = 0; i < sig.Length; i++)
		{
			bool flag = Marshal.ReadByte(b, rva + i) != sig[i];
			bool flag2 = flag;
			if (flag2)
			{
				return false;
			}
		}
		return true;
	}

	private static string DontGc()
	{
		string result;
		try
		{
			bool flag = !BtcGc._boehm || BtcGc._base == IntPtr.Zero;
			bool flag2 = flag;
			if (flag2)
			{
				result = "n/a";
			}
			else
			{
				result = Marshal.ReadInt32((IntPtr)(BtcGc._base.ToInt64() + 2518004L)).ToString();
			}
		}
		catch (Exception)
		{
			result = "n/a";
		}
		return result;
	}

	private static bool ResolveBoehm()
	{
		bool result;
		try
		{
			IntPtr moduleHandle = BtcGc.GetModuleHandle("mono");
			bool flag = moduleHandle == IntPtr.Zero;
			bool flag2 = flag;
			if (flag2)
			{
				result = false;
			}
			else
			{
				byte[] sig = new byte[]
				{
					72,
					131,
					236,
					40,
					72,
					141,
					13,
					97,
					116,
					22,
					0,
					byte.MaxValue,
					21,
					59,
					106,
					3,
					0,
					byte.MaxValue,
					5,
					105,
					243,
					16,
					0
				};
				byte[] sig2 = new byte[]
				{
					72,
					131,
					236,
					40,
					72,
					141,
					13,
					141,
					116,
					22,
					0,
					byte.MaxValue,
					21,
					103,
					106,
					3,
					0,
					byte.MaxValue,
					13,
					149,
					243,
					16,
					0
				};
				bool flag3 = !BtcGc.Match(moduleHandle, 1407092, sig) || !BtcGc.Match(moduleHandle, 1407048, sig2);
				bool flag4 = flag3;
				if (flag4)
				{
					BtcLoadBoost.Note("BtcGc: mono.dll build differs, Boehm GC_disable/enable not used");
					result = false;
				}
				else
				{
					long num = moduleHandle.ToInt64();
					BtcGc._base = moduleHandle;
					BtcGc._disable = (BtcGc.VoidFn)Marshal.GetDelegateForFunctionPointer((IntPtr)(num + 1407092L), typeof(BtcGc.VoidFn));
					BtcGc._enable = (BtcGc.VoidFn)Marshal.GetDelegateForFunctionPointer((IntPtr)(num + 1407048L), typeof(BtcGc.VoidFn));
					BtcGc._boehm = true;
					BtcLoadBoost.Note("BtcGc: using Boehm GC_disable/GC_enable, GC_dont_gc=" + BtcGc.DontGc());
					result = true;
				}
			}
		}
		catch (Exception ex)
		{
			BtcLoadBoost.Note("BtcGc: boehm resolve failed: " + ex.Message);
			result = false;
		}
		return result;
	}

	public static bool Enabled = true;

	public static int MaxHeapMB = 6000;

	public static int MaxSeconds = 150;

	private static BtcGc.VoidFn _disable;

	private static BtcGc.VoidFn _enable;

	private static bool _resolved;

	private static bool _paused;

	private static long _since;

	private static Timer _timer;

	private static readonly object _lock = new object();

	private const int RvaDisable = 1407092;

	private const int RvaEnable = 1407048;

	private const int RvaDontGc = 2518004;

	private static bool _boehm;

	private static IntPtr _base;

	private delegate void VoidFn();
}
}
