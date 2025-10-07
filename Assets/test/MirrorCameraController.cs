using UnityEngine;

public class MirrorCameraController : MonoBehaviour
{
    public Camera mainCamera;       // ��?����
    public Transform mirrorPlane;   // ?�q����?��
    public Camera mirrorCamera;     // ?�q?����

    void LateUpdate()
    {
        if (!mainCamera || !mirrorPlane || !mirrorCamera) return;

        // ?�ʖ@?
        Vector3 normal = mirrorPlane.forward;

        // ���ˎ�?�����I�ʒu
        Vector3 mirrorPos = mirrorPlane.position;
        Vector3 reflectedPos = ReflectPoint(mainCamera.transform.position, mirrorPos, normal);

        // ���ˎ�?�����I forward ����
        Vector3 reflectedForward = ReflectDirection(mainCamera.transform.forward, normal);

        // ?�u?�q?�����I�ʒu�a����
        mirrorCamera.transform.position = reflectedPos;
        mirrorCamera.transform.rotation = Quaternion.LookRotation(reflectedForward, Vector3.up);
    }

    // �_?�����ʓI?�̓_
    Vector3 ReflectPoint(Vector3 point, Vector3 planePos, Vector3 planeNormal)
    {
        Vector3 toPoint = point - planePos;
        Vector3 projected = Vector3.Project(toPoint, planeNormal);
        return point - 2 * projected;
    }

    // ����?�����ʓI���˕���
    Vector3 ReflectDirection(Vector3 dir, Vector3 planeNormal)
    {
        return Vector3.Reflect(dir, planeNormal);
    }
}
