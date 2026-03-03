using UnityEngine;

public class Door : MonoBehaviour {
    [Header("Motion")]
    public Transform hinge;
    public bool isOpen;                 // endpoint state (true = fully open)
    public float openYaw = 90f;
    public float speed = 12f;
    [Tooltip("How close (degrees) counts as fully open/closed.")]
    public float snapAngle = 0.75f;

    [Header("Audio (optional)")]
    public AudioSource sfxSource;
    public AudioClip openClip, closeClip;
    [Range(0f, 1f)] public float volume = 0.9f;

    public Transform interactPoint;     // optional

    Quaternion closedWorld, openWorld;
    bool wantOpen;                      // input intent (held?)
    bool moving;                        // are we currently traveling?
    bool movingToOpen;                  // travel direction while moving (latched)

    void Awake() {
        if (hinge == null) hinge = transform;

        if (sfxSource == null) {
            sfxSource = GetComponent<AudioSource>();
            if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
        }
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;

        closedWorld = hinge.rotation;
        openWorld = Quaternion.AngleAxis(openYaw, Vector3.up) * closedWorld;

        // start at endpoint
        hinge.rotation = isOpen ? openWorld : closedWorld;
        moving = false;
        movingToOpen = isOpen;
        wantOpen = isOpen;
    }

    // Hold-to-open API (DoorInteractor can keep calling SetOpen(true/false))
    public void SetOpen(bool open) {
        wantOpen = open;

        // If we're mid-swing, ignore reversals until endpoint.
        if (moving) return;

        // If at endpoint and intent differs, start moving (and play sound now).
        if (wantOpen != isOpen) StartMove(wantOpen);
    }

    void StartMove(bool toOpen) {
        moving = true;
        movingToOpen = toOpen;

        var clip = toOpen ? openClip : closeClip;
        if (clip != null && sfxSource != null) sfxSource.PlayOneShot(clip, volume);
    }

    void Update() {
        if (hinge == null) return;

        // If not moving, nothing to do (and we only start moving via SetOpen calls)
        if (!moving) return;

        var target = movingToOpen ? openWorld : closedWorld;
        float k = 1f - Mathf.Exp(-speed * Time.deltaTime);
        hinge.rotation = Quaternion.Slerp(hinge.rotation, target, k);

        if (Quaternion.Angle(hinge.rotation, target) <= snapAngle) {
            hinge.rotation = target;
            isOpen = movingToOpen;   // only changes at ends
            moving = false;

            // Now that we're at an endpoint, honor latest intent:
            // (This makes "release mid-open" finish opening, then close.)
            if (wantOpen != isOpen) StartMove(wantOpen);
        }
    }
}