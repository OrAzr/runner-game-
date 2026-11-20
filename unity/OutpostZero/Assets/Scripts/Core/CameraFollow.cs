using UnityEngine;

namespace OutpostZero
{
    /// <summary>High rear camera like Last War's runner view, with optional screen shake.</summary>
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0, 15f, -15.5f);
        public float lookAhead = 5f;
        public float xFollow = 0.35f;
        float shake;

        public void Shake(float amount) { shake = Mathf.Max(shake, amount); }

        void LateUpdate()
        {
            if (!target) return;
            Vector3 t = target.position;
            Vector3 jitter = Vector3.zero;
            if (shake > 0) { shake -= Time.deltaTime; jitter = Random.insideUnitSphere * shake; }
            transform.position = new Vector3(t.x * xFollow, 0, t.z) + offset + jitter;
            transform.LookAt(new Vector3(t.x * 0.3f, 0, t.z + lookAhead));
        }
    }
}
