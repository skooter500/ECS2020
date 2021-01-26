using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

// This system updates all entities in the scene with both a RotationSpeed_ForEach and Rotation component.

// ReSharper disable once InconsistentNaming
public class NoiseSystem : SystemBase
{
    float t = 0;
    float scale= 15;
    // OnUpdate runs on the main thread.
    protected override void OnUpdate()
    {
        float t = (float) Time.ElapsedTime;
        float s = this.scale;
        // Schedule job to rotate around up vector
        Entities
            .WithName("CubeComponent")
            .ForEach((ref Scale scale, ref Translation translation, in CubeComponent cubeComponent) =>
            {
                float nn = Utilities.Map(Perlin.Noise(translation.Value[0] + t, translation.Value[1] + t, translation.Value[2] + t ), -1, 1, 0, s);
                float3 sc = new float3(nn, nn, nn);
                scale.Value = nn;            
            })
            .ScheduleParallel();
                /*
                Vector3 sc = new Vector3(nn, nn, nn);
                transform.localScale = sc;
                rotation.Value = math.mul(
                    math.normalize(rotation.Value),
                    quaternion.AxisAngle(math.up(), rotationSpeed.RadiansPerSecond * deltaTime));
                    */
    }
}
