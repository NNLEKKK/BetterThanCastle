#if DEV
using System.Text;
using UnityEngine;

namespace BetterThanCastle.Dev
{
    /// <summary>DEV multi-camera lab. "camdump": every Camera in the scene (depth, rect, mask, flags) and the component list of the main camera.</summary>
    internal static class BtcDevMultiCam
    {
        internal static void Dump()
        {
            Camera main = Camera.main;
            BtcLog.Info("CamDump", "main=" + (main == null ? "null" : main.name) + " screen=" + Screen.width + "x" + Screen.height);
            foreach (Camera c in Object.FindObjectsOfType<Camera>())
            {
                BtcLog.Info("CamDump", c.name + " en=" + c.enabled + " depth=" + c.depth + " rect=" + c.rect + " mask=" + c.cullingMask + " clear=" + c.clearFlags
                    + " fov=" + c.fieldOfView + " near=" + c.nearClipPlane + " far=" + c.farClipPlane + " tag=" + c.tag + " rt=" + (c.targetTexture != null) + " hdr=" + c.hdr + " path=" + c.actualRenderingPath + " ortho=" + c.orthographic + " aspect=" + c.aspect);
            }
            if (main == null) return;
            StringBuilder sb = new StringBuilder();
            foreach (Component k in main.GetComponents<Component>()) sb.Append(k == null ? "null" : k.GetType().Name).Append(", ");
            BtcLog.Info("CamDump", "main components: " + sb);
            foreach (Component k in main.GetComponents<Component>())
            {
                MonoBehaviour mb = k as MonoBehaviour;
                if (mb == null) continue;
                BtcLog.Info("CamDump", "comp " + k.GetType().FullName + " en=" + mb.enabled);
            }
            foreach (Brix.Game.Components.Skybox.SkyboxCameraDrawer d in Object.FindObjectsOfType<Brix.Game.Components.Skybox.SkyboxCameraDrawer>())
            {
                Camera sc = d.skyboxCamera;
                BtcLog.Info("CamDump", "skyDrawer manual=" + d.renderManually + " drawSky=" + d.drawSkybox + " drawScene=" + d.drawScene + " rev=" + d.reverseOrder + " skyCam=" + (sc == null ? "null" : sc.name + " en=" + sc.enabled + " rt=" + (sc.targetTexture == null ? "null" : sc.targetTexture.width + "x" + sc.targetTexture.height) + " rect=" + sc.rect));
            }
            foreach (Brix.Game.Components.Skybox.SkyboxConfigurator c in Object.FindObjectsOfType<Brix.Game.Components.Skybox.SkyboxConfigurator>())
                BtcLog.Info("CamDump", "skyConf scale=" + c._skyboxCameraScale + " moveScale=" + c._skyboxCameraMovementScale + " height=" + c._skyboxHauteur + " cam=" + (c._skyboxCamera == null ? "null" : c._skyboxCamera.name));
            for (int i = 0; i < main.transform.childCount; i++) BtcLog.Info("CamDump", "child " + main.transform.GetChild(i).name);
        }

        internal static void Cmd(string[] p)
        {
            string op = p.Length > 1 ? p[1] : "";
            float a = p.Length > 2 ? float.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture) : 0f;
            float b = p.Length > 3 ? float.Parse(p[3], System.Globalization.CultureInfo.InvariantCulture) : 0f;
            switch (op)
            {
                case "dump": Dump(); break;
                case "split": BtcMultiCam.Split(); break;
                case "collapse": BtcMultiCam.Collapse(true); break;
                case "toggle": BtcMultiCam.Toggle(); break;
                case "close": BtcMultiCam.CloseActive(); break;
                case "max": BtcMultiCam.ToggleMax(BtcMultiCam.Active); break;
                case "float": BtcMultiCam.ToggleFloat(BtcMultiCam.Active); break;
                case "dock": BtcMultiCam.DockAll(); break;
                case "sync": BtcMultiCam.SyncAll(); break;
                case "focus": BtcMultiCam.Focus((int)a); break;
                case "move": Brix.Input.CameraScript.Target += new Vector3(a, 0f, b); break;
                case "info": break;
                case "mouse": BtcMultiCam.UseMouseOverride = a >= 0f; BtcMultiCam.MouseOverride = new Vector3(a * Screen.width, b * Screen.height, 0f); break;
            }
            BtcLog.Info("MultiCam", "dev " + op + ": " + BtcMultiCam.Info() + " target=" + Brix.Input.CameraScript.Target);
        }
    }
}
#endif
