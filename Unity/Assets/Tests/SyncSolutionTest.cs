using UnityEngine;
using NUnit.Framework;
using System.Reflection;

public class SyncSolutionTest
{
    [Test]
    public void VSSolution()
    {
        var type = System.Type.GetType("Packages.Rider.Editor.RiderScriptEditor, Unity.Rider.Editor");
        var method = type?.GetMethod("SyncSolution", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        method?.Invoke(null, null);
    }
}
