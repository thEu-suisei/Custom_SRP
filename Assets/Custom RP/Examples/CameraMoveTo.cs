using System;
using UnityEngine;

public class CameraMoveTo : MonoBehaviour
{
    public Transform target;    // 目标位置
    public Transform focusPoint;  // 相机的关注点，若存在，摄像机始终看着这个点
    public float moveSpeed = 2.0f;  // 移动速度
    public float rotationSpeed = 5.0f; // 旋转速度
    public float distanceThreshold = 0.1f; // 接近目标时停止的距离
    public bool useLinearMovement = false;  // 是否使用线性移动

    private bool shouldMove = false; // 控制摄像机是否移动

    // 开始移动摄像机到目标位置
    public void MoveToTarget(Transform newTarget)
    {
        target = newTarget;
        shouldMove = true;
    }

    private void Start()
    {
        MoveToTarget(target);
    }

    void Update()
    {
        if (shouldMove && target != null)
        {
            if (useLinearMovement)
            {
                // 线性移动
                Vector3 direction = (target.position - transform.position).normalized;
                transform.position += direction * moveSpeed * Time.deltaTime;
            }
            else
            {
                // 平滑移动 (Lerp)
                transform.position = Vector3.Lerp(transform.position, target.position, moveSpeed * Time.deltaTime);
            }

            // 如果存在关注点，则始终让相机看着关注点
            if (focusPoint != null)
            {
                Quaternion focusRotation = Quaternion.LookRotation(focusPoint.position - transform.position);
                transform.rotation = Quaternion.Slerp(transform.rotation, focusRotation, rotationSpeed * Time.deltaTime);
            }
            else
            {
                // 否则根据目标位置旋转摄像机
                Quaternion targetRotation = Quaternion.LookRotation(target.position - transform.position);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            // 判断是否已经接近目标位置，停止移动
            if (Vector3.Distance(transform.position, target.position) < distanceThreshold)
            {
                shouldMove = false; // 停止移动
            }
        }
    }
}
