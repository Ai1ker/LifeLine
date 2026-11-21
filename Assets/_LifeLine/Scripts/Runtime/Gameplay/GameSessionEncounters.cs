using System.Collections.Generic;
using UnityEngine;

namespace Lifeline
{
    public partial class GameSession
    {
        public void RegisterEnemy(LifelineEnemy enemy)
        {
            if (enemy == null) return;
            enemy.session = this;
            if (!enemies.Contains(enemy)) enemies.Add(enemy);
        }

        void SpawnInitialSharks()
        {
            // Reserve the shared shark slots for nearby cave guards before ordinary patrols.
            if (world != null) foreach (Vector3 point in world.caveGuardSpawnPoints)
            {
                var guard = SpawnEnemy(EnemyKind.WhiteShark, point);
                if (guard != null) { guard.caveGuard = true; guard.patrolRadius = .6f; }
            }
            foreach (Vector3 point in SelectInitialSharkSpawnPoints()) SpawnEnemy(EnemyKind.Shark, point);
            if (world != null) foreach (Vector3 point in world.piranhaSpawnPoints) SpawnEnemy(EnemyKind.Piranha, point);
        }

        // Pure selection, shared by initial and daily spawning. World appends whole areas in order,
        // so taking the first N candidates would omit the last area and its later white-shark variants.
        internal List<Vector3> SelectInitialSharkSpawnPoints()
        {
            var selected = new List<Vector3>();
            if (tuning == null || tuning.maximumSharks <= 0) return selected;
            float surface = world != null ? world.WaterSurface : -1.5f;
            if (world != null && world.sharkSpawnPoints.Count > 0)
            {
                float[] centers = world.areaCenters;
                int areaCount = centers != null && centers.Length > 0 ? centers.Length : 3;
                var areas = new List<Vector3>[areaCount];
                for (int i = 0; i < areas.Length; i++) areas[i] = new List<Vector3>();
                foreach (Vector3 point in world.sharkSpawnPoints)
                {
                    if (surface - point.y < tuning.sharkMinimumDepth) continue;
                    int area = 0;
                    if (centers != null && centers.Length > 0)
                    {
                        float closest = float.MaxValue;
                        for (int i = 0; i < centers.Length; i++)
                        {
                            float distance = Mathf.Abs(point.x - centers[i]);
                            if (distance < closest) { closest = distance; area = i; }
                        }
                    }
                    else area = point.x < tuning.shallowBoundary ? 0 : point.x < tuning.deepBoundary ? 1 : 2;
                    areas[area].Add(point);
                }
                foreach (List<Vector3> candidates in areas)
                    candidates.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
                bool found = true;
                while (selected.Count < tuning.maximumSharks && found)
                {
                    found = false;
                    foreach (List<Vector3> candidates in areas)
                    {
                        if (candidates.Count == 0) continue;
                        // Match the world's three-deep / one-middle candidate weighting. The other
                        // slots prefer deeper points; each fourth slot retains some middle-band danger.
                        // 3:1 baseline gives75% deep weight. Moving45% of middle slots deep
                        // gives86.25%: a relative15% increase, while retaining the global cap.
                        bool retainMiddle = selected.Count % 4 == 3;
                        int sampleHash = Mathf.Abs(unchecked(worldSeed + day*397 + selected.Count*7919)) % 10000;
                        int index = retainMiddle && sampleHash >= Mathf.Clamp01(tuning.deepSharkSpawnBonus*3)*10000 ? candidates.Count - 1 : 0;
                        selected.Add(candidates[index]); candidates.RemoveAt(index); found = true;
                        if (selected.Count >= tuning.maximumSharks) break;
                    }
                }
            }
            else
            {
                for (int i = 0; i < Mathf.Min(3, tuning.maximumSharks); i++) selected.Add(new Vector3(8 + 30 * i, surface - tuning.sharkMinimumDepth - 2 - i * 4, 0));
            }
            return selected;
        }

        public bool HasLivingShadow
        {
            get
            {
                foreach (LifelineEnemy enemy in enemies)
                    if (enemy != null && enemy.kind == EnemyKind.Shadow && enemy.IsAlive) return true;
                return false;
            }
        }

