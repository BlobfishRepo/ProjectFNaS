using System.Collections;
using UnityEngine;

public class ScreenFader : MonoBehaviour {
    public CanvasGroup group;
    public float fadeIn = 0.06f;
    public float hold = 0.04f;
    public float fadeOut = 0.10f;

    Coroutine co;

    public void Pulse() {
        if (group == null) return;
        if (co != null) StopCoroutine(co);
        co = StartCoroutine(PulseCo());
    }

    IEnumerator PulseCo() {
        yield return FadeTo(1f, fadeIn);
        if (hold > 0f) yield return new WaitForSeconds(hold);
        yield return FadeTo(0f, fadeOut);
        co = null;
    }

    IEnumerator FadeTo(float target, float dur) {
        float start = group.alpha;
        if (dur <= 0f) { group.alpha = target; yield break; }

        float t = 0f;
        while (t < dur) {
            t += Time.deltaTime;
            group.alpha = Mathf.Lerp(start, target, t / dur);
            yield return null;
        }
        group.alpha = target;
    }
}