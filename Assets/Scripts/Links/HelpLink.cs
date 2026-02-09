
using UnityEngine;

public class HelpLink : MonoBehaviour
{
    [SerializeField] private string gameTutorialUrl =
        "";
    [SerializeField]
    private string imageManagementUrl =
        "";
    
    public void OpenGameTutorial()
    {
        Application.OpenURL(gameTutorialUrl);
    }

    public void OpenImageManagementTutorial()
    {
        Application.OpenURL(imageManagementUrl);
    }
}
