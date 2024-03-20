using UnityEditor;
using UnityEngine;

public class PTK_Skins_WorkshopManagerEditor : EditorWindow
{
    [MenuItem("PixelTools/WorkshopModule/SkinsManager")]
    public static void ShowExample()
    {
        PTK_Skins_WorkshopManagerEditor wnd = GetWindow<PTK_Skins_WorkshopManagerEditor>();
        wnd.titleContent = new GUIContent("Pixel SkinsManager");
    }

    Vector2 vScrollViewPosition = Vector2.zero;

    public void OnGUI()
    {
        GUIStyle centeredLabelStyle = new GUIStyle(GUI.skin.label);
        centeredLabelStyle.alignment = TextAnchor.MiddleCenter;
        centeredLabelStyle.wordWrap = true;

         
        GUIStyle mainCategoryStyle = new GUIStyle(GUI.skin.label);
        mainCategoryStyle.alignment = TextAnchor.MiddleCenter;
        mainCategoryStyle.wordWrap = true;
        mainCategoryStyle.fontSize = 20;

        char dirSep = System.IO.Path.DirectorySeparatorChar;

        GUILayout.Space(20);

        int iBoxButtonSize = 100;

        GUILayout.BeginVertical("", "window",GUILayout.Width(iBoxButtonSize*4 + 40));
        GUILayout.Label("Heros", mainCategoryStyle);

        vScrollViewPosition = GUILayout.BeginScrollView(vScrollViewPosition);

        GUI.color = Color.yellow;
        if (GUILayout.Button("Add New",  GUILayout.Height(30)) == true)
        {

        }

        GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(3));

        GUI.color = Color.white;

        System.IO.DirectoryInfo herosDirInfo = new System.IO.DirectoryInfo(Application.dataPath + dirSep + "Workshop_Mods" + dirSep + "Skins_Workshop" + dirSep + "Characters");
        System.IO.DirectoryInfo[] herosDir = herosDirInfo.GetDirectories();

        int iRowsCount = (int) Mathf.Ceil( herosDir.Length / 4.0f);

        for (int iRows = 0; iRows < iRowsCount; iRows++)
        {
            GUILayout.BeginHorizontal();

            for (int iColumns = 0; iColumns < 4; iColumns++)
            {
                int index = iRows * 4 + iColumns;

                if (index < herosDir.Length)
                {
                    GUILayout.BeginVertical();
                    if (GUILayout.Button("", GUILayout.Width(iBoxButtonSize), GUILayout.Height(iBoxButtonSize)) == true)
                    {

                    }
                    GUILayout.Label(herosDir[index].Name, centeredLabelStyle, GUILayout.Width(iBoxButtonSize), GUILayout.Height(30));

                    GUILayout.EndVertical();
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(3));
        }

        GUILayout.EndScrollView();


        GUILayout.EndVertical();


        GUILayout.Space(20);
    }
}