using UnityEngine;
//using UnityEngine.Windows;

public class QuitButton : MonoBehaviour
{
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.ExitPlaymode();
#else
       Application.Quit();
#endif
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return))
            QuitGame();
    }
    //public void ReturnToTitle()
    //{
    //    SceneManager.LoadScene("TitleScene");
    //}
}
