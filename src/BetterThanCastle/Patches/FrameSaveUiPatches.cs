using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Brix.Engine;
using Brix.External.Signals;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.Semantique;
using Brix.Input;
using Brix.NewUI.OverheadDisplay;
using Brix.NewUI.UserSettings_;
using HarmonyLib;
using Rewired;
using UnityEngine;

namespace BetterThanCastle
{
    // Frame savers for UI / overhead / sound / input work the game repeated every frame for the same answer.
    // Ported from Castle Story Plus by Tricky12321 (GPLv3, CastleStoryPlus/Performance). Same results, same signals.
    // Every class applies only when Performance/FrameSavers was on at start (Prepare).

    /// Camera position and the overhead distance limit, read once per frame (shared by several savers).
    internal static class FrameSaveCam
    {
        private static int _frame = -1;
        private static bool _have;
        private static Vector3 _position;
        private static Vector3 _forward;
        private static float _limit;

        private static void Refresh()
        {
            int frame = Time.frameCount;
            if (frame == _frame) return;
            _frame = frame;
            Camera camera = Camera.main;
            _have = camera != null;
            if (_have)
            {
                Transform t = camera.transform;
                _position = t.position;
                _forward = t.forward;
                float h = (float)Screen.height / 5f;
                _limit = h * h;
            }
        }

        internal static bool Get(out Vector3 position, out Vector3 forward, out float limit)
        {
            Refresh();
            position = _position; forward = _forward; limit = _limit;
            return _have;
        }
    }

    // ---------------------------------------------------------------- BehindCamera.cs
    /// Game: every overhead icon's BehindCameraDetector looked up Camera.main, its transform, position and forward
    /// every frame (some 330 icons). Saver: camera position/forward read once per frame; angle > 90 = negative dot.
    /// Without a main camera the game's own check runs. Ported from Castle Story Plus (Performance/BehindCamera.cs).
    [HarmonyPatch(typeof(BehindCameraDetector), "BrixUpdate")]
    internal static class FrameSaveBehindCamera
    {
        static bool Prepare() { return BtcFrameSave.On; }

        static bool Prefix(BehindCameraDetector __instance, ref bool ___wasBehind, ref bool ___isBehind,
            Signal ___wentBehindCamera, Signal ___cameBackIntoCamera)
        {
            try
            {
                Vector3 pos, fwd; float lim;
                if (!FrameSaveCam.Get(out pos, out fwd, out lim)) return true;
                Transform pivot = __instance.pivot;
                bool behind = pivot != null && Vector3.Dot(pivot.position - pos, fwd) < 0f;
                bool was = ___isBehind;
                ___wasBehind = was;
                ___isBehind = behind;
                if (!was && behind) ___wentBehindCamera.Invoke();
                else if (was && !behind) ___cameBackIntoCamera.Invoke();
                return false;
            }
            catch (Exception e) { BtcLog.Error("FrameSave", "BehindCamera: " + e); return true; }
        }
    }

    // ---------------------------------------------------------------- OverheadDistance.cs
    /// Game: every overhead icon checked every frame whether the camera is close enough (Camera.main, transform,
    /// Screen.height per icon). Saver: camera position and limit read once per frame; icon shown/hidden only when the
    /// answer changed, as before. No main camera / no pivot: the game's own check. Ported from Castle Story Plus
    /// (Performance/OverheadDistance.cs).
    [HarmonyPatch(typeof(OverheadDisplay), "CheckCloseEnough")]
    internal static class FrameSaveOverheadDistance
    {
        static bool Prepare() { return BtcFrameSave.On; }

        static bool Prefix(OverheadDisplay __instance, ref bool ___IsCloseEnough)
        {
            try
            {
                Transform pivot = __instance.pivot;
                Vector3 pos, fwd; float lim;
                if (pivot == null || !FrameSaveCam.Get(out pos, out fwd, out lim)) return true;
                bool close = (pos - pivot.position).sqrMagnitude < lim;
                if (close != ___IsCloseEnough)
                {
                    ___IsCloseEnough = close;
                    __instance.ShowOrHide();
                }
                return false;
            }
            catch (Exception e) { BtcLog.Error("FrameSave", "OverheadDistance: " + e); return true; }
        }
    }

