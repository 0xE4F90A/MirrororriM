using UnityEngine;
using UnityEngine.UI;

public class RenderToUI : MonoBehaviour
{
    [Header("?定?景中的相机")]
    public Camera renderCamera;

    [Header("?定 UI 中的 RawImage")]
    public RawImage displayUI;

    [Header("?定 RenderTexture ?源")]
    public RenderTexture renderTexture;

    void Start()
    {
        // ?置相机的目??染?理
        renderCamera.targetTexture = renderTexture;

        // ?置 RawImage 的?????理
        displayUI.texture = renderTexture;
    }
}
