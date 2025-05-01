using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public static class PoissonDisk3D
{
    /// <summary>
    /// 3D Poisson Disk Sampling inside a bounding box, returning points at min_dist apart.
    /// new_points_count is how many tries per active point to generate a new candidate before it is discarded
    /// </summary>
    public static List<Vector3> Generate3D(Bounds bounds, float min_dist, int new_points_count = 30, int max_samples = 5000)
    {
        // 1. Setup
        float cell_size = min_dist / Mathf.Sqrt(3f);

        Dictionary<Vector3Int, int> grid = new Dictionary<Vector3Int, int>();
        List<Vector3> points = new List<Vector3>();
        List<int> active = new List<int>();

        Vector3Int GetCellCoords(Vector3 pt)
        {
            return new Vector3Int(
                Mathf.FloorToInt(pt.x / cell_size),
                Mathf.FloorToInt(pt.y / cell_size),
                Mathf.FloorToInt(pt.z / cell_size)
            );
        }

        Vector3 first_sample = RandomPointInBounds(bounds);
        points.Add(first_sample);
        active.Add(0);

        Vector3Int c = GetCellCoords(first_sample);
        grid[c] = 0;

        while (active.Count > 0 && points.Count < max_samples)
        {
            int active_index = Random.Range(0, active.Count);
            int point_index = active[active_index];
            Vector3 center = points[point_index];
            bool found = false;

            for (int i = 0; i < new_points_count; i++)
            {
                Vector3 new_pt = GenerateRandomPointAround(center, min_dist);
                if (!bounds.Contains(new_pt))
                    continue;

                Vector3Int cell = GetCellCoords(new_pt);

                bool ok = true;
                for (int nx = -2; nx <= 2 && ok; nx++)
                {
                    for (int ny = -2; ny <= 2 && ok; ny++)
                    {
                        for (int nz = -2; nz <= 2 && ok; nz++)
                        {
                            Vector3Int neighbor_cell = cell + new Vector3Int(nx, ny, nz);
                            if (grid.TryGetValue(neighbor_cell, out int idx))
                            {
                                // if neighbor sample is too close, fail
                                if ((points[idx] - new_pt).sqrMagnitude < (min_dist * min_dist))
                                {
                                    ok = false;
                                }
                            }
                        }
                    }
                }

                if (ok)
                {
                    points.Add(new_pt);
                    active.Add(points.Count - 1);
                    grid[cell] = points.Count - 1;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                active.RemoveAt(active_index);
            }
        }

        return points;
    }

    private static Vector3 RandomPointInBounds(Bounds b)
    {
        return new Vector3(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.max.y), Random.Range(b.min.z, b.max.z));
    }

    private static Vector3 GenerateRandomPointAround(Vector3 center, float min_dist)
    {
        float r = Random.Range(min_dist, 2f * min_dist);
        float theta = Random.Range(0f, Mathf.PI * 2f);
        float phi = Mathf.Acos(Random.Range(-1f, 1f));

        float sin_phi = Mathf.Sin(phi);
        Vector3 dir = new Vector3(sin_phi * Mathf.Cos(theta), sin_phi * Mathf.Sin(theta), Mathf.Cos(phi));
        return center + dir * r;
    }
}
