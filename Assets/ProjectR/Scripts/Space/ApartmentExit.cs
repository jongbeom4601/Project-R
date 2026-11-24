using UnityEngine;

namespace ProjectR
{
    public sealed class ApartmentExit : MonoBehaviour
    {
        [SerializeField] private Transform sourceDoorway;
        [SerializeField] private Transform destinationDoorway;
        [SerializeField] private DoorController door;

        internal bool IsClosed => door.IsClosed;
        // Doorway marker on the destination side, facing into the destination space.
        internal Transform Entry => destinationDoorway;

        internal void AlignSource(Transform sourceSpace)
        {
            Quaternion rotation = destinationDoorway.rotation * Quaternion.Inverse(sourceDoorway.rotation);
            sourceSpace.rotation = rotation * sourceSpace.rotation;
            sourceSpace.position += destinationDoorway.position - sourceDoorway.position;
        }

        internal void ShareDoor(Transform parent) => door.transform.SetParent(parent, true);

        internal void SetDoorRenderingLayers(uint mask)
        {
            foreach (Renderer renderer in door.GetComponentsInChildren<Renderer>(true))
                renderer.renderingLayerMask = mask;
        }

        internal void SetActiveSides(bool sourceActive, bool destinationActive, bool canOpen)
        {
            door.PassageAvailable = canOpen;
            door.gameObject.SetActive(sourceActive || destinationActive);
        }

        internal bool Crossed(Vector3 previousCenter, Vector3 center, bool forward)
        {
            if (door.IsClosed)
                return false;

            Vector3 from = sourceDoorway.InverseTransformPoint(previousCenter);
            Vector3 to = sourceDoorway.InverseTransformPoint(center);
            if (forward ? from.z > 0f || to.z <= 0f : from.z < 0f || to.z >= 0f)
                return false;

            Vector3 atDoorway = Vector3.LerpUnclamped(from, to, from.z / (from.z - to.z));
            return Mathf.Abs(atDoorway.x) < .55f && atDoorway.y > 0f && atDoorway.y < 2.1f;
        }

        internal int CrossedFoyerBoundary(BoxCollider floor, Vector3 previousCenter, Vector3 center)
        {
            if (floor == null)
                return 0;

            Vector3 from = floor.transform.InverseTransformPoint(previousCenter) - floor.center;
            Vector3 to = floor.transform.InverseTransformPoint(center) - floor.center;
            float fromDepth = from.z + floor.size.z * .5f;
            float toDepth = to.z + floor.size.z * .5f;
            if ((fromDepth > 0f) == (toDepth > 0f))
                return 0;

            float fraction = fromDepth / (fromDepth - toDepth);
            if (Mathf.Abs(Mathf.LerpUnclamped(from.x, to.x, fraction)) > floor.size.x * .5f)
                return 0;

            float height = sourceDoorway.InverseTransformPoint(Vector3.LerpUnclamped(previousCenter, center, fraction)).y;
            return height > 0f && height < 2.3f ? (toDepth > 0f ? 1 : -1) : 0;
        }
    }
}
