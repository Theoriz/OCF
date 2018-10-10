using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ControllableMasterControllable : Controllable {

    [Header("OSC UI control")]
    [OSCProperty]
    public int OSCInputPort;

    [OSCProperty(isInteractible = false)] public bool IsConnected;

    [OSCMethod]
    public void SaveAll()
    {
        ControllableMaster.SaveAllPresets();
    }

    [OSCMethod]
    public void SaveAsAll()
    {
        ControllableMaster.SaveAsAllPresets();
    }

    [OSCMethod]
    public void LoadAll()
    {
        ControllableMaster.LoadAllPresets();
    }
}
