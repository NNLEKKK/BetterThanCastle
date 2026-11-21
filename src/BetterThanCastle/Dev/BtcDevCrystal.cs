#if DEV
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace BetterThanCastle.Dev
{
    /// <summary>DEV: "crystaldump" -> BTClogs/crystal-dump.txt. For the first active CrystalVisuals: every Renderer/Light/ParticleSystem below it and,
    /// for CrystalColor 0..5 and CreepColor 0..5 (separately, animator stepped), the colour properties found in each renderer's MaterialPropertyBlock and material.</summary>
    internal static class BtcDevCrystal
    {
        private static readonly string[] Props = { "_Color", "_Color2", "_SecondaryColor", "_GlowColor", "_LightColor", "_EmissionColor", "_EmissionColor2",
            "_FalloffColor", "_TintColor", "_RimColor", "_MainColor", "_DiffuseColor", "_ReflectColor", "_SpecColor", "_horizonColor", "_skyColor", "_sunColor" };

        internal static void Dump()
        {
            StringBuilder sb = new StringBuilder(8192);
            try
            {
                CrystalVisuals[] all = UnityEngine.Object.FindObjectsOfType<CrystalVisuals>();
                sb.Append("crystals=").Append(all.Length).Append('\n');
                if (all.Length == 0) { File.WriteAllText(System.IO.Path.Combine(BtcDevPaths.LogDir, "crystal-dump.txt"), sb.ToString()); return; }
                CrystalVisuals cv = all[0]; foreach (CrystalVisuals q in all) if (q.DriverGameObject != null) { cv = q; break; }
                Brix.Game.Components.AffiliationComponent af = cv.GetComponentInParent<Brix.Game.Components.AffiliationComponent>(); Transform croot = af != null ? af.transform : (cv.transform.parent != null ? cv.transform.parent : cv.transform);
                Animator an = cv.GetComponent<Animator>();
                sb.Append("go=").Append(cv.name).Append(" animator=").Append(an != null).Append(" ctrl=").Append(an != null && an.runtimeAnimatorController != null ? an.runtimeAnimatorController.name : "-").Append('\n');
                if (an != null && an.runtimeAnimatorController != null)
                    foreach (AnimationClip c in an.runtimeAnimatorController.animationClips) sb.Append("clip ").Append(c.name).Append(" len=").Append(c.length).Append('\n');
                Renderer[] rs = croot.GetComponentsInChildren<Renderer>(true);
                // hierarchy of the crystal root
                sb.Append("root=").Append(croot.name).Append(" parentOf=").Append(Pth(croot)).Append('\n');
                foreach (Renderer r in rs)
                {
                    sb.Append("R ").Append(Pth(r.transform)).Append(" ").Append(r.GetType().Name).Append(" en=").Append(r.enabled);
                    Material[] ms = r.sharedMaterials;
                    for (int i = 0; i < ms.Length; i++) sb.Append(" mat=").Append(ms[i] != null ? ms[i].name + "/" + ms[i].shader.name : "null");
                    ParticleSystem ps = r.GetComponent<ParticleSystem>();
                    if (ps != null) sb.Append(" PS startColor=").Append(ps.startColor);
                    sb.Append('\n');
                }
                foreach (Light l in croot.GetComponentsInChildren<Light>(true)) sb.Append("L ").Append(Pth(l.transform)).Append(" color=").Append(l.color).Append(" int=").Append(l.intensity).Append('\n');
                foreach (var sh in croot.GetComponentsInChildren<Brix.Rendering.ShareMaterialPropertyBlock>(true))
                    sb.Append("SHARE src=").Append(Pth(sh.sourceRenderer.transform)).Append(" targets=").Append(sh.targetRenderers != null ? sh.targetRenderers.Count : 0).Append('\n');
                if (an != null)
                {
                    int c0 = (int)cv.CrystalColor, k0 = cv.CreepColor_Raw();
                    for (int pass = 0; pass < 2; pass++)
                        for (int k = 0; k < 6; k++)
                        {
                            if (pass == 0) { an.SetInteger("Color", k); an.SetInteger("Creep Color", 0); }
                            else { an.SetInteger("Color", 0); an.SetInteger("Creep Color", k); an.SetFloat("Creep", 1f); }
                            for (int s = 0; s < 4; s++) an.Update(0.6f);
                            sb.Append(pass == 0 ? "== Color=" : "== Creep Color=").Append(k).Append('\n');
                            Snapshot(sb, rs, croot);
                        }
                    an.SetInteger("Color", c0); an.SetInteger("Creep Color", k0); an.SetFloat("Creep", 0f);
                }
            }
            catch (Exception e) { sb.Append("EXC ").Append(e).Append('\n'); }
            File.WriteAllText(System.IO.Path.Combine(BtcDevPaths.LogDir, "crystal-dump.txt"), sb.ToString());
            BtcLog.Info("Dev", "crystaldump written");
        }

        /// <summary>"crystaltint Cyan|off [creep]": force the owner (or creep) tint on every crystal with a driver.</summary>
        internal static void Tint(string[] p)
        {
            string n = p.Length > 1 ? p[1] : "off";
            bool creep = p.Length > 2 && p[2] == "creep";
            bool on = n != "off";
            Color c = n == "Rainbow" ? Color.black : n == "Cyan" ? new Color(0f, 0.9f, 0.93f) : n == "Black" ? new Color(0.11f, 0.11f, 0.125f) : new Color(1f, 0.41f, 0.7f);
            int k = 0;
            foreach (CrystalVisuals cv in UnityEngine.Object.FindObjectsOfType<CrystalVisuals>())
            {
                if (cv.DriverGameObject == null) continue;
                BtcCrystalTint.Force(cv, on, c, !creep);
                k++;
            }
            BtcLog.Info("Dev", "crystaltint " + n + (creep ? " creep" : " owner") + " crystals=" + k);
        }

        /// <summary>"crystalread": log animated PB colours of the first driver crystal (sources + one copy target) and tint stats.</summary>
        internal static void Read()
        {
            foreach (CrystalVisuals cv in UnityEngine.Object.FindObjectsOfType<CrystalVisuals>())
            {
                if (cv.DriverGameObject == null) continue;
                MaterialPropertyBlock pb = new MaterialPropertyBlock();
                BtcCrystalTint t = BtcCrystalTint.Find(cv);
                StringBuilder sb = new StringBuilder("crystalread ").Append(cv.name).Append(" color=").Append(cv.CrystalColor).Append(" tint=").Append(t != null ? t.Applied.ToString() : "none");
                foreach (Brix.Rendering.ShareMaterialPropertyBlock sh in cv.GetComponentsInChildren<Brix.Rendering.ShareMaterialPropertyBlock>(true))
                {
                    sh.sourceRenderer.GetPropertyBlock(pb);
                    sb.Append(" | src ").Append(sh.sourceRenderer.name).Append(" glow=").Append(pb.GetVector("_GlowColor").ToString("F2"));
                    if (sh.targetRenderers != null && sh.targetRenderers.Count > 0)
                    {
                        sh.targetRenderers[sh.targetRenderers.Count - 1].GetPropertyBlock(pb);
                        sb.Append(" tgt glow=").Append(pb.GetVector("_GlowColor").ToString("F2"));
                    }
                }
                BtcLog.Info("Dev", sb.ToString());
                return;
            }
        }

        private static int CreepColor_Raw(this CrystalVisuals cv) { return (int)cv.CreepColor; }

        private static void Snapshot(StringBuilder sb, Renderer[] rs, Transform root)
        {
            MaterialPropertyBlock pb = new MaterialPropertyBlock();
            foreach (Renderer r in rs)
            {
                r.GetPropertyBlock(pb);
                Material m = r.sharedMaterial;
                StringBuilder line = new StringBuilder();
                for (int i = 0; i < Props.Length; i++)
                {
                    Vector4 v = pb.GetVector(Props[i]);
                    if (v != Vector4.zero) line.Append(' ').Append(Props[i]).Append("=pb").Append(v.ToString("F2"));
                    else if (m != null && m.HasProperty(Props[i])) line.Append(' ').Append(Props[i]).Append("=mat").Append(((Vector4)m.GetColor(Props[i])).ToString("F2"));
                }
                if (m != null && m.HasProperty("_Progress")) line.Append(" _Progress=").Append(m.GetFloat("_Progress")).Append("/pb").Append(pb.GetFloat("_Progress"));
                if (line.Length > 0) sb.Append("  ").Append(Pth(r.transform)).Append(line).Append('\n');
            }
            foreach (Light l in root.GetComponentsInChildren<Light>(true)) sb.Append("  L ").Append(Pth(l.transform)).Append(" color=").Append(l.color).Append('\n');
            foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true)) sb.Append("  PS ").Append(Pth(ps.transform)).Append(" startColor=").Append(ps.startColor).Append('\n');
        }

        private static string Pth(Transform t)
        {
            StringBuilder s = new StringBuilder(t.name);
            for (Transform p = t.parent; p != null; p = p.parent) s.Insert(0, p.name + "/");
            return s.ToString();
        }
    }
}
#endif
