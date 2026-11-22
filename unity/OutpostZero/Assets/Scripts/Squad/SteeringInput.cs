using UnityEngine;

namespace OutpostZero
{
    /// <summary>Relative drag steering: touch or mouse drag left/right moves the squad's target X.
    /// Arrow keys / A-D also work in the editor. Uses the legacy Input Manager (default for new projects).</summary>
    public class SteeringInput : MonoBehaviour
    {
        public float sensitivity = 2.6f; // full screen-width drag = 2.6 road half-widths
        public float TargetX { get; set; }
        float halfWidth = 5f, startPointerX, startTarget; bool dragging;

        public void Init(float roadHalfWidth) { halfWidth = roadHalfWidth; TargetX = 0; }

        void Update()
        {
            if (GameManager.I == null || !GameManager.I.IsPlaying) { dragging = false; return; }
            bool down = false, held = false; float px = 0;
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0); px = t.position.x;
                down = t.phase == TouchPhase.Began; held = t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled;
            }
            else
            {
                px = Input.mousePosition.x; down = Input.GetMouseButtonDown(0); held = Input.GetMouseButton(0);
            }
            if (down && !GameUI.PointerOverButton(px)) { dragging = true; startPointerX = px; startTarget = TargetX; }
            if (!held) dragging = false;
            // Margin scales with Rook's visual size (RookAvatar.GameplayScale) so the steer target can't drag
            // him wider than his own on-screen body (including the gun-arm reach of the Run_and_Shoot clip -
            // see SquadController's matching edgeMargin) at the edge of the road.
            float edgeMargin = 1f * RookAvatar.GameplayScale;
            if (dragging)
            {
                float dx = (px - startPointerX) / Screen.width;
                TargetX = Mathf.Clamp(startTarget + dx * halfWidth * sensitivity, -halfWidth + edgeMargin, halfWidth - edgeMargin);
            }
            float k = Input.GetAxisRaw("Horizontal");
            if (Mathf.Abs(k) > 0.1f) TargetX = Mathf.Clamp(TargetX + k * 8f * Time.deltaTime, -halfWidth + edgeMargin, halfWidth - edgeMargin);
            if (Input.GetKeyDown(KeyCode.Space)) GetComponent<HeroRook>()?.ActivateShield();
        }
    }
}
