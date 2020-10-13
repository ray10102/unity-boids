using System.Collections.Generic;
using UnityEngine;
interface IBoidBehavior
{
    // Gets the affect of the boid force on the current frame
    Vector3 getAffect(Boid boid, List<Boid> boids);
}