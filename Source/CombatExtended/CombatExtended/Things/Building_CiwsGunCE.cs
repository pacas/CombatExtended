﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;
using UnityEngine;
using CombatExtended.CombatExtended.LoggerUtils;
using CombatExtended.CombatExtended.Jobs.Utils;
using RimWorld.Planet;
using System.Xml;
using CombatExtended.AI;
using RimWorld.BaseGen;

namespace CombatExtended
{
    public class Building_CiwsGunCE : Building_TurretGunCE
    {
        private const int MINSHOTSREQUIRED = 30;
        private const int MAXSHOTSREQUIRED = 80;
        private const int MINBURSTCOUNT = 1;
        private const int MAXBURSTCOUNT = 5;
        private const int MAXSPREADDEGREE = 2;
        private const int TICKSBETWEENSEARCH = 10;
        private const float MINCIWSHEIGHT = 5f;
        
        private int ciwsShotsFired = 0;
        private int ciwsShotsRequired = 0;
        private int ciwsTicksSinceLastSearched = 0;
        private int ciwsTicksToNextShot = 0;
        private LocalTargetInfo ciwsTarget = null;

        #region Aming
        private float shotRotation;
        private float shotAngle;
        private Vector2 origin;
        private Vector2 destination;
        private float destinationHeight;        
        #endregion

        public override LocalTargetInfo CurrentTarget
        {
            get => ciwsTarget != null && ciwsTarget.IsValid && !ciwsTarget.ThingDestroyed ? ciwsTarget : base.CurrentTarget;
        }

        private bool IsCiwsTargetValid
        {
            get => ciwsTarget != null && ciwsTarget.IsValid && ciwsTarget.HasThing && ciwsTarget.Thing.Spawned && !ciwsTarget.ThingDestroyed;
        }

        private int _minShots = -1;
        private int MinCiwsShotsRequired
        {
            get => _minShots != -1 ? _minShots : (_minShots = def.HasModExtension<TurretDefExtensionCE>() ? def.GetModExtension<TurretDefExtensionCE>().ciwsMinShotsRequired : 40);
        }

        private int _maxShots = -1;
        private int MaxCiwsShotsRequired
        {
            get => _maxShots != -1 ? _maxShots : (_maxShots = def.HasModExtension<TurretDefExtensionCE>() ? def.GetModExtension<TurretDefExtensionCE>().ciwsMaxShotsRequired : 80);
        }     

        public Building_CiwsGunCE() : base()
        {            
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ciwsShotsFired, "Ciws_shotsFired");
            Scribe_Values.Look(ref ciwsShotsRequired, "Ciws_shotsRequired");
            Scribe_Values.Look(ref ciwsTicksSinceLastSearched, "Ciws_ticksSinceLastSearched");
            Scribe_TargetInfo.Look(ref ciwsTarget, "Ciws_target");
            Scribe_Values.Look(ref ciwsTicksToNextShot, "Ciws_ticksToNextShot");            
            // scribe for the inspector tool
#if DEBUG
            bool temp;
            temp = Active;
            Scribe_Values.Look(ref temp, "Active");
            temp = this.stunner?.Stunned ?? false;
            Scribe_Values.Look(ref temp, "Stunned");
            temp = CompAmmo.CanBeFiredNow;
            Scribe_Values.Look(ref temp, "CanBeFiredNow");
            temp = isReloading;
            Scribe_Values.Look(ref temp, "isReloading");
#endif
        }

        public override void Tick()
        {
            if (Active && (this.mannableComp == null || this.mannableComp.MannedNow) && base.Spawned)
            {
                if (!this.stunner.Stunned && CompAmmo.CanBeFiredNow)
                {
                    if (!IsCiwsTargetValid)
                    {
                        if(ciwsTicksSinceLastSearched++ > TICKSBETWEENSEARCH)
                        { 
                            ciwsTicksSinceLastSearched = 0;
                            if (TryFindProjectileTarget())
                            {
                                ciwsTicksToNextShot = 0;
                                ciwsShotsFired = 0;                                                                
                            }
                        }                 
                    }                   
                    if(IsCiwsTargetValid) 
                    {
                        ResetCurrentTarget();
                        currentTargetInt = null;
                        if (forcedTarget != null || forcedTarget.IsValid)
                        {
                            ResetForcedTarget();
                            forcedTarget = null;
                        }                                        
                        if (TryFindBurstParameters())
                        {                            
                            BurstNow();
                            if (Rand.Chance(1f / ciwsShotsRequired))
                            {                                
                                FleckMaker.ThrowFireGlow(ciwsTarget.Thing.DrawPos, Map, Rand.Range(1f, 10f));
                                FleckMaker.ThrowSmoke(ciwsTarget.Thing.DrawPos, Map, Rand.Range(1f, 10f));

                                ciwsTarget.Thing.DeSpawn(DestroyMode.Vanish);
                                ciwsTarget.Thing.Destroy();
                                Reset();
                                ResetCurrentTarget();
                            }
                        }                       
                    }                    
                }                
            }
            if (IsCiwsTargetValid)
            {
                if (GunCompEq.PrimaryVerb.state == VerbState.Bursting)
                {
                    top.TurretTopTick();
                }
            }
            else if(ciwsTarget != null)
            {
                Reset();
            }
            base.Tick();
        }

        public override void TryStartShootSomething(bool canBeginBurstImmediately)
        {
            if (!IsCiwsTargetValid)
            {
                base.TryStartShootSomething(canBeginBurstImmediately);
            }          
        }

