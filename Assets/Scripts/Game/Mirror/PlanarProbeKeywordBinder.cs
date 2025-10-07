using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Renderer))]
public class PlanarProbeKeywordBinder : MonoBehaviour
{
    public enum ProbeSlot { One = 0, Two = 1, Three = 2, Four = 3 }
    public ProbeSlot slot = ProbeSlot.One;

    void OnEnable() => Apply();
    void OnValidate() => Apply();

    void Apply()
    {
        var r = GetComponent<Renderer>();
        var m = r ? r.sharedMaterial : null;
        if (!m) return;

        string[] keys = { "_PRID_ONE", "_PRID_TWO", "_PRID_THREE", "_PRID_FOUR" };
        for (int i = 0; i < keys.Length; i++)
        {
            if (i == (int)slot) m.EnableKeyword(keys[i]);
            else m.DisableKeyword(keys[i]);
        }
    }
}
