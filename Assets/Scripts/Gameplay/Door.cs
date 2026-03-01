using UnityEngine;

public class Door : MonoBehaviour {
    [Header("Motion")]
    public Transform hinge;
    public bool isOpen;           // keep public as requested
    public float openYaw = 90f;
    public float speed = 12f;

    [Header("Audio (optional)")]
    public AudioSource sfxSource;
    public AudioClip openClip;
    public AudioClip closeClip;
    [Range(0f, 1f)] public float volume = 0.9f;

    private Quaternion closedWorld;
    private Quaternion openWorld;

    void Awake() {
        if (hinge == null) hinge = transform;

        if (sfxSource == null) {
            sfxSource = GetComponent<AudioSource>();
            // If you prefer to add it manually in prefab, remove the next line.
            if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
        }
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;

        // cache world rotations
        closedWorld = hinge.rotation;
        openWorld = Quaternion.AngleAxis(openYaw, Vector3.up) * closedWorld;
    }

    public void SetOpen(bool open) {
        if (isOpen == open) return; // no change -> no sound

        isOpen = open;

        // play sound on change
        var clip = isOpen ? openClip : closeClip;
        if (clip != null && sfxSource != null) {
            sfxSource.PlayOneShot(clip, volume);
        }
    }

    public void Toggle() => SetOpen(!isOpen);

    void Update() {
        if (hinge == null) return;

        var target = isOpen ? openWorld : closedWorld;
        float k = 1f - Mathf.Exp(-speed * Time.deltaTime);
        hinge.rotation = Quaternion.Slerp(hinge.rotation, target, k);

        // Optional debug (kept inside the single Update)
        // if (Time.frameCount % 30 == 0) Debug.Log($"hinge world rot = {hinge.rotation.eulerAngles}", hinge);
    }
}