    // ---------------------------------------------------------------- OverheadTestComponents.cs
    /// Game: the developers' test components TestContainer / TestOrderQueue sit on every overhead icon and their
    /// Update (inspector check boxes only, nothing sets them) ran every frame. Saver: each switches itself off on its
    /// first update. Ported from Castle Story Plus (Performance/OverheadTestComponents.cs).
    [HarmonyPatch(typeof(TestContainer), "Update")]
    internal static class FrameSaveTestContainer
    {
        static bool Prepare() { return BtcFrameSave.On; }
        static bool Prefix(TestContainer __instance) { __instance.enabled = false; return false; }
    }

    [HarmonyPatch(typeof(TestOrderQueue), "Update")]
    internal static class FrameSaveTestOrderQueue
    {
        static bool Prepare() { return BtcFrameSave.On; }
        static bool Prefix(TestOrderQueue __instance) { __instance.enabled = false; return false; }
    }

    // ---------------------------------------------------------------- ScreenPlanes.cs
    /// Game: every ScreenSignaler (beacons etc., some 40) recomputed the camera frustum planes each frame. Saver: the
    /// planes live in one shared array, so they are worked out once per frame and camera. Multi-camera (one rig moved
    /// between views in a frame) always runs the game's own code. Ported from Castle Story Plus
    /// (Performance/ScreenPlanes.cs).
    [HarmonyPatch(typeof(ScreenSignaler), "GetPlane")]
    internal static class FrameSaveScreenPlanes
    {
        private static int _frame = -1;
        private static int _camera;

        static bool Prepare() { return BtcFrameSave.On; }

        static bool Prefix(Camera cam)
        {
            try
            {
                if (cam == null || BtcMultiCam.On) { _frame = -1; return true; }
                int frame = Time.frameCount;
                int id = cam.GetInstanceID();
                if (frame == _frame && id == _camera) return false;
                _frame = frame;
                _camera = id;
                return true;
            }
            catch (Exception e) { BtcLog.Error("FrameSave", "ScreenPlanes: " + e); return true; }
        }
    }

    // ---------------------------------------------------------------- SoundObjectPositions.cs
    /// Game: every sound object (AkGameObj, some 300) told Wwise where it is every frame, reading its transform up to
    /// a dozen times. Saver: position and rotation read once, forward/up from the rotation; Wwise gets the same values,
    /// only on change. An object still for 8 frames is looked at every 4th frame (at most 3 frames late when it moves).
    /// Ported from Castle Story Plus (Performance/SoundObjectPositions.cs).
    [HarmonyPatch(typeof(AkGameObj), "Update")]
    internal static class FrameSaveSoundObjectPositions
    {
        private const int StillFrames = 8;
        private const int RestFrames = 4;
        private static readonly Dictionary<int, int> Still = new Dictionary<int, int>();

        static bool Prepare() { return BtcFrameSave.On; }

        static bool Prefix(AkGameObj __instance, bool ___isStaticObject, AkGameObjEnvironmentData ___m_envData,
            AkGameObjPositionData ___m_posData)
        {
            try
            {
                int id = __instance.GetInstanceID();
                int still;
                Still.TryGetValue(id, out still);
                if (still >= StillFrames && ((Time.frameCount + id) & (RestFrames - 1)) != 0) return false;
                Transform transform = __instance.transform;
                Vector3 position = transform.position;
                Quaternion rotation = transform.rotation;
                if (__instance.isEnvironmentAware && ___m_envData != null)
                    ___m_envData.UpdateAuxSend(__instance.gameObject, position);
                AkGameObjPositionData data = ___m_posData;
                if (___isStaticObject || data == null) return false;
                if (__instance.m_positionOffsetData != null)
                    position += rotation * __instance.m_positionOffsetData.positionOffset;
                Vector3 forward = rotation * Vector3.forward;
                Vector3 up = rotation * Vector3.up;
                if (data.position == position && data.forward == forward && data.up == up)
                {
                    if (still < StillFrames) Still[id] = still + 1;
                    return false;
                }
                if (still != 0) Still[id] = 0;
                data.position = position;
                data.forward = forward;
                data.up = up;
                AkSoundEngine.SetObjectPosition(__instance.gameObject, position.x, position.y, position.z,
                    forward.x, forward.y, forward.z, up.x, up.y, up.z);
                // Stale ids of destroyed objects: forgetting them only makes objects look "moving" for 8 frames.
                if (Still.Count > 20000) Still.Clear();
                return false;
            }
            catch (Exception e) { BtcLog.Error("FrameSave", "SoundObjectPositions: " + e); return true; }
        }
    }

