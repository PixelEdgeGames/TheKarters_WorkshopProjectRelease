using UnityEditor;
using UnityEngine;

[ExecuteInEditMode]
public class BoundingBoxCalculator : MonoBehaviour
{
    [System.Serializable]
    public class ChildData
    {
        public string path;
        public Vector3 vPosition;
        public Quaternion qRotation;
        public Vector3 vLoosyScale;
        public Transform childReference;
    }

    [HideInInspector]
    public int currentActive = 0;
    [HideInInspector]
    public ChildData[] childDatas;


    private void Update()
    {
        transform.localPosition = new Vector3(0.0F, 0.0F, 6.5F);
        transform.rotation = Quaternion.Euler(0.0F, 200.0F, 0.0F);
    }
    public void RefreshOffsets()
    {
#if UNITY_EDITOR
        childDatas = new ChildData[transform.childCount];
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            child.gameObject.SetActive(i == currentActive);
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject);


            childDatas[i] = new ChildData
            {
                path = path,
                vPosition = child.position,
                qRotation = child.rotation,
                vLoosyScale = child.lossyScale,
                childReference = child
            };
        }
#endif
    }

    public void ActivateChild(int index)
    {
        if (index >= 0 && index < transform.childCount)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                transform.GetChild(i).gameObject.SetActive(i == index);
            }
            currentActive = index;
        }
    }

    public ChildData GetChildWithBestPath(string strPathToFile)
    {
        int bestMatchCount = 0;
        ChildData bestMatchChild = null;

        foreach (var childData in childDatas)
        {
            int matchCount = GetMatchingDirectoryCount(childData.path, strPathToFile);

            if (matchCount > bestMatchCount)
            {
                bestMatchCount = matchCount;
                bestMatchChild = childData;
            }
        }

        return bestMatchChild;
    }

    private int GetMatchingDirectoryCount(string path1, string path2)
    {
        var segments1 = path1.Split(new char[] { '/' }, System.StringSplitOptions.RemoveEmptyEntries);
        var segments2 = path2.Split(new char[] { '/' }, System.StringSplitOptions.RemoveEmptyEntries);

        int matchCount = 0;
        for (int i = 0; i < Mathf.Min(segments1.Length, segments2.Length); i++)
        {
            if (segments1[i] == segments2[i])
                matchCount++;
            else
                break;  // Early break since directories after a mismatch won't be considered a match.
        }

        return matchCount;
    }
}