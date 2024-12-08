using MIConvexHull;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

//represents a cell in the delaunay triangulation
//inherits from TriangulationCell which is a class in MIConvexHull representing a cell in the triangulation.
public class VoronoiCell : TriangulationCell<VoronoiVertex, VoronoiCell>
{
    //circumcenter of cell
    public Vector3 Circumcenter { get; private set; }

    //computes circumcenter of the cell. if the cell has 4 vertices (a tetrahedron) then call CalculateCircumcenter with the 4 points
    //else approximate the circumcenter with the centroid of the vertices, the centroid being the average position of all the points
    public void ComputeCircumcenter()
    {
        // Get the positions of the vertices of the cell
        var points = Vertices.Select(v => v.ToVector3()).ToArray();

        // Ensure we have 4 vertices (tetrahedron)
        if (points.Length == 4)
        {
            Circumcenter = CalculateCircumcenter(points[0], points[1], points[2], points[3]);
        }
        else
        {
            // For cells that are not tetrahedra, approximate with centroid
            Circumcenter = points.Aggregate(Vector3.zero, (sum, v) => sum + v) / points.Length;
        }
    }

    private Vector3 CalculateCircumcenter(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        // Shift coordinate system so that point d is at the origin
        Vector3 a0 = a - d;
        Vector3 b0 = b - d;
        Vector3 c0 = c - d;

        float a0_mag2 = a0.sqrMagnitude;
        float b0_mag2 = b0.sqrMagnitude;
        float c0_mag2 = c0.sqrMagnitude;

        Vector3 cross_bc = Vector3.Cross(b0, c0);
        Vector3 cross_ca = Vector3.Cross(c0, a0);
        Vector3 cross_ab = Vector3.Cross(a0, b0);

        Vector3 numerator = a0_mag2 * cross_bc + b0_mag2 * cross_ca + c0_mag2 * cross_ab;
        float denominator = 2 * Vector3.Dot(a0, cross_bc);

        if (Mathf.Abs(denominator) < Mathf.Epsilon)
        {
            // Degenerate case; return the centroid as an approximation
            return (a + b + c + d) / 4f;
        }

        Vector3 circumcenter = numerator / denominator + d;
        return circumcenter;
    }

    public List<Vector3> GetCellVertices()
    {
        return Vertices.Select(v => v.ToVector3()).ToList();
    }
}