using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;
using UnityEditor;

namespace Reuben.RTXBaker.Editor
{
    public class MainMenuWindow: OdinMenuEditorWindow
    {
        protected override OdinMenuTree BuildMenuTree()
        {
            var tree = new OdinMenuTree();
            tree.Add("Baker Panel", new BakePanel(), EditorIcons.House);
            return tree;
        }

        [MenuItem("Tools/RTX Baker Menu")]
        private static void OpenWindow()
        {
            var window = GetWindow<MainMenuWindow>();
            window.position = GUIHelper.GetEditorWindowRect().AlignCenter(800, 600);
        }
    }
}