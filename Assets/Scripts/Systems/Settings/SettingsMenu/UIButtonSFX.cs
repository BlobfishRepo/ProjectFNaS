using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FNaS.UI {
    [RequireComponent(typeof(Button))]
    public class UIButtonSFX : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler {
        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip hoverClip;
        [SerializeField] private AudioClip clickClip;

        [Header("Tuning")]
        [SerializeField][Range(0f, 1f)] private float hoverVolume = 1f;
        [SerializeField][Range(0f, 1f)] private float clickVolume = 1f;
        [SerializeField] private bool onlyWhenInteractable = true;

        private Button button;

        private void Awake() {
            button = GetComponent<Button>();

            if (audioSource == null) {
                audioSource = FindFirstObjectByType<AudioSource>();
            }
        }

        public void OnPointerEnter(PointerEventData eventData) {
            if (!CanPlay()) return;
            PlayClip(hoverClip, hoverVolume);
        }

        public void OnPointerClick(PointerEventData eventData) {
            if (!CanPlay()) return;
            PlayClip(clickClip, clickVolume);
        }

        private bool CanPlay() {
            if (audioSource == null) return false;
            if (onlyWhenInteractable && button != null && !button.IsInteractable()) return false;
            return true;
        }

        private void PlayClip(AudioClip clip, float volume) {
            if (clip == null) return;
            audioSource.PlayOneShot(clip, volume);
        }
    }
}