using System;
using Unity.Entities;

// ReSharper disable once InconsistentNaming
[GenerateAuthoringComponent]
public struct CubeComponent : IComponentData
{
    public float RadiansPerSecond;
}