using System.Collections.Generic;
using System.Linq;
using UnityEditor;

/// <summary>
/// Queue of "regenerate this mirror once it compiles" requests, kept across the domain reload that
/// follows emptying a mirror whose body no longer compiles.
/// </summary>
/// <remarks>
/// Backed by <see cref="SessionState"/> rather than EditorPrefs because it must die with the Editor:
/// a request left over from a previous session refers to a compile that finished long ago and must
/// never fire. Entries are asset path and source script name, since neither survives the reload as
/// an object reference.
/// </remarks>
public static class PendingControllableUpdates
{
    const string SessionKey = "OCF.PendingControllableUpdates";

    //Asset paths cannot contain a newline, so it separates entries; the field separator is split on
    //from the right, because a path may contain the '|' a source script name never can.
    const char FieldSeparator = '|';

    //A list rather than a single slot: several mirrors can be repaired before the first compile
    //finishes, and one broken source script often breaks all of its mirrors at once.
    public static void Enqueue(string mirrorPath, string sourceName)
    {
        if (string.IsNullOrEmpty(mirrorPath) || string.IsNullOrEmpty(sourceName))
            return;

        string entry = mirrorPath + FieldSeparator + sourceName;

        var queued = Deserialize(SessionState.GetString(SessionKey, "")).ToList();

        //Deduplicated on the path: repairing the same mirror twice before the reload is one request.
        queued.RemoveAll(existing => PathOf(existing) == mirrorPath);
        queued.Add(entry);

        SessionState.SetString(SessionKey, Serialize(queued));
    }

    /// <summary>Returns the queued requests and empties the queue.</summary>
    /// <remarks>
    /// Reading and clearing are one step on purpose. If the regeneration fails - the source script
    /// was deleted, or is broken for a reason of its own - an entry left behind would be retried on
    /// every later reload, forever. Clearing first means even a throw while processing cannot produce
    /// a retry loop.
    /// </remarks>
    public static string[] TakeAll()
    {
        string raw = SessionState.GetString(SessionKey, "");
        SessionState.EraseString(SessionKey);

        return Deserialize(raw);
    }

    public static string PathOf(string entry)
    {
        int separator = entry == null ? -1 : entry.LastIndexOf(FieldSeparator);
        return separator < 0 ? entry : entry.Substring(0, separator);
    }

    public static string SourceNameOf(string entry)
    {
        int separator = entry == null ? -1 : entry.LastIndexOf(FieldSeparator);
        return separator < 0 ? null : entry.Substring(separator + 1);
    }

    public static string Serialize(IEnumerable<string> entries)
    {
        if (entries == null)
            return "";

        return string.Join("\n", entries.Where(entry => !string.IsNullOrEmpty(entry)).ToArray());
    }

    public static string[] Deserialize(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return new string[0];

        return raw.Split('\n')
                  .Select(entry => entry.Trim())
                  .Where(entry => entry.Length > 0)
                  .ToArray();
    }
}
