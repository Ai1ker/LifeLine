using UnityEngine;
namespace Lifeline
{
    public partial class GameSession
    {
        public DiverRope captainRope;
        public CrewRole winchOperatorRole=CrewRole.Captain;
        public float captainBoardProgress;
        public float captainBoardSeconds=3f;
        public bool captainBoardHeld,captainSprinting;
        CrewActor[] crewCache=new CrewActor[2];
        public CrewActor[] Crew {get {crewCache[0]=captain;crewCache[1]=diver;return crewCache;}}
        public bool IsHidden(CrewActor actor)=>actor!=null&&(actor.hidden||actor==diver&&diverHidden);
        public bool IsFainted(CrewActor actor)=>actor!=null&&(actor==diver?diverFainted:actor.fainted);
        public bool IsIncapacitated(CrewActor actor)=>actor==null||actor.downed||IsFainted(actor);
        public bool AnyCrewUnderwater=>(captain!=null&&!captain.onboard)||(diver!=null&&!diver.onboard);
        public float GetOxygenMax(CrewActor actor)=>actor==captain?tuning.baseOxygen:GetOxygenMax();
        public DiverRope GetCrewLifeline(CrewActor actor)
        {
            if(actor!=captain)return GetOrCreateLifeline();
            if(captainRope==null){var obj=new GameObject("Captain collision rope");obj.transform.SetParent(transform,false);captainRope=obj.AddComponent<DiverRope>();}
            captainRope.Configure(captain,ship!=null?ship.transform:null);
            captainRope.collisionMask=world!=null?1<<world.geometryLayer:obstacleMask&~(1<<9)&~(1<<10);
            return captainRope;
        }
        public float CrewRopeLength(CrewActor actor)
        {
            if(actor==null||actor.onboard)return 0;
            var route=GetCrewLifeline(actor);route.UpdateRoute(BoatAnchor,actor.Position);return route.PathLength;
        }
        public CrewActor FindWaterTarget(Vector3 position)
        {
            CrewActor result=null;float nearest=float.PositiveInfinity;
            foreach(var actor in Crew)
            {
                if(IsIncapacitated(actor)||actor.onboard||IsHidden(actor))continue;
                float distance=(actor.Position-position).sqrMagnitude;
                if(distance<nearest){nearest=distance;result=actor;}
            }
            return result;
        }
        void UpdateCaptainOxygen(float dt)
        {
            if(captain==null)return;
            captain.oxygenMax=GetOxygenMax(captain);captain.oxygen=Mathf.Min(captain.oxygen,captain.oxygenMax);
            if(captain.onboard){captain.oxygen=Mathf.MoveTowards(captain.oxygen,captain.oxygenMax,faintOxygenRecovery*dt);return;}
            if(IsFainted(captain))
            {
                if (BeingRevived(captain)) return;
                var rope=GetCrewLifeline(captain);rope.UpdateRoute(BoatAnchor,captain.Position);rope.Pull(captain,faintDriftSpeed*dt);return;
            }
            if(captain.downed)return;
            float drain=(captainSprinting?tuning.sprintOxygenMultiplier:1)*(IsNight?nightOxygenMultiplier:1);
            OverrideOxygenDrain(captain, ref drain);
            captain.oxygen=Mathf.Max(0,captain.oxygen-dt*drain);
            if(captain.oxygen<=0)FaintCrew(captain,"氧气耗尽");
        }
        public void FaintCrew(CrewActor actor,string reason)
        {
            if(actor==null||actor.onboard||IsFainted(actor))return;
            if(actor==diver){FaintDiver(reason);return;}
            actor.SetHidden(false);actor.fainted=true;
            GameAudio.At(actor, AudioCue.CrewDowned);
            DropLostCrewCargo(actor, "Lost captain bag");
            captainBoardHeld=false;captainBoardProgress=0;
            if(actor.body!=null)actor.body.linearVelocity=Vector3.zero;
            Tell(GameNames.Role(actor.role)+"昏厥。水下队友可扶起，也可用绞盘接回。");Changed();
        }
        // An E hold near loose cargo must collect it rather than silently start the boarding channel.
        bool HasNearbyCaptainWaterInteraction(CrewActor actor, bool interactHides)
        {
            if (world == null || actor == null || actor.onboard) return false;
            if (interactHides && world.IsAtHideSpot(actor)) return true;
            foreach (LootPickup pickup in world.pickups)
            {
                if (pickup == null || pickup.attached || !pickup.InRange(actor)) continue;
                if (pickup.isCrate)
                {
                    if (pickup.contents.Count > 0 || actor.backpack.Count > 0 && pickup.contents.Count < pickup.capacity) return true;
                }
                else if (actor.backpack.Count < actor.BagCapacity || pickup.GetComponent<FilterSalvage>() != null) return true;
            }
            return false;
        }
        void UpdateCaptainBoard(float dt)
        {
            if(captain==null||captain.onboard||IsIncapacitated(captain)||IsHidden(captain)||!captainBoardHeld||Vector3.Distance(captain.Position,BoatAnchor)>3)
            {captainBoardProgress=0;return;}
            captainBoardProgress+=dt;
            if(captainBoardProgress>=captainBoardSeconds){BankCrew(captain);captainBoardHeld=false;captainBoardProgress=0;}
        }
        void BankCrew(CrewActor actor)
        {
            if(actor==null||actor.onboard)return;
            if(actor==diver){BankDiver();return;}
            bool needsRevive=IsIncapacitated(actor);
            if (actor.backpack.Count > 0) GameAudio.Global(this, AudioCue.BankDeposit);
            foreach(var item in actor.backpack)AddResource(item,1);actor.backpack.Clear();
            actor.SetHidden(false);actor.fainted=false;if(!needsRevive)actor.health=actor.maxHealth;
            actor.SetOnboard(true,ShipPoint(captainSpawnLocal));actor.SetDowned(needsRevive);
            if(captainRope!=null)captainRope.ResetRoute();actor.oxygenMax=GetOxygenMax(actor);actor.oxygen=actor.oxygenMax;
            signalCount=0;
            if(WinchTargetCount==0){winchActive=winchElectric=winchSignalAuthorized=false;winchStartDistance=0;winchTargetKind="";}
            else PrepareWinchTarget();
            Tell(needsRevive?"船长已回船，背包物资已卸入共享仓库。靠近倒地船长按交互进行救助。":"船长已回船，背包物资已卸入共享仓库。");Changed();
        }
        public void SignalWinch(CrewActor actor)
        {
            if(actor==diver){SignalCaptain();return;}
            if(actor==null||actor.onboard)return;
            signalCount=elapsed-lastSignalAt<=tuning.electricWinchSignalWindow?signalCount+1:1;lastSignalAt=elapsed;
            Tell("船长发出收绳信号。");AttractEnemies(actor.Position);
            if(signalCount>=tuning.electricWinchSignalCount&&IsElectricWinch&&circuitRepaired&&(power>0||infinitePower))
            {
                winchActive=false;winchElectric=true;winchSignalAuthorized=true;winchStartDistance=winchProgress=0;signalCount=0;PrepareWinchTarget();Changed();
            }
        }
    }
}
