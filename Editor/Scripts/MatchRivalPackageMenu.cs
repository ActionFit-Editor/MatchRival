#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ActionFit.MatchRival.Editor
{
    public static class MatchRivalPackageMenu
    {
        private const string MenuRoot = "Tools/Package/ActionFit Match Rival/";
        private const string ReadmePath = "Packages/com.actionfit.match-rival/README.md";
        private const int ReadmePriority = 905;

        [MenuItem(MenuRoot + "README", false, ReadmePriority)]
        private static void OpenReadme()
        {
            var readme = AssetDatabase.LoadAssetAtPath<TextAsset>(ReadmePath);
            if (readme == null)
            {
                EditorUtility.DisplayDialog("Package README", $"README was not found.\n{ReadmePath}", "OK");
                return;
            }

            Selection.activeObject = readme;
            AssetDatabase.OpenAsset(readme);
        }
    }
}
#endif
