using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace ew
{
    public struct Spine : IComponentData
    {
        public int parent;
        public int spineId;
        public Vector3 offset;
    }

    [BurstCompile]
    [UpdateAfter(typeof(BoidJobSystem))]
    public class SpineSystem : SystemBase
    {
        public BoidBootstrap bootstrap;

        public NativeArray<Vector3> positions;
        public NativeArray<Quaternion> rotations;

        public const int MAX_SPINES = 500000;
        public int numSpines = 0;

        public static SpineSystem Instance;

        protected override void OnCreate()
        {
            Instance = this;
            bootstrap = GameObject.FindObjectOfType<BoidBootstrap>();

            positions = new NativeArray<Vector3>(MAX_SPINES, Allocator.Persistent);
            rotations = new NativeArray<Quaternion>(MAX_SPINES, Allocator.Persistent);
            numSpines = 0;
            
            Enabled = false;
        }

        protected override void OnStartRunning()
        {
            bootstrap = GameObject.FindObjectOfType<BoidBootstrap>();            
        }

        protected override void OnDestroy()
        {
            positions.Dispose();
            rotations.Dispose();
        }

        protected override void OnUpdate()
        {
            NativeArray<Vector3> positions = this.positions;
            NativeArray<Quaternion> rotations = this.rotations;
            BoidBootstrap bootstrap = this.bootstrap;
            float dT = Time.DeltaTime;
            float bondDamping = bootstrap.bondDamping;
            float angularBondDamping = bootstrap.angularDamping;

            var ctjHandle = Entities
                .WithNativeDisableParallelForRestriction(positions)
                .WithNativeDisableParallelForRestriction(rotations)
                .ForEach((ref Translation p, ref Rotation r, ref Spine s) =>
            {
                positions[s.spineId] = p.Value;
                rotations[s.spineId] = r.Value;
            })
            .ScheduleParallel(this.Dependency);
        
            var spineHandle = Entities
                .WithNativeDisableParallelForRestriction(positions)
                .WithNativeDisableParallelForRestriction(rotations)                
                .ForEach((ref Spine s, ref Translation p, ref Rotation r) =>
            {
                // Is it the root of a spine?
                if (s.parent == -1)
                {
                    return;
                }
                Vector3 wantedPosition = positions[s.parent] + rotations[s.parent] * s.offset;
                //p.Value = Vector3.Lerp(p.Value, wantedPosition, bondDamping * dT);

                // Clamp the distance
                Vector3 lerpedPosition = Vector3.Lerp(p.Value, wantedPosition, bondDamping * dT);
                Vector3 clampedOffset = lerpedPosition - positions[s.parent];
                clampedOffset = Vector3.ClampMagnitude(clampedOffset, s.offset.magnitude);
                //positions[s.spineId] = Vector3.Lerp(positions[s.spineId], wantedPosition, bondDamping * dT);
                positions[s.spineId] = positions[s.parent] + clampedOffset;
                Vector3 myPos = positions[s.spineId];
                Quaternion wantedQuaternion = Quaternion.LookRotation(positions[s.parent] - myPos);
                rotations[s.spineId] = Quaternion.Slerp(rotations[s.spineId], wantedQuaternion, angularBondDamping * dT);
                //r.Value = Quaternion.Slerp(r.Value, wantedQuaternion, angularBondDamping * dT);
            }
            )
            .ScheduleParallel(ctjHandle);

            var cfJHandle = Entities
                .WithNativeDisableParallelForRestriction(positions)
                .WithNativeDisableParallelForRestriction(rotations)                
                .ForEach((ref Translation p, ref Rotation r, ref Spine s) =>
            {
                p.Value = positions[s.spineId];
                r.Value = rotations[s.spineId];
            }    
            )
            .ScheduleParallel(spineHandle);
            this.Dependency = cfJHandle;
            return;
        }
    }
}


