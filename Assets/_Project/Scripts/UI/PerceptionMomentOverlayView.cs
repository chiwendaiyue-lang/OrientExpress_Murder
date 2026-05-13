using System.Collections;
using UnityEngine;

/// <summary>
/// 挂在察觉演出预制体根或子物体上，用于自定义时长与 Animator 状态名。
/// </summary>
public class PerceptionMomentOverlayView : MonoBehaviour
{
    [SerializeField, Min(0.05f)] private float duration = 1f;
    [SerializeField] private Animator animator;
    [SerializeField] private string playTrigger = "Play";
    [SerializeField] private string stateName = "PerceptionMoment";

    public IEnumerator PlayRoutine()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0f);
            if (!string.IsNullOrEmpty(playTrigger))
            {
                animator.SetTrigger(playTrigger);
            }

            float elapsed = 0f;
            float maxWait = duration + 0.35f;
            while (elapsed < maxWait)
            {
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                if (!string.IsNullOrEmpty(stateName) && state.IsName(stateName) && state.normalizedTime >= 0.98f)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            yield break;
        }

        yield return new WaitForSecondsRealtime(duration);
    }
}
