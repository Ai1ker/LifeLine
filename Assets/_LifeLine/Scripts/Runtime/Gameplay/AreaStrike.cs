using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lifeline
{
    public enum AreaStrikeKind { Lightning, ShadowBite, ShadowDash, WaterShot }
    [Serializable] public sealed class AreaStrike
    {
        public AreaStrikeKind kind;
        public Vector3 point;
        public bool onShipMap;
        public float remaining, duration, radius, hullDamage;
        [NonSerialized] public AreaWarningView view;
    }
    public partial class GameSession
    {
        public List<AreaStrike> areaStrikes = new List<AreaStrike>();
        public GameObject areaWarningPrefab;
        public void IgnoreCrewCollision()
        {
            if(captain==null||diver==null)return;
            foreach(var a in captain.GetComponentsInChildren<Collider>())
                foreach(var b in diver.GetComponentsInChildren<Collider>())Physics.IgnoreCollision(a,b,true);
            foreach(var actor in new[]{captain,diver}) if(actor.GetComponent<CrewStatusView>()==null)actor.gameObject.AddComponent<CrewStatusView>().actor=actor;
        }
        public AreaStrike QueueAreaStrike(AreaStrikeKind kind,Vector3 point,bool shipMap,float radius,float delay,float hullDamage)
        {
            var strike=new AreaStrike{kind=kind,point=point,onShipMap=shipMap,radius=radius,duration=delay,remaining=delay,hullDamage=hullDamage};
            if (kind == AreaStrikeKind.Lightning) GameAudio.At(this, AudioCue.LightningWarning, shipMap ? WaterToShipPoint(point) : point, shipMap);
            areaStrikes.Add(strike); EnsureWarning(strike);return strike;
        }
        void EnsureWarning(AreaStrike strike)
        {
            if(strike.view!=null)return;
            var obj=areaWarningPrefab!=null?Instantiate(areaWarningPrefab):new GameObject("Area warning / replaceable");
            maps?.MoveGameplayObject(obj,strike.onShipMap);
            strike.view=obj.GetComponent<AreaWarningView>();if(strike.view==null)strike.view=obj.AddComponent<AreaWarningView>();
            strike.view.session=this;strike.view.strike=strike;
        }
        public void ClearAreaStrikes()
        {
            foreach(var strike in areaStrikes)if(strike.view!=null){strike.view.gameObject.SetActive(false);if(Application.isPlaying)Destroy(strike.view.gameObject);else DestroyImmediate(strike.view.gameObject);}
            areaStrikes.Clear();
        }
        void UpdateAreaStrikes(float dt)
        {
            for(int i=areaStrikes.Count-1;i>=0;i--)
            {
                var strike=areaStrikes[i];EnsureWarning(strike);strike.remaining-=dt;
                if(strike.remaining>0)continue;
                ResolveAreaStrike(strike);if(strike.view!=null)strike.view.Complete();areaStrikes.RemoveAt(i);
            }
        }
        public void ResolveAreaStrike(AreaStrike strike)
        {
            Vector3 soundPoint = strike.onShipMap ? WaterToShipPoint(strike.point) : strike.point;
            switch (strike.kind)
            {
                case AreaStrikeKind.Lightning:
                    GameAudio.At(this, AudioCue.LightningStrike, soundPoint, strike.onShipMap);
                    GameAudio.At(this, AudioCue.ThunderNear, soundPoint, strike.onShipMap);
                    break;
                case AreaStrikeKind.ShadowBite:
                    GameAudio.At(this, AudioCue.EnemyShadowBite, soundPoint, strike.onShipMap);
                    break;
                case AreaStrikeKind.WaterShot:
                    GameAudio.At(this, AudioCue.EnemyShadowHit, soundPoint, strike.onShipMap);
                    break;
            }
            foreach(var actor in new[]{captain,diver})
            {
                if(actor==null||IsIncapacitated(actor)||IsHidden(actor)||actor.onboard!=strike.onShipMap)continue;
                Vector3 position=actor.onboard?ShipToWaterPoint(actor.Position):actor.Position;
                if(actor.onboard&&ship!=null&&ship.transform.InverseTransformPoint(actor.Position).y<actor.deckY)continue;
                if(Vector2.Distance(position,strike.point)<=strike.radius)actor.ReceiveDamage(strike.kind==AreaStrikeKind.Lightning ? actor==diver?GetMaxDiverBites():tuning.baseDiverHitCapacity : strike.kind==AreaStrikeKind.WaterShot ? tuning.waterShotHitDamage : tuning.shadowDamage);
            }
            bool touchesHull=ship!=null && (strike.onShipMap
                ? Mathf.Abs(strike.point.x-ShipToWaterPoint(ship.Position).x)<=12+strike.radius
                : Vector2.Distance(strike.point,BoatAnchor)<=strike.radius+1.5f);
            if(touchesHull)
            {
                DamageHull(strike.kind == AreaStrikeKind.Lightning ? lightningHullDamage : strike.hullDamage);
                if(strike.kind==AreaStrikeKind.Lightning&&engineRepaired&&UnityEngine.Random.value<tuning.lightningEngineRisk)DamageEngine("雷击损坏了引擎");
            }
        }
    }
}
