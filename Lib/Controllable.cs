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

    public Dictionary<string, FieldInfo> Fields;
    public List<object> PreviousFieldsValues;
    public Dictionary<string, ClassAttributInfo> TargetFields;

    public Dictionary<string, MethodInfo> Methods;

    public delegate void UIValueChangedEvent(string name);

    //public event UIValueChangedEvent uiValueChanged;

    //public delegate void ControllableValueChangedEvent(string name);

    //public event ControllableValueChangedEvent controllableValueChanged;


    //[HideInInspector]
    //[OSCProperty(TargetList = "presetList", IncludeInPresets = false)] public string currentPreset;
    //[HideInInspector]
    //public List<string> presetList;

    // private string tempFileName = "_temp.pst";

    public virtual void Awake()
    {
    }
}
//        debug = false;

//        if (TargetScript == null)
//            Debug.LogError("TargetScript of " + this.GetType().ToString() + " is not set ! Aborting initialization.");

////        this.scriptValueChanged += OnScriptValueChanged;
//        this.uiValueChanged += OnUiValueChanged;

//        //FIELDS
//        Fields = new Dictionary<string, FieldInfo>();
//        TargetFields = new Dictionary<string, ClassAttributInfo>();
//        PreviousFieldsValues = new List<object>();

//        Type t = GetType();
//        FieldInfo[] objectFields = t.GetFields(BindingFlags.Instance | BindingFlags.Public);
//        FieldInfo[] scriptFields = objectFields;
//        PropertyInfo[] scriptProperties = null;
//        if (TargetScript != null)
//        {
//            scriptFields = TargetScript.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);
//            scriptProperties = TargetScript.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
//        }

//        for (int i = 0; i < objectFields.Length; i++)
//        {
//            FieldInfo info = objectFields[i];
            
//            OSCProperty attribute = Attribute.GetCustomAttribute(info, typeof(OSCProperty)) as OSCProperty;
//            if (attribute != null)
//            {
//                if (info.Name == "currentPreset" && !usePresets) continue;

//                Fields.Add(info.Name, info);

//                var fieldAdded = false;
//                for (int j = 0; j < scriptFields.Length; j++)
//                {
//                    if (scriptFields[j].Name == info.Name)
//                    {
//                        var newClassAttributInfo = new ClassAttributInfo();
//                        newClassAttributInfo.Field = scriptFields[j];

//                        TargetFields.Add(scriptFields[j].Name, newClassAttributInfo);
//                        fieldAdded = true;
//                        break;
//                    }
//                }

//                if (!fieldAdded)
//                {
//                    for (int j = 0; j < scriptProperties.Length; j++)
//                    {
//                        if (scriptProperties[j].Name == info.Name)
//                        {
//                            var newClassAttributInfo = new ClassAttributInfo();
//                            newClassAttributInfo.Property = scriptProperties[j];

//                            TargetFields.Add(scriptProperties[j].Name, newClassAttributInfo);
//                            break;
//                        }
//                    }
//                }
                
//                PreviousFieldsValues.Add(info.GetValue(this));
//            }
//        }


//        //METHODS
//        Methods = new Dictionary<string, MethodInfo>();

//        MethodInfo[] methodFields = t.GetMethods(BindingFlags.Instance | BindingFlags.Public);

//        for (int i = 0; i < methodFields.Length; i++)
//        {
//            MethodInfo info = methodFields[i];
//            OSCMethod attribute = Attribute.GetCustomAttribute(info, typeof(OSCMethod)) as OSCMethod;
//            if (attribute != null)
//            {
//                if((info.Name == "Save" || info.Name == "SaveAs" || info.Name == "Load" || info.Name == "Show") && !usePresets) continue;
//                Methods.Add(info.Name, info);
//            }
//        }

//        if (string.IsNullOrEmpty(id))
//            id = TargetScript.GetType().Name;

//        id = id.Replace(' ', '_');
//        sourceScene = SceneManager.GetActiveScene().name;

    //public virtual void OnScriptValueChanged(string name)
    //{
    //    if (String.IsNullOrEmpty(name) || !TargetFields.ContainsKey(name))
    //    {
    //        if (debug)
    //            Debug.Log("Name : " + name + " is null in target");
    //        return;
    //    }
    //    Fields[name].SetValue(this, TargetFields[name].GetValue(TargetScript));
    //    RaiseEventValueChanged(name);
    //}

    //public virtual void OnUiValueChanged(string name)
    //{
    //    if (String.IsNullOrEmpty(name) || !TargetFields.ContainsKey(name)) {
    //        if(debug)
    //            Debug.Log("Name : " + name + " is null in target");
    //        return; }
    //    TargetFields[name].SetValue(TargetScript, Fields[name].GetValue(this));
    //}

  //  public virtual void OnEnable()
  //  {
		//if (debug)
		//	Debug.Log("Registering " + this.GetType().Name + " script as " + id);
  //      OSCExposerMaster.Register(this);

  //      if (usePresets)
  //      {
  //          presetList = new List<string>();
  //          ReadFileList();

  //          if (presetList.Count >= 1)
  //          {
  //              currentPreset = presetList[0];
  //              LoadLatestUsedPreset();
  //          }
  //      }
  //  }

	//public virtual void Update() //Warn UI if attribut changes
 //   {
 //       var TargetFieldsArray = TargetFields.Values.ToArray();

 //       for (var i=0 ; i< TargetFieldsArray.Length ; i++)
 //       {
 //           var value = TargetFieldsArray[i].GetValue(TargetScript);
 //           //if (debug)
 //           //    Debug.Log("Target script value : " + value.ToString() + " previous : " + PreviousFieldsValues[i].ToString());
 //           if (value.ToString() != PreviousFieldsValues[i].ToString())
 //           {
 //               if (scriptValueChanged != null) scriptValueChanged(TargetFieldsArray[i].Name);
 //               RaiseEventValueChanged(TargetFieldsArray[i].Name);
 //               PreviousFieldsValues[i] = value;
 //           }
 //       }
 //   }

    

    

    

    //[OSCMethod]
    //public void Show() //Show preset file in explorer
    //{
    //    if (currentPreset == "") return;

    //    var itemPath = targetDirectory + currentPreset;
    //    itemPath = itemPath.Replace(@"/", @"\");   // explorer doesn't like front slashes
    //    System.Diagnostics.Process.Start("explorer.exe", "/select," + itemPath);
    //}

    

  //  //Override it if you want to do things after a load
  //  public virtual void DataLoaded() { }
  //  //Override it if you want to do things before a preset save
  //  public virtual void CallMeBeforeSave() { }

  //  public virtual void OnDisable()
  //  {
        

		//ControllableMaster.UnRegister(this);
  //  }

    //public FieldInfo getFieldInfoByName(string requestedName)
    //{
    //    var objectFields = GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);
    //    FieldInfo requestedField = null;

    //    foreach (var item in objectFields)
    //    {
    //        if(item.Name == requestedName)
    //            requestedField = item;
    //    }

    //    return requestedField;
    //}

    //protected void RaiseEventValueChanged(string property)
    //{
    //    if (!this.enabled)
    //        return;

    //    if (controllableValueChanged != null)
    //        controllableValueChanged(property);
    //}

    

//}

    
//}
