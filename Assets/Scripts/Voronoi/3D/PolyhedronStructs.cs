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

    public Polyhedron Clone()
    {
        Polyhedron copy = new Polyhedron();
        for (int i = 0; i < faces.Count; i++)
        {
            Polygon3D face = faces[i];
            List<Vector3> new_verts = new List<Vector3>(face.vertices.Count);
            for (int j = 0; j < face.vertices.Count; j++)
            {
                new_verts.Add(face.vertices[j]);
            }
            Polygon3D face_copy = new Polygon3D(new_verts);
            copy.faces.Add(face_copy);
        }
        return copy;
    }
}