using UnityEngine;

namespace OutpostZero
{
    public struct HeroSlot
    {
        public string name, role, weapon, special;
        public bool playable;
    }

    /// <summary>Isolated preview stage. Its camera is not MainCamera, so the run world is not drawn.</summary>
    public class CharacterStage : MonoBehaviour
    {
        public const string PrefKey = "outpostzero.hero";
        public const int RingCount = 28;
        public static readonly HeroSlot[] Roster =
        {
            new HeroSlot { name = "ROOK", role = "COMMANDER", weapon = "ASSAULT RIFLE", special = "SHIELD WALL", playable = true },
            new HeroSlot { name = "SCOUT", role = "LOCKED", weapon = "—", special = "COMING SOON", playable = false },
            new HeroSlot { name = "HEAVY", role = "LOCKED", weapon = "—", special = "COMING SOON", playable = false }
        };

        Transform spin;
        Transform[] ringBits;
        GameObject model;
        RookAvatar preview;
        Camera stageCam;
        Camera mainCam;
        float yaw = 25f;
        float yawVel = 18f;
        bool dragging;

        public static int SavedIndex()
        {
            int i = PlayerPrefs.GetInt(PrefKey, 0);
            if (i < 0 || i >= Roster.Length || !Roster[i].playable) return 0;
            return i;
        }

        public static void Remember(int index)
        {
            PlayerPrefs.SetInt(PrefKey, index);
            PlayerPrefs.Save();
        }

        public void Build()
        {
            transform.position = new Vector3(0f, -500f, 0f);
            transform.rotation = Quaternion.identity;

            var camGo = new GameObject("StageCamera");
            camGo.tag = "Untagged";
            camGo.transform.SetParent(transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.25f, 4.4f);
            camGo.transform.LookAt(transform.position + new Vector3(0f, 1.05f, 0f));
            stageCam = camGo.AddComponent<Camera>();
            stageCam.clearFlags = CameraClearFlags.SolidColor;
            stageCam.backgroundColor = new Color(0.04f, 0.06f, 0.07f, 1f);
            stageCam.fieldOfView = 32f;
            stageCam.nearClipPlane = 0.1f;
            stageCam.farClipPlane = 25f;
            stageCam.depth = 20f;
            stageCam.enabled = false;

            // Soft panel behind the hero. Quad's visible face is -Z, and the camera sits on +Z, so yaw 180 points -Z at the camera.
            var rim = Prims.Part(PrimitiveType.Quad, transform, new Vector3(0f, 1.15f, -0.9f), new Vector3(2.4f, 2.6f, 1f), Prims.Unlit(new Color(0.1f, 0.38f, 0.42f)));
            rim.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var pad = Prims.Part(PrimitiveType.Cylinder, transform, new Vector3(0f, 0.08f, 0f), new Vector3(1.15f, 0.08f, 1.15f), Prims.Mat(new Color(0.16f, 0.17f, 0.19f), true));
            pad.transform.localRotation = Quaternion.identity;
            var top = Prims.Part(PrimitiveType.Cylinder, transform, new Vector3(0f, 0.17f, 0f), new Vector3(0.92f, 0.025f, 0.92f), Prims.Mat(Palette.TealDark, true));
            top.transform.localRotation = Quaternion.identity;

            var ringMat = Prims.Unlit(new Color(0.25f, 0.95f, 0.9f));
            ringBits = new Transform[RingCount];
            for (int i = 0; i < RingCount; i++)
            {
                float a = i * Mathf.PI * 2f / RingCount;
                var bit = Prims.Part(PrimitiveType.Cube, transform, new Vector3(Mathf.Cos(a) * 0.7f, 0.2f, Mathf.Sin(a) * 0.7f), new Vector3(0.07f, 0.05f, 0.07f), ringMat);
                ringBits[i] = bit.transform;
            }

            var pivot = new GameObject("Spin");
            spin = pivot.transform;
            spin.SetParent(transform, false);
            spin.localPosition = new Vector3(0f, 0.2f, 0f);
            gameObject.SetActive(false);
        }

