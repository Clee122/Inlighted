using UnityEditor;
using UnityEngine;

public static class RemoveAnimationEventsTool
{
    [MenuItem("Tools/Animation/Remove Events From Selected Clips")]
    private static void RemoveEventsFromSelectedClips()
    {
        Object[] selectedObjects = Selection.objects;

        if (selectedObjects.Length == 0)
        {
            Debug.LogWarning("No animation clips selected. Select the broken .anim clips first.");
            return;
        }

        int cleanedClipCount = 0;

        foreach (Object selectedObject in selectedObjects)
        {
            AnimationClip animationClip = selectedObject as AnimationClip;

            if (animationClip == null)
            {
                continue;
            }

            // Empty Animation Events can cause Unity to throw errors every time the clip reaches that frame.
            // Clearing the event array removes those accidental markers without changing sprite keyframes.
            AnimationUtility.SetAnimationEvents(animationClip, new AnimationEvent[0]);

            EditorUtility.SetDirty(animationClip);
            cleanedClipCount++;

            Debug.Log("Removed Animation Events from: " + animationClip.name);
        }

        AssetDatabase.SaveAssets();

        Debug.Log("Finished removing Animation Events. Clips cleaned: " + cleanedClipCount);
    }
}