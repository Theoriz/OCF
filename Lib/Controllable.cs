using UnityEngine;
using System.Collections;
using System.Reflection;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Threading;
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
        set
        {
            //this._name = value;
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

    public Dictionary<string, FieldInfo> Fields;
    public List<object> PreviousFieldsValues;
    public Dictionary<string, ClassAttributInfo> TargetFields;

    public Dictionary<string, ClassMethodInfo> Methods;

    public delegate void UIValueChangedEvent(string name);

    public event UIValueChangedEvent uiValueChanged;

    public delegate void ControllableValueChangedEvent(string name);

    public event ControllableValueChangedEvent controllableValueChanged;

    public delegate void ScriptValueChangedEvent(string name);

    public event ScriptValueChangedEvent scriptValueChanged;
    [HideInInspector]
    [OSCProperty(TargetList = "presetList", IncludeInPresets = false)] public string currentPreset;
    [HideInInspector]
    public List<string> presetList;

    private string tempFileName = "_temp.pst";

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

                Fields.Add(info.Name, info);

                var fieldAdded = false;
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

                            break;
                        }
                    }
                }
                
                // if(addedFieldName != "")
                //     PreviousFieldsValues.Add(TargetFields[addedFieldName].GetValue(this));
            }
        }

        //METHODS
        Methods = new Dictionary<string, ClassMethodInfo>();

        MethodInfo[] methodFields = this.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public);

        for (int i = 0; i < methodFields.Length; i++)
        {
            MethodInfo info = methodFields[i];
            OSCMethod attribute = Attribute.GetCustomAttribute(info, typeof(OSCMethod)) as OSCMethod;
            if (attribute != null)
            {
                if((info.Name == "Save" || info.Name == "SaveAs" || info.Name == "Load" || info.Name == "Show") && !usePresets) continue;
                //Debug.Log("Testing : " + info.Name);

                var classMethodInfo = new ClassMethodInfo();
                classMethodInfo.methodInfo = info;
                classMethodInfo.fromTargetScript = false;

                var targetScriptMethod = TargetScript.GetType().GetMethod(info.Name);
                if (targetScriptMethod != null)
                {
                    //Debug.Log("Adding : " + targetScriptMethod.Name);
                    classMethodInfo.methodInfo = targetScriptMethod;
                    classMethodInfo.fromTargetScript = true;

                    Methods.Add(targetScriptMethod.Name, classMethodInfo);
                }
                else
                {
                    Methods.Add(info.Name, classMethodInfo);
                }
            }
        }

        if (string.IsNullOrEmpty(id))
            id = TargetScript.GetType().Name;

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
        var TargetFieldsArray = TargetFields.Values.ToArray();
        
        for (var i=0 ; i< TargetFieldsArray.Length ; i++)
        {
            var value = TargetFieldsArray[i].GetValue(TargetScript);

            if (value.ToString() != PreviousFieldsValues[i].ToString())
            {
                //Debug.Log("Target script value : " + value.ToString() + " previous : " + PreviousFieldsValues[i].ToString());
                if (scriptValueChanged != null) scriptValueChanged(TargetFieldsArray[i].Name);
                    RaiseEventValueChanged(TargetFieldsArray[i].Name);
                    PreviousFieldsValues[i] = value;
            }
        }
    }

    public void LoadLatestUsedPreset()
    {
        //Check if the temp preset containing the last used preset exists
        if (!File.Exists(targetDirectory + tempFileName)) return;

        var file = new StreamReader(targetDirectory + tempFileName);

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
        foreach (var t in Directory.GetFiles(targetDirectory))
        {
            var onlyFileName = t.Split('/').Last();
            //Don't put temp file in list
            if (onlyFileName == tempFileName || onlyFileName.Split('.').Last() != "pst") continue;
            presetList.Add(onlyFileName);
        }

        if(presetList.Count != 0)
        {
            hasPresets = true;
        }
        if (scriptValueChanged != null) scriptValueChanged("currentPreset");
        RaiseEventValueChanged("currentPreset");
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
        var file = File.OpenWrite(targetDirectory + fileName);
        file.Close();

        CallMeBeforeSave();
        File.WriteAllText(targetDirectory + fileName, JsonUtility.ToJson(this.getData()));

        if (debug)
            Debug.Log("Saved in " + targetDirectory + fileName);

        currentPreset = fileName;
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
        if (currentPreset == "") return;

        var itemPath = targetDirectory + currentPreset;
        itemPath = itemPath.Replace(@"/", @"\");   // explorer doesn't like front slashes
        System.Diagnostics.Process.Start("explorer.exe", "/select," + itemPath);
    }

    [OSCMethod]
    public void LoadWithName(string fileName, float duration = 0, string tweenStyle = null)
    {
        if (!fileName.EndsWith(".pst"))
            fileName += ".pst";

        if (debug)
            Debug.Log("Loading " + fileName + " preset for " + id + " with " + (tweenStyle == null ? " no tween " : tweenStyle));

        StreamReader file;
        try
        {
            file = new StreamReader(targetDirectory + fileName);
            ControllableData cData = JsonUtility.FromJson<ControllableData>(file.ReadLine());
            loadData(cData, duration, tweenStyle);
            file.Close();
        }
        catch (Exception e)
        {
            Debug.LogError("Error while loading preset : " + e.Message + e.StackTrace);
            return;
        }
        currentPreset = fileName;
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
            if (!string.IsNullOrEmpty(currentPreset))
            {
                //Create temp file
                var tempFile = File.OpenWrite(targetDirectory + tempFileName);
                tempFile.Close();
                //write last loaded preset
                File.WriteAllText(targetDirectory + tempFileName, currentPreset);
            }
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

    public void setFieldProp(FieldInfo info, List<object> values, bool isEnum = false)
    {
        OSCProperty attribute = Attribute.GetCustomAttribute(info, typeof(OSCProperty)) as OSCProperty;

        if (attribute.isInteractible == false)
            return;

        string typeString = info.FieldType.ToString();

        if(debug)
            Debug.Log("Setting attribut " + info.Name + " of type " + typeString + " (enum?"+ isEnum + ") with " + values.Count + " value(s)");

        // if we detect any attribute print out the data.

        if (isEnum)
        {
            if (values.Count >= 1) info.SetValue(this, TypeConverter.getIndexInEnum(Enum.GetNames(Type.GetType(typeString)).ToList(), (string)values[0]));
        }
        else
        {
            if (typeString == "System.Single")
            {
                if (values.Count >= 1) info.SetValue(this, TypeConverter.getFloat(values[0]));
            }
            else if (typeString == "System.Boolean")
            {
                if (values.Count >= 1) info.SetValue(this, TypeConverter.getBool(values[0]));
            }
            else if (typeString == "System.Int32")
            {
                if (values.Count >= 1) info.SetValue(this, TypeConverter.getInt(values[0]));
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
            else if (typeString == "UnityEngine.Color")
            {
                if (values.Count >= valueIndex + 4)
                {
                    parameters[i] = new Color(TypeConverter.getFloat(values[valueIndex + 0]), TypeConverter.getFloat(values[valueIndex + 1]), TypeConverter.getFloat(values[valueIndex + 2]), TypeConverter.getFloat(values[valueIndex + 3]));
                    valueIndex += 4;
                }
                else if (values.Count >= i + 3)
                {
                    parameters[i] = new Color(TypeConverter.getFloat(values[valueIndex + 0]), TypeConverter.getFloat(values[valueIndex + 1]), TypeConverter.getFloat(values[valueIndex + 2]), 1);
                    valueIndex += 3;
                }

            }
            else if (typeString == "System.String")
            {
                if (values.Count >= valueIndex + 1)
                {
                    parameters[i] = values[i].ToString();
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
            if (attribute.IncludeInPresets)
            {
                if(debug)
                    Debug.Log("Attribute : " + p.Name + " of type " + p.FieldType + " is saved.");

                data.nameList.Add(p.Name);

                //Because a simple "toString" doesn't give the full value
                if (p.FieldType.ToString() == "UnityEngine.Vector3")
                {
                    data.valueList.Add(((Vector3) p.GetValue(this)).ToString("F8"));
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
                else
                    data.valueList.Add(p.GetValue(this).ToString());
            }
        }

        return data;
    }

    public void loadData(ControllableData data, float duration = 0, string tweenStyle = null)
    {
        
        if (tweenStyle != null)
        {
            tweenStyle = tweenStyle.ToLower();
            if (tweenStyle != "easeout" && tweenStyle != "easein" && tweenStyle != "easeinout" && tweenStyle != "linear")
            {
                Debug.LogWarning("Unknow tween style !");
                tweenStyle = null;
            }
        }

        int index = 0;
        foreach (string dn in data.nameList)
        {
            FieldInfo info;
            if (Fields.TryGetValue(dn, out info))
            {
                if (tweenStyle != null)
                {
                    var curve = new AnimationCurve();

                    if (tweenStyle == "easeinout")
                        curve = TweenCurves.Instance.EaseInOutCurve;

                    else if (tweenStyle == "easein")
                        curve = TweenCurves.Instance.EaseInCurve;

                    else if (tweenStyle == "easeout")
                        curve = TweenCurves.Instance.EaseOutCurve;

                    else if (tweenStyle == "linear")
                        curve = TweenCurves.Instance.LinearCurve;

                    StartCoroutine(
                            TweenValue(Fields[dn],
                                TypeConverter.getObjectForValue(Fields[dn].FieldType.ToString(), data.valueList[index]),
                                duration,
                                curve)
                            );
                }
                else
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
            }

            index++;
        }
        StartCoroutine(CallAfterDuration(DataLoaded, duration));
        
    }

    IEnumerator CallAfterDuration(Action callback, float duration)
    {
        var currentTime = 0.0f;
        while (currentTime < duration)
        {
            currentTime += Time.deltaTime;
            yield return new WaitForFixedUpdate();
        }

        callback();
    }

    IEnumerator TweenValue(FieldInfo fieldInfo, object end, float duration, AnimationCurve curve)
    {
        var currentTime = 0f;
        var startValue = fieldInfo.GetValue(this);
        while (currentTime < duration)
        {
            List<object> values = new List<object>();
            //            Debug.Log(fieldInfo.FieldType.ToString() );
            if (fieldInfo.FieldType.ToString() == "System.Single")
            {
                values.Add(Mathf.Lerp((float)startValue, (float)end, curve.Evaluate(currentTime / duration)));
            }

            else if (fieldInfo.FieldType.ToString() == "System.Int32")
            {
                values.Add((int)Mathf.Lerp((int)startValue, (int)end, curve.Evaluate(currentTime / duration)));
            }

            else if (fieldInfo.FieldType.ToString() == "UnityEngine.Vector2")
            {
                values.Add(Vector2.Lerp((Vector2)startValue, (Vector2)end, curve.Evaluate(currentTime / duration)));
            } 
            
            else if (fieldInfo.FieldType.ToString() == "UnityEngine.Vector2Int") 
            {
                Vector2Int startValueVector = (Vector2Int)startValue;
                Vector2Int endVector = (Vector2Int)end;

                values.Add(new Vector2Int(
                    (int)Mathf.Lerp(startValueVector.x, endVector.x, curve.Evaluate(currentTime / duration)), 
                    (int)Mathf.Lerp(startValueVector.y, endVector.y, curve.Evaluate(currentTime / duration))));
            } 
            
            else if (fieldInfo.FieldType.ToString() == "UnityEngine.Vector3")
            {
                values.Add(Vector3.Lerp((Vector3)startValue, (Vector3)end, curve.Evaluate(currentTime / duration)));
            } 
            
            else if (fieldInfo.FieldType.ToString() == "UnityEngine.Vector3Int") 
            {
                Vector3Int startValueVector = (Vector3Int)startValue;
                Vector3Int endVector = (Vector3Int)end;

                values.Add(new Vector3Int(
                    (int)Mathf.Lerp(startValueVector.x, endVector.x, curve.Evaluate(currentTime / duration)),
                    (int)Mathf.Lerp(startValueVector.y, endVector.y, curve.Evaluate(currentTime / duration)),
                    (int)Mathf.Lerp(startValueVector.z, endVector.z, curve.Evaluate(currentTime / duration))));
            } 
            
            else if (fieldInfo.FieldType.ToString() == "UnityEngine.Color")
            {
                values.Add(Color.Lerp((Color)startValue, (Color)end, curve.Evaluate(currentTime / duration)));
            }
            else
            {
                break;
            }
            setFieldProp(fieldInfo, values);
            currentTime += Time.deltaTime;

            yield return new WaitForFixedUpdate();
        }

        List<object> finalValue = new List<object>();
        finalValue.Add(end);
        setFieldProp(fieldInfo, finalValue);
    }

    void UpdateTargetDirectory() {

#if UNITY_STANDALONE || UNITY_EDITOR
        //Should be cleaned up to find the correct ControllableMaster instance instead of using FindObjectOfType
        if (FindObjectOfType<ControllableMaster>().useDocumentsDirectory) {
            targetDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)+"/" + Application.productName + "/Presets/" + (folder.Length > 0 ? folder : sourceScene) + "/" + id + "/";
        } else {
            targetDirectory = Application.dataPath + "/../Presets/" + (folder.Length > 0 ? folder : sourceScene) + "/" + id + "/";
        }
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
        targetDirectory = Application.persistentDataPath + "/Presets/" + (folder.Length > 0 ? folder : sourceScene) + "/" + id + "/";
#endif
    }
}
