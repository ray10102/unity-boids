using System.Collections.Generic;
using UnityEngine;
public class Separation
{
    public static Vector3 getAffect(Boid boid, List<Boid> otherBoids)
    {
        Vector3 c = Vector3.zero;
        foreach (Boid otherBoid in otherBoids)
        {
            if (otherBoid == boid) { continue; }
            if ((otherBoid.transform.position - boid.transform.position).magnitude < BoidConfig.Instance.separationRadius)
            {
                c = c - (otherBoid.transform.position - boid.transform.position);
            }
        }

        return c;
    }
}
