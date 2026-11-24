using System.Collections.Generic;
using UnityEngine;

namespace HexaSort.Utils
{
    /// <summary>
    /// Builds flat-top hexagonal prisms (corner 0 points along +X), flat shaded, pivot at the centre
    /// unless <c>centerY</c> moves the prism's centre up or down.
    /// UV.y runs 0..1 from bottom to top edge on the side faces and is 0.5 on the caps,
    /// so the shader can darken the rim between stacked tiles. On the caps UV.x runs from 0 at the
    /// centre to 1 at the rim, which the shader uses for the glossy bevel ring.
    /// </summary>
    public static class HexMeshBuilder
    {
        public static Mesh BuildPrism(float radius, float height, string name = "HexPrism", float centerY = 0f)
        {
            var vertices = new List<Vector3>(48);
            var normals = new List<Vector3>(48);
            var uvs = new List<Vector2>(48);
            var triangles = new List<int>(72);

            float half = height * 0.5f;
            var corners = new Vector3[6];
            for (int i = 0; i < 6; i++)
            {
                float a = Mathf.Deg2Rad * 60f * i;
                corners[i] = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            }

            // Top cap (clockwise seen from above = front face in Unity).
            AddCap(vertices, normals, uvs, triangles, corners, half, Vector3.up, top: true);
            // Bottom cap.
            AddCap(vertices, normals, uvs, triangles, corners, -half, Vector3.down, top: false);

            // Sides: one flat-shaded quad per edge.
            for (int i = 0; i < 6; i++)
            {
                Vector3 c0 = corners[i];
                Vector3 c1 = corners[(i + 1) % 6];
                Vector3 normal = ((c0 + c1) * 0.5f).normalized;
                int start = vertices.Count;

                vertices.Add(c0 + Vector3.down * half);
                vertices.Add(c0 + Vector3.up * half);
                vertices.Add(c1 + Vector3.up * half);
                vertices.Add(c1 + Vector3.down * half);
                for (int k = 0; k < 4; k++) normals.Add(normal);
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(0f, 1f));
                uvs.Add(new Vector2(1f, 1f));
                uvs.Add(new Vector2(1f, 0f));

                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
            }

            var mesh = new Mesh { name = name };
            if (centerY != 0f)
                for (int i = 0; i < vertices.Count; i++) vertices[i] += new Vector3(0f, centerY, 0f);
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            return mesh;
        }

        /// <summary>Unit quad lying flat in the XZ plane, facing up, white vertex colours (shadows, glows).</summary>
        public static Mesh BuildFlatQuad(string name = "FlatQuad")
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(new List<Vector3>
            {
                new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f),
            });
            mesh.SetNormals(new List<Vector3> { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            mesh.SetUVs(0, new List<Vector2> { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) });
            mesh.SetColors(new List<Color> { Color.white, Color.white, Color.white, Color.white });
            mesh.SetTriangles(new List<int> { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddCap(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles,
            Vector3[] corners, float y, Vector3 normal, bool top)
        {
            int center = vertices.Count;
            vertices.Add(new Vector3(0f, y, 0f));
            for (int i = 0; i < 6; i++) vertices.Add(corners[i] + new Vector3(0f, y, 0f));
            for (int i = 0; i < 7; i++)
            {
                normals.Add(normal);
                uvs.Add(new Vector2(i == 0 ? 0f : 1f, 0.5f));
            }

            for (int i = 0; i < 6; i++)
            {
                int a = center + 1 + i;
                int b = center + 1 + (i + 1) % 6;
                triangles.Add(center);
                triangles.Add(top ? b : a);
                triangles.Add(top ? a : b);
            }
        }
    }
}
