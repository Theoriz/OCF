using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using System.Reflection;

public class ExpositionSettings
{
    public bool Exposed = true;
    public bool IncludeInPresets = true;
    public bool isInteractible = true;
}

public class ComponentExpositionSettings
{
    public bool UseUnityHierarchy;
    public string Id;
    public List<ExpositionSettings> ClassAttributsSettings;
    public List<ExpositionSettings> MethodsSettings;
}

public class OSCExposer : MonoBehaviour
{
    public bool ShowDebug;

    public List<Component> ComponentsToExpose;

    private List<ExposedObject> ExposedObjects;

    private void OnEnable()
    {
        foreach (var element in ExposedObjects)
            OSCExposerMaster.Register(element);
    }

    private void OnDisable()
    {
        foreach (var element in ExposedObjects)
        {
            OSCExposerMaster.Unregister(element);
        }
    }

    private void OnApplicationQuit()
    {
        foreach (var element in ExposedObjects)
        {
            Destroy(element.gameObject);
        }
    }

    private void Awake()
    {
        ExposedObjects = new List<ExposedObject>();

        Test();
    }

    public void Test()
    {
        var testFieldExpositionSettings = new List<ExpositionSettings>();
        testFieldExpositionSettings.Add(new ExpositionSettings() {Exposed = true, IncludeInPresets = true, isInteractible = true });
        testFieldExpositionSettings.Add(new ExpositionSettings() { Exposed = true, IncludeInPresets = true, isInteractible = true });

        var testMethodExpositionSettings = new List<ExpositionSettings>();
        testMethodExpositionSettings.Add(new ExpositionSettings() { Exposed = true, IncludeInPresets = false, isInteractible = false });

        var testComponentExpositionSettings = new ComponentExpositionSettings() { Id = "TESTEXPOSITION", ClassAttributsSettings = testFieldExpositionSettings, MethodsSettings = testMethodExpositionSettings } ;

        CreateExposedObject(ComponentsToExpose[0], testComponentExpositionSettings);
    }

    public void CreateExposedObject(object newComponent, ComponentExpositionSettings attributsAndMethodsSettings)
    {
        var newExposedObjectGO = new GameObject();
        newExposedObjectGO.SetActive(false); //to prevent Awake call when adding ExposedObject component
        newExposedObjectGO.name = attributsAndMethodsSettings.Id;

        var newExposedObject = newExposedObjectGO.AddComponent<ExposedObject>();
        newExposedObject.Id = attributsAndMethodsSettings.Id;
        newExposedObject.AssociatedComponent = newComponent;

        ExtractClassAttributs(newExposedObject, newComponent, attributsAndMethodsSettings.ClassAttributsSettings);
        ExtractMethods(newExposedObject, newComponent, attributsAndMethodsSettings.MethodsSettings);

        ExposedObjects.Add(newExposedObject);

        OSCExposerMaster.Register(newExposedObject);

        newExposedObjectGO.SetActive(true);
    }

    public void ExtractClassAttributs(ExposedObject exposedObject, object newComponent, List<ExpositionSettings> attributsSettings)
    {
        exposedObject.Attributs = new Dictionary<string, ClassAttributInfo>();

        Type t = newComponent.GetType();
        //Fields
        FieldInfo[] componentFields = t.GetFields(BindingFlags.Instance | BindingFlags.Public);

        if(ShowDebug)
            Debug.Log("componentFields.Length : " + componentFields.Length);

        int i = 0;
        for (; i < componentFields.Length; i++)
        {
            FieldInfo info = componentFields[i];

            if (attributsSettings[i].Exposed)
            {
                var exposedAttributInfo = new ClassAttributInfo();
                exposedAttributInfo.Field = info;
                exposedAttributInfo.ExposeSettings = attributsSettings[i];

                exposedObject.Attributs.Add(info.Name, exposedAttributInfo);
            }
        }

        //Properties
        PropertyInfo[] componentProperties = t.GetProperties(BindingFlags.Instance | BindingFlags.Public);

        if (ShowDebug)
            Debug.Log("componentProperties.Length : " + componentProperties.Length);

        for (int j = 0 ; j < componentProperties.Length; j++)
        {
            if (componentProperties[j].Name == "useGUILayout")
                break;

            if (ShowDebug)
                Debug.Log("componentProperties.Name : " + componentProperties[j].Name);

            PropertyInfo info = componentProperties[j];
            if (attributsSettings[i+j].Exposed)
            {
                var exposedAttributInfo = new ClassAttributInfo();
                exposedAttributInfo.Property = info;
                exposedAttributInfo.ExposeSettings = attributsSettings[i];

                exposedObject.Attributs.Add(info.Name, exposedAttributInfo);
            }
        }
    }

    public void ExtractMethods(ExposedObject exposedObject, object newComponent, List<ExpositionSettings> methodsSettings)
    {
        exposedObject.Methods = new Dictionary<string, ExposedMethodInfo>();

        Type t = newComponent.GetType();

        exposedObject.Methods = new Dictionary<string, ExposedMethodInfo>();

        var methodFields = (t.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                                        .Where(m => !m.IsSpecialName)).ToArray();

        if (ShowDebug)
            Debug.Log("methodFields.Length : " + methodFields.Length);

        for (int i = 0; i < methodFields.Length; i++)
        {

            if (methodFields[i].Name == "IsInvoking")
                break;

            if (ShowDebug)
                Debug.Log("methodFields.Name : " + methodFields[i].Name);

            MethodInfo info = methodFields[i];
            if(methodsSettings[i].Exposed) {
                var exposedMethodInfo = new ExposedMethodInfo();
                exposedMethodInfo.Method = info;
                exposedMethodInfo.ExposeSettings = methodsSettings[i];

                exposedObject.Methods.Add(info.Name, exposedMethodInfo);
            }
        }
    }
}
