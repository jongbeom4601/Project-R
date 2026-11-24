using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace ProjectR
{
    public sealed class KitchenOverlapRendering : ScriptableObject
    {
        internal const string ResourceName = "ApartmentKitchenRendering";

        internal enum View { Full, FromPrevious, FromNext, Arrival }

        [SerializeField] private Surface[] surfaces;

        // Surfaces are linked by index through the apartment's ApartmentParts.
        internal Room Bind(ApartmentParts parts, bool trimArrival)
        {
            var renderers = new List<SurfaceRenderer>(surfaces.Length);
            for (int i = 0; i < surfaces.Length; i++)
            {
                Surface surface = surfaces[i];
                MeshRenderer renderer = parts.OverlapSurface(i);
                // Kitchen window panes are shown only by the kitchen's owner, and ApartmentRoute alone switches them.
                // Binding them here too would record a pane hidden for a neighbour as hidden for good.
                if (System.Array.IndexOf(parts.KitchenWindows, renderer) >= 0)
                    continue;
                if (renderer != null && renderer.TryGetComponent(out MeshFilter filter) &&
                    surface.Matches(filter.sharedMesh))
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
            [FormerlySerializedAs("path"), Tooltip("Where the surface sits in the Apartment prefab. A label only.")]
            public string label;
            [Tooltip("Part of the recessed entry, kept whole while the player stands in it.")]
            public bool isFoyer;
            public Bounds sourceBounds;
            public int sourceVertexCount;
            public int sourceSubMeshCount;
            public bool previousIsClipped;
            public Mesh previousMesh;
            public bool nextIsClipped;
            public Mesh nextMesh;
            public Mesh arrivalMesh;

            internal bool IsFoyer => isFoyer;

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