        public void RefreshWorldForDay(int newDay)
        {
            if (world == null) return;
            if (HasLivingShadow)
            {
                shadowWorldRefreshPending = world.activeDay != newDay;
                return;
            }
            // Keep the same obstacles and rope contacts for anyone who stays submerged overnight.
            if (AnyCrewUnderwater) return;
            shadowWorldRefreshPending = false;
            foreach (LifelineEnemy enemy in enemies.ToArray())
            {
                if (enemy == null || !enemy.IsAquaticHunter) continue;
                enemies.Remove(enemy); enemy.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(enemy.gameObject); else DestroyImmediate(enemy.gameObject);
            }
            world.RefreshDay(newDay);
            SpawnInitialSharks();
            Tell("新的一天，海底遗迹与物资已刷新。已挂载货箱、背包和船上升级保留。");
        }

        public LifelineEnemy SpawnEnemy(EnemyKind kind, Vector3 position, bool restoreExisting = false)
        {
            if (kind == EnemyKind.Shadow && !restoreExisting && HasLivingShadow) return null;
            if (kind == EnemyKind.Shark || kind == EnemyKind.WhiteShark)
            {
                float surface = world != null ? world.WaterSurface : -1.5f;
                if (!restoreExisting && surface - position.y < tuning.sharkMinimumDepth) return null;
                int count = 0; foreach (LifelineEnemy existing in enemies) if (existing != null && existing.IsShark && existing.IsAlive) count++;
                if (count >= tuning.maximumSharks) return null;
            }
            if (kind == EnemyKind.Piranha)
            {
                float depth = (world != null ? world.WaterSurface : -1.5f) - position.y;
                if (!restoreExisting && (position.x < tuning.shallowBoundary || position.x >= tuning.deepBoundary || depth < tuning.piranhaDepths.x || depth > tuning.piranhaDepths.y)) return null;
                int count = 0; foreach (var existing in enemies) if (existing != null && existing.kind == EnemyKind.Piranha && existing.IsAlive) count++;
                if (count >= Mathf.Clamp(tuning.maximumPiranhas, 0, 6)) return null;
            }
            GameObject prefab = kind == EnemyKind.Piranha ? piranhaPrefab : kind == EnemyKind.Shadow ? shadowPrefab : kind == EnemyKind.Bird || kind == EnemyKind.ArmoredBird ? birdPrefab : sharkPrefab;
            if (prefab == null && world != null) prefab = kind == EnemyKind.Piranha ? world.piranhaPrefab : kind == EnemyKind.Shadow ? world.shadowPrefab : kind == EnemyKind.Shark || kind == EnemyKind.WhiteShark ? world.sharkPrefab : null;
            GameObject root = prefab != null ? Instantiate(prefab, position, Quaternion.identity) : GameObject.CreatePrimitive(kind == EnemyKind.Bird || kind == EnemyKind.ArmoredBird ? PrimitiveType.Capsule : PrimitiveType.Sphere);
            root.name = kind + "_Gameplay";
            root.transform.position = position;
            maps?.MoveGameplayObject(root, kind == EnemyKind.Bird || kind == EnemyKind.ArmoredBird);
            LifelineEnemy enemy = root.GetComponent<LifelineEnemy>();
            if (enemy == null) enemy = root.AddComponent<LifelineEnemy>();
            enemy.Initialize(this, kind);
            RegisterEnemy(enemy); return enemy;
        }

        void UpdateEncounters(float dt)
        {
            enemies.RemoveAll(enemy => enemy == null);
            if (!IsNight) shadowSpawnedThisNight = false;
            if (IsShadowSpawnAllowed && !shadowSpawnedThisNight && !shadowRepelled && !HasLivingShadow)
            {
                shadowSpawnedThisNight = true;
                SpawnEnemy(EnemyKind.Shadow, BoatAnchor + new Vector3(12, -16, 0));
                Tell("深渊之影正在靠近，累计造成30点伤害可将它逼退。");
            }
            if (IsNight)
            {
                float rate = LightEffective && GetUpgrade(UpgradeKind.Searchlight) >= 2 ? tuning.upgradedLightBirdRate : 1;
                birdCountdown -= dt * rate;
                if (birdCountdown <= 0)
                {
                    birdCountdown += BirdInterval;
                    SpawnBirdWave(UnityEngine.Random.value);
                }
            }
            UpdateTurret(dt);
        }

        public void AttractEnemies(Vector3 point)
        {
            foreach (LifelineEnemy enemy in enemies)
                if (enemy != null && enemy.IsAquaticHunter && Vector3.Distance(enemy.transform.position, point) < tuning.sharkNightDetectionRange * 1.5f) enemy.Alert(point);
        }

