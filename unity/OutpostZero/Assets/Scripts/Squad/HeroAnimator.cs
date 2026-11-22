using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

namespace OutpostZero
{
    /// <summary>Drives the rigged Rook's clips with a hand-rolled PlayableGraph instead of an authored
    /// AnimatorController asset. Every clip gets its own fixed mixer input created once in Build(); Play()
    /// just retargets which input is trending to weight 1, and Update() advances each active/fading slot's
    /// own time (looping it manually, since looping is not guaranteed to survive import) and crossfades the
    /// weights with a simple MoveTowards.</summary>
    public class HeroAnimator : MonoBehaviour
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

        /// <summary>Loads every clip under Resources/Models/Hero/rook and wires one mixer input per clip.
        /// Returns false (nothing built) if the resource folder has no clips, so the caller can skip this
        /// component entirely.</summary>
        public bool Build(Animator animator)
        {
            if (animator == null) return false;
            clipList = Resources.LoadAll<AnimationClip>("Models/Hero/rook");
            if (clipList == null || clipList.Length == 0) return false;

            graph = PlayableGraph.Create("RookAnimator");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(graph, "RookOutput", animator);
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

        /// <summary>Crossfades to the named clip (substring match, e.g. "Idle", "Run_and_Shoot") if it isn't
        /// already the active one. Ignored while a TriggerOnce one-shot is still playing.</summary>
        public void Play(string contains, bool loop)
        {
            if (!graph.IsValid() || Time.time < lockUntil) return;
            int idx = FindIndex(contains);
            if (idx < 0 || idx == activeIndex) return;
            activeIndex = idx; loopFlag[idx] = loop; localTime[idx] = 0f;
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
                localTime[i] += dt;
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
