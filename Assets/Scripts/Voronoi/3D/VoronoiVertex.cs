using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MIConvexHull;
using UnityEngine;

//VoronoiVertex represents... a point (vertex) in the voronoi diagram! It implements the IVertex interface which is used by
//MIConvexHull to define a vertex
public class VoronoiVertex : IVertex
{
    //position provides the coords of the vertex, stored as an array of doubles
    public double[] Position { get; set; }

    public VoronoiVertex(double x, double y, double z)
    {
        Position = new double[] { x, y, z };
    }
    
    //converts the position to a vector3 for easier use
    public Vector3 ToVector3()
    {
        return new Vector3((float)Position[0], (float)Position[1], (float)Position[2]);
    }
}
