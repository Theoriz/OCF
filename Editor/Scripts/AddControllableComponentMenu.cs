using System;
using UnityEditor;
using UnityEngine;

public class AddControllableComponentMenu : Editor
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

        Type sourceType = sourceComponent.GetType();
        string controllableName = sourceType.Name + "Controllable";

        // Try to find the controllable type
        if (ControllableGenerator.FindType(controllableName) == null)
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

            // Generate new controllable script, and remember to finish the job: generating triggers a
            // domain reload, so the component is added by ResumePendingAdds once the type exists.
            PendingControllableAdds.Enqueue(sourceComponent);
            ControllableGenerator.GenerateControllableForScript(sourceType.Name, path);

            return;
        }

        TryAddFor(sourceComponent, interactive: true);
    }

    //Runs after the domain reload that generating a mirror script triggers, completing the requests
    //queued before it. Deferred with delayCall so the work happens once the Editor is idle rather
    //than mid-reload.
    [UnityEditor.Callbacks.DidReloadScripts]
    private static void ResumePendingAdds()
    {
        //Takes and clears in one step - see PendingControllableAdds.TakeAll.
        string[] pending = PendingControllableAdds.TakeAll();
        if (pending.Length == 0)
            return;

        EditorApplication.delayCall += () =>
        {
            foreach (string id in pending)
                ResumeOne(id);
        };
    }

    private static void ResumeOne(string globalObjectId)
    {
        GlobalObjectId parsed;
        if (!GlobalObjectId.TryParse(globalObjectId, out parsed))
        {
            Debug.LogWarning("[OCF] Add Controllable: could not read back the queued target, so no component was added.");
            return;
        }

        //Null when the GameObject was deleted, or its scene was closed or swapped, between the menu
        //click and the reload.
        var sourceComponent = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(parsed) as Component;
        if (sourceComponent == null)
        {
            Debug.LogWarning("[OCF] Add Controllable: the target component no longer exists, so no component was added.");
            return;
        }

        TryAddFor(sourceComponent, interactive: false);
    }

    //Shared by the menu click and the post-reload resume. Failures are reported with a dialog when the
    //user is waiting on one, and with a warning otherwise - a modal appearing by itself seconds after
    //a compile would be worse than the problem this fixes.
    private static void TryAddFor(Component sourceComponent, bool interactive)
    {
        GameObject go = sourceComponent.gameObject;
        Type sourceType = sourceComponent.GetType();
        string controllableName = sourceType.Name + "Controllable";

        Type controllableType = ControllableGenerator.FindType(controllableName);
        if (controllableType == null)
        {
            Report(interactive, "Controllable Script Not Found",
                $"'{controllableName}' was not found. If it was just generated, check the Console for "
                + "compile errors in it.");
            return;
        }

        // Validate inheritance from Controllable
        Type baseControllable = ControllableGenerator.FindType("Controllable"); // or typeof(Controllable) if accessible

        if (baseControllable == null)
        {
            Report(interactive, "Controllable Base Class Not Found",
                "Could not find the 'Controllable' base type in loaded assemblies.");
            return;
        }

        if (!baseControllable.IsAssignableFrom(controllableType))
        {
            Report(interactive, "Invalid Controllable Script",
                $"{controllableName} exists, but it does NOT inherit from 'Controllable'. "
                + "The component will not be added.");
            return;
        }

        //The user may have added it by hand while waiting for the compile.
        if (go.GetComponent(controllableType) != null)
            return;

        AddControllableComponent(go, controllableType, sourceComponent, sourceType);
    }

    private static void Report(bool interactive, string title, string message)
    {
        if (interactive)
            EditorUtility.DisplayDialog(title, message, "OK");
        else
            Debug.LogWarning("[OCF] " + title + ": " + message);
    }

    private static void AddControllableComponent(GameObject go, Type controllableType, Component sourceComponent, Type sourceType)
    {
        //Its own undo entry, deliberately: on the resumed path this lands after a domain reload and
        //cannot be merged with the click that caused it, and writing the generated script is not
        //undoable anyway.
        Component addedComponent = Undo.AddComponent(go, controllableType);

        //The user is probably no longer looking at the Inspector they clicked in, so point at the
        //object as well as logging.
        Debug.Log($"Added {controllableType.Name} to '{go.name}'.");
        EditorGUIUtility.PingObject(go);

        // Initialize added controllable
        Controllable addedControllable = addedComponent as Controllable;
        addedControllable.TargetScript = sourceComponent as MonoBehaviour;
        addedControllable.BarColor = UnityEngine.Random.ColorHSV(0, 1, .6f, 1, 1, 1, 1, 1);
        addedControllable.id = sourceType.Name;
    }
}
