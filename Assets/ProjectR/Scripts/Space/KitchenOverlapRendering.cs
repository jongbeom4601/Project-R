using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectR
{
    public sealed class KitchenOverlapRendering : ScriptableObject
    {
        internal const string ResourceName = "ApartmentKitchenRendering";
        internal const string FoyerFloorPath = "LivingZonePlan/FloorAndCeiling/Recessed Entry Floor";

        internal enum View { Full, FromPrevious, FromNext, Arrival }

        [SerializeField] private Surface[] surfaces;

        internal Room Bind(Transform root, bool trimArrival)
        {
            var renderers = new List<SurfaceRenderer>(surfaces.Length);
            foreach (Surface surface in surfaces)
            {
                Transform target = root.Find(surface.path);
                if (target != null && target.TryGetComponent(out MeshFilter filter) &&
                    surface.Matches(filter.sharedMesh) &&
                    target.TryGetComponent(out MeshRenderer renderer))
                {
                    var part = new SurfaceRenderer(surface, renderer);
                    if (surface.arrivalMesh == null)
                        renderers.Add(part);
                    else if (trimArrival)
                        part.SetView(View.Arrival);
                }
            }
            return new Room(renderers);
        }

        [Serializable]
        internal sealed class Surface
        {
            public string path;
            public Bounds sourceBounds;
            public int sourceVertexCount;
            public int sourceSubMeshCount;
            public bool previousIsClipped;
            public Mesh previousMesh;
            public bool nextIsClipped;
            public Mesh nextMesh;
            public Mesh arrivalMesh;

            internal bool IsFoyer => path == FoyerFloorPath ||
                path == "LivingZonePlan/FloorAndCeiling/FrontDoor Threshold" ||
                path == "LivingZonePlan/FloorAndCeiling/Ceiling 3" ||
                path == "LivingZonePlan/Walls/Front Door Right Jamb" ||
                path.StartsWith("LivingZonePlan/Walls/FrontDoorWall/", StringComparison.Ordinal) ||
                path.StartsWith("LivingZonePlan/Walls/EntryEastWall/", StringComparison.Ordinal) ||
                path.StartsWith("LivingZonePlan/Walls/Entry Base ", StringComparison.Ordinal);

            internal bool Matches(Mesh mesh)
            {
                if (mesh == null || mesh.vertexCount != sourceVertexCount || mesh.subMeshCount != sourceSubMeshCount)
                    return false;

                Bounds bounds = mesh.bounds;
                return (bounds.center - sourceBounds.center).sqrMagnitude < 1e-8f &&
                    (bounds.size - sourceBounds.size).sqrMagnitude < 1e-8f;
            }
        }

        internal sealed class Room
        {
            private readonly List<SurfaceRenderer> renderers;
            private View currentView;
            private bool foyerPreserved;

            internal Room(List<SurfaceRenderer> renderers) => this.renderers = renderers;

            internal void SetView(View view, bool preserveFoyer)
            {
                if (view == currentView && preserveFoyer == foyerPreserved)
                    return;

                foreach (SurfaceRenderer renderer in renderers)
                    renderer.SetView(preserveFoyer && renderer.IsFoyer ? View.Full : view);
                currentView = view;
                foyerPreserved = preserveFoyer;
            }
        }

        internal sealed class SurfaceRenderer
        {
            private readonly Surface surface;
            private readonly MeshRenderer original;
            private readonly ShadowCastingMode originalShadows;
            private readonly bool originallyHidden;
            private MeshRenderer visible;
            private MeshFilter visibleMesh;

            internal bool IsFoyer => surface.IsFoyer;

            internal SurfaceRenderer(Surface surface, MeshRenderer original)
            {
                this.surface = surface;
                this.original = original;
                originalShadows = original.shadowCastingMode;
                originallyHidden = original.forceRenderingOff;
            }

            internal void SetView(View view)
            {
                bool clipped = view == View.Arrival ||
                    (view == View.FromPrevious && surface.previousIsClipped) ||
                    (view == View.FromNext && surface.nextIsClipped);
                bool castsShadows = originalShadows != ShadowCastingMode.Off;
                original.shadowCastingMode = clipped && castsShadows ? ShadowCastingMode.ShadowsOnly : originalShadows;
                original.forceRenderingOff = originallyHidden || (clipped && !castsShadows);

                Mesh mesh = view switch
                {
                    View.FromPrevious => surface.previousMesh,
                    View.FromNext => surface.nextMesh,
                    View.Arrival => surface.arrivalMesh,
                    _ => null
                };
                if (!clipped || mesh == null || originallyHidden || originalShadows == ShadowCastingMode.ShadowsOnly)
                {
                    if (visible != null)
                        visible.enabled = false;
                    return;
                }

                if (visible == null)
                    CreateVisibleRenderer();

                visibleMesh.sharedMesh = mesh;
                visible.enabled = original.enabled;
            }

            private void CreateVisibleRenderer()
            {
                var display = new GameObject("Kitchen visible surface");
                display.layer = original.gameObject.layer;
                display.transform.SetParent(original.transform, false);
                visibleMesh = display.AddComponent<MeshFilter>();
                visible = display.AddComponent<MeshRenderer>();
                visible.sharedMaterials = original.sharedMaterials;
                visible.renderingLayerMask = original.renderingLayerMask;
                visible.shadowCastingMode = ShadowCastingMode.Off;
                visible.receiveShadows = original.receiveShadows;
                visible.lightProbeUsage = original.lightProbeUsage;
                visible.reflectionProbeUsage = original.reflectionProbeUsage;
                visible.probeAnchor = original.probeAnchor;
                visible.lightProbeProxyVolumeOverride = original.lightProbeProxyVolumeOverride;
                visible.lightmapIndex = original.lightmapIndex;
                visible.lightmapScaleOffset = original.lightmapScaleOffset;
                visible.realtimeLightmapIndex = original.realtimeLightmapIndex;
                visible.realtimeLightmapScaleOffset = original.realtimeLightmapScaleOffset;
                visible.sortingLayerID = original.sortingLayerID;
                visible.sortingOrder = original.sortingOrder;
            }
        }
    }
}
