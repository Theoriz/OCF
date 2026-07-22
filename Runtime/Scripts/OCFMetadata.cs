using System;

public class OCFMetadata : Attribute
{
}

[AttributeUsage(AttributeTargets.Field)]
public class OCFProperty : OCFMetadata
{
    public string targetList;
    public bool includeInPresets = true;
    public bool showInUI = true;
    public bool readOnly = false;
}

[AttributeUsage(AttributeTargets.Method)]
public class OCFMethod : OCFMetadata
{
    public bool showInUI = true;
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
public class OCFExposed : Attribute
{
    public bool readOnly = false;

    //Name of a List<string> on the same script whose entries this member is chosen from. The
    //generator forwards it to [OCFProperty(targetList = ...)] on the mirror, and the list is read
    //back off the target script at runtime, so a generated mirror needs no list of its own.
    public string targetList = "";
}
