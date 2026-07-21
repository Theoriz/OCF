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

                t = assembly.GetTypes().FirstOrDefault(x => x.Name == typeName || x.FullName == typeName);
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
        string pollComparisons = "";

        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        // Declared members (fields, properties, methods); fields/properties are emitted before methods below.
        MemberInfo[] members = type.GetMembers(flags);

        foreach (var member in members)
        {
            Attribute oscExposedInstance = member.GetCustomAttribute(oscAttributeType);
            if (oscExposedInstance == null) continue;

            //The mirror derives from Controllable, so a member of the same name would shadow the real
            //one: a 'Save' would disable preset handling, an 'id' would break OSC addressing.
            if (Controllable.IsReservedMemberName(member.Name))
            {
                Debug.LogError($"{type.Name}.{member.Name} has [OSCExposed] but collides with a member "
                    + "Controllable already declares. Skipped - rename it to expose it.");
                continue;
            }

            if (member is FieldInfo field)
            {
                if (!field.IsPublic)
                {
                    Debug.LogWarning($"{type.Name}.{field.Name} has [OSCExposed] but is not public. Ignored.");
                    continue;
                }
                string attributes = GetAttributes(field, oscAttributeType, oscExposedInstance);
                variableDeclarations += $"{attributes}    public {ToFriendlyTypeName(field.FieldType)} {field.Name};\r\n\r\n";
                pollComparisons += BuildPollComparison(field.Name, field.FieldType);
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
                pollComparisons += BuildPollComparison(prop.Name, prop.PropertyType);
            }
            else if (member is MethodInfo method)
            {
                if (!method.IsPublic || method.IsSpecialName) continue;

                string returnType = ToFriendlyTypeName(method.ReturnType);
                ParameterInfo[] parameters = method.GetParameters();
                string paramList = string.Join(", ", parameters.Select(p => $"{ToFriendlyTypeName(p.ParameterType)} {p.Name}"));
                string paramNames = string.Join(", ", parameters.Select(p => p.Name));
                string callPrefix = method.ReturnType == typeof(void) ? "" : "return ";

                methodDeclarations += $"    [OSCMethod]\r\n    public {returnType} {method.Name}({paramList})\r\n    {{\r\n        {callPrefix}(TargetScript as {type.FullName}).{method.Name}({paramNames});\r\n    }}\r\n\r\n";
            }
        }

        string result = variableDeclarations + methodDeclarations + BuildPollMethod(type, pollComparisons);

        if (string.IsNullOrWhiteSpace(result))
            result = "    // No public OSCExposed members found.\r\n";

        return result;
    }

    //Controllable's own poll reads every exposed member through reflection, which returns object and
    //so boxes every value type once per frame. This override compares the mirror against the target
    //directly, allocating nothing.
    private static string BuildPollMethod(Type type, string comparisons)
    {
        if (string.IsNullOrWhiteSpace(comparisons))
            return "";

        string typeName = ToFriendlyTypeName(type);

        return "    //Replaces Controllable's reflection-based poll, which boxes every exposed value every frame.\r\n"
             + "    protected override void PollTargetScript()\r\n"
             + "    {\r\n"
             + $"        var target = TargetScript as {typeName};\r\n"
             + "        if (target == null) return;\r\n"
             + "\r\n"
             + comparisons
             + "    }\r\n";
    }

    //The mirror field is assigned before the event is raised so the change is reported once even if
    //nothing else writes the mirror back.
    private static string BuildPollComparison(string name, Type memberType)
    {
        return $"        if ({BuildInequality(name, memberType)}) {{ {name} = target.{name}; RaiseScriptValueChanged(\"{name}\"); }}\r\n";
    }

    //Primitives, strings and enums compare with != for free. Unity's vector and color types are
    //compared component by component instead, for two reasons: their operator!= is an *approximate*
    //compare that would stop reporting small changes, and EqualityComparer<T>.Default boxes for the
    //ones that do not implement IEquatable<T> (which varies by Unity version). Comparing components
    //with Equals is exact - the same answer the reflection poll's object.Equals gives, including for
    //NaN - and allocates nothing on any version.
    private static string BuildInequality(string name, Type memberType)
    {
        if (memberType.IsEnum
            || memberType == typeof(string)
            || memberType.IsPrimitive)
        {
            return $"{name} != target.{name}";
        }

        string[] components = GetComparableComponents(memberType);
        if (components != null)
        {
            string equal = string.Join(" && ", components.Select(c => $"{name}.{c}.Equals(target.{name}.{c})"));
            return $"!({equal})";
        }

        //Anything else: correct, but boxes once per frame if the type has no IEquatable<T>.
        return $"!System.Collections.Generic.EqualityComparer<{ToFriendlyTypeName(memberType)}>.Default.Equals({name}, target.{name})";
    }

    //The fields to compare for the vector and color types OCF supports, or null for anything else.
    private static string[] GetComparableComponents(Type t)
    {
        if (t == typeof(Vector2) || t == typeof(Vector2Int)) return new[] { "x", "y" };
        if (t == typeof(Vector3) || t == typeof(Vector3Int)) return new[] { "x", "y", "z" };
        if (t == typeof(Vector4)) return new[] { "x", "y", "z", "w" };
        if (t == typeof(Color)) return new[] { "r", "g", "b", "a" };

        return null;
    }

    private static string GetAttributes(MemberInfo member, Type oscAttributeType, Attribute oscInstance)
    {
        string attributes = "";

        // Header
        var header = member.GetCustomAttribute<HeaderAttribute>();
        if (header != null)
            attributes += $"    [Header(\"{EscapeString(header.header)}\")]\r\n";

        // Range
        var range = member.GetCustomAttribute<RangeAttribute>();
        if (range != null)
            attributes += $"    [Range({range.min.ToString(System.Globalization.CultureInfo.InvariantCulture)}f, {range.max.ToString(System.Globalization.CultureInfo.InvariantCulture)}f)]\r\n";

        // Tooltip
        var tooltip = member.GetCustomAttribute<TooltipAttribute>();
        if (tooltip != null)
            attributes += $"    [Tooltip(\"{EscapeString(tooltip.tooltip)}\")]\r\n";

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

    //The result is written inside a C# string literal in the generated file, so anything the compiler
    //would not read as plain text has to be escaped. Backslashes go first, or the escapes added below
    //would be escaped a second time. A raw newline would split the literal and stop the file compiling.
    private static string EscapeString(string s)
    {
        if (s == null) return "";
        return s.Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
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

        if (t.IsGenericType)
        {
            string genericName = t.GetGenericTypeDefinition().FullName;
            genericName = genericName.Substring(0, genericName.IndexOf('`')).Replace('+', '.');
            string args = string.Join(", ", t.GetGenericArguments().Select(ToFriendlyTypeName));
            return $"{genericName}<{args}>";
        }

        return (t.FullName ?? t.Name).Replace('+', '.');
    }
}