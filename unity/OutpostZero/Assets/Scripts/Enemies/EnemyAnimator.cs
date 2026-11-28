using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

namespace OutpostZero
{
    /// <summary>Lightweight PlayableGraph clip player for zombies and the boss - the same crossfade-mixer
    /// recipe as HeroAnimator, minus the hip-pin/arm-lock corrections that only matter for a weapon-carrying
    /// rig. One instance per enemy body; Build() loads every clip under the model's own Resources folder
    /// once and wires one mixer input per clip.</summary>
    public class EnemyAnimator : MonoBehaviour
    {
        const float BlendSeconds = 0.18f;

        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable[] clipPlayables;
        AnimationClip[] clipList;
        float[] localTime;
        bool[] loopFlag;
        float[] weight;
        int activeIndex = -1;
        float lockUntil;

        /// <summary>Per-instance playback rate (1 = authored speed). Zombie crowds randomize this slightly
        /// so a group doesn't shamble in lockstep.</summary>
        public float Speed = 1f;

        /// <summary>Loads every clip under the given Resources path (e.g. "Models/Zombie/zombie") and wires
        /// one mixer input per clip. Returns false (nothing built) if the resource folder has no clips, so
        /// the caller can fall back to the primitive body.</summary>
        public bool Build(Animator animator, string resourcePath)
        {
            if (animator == null) return false;
            clipList = Resources.LoadAll<AnimationClip>(resourcePath);
            if (clipList == null || clipList.Length == 0) return false;

            graph = PlayableGraph.Create("EnemyAnimator");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(graph, "EnemyOutput", animator);
            mixer = AnimationMixerPlayable.Create(graph, clipList.Length);
            output.SetSourcePlayable(mixer);

            clipPlayables = new AnimationClipPlayable[clipList.Length];
            localTime = new float[clipList.Length];
            loopFlag = new bool[clipList.Length];
            weight = new float[clipList.Length];
            for (int i = 0; i < clipList.Length; i++)
            {
                clipPlayables[i] = AnimationClipPlayable.Create(graph, clipList[i]);
                clipPlayables[i].SetApplyFootIK(false);
                graph.Connect(clipPlayables[i], 0, mixer, i);
                mixer.SetInputWeight(i, 0f);
            }
            graph.Play();
            return true;
        }

        int FindIndex(string contains)
        {
            if (clipList == null) return -1;
            for (int i = 0; i < clipList.Length; i++)
                if (clipList[i].name.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0) return i;
            return -1;
        }

        /// <summary>Crossfades to the named clip (substring match, e.g. "Walking", "Charged_Ground_Slam") if
        /// it isn't already the active one. Ignored while a TriggerOnce one-shot is still playing. startTime
        /// seeds the clip's local time the first time it becomes active (mod'd against clip length in
        /// Update), letting a crowd of otherwise-identical bodies start their cycles at different phases.</summary>
        public void Play(string contains, bool loop, float startTime = 0f)
        {
            if (!graph.IsValid() || Time.time < lockUntil) return;
            int idx = FindIndex(contains);
            if (idx < 0 || idx == activeIndex) return;
            activeIndex = idx; loopFlag[idx] = loop; localTime[idx] = startTime;
        }

        /// <summary>Plays the named clip once, locking out Play() until it finishes.</summary>
        public void TriggerOnce(string contains)
        {
            if (!graph.IsValid()) return;
            int idx = FindIndex(contains);
            if (idx < 0) return;
            activeIndex = idx; loopFlag[idx] = false; localTime[idx] = 0f;
            lockUntil = Time.time + Mathf.Max(0.05f, clipList[idx].length);
        }

        void Update()
        {
            if (!graph.IsValid() || activeIndex < 0) return;
            float dt = Time.deltaTime;
            for (int i = 0; i < clipPlayables.Length; i++)
            {
                bool isActive = i == activeIndex;
                weight[i] = Mathf.MoveTowards(weight[i], isActive ? 1f : 0f, dt / BlendSeconds);
                mixer.SetInputWeight(i, weight[i]);
                if (weight[i] <= 0f && !isActive) continue;
                localTime[i] += dt * Speed;
                float len = Mathf.Max(0.0001f, clipList[i].length);
                float t = loopFlag[i] ? localTime[i] % len : Mathf.Min(localTime[i], len);
                clipPlayables[i].SetTime(t);
            }
        }

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
