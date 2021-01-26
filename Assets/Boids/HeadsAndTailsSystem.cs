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

    [UpdateAfter(typeof(SpineSystem))]
    public class HeadsAndTailsSystem : JobComponentSystem
    {
        public BoidBootstrap bootstrap;

        public static HeadsAndTailsSystem Instance;

        protected override void OnCreateManager()
        {
            Instance = this;
            bootstrap = GameObject.FindObjectOfType<BoidBootstrap>();
            Enabled = false;
        }
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            // Animate the head and tail
            var headJob = new HeadJob()
            {
                positions = SpineSystem.Instance.positions,
                rotations = SpineSystem.Instance.rotations,
                speeds = BoidJobSystem.Instance.speeds,
                dT = Time.deltaTime * bootstrap.speed,
                amplitude = bootstrap.headAmplitude,
                frequency = bootstrap.animationFrequency,
                size = bootstrap.size
            };

            var headHandle = headJob.Schedule(this, inputDeps);// Animate the head and tail

            var tailJob = new TailJob()
            {
                positions = SpineSystem.Instance.positions,
                rotations = SpineSystem.Instance.rotations,
                speeds = BoidJobSystem.Instance.speeds,
                dT = Time.deltaTime * bootstrap.speed,
                amplitude = bootstrap.tailAmplitude,
                frequency = bootstrap.animationFrequency,
                size = bootstrap.size
            };

            return tailJob.Schedule(this, headHandle);
        }

    }
}