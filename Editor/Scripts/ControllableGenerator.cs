using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class ControllableGenerator
{
    #region Menu

    [MenuItem("Assets/OCF/Generate Controllable Script", true, 10000)]
    private static bool ValidateMenu()
    {
        //Hidden on a mirror, which gets Update instead: generating from one could only write a
        //FooControllableControllable mirroring a mirror.
        MonoScript selected = SelectedScript();
        return selected != null && MirrorNameFor(selected) == null;
    }

    [MenuItem("Assets/OCF/Generate Controllable Script", false, 10000)]
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

    [MenuItem("Assets/OCF/Update Controllable Script", true, 10001)]
    private static bool ValidateUpdateMenu()
    {
        //The counterpart of the entry above: shown only on a mirror, and only when its source name can
        //be recovered from its own. A hand-written mirror named anything else has nothing to update
        //from, so neither entry appears on it.
        MonoScript selected = SelectedScript();
        return selected != null && IsMirrorName(MirrorNameFor(selected));
    }

    [MenuItem("Assets/OCF/Update Controllable Script", false, 10001)]
    private static void UpdateControllableScript()
    {
        MonoScript selected = SelectedScript();
        if (selected == null) return;

        //Regenerated from the mirror's own path, not from the script it mirrors: the two need not
        //share a folder, and this has to rewrite the file that was clicked rather than write a second
        //copy beside the source.
        UpdateMirror(MirrorNameFor(selected), AssetDatabase.GetAssetPath(selected));
    }

    //The selected asset when it is a C# script, and null otherwise. Both validators need the
    //MonoScript rather than the path, to reach the compiled type behind it.
    private static MonoScript SelectedScript()
    {
        MonoScript selected = Selection.activeObject as MonoScript;
        if (selected == null) return null;

        return AssetDatabase.GetAssetPath(selected).EndsWith(".cs") ? selected : null;
    }

    //The mirror class this script declares, or null when it declares none. The compiled type answers
    //when it is available; when it is not - a stale mirror puts its whole assembly in error, and
    //MonoScript.GetClass() can then return null - the source text answers instead, which is the only
    //reading that survives a failed compile and so the one the validators need.
    public static string MirrorNameFor(MonoScript script)
    {
        if (script == null) return null;

        Type type = script.GetClass();
        if (IsMirrorType(type)) return type.Name;

        //A resolved type that is not a mirror is a definite answer; only an unresolved one falls back.
        return type == null ? MirrorClassNameFromText(script.text) : null;
    }

    #endregion

    #region Mirror naming

    //Every generated mirror is named for the script it mirrors plus this suffix, which is what pairs
    //the two: the file name, the class name and the component all follow from it.
    public const string MirrorSuffix = "Controllable";

    //Whether a type is a mirror rather than a script to be mirrored. The base type decides it and not
    //the name, because a hand-written mirror can be called anything - and the answer gates whether
    //generating from it makes sense at all.
    public static bool IsMirrorType(Type type)
    {
        return type != null && typeof(Controllable).IsAssignableFrom(type);
    }

    //Whether a source name can be recovered from this one. A script named exactly 'Controllable'
    //carries no source name, only the suffix.
    public static bool IsMirrorName(string typeName)
    {
        return !string.IsNullOrEmpty(typeName)
            && typeName.Length > MirrorSuffix.Length
            && typeName.EndsWith(MirrorSuffix);
    }

    //'FooControllable' -> 'Foo', and any other name unchanged, so a source script maps to itself and
    //callers that hold either one can ask without checking first.
    public static string SourceNameFor(string typeName)
    {
        return IsMirrorName(typeName)
            ? typeName.Substring(0, typeName.Length - MirrorSuffix.Length)
            : typeName;
    }

    //A base list starts with the base class, so Controllable is either the end of the declaration or
    //followed by an interface. Reading the text rather than the type is what keeps the menus usable
    //while the assembly is in error - which is exactly when a mirror needs updating.
    static readonly Regex MirrorDeclaration = new Regex(
        @"\bclass\s+(\w+)\s*:\s*(?:[\w.]+\.)?" + MirrorSuffix + @"\s*(?=[,{]|$)",
        RegexOptions.Multiline);

    static readonly Regex NamespaceDeclaration = new Regex(
        @"^\s*namespace\s+([\w.]+)", RegexOptions.Multiline);

    //The mirror class a script's source text declares, or null when it declares none. Pure: the text
    //need not compile, and a mirror whose body no longer does is the case this exists for.
    public static string MirrorClassNameFromText(string scriptText)
    {
        if (string.IsNullOrEmpty(scriptText)) return null;

        Match match = MirrorDeclaration.Match(scriptText);
        return match.Success ? match.Groups[1].Value : null;
    }

    //The namespace a script's source text declares, or null for the global one. A repaired mirror is
    //rewritten from its own text, so this is how it keeps the namespace it was generated into.
    public static string NamespaceFromText(string scriptText)
    {
        if (string.IsNullOrEmpty(scriptText)) return null;

        Match match = NamespaceDeclaration.Match(scriptText);
        return match.Success ? match.Groups[1].Value : null;
    }

    #endregion

    #region Update and repair

    /// <summary>
    /// The single update path both menu entries take: regenerates the mirror, repairing it first when
    /// the project no longer compiles.
    /// </summary>
    /// <remarks>
    /// Regeneration reflects over the source type, so it only works while the project compiles. A
    /// mirror still referencing a renamed member breaks its whole assembly, and Unity keeps the last
    /// successfully compiled assemblies loaded - so reflection would answer with the *stale* members
    /// and write the same broken file back. Emptying the mirror first is what breaks that deadlock.
    /// </remarks>
    public static void UpdateMirror(string mirrorName, string mirrorPath)
    {
        if (!IsMirrorName(mirrorName) || string.IsNullOrEmpty(mirrorPath))
            return;

        string sourceName = SourceNameFor(mirrorName);

        if (!ReflectionIsStale(mirrorPath))
        {
            GenerateControllableForScript(sourceName, mirrorPath, forceReplace: true);
            return;
        }

        //Not confirmed separately: the repair is how this state is updated at all, and the file it
        //rewrites is one it would rewrite anyway. What happened is reported to the Console.
        RepairMirror(mirrorPath, sourceName);
    }

    //Whether reflection can still be trusted to describe the source script. Two readings, because
    //which one reports a broken project depends on whether Unity kept the previous assemblies loaded:
    //the editor-wide compilation state, and the mirror's own type having gone missing.
    private static bool ReflectionIsStale(string mirrorPath)
    {
        if (EditorUtility.scriptCompilationFailed || EditorApplication.isCompiling)
            return true;

        MonoScript mirror = AssetDatabase.LoadAssetAtPath<MonoScript>(mirrorPath);
        return mirror != null && mirror.GetClass() == null;
    }

    //Empties the mirror so the project compiles, and queues the regeneration that follows the reload.
    //The file is rewritten and never deleted: deleting it drops its .meta GUID too, and the mirror
    //written back under a new one would leave every component referencing it a missing script.
    private static void RepairMirror(string mirrorPath, string sourceName)
    {
        //The path the regeneration will write, which need not be the file that was clicked: emptying
        //any other one would blank a file nothing then rewrites.
        string targetPath = MirrorPathFor(sourceName, mirrorPath);
        if (!File.Exists(targetPath))
        {
            EditorUtility.DisplayDialog(
                "Update Controllable Script",
                $"{Path.GetFileName(targetPath)} was not found, so there is nothing to repair.",
                "OK");
            return;
        }

        string existing = File.ReadAllText(targetPath);

        //Queued before the write: the write plus the Refresh below triggers the reload the
        //regeneration has to happen after.
        PendingControllableUpdates.Enqueue(targetPath, sourceName);

        //An empty class body compiles whatever the source script now looks like - including a member
        //whose *type* was renamed, which stripping only the generated method bodies would not survive.
        File.WriteAllText(targetPath, BuildScriptText(
            Path.GetFileNameWithoutExtension(targetPath),
            NamespaceFromText(existing),
            ""));

        //Logged before the Refresh, which induces the domain reload.
        Debug.Log($"[OCF] Emptied {Path.GetFileName(targetPath)} so the project compiles again. "
            + $"It is regenerated from {sourceName} once Unity has reloaded.");

        AssetDatabase.Refresh();
    }

    #endregion

    #region Generation

    public static void GenerateControllableForScript(string originalName, string originalPath, bool forceReplace = false)
    {
        string newPath = MirrorPathFor(originalName, originalPath);

        // Check existing file
        if (File.Exists(newPath) && !forceReplace)
        {
            bool overwrite = EditorUtility.DisplayDialog(
                "File Already Exists",
                $"{Path.GetFileName(newPath)} already exists.\n\nReplace it?",
                "Replace",
                "Cancel"
            );

            if (!overwrite)
                return;
        }

        if (!TryGenerateControllableForScript(originalName, originalPath, out string error))
        {
            EditorUtility.DisplayDialog("Error", error, "OK");
            return;
        }

        AssetDatabase.Refresh();

        Debug.Log($"Generated Controllable script: {Path.GetFileName(newPath)}");
    }

    //Writes the mirror and reports why it could not, without touching the UI or the AssetDatabase: a
    //batch of these must not raise a dialog or force a reimport per file. The prompts and the single
    //Refresh belong to the caller.
    public static bool TryGenerateControllableForScript(string originalName, string originalPath, out string error)
    {
        // Reflection: try to find the original type
        Type originalType = FindType(originalName);
        if (originalType == null)
        {
            error = $"Could not find compiled type: {originalName}\n" +
                    $"Make sure the script compiles with no errors.";
            return false;
        }

        string newPath = MirrorPathFor(originalName, originalPath);
        string newName = Path.GetFileNameWithoutExtension(newPath);

        // Extract OCFProperty fields & properties
        string memberDeclarations = ExtractOCFExposedMembers(originalType);

        string scriptContent = BuildScriptText(newName, originalType.Namespace, memberDeclarations);

        File.WriteAllText(newPath, scriptContent);

        error = null;
        return true;
    }

    //The mirror file for a source name, in the folder of the path it is generated from. Updating passes
    //the mirror's own path, so the file that was clicked is the one rewritten.
    public static string MirrorPathFor(string originalName, string originalPath)
    {
        string directory = Path.GetDirectoryName(originalPath);
        return Path.Combine(directory, originalName + MirrorSuffix + ".cs");
    }

    //The whole text of a generated mirror file. Pure, and public so the emitted shape can be tested
    //without an Editor round-trip: everything that needs the Editor - finding the type, reading its
    //members, writing the file - happens around it.
    public static string BuildScriptText(string controllableName, string namespaceName, string memberDeclarations)
    {
        string classDeclaration =
$@"public class {controllableName} : Controllable
{{
{memberDeclarations}
}}
";

        string scriptContent =
$@"using UnityEngine;

{WrapInNamespace(classDeclaration, namespaceName)}";

        // Force Windows CRLF
        scriptContent = scriptContent.Replace("\r\n", "\n");
        return scriptContent.Replace("\n", "\r\n");
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

    private static string ExtractOCFExposedMembers(Type type)
    {
        Type ocfAttributeType = FindType("OCFExposed");
        if (ocfAttributeType == null)
            return "    // ERROR: Could not find OCFExposed attribute.\r\n";

        // Separate buckets to ensure methods come last
        string variableDeclarations = "";
        string methodDeclarations = "";
        string pollComparisons = "";

        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        // Declared members (fields, properties, methods); fields/properties are emitted before methods below.
        MemberInfo[] members = type.GetMembers(flags);

        foreach (var member in members)
        {
            Attribute ocfExposedInstance = member.GetCustomAttribute(ocfAttributeType);
            if (ocfExposedInstance == null) continue;

            //The mirror derives from Controllable, so a member of the same name would shadow the real
            //one: a 'Save' would disable preset handling, an 'id' would break OSC addressing.
            if (Controllable.IsReservedMemberName(member.Name))
            {
                Debug.LogError($"{type.Name}.{member.Name} has [OCFExposed] but collides with a member "
                    + "Controllable already declares. Skipped - rename it to expose it.");
                continue;
            }

            //A targetList names the List<string> the member is chosen from. It is resolved by name at
            //runtime, so a name that resolves to nothing would fail silently - it is checked here,
            //where the target type is in hand and the user is looking at the console.
            string targetList = ReadAttributeString(ocfAttributeType, ocfExposedInstance, "targetList");
            if (!string.IsNullOrEmpty(targetList) && !(member is MethodInfo)
                && !HasStringList(type, targetList))
            {
                Debug.LogError($"{type.Name}.{member.Name} has [OCFExposed(targetList = \"{targetList}\")] "
                    + $"but {type.Name} has no public List<string> called '{targetList}'. Skipped.");
                continue;
            }

            if (member is FieldInfo field)
            {
                if (!field.IsPublic)
                {
                    Debug.LogWarning($"{type.Name}.{field.Name} has [OCFExposed] but is not public. Ignored.");
                    continue;
                }
                string attributes = GetAttributes(field, ocfAttributeType, ocfExposedInstance, targetList);
                variableDeclarations += $"{attributes}    public {ToFriendlyTypeName(field.FieldType)} {field.Name};\r\n\r\n";
                pollComparisons += BuildPollComparison(field.Name, field.FieldType);
            }
            else if (member is PropertyInfo prop)
            {
                MethodInfo getter = prop.GetGetMethod(true);
                bool isPublic = getter != null && getter.IsPublic;

                if (!isPublic)
                {
                    Debug.LogWarning($"{type.Name}.{prop.Name} has [OCFExposed] but is not public. Ignored.");
                    continue;
                }
                string attributes = GetAttributes(prop, ocfAttributeType, ocfExposedInstance, targetList);
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

                methodDeclarations += $"    [OCFMethod]\r\n    public {returnType} {method.Name}({paramList})\r\n    {{\r\n        {callPrefix}(controllableTargetScript as {type.FullName}).{method.Name}({paramNames});\r\n    }}\r\n\r\n";
            }
        }

        string result = variableDeclarations + methodDeclarations + BuildPollMethod(type, pollComparisons);

        if (string.IsNullOrWhiteSpace(result))
            result = "    // No public OCFExposed members found.\r\n";

        return result;
    }

    //Controllable's own poll reads every exposed member through reflection, which returns object and
    //so boxes every value type once per frame. This override compares the mirror against the target
    //directly, allocating nothing.
    #endregion

    #region Emitted PollTargetScript

    private static string BuildPollMethod(Type type, string comparisons)
    {
        if (string.IsNullOrWhiteSpace(comparisons))
            return "";

        string typeName = ToFriendlyTypeName(type);

        return "    //Replaces Controllable's reflection-based poll, which boxes every exposed value every frame.\r\n"
             + "    protected override void PollTargetScript()\r\n"
             + "    {\r\n"
             + $"        var target = controllableTargetScript as {typeName};\r\n"
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

    #endregion

    #region Source text helpers

    //The mirror is declared in the same namespace as the script it mirrors, so the two are found the
    //same way and the emitted body reaches the source type even when it is only visible from inside
    //that namespace. Nothing binds on the namespace: FindType matches on the short name, and
    //Controllable binds members by name, so a mirror generated before this still resolves.
    private static string WrapInNamespace(string classDeclaration, string namespaceName)
    {
        if (string.IsNullOrEmpty(namespaceName))
            return classDeclaration;

        return $"namespace {namespaceName}\r\n{{\r\n{Indent(classDeclaration)}}}\r\n";
    }

    //Blank lines are left blank rather than filled with spaces, which is what the compiler-agnostic
    //formatting of the rest of the emitted file does too.
    private static string Indent(string text)
    {
        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        return string.Join("\r\n", lines.Select(line => line.Length == 0 ? line : "    " + line));
    }

    //Whether the target script carries a public List<string> under this name, as a field or as a
    //readable property - the two shapes Controllable.GetTargetList resolves at runtime.
    private static bool HasStringList(Type type, string listName)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;

        var field = type.GetField(listName, flags);
        if (field != null && typeof(System.Collections.Generic.List<string>).IsAssignableFrom(field.FieldType))
            return true;

        var property = type.GetProperty(listName, flags);
        return property != null && property.CanRead
            && typeof(System.Collections.Generic.List<string>).IsAssignableFrom(property.PropertyType);
    }

    //The generator never hard-references OCFExposed, so its options are read off the instance by
    //name, tolerating either a field or a property.
    private static string ReadAttributeString(Type attributeType, Attribute instance, string optionName)
    {
        var field = attributeType.GetField(optionName);
        if (field != null)
            return field.GetValue(instance) as string;

        var property = attributeType.GetProperty(optionName);
        return property != null ? property.GetValue(instance) as string : null;
    }

    private static bool ReadAttributeBool(Type attributeType, Attribute instance, string optionName)
    {
        var field = attributeType.GetField(optionName);
        if (field != null)
            return (bool)field.GetValue(instance);

        var property = attributeType.GetProperty(optionName);
        return property != null && (bool)property.GetValue(instance);
    }

    private static string GetAttributes(MemberInfo member, Type ocfAttributeType, Attribute ocfInstance, string targetList)
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

        // OCFProperty logic: every [OCFExposed] option that has an [OCFProperty] equivalent is
        // forwarded, so a generated mirror reaches them without being hand-edited.
        var ocfPropArgs = new System.Collections.Generic.List<string>();

        if (ReadAttributeBool(ocfAttributeType, ocfInstance, "readOnly"))
            ocfPropArgs.Add("readOnly = true");

        if (!string.IsNullOrEmpty(targetList))
            ocfPropArgs.Add($"targetList = \"{EscapeString(targetList)}\"");

        string ocfPropSuffix = ocfPropArgs.Count == 0 ? "" : $"({string.Join(", ", ocfPropArgs)})";
        attributes += $"    [OCFProperty{ocfPropSuffix}]\r\n";

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

    #endregion
}