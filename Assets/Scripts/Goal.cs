using UnityEngine;
using UnityEngine.SceneManagement;

public class Goal : MonoBehaviour
{
    [Header("プレイヤー")]
    [SerializeField] Transform Player;

    [Header("ゴール")]
    [SerializeField] Collider GoalObj;

    [Header("シーン名")]
    [SerializeField] string SceneName;

    bool Load = false;

    // Update is called once per frame
    void Update()
    {
        if (Load)
        {
            return;
        }

        Vector3 pos = Player.position + new Vector3(0f, -0.5f, 0f);

        if(GoalObj.bounds.Contains(pos))
        {
            Load = true;
            SceneManager.LoadScene(SceneName);
        }
    }
}
