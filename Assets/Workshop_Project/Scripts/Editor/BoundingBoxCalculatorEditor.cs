using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(BoundingBoxCalculator))]
[CanEditMultipleObjects]
public class BoundingBoxCalculatorEditor : Editor
{
    private static BoundingBoxCalculator currentlyEditing;

    private void OnEnable()
    {
        currentlyEditing = (BoundingBoxCalculator)target;
        Selection.selectionChanged += OnSelectionChanged;
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= OnSelectionChanged;
        currentlyEditing = null;
    }

    void OnSelectionChanged()
    {
        if (currentlyEditing == null) return;

        Transform[] children = currentlyEditing.transform.GetComponentsInChildren<Transform>();
        for (int i = 1; i < children.Length; i++)
        {
            if (children[i].gameObject == Selection.activeGameObject)
            {
                currentlyEditing.ActivateChild(i - 1);
                break;
            }
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        BoundingBoxCalculator switcher = (BoundingBoxCalculator)target;

        if (GUILayout.Button("RefreshOffsets"))
        {
            switcher.RefreshOffsets();
        }

        if (switcher.childDatas.Length != switcher.transform.childCount)
            switcher.RefreshOffsets();

        if (GUILayout.Button("Prev"))
        {
            int newIndex = Mathf.Clamp(switcher.currentActive - 1, 0, switcher.transform.childCount - 1);
            switcher.ActivateChild(newIndex);
        }

        if (GUILayout.Button("Next"))
        {
            int newIndex = Mathf.Clamp(switcher.currentActive + 1, 0, switcher.transform.childCount - 1);
            switcher.ActivateChild(newIndex);
        }


        for (int i = 0; i < switcher.transform.childCount; i++)
        {
            BoundingBoxCalculator.ChildData data = switcher.childDatas[i];
            Transform child = data.childReference;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(child.name))
            {
                switcher.ActivateChild(i);
            }
            EditorGUILayout.LabelField($"Pos: {data.vPosition} Rot: {data.qRotation.eulerAngles} Scale: {data.vLoosyScale}");
            EditorGUILayout.LabelField($"Path: {data.path}");
            EditorGUILayout.EndHorizontal();
        }

        serializedObject.ApplyModifiedProperties();
    }
}