    // ---------------------------------------------------------------- InputActionIds.cs
    /// Game: input actions are asked from Rewired by name every frame in some 60 places; each call hashes the string.
    /// Saver: the name is turned into the action id once (Rewired's own mapping) and the id overload is called. An
    /// unknown name goes the old way. Ported from Castle Story Plus (Performance/InputActionIds.cs).
    internal static class FrameSaveInputIds
    {
        private static readonly Dictionary<string, int> Ids = new Dictionary<string, int>(StringComparer.Ordinal);

        internal static int IdOf(string name)
        {
            if (name == null) return -1;
            int id;
            if (Ids.TryGetValue(name, out id)) return id;
            if (!ReInput.isReady) return -1;
            id = ReInput.mapping.GetActionId(name);
            Ids[name] = id;
            return id;
        }
    }

    [HarmonyPatch(typeof(Player), "GetButton", new Type[] { typeof(string) })]
    internal static class FrameSaveGetButton
    {
        static bool Prepare() { return BtcFrameSave.On; }
        static bool Prefix(Player __instance, string actionName, ref bool __result)
        {
            try { int id = FrameSaveInputIds.IdOf(actionName); if (id < 0) return true; __result = __instance.GetButton(id); return false; }
            catch (Exception e) { BtcLog.Error("FrameSave", "GetButton: " + e); return true; }
        }
    }

    [HarmonyPatch(typeof(Player), "GetButtonDown", new Type[] { typeof(string) })]
    internal static class FrameSaveGetButtonDown
    {
        static bool Prepare() { return BtcFrameSave.On; }
        static bool Prefix(Player __instance, string actionName, ref bool __result)
        {
            try { int id = FrameSaveInputIds.IdOf(actionName); if (id < 0) return true; __result = __instance.GetButtonDown(id); return false; }
            catch (Exception e) { BtcLog.Error("FrameSave", "GetButtonDown: " + e); return true; }
        }
    }

    [HarmonyPatch(typeof(Player), "GetButtonUp", new Type[] { typeof(string) })]
    internal static class FrameSaveGetButtonUp
    {
        static bool Prepare() { return BtcFrameSave.On; }
        static bool Prefix(Player __instance, string actionName, ref bool __result)
        {
            try { int id = FrameSaveInputIds.IdOf(actionName); if (id < 0) return true; __result = __instance.GetButtonUp(id); return false; }
            catch (Exception e) { BtcLog.Error("FrameSave", "GetButtonUp: " + e); return true; }
        }
    }

    [HarmonyPatch(typeof(Player), "GetNegativeButton", new Type[] { typeof(string) })]
    internal static class FrameSaveGetNegativeButton
    {
        static bool Prepare() { return BtcFrameSave.On; }
        static bool Prefix(Player __instance, string actionName, ref bool __result)
        {
            try { int id = FrameSaveInputIds.IdOf(actionName); if (id < 0) return true; __result = __instance.GetNegativeButton(id); return false; }
            catch (Exception e) { BtcLog.Error("FrameSave", "GetNegativeButton: " + e); return true; }
        }
    }

    [HarmonyPatch(typeof(Player), "GetNegativeButtonDown", new Type[] { typeof(string) })]
    internal static class FrameSaveGetNegativeButtonDown
    {
        static bool Prepare() { return BtcFrameSave.On; }
        static bool Prefix(Player __instance, string actionName, ref bool __result)
        {
            try { int id = FrameSaveInputIds.IdOf(actionName); if (id < 0) return true; __result = __instance.GetNegativeButtonDown(id); return false; }
            catch (Exception e) { BtcLog.Error("FrameSave", "GetNegativeButtonDown: " + e); return true; }
        }
    }

