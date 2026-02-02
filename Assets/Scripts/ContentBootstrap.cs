using UnityEngine;

public class ContentBootstrap : MonoBehaviour
{
    [SerializeField] private PathSettings pathSettings;

    private void Awake()
    {
        if (pathSettings == null)
        {
            Debug.LogError("ContentBootstrap] Missing LuminaPathSettings reference.");
            enabled = false;
            return;
        }

        ContentPaths.EnsureFolders(pathSettings);
    }
}