        private bool TryFindProjectileTarget()
        {
            float rangeSquared = AttackVerb.EffectiveRange * AttackVerb.EffectiveRange;
            FlyOverProjectileTracker tracker = Map.GetComponent<FlyOverProjectileTracker>();            
            Func<ProjectileCE, bool> validatorCE = (projectile) =>
            {                
                return projectile.Spawned && projectile.launcher?.Faction != Faction && projectile.ExactPosition.y > MINCIWSHEIGHT && projectile.Position.DistanceToSquared(Position) < rangeSquared;
            };
            Func<Projectile, bool> validator = (projectile) =>
            {                
                return projectile.Spawned && projectile.launcher?.Faction != Faction && projectile.ArcHeightFactor * GenMath.InverseParabola(projectile.DistanceCoveredFraction) > MINCIWSHEIGHT && projectile.Position.DistanceToSquared(Position) < rangeSquared;
            };
            if (tracker.ProjectilesCE.Where(validatorCE).TryRandomElement(out ProjectileCE projectileCE) && !projectileCE.Destroyed)
            {
                ciwsTarget = new LocalTargetInfo(projectileCE);
                ciwsShotsRequired = GetShotsRequired(CompAmmo?.CurAmmoProjectile?.projectile.speed ?? 300f, projectileCE.shotSpeed, MinCiwsShotsRequired, MaxCiwsShotsRequired);
                return true;
            }
            if (tracker.Projectiles.Where(validator).TryRandomElement(out Projectile projectile) && !projectile.Destroyed)
            {
                ciwsTarget = new LocalTargetInfo(projectile);
                ciwsShotsRequired = GetShotsRequired(CompAmmo?.CurAmmoProjectile?.projectile.speed ?? 300f, projectile.def.projectile.speed, MinCiwsShotsRequired, MaxCiwsShotsRequired);
                return true;
            }
            return false;
        }        

        private bool TryFindBurstParameters()
        {
            Vector3 sourcePos = DrawPos;
            Vector3 targetPos;
            if (ciwsTarget.Thing is ProjectileCE projectileCE)
            {                
                targetPos = ((ProjectileCE)ciwsTarget.Thing).ExactPosition;
                destinationHeight = targetPos.y;                
            }
            else if (ciwsTarget.Thing is Projectile projectile)
            {
                targetPos = projectile.Position.ToVector3();
                destinationHeight = projectile.ArcHeightFactor* GenMath.InverseParabola(projectile.DistanceCoveredFraction);
            }
            else
            {
                Reset();
                Log.Warning("CE: Ciws target is not a ProjectileCE or Projectile");
                return false;
            }
            origin = new Vector2(sourcePos.x, sourcePos.z);
            destination = new Vector2(targetPos.x, targetPos.z);
            Vector2 w = (destination - origin);
            shotRotation = (-90 + Mathf.Rad2Deg * Mathf.Atan2(w.y, w.x) + Rand.Range(-MAXSPREADDEGREE, MAXSPREADDEGREE)) % 360;
            shotAngle = Mathf.Atan(destinationHeight / (Vector2.Distance(origin, destination) + 1e-5f));
            return true;

        }

        private void BurstNow()
        {
            ticksUntilAutoReload = minTicksBeforeAutoReload;           
            if (ciwsShotsFired > 0 && GenTicks.TicksGame % 15 == 0)
            {
                FleckMaker.ThrowSmoke(new Vector3(origin.x, 0, origin.y), Map, Rand.Range(1f, 3f));
            }
            if (ciwsTicksToNextShot-- > 0)
            {
                return;
            }
            int count = AttackVerb.verbProps.ticksBetweenBurstShots > 0 ? 1 : 2;    
            while (count-- > 0 && CompAmmo.TryReduceAmmoCount(1))
            {
                ciwsShotsFired += 1;
                if (AttackVerb.verbProps.muzzleFlashScale > 0.01f)
                {
                    FleckMaker.Static(Position, Map, FleckDefOf.ShotFlash, AttackVerb.verbProps.muzzleFlashScale);
                }
                if (AttackVerb.verbProps.soundCast != null)
                {
                    AttackVerb.verbProps.soundCast.PlayOneShot(new TargetInfo(Position, Map));
                }
                if (AttackVerb.verbProps.soundCastTail != null)
                {
                    AttackVerb.verbProps.soundCastTail.PlayOneShotOnCamera();
                }
                ProjectileCE projectile = (ProjectileCE)ThingMaker.MakeThing(CompAmmo.CurAmmoProjectile);
                projectile.Position = DrawPos.ToIntVec3();
                projectile.minCollisionDistance = 5f;
                projectile.intendedTarget = ciwsTarget.Thing;
                projectile.SpawnSetup(Map, false);
                projectile.Launch(this, origin, shotAngle, shotRotation, 1.0f, Rand.Range(Mathf.Max(projectile.def.projectile.speed * 2, 400), 600), null);
            }
            ciwsTicksToNextShot = AttackVerb.verbProps.ticksBetweenBurstShots / 2;
        }        

        private void Reset()
        {
            ResetCurrentTarget();
            ciwsTarget = null;
            ciwsTicksSinceLastSearched = TICKSBETWEENSEARCH;
            ciwsShotsFired = 0;
            ciwsShotsRequired = 0;            
        }

        private static int GetShotsRequired(float projectileSpeed, float targetSpeed, int minShots, int maxShots)
        {            
            return (int)Mathf.Lerp((float)minShots, (float)maxShots, Rand.Range(0, 1f) * targetSpeed / Mathf.Max(projectileSpeed, targetSpeed * 0.5f));
        }
    }
}

