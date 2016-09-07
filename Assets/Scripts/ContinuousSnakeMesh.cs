﻿using UnityEngine;
using System.Collections;
using System;

namespace Snake3D {

public class ContinuousSnakeMesh : MonoBehaviour, IInitializable, IGrowable {

    [NotNull] public MonoBehaviour snakeMesh_;
    public float interval = 0.25f;
    public bool animateUv = true;

    public ISnakeMesh snakeMesh;
    [NotNull] public Transform tail;
    
    private ValueTransform lastPoppedRing;
    private ISnakeMesh headPatch;
    private ISnakeMesh tailPatch;
    private Material tailMaterial;
    private Vector2 baseTailOffset; // Tail UV offset without sliding animation

    public void Init() {
        snakeMesh = snakeMesh_ as ISnakeMesh;
        Debug.Assert(snakeMesh != null);

        headPatch = InitPatch("Head Patch");
        tailPatch = InitPatch("Tail Patch");
        
        tailMaterial = tail.GetChild(0).GetComponent<MeshRenderer>().material;

        // Add first ring
        // TODO: replace by more sane mechanism (incorrect ring here)
        {
            ValueTransform ring = new ValueTransform(tail);

            GrowBodyMesh(ring, true);
            UpdateLastPoppedRing(ring);

            float distanceTraveled = 0;
            headPatch.PushToEnd(ring, distanceTraveled);
            headPatch.PushToEnd(ring, distanceTraveled);
            tailPatch.PushToEnd(ring, distanceTraveled);
            tailPatch.PushToEnd(ring, distanceTraveled);
        }


        snakeMesh.Kernel.OnPopFromStart += UpdateLastPoppedRing;
    }

    private ISnakeMesh InitPatch(string gameObjectName) {
        Transform patchTransform = transform.FindChild(gameObjectName);
        Debug.Assert(patchTransform != null);
        ISnakeMesh patch = patchTransform.GetComponent<SnakeMesh>();
        Debug.Assert(patch != null);
        return patch;
    }
    
    public void Grow(ValueTransform ring) {
        GrowBodyMesh(ring);
        UpdateHeadPatch(ring);
    }
    
    public void ShrinkToLength(float targetLength) {
        // Body mesh
        {
            // How many rings to remove
            float headPatchLength = GetPatchLength(headPatch);
            int targetRings = (int)((targetLength - headPatchLength) / interval) + 1;

            // Leave at least one ring
            targetRings = Mathf.Max(targetRings, 1);

            while (snakeMesh.Count > targetRings)
                snakeMesh.PopFromStart();
        }

        // Tail position
        {
            float tailPatchLength = targetLength - GetPatchLength(headPatch) - bodyLength;
            float factor = tailPatchLength / interval;

            ValueTransform tailRing = ValueTransform.lerp(snakeMesh.Kernel.Path[0], lastPoppedRing, factor);
            tailRing.SetTransform(tail);
            UpdateTailPatch(tailRing);


            // UV offset
            float distanceTraveled = bodyDistanceTraveled - bodyLength - tailPatchLength;
            baseTailOffset = tailMaterial.GetUnscaledTextureOffset();
            baseTailOffset.y = distanceTraveled / snakeMesh.RingLength;
            tailMaterial.SetUnscaledTextureOffset(baseTailOffset);
        }
    }

    public float ComputeLength() {
        return bodyLength + GetPatchLength(headPatch) + GetPatchLength(tailPatch);
    }

    public void ApplyChanges() {
        if (animateUv)
            AnimateUV();
    }

    #region Private

    private float bodyLength { get { return (snakeMesh.Count - 1) * interval; } }
    private float bodyDistanceTraveled {
        get {
            //return Mathf.Max(0, (snakeMesh.Kernel.RingsAdded - 1) * interval);
            return (snakeMesh.Kernel.RingsAdded - 1) * interval;
        }
    }


    private float GetPatchLength(ISnakeMesh patch) {
        var path = patch.Kernel.Path;
        float result = (path[0].position - path[1].position).magnitude;
        //Debug.Assert(result <= interval);
        return result;
    }

    // Grows body mesh if needed in current frame.
    //
    // TODO:
    //    - Handle adding of several rings per call
    //    - Remove "force" argument
    private void GrowBodyMesh(ValueTransform ring, bool force = false) {
        Vector3 lastGrowPoint;
        if (force)
            lastGrowPoint = ring.position;
        else
            lastGrowPoint = snakeMesh.Kernel.Path.Last.position;

        Vector3 delta = ring.position - lastGrowPoint;
        if (delta.magnitude >= interval || force) {
            // Grow
            //lastGrowPoint += delta.normalized * interval;
            float distanceTraveled = bodyDistanceTraveled + interval;
            snakeMesh.PushToEnd(ring, distanceTraveled);
        }
    }

    // TODO: remove duplicate code in UpdateHeadPatch() and UpdateTailPatch()
    private void UpdateHeadPatch(ValueTransform headRing) {
        headPatch.PopFromStart();
        headPatch.PopFromStart();

        ValueTransform bodyRing = snakeMesh.Kernel.Path.Last;
        float patchLength = (headRing.position - bodyRing.position).magnitude;

        headPatch.PushToEnd(bodyRing, bodyDistanceTraveled);
        headPatch.PushToEnd(headRing, bodyDistanceTraveled + patchLength);
    }

    private void UpdateTailPatch(ValueTransform tailRing) {
        tailPatch.PopFromStart();
        tailPatch.PopFromStart();

        ValueTransform bodyRing = snakeMesh.Kernel.Path[0];
        float bodyDistance = bodyDistanceTraveled - bodyLength;
        float tailPatchLength = (tailRing.position - bodyRing.position).magnitude;

        tailPatch.PushToEnd(tailRing, bodyDistance - tailPatchLength);
        tailPatch.PushToEnd(bodyRing, bodyDistance);
    }

    private void UpdateLastPoppedRing(ValueTransform ring) {
        lastPoppedRing = ring;
    }

    private void AnimateUV() {
        Vector2 offset = snakeMesh.TextureOffset;
        float distanceTraveled = bodyDistanceTraveled + GetPatchLength(headPatch);
        offset.y = -distanceTraveled / snakeMesh.RingLength;
        snakeMesh.TextureOffset = offset;
        headPatch.TextureOffset = offset;
        tailPatch.TextureOffset = offset;

        // Tail
        Vector2 tailOffset = baseTailOffset;
        tailOffset.y += offset.y;
        tailMaterial.SetUnscaledTextureOffset(tailOffset);
    }

    #endregion Private
}

} // namespace Snake3D