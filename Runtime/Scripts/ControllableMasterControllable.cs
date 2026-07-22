using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class ControllableMasterControllable : Controllable {

    [Header("OSC Settings")]

    [OCFProperty(readOnly = true)]
    public string IPAddress;
    [OCFProperty]
    public int OSCInputPort;

    [Tooltip("If connect fails, increment port and retry.")]
    [OCFProperty] public bool IncrementalConnect;

    [OCFProperty(readOnly = true)] public bool IsConnected;


    #region Mirror

    [OCFMethod]
    public void RefreshIP()
    {
        (controllableTargetScript as ControllableMaster).RefreshIP();
    }

    //Written by hand for the same reason the generator emits one: Controllable's reflection poll
    //returns object, so it would box OSCInputPort, IncrementalConnect and IsConnected once per frame.
    protected override void PollTargetScript()
    {
        var target = controllableTargetScript as ControllableMaster;
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
    public static readonly string[] AllPresetMethodNames =
        { "ControllableSaveAll", "ControllableSaveAsAll", "ControllableLoadAll" };

    //Global buttons that are not preset operations. They get their own row under the preset row,
    //rather than being squeezed in beside Save All - their labels are too long to share a row.
    public static readonly string[] GlobalActionMethodNames = { "ControllableOpenPresetsFolder" };

    //On this class rather than Controllable so there is one global button instead of one per panel,
    //and so the name stays out of Controllable's reserved members - which would otherwise forbid it
    //as an [OCFExposed] name in every user script.
    [OCFMethod]
    public void ControllableOpenPresetsFolder()
    {
        (controllableTargetScript as ControllableMaster).OpenPresetsFolder();
    }

    [OCFMethod]
    public void ControllableSaveAll()
    {
        ControllableMaster.SaveAllPresets();
    }

    [OCFMethod]
    public void ControllableSaveAsAll()
    {
        ControllableMaster.SaveAsAllPresets();
    }

    [OCFMethod]
    public void ControllableLoadAll()
    {
        ControllableMaster.LoadAllPresets();
    }

    #endregion
}
