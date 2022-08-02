using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Reuben.RTXBaker.Editor
{
    public class BakePanel
    {
        [Title("Bake Panel")]
        [Button("Test")]
        void TestUse()
        {
            Debug.Log("Test!");
        }

        [Button("Hello")]
        void PrintHello()
        {
            Debug.Log("Hello world");
        }
    }
}