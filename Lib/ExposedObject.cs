using System.Collections;
using System.Reflection;
using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;

public class ClassAttributInfo
{
    public ExpositionSettings ExposeSettings;
    public FieldInfo Field;
    public PropertyInfo Property;

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
            result = Property.GetValue(obj, null);

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

public class ExposedMethodInfo 
{
    public ExpositionSettings ExposeSettings;
    public MethodInfo Method;
}

//QUERY
[Serializable]
public class ExposedAttribut
{
    public int ACCESS;
    public string[] VALUE;
    public string[] RANGE;
    public string DESCRIPTION;
    public string[] TAGS;
    public string[] EXTENDED_TYPE;
    public string[] UNIT;
}

[Serializable]
public class ExposedComponent
{
    public string FULL_PATH;
    public ExposedAttribut CONTENTS;
    public string TYPE;

}

[Serializable]
public class ExposedObjectData
{
    public List<string> nameList;
    public List<string> valueList;

    public ExposedObjectData()
    {
        nameList = new List<string>();
        valueList = new List<string>();
    }
}


public class ExposedObject : MonoBehaviour
{
    public bool ShowDebug = true;

    public delegate void ScriptValueChangedEvent(string name);
    public event ScriptValueChangedEvent scriptValueChanged;

    public delegate void ComponentDisabledEvent(ExposedObject exposedObject);
    public event ComponentDisabledEvent componentDisabled;

    public delegate void ComponentEnabledEvent(ExposedObject exposedObject);
    public event ComponentEnabledEvent componentEnabled;

    public bool UsePresets;
    public string CurrentPreset;
    public string PresetDirectory;

    public string OSCRootAddress;
    public string Id;
    public object AssociatedComponent;

    public List<string> presetList;

    public Dictionary<string, ClassAttributInfo> Attributs;
    private List<object> _previousFieldsValues;

    public Dictionary<string, ExposedMethodInfo> Methods;

    public void Awake()
    {
        var attributsInfos = Attributs.Values.ToList();
        _previousFieldsValues = new List<object>();

        for (int i = 0; i < attributsInfos.Count; i++)
        {
            _previousFieldsValues.Add(attributsInfos[i].GetValue(AssociatedComponent));
        }
    }

    public void Update() //Warn UI if attribut changes
    {
        var TargetFieldsArray = Attributs.Values.ToArray();

        for (var i = 0; i < TargetFieldsArray.Length; i++)
        {
            var value = TargetFieldsArray[i].GetValue(AssociatedComponent);
            //if (debug)
            //    Debug.Log("Target script value : " + value.ToString() + " previous : " + PreviousFieldsValues[i].ToString());
            if (value.ToString() != _previousFieldsValues[i].ToString())
            {
                if (scriptValueChanged != null)
                    scriptValueChanged(TargetFieldsArray[i].Name);

                _previousFieldsValues[i] = value;
            }
        }
    }

    public void UpdatePresetList()
    {
        presetList.Clear();
        PresetDirectory = Application.dataPath + "/../Presets/" + this.gameObject.scene.name + "/" + Id + "/";
        Directory.CreateDirectory(PresetDirectory);
        foreach (var t in Directory.GetFiles(PresetDirectory))
        {
            var onlyFileName = t.Split('/').Last();
            //Don't put temp file in list
            if (onlyFileName == OSCExposerMaster.Instance.TempFileName) continue;
            presetList.Add(onlyFileName);
        }

        if (scriptValueChanged != null)
            scriptValueChanged("currentPreset");
    }

    public ClassAttributInfo getPropInfoForAddress(string address)
    {
        foreach (var p in Attributs)
        {
            if (p.Key == address)
            {
                return p.Value;
            }
        }

        return null;
    }

    public MethodInfo getMethodInfoForAddress(string address)
    {
        foreach (var p in Methods)
        {
            if (p.Key == address) return p.Value.Method;
        }

        return null;
    }

    public object getData()
    {
        ExposedObjectData data = new ExposedObjectData();

        foreach (ClassAttributInfo p in Attributs.Values)
        {
            if (p.ExposeSettings.IncludeInPresets)
            {
                if (ShowDebug)
                    Debug.Log("Attribute : " + p.Name + " of type " + p.FieldType + " is saved.");

                data.nameList.Add(p.Name);

                //Because a simple "toString" doesn't give the full value
                if (p.FieldType.ToString() == "UnityEngine.Vector3")
                {
                    data.valueList.Add(((Vector3)p.GetValue(this)).ToString("F8"));
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
}
