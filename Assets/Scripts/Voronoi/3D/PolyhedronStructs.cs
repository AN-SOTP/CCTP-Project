using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Polygon3D
{
    public List<Vector3> vertices;

    public Polygon3D()
    {
        vertices = new List<Vector3>();
    }

    public Polygon3D(List<Vector3> verts)
    {
        vertices = verts;
    }
}

[System.Serializable]
public class Polyhedron
{
    public List<Polygon3D> faces;

    public Polyhedron()
    {
        faces = new List<Polygon3D>();
    }
}