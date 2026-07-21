using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class ControllableMasterControllable : Controllable {

    [Header("OSC Settings")]

    [OSCProperty(readOnly = true)]
    public string IPAddress;
    [OSCProperty]
    public int OSCInputPort;

    [Tooltip("If connect fails, increment port and retry.")]
    [OSCProperty] public bool IncrementalConnect;

    [OSCProperty(readOnly = true)] public bool IsConnected;


    #region Mirror

    [OSCMethod]
    public void RefreshIP()
    {
        (TargetScript as ControllableMaster).RefreshIP();
    }

    //Written by hand for the same reason the generator emits one: Controllable's reflection poll
    //returns object, so it would box OSCInputPort, IncrementalConnect and IsConnected once per frame.
    protected override void PollTargetScript()
    {
        var target = TargetScript as ControllableMaster;
        if (target == null) return;

        if (IPAddress != target.IPAddress) { IPAddress = target.IPAddress; RaiseScriptValueChanged("IPAddress"); }
        if (OSCInputPort != target.OSCInputPort) { OSCInputPort = target.OSCInputPort; RaiseScriptValueChanged("OSCInputPort"); }
        if (IncrementalConnect != target.IncrementalConnect) { IncrementalConnect = target.IncrementalConnect; RaiseScriptValueChanged("IncrementalConnect"); }
        if (IsConnected != target.IsConnected) { IsConnected = target.IsConnected; RaiseScriptValueChanged("IsConnected"); }
    }

    #endregion

    #region Global actions

    //The global preset methods below, by name. These exist only on this class, so matching a button
    //by name alone is not enough to identify one — a target script may legitimately expose its own
    //SaveAll. Callers must also check the controllable is a ControllableMasterControllable.
    public static readonly string[] AllPresetMethodNames = { "SaveAll", "SaveAsAll", "LoadAll" };

    //Global buttons that are not preset operations. They get their own row under the preset row,
    //rather than being squeezed in beside Save All - three buttons do not fit across the panel.
    public static readonly string[] GlobalActionMethodNames = { "OpenPresetsFolder" };

    //On this class rather than Controllable so there is one global button instead of one per panel,
    //and so the name stays out of Controllable's reserved members - which would otherwise forbid it
    //as an [OSCExposed] name in every user script.
    [OSCMethod]
    public void OpenPresetsFolder()
    {
        (TargetScript as ControllableMaster).OpenPresetsFolder();
    }

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

    [OSCMethod(showInUI = false)]
    public void LoadAll()
    {
        ControllableMaster.LoadAllPresets();
    }

    #endregion
}
