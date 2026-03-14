using UnityEngine;

public class WeakPoint : MonoBehaviour
{
    [Tooltip("이 부분이 약점(Core)입니까?")]
    public bool isCore = true;

    [Tooltip("약점 명중 시 데미지 배율")]
    public float damageMultiplier = 2.0f;
}
