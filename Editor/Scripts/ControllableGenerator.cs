using System;
using System.IO;
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

        string directory = Path.GetDirectoryName(path);
        string originalName = Path.GetFileNameWithoutExtension(path);

        GenerateControllableForScript(originalName, path);
    }


    // -------------------------- Helpers --------------------------

    public static void GenerateControllableForScript(string originalName, string originalPath)
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
        if (File.Exists(newPath))
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

    private static Type FindAttributeTypeByNames(params string[] names)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var name in names)
            {
                var t = asm.GetType(name);
                if (t != null && typeof(Attribute).IsAssignableFrom(t))
                    return t;
            }
        }
        return null;
    }

    private static string ExtractOSCExposedMembers(Type type)
    {
        Type oscAttribute = FindType("OSCExposed");
        if (oscAttribute == null)
            return "    // ERROR: Could not find OSCExposed attribute.\r\n";

        string result = "";
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // Fields
        foreach (var field in type.GetFields(flags))
        {
            if (!Attribute.IsDefined(field, oscAttribute)) continue;

            if (!field.IsPublic)
            {
                Debug.LogWarning($"{type.Name}.{field.Name} has [OSCExposed] but is not public. Ignored.");
                continue;
            }

            string attributes = "";

            // Range
            var range = field.GetCustomAttribute<RangeAttribute>();
            if (range != null)
                attributes += $"    [Range({range.min}f, {range.max}f)]\r\n";

            // Header
            var header = field.GetCustomAttribute<HeaderAttribute>();
            if (header != null)
                attributes += $"    [Header(\"{header.header}\")]\r\n";

            // Tooltip
            var tooltip = field.GetCustomAttribute<TooltipAttribute>();
            if (tooltip != null)
                attributes += $"    [Tooltip(\"{tooltip.tooltip}\")]\r\n";

            // OSCProperty
            attributes += $"    [OSCProperty]\r\n";

            result += $"{attributes}    public {ToFriendlyTypeName(field.FieldType)} {field.Name};\r\n";
        }

        // Properties -> generate as public fields
        foreach (var prop in type.GetProperties(flags))
        {
            if (!Attribute.IsDefined(prop, oscAttribute)) continue;

            MethodInfo getter = prop.GetGetMethod(true);
            bool isPublic = getter != null && getter.IsPublic;

            if (!isPublic)
            {
                Debug.LogWarning($"{type.Name}.{prop.Name} has [OSCExposed] but is not public. Ignored.");
                continue;
            }

            result += $"    [OSCProperty]\r\n    public {ToFriendlyTypeName(prop.PropertyType)} {prop.Name};\r\n";
        }

        // Methods
        foreach (var method in type.GetMethods(flags))
        {
            if (!Attribute.IsDefined(method, oscAttribute)) continue;

            if (!method.IsPublic)
            {
                Debug.LogWarning($"{type.Name}.{method.Name} has [OSCExposed] but is not public. Ignored.");
                continue;
            }

            if (method.IsSpecialName) continue; // skip property accessors/operators

            string returnType = ToFriendlyTypeName(method.ReturnType);
            ParameterInfo[] parameters = method.GetParameters();
            string paramList = "";
            string paramNames = "";
            for (int i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                paramList += $"{ToFriendlyTypeName(p.ParameterType)} {p.Name}";
                paramNames += p.Name;
                if (i < parameters.Length - 1)
                {
                    paramList += ", ";
                    paramNames += ", ";
                }
            }

            result += $"\r\n    [OSCMethod]\r\n    public {returnType} {method.Name}({paramList})\r\n    {{\r\n        (TargetScript as {type.Name}).{method.Name}({paramNames});\r\n    }}\r\n";
        }

        if (string.IsNullOrWhiteSpace(result))
            result = "    // No public OSCExposed members found.\r\n";

        return result;
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
