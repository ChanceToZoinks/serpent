﻿using UnityEngine;
using System.Collections;

namespace Snake3D {

    public static class MathUtils {
        
        /// Calculates angle of the vector in degrees. Angle belongs to [0; 360)
        public static float GetAngle(this Vector2 vector) {
            float angle = Vector2.Angle(Vector2.right, vector);
            if (vector.y < 0)
                angle = 360 - angle;

            if (angle >= 360)
                angle -= 360;

            return angle;
        }

        public static Vector2 GetLinesIntersection(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4) {
            Vector2 result = new Vector2();
            result.x = ((p1.x*p2.y - p1.y*p2.x)*(p3.x - p4.x) - (p1.x - p2.x)*(p3.x*p4.y - p3.y*p4.x))
                / ((p1.x - p2.x)*(p3.y - p4.y) - (p1.y - p2.y)*(p3.x - p4.x));
            result.y = ((p1.x*p2.y - p1.y*p2.x)*(p3.y - p4.y) - (p1.y - p2.y)*(p3.x*p4.y - p3.y*p4.x))
                / ((p1.x - p2.x)*(p3.y - p4.y) - (p1.y - p2.y)*(p3.x - p4.x));
            return result;
        }
    }

} // namespace Snake3D