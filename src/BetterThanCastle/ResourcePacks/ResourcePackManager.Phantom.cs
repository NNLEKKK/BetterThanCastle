using System;
using UnityEngine;

// Phantoms (BtcPhantomLook): the folder of the selected resource pack, where an optional Phantom.png lives.
namespace BetterThanCastle
{
public partial class ResourcePackManager : MonoBehaviour
{
	/// <summary>Folder of the selected pack, or null when no pack is selected.</summary>
	internal static string CurrentPackDir
	{
		get
		{
			ResourcePackManager i = ResourcePackManager.instance;
			if (i == null || string.IsNullOrEmpty(i.currentPack)) return null;
			try { return System.IO.Path.Combine(ResourcePackManager.RootDir, i.currentPack); }
			catch (Exception) { return null; }
		}
	}
}
}
