using UnityEngine;
using System.Collections;
using System.Reflection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

[Serializable]
public class ControllableData
{
    public string dataID;

    public List<string> nameList;
    public List<string> valueList;

    public ControllableData()
    {
        nameList = new List<string>();
        valueList = new List<string>();
    }
}

public class ClassMethodInfo
{
    public MethodInfo methodInfo;
    public bool fromTargetScript;
}

public class ClassAttributInfo
{
    public FieldInfo Field;
    public PropertyInfo Property;

    private string _fieldType;
    public Type FieldType
    {
        get
        {
            Type result = null;

            if (Field != null)
                result = Field.FieldType;

            if (Property != null)
                result = Property.PropertyType;

            return result;
        }
    }


    //private string _name;
    public string Name
    {
        get
        {
            string result = "";

            if (Field != null)
            {
                result = Field.Name;
            }

            if (Property != null)
                result = Property.Name;

            return result;
        }
    }

    public object GetValue(object obj)
    {
        object result = null;
        if (Property != null)
        {
            result = Property.GetValue(obj, null);
        }

        if (Field != null)
            result = Field.GetValue(obj);

        return result;
    }

    public void SetValue(object obj, object value)
    {
        if (Property != null)
            Property.SetValue(obj, value, null);

        if (Field != null)
            Field.SetValue(obj, value);
    }
}

public class Controllable : MonoBehaviour
{
    [Header("Controllable settings")]

    [FormerlySerializedAs("TargetScript")]
    public MonoBehaviour controllableTargetScript;

    [FormerlySerializedAs("id")]
    public string controllableId;

    [HideInInspector]
    [FormerlySerializedAs("folder")]
    public string controllableFolder = "";

    [FormerlySerializedAs("debug")]
    public bool controllableDebug = false;

    [HideInInspector]
    [FormerlySerializedAs("targetDirectory")]
    public string controllableTargetDirectory;

    [HideInInspector]
    [FormerlySerializedAs("sourceScene")]
    public string controllableSourceScene;

    [HideInInspector]
    [FormerlySerializedAs("usePresets")]
    public bool controllableUsePresets = true;

    [System.NonSerialized] public Dictionary<string, FieldInfo> controllableFields;
    [System.NonSerialized] public List<object> controllablePreviousFieldsValues;
    [System.NonSerialized] public Dictionary<string, ClassAttributInfo> controllableTargetFields;

    [System.NonSerialized] public Dictionary<string, ClassMethodInfo> controllableMethods;

    // Snapshot of controllableTargetFields.Values in insertion order, aligned index-for-index with controllablePreviousFieldsValues; iterated in Update to detect target-script changes.
    private ClassAttributInfo[] _targetFieldsArray;

    // Member name to its index in that array, so a write can refresh the value the poll compares against.
    private Dictionary<string, int> _targetFieldIndices;

    public delegate void UIValueChangedEvent(string name);

    public event UIValueChangedEvent controllableUiValueChanged;

    public delegate void ControllableValueChangedEvent(string name);

    public event ControllableValueChangedEvent controllableValueChanged;

    public delegate void ScriptValueChangedEvent(string name);

    public event ScriptValueChangedEvent controllableScriptValueChanged;
    [HideInInspector]
    [FormerlySerializedAs("currentPreset")]
    [OCFProperty(targetList = "controllablePresetList", includeInPresets = false)] public string controllableCurrentPreset;
    [HideInInspector]
    [FormerlySerializedAs("presetList")]
    public List<string> controllablePresetList;

    private string lastUsedPresetFileName = "_lastUsedPreset.txt";

    #region MonoBehaviour

