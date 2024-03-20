using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class GlobalSelectionEvent
{
    static GlobalSelectionEvent()
    {
        Selection.selectionChanged += OnGlobalSelectionChanged;
    }

    private static void OnGlobalSelectionChanged()
    {
        GameObject selectedObject = Selection.activeGameObject;

        if (selectedObject == null) return;

        BoundingBoxCalculator switcher = selectedObject.GetComponentInParent<BoundingBoxCalculator>();

        if (switcher)
        {
            int index = -1;
            for (int i = 0; i < switcher.transform.childCount; i++)
            {
                if (switcher.transform.GetChild(i).gameObject == selectedObject)
                {
                    index = i;
                    break;
                }
            }
            if (index != -1)
            {
                switcher.ActivateChild(index);
            }
        }
    }
}