using System.Collections.Generic;
using UnityEngine;
using MIConvexHull;
using System.Linq;

public static class TetraPartitioner
{
    public static List<List<TetraCell>> Partition(DelaunayTriangulation<TetraVertex, TetraCell> tetra_mesh, List<Vector3> seeds, int refine_iterations = 2)
    {
        Dictionary<TetraCell, List<TetraCell>> adjacency = TetraAdjacency.BuildAdjacencyGraph(tetra_mesh);
        List<TetraCell> all_cells = tetra_mesh.Cells.ToList();
        int cell_count = all_cells.Count;

        Vector3[] centroids = new Vector3[cell_count];
        Dictionary<TetraCell, int> cell_to_index = new Dictionary<TetraCell, int>(cell_count);
        for (int i = 0; i < cell_count; i++)
        {
            cell_to_index[all_cells[i]] = i;
            centroids[i] = ComputeCentroid(all_cells[i]);
        }

        int[] assigned_seed = new int[cell_count];
        for (int i = 0; i < cell_count; i++)
            assigned_seed[i] = -1;

        Queue<int>[] seed_queues = new Queue<int>[seeds.Count];
        for (int s = 0; s < seeds.Count; s++)
            seed_queues[s] = new Queue<int>();

        for (int i = 0; i < cell_count; i++)
        {
            int best_seed = GetClosestSeedIndex(centroids[i], seeds);
            seed_queues[best_seed].Enqueue(i);
        }

        bool any_change = true;
        while (any_change)
        {
            any_change = false;
            for (int seed_index = 0; seed_index < seeds.Count; seed_index++)
            {
                var queue = seed_queues[seed_index];
                int count = queue.Count;
                while (count > 0)
                {
                    int cell_idx = queue.Dequeue();
                    count--;
                    if (assigned_seed[cell_idx] == -1)
                    {
                        assigned_seed[cell_idx] = seed_index;
                        any_change = true;
                        if (adjacency.TryGetValue(all_cells[cell_idx], out var neighbors))
                        {
                            foreach (var neighbor in neighbors)
                            {
                                int n_idx = cell_to_index[neighbor];
                                if (assigned_seed[n_idx] == -1)
                                {
                                    queue.Enqueue(n_idx);
                                }
                            }
                        }
                    }
                }
            }
        }

        for (int iter = 0; iter < refine_iterations; iter++)
        {
            bool updated = false;
            for (int i = 0; i < cell_count; i++)
            {
                int current_seed = assigned_seed[i];
                int best_seed = GetClosestSeedIndex(centroids[i], seeds);
                if (best_seed != current_seed)
                {
                    assigned_seed[i] = best_seed;
                    updated = true;
                }
            }
            if (!updated) break;
        }

        List<HashSet<int>> lump_indices = new List<HashSet<int>>();
        for (int s = 0; s < seeds.Count; s++)
            lump_indices.Add(new HashSet<int>());
        for (int i = 0; i < cell_count; i++)
        {
            int s = assigned_seed[i];
            lump_indices[s].Add(i);
        }

        List<List<TetraCell>> initial_lumps = new List<List<TetraCell>>();
        for (int s = 0; s < seeds.Count; s++)
        {
            if (lump_indices[s].Count > 0)
            {
                List<TetraCell> group = new List<TetraCell>();
                foreach (int idx in lump_indices[s])
                    group.Add(all_cells[idx]);
                initial_lumps.Add(group);
            }
        }

        List<List<TetraCell>> final_lumps = new List<List<TetraCell>>();
        foreach (var lump in initial_lumps)
        {
            List<TetraCell> largest_component = ExtractLargestComponent(lump, adjacency);
            if (largest_component.Count > 0)
                final_lumps.Add(largest_component);
        }

        return final_lumps;
    }

    private static Vector3 ComputeCentroid(TetraCell cell)
    {
        TetraVertex[] v = cell.Vertices;
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < 4; i++)
        {
            sum += new Vector3((float)v[i].Position[0], (float)v[i].Position[1], (float)v[i].Position[2]);
        }
        return sum * 0.25f;
    }

    private static int GetClosestSeedIndex(Vector3 pos, List<Vector3> seeds)
    {
        float best_dist = float.MaxValue;
        int best_index = -1;
        for (int i = 0; i < seeds.Count; i++)
        {
            float d = (pos - seeds[i]).sqrMagnitude;
            if (d < best_dist)
            {
                best_dist = d;
                best_index = i;
            }
        }
        return best_index;
    }

    private static List<TetraCell> ExtractLargestComponent(List<TetraCell> lump, Dictionary<TetraCell, List<TetraCell>> adjacency)
    {
        HashSet<TetraCell> lump_set = new HashSet<TetraCell>(lump);
        HashSet<TetraCell> visited = new HashSet<TetraCell>();
        List<List<TetraCell>> components = new List<List<TetraCell>>();

        foreach (var cell in lump)
        {
            if (visited.Contains(cell))
            {
                continue;
            }

            List<TetraCell> component = new List<TetraCell>();
            Queue<TetraCell> queue = new Queue<TetraCell>();
            queue.Enqueue(cell);
            visited.Add(cell);
            while (queue.Count > 0)
            {
                TetraCell current = queue.Dequeue();
                component.Add(current);
                if (adjacency.TryGetValue(current, out var neighbors))
                {
                    foreach (var neighbor in neighbors)
                    {
                        if (lump_set.Contains(neighbor) && !visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }
            components.Add(component);
        }

        if (components.Count == 0)
        {
            return new List<TetraCell>();
        }

        return components.OrderByDescending(comp => comp.Count).First();
    }
}
