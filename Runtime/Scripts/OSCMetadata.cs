using System;

public class OSCMetadata : Attribute
{
}

[AttributeUsage(AttributeTargets.Field)]
public class OSCProperty : OSCMetadata
{
    public string targetList;
    public string enumName = "";
    public bool includeInPresets = true;
    public bool showInUI = true;
    public bool readOnly = false;
}

[AttributeUsage(AttributeTargets.Method)]
public class OSCMethod : OSCMetadata
{
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
public class OSCExposed : Attribute
{
    public bool readOnly = false;
}