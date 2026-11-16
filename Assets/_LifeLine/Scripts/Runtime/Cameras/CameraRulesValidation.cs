using System;
using Unity.Cinemachine;
using UnityEngine;

namespace Lifeline
{
    /// <summary>Explicit Editor-invoked camera mode checks. No scene/prefs changes remain after Run.</summary>
    public static class CameraRulesValidation
    {
        public static string Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Run camera mode checks outside Play mode.");
            bool hadPreference = PlayerPrefs.HasKey(GameCameraRig.ShipSplitPreferenceKey);
            int oldPreference = PlayerPrefs.GetInt(GameCameraRig.ShipSplitPreferenceKey, 0);
            var existingBrains = UnityEngine.Object.FindObjectsByType<CinemachineBrain>();
            var brainWasEnabled = new bool[existingBrains.Length];
            for (int i = 0; i < existingBrains.Length; i++)
            {
                brainWasEnabled[i] = existingBrains[i].enabled;
                existingBrains[i].enabled = false;
            }
            var root = new GameObject("Camera rules checks (temporary)");
            try
            {
                var cap = Actor(root.transform, "Captain", new Vector3(-8, .9f, 0));
                var div = Actor(root.transform, "Diver", new Vector3(8, -3.1f, 0));
                var rig = root.AddComponent<GameCameraRig>();
                rig.Initialize(null, cap, div, null);
                rig.captainVirtualCamera.Priority = 10000; rig.diverVirtualCamera.Priority = 10000;
                rig.SetShipSplitPreference(false, false);
                Require(rig.IsSharedView && !rig.IsSplitScreen && rig.captainCamera.enabled && !rig.diverCamera.enabled
                    && rig.captainCamera.rect == new Rect(0, 0, 1, 1), "aboard defaults to shared full-screen");
                Vector3 focus;
                float fitted = rig.GetSharedFraming(cap.transform.position, div.transform.position, Vector3.zero, 16f / 9, out focus);
                Require(Mathf.Abs(focus.x) < .001f && fitted * 16f / 9 >= rig.shipHalfWidth + rig.sharedPadding.x,
                    "shared view follows crew midpoint and fits complete ship");
                rig.SetShipSplitPreference(true, false);
                CheckSplit(rig);
                rig.SetShipSplitPreference(false, false);
                div.onboard = false; div.transform.position = new Vector3(0, -20, 0);
                rig.RefreshViewNow(); CheckSplit(rig);
                Require(rig.diverConfiner.BoundingShape2D == rig.waterBoundary, "underwater diver uses water confinement");
                div.onboard = true; rig.RefreshViewNow();
                Require(rig.IsSharedView && !rig.diverCamera.enabled, "boarding restores shared preference");
                rig.SetNetworkView(true, CrewRole.Captain);
                Require(!rig.IsSplitScreen && !rig.IsSharedView && rig.captainCamera.enabled && !rig.diverCamera.enabled
                    && rig.captainCamera.rect == new Rect(0, 0, 1, 1), "online captain only full-screen");
                rig.SetNetworkView(true, CrewRole.Diver);
                Require(!rig.IsSplitScreen && !rig.captainCamera.enabled && rig.diverCamera.enabled
                    && rig.diverCamera.rect == new Rect(0, 0, 1, 1), "online diver only full-screen");
                GameCameraRig.SaveShipSplitPreference(true); Require(GameCameraRig.LoadShipSplitPreference(), "split preference persists");
                GameCameraRig.SaveShipSplitPreference(false); Require(!GameCameraRig.LoadShipSplitPreference(), "shared preference persists");
                Require(rig.captainBrain.ChannelMask == Unity.Cinemachine.OutputChannels.Channel01
                    && rig.diverBrain.ChannelMask == Unity.Cinemachine.OutputChannels.Channel02,
                    "independent Cinemachine channels");
                return "PASS camera modes: shared ship default + full-ship framing, optional ship split, forced diving split, boarding restoration, online role full-screen, saved preference, separate CM channels. Obstacle response still needs a Play-mode scene check.";
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                for (int i = 0; i < existingBrains.Length; i++) if (existingBrains[i] != null) existingBrains[i].enabled = brainWasEnabled[i];
                if (hadPreference) PlayerPrefs.SetInt(GameCameraRig.ShipSplitPreferenceKey, oldPreference);
                else PlayerPrefs.DeleteKey(GameCameraRig.ShipSplitPreferenceKey);
                PlayerPrefs.Save();
            }
        }
        static CrewActor Actor(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = position;
            var actor = go.AddComponent<CrewActor>(); actor.enabled = false; actor.onboard = true; return actor;
        }
        static void CheckSplit(GameCameraRig rig)
        {
            Require(rig.IsSplitScreen && !rig.IsSharedView && rig.captainCamera.enabled && rig.diverCamera.enabled
                && rig.captainCamera.rect == new Rect(0, 0, .5f, 1) && rig.diverCamera.rect == new Rect(.5f, 0, .5f, 1), "local split viewports");
        }
        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Camera check failed: " + message);
        }
    }
}
