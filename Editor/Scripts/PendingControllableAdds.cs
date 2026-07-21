using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Queue of "add the Controllable component once it compiles" requests, kept across the domain
/// reload that follows generating a mirror script.
/// </summary>
/// <remarks>
/// Backed by <see cref="SessionState"/> rather than EditorPrefs because it must die with the Editor:
/// a request left over from a previous session refers to a compile that finished long ago and must
/// never fire. Targets are stored as <see cref="GlobalObjectId"/> strings, since neither an object
/// reference nor an instance ID survives the reload.
/// </remarks>
public static class PendingControllableAdds
{
    const string SessionKey = "OCF.PendingControllableAdds";

    //A list rather than a single slot: the menu can be invoked on several components before the
    //first compile finishes.
    public static void Enqueue(Component sourceComponent)
    {
        if (sourceComponent == null)
            return;

        string id = GlobalObjectId.GetGlobalObjectIdSlow(sourceComponent).ToString();

        var queued = Deserialize(SessionState.GetString(SessionKey, "")).ToList();
        if (queued.Contains(id))
            return;

        queued.Add(id);
        SessionState.SetString(SessionKey, Serialize(queued));
    }

    /// <summary>Returns the queued requests and empties the queue.</summary>
    /// <remarks>
    /// Reading and clearing are one step on purpose. If the generated script fails to compile, the
    /// reload happens with the type still missing; an entry left behind would be retried on every
    /// later reload, forever. Clearing first means even a throw while processing cannot produce a
    /// retry loop.
    /// </remarks>
    public static string[] TakeAll()
    {
        string raw = SessionState.GetString(SessionKey, "");
        SessionState.EraseString(SessionKey);

        return Deserialize(raw);
    }

    //GlobalObjectId.ToString() is "GlobalObjectId_V1-<type>-<guid>-<id>-<prefabid>": hyphens and hex
    //only, so a newline is a safe separator.
    public static string Serialize(IEnumerable<string> ids)
    {
        if (ids == null)
            return "";

        return string.Join("\n", ids.Where(id => !string.IsNullOrEmpty(id)).ToArray());
    }

    public static string[] Deserialize(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return new string[0];

        return raw.Split('\n')
                  .Select(id => id.Trim())
                  .Where(id => id.Length > 0)
                  .ToArray();
    }
}
