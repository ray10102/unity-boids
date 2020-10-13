using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoidConfig : MonoBehaviour
{
    public static BoidConfig Instance;
    [Range(0.0f, 20.0f)]
    public float alignmentRadius;
    [Range(0.0f, 20.0f)]
    public float cohesionRadius;
    [Range(0.0f, 20.0f)]
    public float separationRadius;
    [Range(0.0f, 1f)]
    public float alignmentStrength = 0.125f;
    [Range(0.0f, .5f)]
    public float cohesionStrength = 0.01f;
    [Range(0.0f, 3f)]
    public float separationStrength = 1f;
    [Range(0.0f, 10f)]
    public float maxSpeed = 3f;
    [Range(0.0f, 1f)]
    public float minSpeed = 0.1f;
    private void Awake()
    {
        if (Instance)
        {
            Destroy(this);
        } else
        {
            Instance = this;
        }
    }
}
