using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Lifeline.Tests
{
    public sealed class CrewHidingPlayModeTests
    {
        [UnityTest] public IEnumerator HiddenCrewIgnoreMoveAndHeldInteractUntilANewPress()
        {
            var previous=GameSession.Current;float scale=Time.timeScale;var random=Random.state;
            var root=new GameObject("Hiding physics fixture");var tuning=ScriptableObject.CreateInstance<GameTuning>();tuning.maximumSharks=0;
            var game=root.AddComponent<GameSession>();game.autoStart=false;game.enabled=false;game.tuning=tuning;
            var capObject=new GameObject("Captain");capObject.transform.SetParent(root.transform);game.captain=capObject.AddComponent<CrewActor>();game.captain.enabled=false;
            var divObject=new GameObject("Diver");divObject.transform.SetParent(root.transform);game.diver=divObject.AddComponent<CrewActor>();
            game.diver.visualRoot=new GameObject("VisualRoot").transform;game.diver.visualRoot.SetParent(divObject.transform);
            game.Initialize();var source=new RemoteCrewInputSource{role=CrewRole.Diver};game.diver.SetExternalInput(source);
            var water=new GameObject("Water");water.transform.SetParent(root.transform);var world=water.AddComponent<WorldGenerator>();world.session=game;world.tuning=tuning;game.world=world;world.drawLabels=false;
            var hide=water.AddComponent<UnderwaterHideSpot>();world.hideSpots.Add(hide);hide.world=world;water.transform.position=Vector3.down*5;
            game.diver.RestorePose(false,water.transform.position,Vector3.zero,Vector2.right);Vector3 start=game.diver.Position;
            try
            {
                Assert.That(world.TryHide(game.diver));
                source.Push(new CrewCommand{role=CrewRole.Diver,move=Vector2.right,interactHeld=true,fire=true});
                for(int i=0;i<5;i++)yield return new WaitForFixedUpdate();
                Assert.That(game.diver.Position,Is.EqualTo(start));Assert.That(game.diver.visualRoot.gameObject.activeSelf,Is.False);
                source.Push(new CrewCommand{role=CrewRole.Diver,interact=true,interactHeld=true});yield return new WaitForFixedUpdate();
                Assert.That(game.IsHidden(game.diver),Is.False);Assert.That(game.diver.visualRoot.gameObject.activeSelf);
                source.Push(new CrewCommand{role=CrewRole.Diver,move=Vector2.right});
                for(int i=0;i<4;i++)yield return new WaitForFixedUpdate();
                Assert.That(game.diver.Position.x,Is.GreaterThan(start.x+.03f));
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(tuning);GameSession.RestoreCurrentAfterValidation(previous);Random.state=random;Time.timeScale=scale;}
        }
    }
}
