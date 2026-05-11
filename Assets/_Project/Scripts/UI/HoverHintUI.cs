using TMPro;
using System.Collections;
using UnityEngine;

public class HoverHintUI : MonoBehaviour
{
    public static HoverHintUI Instance;

    [SerializeField] private TMP_Text hintText;
    [SerializeField] private string defaultHint = "";
    private Coroutine tempHintRoutine;
    private bool lockByTemporaryHint;

    void Awake()
    {
        Instance = this;
        HideHint();
    }

    public void ShowHint(string content)
    {
        if (hintText == null || lockByTemporaryHint)
        {
            return;
        }

        hintText.gameObject.SetActive(true);
        hintText.text = content;
    }

    public void HideHint()
    {
        if (hintText == null || lockByTemporaryHint)
        {
            return;
        }

        hintText.text = defaultHint;
        hintText.gameObject.SetActive(!string.IsNullOrEmpty(defaultHint));
    }

    public void ShowTemporaryHint(string content, float duration = 1.5f)
    {
        if (hintText == null || string.IsNullOrEmpty(content))
        {
            return;
        }

        if (tempHintRoutine != null)
        {
            StopCoroutine(tempHintRoutine);
        }
        tempHintRoutine = StartCoroutine(TemporaryHintRoutine(content, duration));
    }

    private IEnumerator TemporaryHintRoutine(string content, float duration)
    {
        lockByTemporaryHint = true;
        hintText.gameObject.SetActive(true);
        hintText.text = content;

        yield return new WaitForSeconds(Mathf.Max(0.1f, duration));

        lockByTemporaryHint = false;
        HideHint();
        tempHintRoutine = null;
    }
}
