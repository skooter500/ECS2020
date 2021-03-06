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
    public struct Head : IComponentData
    {
        public float theta;
        public int spineId;
        public int boidId;
    }

    public struct Tail : IComponentData
    {
        public float theta;
        public int boidId;
        public int spineId;
    }
/*
    [BurstCompile]
    class HeadJob : SystemBase
    {
        [NativeDisableParallelForRestriction]
        public NativeArray<Vector3> positions;

        [NativeDisableParallelForRestriction]
        public NativeArray<Quaternion> rotations;

        [NativeDisableParallelForRestriction]
        public NativeArray<float> speeds;

        public float amplitude;
        public float frequency;
        public float size;
        public float dT;

        protected override void OnUpdate()
        {
            NativeArray<Vector3> positions = this.positions;
            NativeArray<Quaternion> rotations = this.rotations;
            
            Entities.ForEach((ref Head h, ref Translation p, ref Rotation r) =>
            {
                Vector3 up = Vector3.up;
                Quaternion q = rotations[h.spineId] * Quaternion.AngleAxis(Mathf.Sin(h.theta) * amplitude, up);

                // Calculate the center point of the head
                Vector3 pos = positions[h.spineId]
                    + rotations[h.spineId] * (Vector3.forward * size * 0.5f)
                    + q * (Vector3.forward * size * 0.5f);

                p.Value = pos;
                r.Value = q;

                h.theta += frequency * dT * Mathf.PI * 2.0f * speeds[h.boidId];

            })
            .ScheduleParallel();
        }

        
    }

    [BurstCompile]
    class TailJob : SystemBase
    {
        [NativeDisableParallelForRestriction]
        public NativeArray<Vector3> positions;

        [NativeDisableParallelForRestriction]
        public NativeArray<Quaternion> rotations;

        [NativeDisableParallelForRestriction]
        public NativeArray<float> speeds;

        public float amplitude;
        public float frequency;
        public float size;
        public float dT;

        protected override void OnUpdate()
        {
            Entities.ForEach((ref Tail t, ref Translation p, ref Rotation r) =>
            {
                Vector3 up = Vector3.up;
                Quaternion q = rotations[t.spineId] * Quaternion.AngleAxis(Mathf.Sin(-t.theta) * amplitude, up);
                // Calculate the center point of the tail

                //Vector3 pos = positions[t.spineId] - q * (Vector3.forward * size * 0.5f);
                Vector3 pos = positions[t.spineId]
                    - rotations[t.spineId] * (Vector3.forward * size * 0.5f)
                    - q * (Vector3.forward * size * 0.5f);

                p.Value = pos;
                r.Value = q;
                t.theta += frequency * dT * Mathf.PI * 2.0f * speeds[t.boidId];
            })
            .ScheduleParallel();
        }
    }
    */
    [BurstCompile]
    [UpdateAfter(typeof(SpineSystem))]
    public class HeadsAndTailsSystem : SystemBase
    {
        public BoidBootstrap bootstrap;

        public static HeadsAndTailsSystem Instance;

        protected override void OnCreate()
        {
            Instance = this;
            bootstrap = GameObject.FindObjectOfType<BoidBootstrap>();
            Enabled = false;
        }

        protected override void OnStartRunning()
        {
            bootstrap = GameObject.FindObjectOfType<BoidBootstrap>();            
        }

        protected override void OnUpdate()
        {
            NativeArray<Vector3> positions = SpineSystem.Instance.positions;
            NativeArray<Quaternion> rotations = SpineSystem.Instance.rotations;
            NativeArray<float> speeds = BoidJobSystem.Instance.speeds;
            float dT = Time.DeltaTime * bootstrap.speed;
            float headAmplitude = bootstrap.headAmplitude;
            float tailAmplitude = bootstrap.tailAmplitude;
            float frequency = bootstrap.animationFrequency;
            float size = bootstrap.size;

            // Animate the head and tail
            
            var headHandle = Entities
                .WithNativeDisableParallelForRestriction(positions)
                .WithNativeDisableParallelForRestriction(rotations)
                .WithNativeDisableParallelForRestriction(speeds)
                .ForEach((ref Head h, ref Translation p, ref Rotation r) =>
            {
                Vector3 up = Vector3.up;
                Quaternion q = rotations[h.spineId] * Quaternion.AngleAxis(Mathf.Sin(h.theta) * headAmplitude, up);

                // Calculate the center point of the head
                Vector3 pos = positions[h.spineId]
                    + rotations[h.spineId] * (Vector3.forward * size * 0.5f)
                    + q * (Vector3.forward * size * 0.5f);

                p.Value = pos;
                r.Value = q;

                h.theta += frequency * dT * Mathf.PI * 2.0f * speeds[h.boidId];

            })
            .ScheduleParallel(this.Dependency);

            var tailHandle = Entities
                .WithNativeDisableParallelForRestriction(positions)
                .WithNativeDisableParallelForRestriction(rotations)
                .WithNativeDisableParallelForRestriction(speeds)
                .ForEach((ref Tail t, ref Translation p, ref Rotation r) =>
            {
                Vector3 up = Vector3.up;
                Quaternion q = rotations[t.spineId] * Quaternion.AngleAxis(Mathf.Sin(-t.theta) * tailAmplitude, up);
                // Calculate the center point of the tail

                //Vector3 pos = positions[t.spineId] - q * (Vector3.forward * size * 0.5f);
                Vector3 pos = positions[t.spineId]
                    - rotations[t.spineId] * (Vector3.forward * size * 0.5f)
                    - q * (Vector3.forward * size * 0.5f);

                p.Value = pos;
                r.Value = q;
                t.theta += frequency * dT * Mathf.PI * 2.0f * speeds[t.boidId];
            })
            .ScheduleParallel(headHandle);
            this.Dependency = tailHandle;
            return;
        }
    }
}

