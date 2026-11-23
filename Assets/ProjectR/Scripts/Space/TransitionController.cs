using UnityEngine;

namespace ProjectR
{
    public sealed class TransitionController : MonoBehaviour
    {
        [SerializeField] private SpatialManager space;
        [SerializeField] private DoorController door;
        [SerializeField] private Camera playerView;
        [SerializeField] private CharacterController playerBody;
        [SerializeField] private BoxCollider bedroomViewVolume;
        [SerializeField] private BoxCollider changeVolume;
        [SerializeField] private BoxCollider doorwayOpening;
        [SerializeField] private BoxCollider opaqueDoorPanel;
        [SerializeField] private bool extendAfterFirstClose = true;
        [SerializeField] private bool extendFromInsideCorridor;

        private readonly Plane[] viewPlanes = new Plane[6];

        private void LateUpdate()
        {
            if (extendAfterFirstClose && !space.IsExtended && CanChangeCorridor())
                space.ExtendCorridor();
        }

        public bool CanChangeCorridor()
        {
            Bounds affectedSpace = changeVolume.bounds;
            if (affectedSpace.Intersects(playerBody.bounds))
                return false;

            if (extendFromInsideCorridor)
                return door.IsPassageLocked && IsOutsideView(affectedSpace);

            return door.HasOpened && door.IsClosed && Contains(bedroomViewVolume, playerView.transform.position) && DoorCoversOpening();
        }

        private bool IsOutsideView(Bounds affectedSpace)
        {
            // The volume encloses the wall being removed and the inactive extension.
            // Keep a small margin so a swap cannot occur at the edge of the picture.
            affectedSpace.Expand(.1f);
            GeometryUtility.CalculateFrustumPlanes(playerView, viewPlanes);
            return !GeometryUtility.TestPlanesAABB(viewPlanes, affectedSpace);
        }

        private bool DoorCoversOpening()
        {
            if (!opaqueDoorPanel.enabled || !opaqueDoorPanel.gameObject.activeInHierarchy)
                return false;

            // The bedroom is enclosed. Every sightline into either corridor crosses this aperture.
            // The panel box matches the opaque mesh, and its convex shape must cover all four corners.
            Vector3 halfSize = doorwayOpening.size * 0.5f;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    Vector3 corner = doorwayOpening.center + new Vector3(x * halfSize.x, y * halfSize.y, 0f);
                    if (!Contains(opaqueDoorPanel, doorwayOpening.transform.TransformPoint(corner)))
                        return false;
                }
            }

            return true;
        }

        private static bool Contains(BoxCollider box, Vector3 worldPoint)
        {
            Vector3 offset = box.transform.InverseTransformPoint(worldPoint) - box.center;
            Vector3 halfSize = box.size * 0.5f;
            return Mathf.Abs(offset.x) <= halfSize.x && Mathf.Abs(offset.y) <= halfSize.y && Mathf.Abs(offset.z) <= halfSize.z;
        }
    }
}
