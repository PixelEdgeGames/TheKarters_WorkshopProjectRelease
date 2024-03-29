using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

public class PTK_SkinTreeView : TreeView
{
    string ignorePhrases = "Ctrl+D,Outfits,Blender,Color Variations,3D Models, GameplayPrefabBase,WeaponsAnimations";
    string noCheckboxPhrases = "Color Variations";

    public event Action<HashSet<string>> OnCheckedItemsChanged;
    private HashSet<string> ignoreSet = new HashSet<string>();


    internal void RefreshIgnorePhrases()
    {
        SetIgnorePhrases(ignorePhrases.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
        SetNoCheckboxPhrases(noCheckboxPhrases.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
    }

    public void SetIgnorePhrases(string[] phrases)
    {
        ignoreSet.Clear();
        foreach (var phrase in phrases)
        {
            ignoreSet.Add(phrase.Trim());
        }
    }
    private HashSet<string> noCheckboxSet = new HashSet<string>();

    public void SetNoCheckboxPhrases(string[] phrases)
    {
        noCheckboxSet.Clear();
        foreach (var phrase in phrases)
        {
            noCheckboxSet.Add(phrase.Trim());
        }
    }


    public PTK_SkinTreeView(TreeViewState state) : base(state)
    {
    }

    protected override TreeViewItem BuildRoot()
    {
        var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };
        int id = 1;
        var items = CreateChildrenForDirectory(PTK_ModItemConfigurator.rootPath, ref id);
        SetupParentsAndChildrenFromDepths(root, items);
        return root;
    }
    private List<TreeViewItem> CreateChildrenForDirectory(string path, ref int id, int depth = 0)
    {
        var items = new List<TreeViewItem>();

        // Skip directories that match ignore phrases
        if (ShouldIgnore(path))
            return items;

        // Add current directory (skip for the root)
        if (depth > 0)
            items.Add(new MyTreeViewItem { id = id++, depth = depth, displayName = Path.GetFileName(path), pathSegment = Path.GetFileName(path), fullPath = path });

        var subDirs = Directory.GetDirectories(path);
        foreach (var dir in subDirs)
        {
            items.AddRange(CreateChildrenForDirectory(dir, ref id, depth + 1));
        }

        return items;
    }

    private bool ShouldIgnore(string path)
    {
        foreach (var ignore in ignoreSet)
        {
            if (path.Contains(ignore))
                return true;
        }
        return false;
    }
    HashSet<string> checkedItems_ = new HashSet<string>();
    public HashSet<string> GetCheckedItems()
    {
        return checkedItems_;
    }

    protected override void RowGUI(RowGUIArgs args)
    {
        MyTreeViewItem myItem = args.item as MyTreeViewItem;
        if (!ShouldNotRenderCheckbox(myItem.displayName) && ShouldRenderCheckbox_IfParent(myItem))
        {
            EditorGUI.BeginChangeCheck();
            bool wasChecked = checkedItems_.Contains(myItem.fullPath);
            bool isChecked = EditorGUI.Toggle(new Rect(args.rowRect.x + 2, args.rowRect.y, 16, args.rowRect.height), wasChecked);
            if (EditorGUI.EndChangeCheck())
            {
                if (isChecked && !wasChecked)
                {
                    checkedItems_.Add(myItem.fullPath);
                }
                else if (!isChecked && wasChecked)
                {
                    checkedItems_.Remove(myItem.fullPath);
                }

                OnCheckedItemsChanged?.Invoke(checkedItems_);
            }
        }

        base.RowGUI(args);


    }
    private bool ShouldRenderCheckbox_IfParent(MyTreeViewItem item)
    {
        if (item == null)
            return false;

        // if ((item as MyTreeViewItem).pathSegment == "Characters")
        //    return true;

        if (item.parent as MyTreeViewItem == null)
            return false;

        //  if ((item.parent as MyTreeViewItem).pathSegment == "Outfits")
        //        return true;

        if ((item.parent as MyTreeViewItem).pathSegment == "Characters")
            return true;

        if ((item.parent as MyTreeViewItem).pathSegment == "Vehicles")
            return true;

        if ((item.parent as MyTreeViewItem).pathSegment == "Tracks")
            return true;

        if ((item.parent as MyTreeViewItem).pathSegment == "Wheels")
            return true;

        if ((item.parent as MyTreeViewItem).pathSegment == "Stickers")
            return true;

        return false;
    }

    public HashSet<string> GetCheckedPaths()
    {
        return checkedItems_;
    }

    public void SetCheckedPaths(HashSet<string> paths)
    {
        checkedItems_.Clear();
        foreach (var path in paths)
        {
            checkedItems_.Add(path);
        }
    }

    public void RemovePath(string strPath)
    {
        checkedItems_.Remove(strPath);
    }

    public void SaveStateToPrefs()
    {
        string checkedPaths = string.Join(";", checkedItems_);
        EditorPrefs.SetString("SkinTreeView_Checked_Items", checkedPaths);
    }

    public void LoadStateFromPrefs()
    {
        if (EditorPrefs.HasKey("SkinTreeView_Checked_Items"))
        {
            var savedPaths = EditorPrefs.GetString("SkinTreeView_Checked_Items").Split(';');
            checkedItems_ = new HashSet<string>(savedPaths);
        }
    }

    private class MyTreeViewItem : TreeViewItem
    {
        public string pathSegment;
        public string fullPath; // New field
    }

    private bool ShouldNotRenderCheckbox(string path)
    {
        foreach (var noCheckbox in noCheckboxSet)
        {
            if (path.Contains(noCheckbox))
                return true;
        }
        return false;
    }

}
