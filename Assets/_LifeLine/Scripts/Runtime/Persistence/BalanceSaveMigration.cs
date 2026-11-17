using UnityEngine;

namespace Lifeline
{
    /// <summary>Upgrade a cloned snapshot. Old route coordinates and half-hit damage never mutate the caller's save.</summary>
    public static class BalanceSaveMigration
    {
        public static bool Upgrade(GameSaveData data, GameTuning tuning)
        {
            if(data.balanceVersion>=1)return false;
            float oldDistance=data.routeDistance>0?data.routeDistance:90;
            float ratio=tuning.routeDistance/oldDistance;
            float shift=data.shipPosition.x*(ratio-1);
            data.shipPosition.x*=ratio;
            foreach(var crew in new[]{data.captain,data.diver})
            {
                if(!crew.onboard){crew.position.x+=shift;crew.hidingPosition.x+=shift;}
                float max=crew.role==CrewRole.Captain?tuning.baseOxygen:tuning.oxygenLevels[Mathf.Clamp(data.state.upgradeLevels[(int)UpgradeKind.Oxygen],0,tuning.oxygenLevels.Length-1)]*(data.state.diverBuffs.oxygen>0?1+tuning.fishOxygenBonus:1);
                crew.oxygen=crew.oxygenMax>0?Mathf.Clamp01(crew.oxygen/crew.oxygenMax)*max:0;crew.oxygenMax=max;
            }
            data.state.diverBites*=2;
            foreach(var pickup in data.world.pickups)
            {
                if(pickup.dynamic&&pickup.isCrate&&pickup.amount==0&&!pickup.attached)
                {pickup.isLostCargo=true;pickup.persistAcrossDays=true;}
                if(pickup.attached)pickup.position.x+=shift;
                else pickup.position=MapArea(pickup.position,ratio);
                pickup.ropeContacts=System.Array.Empty<Vector3>();
            }
            foreach(var enemy in data.enemies)
            {
                if(enemy.kind==EnemyKind.Bird||enemy.kind==EnemyKind.ArmoredBird)
                {
                    enemy.position+=Vector3.right*shift;enemy.home+=Vector3.right*shift;
                    enemy.spawnPoint+=Vector3.right*shift;enemy.targetPoint+=Vector3.right*shift;
                }
                else
                {
                    enemy.position=MapArea(enemy.position,ratio);enemy.home=MapArea(enemy.home,ratio);
                    enemy.spawnPoint=MapArea(enemy.spawnPoint,ratio);enemy.targetPoint=MapArea(enemy.targetPoint,ratio);enemy.shadowAim=MapArea(enemy.shadowAim,ratio);
                }
            }
            foreach(var shot in data.spears){var point=shot.position;point.x+=shift;shot.position=point;}
            if(data.areaStrikes!=null)foreach(var strike in data.areaStrikes){var point=strike.point;point.x+=shift;strike.point=point;}
            data.captainRopeContacts=data.diverRopeContacts=data.cargoRopeContacts=System.Array.Empty<Vector3>();
            data.routeDistance=tuning.routeDistance;data.balanceVersion=1;return true;
        }
        static Vector3 MapArea(Vector3 point,float ratio)
        {
            float center=point.x<18?0:point.x<54?36:72;
            point.x+=center*(ratio-1);return point;
        }
    }
}
