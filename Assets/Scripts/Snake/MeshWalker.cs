﻿using UnityEngine;

namespace Snake3D {

    /** Analogue of Transform for mesh tangent-space.
     * 
     * Doesn't store scale and reference to corresponding Mesh. */
    public struct TangentTransform {
        public int triangleIndex;
        public Vector2 localPosition;
        public float angle;
    }

    
    public class MeshWalker {
        public bool debugDrawEnabled = false;

        private TangentTransform tangentTransform;
        private Matrix4x4 tangentToWorld, worldToTangent;

        // Current triangle vertices in tangent space
        private Vector2[] triangleCoords;

        private Mesh mesh;
        private TriangleArray triangles;
        private Vector3[] vertices;
        private Vector3[] normals;

        public MeshWalker(Mesh mesh) {
            this.mesh = mesh;

            triangles = new TriangleArray(mesh.triangles);
            vertices = mesh.vertices;
            normals = mesh.normals;

            triangleCoords = new Vector2[3];
        }

        /// Respawns at vertex nearest to Transform
        public void RespawnNearPoint(Vector3 position) {
            // Find nearest triangle
            int nearestTriangle = 0;
            float nearestDistance = float.PositiveInfinity;

            for (int i = 0; i < triangles.Length; ++i) {
                Vector3 vertex = vertices[triangles[i].v1];
                float distance = (position - vertex).sqrMagnitude;
                if (distance < nearestDistance) {
                    // Found new nearest triangle
                    nearestDistance = distance;
                    nearestTriangle = i;
                }
            }
            
            RespawnAtTriangle(nearestTriangle, 0);
        }

        /*public void RespawnAtDefaultPlace() {
            // TODO: throw vertical ray from (0, 0, 0) to (0, +Infinity, 0)
            throw new System.NotImplementedException();
        }*/

        public void RespawnRandomly() {
            // Not true randomness, but enough for our purposes
            
            int triangleIndex = Random.Range(0, triangles.Length);
            RespawnAtTriangle(triangleIndex, Random.Range(0f, 360f));
        }

        public void RespawnAtTriangle(int triangleIndex, float angle, Vector2 coords = new Vector2()) {
            if (triangleIndex < 0 || triangleIndex > triangles.Length)
                throw new System.IndexOutOfRangeException();

            tangentTransform.triangleIndex = triangleIndex;
            tangentTransform.angle = angle;

            UpdateMatrices(triangleIndex);
            UpdateTriangleCoords();

            /*
            // Set position to the middle of triangle
            Vector2[] t = triangleCoords;
            tangentTransform.localPosition = (t[1] + t[2]) / 3;
            */
            tangentTransform.localPosition = coords;
        }

        public void Rotate(float angle) {
            tangentTransform.angle += angle;
        }

        /**
         * Moves IWalker forward by \param distance, stopping if an edge has been reached
         * (in which case \param distanceLeft > 0)
         */
        public void StepUntilEdge(float distance, out float distanceLeft) {
            bool[] filteredEdges = new bool[3];
            CullBackEdges(ref filteredEdges);

            int intersectedEdge;
            Vector2 intersectionPoint = GetEdgeIntersection(filteredEdges, out intersectedEdge);
            
            // Have we reached the edge?
            {
                float edgeDistance = (tangentTransform.localPosition - intersectionPoint).magnitude;
                distanceLeft = distance - edgeDistance;

                if (distanceLeft < 0) {
                    // The step haven't reached triangle sides
                    distanceLeft = 0;
                    tangentTransform.localPosition += LocalDirection * distance;
                    return;
                }
            }

            // Get neighbor triangle
            int neighbor = GetNeighborTriangle(intersectedEdge);

            // Get coordinates and angle in neighbor triangle space
            // ...
            
        }

#if UNITY_EDITOR
        public void DebugDrawAxes() {
            if (!debugDrawEnabled)
                return;

            DrawLocalLine(Vector3.zero, Vector3.right, Color.red);
            DrawLocalLine(Vector3.zero, Vector3.up, Color.green);
            DrawLocalLine(Vector3.zero, Vector3.forward, Color.blue);
        }
#endif

        public void WriteToTransform(Transform transform) {
            // Position
            Vector3 localPosition = tangentTransform.localPosition;
            transform.position = tangentToWorld.MultiplyPoint3x4(localPosition);

            // Rotation
            // TODO: smooth normal
            Vector3 forward = tangentToWorld.MultiplyVector(LocalDirection);
            Vector3 up = tangentToWorld.MultiplyVector(Vector3.forward);
            transform.rotation = Quaternion.LookRotation(forward, up);
        }

