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
        transform.localRotation = Quaternion.Euler(0.0F, -148f, 0.0F);
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
        Transform activeChildModel = null;

        if (index >= 0 && index < transform.childCount)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                bool bActive = i == index;
                transform.GetChild(i).gameObject.SetActive(bActive);

                if (bActive == true)
                    activeChildModel = transform.GetChild(i);
            }
            currentActive = index;
        }

#if UNITY_EDITOR
        // ensure it is presented in menu-standing-pose for icon render
        Editor_SampleMenuStandingAnimationPoseForCharacter(activeChildModel);
#endif
    }

#if UNITY_EDITOR
    public void Editor_SampleMenuStandingAnimationPoseForCharacter(Transform prefabCharacterParent)
    {
        if (prefabCharacterParent == null)
            return;

        GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSource(prefabCharacterParent.gameObject) as GameObject;

        string path = AssetDatabase.GetAssetPath(prefab);
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("Active child model is not part of the assets.");
            return;
        }

        // is it character?
        if (!path.Contains("/Characters/"))
        {
            return;
        }

        string[] segments = path.Split('/');
        int charIndex = System.Array.IndexOf(segments, "Characters") + 1;

        if (charIndex <= 0 || charIndex >= segments.Length)
        {
            Debug.LogError("Character directory not found in the path.");
            return;
        }

        string charName = segments[charIndex];
        string scriptableObjectPath = path.Substring(0, path.IndexOf("Characters")) + $"Characters/{charName}/__PUT_Blender_ANIM_Export_here/PTK_Workshop_Char Anim Config.asset";
        

        PTK_Workshop_CharAnimConfig config = AssetDatabase.LoadAssetAtPath<PTK_Workshop_CharAnimConfig>(scriptableObjectPath);

        if (config != null)
        {

            foreach (Transform child in prefabCharacterParent.transform)
            {
                child.localPosition = Vector3.zero;
            }

            if (config.CharacterA.Menu.Count > 0)
            {
                AnimationClip animClip = config.CharacterA.GetClipByNameFull("idle_menu");
                if (animClip == null)
                {
                    Debug.LogError("Cant find menu animation clip for character " + path);
                }
                else
                {
                    GameObject animModel = prefabCharacterParent.GetChild(0).gameObject;
                    animClip.SampleAnimation(animModel, 0.0f);
                }
            }


            if (config.CharacterB.Menu.Count > 0)
            {
                AnimationClip animClip = config.CharacterB.GetClipByNameFull("idle_menu");
                if (animClip == null)
                {
                    Debug.LogError("Cant find menu animation clip for character " + path);
                }
                else
                {
                    GameObject animModel = prefabCharacterParent.GetChild(1).gameObject;
                    animClip.SampleAnimation(animModel, 0.0f);
                }
            }

            if (config.CharacterC.Menu.Count > 0)
            {
                AnimationClip animClip = config.CharacterC.GetClipByNameFull("idle_menu");
                if (animClip == null)
                {
                    Debug.LogError("Cant find menu animation clip for character " + path);
                }
                else
                {
                    GameObject animModel = prefabCharacterParent.GetChild(2).gameObject;
                    animClip.SampleAnimation(animModel, 0.0f);
                }
            }
        }
        else
        {
            Debug.LogError("Failed to load the PTK_Workshop_CharAnimConfig scriptable object for character.");
        }
    }
#endif

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