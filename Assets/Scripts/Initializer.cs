using System.Collections.Generic;
using UnityEngine;

public class Initializer : MonoBehaviour
{
    public GameObject boidPrefab;
    public int count = 10;
    public Vector3 offset = Vector3.zero;
    public float radius;
    public List<Boid> boids = new List<Boid>();
    public float speed = 2f;
    // Start is called before the first frame update
    void Start()
    {
       for(int i = 0; i < count; i++)
        {
            boids.Add(Object.Instantiate(boidPrefab, Random.insideUnitSphere * radius + offset, Random.rotation).GetComponent<Boid>());
            boids[i].velocity = Random.insideUnitSphere * speed;
        }
        
       foreach(Boid boid in boids)
        {
            boid.allBoids = boids;
        }
    }
}
