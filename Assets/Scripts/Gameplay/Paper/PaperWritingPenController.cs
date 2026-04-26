using UnityEngine;

namespace FNaS.Systems {
    public class PaperWritingPenController : MonoBehaviour {
        [Header("References")]
        public PaperWritingStrokeDisplay display;
        public PaperWinProgress paperProgress;
        public Transform penRoot;
        public Transform penBody;
        public Transform penTip;
        public Transform cap;

        [Header("Desk Pose")]
        public bool cacheDeskPoseOnStart = true;
        public Vector3 deskRootLocalPosition;
        public Vector3 deskRootLocalEulerAngles;

        [Header("Pen Body Pose")]
        public Vector3 deskPenLocalPosition;
        public Vector3 deskPenLocalEulerAngles;
        public Vector3 writingPenLocalEulerAngles;

        [Header("Cap")]
        public Vector3 capOnLocalPosition;
        public Vector3 capOnLocalEulerAngles;
        public Vector3 capOffLocalPosition = new(0f, 0.03f, 0f);
        public Vector3 capOffLocalEulerAngles;

        [Header("Motion")]
        [Range(0.05f, 0.95f)] public float pickupCapPhase = 0.45f;
        public float capLerpSpeed = 16f;
        public float moveToWriteSpeed = 12f;
        public float moveBetweenLinesSpeed = 18f;
        public float returnSpeed = 10f;
        public float rotationSpeed = 12f;
        public Vector3 writingOffsetWorld = new(0f, 0.002f, 0f);

        [Header("Return Behavior")]
        public float capReturnStartDistance = 0.03f;
        public float capReturnLerpSpeed = 8f;

        [Header("Visibility")]
        public bool hidePenBodyWhenInactive = false;

        [Header("Debug")]
        public bool drawTargetGizmo;

        private int lastLineIndex = -1;
        private bool hasTarget;
        private Vector3 currentTarget;

        private void Start() {
            if (penRoot != null && cacheDeskPoseOnStart) {
                deskRootLocalPosition = penRoot.localPosition;
                deskRootLocalEulerAngles = penRoot.localEulerAngles;
            }

            if (penBody != null && cacheDeskPoseOnStart) {
                deskPenLocalPosition = penBody.localPosition;
                deskPenLocalEulerAngles = penBody.localEulerAngles;
            }

            if (cap != null && cacheDeskPoseOnStart) {
                capOnLocalPosition = cap.localPosition;
                capOnLocalEulerAngles = cap.localEulerAngles;
            }
        }

        private void Update() {
            if (display == null || paperProgress == null || penBody == null || penTip == null) return;

            bool active = paperProgress.IsInPickupDelay() || paperProgress.IsWritingActive();
            if (hidePenBodyWhenInactive) {
                penBody.gameObject.SetActive(active);
            }

            if (!active) {
                ReturnToDeskPose();
                return;
            }

            if (paperProgress.IsInPickupDelay()) {
                UpdatePickupDelayPose();
                return;
            }

            UpdateWritingPose();
        }

        private void UpdatePickupDelayPose() {
            float duration = Mathf.Max(0.0001f, paperProgress.GetPickupDelayDuration());
            float t = 1f - Mathf.Clamp01(paperProgress.GetPickupDelayRemaining() / duration);

            if (t < pickupCapPhase) {
                ApplyCapPose(0f, 1f, Mathf.InverseLerp(0f, pickupCapPhase, t), capLerpSpeed);
                MovePenBodyLocal(deskPenLocalPosition, deskPenLocalEulerAngles, returnSpeed, rotationSpeed);
                return;
            }

            ApplyCapPose(0f, 1f, 1f, capLerpSpeed);
            MoveTowardWriteTarget(moveToWriteSpeed * Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(pickupCapPhase, 1f, t)));
        }

        private void UpdateWritingPose() {
            ApplyCapPose(0f, 1f, 1f, capLerpSpeed);

            int currentLine = display.GetCurrentLineIndex();
            float moveSpeed = currentLine != lastLineIndex && lastLineIndex >= 0
                ? moveBetweenLinesSpeed
                : moveToWriteSpeed;

            MoveTowardWriteTarget(moveSpeed);
            lastLineIndex = currentLine;
        }

        private void MoveTowardWriteTarget(float moveSpeed) {
            Vector3 worldTarget = display.GetWorldPointAtCurrentRevealLengthOrStart() + writingOffsetWorld;
            currentTarget = worldTarget;
            hasTarget = true;

            Vector3 delta = worldTarget - penTip.position;
            penBody.position = Vector3.Lerp(penBody.position, penBody.position + delta, Time.deltaTime * moveSpeed);
            penBody.localRotation = Quaternion.Slerp(
                penBody.localRotation,
                Quaternion.Euler(writingPenLocalEulerAngles),
                Time.deltaTime * rotationSpeed
            );
        }

        private void ReturnToDeskPose() {
            lastLineIndex = -1;
            hasTarget = false;

            if (penRoot != null) {
                penRoot.localPosition = Vector3.Lerp(
                    penRoot.localPosition,
                    deskRootLocalPosition,
                    Time.deltaTime * returnSpeed
                );

                penRoot.localRotation = Quaternion.Slerp(
                    penRoot.localRotation,
                    Quaternion.Euler(deskRootLocalEulerAngles),
                    Time.deltaTime * rotationSpeed
                );
            }

            MovePenBodyLocal(deskPenLocalPosition, deskPenLocalEulerAngles, returnSpeed, rotationSpeed);

            float penHomeDistance = Vector3.Distance(penBody.localPosition, deskPenLocalPosition);

            if (penHomeDistance <= capReturnStartDistance) {
                ApplyCapPose(1f, 0f, 1f, capReturnLerpSpeed);
            }
            else {
                ApplyCapPose(0f, 1f, 1f, capLerpSpeed);
            }
        }

        private void MovePenBodyLocal(Vector3 targetLocalPos, Vector3 targetLocalEuler, float posSpeed, float rotSpeed) {
            penBody.localPosition = Vector3.Lerp(penBody.localPosition, targetLocalPos, Time.deltaTime * posSpeed);
            penBody.localRotation = Quaternion.Slerp(
                penBody.localRotation,
                Quaternion.Euler(targetLocalEuler),
                Time.deltaTime * rotSpeed
            );
        }

        private void ApplyCapPose(float fromOff, float toOff, float t, float speed) {
            if (cap == null) return;

            float off = Mathf.Lerp(fromOff, toOff, t);
            Vector3 targetPos = Vector3.Lerp(capOnLocalPosition, capOffLocalPosition, off);
            Quaternion targetRot = Quaternion.Slerp(
                Quaternion.Euler(capOnLocalEulerAngles),
                Quaternion.Euler(capOffLocalEulerAngles),
                off
            );

            cap.localPosition = Vector3.Lerp(cap.localPosition, targetPos, Time.deltaTime * speed);
            cap.localRotation = Quaternion.Slerp(cap.localRotation, targetRot, Time.deltaTime * speed);
        }

        private void OnDrawGizmosSelected() {
            if (!drawTargetGizmo || !hasTarget) return;
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(currentTarget, 0.01f);
        }
    }
}