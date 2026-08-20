using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

//Completes the repairs queued by ControllableGenerator: the mirrors were emptied so the project would
//compile, and this regenerates them from the source scripts now that the reload has made the real
//members visible to reflection.
public static class ControllableUpdateResumer
{
    #region Resuming after the domain reload

    //Deferred with delayCall so the work happens once the Editor is idle rather than mid-reload.
    [UnityEditor.Callbacks.DidReloadScripts]
    private static void ResumePendingUpdates()
    {
        //Takes and clears in one step - see PendingControllableUpdates.TakeAll.
        string[] pending = PendingControllableUpdates.TakeAll();
        if (pending.Length == 0)
            return;

        EditorApplication.delayCall += () => ResumeAll(pending);
    }

    private static void ResumeAll(string[] pending)
    {
        int updated = 0;
        var failed = new List<string>();

        foreach (string entry in pending)
        {
            string mirrorPath = PendingControllableUpdates.PathOf(entry);
            string sourceName = PendingControllableUpdates.SourceNameOf(entry);

            if (string.IsNullOrEmpty(sourceName) || !File.Exists(mirrorPath))
            {
                failed.Add($"{mirrorPath}: the mirror script is gone.");
                continue;
            }

            //The non-interactive form: this runs unattended, and a modal appearing by itself seconds
            //after the click would have no context. The single Refresh below is this method's job.
            if (ControllableGenerator.TryGenerateControllableForScript(sourceName, mirrorPath, out string error))
                updated++;
            else
                failed.Add($"{mirrorPath}: {error.Replace("\n", " ")}");
        }

        //Logged before the Refresh, which induces a domain reload.
        if (updated > 0)
            Debug.Log($"[OCF] Regenerated {updated} repaired Controllable script(s).");

        //An emptied mirror that could not be regenerated still compiles, so the project is usable and
        //running Update again once the source script is fixed finishes the job.
        if (failed.Count > 0)
            Debug.LogWarning($"[OCF] Could not regenerate {failed.Count} repaired Controllable script(s):\n- "
                + string.Join("\n- ", failed.ToArray()));

        if (updated > 0)
            AssetDatabase.Refresh();
    }

    #endregion
}
