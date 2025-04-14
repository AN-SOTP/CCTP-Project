using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CappingUtilities
{
    public class VertexNode
    {
        public Vector3 position;
        public List<HalfEdge> edges = new List<HalfEdge>();

        public VertexNode(Vector3 pos)
        {
            position = pos;
        }
    }

    public class HalfEdge
    {
        public VertexNode end_node;

        public HalfEdge(VertexNode end)
        {
            end_node = end;
        }
    }

    /// <summary>
    ///Build robust loops from intersection edges, removing used edges 
    /// </summary>
    public static List<List<Vector3>> BuildRobustCapLoops(List<VoronoiTest3D.Edge3D> edges, float epsilon = 1e-6f)
    {

        List<Vector3> unique_positions = new List<Vector3>();
        Dictionary<Vector3, int> position_to_index = new Dictionary<Vector3, int>(new Vector3Comparer(epsilon));

        List<(int idxA, int idxB)> edge_pairs = new List<(int, int)>();

        foreach (var e in edges)
        {
            int iA = FindOrAddPosition(unique_positions, position_to_index, e.start, epsilon);
            int iB = FindOrAddPosition(unique_positions, position_to_index, e.end, epsilon);
            if (iA != iB)
            {
                edge_pairs.Add((iA, iB));
            }
        }

        List<VertexNode> nodes = new List<VertexNode>(unique_positions.Count);
        for (int i = 0; i < unique_positions.Count; i++)
        {
            nodes.Add(new VertexNode(unique_positions[i]));
        }

        foreach (var (idxA, idxB) in edge_pairs)
        {
            var nA = nodes[idxA];
            var nB = nodes[idxB];

            nA.edges.Add(new HalfEdge(nB));
            nB.edges.Add(new HalfEdge(nA));
        }

        List<List<Vector3>> loops = new List<List<Vector3>>();

        for (int i = 0; i < nodes.Count; i++)
        {
            VertexNode start_node = nodes[i];

            while (start_node.edges.Count > 0)
            {
                HalfEdge e0 = start_node.edges[0];
                List<Vector3> loop = BuildLoopFromHalfEdge(start_node, e0, epsilon);

                if (loop.Count >= 3)
                {
                    loops.Add(loop);
                }
            }
        }

        return loops;
    }

    /// <summary>
    ///Attempt to build a loop by walking half-edges until we come back to start_noden or run out of edgfes
    /// </summary>
    private static List<Vector3> BuildLoopFromHalfEdge(VertexNode start_node, HalfEdge initial_edge, float epsilon)
    {
        List<Vector3> path = new List<Vector3>();
        path.Add(start_node.position);

        VertexNode current_node = start_node;
        HalfEdge current_edge = initial_edge;

        RemoveHalfEdge(current_node, current_edge);

        VertexNode next_node = current_edge.end_node;
        path.Add(next_node.position);

        VertexNode previous_node = current_node;
        current_node = next_node;

        int safety_count = 0;

        while (true)
        {
            safety_count++;
            if (safety_count > 10000)
            {
                //prevents infinite loop
                Debug.LogError("BuildLoopFromHalfEdge: Exceeded iteration limit. Breaking to avoid freeze.");
                path.Clear();
                return path;
            }

            HalfEdge next_edge = FindUnvisitedEdge(current_node, previous_node);
            if (next_edge == null)
            {
                path.Clear();
                return path;
            }

            RemoveHalfEdge(current_node, next_edge);
            next_node = next_edge.end_node;
            path.Add(next_node.position);

            if ((next_node.position - start_node.position).sqrMagnitude < epsilon * epsilon)
            {
                break;
            }

            previous_node = current_node;
            current_node = next_node;
        }

        if ((path[path.Count - 1] - path[0]).sqrMagnitude < epsilon * epsilon)
        {
            path.RemoveAt(path.Count - 1);
        }

        if (path.Count < 3)
        {
            path.Clear();
        }

        return path;
    }

    /// <summary>
    /// Find an edge from node that leads to a next node that isn't previous_node. If none exist, returns null.
    /// </summary>
    private static HalfEdge FindUnvisitedEdge(VertexNode node, VertexNode previous_node)
    {
        for (int i = 0; i < node.edges.Count; i++)
        {
            //pick the first edge that doesn't go back to prevNode
            if (node.edges[i].end_node != previous_node)
            {
                return node.edges[i];
            }
        }
        return null;
    }

    private static void RemoveHalfEdge(VertexNode node, HalfEdge edge_to_remove)
    {
        for (int i = 0; i < node.edges.Count; i++)
        {
            if (node.edges[i].end_node == edge_to_remove.end_node)
            {
                node.edges.RemoveAt(i);
                return;
            }
        }
    }

    private static int FindOrAddPosition(List<Vector3> unique_positions, Dictionary<Vector3, int> position_to_index, Vector3 pos, float epsilon)
    {
        if (!position_to_index.TryGetValue(pos, out int index))
        {
            index = unique_positions.Count;
            unique_positions.Add(pos);
            position_to_index[pos] = index;
        }
        return index;
    }

    private class Vector3Comparer : IEqualityComparer<Vector3>
    {
        private float epsilon;
        public Vector3Comparer(float eps)
        {
            epsilon = eps;
        }
        public bool Equals(Vector3 a, Vector3 b)
        {
            return (a - b).sqrMagnitude < (epsilon * epsilon);
        }
        public int GetHashCode(Vector3 obj)
        {
            int x = Mathf.RoundToInt(obj.x / epsilon);
            int y = Mathf.RoundToInt(obj.y / epsilon);
            int z = Mathf.RoundToInt(obj.z / epsilon);
            int h = 17;
            h = h * 31 + x;
            h = h * 31 + y;
            h = h * 31 + z;
            return h;
        }
    }
}
