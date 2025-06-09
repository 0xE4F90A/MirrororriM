using UnityEngine;
using UnityEngine.UI;

public class RenderToUI : MonoBehaviour
{
    [Header("?��?�i���I����")]
    public Camera renderCamera;

    [Header("?�� UI ���I RawImage")]
    public RawImage displayUI;

    [Header("?�� RenderTexture ?��")]
    public RenderTexture renderTexture;

    void Start()
    {
        // ?�u�����I��??��?��
        renderCamera.targetTexture = renderTexture;

        // ?�u RawImage �I?????��
        displayUI.texture = renderTexture;
    }
}
