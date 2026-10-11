using UnityEngine;

namespace CodeEscape.Player
{
    /// <summary>
    /// A camera's "view size" for both projection types: the orthographic size, or for a perspective
    /// camera the half-height it sees on the gameplay plane (z = 0). The follow camera and the cutscene
    /// zooms work in view size, so they behave the same with the 2.5D perspective camera.
    /// </summary>
    public static class CameraView
    {
        /// <summary>Alex and every collider live on this plane.</summary>
        public const float GameplayPlaneZ = 0f;

        public static float DistanceToPlane(Camera cam)
        {
            return Mathf.Max(0.01f, GameplayPlaneZ - cam.transform.position.z);
        }

        public static float GetSize(Camera cam)
        {
            if (cam.orthographic)
                return cam.orthographicSize;
            return DistanceToPlane(cam) * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        }

        public static void SetSize(Camera cam, float size)
        {
            if (cam.orthographic)
                cam.orthographicSize = size;
            else
                cam.fieldOfView = 2f * Mathf.Atan(size / DistanceToPlane(cam)) * Mathf.Rad2Deg;
        }
    }
}
