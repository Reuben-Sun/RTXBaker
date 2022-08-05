using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class LightProbeItem : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f,1f,1f,0.5f);
        Gizmos.DrawCube(transform.position, Vector3.one);
    }

    private void Start()
    {
        
    }
}