        public bool ThrowBait(CrewActor actor)
        {
            if (actor == null || actor.onboard || !Spend(new[] { new ResourceAmount(ResourceKind.Bait, 1) })) return false;
            baitPosition = actor.Position + new Vector3(actor.Facing.x == 0 ? 1 : actor.Facing.x, 0, 0) * 3;
            baitTime = tuning.baitSeconds;
            Tell("诱饵投入水中，附近鲨鱼暂时转移注意力。"); Changed(); return true;
        }

        public bool MeleeAttack(CrewActor actor)
        {
            if (actor == null || IsActorInputBlocked(actor) || meleeCooldowns[(int)actor.role] > 0) return false;
            var presentation = actor.GetComponent<CrewPresentation>();
            if (presentation != null && presentation.MeleeAnimationPlaying) return false;
            Vector2 facing = MeleeArc.Direction(actor.Facing);
            float range = MeleeReach;
            meleeCooldowns[(int)actor.role] = Mathf.Max(tuning.meleeCooldown, presentation != null ? presentation.swingSeconds : 0);
            actor.EmitPresentationEvent(CrewVisualEvent.Melee, direction: facing);
            if (!actor.onboard) TryPushUnderwaterCargo(actor, facing, range);
            float damage = hasHarpoon ? harpoonDamage : meleeDamage;
            LifelineEnemy closest = null; float nearest = range;
            foreach (LifelineEnemy enemy in enemies)
            {
                if (enemy == null || !enemy.IsAlive) continue;
                // The ship deck and seabed are separate maps. Attacks against the shadow
                // use the matching point at the small surface boat, never the distant cabin.
                if (!actor.onboard && enemy.IsAirborne) continue;
                Vector3 attackOrigin = actor.onboard && !enemy.IsAirborne ? ShipToWaterPoint(actor.Position) : actor.Position;
                Vector2 offset = enemy.transform.position - attackOrigin;
                float distance = Mathf.Max(0, offset.magnitude - enemy.hitRadius);
                if (distance > nearest || !MeleeArc.Intersects(offset, facing, range, enemy.hitRadius)) continue;
                if (actor.onboard && !enemy.IsAirborne && enemy.kind != EnemyKind.Shadow && enemy.transform.position.y < -2) continue;
                closest = enemy; nearest = distance;
            }
            if (closest != null) closest.ReceiveHit(closest.IsAirborne ? damage * AirborneWeaponMultiplier(actor) : damage, actor);
            return true;
        }

        public float MeleeReach => tuning.meleeRange * (hasHarpoon ? 1f : .65f);
        internal float AirborneWeaponMultiplier(CrewActor actor) => actor != null && Buffs(actor).attack > 0 ? 1 + tuning.fishAttackBonus : 1;

        void TryPushUnderwaterCargo(CrewActor actor, Vector2 facing, float range)
        {
            if (world == null) return;
            LootPickup target = null;
            float nearest = range;
            foreach (var pickup in world.pickups)
            {
                if (pickup == null || !pickup.isCrate || pickup.consumed || !pickup.gameObject.activeInHierarchy) continue;
                Vector2 offset = pickup.transform.position - actor.Position;
                float distance = offset.magnitude;
                if (distance > nearest || !MeleeArc.Intersects(offset, facing, range)) continue;
                target = pickup;
                nearest = distance;
            }
            if (target == null) return;
            Vector3 push = (Vector2)(target.transform.position - actor.Position);
            if (push.sqrMagnitude < .001f) push = facing;
            push.Normalize();
            var collider = target.GetComponent<Collider>();
            Bounds bounds = collider != null ? collider.bounds : new Bounds(target.transform.position, new Vector3(.6f, .45f, .5f));
            float distanceLimit = Mathf.Max(bounds.size.x, bounds.size.y);
            Vector3 half = Vector3.Max(Vector3.one * .025f, bounds.extents - Vector3.one * .025f);
            if (Physics.BoxCast(bounds.center, half, push, out RaycastHit hit, Quaternion.identity, distanceLimit, 1 << world.geometryLayer, QueryTriggerInteraction.Ignore))
                distanceLimit = Mathf.Max(0, hit.distance - .025f);
            if (distanceLimit <= .001f) return;
            var origin = target.transform.position;
            var destination = origin + push * distanceLimit;
            if (target.attached)
            {
                var route = GetCargoLifeline(target);
                bool wasRoutable = route.UpdateRoute(BoatAnchor, origin);
                float previousLength = route.PathLength;
                bool isRoutable = route.UpdateRoute(BoatAnchor, destination);
                if (wasRoutable && !isRoutable || isRoutable && route.PathLength > Mathf.Max(ropeLength, previousLength) + .01f)
                {
                    route.UpdateRoute(BoatAnchor, origin);
                    return;
                }
            }
            target.transform.position = destination;
            target.blockedPull = 0;
            Changed();
        }

