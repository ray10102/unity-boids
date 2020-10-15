using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class DotGenerator : MonoBehaviour
{
    public int count = 200;
    public float angle = Mathf.PI / 4f;
    public float turnFraction = 0.6180f;
    private List<Vector3> points = new List<Vector3>();

    void Update()
    {
        GetPoints();
        foreach (Vector3 point in points)
        {
            if (Mathf.Acos(Vector3.Dot(point, transform.forward)) < angle)
            {
                Debug.DrawLine(transform.position, point, Color.green);
            }
            else
            {
                Debug.DrawLine(transform.position, point, Color.red);
            }
        }
        Debug.DrawLine(transform.position, transform.forward * 2f, Color.yellow);
    }

    void GetPoints()
    {
        points.Clear();
        for (int i = 0; i < count; i++)
        {
            float t = i / (count - 1f);
            float phi = Mathf.Acos(1f - 2f * t);
            float theta = 2 * Mathf.PI * turnFraction * i;

            float x = math.sin(phi) * math.cos(theta);
            float y = math.sin(phi) * math.sin(theta);
            float z = math.cos(phi);
            points.Add(new Vector3(x, y, z));
        }
    }

    public static NativeArray<float3> GetPoints(int count, float rad, float turnFraction = 0.6180f)
    {
        Debug.Log("in");
        List<float3> points = new List<float3>();
        for (int i = 0; i < count; i++)
        {
            float t = i / (count - 1f);
            float phi = Mathf.Acos(1f - 2f * t);
            float theta = 2 * Mathf.PI * turnFraction * i;

            float x = math.sin(phi) * math.cos(theta);
            float y = math.sin(phi) * math.sin(theta);
            float z = math.cos(phi);
            float3 p = new float3(x, y, z);
            if (math.acos(math.dot(p, new float3(0, 0, 1))) < rad)
            {
                points.Add(p);
            }
        }
        NativeArray<float3> result = new NativeArray<float3>(points.Count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        for(int i = 0; i < points.Count; i++)
        {
            result[i] = points[i];
        }
        Debug.Log("points" + points.Count);
        Debug.Log("result" + result.Length);
        return result;
    }
}
