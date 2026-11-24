using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ProjectR
{
    // The route runs starting bedroom -> Apartment 01 -> ... -> Apartment 08. Each apartment is entered through its
    // front door and left through its bedroom-1 doorway, which opens onto the next apartment's front door.
    [DefaultExecutionOrder(-20)]
    public sealed class ApartmentRoute : MonoBehaviour
    {
        [SerializeField] private CharacterController player;
        [SerializeField] private Transform playerStart;
        [SerializeField] private GameObject[] spaces;
        [SerializeField] private ApartmentExit[] exits;
        [SerializeField] private GameObject[] lastApartmentAnnexes;

        private int current;

        // Index into spaces of the room the player is in (0 = starting bedroom).
        public int CurrentSpace => current;

        // A space of the route by index (0 = starting bedroom, 1-8 = apartments), or null.
        public GameObject Space(int index) => index >= 0 && index < spaces.Length ? spaces[index] : null;
        private int kitchenOwner;
        private bool previousIsOpen;
        private Vector3 previousCenter;
        private KitchenOverlapRendering kitchenRendering;
        private KitchenOverlapRendering.Room[] kitchenRooms;
        private BoxCollider[] foyerFloors;
        private Renderer[][] kitchenWindows;
        private ReflectionProbe[][] roomProbes;
        private Collider[][] intrudersInto;
        private ApartmentParts[] parts;
        private readonly System.Collections.Generic.List<Collider> suppressedColliders = new();

        // Living room, hallway, dining and kitchen of an apartment, in its own local space.
        // Stops short of the bedroom-1 doorway (x > 16), where the next apartment's front-door frame sits,
        // so the shared doorway keeps all of its collision.
        private static readonly Bounds LivingZone = new(new Vector3(11.6f, 1.2f, -4.05f), new Vector3(8.8f, 2.5f, 9.7f));

        private void Awake()
        {
            // Work back from Apartment 08 so its later corridors keep their authored world positions.
            for (int i = exits.Length - 1; i >= 0; i--)
                exits[i].AlignSource(spaces[i].transform);

            foreach (ApartmentExit exit in exits)
                exit.ShareDoor(transform);

            parts = new ApartmentParts[spaces.Length];
            for (int i = 0; i < spaces.Length; i++)
                parts[i] = spaces[i].GetComponent<ApartmentParts>();

            SetUpApartmentRendering();
            CollectKitchenWindows();
            CollectIntruders();
            player.transform.SetPositionAndRotation(playerStart.position, playerStart.rotation);
            UpdateActiveSpaces();
            UpdateKitchenRendering();
            previousCenter = player.bounds.center;
        }

        private void LateUpdate()
        {
            Vector3 center = player.bounds.center;
            int oldKitchenOwner = kitchenOwner;
            bool activeSpacesChanged = false;
            if (previousIsOpen)
            {
                ApartmentExit entrance = exits[current - 1];
                if (entrance.Crossed(previousCenter, center, false))
                {
                    // Back through the front door: the player is in the previous apartment, by its bedroom-1 doorway.
                    current--;
                    kitchenOwner = current;
                    previousIsOpen = false;
                    activeSpacesChanged = true;
                }
                else if (entrance.IsClosed)
                {
                    // The apartment behind the front door switches off now, so it can no longer hold the kitchen,
                    // even while the player still stands in the entry.
                    previousIsOpen = false;
                    kitchenOwner = current;
                    activeSpacesChanged = true;
                }
            }
            else if (current < exits.Length && exits[current].Crossed(previousCenter, center, true))
            {
                // Through the front door into the next apartment's entry. Its kitchen overlaps the apartment behind
                // that door, so the one the player just left keeps the kitchen until they step out of the entry.
                current++;
                kitchenOwner = current - 1;
                previousIsOpen = true;
                activeSpacesChanged = true;
            }

            // Stepping into the entry hands the kitchen to the apartment beyond the front door while that door is
            // still open (that apartment is active only then); stepping out takes it back.
            if (current > 0 && current <= exits.Length)
            {
                int crossing = exits[current - 1].CrossedFoyerBoundary(foyerFloors[current], previousCenter, center);
                if (crossing != 0)
                    kitchenOwner = crossing > 0 && previousIsOpen ? current - 1 : current;
            }

            if (activeSpacesChanged)
                UpdateActiveSpaces();
            if (activeSpacesChanged || kitchenOwner != oldKitchenOwner)
                UpdateKitchenRendering();

            previousCenter = center;
        }

        private void SetUpApartmentRendering()
        {
            kitchenRendering = Resources.Load<KitchenOverlapRendering>(KitchenOverlapRendering.ResourceName);
            kitchenRooms = new KitchenOverlapRendering.Room[spaces.Length];
            foyerFloors = new BoxCollider[spaces.Length];
            if (kitchenRendering == null)
                return;

            for (int i = 0; i < spaces.Length; i++)
            {
                uint layer = RoomLightLayer(i);
                SetRenderingLayers(spaces[i], layer, RoomGeometryLayers(i));
                if (i > 0 && parts[i] != null)
                    foyerFloors[i] = parts[i].FoyerFloor;
            }

            int last = spaces.Length - 1;
            foreach (GameObject annex in lastApartmentAnnexes)
                SetRenderingLayers(annex, RoomLightLayer(last), RoomGeometryLayers(last));

            for (int i = 0; i < exits.Length; i++)
                exits[i].SetDoorRenderingLayers(RoomGeometryLayers(i) | RoomGeometryLayers(i + 1));
        }

        // Only two consecutive rooms are active. The final room also receives the exterior light.
        private static uint RoomLightLayer(int index) => 1u << (index % 2 + 1);
        private uint RoomGeometryLayers(int index) => RoomLightLayer(index) | (index == spaces.Length - 1 ? 1u : 0u);

        private static void SetRenderingLayers(GameObject root, uint lightLayer, uint geometryLayers)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                renderer.renderingLayerMask = geometryLayers;

            foreach (Light light in root.GetComponentsInChildren<Light>(true))
            {
                if (light.TryGetComponent(out UniversalAdditionalLightData data))
                {
                    data.renderingLayers = lightLayer;
                    data.shadowRenderingLayers = lightLayer;
                }
                else
                {
                    light.renderingLayerMask = (int)lightLayer;
                }
            }
        }

        private void CollectKitchenWindows()
        {
            kitchenWindows = new Renderer[spaces.Length][];
            roomProbes = new ReflectionProbe[spaces.Length][];
            for (int i = 0; i < spaces.Length; i++)
            {
                roomProbes[i] = spaces[i].GetComponentsInChildren<ReflectionProbe>(true);
                // Panes and their sky cards over the kitchen counter. Neighbouring apartments overlap there,
                // so only the apartment that owns the kitchen may draw them.
                kitchenWindows[i] = parts[i] != null ? parts[i].KitchenWindows : System.Array.Empty<Renderer>();
            }
        }

        // After alignment, find each neighbour's colliders that reach into an apartment's living zone.
        // Their surfaces are already hidden there; their collision has to go too, or the player walks into
        // invisible walls and gets pushed through the floor when that neighbour is switched on.
        private void CollectIntruders()
        {
            Physics.SyncTransforms();
            intrudersInto = new Collider[spaces.Length][];
            for (int owner = 0; owner < spaces.Length; owner++)
            {
                var found = new System.Collections.Generic.List<Collider>();
                Transform zone = spaces[owner].transform;
                for (int neighbour = owner - 1; neighbour <= owner + 1; neighbour += 2)
                {
                    if (owner == 0 || neighbour < 0 || neighbour >= spaces.Length)
                        continue;
                    foreach (Collider c in spaces[neighbour].GetComponentsInChildren<Collider>(true))
                        if (OverlapsLivingZone(zone, c))
                            found.Add(c);
                }
                intrudersInto[owner] = found.ToArray();
            }
        }

        private static bool OverlapsLivingZone(Transform zone, Collider c)
        {
            // Colliders of inactive spaces still report their authored bounds through the transform.
            Bounds world = c.enabled && c.gameObject.activeInHierarchy ? c.bounds : WorldBounds(c);
            Vector3 a = zone.InverseTransformPoint(world.min), b = zone.InverseTransformPoint(world.max);
            var local = new Bounds((a + b) * .5f, Vector3.zero);
            local.Encapsulate(a);
            local.Encapsulate(b);
            Vector3 lo = Vector3.Max(local.min, LivingZone.min), hi = Vector3.Min(local.max, LivingZone.max);
            return hi.x - lo.x > .02f && hi.y - lo.y > .02f && hi.z - lo.z > .02f;
        }

        private static Bounds WorldBounds(Collider c)
        {
            Vector3 center = Vector3.zero, size = Vector3.zero;
            switch (c)
            {
                case BoxCollider box: center = box.center; size = box.size; break;
                case MeshCollider mesh when mesh.sharedMesh != null: center = mesh.sharedMesh.bounds.center; size = mesh.sharedMesh.bounds.size; break;
                case SphereCollider sphere: center = sphere.center; size = Vector3.one * sphere.radius * 2f; break;
                case CapsuleCollider capsule: center = capsule.center; size = Vector3.one * Mathf.Max(capsule.radius * 2f, capsule.height); break;
            }
            var bounds = new Bounds(c.transform.TransformPoint(center), Vector3.zero);
            Vector3 e = size * .5f;
            for (int i = 0; i < 8; i++)
                bounds.Encapsulate(c.transform.TransformPoint(center + Vector3.Scale(e, new Vector3(i % 2 * 2 - 1, i / 2 % 2 * 2 - 1, i / 4 * 2 - 1))));
            return bounds;
        }

        private void UpdateIntruders()
        {
            foreach (Collider c in suppressedColliders)
                if (c != null)
                    c.enabled = true;
            suppressedColliders.Clear();

            if (kitchenOwner <= 0)
                return;
            foreach (Collider c in intrudersInto[kitchenOwner])
            {
                if (c == null || !c.enabled)
                    continue; // leave colliders that are off for other reasons alone
                c.enabled = false;
                suppressedColliders.Add(c);
            }
        }

        private void UpdateKitchenRendering()
        {
            UpdateIntruders();
            for (int i = 0; i < spaces.Length; i++)
            {
                bool owns = kitchenOwner <= 0 || i == kitchenOwner;
                foreach (Renderer pane in kitchenWindows[i])
                    if (pane != null)
                        pane.forceRenderingOff = !owns;
                // Where kitchens overlap, the owner's room capture must win the reflection blend.
                foreach (ReflectionProbe probe in roomProbes[i])
                    probe.importance = owns ? 2 : 1;
            }

            if (kitchenRendering == null)
                return;

            for (int i = 1; i < spaces.Length; i++)
            {
                var view = KitchenOverlapRendering.View.Full;
                if (kitchenOwner > 0 && spaces[i].activeSelf)
                {
                    // The surface sets are named by where the owner stands: "Previous" is through the bedroom-1 doorway,
                    // "Next" is through the front door. The next apartment along the route sits behind the front door.
                    if (i == kitchenOwner + 1)
                        view = KitchenOverlapRendering.View.FromNext;
                    else if (i == kitchenOwner - 1)
                        view = KitchenOverlapRendering.View.FromPrevious;
                }

                if (spaces[i].activeSelf && kitchenRooms[i] == null && parts[i] != null)
                    kitchenRooms[i] = kitchenRendering.Bind(parts[i], i < spaces.Length - 1); // trims the bedroom-1 wall where the next apartment's entry meets it

                bool preserveFoyer = i == current && view == KitchenOverlapRendering.View.FromNext;
                kitchenRooms[i]?.SetView(view, preserveFoyer);
            }
        }

        private void UpdateActiveSpaces()
        {
            int adjacent = previousIsOpen ? current - 1 : current + 1;
            for (int i = 0; i < spaces.Length; i++)
                spaces[i].SetActive(i == current || i == adjacent);

            foreach (GameObject annex in lastApartmentAnnexes)
                annex.SetActive(spaces[spaces.Length - 1].activeSelf);

            int openableExit = previousIsOpen ? current - 1 : current;
            for (int i = 0; i < exits.Length; i++)
                exits[i].SetActiveSides(spaces[i].activeSelf, spaces[i + 1].activeSelf, i == openableExit);

            Physics.SyncTransforms();
        }
    }
}