        public bool FireSpear(CrewActor actor)
        {
            if (actor == null || IsActorInputBlocked(actor) || !hasSpeargun) return false;
            int index = (int)actor.role;
            if (activeSpears[index] != null) { activeSpears[index].Recall(); return true; }
            Vector2 facing = actor.Facing.sqrMagnitude > .1f ? actor.Facing.normalized : Vector2.right;
            GameObject root = spearPrefab != null ? Instantiate(spearPrefab) : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            root.name = "ReusableSpear_" + actor.role;
            root.transform.position = actor.Position + (Vector3)facing * .7f;
            if (spearPrefab == null) root.transform.localScale = new Vector3(.07f, .55f, .07f);
            root.transform.rotation = Quaternion.FromToRotation(Vector3.up, (Vector3)facing);
            Collider collider = root.GetComponent<Collider>(); if (collider != null) collider.isTrigger = true;
            LifelineSpear spear = root.GetComponent<LifelineSpear>(); if (spear == null) spear = root.AddComponent<LifelineSpear>();
            spear.session = this; spear.owner = actor; spear.direction = facing;
            spear.onShipMap = actor.onboard;
            maps?.MoveGameplayObject(root, spear.onShipMap);
            spear.damage = spearDamage;
            var spearMesh = root.GetComponentInChildren<MeshRenderer>();
            if (spearMaterial != null && spearMesh != null) spearMesh.sharedMaterial = spearMaterial;
            spear.maximumRange=10f;
            if(root.GetComponent<SpearTetherView>()==null)root.AddComponent<SpearTetherView>().spear=spear;
            activeSpears[index] = spear; actor.EmitPresentationEvent(CrewVisualEvent.Fire, direction: facing); return true;
        }

        public void NotifyWeaponHit(GameObject target, CrewActor actor)
        {
            if (target == null || actor == null) return;
            LifelineEnemy enemy = target.GetComponentInParent<LifelineEnemy>();
            if (enemy != null) enemy.ReceiveHit(hasHarpoon ? harpoonDamage : meleeDamage, actor);
        }

        public bool BuildTurret()
        {
            if (phase != SessionPhase.Playing || turretInstalled || !circuitRepaired) return false;
            if (!Spend(turretBuildCost)) return false;
            turretInstalled = true; turretTier = 1; turretOn = true; turretPowerClock = turretShotClock = 0;
            RefreshEquipmentVisibility();
            Tell("甲板炮塔已安装。每3秒消耗1%电力，自动驱鸟并对抗深渊巨影。"); Changed(); return true;
        }

        public bool ToggleTurret()
        {
            if (!turretInstalled || !circuitRepaired) return false;
            turretOn = !turretOn; Changed(); return true;
        }

