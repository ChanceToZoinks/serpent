﻿using UnityEngine;

namespace Snake3D {

    /** Analogue of ValueTransform for current triangle space
     * (local surface space).
     * 
     * Doesn't store scale and reference to corresponding Mesh. */
    public struct SurfaceTransform {
        public int triangleIndex;
        public Vector2 localPosition;
        
        public float angle; // In degrees
    }

    
    public class MeshWalker {
        public bool debugDrawEnabled = false;

        private SurfaceTransform surfaceTransform;
        private Matrix4x4 surfaceToWorld, worldToSurface;

        // Current triangle vertices in surface space
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
                Vector3 vertex = vertices[triangles[i].i1];
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

            surfaceTransform.triangleIndex = triangleIndex;
            surfaceTransform.angle = angle;

            UpdateMatrices(triangleIndex);
            UpdateTriangleCoords();

            /*
            // Set position to the middle of triangle
            Vector2[] t = triangleCoords;
            surfaceTransform.localPosition = (t[1] + t[2]) / 3;
            */
            surfaceTransform.localPosition = coords;
        }

        public void Rotate(float angle) {
            surfaceTransform.angle += angle;
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
                float edgeDistance = (surfaceTransform.localPosition - intersectionPoint).magnitude;
                distanceLeft = distance - edgeDistance;

                if (distanceLeft < 0) {
                    // The step haven't reached triangle sides
                    distanceLeft = 0;
                    surfaceTransform.localPosition += LocalDirection * distance;
                    return;
                }
            }

            // The edge had been reached, now we should move to the neighbor triangle

            int neighbor = GetNeighborTriangle(intersectedEdge);

            // Get coordinates and angle in neighbor triangle space
            {
                // Intersected edge direction in old triangle's space
                Vector2 edgeDirection = triangleCoords[(intersectedEdge + 1) % 3]
                                      - triangleCoords[intersectedEdge];

                // Index of intersected edge end vertex, to find corresponding edge
                // in the neighbor triangle (it will be the start vertex index there)
                int intersectedEdgeEnd = CurrentTriangle[(intersectedEdge + 1) % 3];

                // "Intermediate" angle (the angle which is equal between both triangle's coordinate systems)
                float beta = surfaceTransform.angle - (-edgeDirection).GetAngle();

                Vector3 worldPosition = surfaceToWorld.MultiplyPoint3x4(intersectionPoint);
                surfaceTransform.triangleIndex = neighbor;
                UpdateMatrices(surfaceTransform.triangleIndex);
                UpdateTriangleCoords();

                surfaceTransform.localPosition = worldToSurface.MultiplyPoint3x4(worldPosition);

                // Intersected edge direction in new triangle's space
                int intersectedEdgeNew = 0;
                IndexedTriangle triangle = CurrentTriangle;
                for (int i = 0; i < 3; i++) {
                    if (triangle[i] == intersectedEdgeEnd)
                        intersectedEdgeNew = i;
                }
                Vector2 edgeDirectionNew = triangleCoords[(intersectedEdgeNew + 1) % 3]
                                         - triangleCoords[intersectedEdgeNew];
                float a = beta + edgeDirectionNew.GetAngle();
                surfaceTransform.angle = MathUtils.NormalizeAngle(a);

#if UNITY_EDITOR
                if (debugDrawEnabled) {
                    Vector2 center = (triangleCoords[1] + triangleCoords[2]) / 3;
                    Vector2 start = triangleCoords[intersectedEdgeNew];
                    Vector2 end   = triangleCoords[(intersectedEdgeNew + 1) % 3];
                    start = Vector2.Lerp(start, center, 0.2f);
                    end   = Vector2.Lerp(end, center, 0.2f);
                    DrawLocalLine(start, end, Color.magenta);
                }
#endif
            }
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
            Vector3 localPosition = surfaceTransform.localPosition;
            transform.position = surfaceToWorld.MultiplyPoint3x4(localPosition);


            // Rotation
            Vector3 up = CalculateSmoothedNormal();
            Vector3 forward = surfaceToWorld.MultiplyVector(LocalDirection);

            // Make forward vector orthogonal to up
            forward = Vector3.Cross(up, forward).normalized;
            forward = Vector3.Cross(forward, up);

            transform.rotation = Quaternion.LookRotation(forward, up);
        }

        #region Private

        private Vector2 LocalDirection {
            get {
                float a = surfaceTransform.angle * Mathf.Deg2Rad;
                return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            }
        }

        private IndexedTriangle CurrentTriangle {
            get {
                return triangles[surfaceTransform.triangleIndex];
            }
        }

