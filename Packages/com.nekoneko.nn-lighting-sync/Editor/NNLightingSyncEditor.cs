using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Nekoneko.NNLightingSync.Editor
{
    [CustomEditor(typeof(NNLightingSync))]
    internal sealed class NNLightingSyncEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "アバター側のライティング設定を自動取得し、衣装側のlilToonマテリアルに合わせます。",
                MessageType.Info
            );

            EditorGUILayout.Space(4);

            EditorGUILayout.HelpBox(
                "非破壊方式：元のMaterialやPrefabは変更しません。\n" +
                "VRChatアップロード / NDMFビルド時のコピーにのみ適用されます。",
                MessageType.None
            );

            if (!IsNdmfInstalled())
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    "NDMFが見つかりません。\n" +
                    "このコンポーネントを動作させるにはNDMFが必要です。" +
                    "Modular Avatarを導入している環境では通常NDMFも導入されます。",
                    MessageType.Warning
                );
            }
        }

        private static bool IsNdmfInstalled()
        {
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .Any(a => string.Equals(
                    a.GetName().Name,
                    "nadena.dev.ndmf",
                    StringComparison.Ordinal
                ));
        }
    }
}
