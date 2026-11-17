using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Lifeline.Tests
{
    public sealed class AquaticVisibilityPlayModeTests
    {
        [UnityTest] public IEnumerator NightExteriorDoesNotLeakBrightLootButCentreRemainsVisible()
        {
            var shader = Shader.Find("LifeLine/UI/Night Visibility"); Assert.That(shader, Is.Not.Null);
            var material = new Material(shader); var target = new RenderTexture(64, 64, 0); target.Create();
            var readback = new Texture2D(64, 64, TextureFormat.RGBA32, false); var previous = RenderTexture.active;
            try
            {
                material.SetVector("_FocusUV", new Vector4(.5f, .5f, 0, 0)); material.SetVector("_ViewportPixels", new Vector4(64, 64, 0, 0));
                material.SetFloat("_RadiusPixels", 18); material.SetFloat("_FeatherPixels", 4); material.SetFloat("_Opacity", 1);
                // Authored tint alpha and the old .98 dark alpha must not expose silhouettes.
                material.SetColor("_DarknessColor", new Color(.006f, .014f, .025f, .98f)); material.SetColor("_Color", new Color(1, 1, 1, .5f));
                Color[] samples = new Color[4];
                for (int i = 0; i < 2; i++)
                {
                    RenderTexture.active = target; GL.Clear(true, true, i == 0 ? Color.white : Color.black);
                    Graphics.Blit(Texture2D.whiteTexture, target, material); RenderTexture.active = target;
                    readback.ReadPixels(new Rect(0, 0, 64, 64), 0, 0); readback.Apply();
                    samples[i * 2] = readback.GetPixel(60, 32); samples[i * 2 + 1] = readback.GetPixel(32, 32);
                }
                Assert.That(Vector4.Distance(samples[0], samples[2]), Is.LessThan(.005f), "outside pixels must be independent of bright objects behind the mask");
                Assert.That(Vector4.Distance(samples[1], samples[3]), Is.GreaterThan(.6f), "the centre must remain transparent so nearby resources are visible");
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(material); target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(readback); }
            yield return null;
        }
        [UnityTest] public IEnumerator CaptainStaysInWaterWithFaintedDiverAfterLeavingPoweredWinchRunning()
        {
            var previous = GameSession.Current; float scale = Time.timeScale; var random = Random.state;
            var root = new GameObject("Captain water rescue physics fixture"); var tuning = ScriptableObject.CreateInstance<GameTuning>(); tuning.maximumSharks = 0;
            var game = root.AddComponent<GameSession>(); game.autoStart = false; game.enabled = false; game.tuning = tuning;
            var ship = new GameObject("Ship"); ship.transform.SetParent(root.transform); game.ship = ship.AddComponent<ShipMotor>(); game.ship.enabled = false;
            var cap = new GameObject("Captain"); cap.transform.SetParent(root.transform); game.captain = cap.AddComponent<CrewActor>();
            var div = new GameObject("Diver"); div.transform.SetParent(root.transform); game.diver = div.AddComponent<CrewActor>(); game.diver.enabled = false;
            game.Initialize(); game.captain.SetExternalInput(new RemoteCrewInputSource { role = CrewRole.Captain });
            try
            {
                game.BeginDive(); game.diver.RestorePose(false, game.BoatAnchor + Vector3.down * 8, Vector3.zero, Vector2.right); game.FaintDiver("test");
                game.upgradeLevels[(int)UpgradeKind.Rope] = 3; game.power = 80; game.circuitRepaired = true; game.winchElectric = game.winchSignalAuthorized = true;
                Assert.That(game.BeginDive(game.captain));
                for (int i = 0; i < 12; i++) { game.StepSimulation(.02f); yield return new WaitForFixedUpdate(); }
                Assert.That(game.captain.onboard, Is.False); Assert.That(game.captain.Position.y, Is.LessThan(0)); Assert.That(game.diverFainted);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(tuning); GameSession.RestoreCurrentAfterValidation(previous); Time.timeScale = scale; Random.state = random; }
        }
    }
}
