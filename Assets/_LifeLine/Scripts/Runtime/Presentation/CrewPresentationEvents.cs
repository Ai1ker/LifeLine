using UnityEngine;

namespace Lifeline
{
    public enum CrewVisualEvent { Jump, Land, Dive, Board, Melee, Fire, Hit, Downed, Revived, Winched }

    /// <summary>Attach a MonoBehaviour implementing this seam for authored audio, particles, camera shake or UI feedback.</summary>
    public interface ICrewPresentationSink
    {
        void OnCrewPresentationEvent(CrewActor actor, CrewVisualEvent kind, Vector3 worldPosition, float strength);
    }
}
