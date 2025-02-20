using System.Collections.Generic;
using MIConvexHull;
using UnityEngine;

public static class TetraAdjacency
{
    public static Dictionary<TetraCell, List<TetraCell>> BuildAdjacencyGraph(
        DelaunayTriangulation<TetraVertex, TetraCell> tetra_mesh)
    {
        Dictionary<TetraCell, List<TetraCell>> adjacency = new Dictionary<TetraCell, List<TetraCell>>();
        Dictionary<string, List<TetraCell>> face_map = new Dictionary<string, List<TetraCell>>();

        foreach (TetraCell cell in tetra_mesh.Cells)
        {
            if (!adjacency.ContainsKey(cell))
            {
                adjacency[cell] = new List<TetraCell>();
            }

            TetraVertex[] verts = cell.Vertices;
            if (verts == null || verts.Length < 4) continue;

            //each tetrahedron has 4 triangular faces:
            //Face A: (0,1,2)
            //Face B: (0,1,3)
            //Face C: (0,2,3)
            //Face D: (1,2,3)

            AddFaceToMap(verts[0], verts[1], verts[2], cell, face_map);
            AddFaceToMap(verts[0], verts[1], verts[3], cell, face_map);
            AddFaceToMap(verts[0], verts[2], verts[3], cell, face_map);
            AddFaceToMap(verts[1], verts[2], verts[3], cell, face_map);
        }

        //each face signature in face_map now points to 1 or more tetrahedra that share that face
        //if a face is shared by exactly 2 tetrahedra, they are neighbors
        foreach (var kvp in face_map)
        {
            List<TetraCell> shared_cells = kvp.Value;
            if (shared_cells.Count == 2)
            {
                TetraCell c0 = shared_cells[0];
                TetraCell c1 = shared_cells[1];

                adjacency[c0].Add(c1);
                adjacency[c1].Add(c0);
            }
        }

        return adjacency;
    }

    private static void AddFaceToMap(TetraVertex a, TetraVertex b, TetraVertex c,
                                     TetraCell cell,
                                     Dictionary<string, List<TetraCell>> face_map)
    {
        //create a unique signature for the face, can sort the vertex positions or use a stable ID
        //here we convert to string with sorted floats
        Vector3 A = ToV3(a);
        Vector3 B = ToV3(b);
        Vector3 C = ToV3(c);

        //aort them by something stable, we'll sort by x, then y, then z:
        Vector3[] sorted = new Vector3[] { A, B, C };
        System.Array.Sort(sorted, (p1, p2) => CompareVectors(p1, p2));

        string face_key =
            sorted[0].x + "_" + sorted[0].y + "_" + sorted[0].z + "_" +
            sorted[1].x + "_" + sorted[1].y + "_" + sorted[1].z + "_" +
            sorted[2].x + "_" + sorted[2].y + "_" + sorted[2].z;

        if (!face_map.ContainsKey(face_key))
        {
            face_map[face_key] = new List<TetraCell>();
        }
        face_map[face_key].Add(cell);
    }

    private static int CompareVectors(Vector3 v1, Vector3 v2)
    {
        if (v1.x != v2.x)
        {
            return v1.x.CompareTo(v2.x);
        }
        if (v1.y != v2.y)
        {
            return v1.y.CompareTo(v2.y);
        }

        return v1.z.CompareTo(v2.z);
    }

    private static Vector3 ToV3(TetraVertex tv)
    {
        return new Vector3((float)tv.Position[0], (float)tv.Position[1], (float)tv.Position[2]);
    }

    public static int CountConnectedComponentSize(
        Dictionary<TetraCell, List<TetraCell>> adjacency,
        TetraCell start_cell)
    {
        HashSet<TetraCell> visited = new HashSet<TetraCell>();
        Stack<TetraCell> stack = new Stack<TetraCell>();
        stack.Push(start_cell);

        while (stack.Count > 0)
        {
            TetraCell current = stack.Pop();
            if (!visited.Contains(current))
            {
                visited.Add(current);
                if (adjacency.TryGetValue(current, out List<TetraCell> neighbors))
                {
                    for (int i = 0; i < neighbors.Count; i++)
                    {
                        TetraCell n = neighbors[i];
                        if (!visited.Contains(n))
                        {
                            stack.Push(n);
                        }
                    }
                }
            }
        }
        return visited.Count;
    }

    //groups tetrahedra into lumps of up to max_size
    //adjacency: dictionary of TetraCell -> List<TetraCell>
    public static List<List<TetraCell>> ClusterTetrahedra(Dictionary<TetraCell, List<TetraCell>> adjacency, int max_size)
    {
        List<List<TetraCell>> clusters = new List<List<TetraCell>>();
        HashSet<TetraCell> visited = new HashSet<TetraCell>();

        //iterate over all tetrahedra in adjacency
        foreach (TetraCell cell in adjacency.Keys)
        {
            if (visited.Contains(cell))
                continue;

            //start new cluster
            List<TetraCell> current_cluster = new List<TetraCell>();
            Stack<TetraCell> stack = new Stack<TetraCell>();
            stack.Push(cell);

            while (stack.Count > 0)
            {
                TetraCell current = stack.Pop();
                if (!visited.Contains(current))
                {
                    visited.Add(current);
                    current_cluster.Add(current);

                    //if this hasn't reached the max_size limit, continue exploring
                    if (current_cluster.Count < max_size)
                    {
                        if (adjacency.TryGetValue(current, out List<TetraCell> neighbors))
                        {
                            for (int i = 0; i < neighbors.Count; i++)
                            {
                                TetraCell n = neighbors[i];
                                if (!visited.Contains(n))
                                {
                                    stack.Push(n);
                                }
                            }
                        }
                    }
                }
            }

            clusters.Add(current_cluster);
        }

        return clusters;
    }
}