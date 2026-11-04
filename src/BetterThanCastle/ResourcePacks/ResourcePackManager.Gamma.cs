using System;
using UnityEngine;

// Auto gamma: the light multiplier that OnCamPreCull applies is chosen so that the scene's measured light level
// (ambient + directional lights, as the game sets them for the current time of day) always ends up at masterGamma x the
// brightest level seen on this map. Measured once per frame from a handful of lights: no allocations, no scene walks.
namespace BetterThanCastle
{
public partial class ResourcePackManager : MonoBehaviour
{
	private bool autoGamma;
	private float masterGamma = 1f;
	private float lumRef;
	private float autoMul = 1f;
	private int autoFrame = -1;
	private bool refDay;

	private bool LightIdle()
	{
		return !this.autoGamma && Mathf.Approximately(this.dayBright, 1f) && Mathf.Approximately(this.nightBright, 1f);
	}

	private float CurrentLightMul()
	{
		if (!this.autoGamma)
		{
			return Mathf.Lerp(this.dayBright, this.nightBright, ResourcePackManager.NightFactor());
		}
		int frame = Time.frameCount;
		if (frame == this.autoFrame)
		{
			return this.autoMul;
		}
		this.autoFrame = frame;
		float lum = RenderSettings.ambientLight.grayscale;
		Light[] lights = this.dirLights;
		if (lights != null)
		{
			for (int i = 0; i < lights.Length; i++)
			{
				Light l = lights[i];
				if (l != null && l.enabled)
				{
					lum += l.intensity * l.color.grayscale;
				}
			}
		}
		// reference level = the daytime level (slow average, so flashes/explosions cannot inflate it); before the first day
		// is seen the brightest level so far stands in
		if (this.lumRef <= 0f)
		{
			this.refDay = false;
		}
		if (ResourcePackManager.NightFactor() < 0.1f)
		{
			this.lumRef = this.refDay ? Mathf.Lerp(this.lumRef, lum, 0.02f) : lum;
			this.refDay = true;
		}
		else if (!this.refDay && lum > this.lumRef)
		{
			this.lumRef = lum;
		}
		float target = this.lumRef > 0.0001f ? this.masterGamma * this.lumRef / Mathf.Max(lum, this.lumRef * 0.4f) : this.masterGamma;
		target = Mathf.Clamp(target, 0.3f, 2.5f);
		float k = 1f - Mathf.Exp(-Mathf.Min(Time.unscaledDeltaTime, 0.1f) * 4f);
		this.autoMul = Mathf.Abs(this.autoMul - target) < 0.001f ? target : Mathf.Lerp(this.autoMul, target, k);
		return this.autoMul;
	}
}
}