        public void Show(int index)
        {
            gameObject.SetActive(true);
            if (mainCam == null) mainCam = Camera.main;
            if (mainCam != null) mainCam.enabled = false;
            if (stageCam != null) stageCam.enabled = true;
            if (model != null) Destroy(model);
            bool locked = index < 0 || index >= Roster.Length || !Roster[index].playable;
            var holder = new GameObject("Preview");
            model = holder;
            holder.transform.SetParent(spin, false);
            var built = RookAvatar.Build(holder.transform, locked);
            preview = locked ? null : built;
            // World +Z (WeaponVisual's default aim basis) points at the stage camera, not wherever the
            // spinning preview body is currently facing - aim the gun off the body root instead so it
            // doesn't swing independently of Rook as the turntable rotates.
            if (built.Weapon != null) built.Weapon.AimReference = built.Root;
            FrameHero(built.Root);
        }

        /// <summary>Places the stage camera so the hero (head to feet, including the weapon) fits in the portrait
        /// frame with margin, computed from the model's own bounds and the camera's FOV/aspect instead of a
        /// fixed distance - the fixed 4.4m distance used to crop tall phones' narrower vertical field of view.</summary>
        void FrameHero(Transform heroRoot)
        {
            if (stageCam == null || heroRoot == null) return;
            var savedYaw = spin.localRotation;
            spin.localRotation = Quaternion.identity;
            // CombinedBounds uses SkinnedMeshRenderer.localBounds (not .bounds, which can still be the stale
            // pre-skinning box this same frame) so this is safe to call immediately after the rigged Rook
            // is instantiated, before the animation graph has evaluated a single frame.
            var measured = Prims.CombinedBounds(heroRoot);
            spin.localRotation = savedYaw;
            if (measured == null) return;
            var bounds = measured.Value;

            // The preview keeps spinning after this runs, so use the farthest-corner distance from the spin
            // axis (not just this one orientation's half-width) as the horizontal radius: that bounds the
            // model at every yaw angle, including the arm holding a weapon out to one side.
            float cx = bounds.center.x - spin.position.x, cz = bounds.center.z - spin.position.z;
            float ex = bounds.extents.x, ez = bounds.extents.z;
            float radius = 0f;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    radius = Mathf.Max(radius, new Vector2(cx + sx * ex, cz + sz * ez).magnitude);

            const float margin = 1.2f; // headroom so the hero doesn't touch the frame edges
            float aspect = stageCam.aspect > 0.01f ? stageCam.aspect : (float)Screen.width / Mathf.Max(1, Screen.height);
            float vFov = stageCam.fieldOfView * Mathf.Deg2Rad;
            float hFov = 2f * Mathf.Atan(Mathf.Tan(vFov * 0.5f) * aspect);
            float distV = (bounds.extents.y * margin) / Mathf.Tan(vFov * 0.5f);
            float distH = (radius * margin) / Mathf.Tan(hFov * 0.5f);
            float dist = Mathf.Max(distV, distH) + radius;

            var camT = stageCam.transform;
            Vector3 focusWorld = new Vector3(spin.position.x, bounds.center.y, spin.position.z);
            Vector3 focusLocal = transform.InverseTransformPoint(focusWorld);
            camT.localPosition = new Vector3(focusLocal.x, focusLocal.y, focusLocal.z + dist);
            camT.LookAt(focusWorld);
        }

        public void Hide()
        {
            if (stageCam != null) stageCam.enabled = false;
            if (mainCam != null) mainCam.enabled = true;
            gameObject.SetActive(false);
        }

        public void BeginDrag() { dragging = true; yawVel = 0f; }
        public void Drag(float degrees) { yaw += degrees; yawVel = degrees / Mathf.Max(0.01f, Time.deltaTime); }
        public void EndDrag() { dragging = false; }

        void Update()
        {
            if (!isActiveAndEnabled) return;
            if (!dragging)
            {
                yaw += yawVel * Time.deltaTime;
                yawVel = Mathf.Lerp(yawVel, 18f, 1f - Mathf.Exp(-1.4f * Time.deltaTime));
            }
            if (spin != null) spin.localRotation = Quaternion.Euler(0f, yaw, 0f);
            if (preview != null) preview.Tick(Time.deltaTime, false);
            if (ringBits != null)
            {
                float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 2.2f);
                var s = new Vector3(0.07f * pulse, 0.05f * pulse, 0.07f * pulse);
                for (int i = 0; i < ringBits.Length; i++) if (ringBits[i] != null) ringBits[i].localScale = s;
            }
        }
    }
}
