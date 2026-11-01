using UnityEngine;
using nadena.dev.ndmf;

namespace Nekoneko.NNLightingSync
{
    /// <summary>
    /// Marker component for NN - Lighting Sync.
    ///
    /// This component does not modify materials in the Unity scene or prefab.
    /// When NDMF builds the avatar, the editor plugin finds this marker and
    /// applies the avatar's lilToon lighting values only to the build clone.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("NN - Lighting Sync")]
    public sealed class NNLightingSync : MonoBehaviour, INDMFEditorOnly
    {
        // Intentionally no user settings.
    }
}
