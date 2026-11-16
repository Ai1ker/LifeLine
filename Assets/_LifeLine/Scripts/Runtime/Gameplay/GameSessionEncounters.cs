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
            foreach (Vector3 point in SelectInitialSharkSpawnPoints()) SpawnEnemy(EnemyKind.Shark, point);
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
                        int index = selected.Count % 4 == 3 ? candidates.Count - 1 : 0;
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

        public void RefreshWorldForDay(int newDay)
        {
            if (world == null) return;
            foreach (LifelineEnemy enemy in enemies.ToArray())
            {
                if (enemy == null || !enemy.IsShark) continue;
                enemies.Remove(enemy); enemy.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(enemy.gameObject); else DestroyImmediate(enemy.gameObject);
            }
            world.RefreshDay(newDay);
            SpawnInitialSharks();
            Tell("新的一天，海底遗迹与物资已刷新。已挂载货箱、背包和船上升级保留。");
        }

        public LifelineEnemy SpawnEnemy(EnemyKind kind, Vector3 position)
        {
            if (kind == EnemyKind.Shark || kind == EnemyKind.WhiteShark)
            {
                float surface = world != null ? world.WaterSurface : -1.5f;
                if (surface - position.y < tuning.sharkMinimumDepth) return null;
                int count = 0; foreach (LifelineEnemy existing in enemies) if (existing != null && existing.IsShark && existing.IsAlive) count++;
                if (count >= tuning.maximumSharks) return null;
            }
            GameObject prefab = kind == EnemyKind.Shadow ? shadowPrefab : kind == EnemyKind.Bird || kind == EnemyKind.ArmoredBird ? birdPrefab : sharkPrefab;
            if (prefab == null && world != null) prefab = kind == EnemyKind.Shadow ? world.shadowPrefab : kind == EnemyKind.Shark || kind == EnemyKind.WhiteShark ? world.sharkPrefab : null;
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
            if (IsShadowSpawnAllowed && !shadowSpawnedThisNight && !shadowRepelled)
            {
                shadowSpawnedThisNight = true;
                SpawnEnemy(EnemyKind.Shadow, BoatAnchor + new Vector3(12, -16, 0));
                Tell("深渊巨影正在靠近。每次横扫前都有明显蓄力，命中20次可将它逼退。");
            }
            if (IsNight)
            {
                float rate = LightEffective && GetUpgrade(UpgradeKind.Searchlight) >= 1 ? tuning.upgradedLightBirdRate : 1;
                birdCountdown -= dt * rate;
                if (birdCountdown <= 0)
                {
                    birdCountdown += Mathf.Max(1, tuning.birdInterval);
                    float side = UnityEngine.Random.value < .5f ? -1 : 1;
                    SpawnEnemy(day >= tuning.mutationStartDay && UnityEngine.Random.value < tuning.armoredBirdChance ? EnemyKind.ArmoredBird : EnemyKind.Bird, ShipPoint(new Vector3(side * 16, 6, 0)));
                }
            }
            UpdateTurret(dt);
        }

        public void AttractEnemies(Vector3 point)
        {
            foreach (LifelineEnemy enemy in enemies)
                if (enemy != null && (enemy.kind == EnemyKind.Shark || enemy.kind == EnemyKind.WhiteShark) && Vector3.Distance(enemy.transform.position, point) < tuning.sharkNightDetectionRange * 1.5f) enemy.Alert(point);
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
            if (!actor.onboard && !hasHarpoon) return false;
            meleeCooldowns[(int)actor.role] = tuning.meleeCooldown;
            float damage = (hasHarpoon ? harpoonDamage : meleeDamage) * (Buffs(actor).attack > 0 ? 1 + tuning.fishAttackBonus : 1);
            float range = tuning.meleeRange * (hasHarpoon ? 1 : .65f);
            LifelineEnemy closest = null; float nearest = range;
            Vector2 facing = actor.Facing.sqrMagnitude > .1f ? actor.Facing.normalized : Vector2.right;
            foreach (LifelineEnemy enemy in enemies)
            {
                if (enemy == null || !enemy.IsAlive) continue;
                // The ship deck and seabed are separate maps. Attacks against the shadow
                // use the matching point at the small surface boat, never the distant cabin.
                if (!actor.onboard && enemy.IsAirborne) continue;
                Vector3 attackOrigin = actor.onboard && !enemy.IsAirborne ? ShipToWaterPoint(actor.Position) : actor.Position;
                Vector3 offset = enemy.transform.position - attackOrigin;
                float distance = Mathf.Max(0, offset.magnitude - enemy.hitRadius);
                if (distance > nearest || Vector2.Dot(facing, new Vector2(offset.x, offset.y).normalized) < -.25f) continue;
                if (actor.onboard && !enemy.IsAirborne && enemy.kind != EnemyKind.Shadow && enemy.transform.position.y < -2) continue;
                closest = enemy; nearest = distance;
            }
            if (closest != null) { closest.ReceiveHit(damage, actor); return true; }
            return true;
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
            spear.damage = spearDamage * (Buffs(actor).attack > 0 ? 1 + tuning.fishAttackBonus : 1);
            if (spearMaterial != null && root.GetComponentInChildren<Renderer>() != null) root.GetComponentInChildren<Renderer>().sharedMaterial = spearMaterial;
            activeSpears[index] = spear; return true;
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
            turretInstalled = true; turretOn = true; turretPowerClock = turretShotClock = 0;
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
            LifelineEnemy target = null;
            foreach (LifelineEnemy enemy in enemies)
            {
                if (enemy == null || !enemy.IsAlive) continue;
                if (enemy.IsAirborne && Vector3.Distance(enemy.transform.position, ShipPoint(boatAnchorLocal)) < 20) { target = enemy; break; }
                if (enemy.kind == EnemyKind.Shadow && Vector3.Distance(enemy.transform.position, BoatAnchor) < 28) target = enemy;
            }
            if (target == null) return;
            turretShotClock = 0;
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
            shot.direction = (target.transform.position - root.transform.position).normalized; shot.damage = 1;
            if (spearMaterial != null && root.GetComponentInChildren<Renderer>() != null) root.GetComponentInChildren<Renderer>().sharedMaterial = spearMaterial;
        }

        void UpdateWeather(float dt)
        {
            if (!StormActive) { stormWarning = 0; return; }
            if (stormWarning > 0)
            {
                stormWarning -= dt;
                if (stormWarning <= 0)
                {
                    CrewActor actor = lightningTarget == CrewRole.Captain ? captain : diver;
                    if (actor != null && actor.onboard && ship != null && ship.transform.InverseTransformPoint(actor.Position).y > -2)
                    {
                        actor.SetDowned(true); actor.controlsCaptured = false;
                        DamageHull(lightningHullDamage);
                        Tell("雷击击倒了" + GameNames.Role(actor.role) + "！同伴靠近按交互救助，船舱可躲避雷击。");
                    }
                    // Exactly one probability roll per strike, independent of frame rate.
                    if (engineRepaired && UnityEngine.Random.value < tuning.lightningEngineRisk) DamageEngine("雷击损坏了引擎");
                    stormCountdown = UnityEngine.Random.Range(tuning.lightningIntervalMin, tuning.lightningIntervalMax);
                }
            }
            else
            {
                stormCountdown -= dt;
                if (stormCountdown <= 0)
                {
                    lightningTarget = UnityEngine.Random.value < .5f ? CrewRole.Captain : CrewRole.Diver;
                    stormWarning = tuning.lightningWarning;
                    Tell("雷击预警：" + GameNames.Role(lightningTarget) + "尽快进入船舱！");
                }
            }
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
