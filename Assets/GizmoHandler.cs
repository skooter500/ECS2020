using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;


//From: https://forum.unity.com/threads/draw-gizmos-within-componentsystems.538881/

public class MyGizmoHandler : MonoBehaviour
{
    public Action DrawGizmos;
    public Action DrawGizmosSelected;

    private void OnDrawGizmos()
    {
        DrawGizmos?.Invoke();
    }

    private void OnDrawGizmosSelected()
    {
        DrawGizmosSelected?.Invoke();
    }
}

public static class MyGizmo
{
    // some other helper methods here
 
    public static void OnDrawGizmos(Action action)
    {
        Handler.DrawGizmos += action;
    }
 
    private static MyGizmoHandler Handler => _handler != null ? _handler : (_handler = CreateHandler());
    private static MyGizmoHandler _handler;
 
    private static MyGizmoHandler CreateHandler()
    {
        var go = new GameObject("Gizmo Handler");
        go.hideFlags = HideFlags.DontSave;
 
        return go.AddComponent<MyGizmoHandler>();
    }
   
}