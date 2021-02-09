/*using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using ew;

public class StackOverFlowTest : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

    public class TestSystem : SystemBase
    {
        public NativeArray<Vector3> positions;

        public static int MAX = 10000;

        protected override void OnCreate()
        {
            positions = new NativeArray<Vector3>(MAX, Allocator.Persistent);
        }

        protected override void OnUpdate()
        {
            var copyToNativeHandle = Entities
                .WithNativeDisableParallelForRestriction(positions)
                .ForEach((ref Translation p, ref Boid b) =>
            {
                positions[b.boidId] = p.Value;
            })
            .ScheduleParallel(this.Dependency);

            var boidJob = Entities
                .WithNativeDisableParallelForRestriction(positions)
                .ForEach((ref Translation p, ref Boid b) =>
            {

            }
            )
            .ScheduleParallel(copyToNativeHandle);

            this.Dependency


        }

        protected override void OnDestroy()
        {
            positions.Dispose();
        }
    }
    */
