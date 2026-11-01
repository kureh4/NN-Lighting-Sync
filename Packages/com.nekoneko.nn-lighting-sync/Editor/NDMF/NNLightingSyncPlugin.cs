using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using UnityEngine;
using Object = UnityEngine.Object;

[assembly: ExportsPlugin(typeof(Nekoneko.NNLightingSync.Editor.NDMF.NNLightingSyncPlugin))]

namespace Nekoneko.NNLightingSync.Editor.NDMF
{
    /// <summary>
    /// Non-destructive NDMF build plugin.
    ///
    /// The original scene, prefabs and material assets are never modified.
    /// All material clones exist only in the NDMF build avatar.
    /// </summary>
    public sealed class NNLightingSyncPlugin : Plugin<NNLightingSyncPlugin>
    {
        private const string LightMinLimit = "_LightMinLimit";
        private const string LightMaxLimit = "_LightMaxLimit";
        private const string MonochromeLighting = "_MonochromeLighting";

        public override string QualifiedName => "com.nekoneko.nn-lighting-sync";
        public override string DisplayName => "NN - Lighting Sync";

        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming)
                .AfterPlugin("nadena.dev.modular-avatar")
                .AfterPlugin("nadena.dev.modular-avatar.late-transform-stages")
                .AfterPlugin("net.rs64.tex-trans-tool")
                .Run("Sync lilToon lighting", ApplyLightingSync);
        }

        private static void ApplyLightingSync(BuildContext ctx)
        {
            if (ctx == null || ctx.AvatarRootObject == null)
                return;

            var avatarRoot = ctx.AvatarRootObject.transform;

            var allMarkers = avatarRoot
                .GetComponentsInChildren<NNLightingSync>(true)
                .Where(m => m != null)
                .ToArray();

            if (allMarkers.Length == 0)
                return;

            var excludedRoots = allMarkers
                .Select(m => m.transform)
                .Where(t => t != null)
                .Distinct()
                .ToArray();

            var enabledMarkers = allMarkers
                .Where(m => m.enabled)
                .ToArray();

            if (enabledMarkers.Length > 0)
            {
                var source = FindBestSourceMaterial(avatarRoot, excludedRoots);

                if (source != null)
                {
                    var topLevelMarkers = enabledMarkers
                        .Where(m => !HasEnabledMarkerAncestor(m.transform, enabledMarkers))
                        .ToArray();

                    var clonedMaterials = new Dictionary<Material, Material>();

                    foreach (var marker in topLevelMarkers)
                        ApplyToMarker(ctx, marker, source, clonedMaterials);
                }
                else
                {
                    Debug.LogWarning(
                        "[NN - Lighting Sync] アバター側のlilToonライティング設定を取得できませんでした。" +
                        "衣装のMaterialは変更せずにビルドを続行します。",
                        ctx.AvatarRootObject
                    );
                }
            }

            foreach (var marker in allMarkers)
            {
                if (marker != null)
                    Object.DestroyImmediate(marker);
            }
        }

        private static void ApplyToMarker(
            BuildContext ctx,
            NNLightingSync marker,
            Material source,
            Dictionary<Material, Material> clonedMaterials
        )
        {
            if (marker == null || source == null)
                return;

            var renderers = marker.GetComponentsInChildren<Renderer>(true);

            foreach (var renderer in renderers)
            {
                if (renderer == null)
                    continue;

                var materials = renderer.sharedMaterials;
                if (materials == null || materials.Length == 0)
                    continue;

                bool changed = false;
                var replaced = (Material[])materials.Clone();

                for (int i = 0; i < replaced.Length; i++)
                {
                    var original = replaced[i];
                    if (!IsLilToonMaterial(original))
                        continue;

                    if (!clonedMaterials.TryGetValue(original, out var clone) || clone == null)
                    {
                        clone = new Material(original)
                        {
                            name = original.name + " [NN Lighting Sync]"
                        };

                        ApplyLightingValues(source, clone);
                        ctx.AssetSaver.SaveAsset(clone);
                        clonedMaterials[original] = clone;
                    }

                    replaced[i] = clone;
                    changed = true;
                }

                if (changed)
                    renderer.sharedMaterials = replaced;
            }
        }

        private static Material FindBestSourceMaterial(Transform avatarRoot, Transform[] excludedRoots)
        {
            var candidates = new List<SourceCandidate>();

            foreach (var renderer in avatarRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || IsInsideAny(renderer.transform, excludedRoots))
                    continue;

                var materials = renderer.sharedMaterials;
                if (materials == null)
                    continue;

                string path = GetRelativePath(renderer.transform, avatarRoot);

                for (int i = 0; i < materials.Length; i++)
                {
                    var material = materials[i];
                    if (!IsLilToonMaterial(material) || !HasLightingProperties(material))
                        continue;

                    candidates.Add(new SourceCandidate
                    {
                        Material = material,
                        Score = ScoreSource(path, renderer.name, material.name),
                        StableKey = path + "|" + material.name + "|" + i
                    });
                }
            }

            if (candidates.Count == 0)
                return null;

            var bestNamed = candidates
                .OrderByDescending(c => c.Score)
                .ThenBy(c => c.StableKey, StringComparer.Ordinal)
                .First();

            if (bestNamed.Score >= 70)
                return bestNamed.Material;

            var majority = candidates
                .GroupBy(c => LightingValueKey(c.Material))
                .OrderByDescending(g => g.Count())
                .ThenByDescending(g => g.Max(c => c.Score))
                .ThenBy(
                    g => g.Select(c => c.StableKey).OrderBy(k => k, StringComparer.Ordinal).First(),
                    StringComparer.Ordinal
                )
                .First();

            return majority
                .OrderByDescending(c => c.Score)
                .ThenBy(c => c.StableKey, StringComparer.Ordinal)
                .First()
                .Material;
        }

        private static bool HasEnabledMarkerAncestor(Transform transform, NNLightingSync[] enabledMarkers)
        {
            if (transform == null)
                return false;

            var markerTransforms = new HashSet<Transform>(
                enabledMarkers.Where(m => m != null).Select(m => m.transform)
            );

            for (var parent = transform.parent; parent != null; parent = parent.parent)
            {
                if (markerTransforms.Contains(parent))
                    return true;
            }

            return false;
        }

        private static bool IsInsideAny(Transform transform, Transform[] roots)
        {
            if (transform == null || roots == null)
                return false;

            foreach (var root in roots)
            {
                if (root != null && (transform == root || transform.IsChildOf(root)))
                    return true;
            }

            return false;
        }

        private static bool IsLilToonMaterial(Material material)
        {
            if (material == null || material.shader == null)
                return false;

            string shaderName = material.shader.name ?? string.Empty;
            return shaderName.IndexOf("lilToon", StringComparison.OrdinalIgnoreCase) >= 0
                   && HasLightingProperties(material);
        }

        private static bool HasLightingProperties(Material material)
        {
            return material != null
                   && material.HasProperty(LightMinLimit)
                   && material.HasProperty(LightMaxLimit)
                   && material.HasProperty(MonochromeLighting);
        }

        private static void ApplyLightingValues(Material source, Material target)
        {
            if (!HasLightingProperties(source) || !HasLightingProperties(target))
                return;

            target.SetFloat(LightMinLimit, source.GetFloat(LightMinLimit));
            target.SetFloat(LightMaxLimit, source.GetFloat(LightMaxLimit));
            target.SetFloat(MonochromeLighting, source.GetFloat(MonochromeLighting));
        }

        private static string LightingValueKey(Material material)
        {
            int min = Mathf.RoundToInt(material.GetFloat(LightMinLimit) * 10000f);
            int max = Mathf.RoundToInt(material.GetFloat(LightMaxLimit) * 10000f);
            int mono = Mathf.RoundToInt(material.GetFloat(MonochromeLighting) * 10000f);
            return min + ":" + max + ":" + mono;
        }

        private static int ScoreSource(string path, string rendererName, string materialName)
        {
            string text = (
                (path ?? string.Empty) + " " +
                (rendererName ?? string.Empty) + " " +
                (materialName ?? string.Empty)
            ).ToLowerInvariant();

            int score = 0;

            score += ContainsAny(text, "素体", "body") ? 120 : 0;
            score += ContainsAny(text, "肌", "skin") ? 110 : 0;
            score += ContainsAny(text, "face", "顔") ? 90 : 0;
            score += ContainsAny(text, "head", "頭") ? 70 : 0;
            score += ContainsAny(text, "base") ? 20 : 0;

            score -= ContainsAny(
                text, "outfit", "costume", "dress", "cloth", "clothes",
                "wear", "衣装", "服"
            ) ? 140 : 0;

            score -= ContainsAny(text, "hair", "髪") ? 60 : 0;
            score -= ContainsAny(text, "eye", "eyes", "瞳", "目") ? 80 : 0;
            score -= ContainsAny(
                text, "accessory", "accessories", "アクセ", "acc_"
            ) ? 80 : 0;

            return score;
        }

        private static bool ContainsAny(string text, params string[] words)
        {
            foreach (var word in words)
            {
                if (!string.IsNullOrEmpty(word) && text.Contains(word))
                    return true;
            }

            return false;
        }

        private static string GetRelativePath(Transform target, Transform root)
        {
            if (target == null)
                return string.Empty;

            var names = new Stack<string>();
            var current = target;

            while (current != null && current != root)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names.ToArray());
        }

        private sealed class SourceCandidate
        {
            public Material Material;
            public int Score;
            public string StableKey;
        }
    }
}
