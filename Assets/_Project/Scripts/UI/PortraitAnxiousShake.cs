using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 立绘名以 <c>_shake</c> 结尾时，在基准位姿上叠加抖动；单次展示最多持续 <see cref="maxShakeDuration"/> 秒。
/// </summary>
[DisallowMultipleComponent]
public class PortraitAnxiousShake : MonoBehaviour
{
    [SerializeField] private RectTransform target;
    [SerializeField, Min(0f)] private float positionAmplitude = 7f;
    [SerializeField, Min(0f)] private float rotationAmplitude = 2.2f;
    [SerializeField, Min(0.1f)] private float frequency = 24f;
    [SerializeField, Min(0.05f)] private float maxShakeDuration = 0.7f;

    private Coroutine shakeRoutine;
    private Vector2 baseAnchoredPosition;
    private float baseLocalRotationZ;
    private bool hasBase;

    public static bool IsShakePortraitName(string portraitName)
    {
        return !string.IsNullOrEmpty(portraitName)
            && portraitName.EndsWith("_shake", StringComparison.OrdinalIgnoreCase);
    }

    private void Awake()
    {
        if (target == null)
        {
            target = transform as RectTransform;
        }
    }

    public void CaptureBasePose()
    {
        if (target == null)
        {
            return;
        }

        baseAnchoredPosition = target.anchoredPosition;
        baseLocalRotationZ = target.localEulerAngles.z;
        hasBase = true;
    }

    public void SetShaking(bool enabled)
    {
        if (!enabled)
        {
            StopShaking();
            return;
        }

        if (target == null)
        {
            return;
        }

        if (!hasBase)
        {
            CaptureBasePose();
        }

        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            shakeRoutine = null;
        }

        shakeRoutine = StartCoroutine(ShakeRoutine());
    }

    public void ApplyPortrait(string portraitName)
    {
        SetShaking(IsShakePortraitName(portraitName));
    }

    private void StopShaking()
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            shakeRoutine = null;
        }

        RestoreBasePose();
    }

    private void RestoreBasePose()
    {
        if (target == null || !hasBase)
        {
            return;
        }

        target.anchoredPosition = baseAnchoredPosition;
        Vector3 euler = target.localEulerAngles;
        euler.z = baseLocalRotationZ;
        target.localEulerAngles = euler;
    }

    private IEnumerator ShakeRoutine()
    {
        float seedX = UnityEngine.Random.Range(0f, 100f);
        float seedY = UnityEngine.Random.Range(0f, 100f);
        float elapsed = 0f;

        while (elapsed < maxShakeDuration)
        {
            if (target == null)
            {
                shakeRoutine = null;
                yield break;
            }

            float t = Time.unscaledTime * frequency;
            float nx = Mathf.PerlinNoise(seedX, t) * 2f - 1f;
            float ny = Mathf.PerlinNoise(seedY, t + 17.3f) * 2f - 1f;
            float nr = Mathf.PerlinNoise(seedX + 31.7f, t + 8.9f) * 2f - 1f;

            target.anchoredPosition = baseAnchoredPosition + new Vector2(nx, ny) * positionAmplitude;
            Vector3 euler = target.localEulerAngles;
            euler.z = baseLocalRotationZ + nr * rotationAmplitude;
            target.localEulerAngles = euler;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        shakeRoutine = null;
        RestoreBasePose();
    }
}
