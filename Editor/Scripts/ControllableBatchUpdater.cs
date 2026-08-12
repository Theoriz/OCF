using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

//Regenerates every mirror in the project in one action. A release that changes the emitted shape - the
//`controllable` prefix, the typed PollTargetScript override - otherwise means walking the project and
//updating each mirror by hand.
public static class ControllableBatchUpdater
{
    #region Menu
    [MenuItem("Theoriz/OCF/Update All Controllables", false, 3100)]
    public static void UpdateAllControllables()
    {
        List<MonoScript> mirrors = FindUpdatableMirrors();
        if (mirrors.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Update All Controllables",
                "No Controllable script to update was found in this project.\n\n" +
                "Scripts inside packages are left alone.",
                "OK");
            return;
        }

        bool proceed = EditorUtility.DisplayDialog(
            "Update All Controllables",
            $"{mirrors.Count} Controllable script(s) will be regenerated from the scripts they mirror.\n\n" +
            "The files are rewritten in place and this cannot be undone.",
            "Update",
            "Cancel");

        if (!proceed)
            return;

        int updated = 0;
        var skipped = new List<string>();

        try
        {
            AssetDatabase.StartAssetEditing();

            for (int i = 0; i < mirrors.Count; i++)
            {
                MonoScript mirror = mirrors[i];
                string path = AssetDatabase.GetAssetPath(mirror);

                EditorUtility.DisplayProgressBar(
                    "Update All Controllables",
                    Path.GetFileName(path),
                    (float)i / mirrors.Count);

                //The mirror's own path, not the source script's: the two need not share a folder, and
                //this has to rewrite the file that was found rather than write a copy beside the source.
                string sourceName = ControllableGenerator.SourceNameFor(mirror.GetClass().Name);

                if (ControllableGenerator.TryGenerateControllableForScript(sourceName, path, out string error))
                    updated++;
                else
                    skipped.Add($"{path}: {error.Replace("\n", " ")}");
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
        }

        //Logged before the Refresh, which induces a domain reload.
        string report = $"[OCF] Updated {updated} Controllable script(s).";
        if (skipped.Count > 0)
            report += $"\nSkipped {skipped.Count}:\n- " + string.Join("\n- ", skipped);

        if (skipped.Count > 0)
            Debug.LogWarning(report);
        else
            Debug.Log(report);

        AssetDatabase.Refresh();
    }

    #endregion

    #region Mirror discovery

    //Every mirror script this project owns that can be regenerated: the same pair of conditions the
    //single-script Update entry validates on, minus anything shipped by a package.
    public static List<MonoScript> FindUpdatableMirrors()
    {
        var mirrors = new List<MonoScript>();

        foreach (string guid in AssetDatabase.FindAssets("t:MonoScript"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".cs") || IsPackagePath(path))
                continue;

            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            if (script == null)
                continue;

            //Null for a script that does not compile, or whose class name differs from its file name.
            Type type = script.GetClass();
            if (type == null)
                continue;

            if (ControllableGenerator.IsMirrorType(type) && ControllableGenerator.IsMirrorName(type.Name))
                mirrors.Add(script);
        }

        return mirrors;
    }

    //Whether an asset belongs to a package rather than to the project's own assets. Anything under
    //Packages/ is one; under Assets/ a package is a folder holding a package.json, which is how the
    //embedded sources of a package under development are recognised without naming any folder.
    public static bool IsPackagePath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return false;

        string path = assetPath.Replace('\\', '/');

        if (path.StartsWith("Packages/"))
            return true;

        //Walk up to, but not including, Assets/ itself.
        string directory = Path.GetDirectoryName(path)?.Replace('\\', '/');
        while (!string.IsNullOrEmpty(directory) && directory != "Assets")
        {
            if (File.Exists(Path.Combine(directory, "package.json")))
                return true;

            directory = Path.GetDirectoryName(directory)?.Replace('\\', '/');
        }

        return false;
    }

    #endregion
}
