using System.Collections;
using UnityEngine;

public class ScreenFader : MonoBehaviour {
    [Header("References")]
    public CanvasGroup group;

    [Header("Single Pulse")]
    public float fadeIn = 0.06f;
    public float hold = 0.04f;
    public float fadeOut = 0.10f;

    [Header("Pulse Train")]
    [Tooltip("Default gap between pulses in a repeated post-jumpscare effect.")]
    public float repeatGap = 0.12f;

    [Header("Jumpscare Pulse Override")]
    public bool useJumpscareTimings = true;

    public float jsFadeIn = 0.14f;
    public float jsHold = 0.14f;
    public float jsFadeOut = 0.28f;

    Coroutine co;

    public void Pulse() {
        if (group == null) return;
        StopCurrent();
        co = StartCoroutine(PulseCo());
    }

    public void PulseRepeated(int count, float gapSeconds = -1f) {
        if (group == null) return;
        StopCurrent();
        co = StartCoroutine(PulseRepeatedCo(count, gapSeconds));
    }

    public void StopAndClear() {
        StopCurrent();
        if (group != null) {
            group.alpha = 0f;
        }
    }

    private void StopCurrent() {
        if (co != null) {
            StopCoroutine(co);
            co = null;
        }
    }

    IEnumerator PulseCo() {
        yield return FadeTo(1f, fadeIn);

        if (hold > 0f) {
            yield return new WaitForSeconds(hold);
        }

        yield return FadeTo(0f, fadeOut);
        co = null;
    }

    IEnumerator PulseRepeatedCo(int count, float gapSeconds) {
        count = Mathf.Max(1, count);
        float gap = gapSeconds >= 0f ? gapSeconds : repeatGap;

        float fi = useJumpscareTimings ? jsFadeIn : fadeIn;
        float h = useJumpscareTimings ? jsHold : hold;
        float fo = useJumpscareTimings ? jsFadeOut : fadeOut;

        for (int i = 0; i < count; i++) {
            yield return FadeTo(1f, fi);

            if (h > 0f)
                yield return new WaitForSeconds(h);

            yield return FadeTo(0f, fo);

            if (i < count - 1 && gap > 0f)
                yield return new WaitForSeconds(gap);
        }

        co = null;
    }

    IEnumerator FadeTo(float target, float dur) {
        float start = group.alpha;

        if (dur <= 0f) {
            group.alpha = target;
            yield break;
        }

        float t = 0f;
        while (t < dur) {
            t += Time.deltaTime;
            group.alpha = Mathf.Lerp(start, target, t / dur);
            yield return null;
        }

        group.alpha = target;
    }
}