        void UpdateTurret(float dt)
        {
            if (!turretInstalled || !turretOn || !circuitRepaired || !infinitePower && power <= 0) return;
            turretPowerClock += dt;
            while (turretPowerClock >= Mathf.Max(.1f, tuning.turretPowerInterval))
            {
                turretPowerClock -= Mathf.Max(.1f, tuning.turretPowerInterval);
                if (!infinitePower) power = Mathf.Max(0, power - 1);
            }
            if (!infinitePower && power <= 0) return;
            turretShotClock += dt;
            if (turretShotClock < tuning.turretShotInterval) return;
            var targets = SelectTurretTargets();
            if(targets.Count==0)return;
            turretShotClock=0;
            foreach(var target in targets) FireTurret(target);
        }
        public bool UpgradeTurret()
        {
            if(!turretInstalled || turretTier>=2 || !Spend(turretUpgradeCost))return false;
            turretTier=2;Tell("炮塔T2：可同时攻击两个不同目标。");Changed();return true;
        }
        public List<LifelineEnemy> SelectTurretTargets()
        {
            var result=new List<LifelineEnemy>();
            foreach(var enemy in enemies)
            {
                if(enemy==null||!enemy.IsAlive)continue;
                float depth=(world!=null?world.WaterSurface:diver!=null?diver.waterSurfaceY:-1.5f)-enemy.transform.position.y;
                bool eligible=enemy.kind==EnemyKind.Shadow?Vector3.Distance(enemy.transform.position,BoatAnchor)<28
                    :enemy.IsAirborne?Vector3.Distance(enemy.transform.position,ShipPoint(boatAnchorLocal))<20
                    :enemy.IsShark&&depth>=0&&depth<=tuning.turretSharkDepth&&Mathf.Abs(enemy.transform.position.x-BoatAnchor.x)<24;
                if(eligible)result.Add(enemy);
            }
            result.Sort((a,b)=>{int rankA=a.kind==EnemyKind.Shadow?0:a.IsAirborne?1:2;int rankB=b.kind==EnemyKind.Shadow?0:b.IsAirborne?1:2;
                return rankA!=rankB?rankA.CompareTo(rankB):Vector2.Distance(a.transform.position,a.IsAirborne?ShipPoint(boatAnchorLocal):BoatAnchor).CompareTo(Vector2.Distance(b.transform.position,b.IsAirborne?ShipPoint(boatAnchorLocal):BoatAnchor));});
            int count=turretTier>=2?2:1;if(result.Count>count)result.RemoveRange(count,result.Count-count);return result;
        }
        void FireTurret(LifelineEnemy target)
        {
            // The shot is a real traveling projectile: no remote damage or idle click reward.
            GameObject root = spearPrefab != null ? Instantiate(spearPrefab) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bool shipMap = target.IsAirborne;
            root.name = "TurretProjectile";
            root.transform.position = shipMap ? ShipPoint(new Vector3(11, 1.5f, 0)) : WaterShipPoint(new Vector3(11, 1.5f, 0));
            maps?.MoveGameplayObject(root, shipMap);
            if (spearPrefab == null) root.transform.localScale = Vector3.one * .18f;
            Collider shotCollider = root.GetComponent<Collider>(); if (shotCollider != null) shotCollider.isTrigger = true;
            LifelineSpear shot = root.GetComponent<LifelineSpear>(); if (shot == null) shot = root.AddComponent<LifelineSpear>();
            shot.session = this; shot.owner = captain; shot.turretShot = true; shot.target = target;
            shot.onShipMap = shipMap;
            shot.ignoreShipColliders = true;
            shot.direction = (target.transform.position - root.transform.position).normalized; shot.damage = target.IsAirborne ? 1f : harpoonDamage;
            var shotMesh = root.GetComponentInChildren<MeshRenderer>();
            if (spearMaterial != null && shotMesh != null) shotMesh.sharedMaterial = spearMaterial;
        }

        void UpdateWeather(float dt)
        {
            if(!StormActive){stormWarning=0;return;}
            if(stormWarning>0)
            {
                stormWarning=Mathf.Max(0,stormWarning-dt);
                if(stormWarning<=0)stormCountdown=UnityEngine.Random.Range(LightningInterval.x,LightningInterval.y);
                return;
            }
            stormCountdown-=dt;if(stormCountdown>0)return;
            stormWarning=LightningWarningSeconds;
            Vector3 point=ShipToWaterPoint(ShipPoint(new Vector3(UnityEngine.Random.Range(-10.5f,10.5f),.9f,0)));
            QueueAreaStrike(AreaStrikeKind.Lightning,point,true,tuning.lightningRadius,LightningWarningSeconds,lightningHullDamage);
            Tell("雷击落点已标记，离开预警圆圈或进入船舱。");
        }

        public float GetStormDriftSpeed()
        {
            if (phase != SessionPhase.Playing || !StormActive || tuning == null) return 0;
            float drift = Mathf.Sin(elapsed * tuning.stormDriftFrequency) * tuning.stormDriftSpeed;
            if (GetUpgrade(UpgradeKind.Sail) >= 2) drift *= tuning.sailT2DriftMultiplier;
            CrewActor helm = ship != null ? ship.helmOperator : null;
            if (helm != null && helm.onboard && !helm.downed && !helm.controlsCaptured && Mathf.Abs(helm.LastCommand.move.x) > .01f) drift *= tuning.activeSteeringDriftMultiplier;
            return drift;
        }
    }
}