#if UNITY_EDITOR

        /// Draws a line, converting coordinates from triangle to world space
        private void DrawLocalLine(Vector3 start, Vector3 end, Color color, bool depthTest = false, float duration = 0) {
            start = surfaceToWorld.MultiplyPoint3x4(start);
            end = surfaceToWorld.MultiplyPoint3x4(end);
            Debug.DrawLine(start, end, color, duration, depthTest);
        }
        
        private void DrawLocalGradientLine(Vector3 start, Vector3 end, Color startColor, Color endColor) {
            start = surfaceToWorld.MultiplyPoint3x4(start);
            end = surfaceToWorld.MultiplyPoint3x4(end);
            DebugUtils.DrawGradientLine(start, end, startColor, endColor, false, 0);
        }

#endif

        private Vector3 CalculateSmoothedNormal() {
            // Reference: https://www.google.com/search?q=barycentric+interpolation

            Vector3 normal = new Vector3();
            for (int i = 0; i < 3; ++i) {
                // Side start and end
                Vector3 a = triangleCoords[(i + 1) % 3];
                Vector3 b = triangleCoords[(i + 2) % 3];
                float sideLength = (a - b).magnitude;
                float height = MathUtils.LineToPointDistance(a, b, surfaceTransform.localPosition);
                float area = 0.5f * sideLength * height;
                Vector3 vertexNormal = normals[CurrentTriangle[i]];
                normal += vertexNormal * area;
            }

            return normal.normalized;
        }

        private Vector2 GetEdgeIntersection(bool[] filteredEdges, out int intersectedEdge) {
            Vector2 nearestIntersection = new Vector2();
            intersectedEdge = -1;
            float minimumDistance = float.PositiveInfinity;
            for (int i = 0; i < 3; ++i) {
                if (!filteredEdges[i])
                    continue;

                Vector2 edgeStart = triangleCoords[i];
                Vector2 edgeEnd = triangleCoords[(i + 1) % 3];
                Vector2 pos = surfaceTransform.localPosition;
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
                DrawLocalGradientLine(surfaceTransform.localPosition, nearestIntersection,
                    Color.black, Color.white);
                //Debug.Log("intersectedEdge: " + intersectedEdge);
            }
#endif

            return nearestIntersection;
        }

        /// Gets neighbor triangle index
        private int GetNeighborTriangle(int intersectedEdge) {
            // Note the reverse order of start and end indices
            IndexedEdge edge = new IndexedEdge(
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

        // The argument triangleIndex is transfered explicitly to indicate
        // that the method is dependent on it.
        private void UpdateMatrices(int triangleIndex) {
            CalculateTangentToWorldMatrix(triangleIndex, mesh, out surfaceToWorld);
            worldToSurface = surfaceToWorld.inverse;
        }

        private void UpdateTriangleCoords() {
            IndexedTriangle t = CurrentTriangle;
            // triangleCoords[0] is always (0, 0)
            triangleCoords[1] = worldToSurface.MultiplyPoint3x4(vertices[t.i2]);
            triangleCoords[2] = worldToSurface.MultiplyPoint3x4(vertices[t.i3]);
        }
        
        private static void CalculateTangentToWorldMatrix(int triangleIndex, Mesh mesh, out Matrix4x4 surfaceToWorld) {
            IndexedTriangle t = mesh.GetSaneTriangles(0)[triangleIndex];
            /* 
             *                            TODO
             * 1) Replace by:
             * RawTriangle t = mesh.GetRawTriangleByIndex(triangleIndex);
             * 
             * 2) See which other code we can move to RawTriangle
             *
             */             
            Vector3 v1 = mesh.vertices[t.i1];
            Vector3 v2 = mesh.vertices[t.i2];
            Vector3 v3 = mesh.vertices[t.i3];
            Vector3 right = (v2 - v1).normalized;
            Vector3 forward = Vector3.Cross(right, v3 - v1).normalized;
            Vector3 up = Vector3.Cross(forward, right);

            surfaceToWorld = new Matrix4x4();
            surfaceToWorld.SetColumn(0, right);
            surfaceToWorld.SetColumn(1, up);
            surfaceToWorld.SetColumn(2, forward);
            Vector3 p = v1;
            surfaceToWorld.SetColumn(3, new Vector4(p.x, p.y, p.z, 1));
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