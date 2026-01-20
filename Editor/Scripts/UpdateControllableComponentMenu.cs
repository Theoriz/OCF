using System;
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

        GameObject go = sourceComponent.gameObject;
        Type sourceType = sourceComponent.GetType();
        string sourceName = sourceType.Name;

        string baseName;
        string controllableName;

        // Logic: Determine if we are starting from the Base or the Controllable
        if (sourceName.EndsWith("Controllable"))
        {
            // We are on the "PlayerControllable", so the base is "Player"
            baseName = sourceName.Substring(0, sourceName.Length - "Controllable".Length);
            controllableName = sourceName;
        }
        else
        {
            // We are on "Player", so the controllable is "PlayerControllable"
            baseName = sourceName;
            controllableName = sourceName + "Controllable";
        }

        // Try to find the controllable type
        Type controllableType = ControllableGenerator.FindType(controllableName);

        if (controllableType == null)
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
        }

        // Generate new controllable script
        ControllableGenerator.GenerateControllableForScript(baseName, path, true);
    }
}
