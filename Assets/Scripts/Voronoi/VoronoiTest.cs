using System.Linq;
using VoronoiLib;
using VoronoiLib.Structures;

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.VisualScripting;
using UnityEditor.U2D;

public class VoronoiTest : MonoBehaviour
{
    PolygonCollider2D poly_collider;
    public int num_of_fragments = 10;
    List<Vector2> fracture_points;
    LinkedList<VEdge> edges;

    private void Start()
    {
        poly_collider = GetComponent<PolygonCollider2D>();
        if (poly_collider == null)
        {
            Debug.LogError("No Polygon Collider found on this GameObject! Adding one.");
            poly_collider = gameObject.AddComponent<PolygonCollider2D>();
        }
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 click_position = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            Collider2D collider = GetComponent<Collider2D>();
            if (collider == Physics2D.OverlapPoint(click_position))
            {
                StartCoroutine(FractureObject(click_position));
            }
        }
    }

    IEnumerator FractureObject(Vector2 fracture_point)
    {
        Debug.Log("Object is fractured surely!");
        
        fracture_points = new List<Vector2>();
        //convert click pos (fracture point) to local space
        fracture_points.Add(transform.InverseTransformPoint(fracture_point)); 
        
        //get the array of points that define the shape of the polygon collider
        Vector2[] path_points = poly_collider.points;

        //get object bounds in local space
        Bounds object_bounds = new Bounds();
        foreach (Vector2 point in path_points)
        {
            object_bounds.Encapsulate(point);
        }

        //generate random fracture points in local space within a certain amount of attempts
        int attempts = 0;
        while (fracture_points.Count < num_of_fragments + 1 && attempts < num_of_fragments * 10)
        {
            Vector2 random_point = new Vector2(
                Random.Range(object_bounds.min.x, object_bounds.max.x),
                Random.Range(object_bounds.min.y, object_bounds.max.y));

            // check if point is inside the polygon collider in local space
            if (IsPointInPolygon(random_point, path_points))
            {
                fracture_points.Add(random_point);
            }
            attempts++;
        }

        //convert fracture points to VoronoiLib's FortuneSite
        List<FortuneSite> sites = fracture_points.Select(p => new FortuneSite(p.x, p.y)).ToList();

        //Rect bounds_rect = new Rect(object_bounds.min.x, object_bounds.min.y, object_bounds.max.x, object_bounds.max.y);

        //run fortune algorithm to get voronoi diagram
        edges = FortunesAlgorithm.Run(sites, object_bounds.min.x, object_bounds.min.y, object_bounds.max.x, object_bounds.max.y);

        Dictionary<FortuneSite, List<VEdge>> site_edges_map = new Dictionary<FortuneSite, List<VEdge>>();
        foreach (FortuneSite site in sites)
        {
            site_edges_map[site] = new List<VEdge>();
        }
        foreach (VEdge edge in edges)
        {
            if(edge.Left != null)
            {
                site_edges_map[edge.Left].Add(edge);
            }
            if (edge.Right != null)
            {
                site_edges_map[edge.Right].Add(edge);
            }
        }

        //use diagram to create the fracture pieces
        foreach (FortuneSite site in sites)
        {
            List<Vector2> cell_vertices = new List<Vector2>();

            foreach (VEdge edge in site_edges_map[site])
            {
                if (edge.Start != null || edge.End != null)
                {
                    //start and end point of each VEdge
                    Vector2 start = new Vector2((float)edge.Start.X, (float)edge.Start.Y);
                    Vector2 end = new Vector2((float)edge.End.X, (float)edge.End.Y);

                    if (!cell_vertices.Contains(start))
                    {
                        cell_vertices.Add(start);
                    }
                    if (!cell_vertices.Contains(end))
                    {
                        cell_vertices.Add(end);
                    }
                }
            }

            cell_vertices = OrderPolygonVerts(cell_vertices);
            if (cell_vertices.Count < 3)
            {
                continue;
            }

            Mesh cell_mesh = CreateMeshFromPolygon(cell_vertices);
            
            GameObject fragment = new GameObject("Fragment");
            fragment.transform.position = transform.position;
            fragment.transform.rotation = transform.rotation;
            fragment.transform.localScale = transform.localScale;

            MeshFilter mesh_filter = fragment.AddComponent<MeshFilter>();
            mesh_filter.mesh = cell_mesh;

            MeshRenderer mesh_renderer = fragment.AddComponent<MeshRenderer>();

            //can't just GetComponent from parent mesh renderer for the material as the parent won't have one! for 2d stuff
            Material sprite_material = new Material(Shader.Find("Sprites/Default"));
            sprite_material.mainTexture = GetComponent<SpriteRenderer>().sprite.texture;
            mesh_renderer.material = sprite_material;

            Rigidbody2D rigidbody_2D = fragment.AddComponent<Rigidbody2D>();
            rigidbody_2D.gravityScale = 1.0f;

            PolygonCollider2D polygon_collider_2D = fragment.AddComponent<PolygonCollider2D>();
            polygon_collider_2D.SetPath(0,cell_vertices.ToArray());
            polygon_collider_2D.offset = Vector2.zero;

        }

        //GetComponent<Renderer>().enabled = false;
        //GetComponent<Collider2D>().enabled = false;

        yield return null;
    }

    bool IsPointInPolygon(Vector2 point, Vector2[] polygon)
    {
        int poly_points = polygon.Length;
        int j = poly_points - 1;
        bool inside = false;

        for (int i = 0; i < poly_points; i++)
        {
            if ((polygon[i].y > point.y) != (polygon[j].y > point.y))
            {
                float intersect_x = (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y + Mathf.Epsilon) + polygon[i].x;
                if (point.x < intersect_x)
                {
                    inside = !inside;
                }
            }
            j = i;
        }
        return inside;
    }

    List<Vector2> OrderPolygonVerts(List<Vector2> vertices_2D)
    {
        //get the centroid of the polygon
        Vector2 centroid = Vector2.zero;
        foreach (Vector2 vertex in vertices_2D)
        {
            centroid += vertex;
        }
        centroid /= vertices_2D.Count;

        //sort the vertices based on their angle from the centroid
        //vertices_2D.Sort((a, b) => Mathf.Atan2(a.y - centroid.y, a.x - centroid.x).CompareTo(Mathf.Atan2(b.y - centroid.y, b.x - centroid.x)));
        vertices_2D.Sort((a, b) =>
        {
            float angle_a = Mathf.Atan2(a.y - centroid.y, a.x - centroid.x);
            float angle_b = Mathf.Atan2(b.y - centroid.y, b.x - centroid.x);
            return angle_a.CompareTo(angle_b);
        });

        return vertices_2D;
    }

    Mesh CreateMeshFromPolygon(List<Vector2> vertices_2D)
    {
        //2d vertices to 3d
        Vector3[] vertices_3D = vertices_2D.Select(v => new Vector3(v.x, v.y, 0)).ToArray();

        Triangulator triangulator = new Triangulator(vertices_2D.ToArray());
        int[] indices = triangulator.Triangulate();

        Mesh mesh = new Mesh();
        mesh.vertices = vertices_3D;
        mesh.triangles = indices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    private void OnDrawGizmos()
    {
        if (fracture_points != null)
        {
            Gizmos.color = Color.green;
            foreach (Vector2 point in fracture_points)
            {
                Vector3 worldPoint = transform.TransformPoint(point); //local space point to world space
                Gizmos.DrawSphere(worldPoint, 0.05f);
            }
        }

        if (edges != null)
        {
            Gizmos.color = Color.red;
            foreach (VEdge edge in edges)
            {
                //check if edge has start and end points
                if (edge.Start != null && edge.End != null)
                {
                    //convert edge points from doubles to floats
                    Vector2 start = new Vector2((float)edge.Start.X, (float)edge.Start.Y);
                    Vector2 end = new Vector2((float)edge.End.X, (float)edge.End.Y);

                    //transform to world space
                    Vector3 worldStart = transform.TransformPoint(start);
                    Vector3 worldEnd = transform.TransformPoint(end);

                    Gizmos.DrawLine(worldStart, worldEnd);
                }
            }
        }
    }
}
