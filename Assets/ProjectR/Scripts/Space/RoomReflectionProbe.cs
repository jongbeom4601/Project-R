using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectR
{
    // Captures the room once, the first time its space becomes active.
    // Spaces are moved into place at runtime, so a baked cubemap would not line up;
    // a single realtime capture costs nothing per frame afterwards.
    [RequireComponent(typeof(ReflectionProbe))]
    public sealed class RoomReflectionProbe : MonoBehaviour
    {
        private ReflectionProbe probe;
        private Vector3 authoredSize;
        private Vector3 authoredCenter;
        private bool captured;
        private bool aligned;

        private void Awake()
        {
            probe = GetComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            authoredSize = probe.size;
            authoredCenter = probe.center;
        }

        private void OnEnable()
        {
            if (!captured)
                StartCoroutine(CaptureNextFrame());
        }

        private IEnumerator CaptureNextFrame()
        {
            // Wait one frame so route alignment and light setup have finished.
            yield return null;
            AlignBoxToWorld();
            probe.RenderProbe();
            captured = true;
        }

        // Capture afresh the next time the room is switched on.
        public void ForgetCapture() => captured = false;

        // Capture again after the room's lighting changed for good (for example its lights were switched on).
        public void Recapture()
        {
            if (!isActiveAndEnabled)
                return;
            StopAllCoroutines();
            StartCoroutine(CaptureNextFrame());
        }

        // Apartments are turned in 90-degree steps along the route. URP's probe rotation then turns
        // the box and the lookup direction, which does not match the world-aligned capture.
        // Keep the probe itself unrotated and turn its authored box into world axes instead.
        internal void AlignBoxToWorld()
        {
            if (probe == null)
                Awake();
            if (aligned)
                return; // already turned; turning again would undo it
            aligned = true;

            Quaternion rotation = transform.rotation;
            Vector3 size = rotation * authoredSize;
            probe.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            probe.center = rotation * authoredCenter;
            transform.rotation = Quaternion.identity;
        }
    }
}
