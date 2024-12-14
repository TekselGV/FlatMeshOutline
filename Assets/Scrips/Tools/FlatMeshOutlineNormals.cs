using System.Collections.Generic;
using Unity.VisualScripting.Dependencies.NCalc;
using UnityEditor;
using UnityEngine;

[ExecuteInEditMode]
public class FlatMeshOutlineNormals : MonoBehaviour
{
    private const float ExtrudeEdgeWidth = 0.5f;
    private const float ExtrudeEdgeDepth = 0.1f;
    
    [SerializeField] private bool _run;

    [Space(5f)]
    [SerializeField] private MeshFilter _meshFilter;
    [SerializeField] private MeshFilter _testResultMeshFilter;
    
    [SerializeField] private bool _shouldExtrudeEdges;

    private void Update()
    {
        if (_run)
        {
            CalculateAverageNormals(_meshFilter.sharedMesh);
            _run = false;
        }
    }

    private void CalculateAverageNormals(Mesh mesh)
    {
        var processedMesh = CreateMeshCopy(mesh);
        var allEdges = GetEdgeDatasFromMesh(processedMesh);
        var outerEdges = GetOuterEdgesFromEdges(allEdges);

        GenerateOuterNormalsForMesh(processedMesh, outerEdges);

        SaveMesh(processedMesh);
    }

    private Mesh CreateMeshCopy(Mesh mesh)
    {
        var meshCopy = new Mesh
        {
            vertices = mesh.vertices,
            triangles = mesh.triangles,
            uv = mesh.uv,
            normals = mesh.normals
        };

        if (meshCopy.normals.Length == 0 || meshCopy.normals.Length != meshCopy.vertices.Length)
            meshCopy.RecalculateNormals();
        
        return meshCopy;
    }
    
    private List<EdgeData> GetEdgeDatasFromMesh(Mesh mesh)
    {
        var triangles = mesh.triangles;
        var vertices = mesh.vertices;
        var normals = mesh.normals;

        var edgeDatas = new List<EdgeData>();

        // Run through all triangles and form up data objects by triangles with corresponding with vertex indices,
        // vertex positions and average normal
        for (var i = 0; i < triangles.Length; i += 3)
        {
            var indices = new int[3];
            var positions = new Vector3[3];
            var triangleNormals = new Vector3[3];

            var positionsSum = Vector3.zero;
            for (var j = 0; j < 3; j++)
            {
                indices[j] = triangles[i + j];
                positions[j] = vertices[indices[j]];
                triangleNormals[j] = normals[indices[j]];

                positionsSum += positions[j];
            }
            
            var triangleCenter = positionsSum / 3f;
            // Calculate triangle normal from vertex order.
            // Source: https://stackoverflow.com/questions/19350792/calculate-normal-of-a-single-triangle-in-3d-space
            var triangleNormal = Vector3.Cross(positions[1] - positions[0], positions[2] - positions[0]).normalized;
            
            var edgeData0 = new EdgeData(positions[0], indices[0], positions[1], indices[1], triangleCenter, triangleNormal);
            var edgeData1 = new EdgeData(positions[1], indices[1], positions[2], indices[2], triangleCenter, triangleNormal);
            var edgeData2 = new EdgeData(positions[2], indices[2], positions[0], indices[0], triangleCenter, triangleNormal);

            edgeDatas.Add(edgeData0);
            edgeDatas.Add(edgeData1);
            edgeDatas.Add(edgeData2);
        }

        return edgeDatas;
    }

    private List<EdgeData> GetOuterEdgesFromEdges(List<EdgeData> allEdges)
    {
        var outerEdges = new List<EdgeData>();
        for (var i = 0; i < allEdges.Count; i++)
        {
            if (allEdges[i].HasTwin)
                continue;

            var hasTwin = false;
            for (var j = i + 1; j < allEdges.Count - 1; j++)
            {
                if (allEdges[i].Approximate(allEdges[j]))
                {
                    hasTwin = true;
                    allEdges[j].HasTwin = true;

                    break;
                }
            }

            if (hasTwin == false)
                outerEdges.Add(allEdges[i]);
        }

        return outerEdges;
    }

