﻿using UnityEngine;

public static class Utils {

    public static T FindNearestParentWithComponent<T>(Transform trans) where T : Component {
        for (; trans != null; trans = trans.parent) {
            T component = trans.GetComponent<T>();
            if (component != null)
                return component;
        }

        return null;
    }

    public static Vector2 GetUnscaledTextureOffset(this Material material) {
        Vector2 result = material.mainTextureOffset;
        Vector2 scale = material.mainTextureScale;
        result.x /= scale.x;
        result.y /= scale.y;
        return result;
    }

    public static void SetUnscaledTextureOffset(this Material material, Vector2 offset) {
        Vector2 result = offset;
        result.Scale(material.mainTextureScale);
        material.mainTextureOffset = result;
    }
}
