using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class ControllableGenerator
{
    [MenuItem("Assets/Controllable/Generate Controllable Script", true, 10000)]
    private static bool ValidateMenu()
    {
        TextAsset selected = Selection.activeObject as TextAsset;
        if (!selected) return false;

        string path = AssetDatabase.GetAssetPath(selected);
        return path.EndsWith(".cs");
    }

    [MenuItem("Assets/Controllable/Generate Controllable Script", false, 10000)]
    private static void CreateControllableScript()
    {
        MonoScript selected = Selection.activeObject as MonoScript;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("Error", "Please select a C# script.", "OK");
            return;
        }

        string path = AssetDatabase.GetAssetPath(selected);
        string originalName = Path.GetFileNameWithoutExtension(path);

        GenerateControllableForScript(originalName, path);
    }


    // -------------------------- Helpers --------------------------

    public static void GenerateControllableForScript(string originalName, string originalPath, bool forceReplace = false)
    {
        string directory = Path.GetDirectoryName(originalPath);

        string newName = originalName + "Controllable";
        string newPath = Path.Combine(directory, newName + ".cs");

        // Reflection: try to find the original type
        Type originalType = FindType(originalName);
        if (originalType == null)
        {
            EditorUtility.DisplayDialog("Error",
                $"Could not find compiled type: {originalName}\n" +
                $"Make sure the script compiles with no errors.",
                "OK");
            return;
        }

        // Check existing file
        if (File.Exists(newPath) && !forceReplace)
        {
            bool overwrite = EditorUtility.DisplayDialog(
                "File Already Exists",
                $"{newName}.cs already exists.\n\nReplace it?",
                "Replace",
                "Cancel"
            );

            if (!overwrite)
                return;
        }

        // Extract OSCProperty fields & properties
        string memberDeclarations = ExtractOSCExposedMembers(originalType);

        string scriptContent =
$@"using UnityEngine;

public class {newName} : Controllable
{{
{memberDeclarations}
}}
";

        // Force Windows CRLF
        scriptContent = scriptContent.Replace("\r\n", "\n");
        scriptContent = scriptContent.Replace("\n", "\r\n");

        File.WriteAllText(newPath, scriptContent);
        AssetDatabase.Refresh();

        Debug.Log($"Generated Controllable script: {newName}.cs");
    }

    public static Type FindType(string typeName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                Type t = assembly.GetType(typeName);
                if (t != null) return t;
            }
            catch { /* ignore */ }
        }
        return null;
    }

    private static string ExtractOSCExposedMembers(Type type)
    {
        Type oscAttributeType = FindType("OSCExposed");
        if (oscAttributeType == null)
            return "    // ERROR: Could not find OSCExposed attribute.\r\n";

        // Separate buckets to ensure methods come last
        string variableDeclarations = "";
        string methodDeclarations = "";

        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        // Get all members to preserve source order
        MemberInfo[] members = type.GetMembers(flags);

        foreach (var member in members)
        {
            Attribute oscExposedInstance = member.GetCustomAttribute(oscAttributeType);
            if (oscExposedInstance == null) continue;

            if (member is FieldInfo field)
            {
                if (!field.IsPublic)
                {
                    Debug.LogWarning($"{type.Name}.{field.Name} has [OSCExposed] but is not public. Ignored.");
                    continue;
                }
                string attributes = GetAttributes(field, oscAttributeType, oscExposedInstance);
                variableDeclarations += $"{attributes}    public {ToFriendlyTypeName(field.FieldType)} {field.Name};\r\n\r\n";
            }
            else if (member is PropertyInfo prop)
            {
                MethodInfo getter = prop.GetGetMethod(true);
                bool isPublic = getter != null && getter.IsPublic;

                if (!isPublic)
                {
                    Debug.LogWarning($"{type.Name}.{prop.Name} has [OSCExposed] but is not public. Ignored.");
                    continue;
                }
                string attributes = GetAttributes(prop, oscAttributeType, oscExposedInstance);
                variableDeclarations += $"{attributes}    public {ToFriendlyTypeName(prop.PropertyType)} {prop.Name};\r\n\r\n";
            }
            else if (member is MethodInfo method)
            {
                if (!method.IsPublic || method.IsSpecialName) continue;

                string returnType = ToFriendlyTypeName(method.ReturnType);
                ParameterInfo[] parameters = method.GetParameters();
                string paramList = string.Join(", ", parameters.Select(p => $"{ToFriendlyTypeName(p.ParameterType)} {p.Name}"));
                string paramNames = string.Join(", ", parameters.Select(p => p.Name));

                methodDeclarations += $"    [OSCMethod]\r\n    public {returnType} {method.Name}({paramList})\r\n    {{\r\n        (TargetScript as {type.Name}).{method.Name}({paramNames});\r\n    }}\r\n\r\n";
            }
        }

        string result = variableDeclarations + methodDeclarations;

        if (string.IsNullOrWhiteSpace(result))
            result = "    // No public OSCExposed members found.\r\n";

        return result;
    }

    private static string GetAttributes(MemberInfo member, Type oscAttributeType, Attribute oscInstance)
    {
        string attributes = "";

        // Header
        var header = member.GetCustomAttribute<HeaderAttribute>();
        if (header != null)
            attributes += $"    [Header(\"{header.header}\")]\r\n";

        // Range
        var range = member.GetCustomAttribute<RangeAttribute>();
        if (range != null)
            attributes += $"    [Range({range.min}f, {range.max}f)]\r\n";

        // Tooltip
        var tooltip = member.GetCustomAttribute<TooltipAttribute>();
        if (tooltip != null)
            attributes += $"    [Tooltip(\"{tooltip.tooltip}\")]\r\n";

        // OSCProperty logic
        bool isReadOnly = false;
        var readOnlyField = oscAttributeType.GetField("readOnly");
        if (readOnlyField != null)
        {
            isReadOnly = (bool)readOnlyField.GetValue(oscInstance);
        }
        else
        {
            var readOnlyProp = oscAttributeType.GetProperty("readOnly");
            if (readOnlyProp != null)
                isReadOnly = (bool)readOnlyProp.GetValue(oscInstance);
        }

        string oscPropArgs = isReadOnly ? "(readOnly = true)" : "";
        attributes += $"    [OSCProperty{oscPropArgs}]\r\n";

        return attributes;
    }

    private static string ToFriendlyTypeName(Type t)
    {
        if (t == typeof(int)) return "int";
        if (t == typeof(float)) return "float";
        if (t == typeof(bool)) return "bool";
        if (t == typeof(string)) return "string";
        if (t == typeof(double)) return "double";
        if (t == typeof(long)) return "long";
        if (t == typeof(short)) return "short";
        if (t == typeof(byte)) return "byte";
        if (t == typeof(void)) return "void";

        if (t.IsArray)
            return ToFriendlyTypeName(t.GetElementType()) + "[]";

        return t.Name;
    }
}