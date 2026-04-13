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

    bool manualHeld;                    // DoorInteractor
    bool traversalOpen;                 // waypoint traversal override

    bool moving;
    bool movingToOpen;

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

        hinge.rotation = isOpen ? openWorld : closedWorld;
        moving = false;
        movingToOpen = isOpen;
        manualHeld = false;
        traversalOpen = isOpen;
    }

    public void SetManualHeld(bool held) {
        manualHeld = held;
        RefreshWantedState();
    }

    public void SetTraversalOpen(bool open) {
        traversalOpen = open;
        RefreshWantedState();
    }

    void RefreshWantedState() {
        bool wantOpen = manualHeld || traversalOpen;

        // If mid-swing, ignore reversals until endpoint.
        if (moving) return;

        // If at endpoint and desired differs, begin movement.
        if (wantOpen != isOpen) StartMove(wantOpen);
    }

    void StartMove(bool toOpen) {
        moving = true;
        movingToOpen = toOpen;

        var clip = toOpen ? openClip : closeClip;
        if (clip != null && sfxSource != null)
            sfxSource.PlayOneShot(clip, volume);
    }

    void Update() {
        if (hinge == null || !moving) return;

        var target = movingToOpen ? openWorld : closedWorld;
        float k = 1f - Mathf.Exp(-speed * Time.deltaTime);
        hinge.rotation = Quaternion.Slerp(hinge.rotation, target, k);

        if (Quaternion.Angle(hinge.rotation, target) <= snapAngle) {
            hinge.rotation = target;
            isOpen = movingToOpen;
            moving = false;

            // Re-evaluate after reaching endpoint.
            RefreshWantedState();
        }
    }

    public float GetOpenFraction01() {
        if (hinge == null) return isOpen ? 1f : 0f;

        float currentYaw = Mathf.Abs(Mathf.DeltaAngle(
            closedWorld.eulerAngles.y,
            hinge.rotation.eulerAngles.y
        ));

        float maxYaw = Mathf.Abs(openYaw);
        if (maxYaw <= 0.001f) return isOpen ? 1f : 0f;

        return Mathf.Clamp01(currentYaw / maxYaw);
    }

    public bool IsOpenEnough(float threshold01 = 0.3f) {
        return GetOpenFraction01() >= Mathf.Clamp01(threshold01);
    }
}