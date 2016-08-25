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


    // TODO: (?) remove dependency on Transform (at the other hand, it forces not to allocate MeshWalker)
    [RequireComponent(typeof(Transform))]
    public class MeshWalker : MonoBehaviour, IInitializable {
        [NotNull]
        public MeshFilter meshFilter;
        public bool debugDrawEnabled = false;

        private TangentTransform tangentTransform;
        private Matrix4x4 tangentToWorld, worldToTangent;

        // Current triangle vertices in tangent space
        private Vector2[] triangleCoords;

        private Mesh mesh;
        private TriangleArray triangles;
        private Vector3[] vertices;
        private Vector3[] normals;

        public void Init() {
            mesh = meshFilter.mesh;

            triangles = new TriangleArray(mesh.triangles);
            vertices = mesh.vertices;
            normals = mesh.normals;

            triangleCoords = new Vector2[3];
        }

#if UNITY_EDITOR
        // For debug only, walker shouldn't be dependent on Update()
        void Update() {
            if (!debugDrawEnabled)
                return;

            Vector3 right    = tangentToWorld.MultiplyPoint3x4(Vector3.right);
            Vector3 up       = tangentToWorld.MultiplyPoint3x4(Vector3.up);
            Vector3 forward  = tangentToWorld.MultiplyPoint3x4(Vector3.forward);
            Vector3 position = tangentToWorld.MultiplyPoint3x4(Vector3.zero);

            Debug.DrawLine(position, right, Color.red, 0, false);
            Debug.DrawLine(position, up, Color.green, 0, false);
            Debug.DrawLine(position, forward, Color.blue, 0, false);
        }
#endif // UNITY_EDITOR

        /// Respawns at vertex nearest to Transform
        public void RespawnNearestToTransform() {
            // Find nearest triangle to transform
            int nearestTriangle = 0;
            float nearestDistance = float.PositiveInfinity;
            Vector3 position = transform.position;

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

            WriteToTransform();
        }

        public void Rotate(float angle) {
            tangentTransform.angle += angle;
        }

        /**
         * Moves IWalker forward by \param distance, stopping if an edge has been reached
         * (in which case \param distanceLeft > 0)
         */
        public void MoveForward(float distance, out float distanceLeft) {
            // TODO: remove
            distanceLeft = Mathf.Max(0, distance - 1);

            bool[] filteredEdges = new bool[3];
            CullBackEdges(ref filteredEdges);

            int intersectedEdge;
            Vector2 intersectionPoint = GetEdgeIntersection(filteredEdges, out intersectedEdge);

            // Get neighbor triangle
            // ...

            // Get coordinates and angle in neighbor triangle space
            // ...

            Vector3[] vertices = mesh.vertices;
            Vector2 direction = LocalDirection;
            Vector2 position = tangentTransform.localPosition;
            Triangle triangle = CurrentTriangle;

            // Triangle vertices in world space
            //Vector3 v1w = vertices[triangle.v1];
            Vector3 v2w = vertices[triangle.v2];
            Vector3 v3w = vertices[triangle.v3];
            
            Vector2 v1 = Vector2.zero;
            Vector2 v2 = worldToTangent * v2w;
            Vector2 v3 = worldToTangent * v3w;
        }

        private Vector2 GetEdgeIntersection(bool[] filteredEdges, out int intersectedEdge) {
            Vector2 nearestIntersection = new Vector2();
            intersectedEdge = -1;
            float minimumDistance = float.PositiveInfinity;
            for (int i = 0; i < 3; ++i) {
                if (!filteredEdges[i])
                    continue;

                Vector2 edgeStart = triangleCoords[i];
                Vector2 edgeEnd   = triangleCoords[(i + 1) % 3];
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
                Vector3 p = tangentToWorld.MultiplyPoint3x4(nearestIntersection);
                Debug.DrawLine(p, transform.position, Color.black, 0, false);
                //Debug.Log("intersectedEdge: " + intersectedEdge);
            }
#endif

            return nearestIntersection;
        }

        /// Walker doesn't necessarily updates Transform during MoveForward and Rotate,
        /// so user needs to do it manually
        public void WriteToTransform() {
            Vector3 localPosition = tangentTransform.localPosition;
            transform.position = tangentToWorld.MultiplyPoint3x4(localPosition);

            transform.forward = tangentToWorld.MultiplyVector(LocalDirection);
            // TODO: normal
            // transform.up = normals[vertexIndex];
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
                    Vector3 startW = tangentToWorld.MultiplyPoint3x4(start);
                    Vector3 endW   = tangentToWorld.MultiplyPoint3x4(end);
                    Color color = filteredEdges[i]? Color.blue : Color.red;
                    Debug.DrawLine(startW, endW, color, 0, false);
                }
#endif
            }
        }

        #endregion Private
    }

} // namespace Snake3D