        #region Private

        private Vector2 LocalDirection {
            get {
                float a = tangentTransform.angle;
                return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            }
        }

        private Triangle CurrentTriangle {
            get {
                return triangles[tangentTransform.triangleIndex];
            }
        }

#if UNITY_EDITOR

        /// Draws a line, converting coordinates from tangent to world space
        private void DrawLocalLine(Vector3 start, Vector3 end, Color color, bool depthTest=false, float duration=0) {
            start = tangentToWorld.MultiplyPoint3x4(start);
            end   = tangentToWorld.MultiplyPoint3x4(end);
            Debug.DrawLine(start, end, color, duration, depthTest);
        }

#endif

        private Vector2 GetEdgeIntersection(bool[] filteredEdges, out int intersectedEdge) {
            Vector2 nearestIntersection = new Vector2();
            intersectedEdge = -1;
            float minimumDistance = float.PositiveInfinity;
            for (int i = 0; i < 3; ++i) {
                if (!filteredEdges[i])
                    continue;

                Vector2 edgeStart = triangleCoords[i];
                Vector2 edgeEnd = triangleCoords[(i + 1) % 3];
                Vector2 pos = tangentTransform.localPosition;
                Vector2 intersection = MathUtils.GetLinesIntersection(edgeStart, edgeEnd, pos, pos + LocalDirection);
                float distance = (intersection - pos).sqrMagnitude;
                if (distance < minimumDistance) {
                    intersectedEdge = i;
                    nearestIntersection = intersection;
                    minimumDistance = distance;
                }
            }

#if UNITY_EDITOR
            if (debugDrawEnabled) {
                DrawLocalLine(tangentTransform.localPosition, nearestIntersection, Color.black);
                //Debug.Log("intersectedEdge: " + intersectedEdge);
            }
#endif

            return nearestIntersection;
        }

        private int GetNeighborTriangle(int intersectedEdge) {
            // Note the reverse order of start and end indices
            Edge edge = new Edge(
                CurrentTriangle[(intersectedEdge + 1) % 3],
                CurrentTriangle[intersectedEdge]
            );

            int neighbor = MeshIndex.instance.FindTriangleByEdge(edge);
#if UNITY_EDITOR
            if (debugDrawEnabled)
                mesh.DrawTriangle(neighbor, Color.yellow);
#endif
            return neighbor;
        }

        private void UpdateMatrices(int triangleIndex) {
            CalculateTangentToWorldMatrix(triangleIndex, mesh, out tangentToWorld);
            worldToTangent = tangentToWorld.inverse;
        }

        private void UpdateTriangleCoords() {
            Triangle t = CurrentTriangle;
            // triangleCoords[0] is always (0, 0)
            triangleCoords[1] = worldToTangent.MultiplyPoint3x4(vertices[t.v2]);
            triangleCoords[2] = worldToTangent.MultiplyPoint3x4(vertices[t.v3]);
        }
        
        private static void CalculateTangentToWorldMatrix(int triangleIndex, Mesh mesh, out Matrix4x4 tangentToWorld) {
            Triangle t = mesh.GetSaneTriangles(0)[triangleIndex];
            Vector3 v1 = mesh.vertices[t.v1];
            Vector3 v2 = mesh.vertices[t.v2];
            Vector3 v3 = mesh.vertices[t.v3];
            Vector3 right = (v2 - v1).normalized;
            Vector3 forward = Vector3.Cross(right, v3 - v1).normalized;
            Vector3 up = Vector3.Cross(forward, right);

            tangentToWorld = new Matrix4x4();
            tangentToWorld.SetColumn(0, right);
            tangentToWorld.SetColumn(1, up);
            tangentToWorld.SetColumn(2, forward);
            Vector3 p = v1;
            tangentToWorld.SetColumn(3, new Vector4(p.x, p.y, p.z, 1));
        }

        /**
         * Determine back facing edges to cull them.
         * true == edge is not culled.
         */
        private void CullBackEdges(ref bool[] filteredEdges) {
            for (int i = 0; i < 3; ++i) {
                Vector2 start = triangleCoords[i];
                Vector2 end   = triangleCoords[(i + 1) % 3];
                filteredEdges[i] = Vector3.Cross(end - start, LocalDirection).z <= 0;

#if UNITY_EDITOR
                if (debugDrawEnabled) {
                    Color color = filteredEdges[i]? Color.blue : Color.red;
                    DrawLocalLine(start, end, color);
                }
#endif
            }
        }

        #endregion Private
    }

} // namespace Snake3D