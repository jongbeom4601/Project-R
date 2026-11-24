using UnityEngine;

namespace ProjectR
{
    // Parts of an apartment that the route treats specially. Linked in the Apartment prefab,
    // so renaming or regrouping objects in the hierarchy keeps these references intact.
    public sealed class ApartmentParts : MonoBehaviour
    {
        [SerializeField, Tooltip("Window panes and sky cards over the kitchen counter. Neighbouring apartments overlap there, so only the kitchen's owner draws them.")]
        private Renderer[] kitchenWindows = System.Array.Empty<Renderer>();

        [SerializeField, Tooltip("Floor of the recessed entry by the front door. Crossing its edge hands the kitchen to the next apartment.")]
        private BoxCollider foyerFloor;

        [SerializeField, Tooltip("Surfaces clipped where kitchens overlap, in the same order as the surfaces of ApartmentKitchenRendering.")]
        private MeshRenderer[] overlapSurfaces = System.Array.Empty<MeshRenderer>();

        public Renderer[] KitchenWindows => kitchenWindows;
        public BoxCollider FoyerFloor => foyerFloor;

        internal MeshRenderer OverlapSurface(int index) =>
            index >= 0 && index < overlapSurfaces.Length ? overlapSurfaces[index] : null;
    }
}
