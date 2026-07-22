using UnityEditor;
using UnityEngine;

/// <summary>
/// Adds an Open Presets Folder button to the ControllableMaster inspector, so the folder is reachable
/// without entering Play mode - the panel button only exists while the scene is running.
/// </summary>
[CustomEditor(typeof(ControllableMaster))]
public class ControllableMasterEditor : Editor
{
    //Reported by the OSC connection rather than set by hand, so these are drawn disabled - the panel
    //already exposes both as [OCFProperty(readOnly = true)], and Start overwrites IPAddress anyway.
    static readonly string[] ReadOnlyFields =
    {
        nameof(ControllableMaster.IsConnected),
        nameof(ControllableMaster.IPAddress),
    };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var property = serializedObject.GetIterator();
        var enterChildren = true;

        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;

            //m_Script is the object-field Unity draws greyed out at the top of every inspector.
            var disabled = property.propertyPath == "m_Script"
                        || System.Array.IndexOf(ReadOnlyFields, property.propertyPath) >= 0;

            using (new EditorGUI.DisabledScope(disabled))
                EditorGUILayout.PropertyField(property, true);
        }

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();

        if (GUILayout.Button("Open Presets Folder"))
        {
            //The root is cached for the run, but customPresetDirectory can be edited between clicks
            //in the Editor, so resolve it again rather than opening the folder it used to be.
            ControllableMaster.InvalidatePresetRoot();

            ((ControllableMaster)target).OpenPresetsFolder();
        }
    }
}
