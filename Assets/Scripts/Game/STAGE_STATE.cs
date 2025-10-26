using UnityEngine;

public enum StageId
{
    STAGE1,
    STAGE2,
    STAGE3,
    STAGE4,
    STAGE5,
    STAGE6,
    STAGE6_2,
    STAGE6_21,
    STAGE6_22,
}

public static class STAGE_STATE
{
    public const int POS = 0;
    public const int ROTATE = 1;

    // 各ステージ： [0]=位置[pos] [1]=回転[euler(deg)]
    public static readonly Vector3[] STAGE1 =
    {
        new Vector3(5.58f, 1.6f, -1.9f),
        new Vector3(0f, 0f, 90f),
    };

    public static readonly Vector3[] STAGE2 =
    {
        new Vector3(6.15f, 1f, 3f),
        new Vector3(0f, -45f, 90f),
    };

    public static readonly Vector3[] STAGE3 =
    {
        new Vector3(8.55f, 1.6f, -0.7f),
        new Vector3(-90f, 0f, 90f),
    };

    public static readonly Vector3[] STAGE4 =
    {
        new Vector3(13.07f, 1.68f, 3.4f),
        new Vector3(-90f, 0f, 45f),
    };

    public static readonly Vector3[] STAGE5 =
    {
        new Vector3(6.45f, 0.6f, 3.09f),
        new Vector3(-90f, 0f, 45f),
    };

    public static readonly Vector3[] STAGE6 =
    {
        new Vector3(9.26f, 0.6f, 0.15f),
        new Vector3(0f, -90f, 90f),
    };

    public static readonly Vector3[] STAGE6_2 =
    {
        new Vector3(8.98f, 1.58f, 2.12f),
        new Vector3(-90f, 0f, 45f),
    };

    public static readonly Vector3[] STAGE6_21 =
    {
        new Vector3(6.48f, 1.58f, -2.88f),
        new Vector3(0f, 0f, 90f),
    };

    public static readonly Vector3[] STAGE6_22 =
    {
        new Vector3(6.48f, 1.58f, -1.63f),
        new Vector3(-90f, 90f, 0f),
    };

    /// <summary>
    /// enum からステージのPOS/ROTデータを返すユーティリティ
    /// </summary>
    public static bool TryGet(StageId id, out Vector3 pos, out Vector3 eulerDeg)
    {
        switch (id)
        {
            case StageId.STAGE1:
                pos = STAGE1[POS];
                eulerDeg = STAGE1[ROTATE];
                return true;
            case StageId.STAGE2:
                pos = STAGE2[POS];
                eulerDeg = STAGE2[ROTATE];
                return true;
            case StageId.STAGE3:
                pos = STAGE3[POS];
                eulerDeg = STAGE3[ROTATE];
                return true;
            case StageId.STAGE4:
                pos = STAGE4[POS];
                eulerDeg = STAGE4[ROTATE];
                return true;
            case StageId.STAGE5:
                pos = STAGE5[POS];
                eulerDeg = STAGE5[ROTATE];
                return true;
            case StageId.STAGE6:
                pos = STAGE6[POS];
                eulerDeg = STAGE6[ROTATE];
                return true;
            case StageId.STAGE6_2:
                pos = STAGE6_2[POS];
                eulerDeg = STAGE6_2[ROTATE];
                return true;
            case StageId.STAGE6_21:
                pos = STAGE6_21[POS];
                eulerDeg = STAGE6_21[ROTATE];
                return true;
            case StageId.STAGE6_22:
                pos = STAGE6_22[POS];
                eulerDeg = STAGE6_22[ROTATE];
                return true;
        }

        pos = default;
        eulerDeg = default;
        return false;
    }
}
