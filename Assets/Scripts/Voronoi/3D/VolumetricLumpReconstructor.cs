using System.Collections.Generic;
using UnityEngine;

public static class VolumetricLumpReconstructor
{
    public static Mesh BuildMeshFromLump(List<TetraCell> lump, int grid_resolution = 16, float iso_level = 0.5f)
    {
        Vector3 min, max;
        GetLumpBounds(lump, out min, out max);
        Vector3 padding = (max - min) * 0.05f;
        min -= padding;
        max += padding;

        int nx = grid_resolution, ny = grid_resolution, nz = grid_resolution;
        float[,,] density = new float[nx, ny, nz];
        Vector3 grid_size = max - min;
        Vector3 cell_size = new Vector3(grid_size.x / (nx - 1), grid_size.y / (ny - 1), grid_size.z / (nz - 1));

        for (int i = 0; i < nx; i++)
        {
            for (int j = 0; j < ny; j++)
            {
                for (int k = 0; k < nz; k++)
                {
                    Vector3 point = new Vector3(min.x + i * cell_size.x, min.y + j * cell_size.y, min.z + k * cell_size.z);
                    density[i, j, k] = IsPointInsideLump(point, lump) ? 1f : 0f;
                }
            }
        }

        return MarchingCubes.GenerateMesh(density, min, cell_size, iso_level);
    }

    private static void GetLumpBounds(List<TetraCell> lump, out Vector3 min, out Vector3 max)
    {
        min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        foreach (var cell in lump)
        {
            foreach (var v in cell.Vertices)
            {
                Vector3 p = new Vector3((float)v.Position[0], (float)v.Position[1], (float)v.Position[2]);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
        }
    }

    private static bool IsPointInsideLump(Vector3 point, List<TetraCell> lump)
    {
        foreach (var cell in lump)
        {
            if (IsPointInsideTetrahedron(point, cell))
                return true;
        }
        return false;
    }

    private static bool IsPointInsideTetrahedron(Vector3 p, TetraCell cell)
    {
        TetraVertex[] verts = cell.Vertices;
        if (verts == null || verts.Length < 4)
            return false;
        Vector3 v0 = ToV3(verts[0]);
        Vector3 v1 = ToV3(verts[1]);
        Vector3 v2 = ToV3(verts[2]);
        Vector3 v3 = ToV3(verts[3]);

        float vol = SignedTetraVolume(v0, v1, v2, v3);
        float v0p = SignedTetraVolume(p, v1, v2, v3);
        float v1p = SignedTetraVolume(v0, p, v2, v3);
        float v2p = SignedTetraVolume(v0, v1, p, v3);
        float v3p = SignedTetraVolume(v0, v1, v2, p);

        float tol = 1e-5f;
        if (Mathf.Abs(vol) < tol)
            return false;
        if (Mathf.Abs((v0p + v1p + v2p + v3p) - vol) > tol)
            return false;

        bool has_neg = (v0p < -tol) || (v1p < -tol) || (v2p < -tol) || (v3p < -tol);
        bool has_pos = (v0p > tol) || (v1p > tol) || (v2p > tol) || (v3p > tol);
        return !(has_neg && has_pos);
    }

    private static float SignedTetraVolume(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        return Vector3.Dot(a - d, Vector3.Cross(b - d, c - d)) / 6f;
    }

    private static Vector3 ToV3(TetraVertex tv)
    {
        return new Vector3((float)tv.Position[0], (float)tv.Position[1], (float)tv.Position[2]);
    }
}
