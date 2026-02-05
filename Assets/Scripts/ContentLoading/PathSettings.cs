using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Lumina/Path Settings", fileName = "PathSettings")]
public class PathSettings : ScriptableObject
{
    [Header("Launcher Paths")]
    public string gamesFolder = "Games";
    public string userContentFolder = "UserContent";

    [Header("MyGame Paths")]
    [Header("MyGame Paths")]
    public string myGameFolder = "Lumina";
    public string imagesFolder = "Images";
    public string thumbnailsFolder = "Thumbnails";
    public string savesFolder = "Saves";  


    [Header("Optional Overrides (Advanced)")]
    [Tooltip("If not empty, treat this as absolute LauncherRoot. Leave empty for auto-detection.")]
    public string launcherRootAbsoluteOverride = "";

    [Tooltip("If not empty, treat this as absolute UserContent root. Leave empty for default LauncherRoot/UserContent.")]
    public string userContentAbsoluteOverride = "";
}
