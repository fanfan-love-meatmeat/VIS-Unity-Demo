using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelIntrusion.Demo
{
    [Serializable]
    public sealed class DemoParameters
    {
        public int rowCount = 8;
        public float cropWidth = 1.2f, rowPitch = 1.4f, rowLength = 25f;
        public float voxelSize = 0.05f;
        public float trackGauge = 1.4f, trackWidth = 0.2f, trackLength = 2f, trackHeight = 1f;
        public float forwardSpeed = 0.5f, maxTrackSpeed = 0.8f, detectorHz = 10f;
        public float cameraHeight = 1.45f, cameraForwardOffset = 1.05f;
        public float cameraPitchDegrees = 50f, cameraVerticalFov = 58f;
        public int cameraWidth = 640, cameraHeightPixels = 480;
        public float cameraHz = 30f, cameraNearClip = 0.05f, cameraFarClip = 10f;
        public float depthPreviewMaxMeters = 3f;

        public float Radius { get { return cropWidth * 0.5f; } }
        public float MaxYawRate { get { return 2f * (maxTrackSpeed - forwardSpeed) / trackGauge; } }

        public void Validate()
        {
            float[] positive = { cropWidth, rowPitch, rowLength, voxelSize, trackGauge,
                trackWidth, trackLength, trackHeight, forwardSpeed, maxTrackSpeed, detectorHz,
                cameraHeight, cameraVerticalFov, cameraHz, cameraNearClip, cameraFarClip, depthPreviewMaxMeters };
            foreach (float value in positive)
                if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
                    throw new ArgumentException("Demo dimensions and rates must be finite and positive.");
            if (rowCount < 1 || rowCount > 32 || rowPitch < cropWidth || maxTrackSpeed <= forwardSpeed)
                throw new ArgumentException("Invalid row count, row pitch, or track speed limit.");
            if (cameraWidth < 1 || cameraHeightPixels < 1 || cameraWidth > 4096 || cameraHeightPixels > 4096
                || cameraVerticalFov >= 179f || cameraNearClip >= cameraFarClip
                || depthPreviewMaxMeters > cameraFarClip
                || float.IsNaN(cameraForwardOffset) || float.IsInfinity(cameraForwardOffset)
                || float.IsNaN(cameraPitchDegrees) || float.IsInfinity(cameraPitchDegrees))
                throw new ArgumentException("Invalid camera resolution, pose, FOV or depth range.");
            Cells(cropWidth); Cells(Radius); Cells(rowLength);
            Cells(trackWidth); Cells(trackHeight); Cells(trackLength);
        }

        // Keep the shared JSON dimensions exactly divisible by the voxel spacing.
        public int Cells(float length)
        {
            int n = (int)Math.Round(length / voxelSize);
            if (n < 1 || Math.Abs(n * (double)voxelSize - length) > 1e-5)
                throw new ArgumentException("Demo dimensions must be integer multiples of voxelSize.");
            return n;
        }

        public float RowCenterX(int row)
        {
            return (row - (rowCount - 1) * 0.5f) * rowPitch;
        }
    }

    public static class DemoGeometry
    {
        // Synthetic solid half-cylinder: (x-x_row)^2 + y^2 <= radius^2, y >= 0.
        // x = transverse, y = up, z = longitudinal. Each occupied grid cell contributes
        // one center, with no duplicate boundary centers between adjacent rows.
        public static List<Vector3> CropCenters(DemoParameters p)
        {
            p.Validate();
            int nx = p.Cells(p.cropWidth), ny = p.Cells(p.Radius), nz = p.Cells(p.rowLength);
            List<Vector3> centers = new List<Vector3>();
            for (int row = 0; row < p.rowCount; row++)
            for (int ix = 0; ix < nx; ix++)
            for (int iy = 0; iy < ny; iy++)
            {
                float x = -p.Radius + (ix + 0.5f) * p.voxelSize;
                float y = (iy + 0.5f) * p.voxelSize;
                if (x * x + y * y > p.Radius * p.Radius) continue;
                for (int iz = 0; iz < nz; iz++)
                    centers.Add(new Vector3(p.RowCenterX(row) + x, y, (iz + 0.5f) * p.voxelSize));
            }
            return centers;
        }

        // One track: 4 x 20 x 40 = 3,200 centers for the paper's nominal dimensions.
        public static List<Vector3> TrackCenters(DemoParameters p)
        {
            p.Validate();
            int nx = p.Cells(p.trackWidth), ny = p.Cells(p.trackHeight), nz = p.Cells(p.trackLength);
            List<Vector3> centers = new List<Vector3>(nx * ny * nz);
            for (int ix = 0; ix < nx; ix++)
            for (int iy = 0; iy < ny; iy++)
            for (int iz = 0; iz < nz; iz++)
                centers.Add(new Vector3(-p.trackWidth * 0.5f + (ix + 0.5f) * p.voxelSize,
                    (iy + 0.5f) * p.voxelSize,
                    -p.trackLength * 0.5f + (iz + 0.5f) * p.voxelSize));
            return centers;
        }

        public static Mesh HalfCylinder(float radius, float length)
        {
            const int segments = 32;
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            for (int end = 0; end < 2; end++)
            for (int i = 0; i <= segments; i++)
            {
                float theta = i * Mathf.PI / segments;
                vertices.Add(new Vector3(radius * Mathf.Cos(theta), radius * Mathf.Sin(theta), end * length));
            }
            int stride = segments + 1;
            vertices.Add(Vector3.zero);
            vertices.Add(new Vector3(0f, 0f, length));
            for (int i = 0; i < segments; i++)
            {
                AddQuad(triangles, i, i + 1, stride + i + 1, stride + i);
                triangles.Add(2 * stride); triangles.Add(i + 1); triangles.Add(i);
                triangles.Add(2 * stride + 1); triangles.Add(stride + i); triangles.Add(stride + i + 1);
            }
            AddQuad(triangles, 0, stride, stride + segments, segments);
            Mesh mesh = new Mesh { name = "Synthetic half-cylinder" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddQuad(List<int> t, int a, int b, int c, int d)
        {
            t.Add(a); t.Add(b); t.Add(c); t.Add(a); t.Add(c); t.Add(d);
        }
    }
}
