using UnityEngine;

namespace OutpostZero
{
    /// <summary>High rear camera like Last War's runner view, with optional screen shake.</summary>
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        // Raised/pulled back a bit and the look-at point lifted (see lookHeight) from the pre-2.5x-scale
        // values so the taller rigged hero (RookAvatar.GameplayScale) keeps full headroom in frame.
        public Vector3 offset = new Vector3(0, 16.5f, -16.8f);
        public float lookAhead = 6.5f;
        public float lookHeight = 1.1f;
        public float xFollow = 0.42f;
        float shake;
        Vector3 smooth;
        bool hasSmooth;

        public void Shake(float amount) { shake = Mathf.Max(shake, amount); }

        void LateUpdate()
        {
            if (!target) return;
            Vector3 t = target.position;
            Vector3 desired = new Vector3(t.x * xFollow, 0, t.z) + offset;
            if (!hasSmooth) { smooth = desired; hasSmooth = true; }
            float k = 1f - Mathf.Exp(-6f * Time.deltaTime);
            smooth = Vector3.Lerp(smooth, desired, k);
            shake = Mathf.Max(0f, shake - Time.deltaTime * 2.2f);
            Vector3 jitter = shake > 0.01f ? Random.insideUnitSphere * shake : Vector3.zero;
            transform.position = smooth + jitter;
            transform.LookAt(new Vector3(Mathf.Lerp(smooth.x, t.x * 0.75f, 0.5f), lookHeight, t.z + lookAhead));
        }
    }
}