    public virtual void Awake()
    {
        controllableDebug = false;

        if (controllableTargetScript == null)
            Debug.LogError("controllableTargetScript of " + this.GetType().ToString() + " is not set ! Aborting initialization.");

        this.controllableScriptValueChanged += OnScriptValueChanged;
        this.controllableUiValueChanged += OnUiValueChanged;

        //FIELDS
        controllableFields = new Dictionary<string, FieldInfo>();
        controllableTargetFields = new Dictionary<string, ClassAttributInfo>();
        controllablePreviousFieldsValues = new List<object>();

        FieldInfo[] objectFields = this.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);
        FieldInfo[] scriptFields = objectFields;
        PropertyInfo[] scriptProperties = null;
        if (controllableTargetScript != null)
        {
            scriptFields = controllableTargetScript.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);
            scriptProperties = controllableTargetScript.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
        }

        for (int i = 0; i < objectFields.Length; i++)
        {
            FieldInfo info = objectFields[i];
            
            OCFProperty attribute = Attribute.GetCustomAttribute(info, typeof(OCFProperty)) as OCFProperty;
            if (attribute != null)
            {
                if (info.Name == "controllableCurrentPreset" && !controllableUsePresets) continue;

                //Unlike a method, a shadowing field cannot be worked around at runtime: it really
                //exists on the derived class and Unity serializes it, while Controllable's own code
                //keeps reading the base field. All that can be done is report it.
                if (info.DeclaringType != typeof(Controllable) && _reservedMemberNames.Contains(info.Name))
                    Debug.LogWarning("[OCF] " + GetType().Name + ": [OCFProperty] '" + info.Name
                        + "' shadows a member Controllable already declares. The real member is now "
                        + "unreachable and may misbehave - rename it.");

                //A shadowed base field can surface here alongside the one that shadows it; both carry
                //the same name and only one OSC address exists. The warning above already reported it.
                if (controllableFields.ContainsKey(info.Name))
                    continue;

                controllableFields.Add(info.Name, info);

                var fieldAdded = false;
                var propertyAdded = false;
                for (int j = 0; j < scriptFields.Length; j++)
                {
                    if (scriptFields[j].Name == info.Name)
                    {
                        var newClassAttributInfo = new ClassAttributInfo();
                        newClassAttributInfo.Field = scriptFields[j];

                        controllableTargetFields.Add(scriptFields[j].Name, newClassAttributInfo);

                        controllablePreviousFieldsValues.Add(newClassAttributInfo.GetValue(controllableTargetScript));
                        info.SetValue(this, newClassAttributInfo.GetValue(controllableTargetScript));
                        fieldAdded = true;
                        break;
                    }
                }

                if (!fieldAdded) 
                {
                    for (int j = 0; j < scriptProperties.Length; j++)
                    {
                        if (scriptProperties[j].Name == info.Name)
                        {
                            var newClassAttributInfo = new ClassAttributInfo();
                            newClassAttributInfo.Property = scriptProperties[j];

                            controllableTargetFields.Add(scriptProperties[j].Name, newClassAttributInfo);
                            controllablePreviousFieldsValues.Add(newClassAttributInfo.GetValue(controllableTargetScript));
                            info.SetValue(this, newClassAttributInfo.GetValue(controllableTargetScript));

                            propertyAdded = true;
                            break;
                        }
                    }
                }
                
                // if(addedFieldName != "")
                //     controllablePreviousFieldsValues.Add(controllableTargetFields[addedFieldName].GetValue(this));

                if (!fieldAdded && !propertyAdded && info.Name != "controllableCurrentPreset")
                    Debug.LogWarning("[OCF] " + GetType().Name + ": [OCFProperty] '" + info.Name
                        + "' has no matching public field/property on target '"
                        + (controllableTargetScript != null ? controllableTargetScript.GetType().Name : "null")
                        + "'. It will not be controllable.");
            }
        }

        _targetFieldsArray = controllableTargetFields.Values.ToArray();

        _targetFieldIndices = new Dictionary<string, int>(_targetFieldsArray.Length);
        for (int i = 0; i < _targetFieldsArray.Length; i++)
            _targetFieldIndices[_targetFieldsArray[i].Name] = i;

        //METHODS
        controllableMethods = new Dictionary<string, ClassMethodInfo>();

        //Controllable's own [OCFMethod] members are registered straight from typeof(Controllable) and
        //never consult controllableTargetScript, so a target script's same-named method cannot displace them.
        //They are taken from the base type rather than from this.GetType()'s method list because a
        //derived class declaring the same signature hides the base method from GetMethods entirely.
        foreach (var builtIn in typeof(Controllable).GetMethods(BindingFlags.Instance | BindingFlags.Public))
        {
            if (Attribute.GetCustomAttribute(builtIn, typeof(OCFMethod)) == null) continue;
            if (Array.IndexOf(PresetMethodNames, builtIn.Name) >= 0 && !controllableUsePresets) continue;

            var builtInMethodInfo = new ClassMethodInfo();
            builtInMethodInfo.methodInfo = builtIn;
            builtInMethodInfo.fromTargetScript = false;

            controllableMethods.Add(builtIn.Name, builtInMethodInfo);
        }

        MethodInfo[] methodFields = this.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public);

        for (int i = 0; i < methodFields.Length; i++)
        {
            MethodInfo info = methodFields[i];
            OCFMethod attribute = Attribute.GetCustomAttribute(info, typeof(OCFMethod)) as OCFMethod;
            if (attribute != null)
            {
                //Already registered above, from the base type.
                if (_builtInOCFMethodNames.Contains(info.Name))
                {
                    if (info.DeclaringType != typeof(Controllable))
                        Debug.LogWarning("[OCF] " + GetType().Name + ": [OCFMethod] '" + info.Name
                            + "' reuses the name of a built-in Controllable method. It is ignored and "
                            + "the built-in is used instead - rename it if you want it exposed.");
                    continue;
                }

                if (controllableMethods.ContainsKey(info.Name))
                {
                    Debug.LogWarning("[OCF] " + GetType().Name + ": [OCFMethod] '" + info.Name
                        + "' is declared more than once (overloads share a single OSC address). "
                        + "Only the first is exposed - rename the others.");
                    continue;
                }

                var classMethodInfo = new ClassMethodInfo();
                classMethodInfo.methodInfo = info;
                classMethodInfo.fromTargetScript = false;

                //Matched on signature, not just name: a target's Foo(int) must not bind to a
                //parameterless Foo(), and a name-only lookup throws on overloaded targets.
                var parameterTypes = info.GetParameters().Select(p => p.ParameterType).ToArray();
                var targetScriptMethod = controllableTargetScript != null
                    ? controllableTargetScript.GetType().GetMethod(info.Name,
                        BindingFlags.Instance | BindingFlags.Public, null, parameterTypes, null)
                    : null;

                if (targetScriptMethod != null)
                {
                    classMethodInfo.methodInfo = targetScriptMethod;
                    classMethodInfo.fromTargetScript = true;
                }

                controllableMethods.Add(info.Name, classMethodInfo);
            }
        }

        if (string.IsNullOrEmpty(controllableId))
            controllableId = controllableTargetScript != null ? controllableTargetScript.GetType().Name : GetType().Name;

        controllableId = controllableId.Replace(' ', '_');
        controllableSourceScene = SceneManager.GetActiveScene().name;
    }

    public virtual void OnEnable()
    {
		if (controllableDebug)
			Debug.Log("Registering " + this.GetType().Name + " script as " + controllableId);
        ControllableMaster.Register(this);

        if (controllableUsePresets)
        {
            controllablePresetList = new List<string>();
            ReadFileList();

            if (controllablePresetList.Count >= 1)
            {
                controllableCurrentPreset = controllablePresetList[0];
                LoadLatestUsedPreset();
            }
        }
    }

	public virtual void Update() //Warn UI if attribut changes
    {
        PollTargetScript();
    }

    public virtual void OnDisable()
    {
        if (controllableDebug)
            Debug.Log("Saving temp file with : " + controllableCurrentPreset);

        if (controllableUsePresets)
        {
            WriteLastUsedPreset();
        }

        if (controllableDebug)
            Debug.Log("Done saving");

		if (controllableDebug)
			Debug.Log("Unregistering " + this.GetType().Name + " script on " + this.gameObject.name);

		ControllableMaster.UnRegister(this);
    }

    #endregion

    #region Mirror synchronisation

    public virtual void OnScriptValueChanged(string name)
    {
        if (String.IsNullOrEmpty(name) || !controllableTargetFields.ContainsKey(name))
        {
            if (controllableDebug)
                Debug.Log("Name : " + name + " is null in target");
            return;
        }
        controllableFields[name].SetValue(this, controllableTargetFields[name].GetValue(controllableTargetScript));
        RaiseEventValueChanged(name);
    }

    public virtual void OnUiValueChanged(string name)
    {
        if (String.IsNullOrEmpty(name) || !controllableTargetFields.ContainsKey(name)) {
            if (controllableDebug)
            {
                Debug.Log("Name : " + name + " doesn't exist in target");
            }
            return;
        }
        controllableTargetFields[name].SetValue(controllableTargetScript, controllableFields[name].GetValue(this));

        //The UI just wrote this value, so record it as the polled baseline too. Without this the
        //next poll reads a value it has not seen before and reports it as a script-side change.
        MarkPolledValueCurrent(name);
    }

    //Refreshes what PollTargetScript compares against, for a member OCF itself has just written.
    private void MarkPolledValueCurrent(string name)
    {
        if (_targetFieldIndices == null || !_targetFieldIndices.TryGetValue(name, out int index))
            return;

        controllablePreviousFieldsValues[index] = _targetFieldsArray[index].GetValue(controllableTargetScript);
    }

    /// <summary>
    /// Detects changes made to the target script and raises <see cref="controllableScriptValueChanged"/>
    /// for each one.
    /// </summary>
    /// <remarks>
    /// This default reads every exposed member through reflection, which returns <c>object</c> and
    /// so boxes every float, int, bool, Vector and Color once per frame whether or not it changed.
    /// Generated mirrors override it with direct typed comparisons that allocate nothing; this
    /// implementation is what hand-written mirrors, and generated ones not yet regenerated, still
    /// run on.
    /// </remarks>
    protected virtual void PollTargetScript()
    {
        if (_targetFieldsArray == null)
            return;

        for (var i = 0; i < _targetFieldsArray.Length; i++)
        {
            var value = _targetFieldsArray[i].GetValue(controllableTargetScript);

            if (!object.Equals(value, controllablePreviousFieldsValues[i]))
            {
                RaiseScriptValueChanged(_targetFieldsArray[i].Name);

                controllablePreviousFieldsValues[i] = value;
            }
        }
    }

    /// <summary>
    /// Raises <see cref="controllableScriptValueChanged"/>. An event can only be raised from the type
    /// that declares it, so an overriding <see cref="PollTargetScript"/> needs this.
    /// </summary>
    protected void RaiseScriptValueChanged(string name)
    {
        if (controllableScriptValueChanged != null)
            controllableScriptValueChanged(name);
    }

    #endregion

    #region Reserved names

    //Controllable's own [OCFMethod] members: the four preset methods plus LoadWithName. These are
    //always bound to Controllable's implementation, never to a target script's same-named method.
    static readonly HashSet<string> _builtInOCFMethodNames = new HashSet<string>(
        typeof(Controllable).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(m => Attribute.GetCustomAttribute(m, typeof(OCFMethod)) != null)
            .Select(m => m.Name));

    //Every public member Controllable exposes, including those inherited from MonoBehaviour (name,
    //tag, transform, enabled...). A mirror declaring any of these names shadows the real member.
    static readonly HashSet<string> _reservedMemberNames = new HashSet<string>(
        typeof(Controllable).GetMembers(BindingFlags.Instance | BindingFlags.Public)
            .Select(m => m.Name));

    /// <summary>
    /// True if <paramref name="name"/> is the name of a member Controllable already exposes.
    /// An [OCFExposed] member must not reuse one: the generated mirror would declare a member of the
    /// same name, shadowing the real one and breaking it silently.
    /// </summary>
    public static bool IsReservedMemberName(string name)
    {
        return _reservedMemberNames.Contains(name);
    }

    #endregion

    #region Presets

    public void LoadLatestUsedPreset()
    {
        //Check if the file recording the last used preset exists
        if (!File.Exists(controllableTargetDirectory + lastUsedPresetFileName)) return;

        var file = new StreamReader(controllableTargetDirectory + lastUsedPresetFileName);

        var lastPresetRead =  file.ReadLine();
        file.Close();

        if (controllableDebug)
            Debug.Log("LastUsedPreset for "+controllableId+" : " + lastPresetRead);

        if (string.IsNullOrEmpty(lastPresetRead)) return;

        controllableCurrentPreset = lastPresetRead;
        ControllableLoad();

        if (controllableScriptValueChanged != null) controllableScriptValueChanged("controllableCurrentPreset");
        //if (controllableUiValueChanged != null) controllableUiValueChanged("controllableCurrentPreset");
        RaiseEventValueChanged("controllableCurrentPreset");
    }

    public void ReadFileList()
    {
        controllablePresetList.Clear();

        UpdateTargetDirectory();

        Directory.CreateDirectory(controllableTargetDirectory);

        MigrateLegacyLastUsedPreset();

        foreach (var t in Directory.GetFiles(controllableTargetDirectory))
        {
            var onlyFileName = Path.GetFileName(t);
            //Only .pst files are presets; the last-used-preset marker (.txt) is excluded by extension
            if (onlyFileName.Split('.').Last() != "pst") continue;
            controllablePresetList.Add(onlyFileName);
        }

        if (controllableScriptValueChanged != null) controllableScriptValueChanged("controllableCurrentPreset");
        RaiseEventValueChanged("controllableCurrentPreset");
    }

    //The preset methods below, by name. Callers identify preset buttons/methods by matching against
    //this rather than their displayed label, which is a derived string (see GenUI's ParseNameString).
    public static readonly string[] PresetMethodNames =
        { "ControllableSave", "ControllableSaveAs", "ControllableLoad", "ControllableShow" };

    [OCFMethod]
    public void ControllableSave()
    {
        if (string.IsNullOrEmpty(controllableCurrentPreset))
        {
            ControllableSaveAs();
            return;
        }

        ControllableSave(controllableCurrentPreset);
    }

    [OCFMethod]
    public void ControllableSaveAs()
    {

        var date = DateTime.Today.Day + "-" + DateTime.Today.Month + "-" + DateTime.Today.Year + "_" +
                   DateTime.Now.Hour + "-" + DateTime.Now.Minute + "-" + DateTime.Now.Second;
        var fileName = date + ".pst";

        ControllableSave(fileName);
    }

    private void ControllableSave(string fileName)
    {

        UpdateTargetDirectory();

        if (controllableDebug)
            Debug.Log("Saving in " + controllableTargetDirectory + fileName + "...");
        //create file
        if (!Directory.Exists(controllableTargetDirectory)) Directory.CreateDirectory(controllableTargetDirectory);

        CallMeBeforeSave();
        File.WriteAllText(controllableTargetDirectory + fileName, JsonUtility.ToJson(this.GetData()));

        if (controllableDebug)
            Debug.Log("Saved in " + controllableTargetDirectory + fileName);

        controllableCurrentPreset = fileName;
        WriteLastUsedPreset();

        ReadFileList();
    }

    [OCFMethod]
    public void ControllableLoad()
    {
        ControllableLoadWithName(controllableCurrentPreset);
    }

    [OCFMethod]
    public void ControllableShow() //Show preset file in explorer
    {
        if (string.IsNullOrEmpty(controllableCurrentPreset)) return;

        var itemPath = controllableTargetDirectory + controllableCurrentPreset;
        itemPath = itemPath.Replace(@"/", @"\");   // explorer doesn't like front slashes
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        System.Diagnostics.Process.Start("explorer.exe", "/select," + itemPath);
#else
        //Only Explorer can select the file itself, so elsewhere open the folder holding it.
        ControllableMaster.OpenFolder(controllableTargetDirectory);
#endif
    }

    [OCFMethod]
    public void ControllableLoadWithName(string fileName)
    {
        if (!fileName.EndsWith(".pst"))
            fileName += ".pst";

        if (controllableDebug)
            Debug.Log("Loading " + fileName + " preset for " + controllableId);

        StreamReader file;
        try
        {
            file = new StreamReader(controllableTargetDirectory + fileName);
            ControllableData cData = JsonUtility.FromJson<ControllableData>(file.ReadLine());
            LoadData(cData);
            file.Close();
        }
        catch (Exception e)
        {
            Debug.LogError("Error while loading preset : " + e.Message + e.StackTrace);
            return;
        }
        controllableCurrentPreset = fileName;
        WriteLastUsedPreset();
    }

    //Override it if you want to do things after a load
    public virtual void DataLoaded() { }
    //Override it if you want to do things before a preset save
    public virtual void CallMeBeforeSave() { }

    #endregion

    #region Members and OSC

    public FieldInfo GetFieldInfoByName(string requestedName)
    {
        var objectFields = GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);
        FieldInfo requestedField = null;

        foreach (var item in objectFields)
        {
            if(item.Name == requestedName)
                requestedField = item;
        }

        return requestedField;
    }

    /// <summary>
    /// The live <c>List&lt;string&gt;</c> an <c>[OCFProperty(targetList = "...")]</c> name refers to,
    /// or null when the name resolves to nothing usable.
    /// </summary>
    /// <remarks>
    /// The mirror is searched first, because that is where a hand-written mirror declares its list and
    /// where <see cref="controllablePresetList"/> lives; the target script is searched second, because a generated
    /// mirror declares no list of its own and the user's list stays on their own script. Resolved on
    /// every call rather than cached: a caller only asks when its own member changed, never per frame,
    /// and reading live means entries added at runtime are always the ones shown.
    /// </remarks>
    public List<string> GetTargetList(string listName)
    {
        if (string.IsNullOrEmpty(listName))
            return null;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;

        var mirrorField = GetType().GetField(listName, flags);
        if (mirrorField != null && typeof(List<string>).IsAssignableFrom(mirrorField.FieldType))
            return mirrorField.GetValue(this) as List<string>;

        if (controllableTargetScript == null)
            return null;

        var targetField = controllableTargetScript.GetType().GetField(listName, flags);
        if (targetField != null && typeof(List<string>).IsAssignableFrom(targetField.FieldType))
            return targetField.GetValue(controllableTargetScript) as List<string>;

        var targetProperty = controllableTargetScript.GetType().GetProperty(listName, flags);
        if (targetProperty != null && targetProperty.CanRead
            && typeof(List<string>).IsAssignableFrom(targetProperty.PropertyType))
            return targetProperty.GetValue(controllableTargetScript) as List<string>;

        return null;
    }

    protected void RaiseEventValueChanged(string property)
    {
        if (!this.enabled)
            return;

        if (controllableValueChanged != null)
            controllableValueChanged(property);
    }

    public void SetProp(string property, List<object> values)
    {
        FieldInfo info = GetPropInfoForAddress(property);
        if (info != null)
        {
            SetFieldProp(info, values);
            return;
        }

        ClassMethodInfo mInfo = GetMethodInfoForAddress(property);
        if (mInfo != null)
        {
            SetMethodProp(mInfo, values);
            return;
        }
    }

    // A [Range] on the mirrored field is a declared constraint on the value, so it is enforced
    // here for every source (OSC, presets, UI) rather than only by the slider widget.
    static float ClampToRange(FieldInfo info, float value)
    {
        var range = Attribute.GetCustomAttribute(info, typeof(RangeAttribute)) as RangeAttribute;
        if (range == null)
            return value;

        return Mathf.Clamp(value, range.min, range.max);
    }

    public void SetFieldProp(FieldInfo info, List<object> values)
    {
        OCFProperty attribute = Attribute.GetCustomAttribute(info, typeof(OCFProperty)) as OCFProperty;

        if (attribute.readOnly == true)
            return;

        string typeString = info.FieldType.ToString();

        if(controllableDebug)
            Debug.Log("Setting attribut " + info.Name + " of type " + typeString + " with " + values.Count + " value(s)");

        // if we detect any attribute print out the data.

        //An enum is dispatched on the field's Type rather than on its name, so the members and their
        //declared values come from the FieldInfo itself - a name, an underlying value or the member
        //all resolve, and nothing has to be told which assembly the type lives in.
        if (info.FieldType.IsEnum)
        {
            if (values.Count >= 1)
            {
                if (TypeConverter.TryGetEnumValue(info.FieldType, values[0], out var enumValue))
                    info.SetValue(this, enumValue);
                else
                    Debug.LogWarning("[OCF] " + values[0] + " is not a value of " + info.FieldType.Name
                        + " for '" + info.Name + "'. Expected one of: "
                        + TypeConverter.DescribeEnumValues(info.FieldType) + ".");
            }
        }
        else
        {
            if (typeString == "System.Single")
            {
                if (values.Count >= 1) info.SetValue(this, ClampToRange(info, TypeConverter.GetFloat(values[0])));
            }
            else if (typeString == "System.Boolean")
            {
                if (values.Count >= 1) info.SetValue(this, TypeConverter.GetBool(values[0]));
            }
            else if (typeString == "System.Int32")
            {
                if (values.Count >= 1) info.SetValue(this, (int)ClampToRange(info, TypeConverter.GetInt(values[0])));
            }
            else if (typeString == "UnityEngine.Vector2")
            {
                if (values.Count == 1) info.SetValue(this, (Vector2)values[0]);
                if (values.Count >= 2) info.SetValue(this, new Vector2(TypeConverter.GetFloat(values[0]), TypeConverter.GetFloat(values[1])));
            } 
            else if (typeString == "UnityEngine.Vector2Int") 
            {
                if (values.Count == 1) info.SetValue(this, (Vector2Int)values[0]);
                if (values.Count >= 2) info.SetValue(this, new Vector2Int(TypeConverter.GetInt(values[0]), TypeConverter.GetInt(values[1])));
            } 
            else if (typeString == "UnityEngine.Vector3")
            {
                if (values.Count == 1) info.SetValue(this, (Vector3)values[0]);
                if (values.Count >= 3) info.SetValue(this, new Vector3(TypeConverter.GetFloat(values[0]), TypeConverter.GetFloat(values[1]), TypeConverter.GetFloat(values[2])));
            } 
            else if (typeString == "UnityEngine.Vector3Int")
            {
                if (values.Count == 1) info.SetValue(this, (Vector3Int)values[0]);
                if (values.Count >= 3) info.SetValue(this, new Vector3Int(TypeConverter.GetInt(values[0]), TypeConverter.GetInt(values[1]), TypeConverter.GetInt(values[2])));
            }
            else if (typeString == "UnityEngine.Vector4")
            {
                if (values.Count == 1) info.SetValue(this, (Vector4)values[0]);
                if (values.Count >= 4) info.SetValue(this, new Vector4(TypeConverter.GetFloat(values[0]), TypeConverter.GetFloat(values[1]), TypeConverter.GetFloat(values[2]), TypeConverter.GetFloat(values[3])));
            }
            else if (typeString == "UnityEngine.Color")
            {
                if (values.Count == 1) info.SetValue(this, (Color)values[0]);
                else if (values.Count >= 4) info.SetValue(this, new Color(TypeConverter.GetFloat(values[0]), TypeConverter.GetFloat(values[1]), TypeConverter.GetFloat(values[2]), TypeConverter.GetFloat(values[3])));
                else if (values.Count >= 3) info.SetValue(this, new Color(TypeConverter.GetFloat(values[0]), TypeConverter.GetFloat(values[1]), TypeConverter.GetFloat(values[2]), 1));
            }
            else if (typeString == "System.String")
            {
                // Debug.Log("String received : " + values.ToString());
                info.SetValue(this, values[0].ToString());
            }
        }
        if (controllableUiValueChanged != null) controllableUiValueChanged(info.Name);

        //Write-through happened above; now tell the UI the value moved. This has to be explicit:
        //OSC and preset writes leave the mirror and the target agreeing, so no poll - typed or
        //reflection-based - can detect them, and nothing else would refresh the widgets.
        RaiseEventValueChanged(info.Name);

        // Selecting a preset (via the dropdown or over OSC) loads it immediately.
        if (info.Name == "controllableCurrentPreset" && controllableUsePresets
            && !string.IsNullOrEmpty(controllableCurrentPreset))
            ControllableLoad();
    }

    public void SetMethodProp(ClassMethodInfo info, List<object> values)
    {

        object[] parameters = new object[info.methodInfo.GetParameters().Length];

        if(controllableDebug) Debug.Log("Set Method, num expected parameters : " + parameters.Length);

        int valueIndex = 0;
        for(int i=0;i<parameters.Length;i++)
        {
            string typeString = info.methodInfo.GetParameters()[i].ParameterType.ToString();
            //Debug.Log("OSC IN Method, arg "+i+" TYPE : " + typeString + ", num values in OSC Message " + values.Count);

            if (typeString == "System.Single")
            {
                if (values.Count >= valueIndex + 1)
                {
                    parameters[i] = TypeConverter.GetFloat(values[valueIndex]);
                    valueIndex += 1;
                }
            }
            else if (typeString == "System.Boolean")
            {
                if (values.Count >= valueIndex + 1)
                {
                    parameters[i] = TypeConverter.GetBool(values[valueIndex]);
                    valueIndex += 1;
                }
            }
            else if (typeString == "System.Int32")
            {
                if (values.Count >= valueIndex + 1)
                {
                    parameters[i] = TypeConverter.GetInt(values[valueIndex]);
                    valueIndex += 1;
                }
            }
            else if (typeString == "UnityEngine.Vector2")
            {
                if (values.Count >= valueIndex + 2)
                {
                    parameters[i] = new Vector2(TypeConverter.GetFloat(values[valueIndex]), TypeConverter.GetFloat(values[valueIndex + 1]));
                    valueIndex += 2;
                }
            } 
            else if (typeString == "UnityEngine.Vector2Int") 
            {
                if (values.Count >= valueIndex + 2) 
                {
                    parameters[i] = new Vector2Int(TypeConverter.GetInt(values[valueIndex]), TypeConverter.GetInt(values[valueIndex + 1]));
                    valueIndex += 2;
                }
            } 
            else if (typeString == "UnityEngine.Vector3")
            {
                if (values.Count >= valueIndex + 3)
                {
                    parameters[i] = new Vector3(TypeConverter.GetFloat(values[valueIndex]), TypeConverter.GetFloat(values[valueIndex + 1]), TypeConverter.GetFloat(values[valueIndex + 2]));
                    valueIndex += 3;
                }
            } 
            else if (typeString == "UnityEngine.Vector3Int")
            {
                if (values.Count >= valueIndex + 3)
                {
                    parameters[i] = new Vector3Int(TypeConverter.GetInt(values[valueIndex]), TypeConverter.GetInt(values[valueIndex + 1]), TypeConverter.GetInt(values[valueIndex + 2]));
                    valueIndex += 3;
                }
            }
            else if (typeString == "UnityEngine.Vector4")
            {
                if (values.Count >= valueIndex + 4)
                {
                    parameters[i] = new Vector4(TypeConverter.GetFloat(values[valueIndex]), TypeConverter.GetFloat(values[valueIndex + 1]), TypeConverter.GetFloat(values[valueIndex + 2]), TypeConverter.GetFloat(values[valueIndex + 3]));
                    valueIndex += 4;
                }
            }
            else if (typeString == "UnityEngine.Color")
            {
                if (values.Count >= valueIndex + 4)
                {
                    parameters[i] = new Color(TypeConverter.GetFloat(values[valueIndex + 0]), TypeConverter.GetFloat(values[valueIndex + 1]), TypeConverter.GetFloat(values[valueIndex + 2]), TypeConverter.GetFloat(values[valueIndex + 3]));
                    valueIndex += 4;
                }
                else if (values.Count >= valueIndex + 3)
                {
                    parameters[i] = new Color(TypeConverter.GetFloat(values[valueIndex + 0]), TypeConverter.GetFloat(values[valueIndex + 1]), TypeConverter.GetFloat(values[valueIndex + 2]), 1);
                    valueIndex += 3;
                }

            }
            else if (typeString == "System.String")
            {
                if (values.Count >= valueIndex + 1)
                {
                    parameters[i] = values[valueIndex].ToString();
                    valueIndex += 1;
                }
            }

        }
        
        if(!info.fromTargetScript)
            info.methodInfo.Invoke(this, parameters);
        else
            info.methodInfo.Invoke(controllableTargetScript, parameters);
    }

    

    public FieldInfo GetPropInfoForAddress(string address)
    {
        if (controllableFields.ContainsKey(address))
        {
            return controllableFields[address];
        }
        else
        {
            return null;
        }
    }

    public ClassMethodInfo GetMethodInfoForAddress(string address)
    {
        if (controllableMethods.ContainsKey(address))
        {
            return controllableMethods[address];
        }
        else
        {
            return null;
        }
    }

    #endregion

    #region Preset data and files

    public object GetData()
    {
        ControllableData data = new ControllableData();
        data.dataID = controllableId;

        foreach (FieldInfo p in controllableFields.Values)
        {
            OCFProperty attribute = Attribute.GetCustomAttribute(p, typeof(OCFProperty)) as OCFProperty;

            //A read-only member is never restored - SetFieldProp refuses to write one - so recording
            //it would put a value in the file that no load can apply.
            if (attribute.includeInPresets && !attribute.readOnly)
            {
                if(controllableDebug)
                    Debug.Log("Attribute : " + p.Name + " of type " + p.FieldType + " is saved.");

                data.nameList.Add(p.Name);

                //Because a simple "toString" doesn't give the full value
                if (p.FieldType.ToString() == "UnityEngine.Vector3")
                {
                    data.valueList.Add(((Vector3) p.GetValue(this)).ToString("F8"));
                }
                else if (p.FieldType.ToString() == "UnityEngine.Vector4")
                {
                    data.valueList.Add(((Vector4)p.GetValue(this)).ToString("F8"));
                }
                else if (p.FieldType.ToString() == "UnityEngine.Vector3Int")
                {
                    data.valueList.Add(((Vector3Int)p.GetValue(this)).ToString());
                }
                else if (p.FieldType.ToString() == "UnityEngine.Vector2") 
                {
                    data.valueList.Add(((Vector2)p.GetValue(this)).ToString("F8"));
                } 
                else if (p.FieldType.ToString() == "UnityEngine.Vector2Int") 
                {
                    data.valueList.Add(((Vector2Int)p.GetValue(this)).ToString());
                } 
                else if (p.FieldType.ToString() == "System.Single")
                {
                    data.valueList.Add(((float)p.GetValue(this)).ToString("F8"));
                }
                else if (p.FieldType.ToString() == "UnityEngine.Color")
                {
                    data.valueList.Add(((Color)p.GetValue(this)).ToString("F8"));
                }
                else
                    data.valueList.Add(p.GetValue(this).ToString());
            }
        }

        return data;
    }

    public void LoadData(ControllableData data)
    {
        int index = 0;
        foreach (string dn in data.nameList)
        {
            FieldInfo info;
            if (controllableFields.TryGetValue(dn, out info))
            {
                List<object> values = new List<object>();

                //An enum is routed on the field's Type before the string-keyed converter is asked:
                //saveData writes the member name, and SetFieldProp resolves it from the FieldInfo.
                if (info.FieldType.IsEnum)
                {
                    values.Add(data.valueList[index]);
                    SetFieldProp(controllableFields[dn], values);
                }
                else
                {
                    var convertedObject = TypeConverter.GetObjectForValue(info.FieldType.ToString(), data.valueList[index]);

                    if (convertedObject == null)
                        Debug.LogWarning("[OCF] " + GetType().Name + ": cannot restore '" + dn
                            + "' from a preset - " + info.FieldType + " is not a supported type.");
                    else
                    {
                        values.Add(convertedObject);
                        SetFieldProp(controllableFields[dn], values);
                    }
                }
            }

            index++;
        }
        DataLoaded();
    }

    void UpdateTargetDirectory() {

        //Where this controllable's presets sit under the shared root: one folder per scene (or per
        //'controllableFolder' override), one per controllable id.
        string subPath = (controllableFolder.Length > 0 ? controllableFolder : controllableSourceScene)
                         + "/" + controllableId + "/";

#if UNITY_STANDALONE || UNITY_EDITOR
        //The root is resolved once by ControllableMaster, which also owns the command-line and
        //inspector overrides. It always ends in '/'.
        controllableTargetDirectory = ControllableMaster.PresetRootDirectory + subPath;
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
        //persistentDataPath is the only writable location on Android, so the overrides do not apply.
        controllableTargetDirectory = Application.persistentDataPath + "/Presets/" + subPath;
#endif
    }

    void WriteLastUsedPreset()
    {
        if (!string.IsNullOrEmpty(controllableCurrentPreset))
        {
            //write last loaded preset
            File.WriteAllText(controllableTargetDirectory + lastUsedPresetFileName, controllableCurrentPreset);
        }
    }

    // Earlier versions stored the last-used preset name in "_temp.pst" — neither temporary nor a
    // preset. Adopt its content under the new name and remove the legacy file.
    void MigrateLegacyLastUsedPreset()
    {
        var legacyPath = controllableTargetDirectory + "_temp.pst";
        if (!File.Exists(legacyPath)) return;

        var newPath = controllableTargetDirectory + lastUsedPresetFileName;
        if (File.Exists(newPath))
            File.Delete(legacyPath);      // new file wins; drop the stale legacy one
        else
            File.Move(legacyPath, newPath); // preserves the last-used preset name
    }

    #endregion
}
