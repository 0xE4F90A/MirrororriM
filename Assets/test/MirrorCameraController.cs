using UnityEngine;

public class MirrorCameraController : MonoBehaviour
{
    public Camera mainCamera;       // 主?像机
    public Transform mirrorPlane;   // ?子平面?象
    public Camera mirrorCamera;     // ?子?像机

    void LateUpdate()
    {
        if (!mainCamera || !mirrorPlane || !mirrorCamera) return;

        // ?面法?
        Vector3 normal = mirrorPlane.forward;

        // 反射主?像机的位置
        Vector3 mirrorPos = mirrorPlane.position;
        Vector3 reflectedPos = ReflectPoint(mainCamera.transform.position, mirrorPos, normal);

        // 反射主?像机的 forward 向量
        Vector3 reflectedForward = ReflectDirection(mainCamera.transform.forward, normal);

        // ?置?子?像机的位置和方向
        mirrorCamera.transform.position = reflectedPos;
        mirrorCamera.transform.rotation = Quaternion.LookRotation(reflectedForward, Vector3.up);
    }

    // 点?于平面的?称点
    Vector3 ReflectPoint(Vector3 point, Vector3 planePos, Vector3 planeNormal)
    {
        Vector3 toPoint = point - planePos;
        Vector3 projected = Vector3.Project(toPoint, planeNormal);
        return point - 2 * projected;
    }

    // 向量?于平面的反射方向
    Vector3 ReflectDirection(Vector3 dir, Vector3 planeNormal)
    {
        return Vector3.Reflect(dir, planeNormal);
    }
}
