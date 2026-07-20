using UnityEngine;
using System.Collections;
using System.Reflection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.SceneManagement;

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

    public MonoBehaviour TargetScript; 

    public Color BarColor = Color.white;
    public string id;
    [HideInInspector]
    public string folder = "";
    public bool debug = false;
    [HideInInspector]
    public string targetDirectory;
    [HideInInspector]
    public string sourceScene;
    [HideInInspector]
    public bool usePanel = true, usePresets = true;
    [HideInInspector]
    public bool hasPresets = false;
    public bool closePanelAtStart = true;

    [System.NonSerialized] public Dictionary<string, FieldInfo> Fields;
    [System.NonSerialized] public List<object> PreviousFieldsValues;
    [System.NonSerialized] public Dictionary<string, ClassAttributInfo> TargetFields;

    [System.NonSerialized] public Dictionary<string, ClassMethodInfo> Methods;

    // Snapshot of TargetFields.Values in insertion order, aligned index-for-index with PreviousFieldsValues; iterated in Update to detect target-script changes.
    private ClassAttributInfo[] _targetFieldsArray;

    public delegate void UIValueChangedEvent(string name);

    public event UIValueChangedEvent uiValueChanged;

    public delegate void ControllableValueChangedEvent(string name);

    public event ControllableValueChangedEvent controllableValueChanged;

    public delegate void ScriptValueChangedEvent(string name);

    public event ScriptValueChangedEvent scriptValueChanged;
    [HideInInspector]
    [OSCProperty(targetList = "presetList", includeInPresets = false)] public string currentPreset;
    [HideInInspector]
    public List<string> presetList;

    private string lastUsedPresetFileName = "_lastUsedPreset.txt";

    public virtual void Awake()
    {
        debug = false;

        if (TargetScript == null)
            Debug.LogError("TargetScript of " + this.GetType().ToString() + " is not set ! Aborting initialization.");

        this.scriptValueChanged += OnScriptValueChanged;
        this.uiValueChanged += OnUiValueChanged;

        //FIELDS
        Fields = new Dictionary<string, FieldInfo>();
        TargetFields = new Dictionary<string, ClassAttributInfo>();
        PreviousFieldsValues = new List<object>();

        FieldInfo[] objectFields = this.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);
        FieldInfo[] scriptFields = objectFields;
        PropertyInfo[] scriptProperties = null;
        if (TargetScript != null)
        {
            scriptFields = TargetScript.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);
            scriptProperties = TargetScript.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
        }

        for (int i = 0; i < objectFields.Length; i++)
        {
            FieldInfo info = objectFields[i];
            
            OSCProperty attribute = Attribute.GetCustomAttribute(info, typeof(OSCProperty)) as OSCProperty;
            if (attribute != null)
            {
                if (info.Name == "currentPreset" && !usePresets) continue;

                //Unlike a method, a shadowing field cannot be worked around at runtime: it really
                //exists on the derived class and Unity serializes it, while Controllable's own code
                //keeps reading the base field. All that can be done is report it.
                if (info.DeclaringType != typeof(Controllable) && _reservedMemberNames.Contains(info.Name))
                    Debug.LogWarning("[OCF] " + GetType().Name + ": [OSCProperty] '" + info.Name
                        + "' shadows a member Controllable already declares. The real member is now "
                        + "unreachable and may misbehave - rename it.");

                //A shadowed base field can surface here alongside the one that shadows it; both carry
                //the same name and only one OSC address exists. The warning above already reported it.
                if (Fields.ContainsKey(info.Name))
                    continue;

                Fields.Add(info.Name, info);

                var fieldAdded = false;
                var propertyAdded = false;
                for (int j = 0; j < scriptFields.Length; j++)
                {
                    if (scriptFields[j].Name == info.Name)
                    {
                        var newClassAttributInfo = new ClassAttributInfo();
                        newClassAttributInfo.Field = scriptFields[j];

                        TargetFields.Add(scriptFields[j].Name, newClassAttributInfo);

                        PreviousFieldsValues.Add(newClassAttributInfo.GetValue(TargetScript));
                        info.SetValue(this, newClassAttributInfo.GetValue(TargetScript));
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

                            TargetFields.Add(scriptProperties[j].Name, newClassAttributInfo);
                            PreviousFieldsValues.Add(newClassAttributInfo.GetValue(TargetScript));
                            info.SetValue(this, newClassAttributInfo.GetValue(TargetScript));

                            propertyAdded = true;
                            break;
                        }
                    }
                }
                
                // if(addedFieldName != "")
                //     PreviousFieldsValues.Add(TargetFields[addedFieldName].GetValue(this));

                if (!fieldAdded && !propertyAdded && info.Name != "currentPreset")
                    Debug.LogWarning("[OCF] " + GetType().Name + ": [OSCProperty] '" + info.Name
                        + "' has no matching public field/property on target '"
                        + (TargetScript != null ? TargetScript.GetType().Name : "null")
                        + "'. It will not be controllable.");
            }
        }

        _targetFieldsArray = TargetFields.Values.ToArray();

        //METHODS
        Methods = new Dictionary<string, ClassMethodInfo>();

        //Controllable's own [OSCMethod] members are registered straight from typeof(Controllable) and
        //never consult TargetScript, so a target script's same-named method cannot displace them.
        //They are taken from the base type rather than from this.GetType()'s method list because a
        //derived class declaring the same signature hides the base method from GetMethods entirely.
        foreach (var builtIn in typeof(Controllable).GetMethods(BindingFlags.Instance | BindingFlags.Public))
        {
            if (Attribute.GetCustomAttribute(builtIn, typeof(OSCMethod)) == null) continue;
            if (Array.IndexOf(PresetMethodNames, builtIn.Name) >= 0 && !usePresets) continue;

            var builtInMethodInfo = new ClassMethodInfo();
            builtInMethodInfo.methodInfo = builtIn;
            builtInMethodInfo.fromTargetScript = false;

            Methods.Add(builtIn.Name, builtInMethodInfo);
        }

        MethodInfo[] methodFields = this.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public);

        for (int i = 0; i < methodFields.Length; i++)
        {
            MethodInfo info = methodFields[i];
            OSCMethod attribute = Attribute.GetCustomAttribute(info, typeof(OSCMethod)) as OSCMethod;
            if (attribute != null)
            {
                //Already registered above, from the base type.
                if (_builtInOSCMethodNames.Contains(info.Name))
                {
                    if (info.DeclaringType != typeof(Controllable))
                        Debug.LogWarning("[OCF] " + GetType().Name + ": [OSCMethod] '" + info.Name
                            + "' reuses the name of a built-in Controllable method. It is ignored and "
                            + "the built-in is used instead - rename it if you want it exposed.");
                    continue;
                }

                if (Methods.ContainsKey(info.Name))
                {
                    Debug.LogWarning("[OCF] " + GetType().Name + ": [OSCMethod] '" + info.Name
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
                var targetScriptMethod = TargetScript != null
                    ? TargetScript.GetType().GetMethod(info.Name,
                        BindingFlags.Instance | BindingFlags.Public, null, parameterTypes, null)
                    : null;

                if (targetScriptMethod != null)
                {
                    classMethodInfo.methodInfo = targetScriptMethod;
                    classMethodInfo.fromTargetScript = true;
                }

                Methods.Add(info.Name, classMethodInfo);
            }
        }

        if (string.IsNullOrEmpty(id))
            id = TargetScript != null ? TargetScript.GetType().Name : GetType().Name;

        id = id.Replace(' ', '_');
        sourceScene = SceneManager.GetActiveScene().name;
    }

    public virtual void OnScriptValueChanged(string name)
    {
        if (String.IsNullOrEmpty(name) || !TargetFields.ContainsKey(name))
        {
            if (debug)
                Debug.Log("Name : " + name + " is null in target");
            return;
        }
        Fields[name].SetValue(this, TargetFields[name].GetValue(TargetScript));
        RaiseEventValueChanged(name);
    }

    public virtual void OnUiValueChanged(string name)
    {
        if (String.IsNullOrEmpty(name) || !TargetFields.ContainsKey(name)) {
            if (debug)
            {
                Debug.Log("Name : " + name + " doesn't exist in target");
            }
            return;
        }
        TargetFields[name].SetValue(TargetScript, Fields[name].GetValue(this));
    }

    public virtual void OnEnable()
    {
		if (debug)
			Debug.Log("Registering " + this.GetType().Name + " script as " + id);
        ControllableMaster.Register(this);

        if (usePresets)
        {
            presetList = new List<string>();
            ReadFileList();

            if (presetList.Count >= 1)
            {
                currentPreset = presetList[0];
                LoadLatestUsedPreset();
            }
        }
    }

	public virtual void Update() //Warn UI if attribut changes
    {
        if (_targetFieldsArray == null)
            return;

        for (var i = 0; i < _targetFieldsArray.Length; i++)
        {
            var value = _targetFieldsArray[i].GetValue(TargetScript);

            if (!object.Equals(value, PreviousFieldsValues[i]))
            {
                //Debug.Log("Target script value : " + value.ToString() + " previous : " + PreviousFieldsValues[i].ToString());
                if (scriptValueChanged != null)
                    scriptValueChanged(_targetFieldsArray[i].Name);

                PreviousFieldsValues[i] = value;
            }
        }
    }

    public void LoadLatestUsedPreset()
    {
        //Check if the file recording the last used preset exists
        if (!File.Exists(targetDirectory + lastUsedPresetFileName)) return;

        var file = new StreamReader(targetDirectory + lastUsedPresetFileName);

        var lastPresetRead =  file.ReadLine();
        file.Close();

        if (debug)
            Debug.Log("LastUsedPreset for "+id+" : " + lastPresetRead);

        if (string.IsNullOrEmpty(lastPresetRead)) return;

        currentPreset = lastPresetRead;
        Load();

        if (scriptValueChanged != null) scriptValueChanged("currentPreset");
        //if (uiValueChanged != null) uiValueChanged("currentPreset");
        RaiseEventValueChanged("currentPreset");
    }

    public void ReadFileList()
    {
        presetList.Clear();

        UpdateTargetDirectory();

        Directory.CreateDirectory(targetDirectory);

        MigrateLegacyLastUsedPreset();

        foreach (var t in Directory.GetFiles(targetDirectory))
        {
            var onlyFileName = Path.GetFileName(t);
            //Only .pst files are presets; the last-used-preset marker (.txt) is excluded by extension
            if (onlyFileName.Split('.').Last() != "pst") continue;
            presetList.Add(onlyFileName);
        }

        if(presetList.Count != 0)
        {
            hasPresets = true;
        }
        if (scriptValueChanged != null) scriptValueChanged("currentPreset");
        RaiseEventValueChanged("currentPreset");
    }

    //The preset methods below, by name. Callers identify preset buttons/methods by matching against
    //this rather than their displayed label, which is a derived string (see GenUI's ParseNameString).
    public static readonly string[] PresetMethodNames = { "Save", "SaveAs", "Load", "Show" };

    //Controllable's own [OSCMethod] members: the four preset methods plus LoadWithName. These are
    //always bound to Controllable's implementation, never to a target script's same-named method.
    static readonly HashSet<string> _builtInOSCMethodNames = new HashSet<string>(
        typeof(Controllable).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(m => Attribute.GetCustomAttribute(m, typeof(OSCMethod)) != null)
            .Select(m => m.Name));

    //Every public member Controllable exposes, including those inherited from MonoBehaviour (name,
    //tag, transform, enabled...). A mirror declaring any of these names shadows the real member.
    static readonly HashSet<string> _reservedMemberNames = new HashSet<string>(
        typeof(Controllable).GetMembers(BindingFlags.Instance | BindingFlags.Public)
            .Select(m => m.Name));

    /// <summary>
    /// True if <paramref name="name"/> is the name of a member Controllable already exposes.
    /// An [OSCExposed] member must not reuse one: the generated mirror would declare a member of the
    /// same name, shadowing the real one and breaking it silently.
    /// </summary>
    public static bool IsReservedMemberName(string name)
    {
        return _reservedMemberNames.Contains(name);
    }

    [OSCMethod]
    public void Save()
    {
        if (string.IsNullOrEmpty(currentPreset))
        {
            SaveAs();
            return;
        }

        Save(currentPreset);
    }

    [OSCMethod]
    public void SaveAs()
    {

        var date = DateTime.Today.Day + "-" + DateTime.Today.Month + "-" + DateTime.Today.Year + "_" +
                   DateTime.Now.Hour + "-" + DateTime.Now.Minute + "-" + DateTime.Now.Second;
        var fileName = date + ".pst";

        Save(fileName);
    }

    private void Save(string fileName)
    {

        UpdateTargetDirectory();

        if (debug)
            Debug.Log("Saving in " + targetDirectory + fileName + "...");
        //create file
        if (!Directory.Exists(targetDirectory)) Directory.CreateDirectory(targetDirectory);

        CallMeBeforeSave();
        File.WriteAllText(targetDirectory + fileName, JsonUtility.ToJson(this.getData()));

        if (debug)
            Debug.Log("Saved in " + targetDirectory + fileName);

        currentPreset = fileName;
        WriteLastUsedPreset();

        ReadFileList();
    }

    [OSCMethod]
    public void Load()
    {
        LoadWithName(currentPreset);
    }

    [OSCMethod]
    public void Show() //Show preset file in explorer
    {
        if (string.IsNullOrEmpty(currentPreset)) return;

        var itemPath = targetDirectory + currentPreset;
        itemPath = itemPath.Replace(@"/", @"\");   // explorer doesn't like front slashes
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        System.Diagnostics.Process.Start("explorer.exe", "/select," + itemPath);
#else
        Debug.Log("[OCF] Showing the preset folder is only supported on Windows. Path: " + itemPath);
#endif
    }

    [OSCMethod]
    public void LoadWithName(string fileName)
    {
        if (!fileName.EndsWith(".pst"))
            fileName += ".pst";

        if (debug)
            Debug.Log("Loading " + fileName + " preset for " + id);

        StreamReader file;
        try
        {
            file = new StreamReader(targetDirectory + fileName);
            ControllableData cData = JsonUtility.FromJson<ControllableData>(file.ReadLine());
            loadData(cData);
            file.Close();
        }
        catch (Exception e)
        {
            Debug.LogError("Error while loading preset : " + e.Message + e.StackTrace);
            return;
        }
        currentPreset = fileName;
        WriteLastUsedPreset();
    }

    //Override it if you want to do things after a load
    public virtual void DataLoaded() { }
    //Override it if you want to do things before a preset save
    public virtual void CallMeBeforeSave() { }

    public virtual void OnDisable()
    {
        if (debug)
            Debug.Log("Saving temp file with : " + currentPreset);

        if (usePresets)
        {
            WriteLastUsedPreset();
        }

        if (debug)
            Debug.Log("Done saving");

		if (debug)
			Debug.Log("Unregistering " + this.GetType().Name + " script on " + this.gameObject.name);

		ControllableMaster.UnRegister(this);
    }

    public FieldInfo getFieldInfoByName(string requestedName)
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

    protected void RaiseEventValueChanged(string property)
    {
        if (!this.enabled)
            return;

        if (controllableValueChanged != null)
            controllableValueChanged(property);
    }

    public void setProp(string property, List<object> values)
    {
        FieldInfo info = getPropInfoForAddress(property);
        if (info != null)
        {
            setFieldProp(info, values);
            return;
        }

        ClassMethodInfo mInfo = getMethodInfoForAddress(property);
        if (mInfo != null)
        {
            setMethodProp(mInfo, values);
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

    public void setFieldProp(FieldInfo info, List<object> values, bool isEnum = false)
    {
        OSCProperty attribute = Attribute.GetCustomAttribute(info, typeof(OSCProperty)) as OSCProperty;

        if (attribute.readOnly == true)
            return;

        string typeString = info.FieldType.ToString();

        if(debug)
            Debug.Log("Setting attribut " + info.Name + " of type " + typeString + " (enum?"+ isEnum + ") with " + values.Count + " value(s)");

        // if we detect any attribute print out the data.

        if (isEnum)
        {
            if (values.Count >= 1)
            {
                var enumType = Type.GetType(typeString);
                if (enumType == null)
                {
                    Debug.LogWarning("[OCF] Could not resolve enum type '" + typeString + "' for " + info.Name + " (move the enum out of a Plugins folder or qualify its assembly).");
                }
                else
                {
                    var enumIndex = TypeConverter.getIndexInEnum(Enum.GetNames(enumType).ToList(), (string)values[0]);
                    if (enumIndex >= 0)
                        info.SetValue(this, enumIndex);
                }
            }
        }
        else
        {
            if (typeString == "System.Single")
            {
                if (values.Count >= 1) info.SetValue(this, ClampToRange(info, TypeConverter.getFloat(values[0])));
            }
            else if (typeString == "System.Boolean")
            {
                if (values.Count >= 1) info.SetValue(this, TypeConverter.getBool(values[0]));
            }
            else if (typeString == "System.Int32")
            {
                if (values.Count >= 1) info.SetValue(this, (int)ClampToRange(info, TypeConverter.getInt(values[0])));
            }
            else if (typeString == "UnityEngine.Vector2")
            {
                if (values.Count == 1) info.SetValue(this, (Vector2)values[0]);
                if (values.Count >= 2) info.SetValue(this, new Vector2(TypeConverter.getFloat(values[0]), TypeConverter.getFloat(values[1])));
            } 
            else if (typeString == "UnityEngine.Vector2Int") 
            {
                if (values.Count == 1) info.SetValue(this, (Vector2Int)values[0]);
                if (values.Count >= 2) info.SetValue(this, new Vector2Int(TypeConverter.getInt(values[0]), TypeConverter.getInt(values[1])));
            } 
            else if (typeString == "UnityEngine.Vector3")
            {
                if (values.Count == 1) info.SetValue(this, (Vector3)values[0]);
                if (values.Count >= 3) info.SetValue(this, new Vector3(TypeConverter.getFloat(values[0]), TypeConverter.getFloat(values[1]), TypeConverter.getFloat(values[2])));
            } 
            else if (typeString == "UnityEngine.Vector3Int")
            {
                if (values.Count == 1) info.SetValue(this, (Vector3Int)values[0]);
                if (values.Count >= 3) info.SetValue(this, new Vector3Int(TypeConverter.getInt(values[0]), TypeConverter.getInt(values[1]), TypeConverter.getInt(values[2])));
            }
            else if (typeString == "UnityEngine.Vector4")
            {
                if (values.Count == 1) info.SetValue(this, (Vector4)values[0]);
                if (values.Count >= 4) info.SetValue(this, new Vector4(TypeConverter.getFloat(values[0]), TypeConverter.getFloat(values[1]), TypeConverter.getFloat(values[2]), TypeConverter.getFloat(values[3])));
            }
            else if (typeString == "UnityEngine.Color")
            {
                if (values.Count == 1) info.SetValue(this, (Color)values[0]);
                else if (values.Count >= 4) info.SetValue(this, new Color(TypeConverter.getFloat(values[0]), TypeConverter.getFloat(values[1]), TypeConverter.getFloat(values[2]), TypeConverter.getFloat(values[3])));
                else if (values.Count >= 3) info.SetValue(this, new Color(TypeConverter.getFloat(values[0]), TypeConverter.getFloat(values[1]), TypeConverter.getFloat(values[2]), 1));
            }
            else if (typeString == "System.String")
            {
                // Debug.Log("String received : " + values.ToString());
                info.SetValue(this, values[0].ToString());
            }
        }
        if (uiValueChanged != null) uiValueChanged(info.Name);
    }

    public void setMethodProp(ClassMethodInfo info, List<object> values)
    {

        object[] parameters = new object[info.methodInfo.GetParameters().Length];

        if(debug) Debug.Log("Set Method, num expected parameters : " + parameters.Length);

        int valueIndex = 0;
        for(int i=0;i<parameters.Length;i++)
        {
            string typeString = info.methodInfo.GetParameters()[i].ParameterType.ToString();
            //Debug.Log("OSC IN Method, arg "+i+" TYPE : " + typeString + ", num values in OSC Message " + values.Count);

            if (typeString == "System.Single")
            {
                if (values.Count >= valueIndex + 1)
                {
                    parameters[i] = TypeConverter.getFloat(values[valueIndex]);
                    valueIndex += 1;
                }
            }
            else if (typeString == "System.Boolean")
            {
                if (values.Count >= valueIndex + 1)
                {
                    parameters[i] = TypeConverter.getBool(values[valueIndex]);
                    valueIndex += 1;
                }
            }
            else if (typeString == "System.Int32")
            {
                if (values.Count >= valueIndex + 1)
                {
                    parameters[i] = TypeConverter.getInt(values[valueIndex]);
                    valueIndex += 1;
                }
            }
            else if (typeString == "UnityEngine.Vector2")
            {
                if (values.Count >= valueIndex + 2)
                {
                    parameters[i] = new Vector2(TypeConverter.getFloat(values[valueIndex]), TypeConverter.getFloat(values[valueIndex + 1]));
                    valueIndex += 2;
                }
            } 
            else if (typeString == "UnityEngine.Vector2Int") 
            {
                if (values.Count >= valueIndex + 2) 
                {
                    parameters[i] = new Vector2Int(TypeConverter.getInt(values[valueIndex]), TypeConverter.getInt(values[valueIndex + 1]));
                    valueIndex += 2;
                }
            } 
            else if (typeString == "UnityEngine.Vector3")
            {
                if (values.Count >= valueIndex + 3)
                {
                    parameters[i] = new Vector3(TypeConverter.getFloat(values[valueIndex]), TypeConverter.getFloat(values[valueIndex + 1]), TypeConverter.getFloat(values[valueIndex + 2]));
                    valueIndex += 3;
                }
            } 
            else if (typeString == "UnityEngine.Vector3Int")
            {
                if (values.Count >= valueIndex + 3)
                {
                    parameters[i] = new Vector3Int(TypeConverter.getInt(values[valueIndex]), TypeConverter.getInt(values[valueIndex + 1]), TypeConverter.getInt(values[valueIndex + 2]));
                    valueIndex += 3;
                }
            }
            else if (typeString == "UnityEngine.Vector4")
            {
                if (values.Count >= valueIndex + 4)
                {
                    parameters[i] = new Vector4(TypeConverter.getFloat(values[valueIndex]), TypeConverter.getFloat(values[valueIndex + 1]), TypeConverter.getFloat(values[valueIndex + 2]), TypeConverter.getFloat(values[valueIndex + 3]));
                    valueIndex += 4;
                }
            }
            else if (typeString == "UnityEngine.Color")
            {
                if (values.Count >= valueIndex + 4)
                {
                    parameters[i] = new Color(TypeConverter.getFloat(values[valueIndex + 0]), TypeConverter.getFloat(values[valueIndex + 1]), TypeConverter.getFloat(values[valueIndex + 2]), TypeConverter.getFloat(values[valueIndex + 3]));
                    valueIndex += 4;
                }
                else if (values.Count >= valueIndex + 3)
                {
                    parameters[i] = new Color(TypeConverter.getFloat(values[valueIndex + 0]), TypeConverter.getFloat(values[valueIndex + 1]), TypeConverter.getFloat(values[valueIndex + 2]), 1);
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
            info.methodInfo.Invoke(TargetScript, parameters);
    }

    

    public FieldInfo getPropInfoForAddress(string address)
    {
        if (Fields.ContainsKey(address))
        {
            return Fields[address];
        }
        else
        {
            return null;
        }
    }

    public ClassMethodInfo getMethodInfoForAddress(string address)
    {
        if (Methods.ContainsKey(address))
        {
            return Methods[address];
        }
        else
        {
            return null;
        }
    }

    public object getData()
    {
        ControllableData data = new ControllableData();
        data.dataID = id;

        foreach (FieldInfo p in Fields.Values)
        {
            OSCProperty attribute = Attribute.GetCustomAttribute(p, typeof(OSCProperty)) as OSCProperty;
            if (attribute.includeInPresets)
            {
                if(debug)
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

    public void loadData(ControllableData data)
    {
        int index = 0;
        foreach (string dn in data.nameList)
        {
            FieldInfo info;
            if (Fields.TryGetValue(dn, out info))
            {
                List<object> values = new List<object>();
                var convertedObject = TypeConverter.getObjectForValue(Fields[dn].FieldType.ToString(), data.valueList[index]);

                if (convertedObject == null) //Might be an enum
                {
                    values.Add(data.valueList[index]);
                    setFieldProp(Fields[dn], values, true);
                }
                else
                {
                    values.Add(convertedObject);
                    setFieldProp(Fields[dn], values);
                }
            }

            index++;
        }
        DataLoaded();
    }

    void UpdateTargetDirectory() {

#if UNITY_STANDALONE || UNITY_EDITOR
        //TODO: Should be cleaned up to find the correct ControllableMaster instance instead of using FindObjectOfType
        ControllableMaster controllableMaster = FindAnyObjectByType<ControllableMaster>();

        if (controllableMaster && controllableMaster.useDocumentsDirectory)
        {
            targetDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "/" + Application.productName + "/Presets/" + (folder.Length > 0 ? folder : sourceScene) + "/" + id + "/";
        }
        else
        {
            targetDirectory = Application.dataPath + "/../Presets/" + (folder.Length > 0 ? folder : sourceScene) + "/" + id + "/";
        }
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
        targetDirectory = Application.persistentDataPath + "/Presets/" + (folder.Length > 0 ? folder : sourceScene) + "/" + id + "/";
#endif
    }

    void WriteLastUsedPreset()
    {
        if (!string.IsNullOrEmpty(currentPreset))
        {
            //write last loaded preset
            File.WriteAllText(targetDirectory + lastUsedPresetFileName, currentPreset);
        }
    }

    // Earlier versions stored the last-used preset name in "_temp.pst" — neither temporary nor a
    // preset. Adopt its content under the new name and remove the legacy file.
    void MigrateLegacyLastUsedPreset()
    {
        var legacyPath = targetDirectory + "_temp.pst";
        if (!File.Exists(legacyPath)) return;

        var newPath = targetDirectory + lastUsedPresetFileName;
        if (File.Exists(newPath))
            File.Delete(legacyPath);      // new file wins; drop the stale legacy one
        else
            File.Move(legacyPath, newPath); // preserves the last-used preset name
    }
}
