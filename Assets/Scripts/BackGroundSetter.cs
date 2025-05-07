using UnityEngine;
using UnityEngine.UI;

public class BackGroundSetter : MonoBehaviour
{
    [SerializeField] private RawImage bgImage;

    void Start()
    {
        bgImage.transform.SetAsFirstSibling();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
