using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ScrollbarAutoHide : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
    [Header("References")]
    [SerializeField] private CanvasGroup scrollbarCanvasGroup;
    [SerializeField] private ScrollRect scrollRect;

    [Header("Fade")]
    [SerializeField] private float visibleAlpha = 0.35f;
    [SerializeField] private float hiddenAlpha = 0f;
    [SerializeField] private float fadeInSeconds = 0.12f;
    [SerializeField] private float fadeOutSeconds = 0.35f;

    [Header("Scrolling")]
    [SerializeField] private float scrollVelocityThreshold = 5f;
    [SerializeField] private float lingerAfterScrollSeconds = 0.4f;

    private bool hovering;
    private float scrollLingerTimer;
    private float currentVelocity;

    private void Awake() {
        if (scrollRect == null) {
            scrollRect = GetComponent<ScrollRect>();
        }

        if (scrollbarCanvasGroup != null) {
            scrollbarCanvasGroup.alpha = hiddenAlpha;
        }
    }

    private void Update() {
        if (scrollbarCanvasGroup == null) return;

        bool scrolling = scrollRect != null &&
                         Mathf.Abs(scrollRect.velocity.y) > scrollVelocityThreshold;

        if (scrolling) {
            scrollLingerTimer = lingerAfterScrollSeconds;
        }
        else if (scrollLingerTimer > 0f) {
            scrollLingerTimer -= Time.unscaledDeltaTime;
        }

        bool shouldShow = hovering || scrollLingerTimer > 0f;
        float target = shouldShow ? visibleAlpha : hiddenAlpha;

        float fadeTime = target > scrollbarCanvasGroup.alpha
            ? fadeInSeconds
            : fadeOutSeconds;

        scrollbarCanvasGroup.alpha = Mathf.SmoothDamp(
            scrollbarCanvasGroup.alpha,
            target,
            ref currentVelocity,
            Mathf.Max(0.001f, fadeTime),
            Mathf.Infinity,
            Time.unscaledDeltaTime
        );
    }

    public void OnPointerEnter(PointerEventData eventData) {
        hovering = true;
    }

    public void OnPointerExit(PointerEventData eventData) {
        hovering = false;
    }
}