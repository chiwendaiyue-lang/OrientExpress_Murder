using TMPro;
using UnityEngine;

/// <summary>让 TMP 文本末尾若干字符按固定间隔闪烁（用于「有猫腻....」最后一个点）。</summary>
[DisallowMultipleComponent]
public class BlinkingTextTail : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField, Min(1)] private int tailLength = 1;
    [SerializeField, Min(0.05f)] private float interval = 0.45f;

    private string baseText = string.Empty;

    public void Configure(string text, int tailLen, float blinkInterval)
    {
        if (!string.IsNullOrEmpty(text))
        {
            baseText = text;
            if (label != null)
            {
                label.text = text;
            }
        }

        tailLength = Mathf.Max(1, tailLen);
        interval = Mathf.Max(0.05f, blinkInterval);
    }

    private void Awake()
    {
        if (label == null)
        {
            label = GetComponent<TMP_Text>();
        }

        if (label != null)
        {
            baseText = label.text;
        }
    }

    private void OnEnable()
    {
        if (label != null && string.IsNullOrEmpty(baseText))
        {
            baseText = label.text;
        }
    }

    private void Update()
    {
        if (label == null || string.IsNullOrEmpty(baseText) || tailLength <= 0)
        {
            return;
        }

        int split = Mathf.Clamp(tailLength, 1, baseText.Length);
        string head = baseText.Substring(0, baseText.Length - split);
        string tail = baseText.Substring(baseText.Length - split);
        bool visible = (Mathf.FloorToInt(Time.unscaledTime / interval) % 2) == 0;
        label.text = visible ? head + tail : head + tail.Substring(0, tail.Length - 1);
    }
}
