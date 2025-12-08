using System;

public class OSCMetadata : Attribute
{
}

[AttributeUsage(AttributeTargets.Field)]
public class OSCProperty : OSCMetadata
{
    public string TargetList;
    public string enumName = "";
    public bool IncludeInPresets = true;
    public bool ShowInUI = true;
    public bool isInteractible = true;
}

[AttributeUsage(AttributeTargets.Method)]
public class OSCMethod : OSCMetadata
{
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
public class OSCExposed : Attribute
{
}