    [HarmonyPatch(typeof(Player), "GetButtonTimePressed", new Type[] { typeof(string) })]
    internal static class FrameSaveGetButtonTimePressed
    {
        static bool Prepare() { return BtcFrameSave.On; }
        static bool Prefix(Player __instance, string actionName, ref float __result)
        {
            try { int id = FrameSaveInputIds.IdOf(actionName); if (id < 0) return true; __result = __instance.GetButtonTimePressed(id); return false; }
            catch (Exception e) { BtcLog.Error("FrameSave", "GetButtonTimePressed: " + e); return true; }
        }
    }

    [HarmonyPatch(typeof(Player), "GetAxis", new Type[] { typeof(string) })]
    internal static class FrameSaveGetAxis
    {
        static bool Prepare() { return BtcFrameSave.On; }
        static bool Prefix(Player __instance, string actionName, ref float __result)
        {
            try { int id = FrameSaveInputIds.IdOf(actionName); if (id < 0) return true; __result = __instance.GetAxis(id); return false; }
            catch (Exception e) { BtcLog.Error("FrameSave", "GetAxis: " + e); return true; }
        }
    }

    // ---------------------------------------------------------------- PickingRaycasts.cs
    /// Game: while Picking.SelectActivePickers runs, each picker casts the mouse ray (Physics.Raycast up to 1000 units)
    /// into the world, many with the same ray/length/layers. Saver: a raycast identical to an earlier one in that pass
    /// reuses its result; a picker that takes up a held object (can put a ghost into the world) restarts the sharing.
    /// Ported from Castle Story Plus (Performance/PickingRaycasts.cs).
    [HarmonyPatch(typeof(Picking), "SelectActivePickers")]
    internal static class FrameSavePickingRaycasts
    {
        private struct Cast
        {
            public Ray Ray; public float Distance; public int Mask; public QueryTriggerInteraction Triggers;
            public bool Hit; public RaycastHit Result;
        }

        private const int MostCasts = 16;
        private static readonly List<Cast> Casts = new List<Cast>(MostCasts);
        private static bool _sharing;

        static bool Prepare() { return BtcFrameSave.On; }
        static void Prefix() { Casts.Clear(); _sharing = true; }
        static void Finalizer() { _sharing = false; Casts.Clear(); }
        internal static void Forget() { Casts.Clear(); }

        /// Takes the place of Physics.Raycast(ray, out hit, distance, mask, triggers) in VoxelRaycastHit.PlaceAtRaycast.
        internal static bool Raycast(Ray ray, out RaycastHit hit, float distance, int mask, QueryTriggerInteraction triggers)
        {
            if (!_sharing) return Physics.Raycast(ray, out hit, distance, mask, triggers);
            for (int i = 0; i < Casts.Count; i++)
            {
                Cast c = Casts[i];
                if (c.Mask == mask && c.Distance == distance && c.Triggers == triggers
                    && c.Ray.origin.x == ray.origin.x && c.Ray.origin.y == ray.origin.y && c.Ray.origin.z == ray.origin.z
                    && c.Ray.direction.x == ray.direction.x && c.Ray.direction.y == ray.direction.y && c.Ray.direction.z == ray.direction.z)
                {
                    hit = c.Result;
                    return c.Hit;
                }
            }
            bool result = Physics.Raycast(ray, out hit, distance, mask, triggers);
            if (Casts.Count < MostCasts)
                Casts.Add(new Cast { Ray = ray, Distance = distance, Mask = mask, Triggers = triggers, Hit = result, Result = hit });
            return result;
        }
    }

