using System.Linq;
using VoronoiLib;
using VoronoiLib.Structures;

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.VisualScripting;
using UnityEditor.U2D;

public class VoronoiTest2D : MonoBehaviour
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

        //fortunes algorithm computes voronoi diagram
        edges = FortunesAlgorithm.Run(sites, object_bounds.min.x, object_bounds.min.y, object_bounds.max.x, object_bounds.max.y);

        //now using diagram to create the fracture pieces
        foreach (FortuneSite site in sites)
        {
            List<Vector2> cell_vertices = new List<Vector2>();

            foreach (VEdge edge in edges)
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

            cell_vertices = OrderPolygonVerts(cell_vertices);
            if (cell_vertices.Count < 3)
            {
                continue;
            }

            Sprite cell_sprite = CreateSpriteFromPolygon(cell_vertices);

            GameObject fragment = new GameObject("Fragment");
            fragment.transform.position = transform.position;
            fragment.transform.rotation = transform.rotation;
            fragment.transform.localScale = transform.localScale;

            //add components to the fragment game object
            SpriteRenderer sprite_renderer = fragment.AddComponent<SpriteRenderer>();
            sprite_renderer.sprite = cell_sprite;
            sprite_renderer.material = GetComponent<SpriteRenderer>().material;
            Rigidbody2D rigidbody_2D = fragment.AddComponent<Rigidbody2D>();
            rigidbody_2D.gravityScale = 1.0f;

            PolygonCollider2D polygon_collider_2D = fragment.AddComponent<PolygonCollider2D>();
            polygon_collider_2D.SetPath(0, cell_vertices.ToArray());
            polygon_collider_2D.offset = Vector2.zero;

        }

        GetComponent<SpriteRenderer>().enabled = true;
        GetComponent<Collider2D>().enabled = true;

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

    Sprite CreateSpriteFromPolygon(List<Vector2> _vertices_2D)
    {
        Triangulator triangulator = new Triangulator(_vertices_2D.ToArray());
        ushort[] indices = triangulator.TriangulateUShort().Select(i => (ushort)i).ToArray();

        Vector2[] vertices_2D = _vertices_2D.ToArray();

        Sprite sprite = Sprite.Create(
            texture: Texture2D.whiteTexture, //change this to get og object texture
            rect: new Rect(0, 0, 1, 1),
            pivot: Vector2.zero,
            pixelsPerUnit: 1f,
            extrude: 0,
            meshType: SpriteMeshType.FullRect,
            border: Vector4.zero,
            generateFallbackPhysicsShape: false
            );

        sprite.OverrideGeometry(vertices_2D, indices);

        return sprite;
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
