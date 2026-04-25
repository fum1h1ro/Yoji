using UnityEngine.TestTools;
using NUnit.Framework;
using UnityEditor;

public class SyncSolution
{
    [Test]
    public void VSSolution()
    {
        AssetDatabase.Refresh();
        EditorApplication.ExecuteMenuItem("Assets/Open C# Project");
    }
}
