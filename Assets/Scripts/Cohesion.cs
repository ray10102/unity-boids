using System.Collections.Generic;
using UnityEngine;
public class Cohesion
{
    public static Vector3 getAffect(Boid boid, List<Boid> otherBoids)
    {
        Vector3 average = Vector3.zero;
        int found = 0;
        foreach (Boid otherBoid in otherBoids)
        {
            if (otherBoid == boid) { continue; }
            Vector3 diff = otherBoid.transform.position - boid.transform.position;
            if (diff.magnitude < BoidConfig.Instance.cohesionRadius)
            {
                average += otherBoid.transform.position;
                found++;
            }
        }

        if (found > 0)
        {
            average = average / found;
            return (average - boid.transform.position);
        }

        return Vector3.zero;
    }
}
