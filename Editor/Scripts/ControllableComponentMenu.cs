using System;
using UnityEditor;
using UnityEngine;

public class ControllableComponentMenu : Editor
{
    [MenuItem("CONTEXT/Component/Add Controllable", true, 10000)]
    private static bool ValidateMenu(MenuCommand command)
    {
        Component sourceComponent = command.context as Component;
        if (sourceComponent == null)
            return false;

        // Get the MonoScript associated with the component's type
        MonoScript monoScript = MonoScript.FromMonoBehaviour(sourceComponent as MonoBehaviour);

        return monoScript != null;
    }

    [MenuItem("CONTEXT/Component/Add Controllable", false, 10000)]
    public static void AddControllable(MenuCommand command)
    {
        Component sourceComponent = command.context as Component;
        if (sourceComponent == null)
            return;

        // Get the MonoScript associated with the component's type
        MonoScript monoScript = MonoScript.FromMonoBehaviour(sourceComponent as MonoBehaviour);

        if(monoScript == null)
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
        string controllableName = sourceType.Name + "Controllable";

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

            // Generate new controllable script
            ControllableGenerator.GenerateControllableForScript(sourceType.Name, path);

            return;
        }

        // Validate inheritance from Controllable
        Type baseControllable = ControllableGenerator.FindType("Controllable"); // or typeof(Controllable) if accessible

        if (baseControllable == null)
        {
            EditorUtility.DisplayDialog(
                "Controllable Base Class Not Found",
                "Could not find the 'Controllable' base type in loaded assemblies.",
                "OK"
            );
            return;
        }

        if (!baseControllable.IsAssignableFrom(controllableType))
        {
            EditorUtility.DisplayDialog(
                "Invalid Controllable Script",
                $"{controllableName} exists, but it does NOT inherit from 'Controllable'.\n" +
                "The component will not be added.",
                "OK"
            );
            return;
        }

        AddControllableComponent(go, controllableType, sourceComponent, sourceType);
    }

    private static void AddControllableComponent(GameObject go, Type controllableType, Component sourceComponent, Type sourceType)
    {
        Component addedComponent = Undo.AddComponent(go, controllableType);
        Debug.Log($"Added {controllableType.Name} to '{go.name}'.");

        // Initialize added controllable
        Controllable addedControllable = addedComponent as Controllable;
        addedControllable.TargetScript = sourceComponent as MonoBehaviour;
        addedControllable.BarColor = UnityEngine.Random.ColorHSV(0, 1, .6f, 1, 1, 1, 1, 1);
        addedControllable.id = sourceType.Name;
    }
}
