using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ControllableMasterControllable : MonoBehaviour {

    public int IntField;

    private int _intProperty;
    public int IntProperty {
        get { return _intProperty; }
        set { _intProperty = value; }
    }

    public void Hi() {
        Debug.Log("Hi !");
    }
}
