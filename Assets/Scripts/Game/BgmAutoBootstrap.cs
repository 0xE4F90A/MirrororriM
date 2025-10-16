using UnityEngine;

public static class BgmAutoBootstrap
{
    // 最初のシーンがロードされる前に実行される
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        // 既に手動配置されているなら何もしない
        if (Object.FindFirstObjectByType<BgmManager>() != null)
        {
            return;
        }

        // 可能なら Resources からプレハブを使う（インスペクタの設定を活かせる）
        var prefab = Resources.Load<GameObject>("Bgm/BgmManager"); // 例: Assets/Resources/Bgm/BgmManager.prefab
        if (prefab != null)
        {
            Object.Instantiate(prefab); // BgmManager が付いているプレハブにしておく
            return;
        }

        // フォールバック：空のGOにコンポーネントを付ける
        var go = new GameObject("BgmManager(Auto)");
        go.AddComponent<BgmManager>(); // AwakeでDontDestroyOnLoadされ、EnsureReadyもあるのでOK
    }
}
