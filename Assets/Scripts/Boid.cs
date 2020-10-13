using System;
using System.Collections.Generic;
using UnityEngine;

public class Boid : MonoBehaviour
{
    public Vector3 velocity;
    public List<Boid> allBoids;

    // Update is called once per frame
    void Update()
    {
        velocity += Cohesion.getAffect(this, allBoids) * BoidConfig.Instance.cohesionStrength;
        velocity += Separation.getAffect(this, allBoids) * BoidConfig.Instance.separationStrength;
        velocity += Alignment.getAffect(this, allBoids) * BoidConfig.Instance.alignmentStrength;
        if (velocity.magnitude > BoidConfig.Instance.maxSpeed)
        {
            velocity = Vector3.Normalize(velocity) * BoidConfig.Instance.maxSpeed;
        } 
        else if (velocity.magnitude < BoidConfig.Instance.minSpeed)
        {
            velocity = Vector3.Normalize(velocity) * BoidConfig.Instance.minSpeed;
        }
        transform.position += velocity * Time.deltaTime;
        transform.rotation = Quaternion.LookRotation(velocity);
    }
}
