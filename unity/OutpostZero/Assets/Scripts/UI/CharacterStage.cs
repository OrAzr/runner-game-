using UnityEngine;

namespace OutpostZero
{
    public struct HeroSlot
    {
        public string name, role, weapon, special;
        public bool playable;
    }

    /// <summary>Lit pedestal and a Rook built with RookAvatar. Spin is driven by the select screen.</summary>
    public class CharacterStage : MonoBehaviour
    {
        public const string PrefKey = "outpostzero.hero";
        public static readonly HeroSlot[] Roster =
        {
            new HeroSlot { name = "ROOK", role = "COMMANDER", weapon = "ASSAULT RIFLE", special = "SHIELD WALL", playable = true },
            new HeroSlot { name = "SCOUT", role = "LOCKED", weapon = "—", special = "COMING SOON", playable = false },
            new HeroSlot { name = "HEAVY", role = "LOCKED", weapon = "—", special = "COMING SOON", playable = false }
        };

        Transform spin, backdrop, rim, ring;
        GameObject model;
        RookAvatar preview;
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
            var back = Prims.Part(PrimitiveType.Quad, transform, new Vector3(0f, 1.2f, -2.4f), new Vector3(48f, 64f, 1f), Prims.Unlit(new Color(0.04f, 0.06f, 0.07f)));
            backdrop = back.transform;
            var glow = Prims.Part(PrimitiveType.Quad, transform, new Vector3(0f, 1.35f, -1.15f), new Vector3(4.2f, 5.4f, 1f), Prims.Unlit(new Color(0.1f, 0.42f, 0.45f)));
            rim = glow.transform;
            var pad = Prims.Part(PrimitiveType.Cylinder, transform, new Vector3(0f, 0.08f, 0f), new Vector3(1.35f, 0.08f, 1.35f), Prims.Mat(Palette.Gunmetal));
            pad.transform.localRotation = Quaternion.identity;
            var trim = Prims.Part(PrimitiveType.Cylinder, transform, new Vector3(0f, 0.18f, 0f), new Vector3(0.95f, 0.03f, 0.95f), Prims.Mat(Palette.TealDark));
            trim.transform.localRotation = Quaternion.identity;
            var lightRing = Prims.Part(PrimitiveType.Cylinder, transform, new Vector3(0f, 0.22f, 0f), new Vector3(1.2f, 0.015f, 1.2f), Prims.Unlit(new Color(0.25f, 0.95f, 0.9f)));
            lightRing.transform.localRotation = Quaternion.identity;
            ring = lightRing.transform;
            var pivot = new GameObject("Spin");
            spin = pivot.transform;
            spin.SetParent(transform, false);
            spin.localPosition = new Vector3(0f, 0.2f, 0f);
            gameObject.SetActive(false);
        }

        public void Show(int index)
        {
            gameObject.SetActive(true);
            if (model != null) Destroy(model);
            bool locked = index < 0 || index >= Roster.Length || !Roster[index].playable;
            var holder = new GameObject("Preview");
            model = holder;
            holder.transform.SetParent(spin, false);
            var built = RookAvatar.Build(holder.transform, locked);
            preview = locked ? null : built;
            Place();
            FaceBackdrop();
        }

        public void Hide() { gameObject.SetActive(false); }

        public void BeginDrag() { dragging = true; yawVel = 0f; }
        public void Drag(float degrees) { yaw += degrees; yawVel = degrees / Mathf.Max(0.01f, Time.deltaTime); }
        public void EndDrag() { dragging = false; }

        void Update()
        {
            if (!gameObject.activeSelf) return;
            if (!dragging)
            {
                yaw += yawVel * Time.deltaTime;
                yawVel = Mathf.Lerp(yawVel, 18f, 1f - Mathf.Exp(-1.4f * Time.deltaTime));
            }
            if (spin != null) spin.localRotation = Quaternion.Euler(0f, yaw, 0f);
            if (preview != null) preview.Tick(Time.deltaTime, false);
            if (ring != null)
            {
                float pulse = 1f + 0.035f * Mathf.Sin(Time.time * 2.2f);
                ring.localScale = new Vector3(1.2f * pulse, 0.015f, 1.2f * pulse);
            }
            Place();
            FaceBackdrop();
        }

        void Place()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 focus = cam.transform.position + cam.transform.forward * 7.2f;
            transform.position = focus + Vector3.down * 0.85f;
            transform.rotation = Quaternion.identity;
        }

        void FaceBackdrop()
        {
            Face(backdrop, 3.2f, 48f, 64f, 1.3f);
            Face(rim, 1.35f, 4.2f, 5.4f, 1.45f);
        }

        void Face(Transform quad, float behind, float width, float height, float lift)
        {
            if (quad == null) return;
            var cam = Camera.main;
            if (cam == null) return;
            quad.position = transform.position + cam.transform.forward * behind + Vector3.up * lift;
            Vector3 fromCam = quad.position - cam.transform.position;
            if (fromCam.sqrMagnitude < 0.001f) return;
            quad.rotation = Quaternion.LookRotation(fromCam, cam.transform.up);
            quad.localScale = new Vector3(width, height, 1f);
        }
    }
}
