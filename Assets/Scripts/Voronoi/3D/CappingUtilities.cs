using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CappingUtilities
{
    public class VertexNode
    {
        public Vector3 position;
        // We store edges that start at this node.
        // Each edge is a reference to the other node, plus an "EdgeID" or something.
        public List<HalfEdge> edges = new List<HalfEdge>();

        public VertexNode(Vector3 pos)
        {
            position = pos;
        }
    }

    // A half-edge storing a reference to the "end" node
    public class HalfEdge
    {
        public VertexNode endNode;

        public HalfEdge(VertexNode e)
        {
            endNode = e;
        }
    }

    /// <summary>
    /// Build robust loops from intersection edges, removing used edges 
    /// so we can't re-traverse them infinitely.
    /// </summary>
    public static List<List<Vector3>> BuildRobustCapLoops(List<VoronoiTest3D.Edge3D> edges, float epsilon = 1e-6f)
    {
        // 1) Merge near-duplicate vertices
        // 2) Build adjacency as half-edges
        // 3) Extract loops by systematically removing used edges
        // 4) Return final loops

        List<Vector3> uniquePositions = new List<Vector3>();
        Dictionary<Vector3, int> positionToIndex = new Dictionary<Vector3, int>(new Vector3Comparer(epsilon));

        // For building the node list
        List<(int idxA, int idxB)> edgePairs = new List<(int, int)>();

        // Step 1: gather edges, merge endpoints
        foreach (var e in edges)
        {
            int iA = FindOrAddPosition(uniquePositions, positionToIndex, e.start, epsilon);
            int iB = FindOrAddPosition(uniquePositions, positionToIndex, e.end, epsilon);
            if (iA != iB)
            {
                edgePairs.Add((iA, iB));
            }
        }

        // Step 2: create VertexNode for each unique position
        List<VertexNode> nodes = new List<VertexNode>(uniquePositions.Count);
        for (int i = 0; i < uniquePositions.Count; i++)
        {
            nodes.Add(new VertexNode(uniquePositions[i]));
        }

        // Build adjacency with half-edges
        foreach (var (idxA, idxB) in edgePairs)
        {
            var nA = nodes[idxA];
            var nB = nodes[idxB];

            // add half-edge A->B
            nA.edges.Add(new HalfEdge(nB));
            // add half-edge B->A
            nB.edges.Add(new HalfEdge(nA));
        }

        // Step 3: find loops by systematically "using" edges
        // We'll store them in a final list
        List<List<Vector3>> loops = new List<List<Vector3>>();

        // We'll systematically attempt to build loops from each node's edges
        // removing edges as we go so we can't infinitely loop
        for (int i = 0; i < nodes.Count; i++)
        {
            VertexNode startNode = nodes[i];

            // while this node still has edges
            while (startNode.edges.Count > 0)
            {
                // attempt building a loop from the first half-edge
                HalfEdge e0 = startNode.edges[0];
                List<Vector3> loop = BuildLoopFromHalfEdge(startNode, e0, epsilon);

                if (loop.Count >= 3)
                {
                    loops.Add(loop);
                }
                // if it’s partial or fails, we discard
            }
        }

        return loops;
    }

    /// <summary>
    /// Attempt to build a loop by walking half-edges until we come back to 'startNode'
    /// or run out of edges. Each half-edge we use is removed from adjacency so we can't re-traverse it.
    /// </summary>
    private static List<Vector3> BuildLoopFromHalfEdge(VertexNode startNode, HalfEdge initialEdge, float epsilon)
    {
        List<Vector3> path = new List<Vector3>();
        path.Add(startNode.position);

        VertexNode currentNode = startNode;
        HalfEdge currentEdge = initialEdge;

        // Remove the edge from adjacency
        RemoveHalfEdge(currentNode, currentEdge);

        // Follow it
        VertexNode nextNode = currentEdge.endNode;
        path.Add(nextNode.position);

        VertexNode prevNode = currentNode;
        currentNode = nextNode;

        int safetyCount = 0;

        while (true)
        {
            safetyCount++;
            if (safetyCount > 10000)
            {
                // prevents infinite loop
                Debug.LogError("BuildLoopFromHalfEdge: Exceeded iteration limit. Breaking to avoid freeze.");
                path.Clear();
                return path;
            }

            // find a half-edge from currentNode that isn't pointing back to prevNode
            HalfEdge nextEdge = FindUnvisitedEdge(currentNode, prevNode);
            if (nextEdge == null)
            {
                // can't continue, partial chain
                path.Clear();
                return path;
            }

            RemoveHalfEdge(currentNode, nextEdge);
            nextNode = nextEdge.endNode;
            path.Add(nextNode.position);

            // check if nextNode is startNode
            if ((nextNode.position - startNode.position).sqrMagnitude < epsilon * epsilon)
            {
                // closed
                break;
            }

            prevNode = currentNode;
            currentNode = nextNode;
        }

        // remove last if same as first
        if ((path[path.Count - 1] - path[0]).sqrMagnitude < epsilon * epsilon)
            path.RemoveAt(path.Count - 1);

        if (path.Count < 3)
            path.Clear();

        return path;
    }

    /// <summary>
    /// Find an edge from 'node' that leads to a next node 
    /// that isn't 'prevNode'. If none exist, returns null.
    /// </summary>
    private static HalfEdge FindUnvisitedEdge(VertexNode node, VertexNode prevNode)
    {
        for (int i = 0; i < node.edges.Count; i++)
        {
            // we pick the first edge that doesn't go back to prevNode
            if (node.edges[i].endNode != prevNode)
            {
                return node.edges[i];
            }
        }
        return null;
    }

    /// <summary>
    /// Removes 'edgeToRemove' from 'node''s list of half-edges
    /// (the one that matches endNode).
    /// </summary>
    private static void RemoveHalfEdge(VertexNode node, HalfEdge edgeToRemove)
    {
        // remove the first instance that has the same endNode
        for (int i = 0; i < node.edges.Count; i++)
        {
            if (node.edges[i].endNode == edgeToRemove.endNode)
            {
                node.edges.RemoveAt(i);
                return;
            }
        }
    }

    // Merge or find existing position close to 'pos' within 'epsilon'
    private static int FindOrAddPosition(
        List<Vector3> uniquePositions,
        Dictionary<Vector3, int> positionToIndex,
        Vector3 pos, float epsilon
    )
    {
        if (!positionToIndex.TryGetValue(pos, out int index))
        {
            index = uniquePositions.Count;
            uniquePositions.Add(pos);
            positionToIndex[pos] = index;
        }
        return index;
    }

    /// <summary>
    /// A custom comparer that treats positions within epsilon as equal.
    /// Ensures dictionary lookups treat near-duplicates as the same key.
    /// </summary>
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
