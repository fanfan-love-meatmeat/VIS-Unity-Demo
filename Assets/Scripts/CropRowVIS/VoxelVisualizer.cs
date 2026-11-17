using UnityEngine;
using System.Collections.Generic;

public class VoxelVisualizer
{
    public void DrawGizmosWithOverlapColors(
        GameObject targetGameObject,
        VoxelData voxelData,
        OverlapDetector detector,
        Transform cropRowTransform,
        Transform leftTrackTransform,
        Transform rightTrackTransform,
        Transform leftVoxelTransform,
        Transform rightVoxelTransform,
        float voxelSize,
        bool onlyWhenSelected,
        bool showTrackVoxels,
        bool showVoxelWireframes,
        bool showLeftTrackWireframe,
        bool showRightTrackWireframe,
        bool showCropRowBounds,                        // Crop-row bounding-box display flag.
        Color cropRowBoundsColor,
        Color leftTrackWireColor,
        Color rightTrackWireColor,
        Color leftTrackBaseColor,
        Color leftTrackOverlapColor,
        Color rightTrackBaseColor,
        Color rightTrackOverlapColor
    )
    {
#if UNITY_EDITOR
        if (onlyWhenSelected && UnityEditor.Selection.activeGameObject != targetGameObject)
            return;

        // Control crop-row bounding-box rendering.
        if (showCropRowBounds && cropRowTransform != null && voxelData.CropBounds.size != Vector3.zero)
        {
            Gizmos.color = cropRowBoundsColor;
            Gizmos.matrix = cropRowTransform.localToWorldMatrix;
            Gizmos.DrawWireCube(voxelData.CropBounds.center, voxelData.CropBounds.size);
            Gizmos.matrix = Matrix4x4.identity;
        }

        // Draw the track voxel visualization.
        if (showTrackVoxels)
        {
            if (leftVoxelTransform != null && detector.LeftOverlapFlags != null)
            {
                DrawTrackVoxels(
                    voxelData.TrackLocalCenters,
                    leftVoxelTransform,
                    detector.LeftOverlapFlags,
                    leftTrackBaseColor,
                    leftTrackOverlapColor,
                    voxelSize,
                    showVoxelWireframes
                );
            }

            if (rightVoxelTransform != null && detector.RightOverlapFlags != null)
            {
                DrawTrackVoxels(
                    voxelData.TrackLocalCenters,
                    rightVoxelTransform,
                    detector.RightOverlapFlags,
                    rightTrackBaseColor,
                    rightTrackOverlapColor,
                    voxelSize,
                    showVoxelWireframes
                );
            }
        }

        // Track wireframes.
        if (showLeftTrackWireframe && leftTrackTransform != null)
        {
            Gizmos.color = leftTrackWireColor;
            DrawMeshBounds(leftTrackTransform);
        }

        if (showRightTrackWireframe && rightTrackTransform != null)
        {
            Gizmos.color = rightTrackWireColor;
            DrawMeshBounds(rightTrackTransform);
        }
#endif
    }

    private void DrawTrackVoxels(
        List<Vector3> localCenters,
        Transform transform,
        bool[] overlapFlags,
        Color baseColor,
        Color overlapColor,
        float voxelSize,
        bool wireframe
    )
    {
#if UNITY_EDITOR
        for (int i = 0; i < localCenters.Count; i++)
        {
            Vector3 worldPos = transform.TransformPoint(localCenters[i]);
            bool overlap = i < overlapFlags.Length && overlapFlags[i];

            Color drawColor = overlap ? overlapColor : baseColor;
            Gizmos.color = drawColor;

            if (wireframe)
                Gizmos.DrawWireCube(worldPos, Vector3.one * voxelSize);
            else
                Gizmos.DrawCube(worldPos, Vector3.one * voxelSize);
        }
#endif
    }

    private void DrawMeshBounds(Transform t)
    {
        var mf = t.GetComponent<MeshFilter>();
        if (mf?.sharedMesh == null) return;

        Bounds bounds = mf.sharedMesh.bounds;

        Vector3[] corners = new Vector3[8];
        corners[0] = t.TransformPoint(bounds.min);
        corners[1] = t.TransformPoint(new Vector3(bounds.max.x, bounds.min.y, bounds.min.z));
        corners[2] = t.TransformPoint(new Vector3(bounds.max.x, bounds.max.y, bounds.min.z));
        corners[3] = t.TransformPoint(new Vector3(bounds.min.x, bounds.max.y, bounds.min.z));
        corners[4] = t.TransformPoint(new Vector3(bounds.min.x, bounds.min.y, bounds.max.z));
        corners[5] = t.TransformPoint(new Vector3(bounds.max.x, bounds.min.y, bounds.max.z));
        corners[6] = t.TransformPoint(bounds.max);
        corners[7] = t.TransformPoint(new Vector3(bounds.min.x, bounds.max.y, bounds.max.z));

        int[,] edges = new int[,]
        {
            {0,1},{1,2},{2,3},{3,0},
            {4,5},{5,6},{6,7},{7,4},
            {0,4},{1,5},{2,6},{3,7}
        };

        for (int i = 0; i < 12; i++)
        {
            Gizmos.DrawLine(corners[edges[i, 0]], corners[edges[i, 1]]);
        }
    }
}