    private void GenerateOuterNormalsForMesh(Mesh mesh, List<EdgeData> outerEdges)
    {
        foreach (var edge in outerEdges)
        {
            Debug.DrawLine(edge.Point1, edge.Point2, Color.green, 10f);
            Debug.LogError($"edge: <b>{edge.Point1}</b>, P2: <b>{edge.Point2}</b>");
        }
        
        var outerVertices = new List<int>();
        foreach (var edgeData in outerEdges)
        {
            if (!outerVertices.Contains(edgeData.Point1Index))
                outerVertices.Add(edgeData.Point1Index);
            
            if (!outerVertices.Contains(edgeData.Point2Index))
                outerVertices.Add(edgeData.Point2Index);
        }

        // Create copy of mesh normals and UVs
        var newNormals = mesh.normals;
        var newUVs = new Vector3[mesh.uv.Length];

        for (var i = 0; i < newUVs.Length; i++)
            newUVs[i] = new Vector3(mesh.uv[i].x, mesh.uv[i].y, 0f);
        
        // Get two connected Edges and average their normals
        foreach (var vertexIndex in outerVertices)
        {
            var outerEdgeNormals = new List<Vector3>();
            
            foreach (var edge in outerEdges)
                if (edge.Point1Index == vertexIndex || edge.Point2Index == vertexIndex)
                    outerEdgeNormals.Add(edge.OuterNormal);

            if (outerEdgeNormals.Count != 2)
            {
                Debug.LogWarning($"outerEdgeNormals Count is {outerEdgeNormals.Count} for {vertexIndex} and should be 2. " +
                               "\nMesh might have topology errors");
                continue;                
            }

            // We make length correction to maintain the uniform outline width in shader,
            // so the normals length have width coefficient built inside
            var angle = Vector3.Angle(outerEdgeNormals[0], outerEdgeNormals[1]) / 2f;
            var length = 1 / Mathf.Cos(angle * Mathf.Deg2Rad);
            var averageNormal = (outerEdgeNormals[0] + outerEdgeNormals[1]).normalized * length;
            
            Debug.DrawRay(mesh.vertices[vertexIndex], averageNormal, Color.blue, 10f);

            newNormals[vertexIndex] = averageNormal;
            newUVs[vertexIndex].z = 1f; // Mask out the vertices with averaged normals
        }

        if (!_shouldExtrudeEdges)
        {
            mesh.SetUVs(0, newUVs);
            mesh.SetNormals(newNormals);
            
            return;
        }
        
        // Create copy of mesh normals and UVs
        var newExtrudedVertices = new List<Vector3>();
        newExtrudedVertices.AddRange(mesh.vertices);

        var newExtrudedTriangles = new List<int>();
        newExtrudedTriangles.AddRange(mesh.triangles);
        
        var newExtrudedNormals = new List<Vector3>();
        newExtrudedNormals.AddRange(mesh.normals);
        
        var newExtrudedUVs = new List<Vector3>();
        newExtrudedUVs.AddRange(newUVs);

        var originalVertexCount = newExtrudedVertices.Count;
        // Extrude along normals
        for (var i = 0; i < outerVertices.Count; i++)
        {
            var extrudedVertex = mesh.vertices[outerVertices[i]] + newNormals[outerVertices[i]] * ExtrudeEdgeWidth;
            newExtrudedVertices.Add(extrudedVertex);
            newExtrudedNormals.Add(newNormals[outerVertices[i]]);
            var newUV = new Vector3(newUVs[outerVertices[i]].x, newUVs[outerVertices[i]].y, 1f);
            newExtrudedUVs.Add(newUV);
            
            var newVertexIndex = originalVertexCount + i;
            foreach (var edge in outerEdges)
            {
                if (edge.Point1Index == outerVertices[i])
                    edge.ExtrudedPoint1Index = newVertexIndex;

                if (edge.Point2Index == outerVertices[i])
                    edge.ExtrudedPoint2Index = newVertexIndex;
            }
        }

        foreach (var outerEdge in outerEdges)
        {
            var triangleData = new int[6];
            
            triangleData[0] = outerEdge.Point1Index;
            triangleData[1] = outerEdge.ExtrudedPoint1Index;
            triangleData[2] = outerEdge.Point2Index;
            
            triangleData[3] = outerEdge.Point2Index;
            triangleData[4] = outerEdge.ExtrudedPoint1Index;
            triangleData[5] = outerEdge.ExtrudedPoint2Index;
            
            newExtrudedTriangles.AddRange(triangleData);
        }

        mesh.SetVertices(newExtrudedVertices);
        mesh.triangles = newExtrudedTriangles.ToArray();
        mesh.SetUVs(0, newExtrudedUVs);
        mesh.SetNormals(newExtrudedNormals);

        //mesh.Optimize();
    }
    
    private void SaveMesh(Mesh mesh)
    {
        var path = "Assets/MeshWithOutlineNormals.mesh";
        AssetDatabase.CreateAsset(mesh, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        _testResultMeshFilter.sharedMesh = AssetDatabase.LoadAssetAtPath(path, typeof(Mesh)) as Mesh;
        
        Debug.Log($"Mesh saved, path: <b>{path}</b>");
        
    }
    
    public class EdgeData
    {
        internal readonly int Point1Index;
        internal readonly Vector3 Point1;
        internal readonly int Point2Index;
        internal readonly Vector3 Point2;
        internal readonly Vector3 OuterNormal;
    
        internal int ExtrudedPoint1Index;
        internal int ExtrudedPoint2Index;
        internal bool HasTwin;

        public EdgeData(Vector3 point1, int point1Index, Vector3 point2, int point2Index, Vector3 triangleCenter, Vector3 triangleNormal)
        {
            Point1Index = point1Index;
            Point2Index = point2Index;
            Point1 = point1;
            Point2 = point2;

            var edgeCenter = (point1 + point2) / 2;
      
            var direction1 = point1 - point2;
            var direction2 = point2 - point1;
      
            var edgeNormal1 = Vector3.Cross(direction1, triangleNormal).normalized;
            var edgeNormal2 = Vector3.Cross(direction2, triangleNormal).normalized;

            var edgeCenterToTriangleCenterDirection = triangleCenter - edgeCenter;
            var angle1 = Vector3.Angle(edgeNormal1, edgeCenterToTriangleCenterDirection);
            var angle2 = Vector3.Angle(edgeNormal2, edgeCenterToTriangleCenterDirection);
      
            OuterNormal = angle1 < angle2 ? edgeNormal2 : edgeNormal1;
        }

        public bool Approximate(EdgeData edgeData) =>
            (Point1Index == edgeData.Point1Index && Point2Index == edgeData.Point2Index) || 
            (Point1Index == edgeData.Point2Index && Point2Index == edgeData.Point1Index);
            //(Point1 == edgeData.Point1 && Point2 == edgeData.Point2) || (Point1 == edgeData.Point2 && Point2 == edgeData.Point1);
    }
}