    [HarmonyPatch(typeof(VoxelRaycastHit), "PlaceAtRaycast")]
    internal static class FrameSavePickingCastPatch
    {
        static bool Prepare() { return BtcFrameSave.On; }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo raycast = typeof(Physics).GetMethod("Raycast",
                new Type[] { typeof(Ray), typeof(RaycastHit).MakeByRefType(), typeof(float), typeof(int), typeof(QueryTriggerInteraction) });
            MethodInfo shared = typeof(FrameSavePickingRaycasts).GetMethod("Raycast", BindingFlags.Static | BindingFlags.NonPublic);
            List<CodeInstruction> list = new List<CodeInstruction>(instructions);
            int replaced = 0;
            if (raycast != null && shared != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    MethodInfo m = list[i].operand as MethodInfo;
                    if (m != null && m == raycast)
                    {
                        list[i].opcode = OpCodes.Call;
                        list[i].operand = shared;
                        replaced++;
                    }
                }
            }
            if (replaced != 1)
            {
                BtcLog.Error("FrameSave", "PickingRaycasts: found " + replaced + " raycasts in VoxelRaycastHit.PlaceAtRaycast instead of 1, left unchanged");
                return instructions;
            }
            return list;
        }
    }

    /// Taking up a held object can put something new in the world: later pickers cast their own rays again.
    [HarmonyPatch(typeof(Picker), "PlaceAtRaycast")]
    internal static class FrameSavePickingHoldPatch
    {
        static bool Prepare() { return BtcFrameSave.On; }
        static void Prefix(Picker __instance, out bool __state) { __state = __instance.HeldKey != null; }
        static void Postfix(bool __state) { if (__state) FrameSavePickingRaycasts.Forget(); }
    }

    // ---------------------------------------------------------------- NatureLodRest.cs
    /// Game: every frame the game goes over megavoxels and recomputes each plant's level of detail against the camera
    /// (MegaVoxel.CalculateNatureLod) even with the camera standing still. Saver: a megavoxel whose last pass saw the
    /// camera within half a block is skipped for at most 2 s (new plants get detail within that time).
    /// Ported from Castle Story Plus (Performance/NatureLodRest.cs).
    [HarmonyPatch(typeof(MegaVoxel), "CalculateNatureLod")]
    internal static class FrameSaveNatureLodRest
    {
        private const float MovedSqr = 0.25f;
        private const float RestSeconds = 2f;

        private struct Pass { public Vector3 Camera; public float Time; }

        private static readonly Dictionary<MegaVoxel, Pass> Passes = new Dictionary<MegaVoxel, Pass>();

        static bool Prepare() { return BtcFrameSave.On; }

        internal static void Clear() { Passes.Clear(); }

        static bool Prefix(MegaVoxel __instance, Transform ___CamTransform)
        {
            try
            {
                if (!__instance.HasNature || ___CamTransform == null) return true;
                Vector3 cam = ___CamTransform.position;
                float now = Time.time;
                Pass last;
                if (Passes.TryGetValue(__instance, out last) && (last.Camera - cam).sqrMagnitude < MovedSqr && now - last.Time < RestSeconds)
                    return false;
                Pass p; p.Camera = cam; p.Time = now;
                Passes[__instance] = p;
                return true;
            }
            catch (Exception e) { BtcLog.Error("FrameSave", "NatureLodRest: " + e); return true; }
        }
    }

    /// Megavoxels are rebuilt per map: forget the old ones.
    [HarmonyPatch(typeof(MegaVoxel), "StaticDestroy")]
    internal static class FrameSaveNatureLodClear
    {
        static bool Prepare() { return BtcFrameSave.On; }
        static void Postfix() { FrameSaveNatureLodRest.Clear(); }
    }

    // ---------------------------------------------------------------- MenuFrameCap.cs
    /// Game: outside a running game (menu, lobby, loading) the frame rate was only the player's limit; without vsync
    /// and with the limit off the menus ran at thousands of frames a second (heat, coil whine). Saver: at most 120
    /// frames a second outside a running game; in a game the player's own limit is put back. The options read the
    /// player's own limit back, not the cap. Ported from Castle Story Plus (Performance/MenuFrameCap.cs).
    internal sealed class FrameSaveMenuCap : MonoBehaviour
    {
        private const int Cap = 120;
        private static FrameSaveMenuCap _instance;
        private int _gameRate;
        private bool _capping;
        private int _lastSet = -2;

        internal static void Ensure()
        {
            if (_instance != null) return;
            GameObject go = new GameObject("BtcFrameSaveMenuCap");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            _instance = go.AddComponent<FrameSaveMenuCap>();
        }

        internal static int PlayerRate(int rate)
        {
            return (_instance != null && _instance._capping && rate == _instance._lastSet) ? _instance._gameRate : rate;
        }

        private void LateUpdate()
        {
            bool playing = Architecte.commence && !Architecte.termine;
            if (playing)
            {
                if (_capping)
                {
                    _capping = false;
                    if (Application.targetFrameRate == _lastSet) Application.targetFrameRate = _gameRate;
                }
                return;
            }
            int rate = Application.targetFrameRate;
            if (_capping && rate == _lastSet) return;
            _gameRate = rate;
            _capping = true;
            _lastSet = (rate > 0 && rate <= Cap) ? rate : Cap;
            Application.targetFrameRate = _lastSet;
        }
    }

    /// Creates the cap component at the first game-loop point we hook (sound init runs at startup).
    [HarmonyPatch(typeof(AkInitializer), "Awake")]
    internal static class FrameSaveMenuCapStart
    {
        static bool Prepare() { return BtcFrameSave.On; }
        static void Postfix()
        {
            try { FrameSaveMenuCap.Ensure(); }
            catch (Exception e) { BtcLog.Error("FrameSave", "MenuFrameCap start: " + e); }
        }
    }

    [HarmonyPatch(typeof(UserSettingManager), "GetSettingsFromGame")]
    internal static class FrameSaveMenuCapSettings
    {
        static bool Prepare() { return BtcFrameSave.On; }
        static void Postfix(UserSettings __result)
        {
            try
            {
                if (__result != null && __result.graphic != null)
                    __result.graphic.targetFPS = FrameSaveMenuCap.PlayerRate(__result.graphic.targetFPS);
            }
            catch (Exception e) { BtcLog.Error("FrameSave", "MenuFrameCap settings: " + e); }
        }
    }

    // ---------------------------------------------------------------- FrameSavers.cs (overhead parts only)
    /// Game: a hidden overhead icon is only made see-through (Content canvas group alpha 0), so Unity still rebuilt and
    /// batched its canvases every frame. Saver: canvases that lie inside the hidden content (the icon's own canvas
    /// when it holds only the content, and nested canvases under the content) are switched off while hidden, on again
    /// when it shows or the icon / line goes back to the pool. Differences from CSP: nested canvases are switched only
    /// when inside Content (the arrow canvas outside it stays on). Ported from Castle Story Plus
    /// (Performance/FrameSavers.cs, FrameSaversOverheadPatch).
    [HarmonyPatch(typeof(OverheadDisplay), "Show")]
    internal static class FrameSaveOverheadShow
    {
        internal static readonly HashSet<Canvas> Off = new HashSet<Canvas>();
        private static readonly HashSet<OverheadDisplay> Dimmed = new HashSet<OverheadDisplay>();
        private static readonly List<Canvas> Found = new List<Canvas>();

        static bool Prepare() { return BtcFrameSave.On; }

        static void Postfix(OverheadDisplay __instance, bool value, Transform ___Content, Canvas ___MainCanvas, Canvas ___ArrowCanvas)
        {
            try
            {
                if (___Content == null) return;
                if (!value)
                {
                    // hidden: the canvases go off only after HideDelay (thought bubbles show an icon for ~2 s every few seconds; each
                    // canvas re-enable re-registers its graphics = ~1 KB of garbage, 52 KB/s with 60 workers)
                    if (!Pending.ContainsKey(__instance)) PendingList.Add(__instance);
                    Pending[__instance] = new Hide { At = Time.unscaledTime + HideDelay, Content = ___Content, Main = ___MainCanvas, Arrow = ___ArrowCanvas };
                    return;
                }
                Pending.Remove(__instance);
                Apply(__instance, ___Content, ___MainCanvas, ___ArrowCanvas, true);
            }
            catch (Exception e) { BtcLog.Error("FrameSave", "OverheadShow: " + e); }
        }

        private struct Hide { internal float At; internal Transform Content; internal Canvas Main, Arrow; }
        private const float HideDelay = 3f;
        private static readonly Dictionary<OverheadDisplay, Hide> Pending = new Dictionary<OverheadDisplay, Hide>();
        private static readonly List<OverheadDisplay> PendingList = new List<OverheadDisplay>();

        private static void Apply(OverheadDisplay d, Transform content, Canvas main, Canvas arrow, bool value)
        {
            Switch(main, content, value);
            Switch(arrow, content, value);
            SwitchNested(d, content, main, value);
        }

        /// LateUpdate (BtcVfxCut.Tick): displays hidden for HideDelay switch their canvases off now
        internal static void Tick()
        {
            if (PendingList.Count == 0) return;
            float now = Time.unscaledTime;
            for (int i = PendingList.Count - 1; i >= 0; i--)
            {
                OverheadDisplay d = PendingList[i];
                Hide h;
                if (d == null || !Pending.TryGetValue(d, out h)) { Pending.Remove(d); PendingList.RemoveAt(i); continue; }
                if (now < h.At) continue;
                Pending.Remove(d); PendingList.RemoveAt(i);
                if (h.Content != null) Apply(d, h.Content, h.Main, h.Arrow, false);
            }
        }

        internal static void Restore(Transform root)
        {
            if (Off.Count == 0 || root == null) return;
            Found.Clear();
            root.GetComponentsInChildren<Canvas>(true, Found);
            for (int i = 0; i < Found.Count; i++)
            {
                Canvas c = Found[i];
                if (Off.Remove(c)) c.enabled = true;
            }
            Found.Clear();
        }

        internal static void Released(OverheadDisplay display)
        {
            if (display != null) { Dimmed.Remove(display); Pending.Remove(display); }
        }

        private static void SwitchNested(OverheadDisplay display, Transform content, Canvas main, bool value)
        {
            if (value)
            {
                if (Dimmed.Remove(display)) Restore(display.transform);
                return;
            }
            Found.Clear();
            display.GetComponentsInChildren<Canvas>(true, Found);
            for (int i = 0; i < Found.Count; i++)
            {
                Canvas canvas = Found[i];
                if (canvas == main || !canvas.enabled) continue;
                if (!canvas.transform.IsChildOf(content)) continue;
                canvas.enabled = false;
                Off.Add(canvas);
                Dimmed.Add(display);
            }
            Found.Clear();
        }

        private static void Switch(Canvas canvas, Transform content, bool value)
        {
            if (canvas == null || canvas.enabled == value) return;
            Transform t = canvas.transform;
            bool inside = t == content || t.IsChildOf(content);
            bool holdsOnlyContent = content.IsChildOf(t) && content.parent == t && t.childCount == 1;
            if (inside || holdsOnlyContent) canvas.enabled = value;
        }
    }

    /// An icon or overhead line going back to the pool gets its canvases back before it is reset.
    [HarmonyPatch(typeof(OverheadDisplay), "ResetComponent")]
    internal static class FrameSaveOverheadDisplayReset
    {
        static bool Prepare() { return BtcFrameSave.On; }
        static void Prefix(OverheadDisplay __instance)
        {
            try { FrameSaveOverheadShow.Restore(__instance.transform); FrameSaveOverheadShow.Released(__instance); }
            catch (Exception e) { BtcLog.Error("FrameSave", "OverheadDisplay reset: " + e); }
        }
    }

    [HarmonyPatch(typeof(Overhead), "ResetComponent")]
    internal static class FrameSaveOverheadReset
    {
        static bool Prepare() { return BtcFrameSave.On; }
        static void Prefix(Overhead __instance)
        {
            try { FrameSaveOverheadShow.Restore(__instance.transform); }
            catch (Exception e) { BtcLog.Error("FrameSave", "Overhead reset: " + e); }
        }
    }

    /// Game: an icon sorted by camera distance set its canvas sortingOrder every frame (also unchanged, also while
    /// hidden), which makes Unity sort and batch the canvas again. Saver: set only on change and only while the icon's
    /// main canvas is on; camera position read once per frame. Ported from Castle Story Plus
    /// (Performance/FrameSavers.cs, FrameSaversOverheadSortPatch).
    [HarmonyPatch(typeof(OverheadDisplay), "BrixUpdate")]
    internal static class FrameSaveOverheadSort
    {
        static bool Prepare() { return BtcFrameSave.On; }

        static bool Prefix(OverheadDisplay __instance, Canvas ___MainCanvas)
        {
            try
            {
                if (!__instance.SortBasedOnCamera) return false;
                Canvas canvas = ___MainCanvas;
                if (canvas == null || !canvas.enabled) return false;
                Transform pivot = __instance.pivot;
                Vector3 pos, fwd; float lim;
                if (pivot == null || !FrameSaveCam.Get(out pos, out fwd, out lim)) return true;
                int order = -(int)Mathf.Abs(Vector3.Distance(pivot.position, pos));
                if (canvas.sortingOrder != order) canvas.sortingOrder = order;
                return false;
            }
            catch (Exception e) { BtcLog.Error("FrameSave", "OverheadSort: " + e); return true; }
        }
    }

    /// Game: a storage's shelf display (RecepteurTracksDisplay) worked out its counts and set every shelf object
    /// active or not each frame. Saver: touches them only when a track's count changed. Ported from Castle Story Plus
    /// (Performance/FrameSavers.cs, FrameSaversShelfPatch; kit bundle part dropped, vanilla counting).
    [HarmonyPatch(typeof(RecepteurTracksDisplay), "Update")]
    internal static class FrameSaveShelfUpdate
    {
        private static readonly Dictionary<RecepteurTracksDisplay, int[]> Shown = new Dictionary<RecepteurTracksDisplay, int[]>();
        private static readonly FieldInfo AdjsField = typeof(RecepteurTracksDisplay.DisplayTrack).GetField("adjs", BindingFlags.Instance | BindingFlags.NonPublic);
        private static float _nextCleanup;

        static bool Prepare() { return BtcFrameSave.On && AdjsField != null; }

        internal static void Forget(RecepteurTracksDisplay d) { Shown.Remove(d); }

        static bool Prefix(RecepteurTracksDisplay __instance)
        {
            try
            {
                Recepteur target = __instance.targetRecepteur;
                List<RecepteurTracksDisplay.DisplayTrack> tracks = __instance.DisplayTracks;
                if (target == null || tracks == null) return true;
                int[] shown;
                if (!Shown.TryGetValue(__instance, out shown) || shown.Length != tracks.Count)
                {
                    shown = new int[tracks.Count];
                    for (int i = 0; i < shown.Length; i++) shown[i] = -1;
                    Shown[__instance] = shown;
                }
                for (int i = 0; i < tracks.Count; i++)
                {
                    RecepteurTracksDisplay.DisplayTrack track = tracks[i];
                    if (track == null || !track.Valid) continue;
                    List<Adjectif> adjs = (List<Adjectif>)AdjsField.GetValue(track);
                    int count = 0;
                    for (int k = 0; k < adjs.Count; k++) count += target.ContentDescription.Value(adjs[k]);
                    if (count == shown[i]) continue;
                    shown[i] = count;
                    track.UpdateDisplay(target);
                }
                if (Time.time >= _nextCleanup) Cleanup();
                return false;
            }
            catch (Exception e) { BtcLog.Error("FrameSave", "ShelfUpdate: " + e); return true; }
        }

        private static void Cleanup()
        {
            _nextCleanup = Time.time + 30f;
            List<RecepteurTracksDisplay> dead = null;
            foreach (RecepteurTracksDisplay d in Shown.Keys)
                if (d == null) { if (dead == null) dead = new List<RecepteurTracksDisplay>(); dead.Add(d); }
            if (dead != null) for (int i = 0; i < dead.Count; i++) Shown.Remove(dead[i]);
        }
    }

    /// A reused (pooled) shelf display starts over.
    [HarmonyPatch(typeof(RecepteurTracksDisplay), "OnRequested")]
    internal static class FrameSaveShelfReset
    {
        static bool Prepare() { return BtcFrameSave.On; }
        static void Postfix(RecepteurTracksDisplay __instance) { FrameSaveShelfUpdate.Forget(__instance); }
    }
}
