using UnityEngine.SceneManagement;

public static class EndingManager
{
    public static void TriggerGoodEnding()
    {
        if (TransitionManager.Instance != null)
            TransitionManager.Instance.DoSceneTransition(SceneNames.Credits);
        else
            SceneManager.LoadScene(SceneNames.Credits);
    }
}
