using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public class UpdateControllableComponentMenu : Editor
{
    [MenuItem("CONTEXT/Component/Update Controllable", true, 10000)]
    private static bool ValidateMenu(MenuCommand command)
    {
        Component sourceComponent = command.context as Component;
        if (sourceComponent == null)
            return false;

        // Get the MonoScript associated with the component's type
        MonoScript monoScript = MonoScript.FromMonoBehaviour(sourceComponent as MonoBehaviour);

        return monoScript != null;
    }

    [MenuItem("CONTEXT/Component/Update Controllable", false, 10000)]
    public static void UpdateControllable(MenuCommand command)
    {
        Component sourceComponent = command.context as Component;
        if (sourceComponent == null)
            return;

        // Get the MonoScript associated with the component's type
        MonoScript monoScript = MonoScript.FromMonoBehaviour(sourceComponent as MonoBehaviour);

        if (monoScript == null)
        {
            EditorUtility.DisplayDialog(
                "Invalid Component",
                "Controllables only work with MonoBehaviour components.",
                "OK"
            );
            return;
        }

        // Get the asset path
        string path = AssetDatabase.GetAssetPath(monoScript);

        Type sourceType = sourceComponent.GetType();
        string sourceName = sourceType.Name;

        // Either end of the pair works: "PlayerControllable" reports "Player" as its source name, and
        // "Player" reports itself, so both arrive at the same pair.
        string baseName = ControllableGenerator.SourceNameFor(sourceName);
        string controllableName = baseName + ControllableGenerator.MirrorSuffix;

        // The file that would be rewritten, found on disk rather than through the mirror type: a
        // project that no longer compiles keeps the previous type loaded, so its presence says
        // nothing about the file being there.
        string mirrorPath = ControllableGenerator.MirrorPathFor(baseName, path);

        if (!File.Exists(mirrorPath))
        {
            bool generate = EditorUtility.DisplayDialog(
                "Controllable Script Not Found",
                $"No script named '{controllableName}' was found.\n\n" +
                "Would you like to generate it now?",
                "Generate",
                "Cancel"
            );

            if (!generate)
                return;

            ControllableGenerator.GenerateControllableForScript(baseName, path, true);
            return;
        }

        // The shared path, which repairs a mirror that no longer compiles before regenerating it.
        ControllableGenerator.UpdateMirror(controllableName, path);
    }
}
