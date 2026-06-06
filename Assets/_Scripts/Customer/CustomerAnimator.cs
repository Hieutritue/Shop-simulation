using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Random = UnityEngine.Random;

/// <summary>
/// Hệ thống animation cho Customer chạy bằng Playables API — KHÔNG cần AnimatorController.
/// Chỉ việc kéo clip vào từng action; mỗi action có thể chứa NHIỀU clip → random 1 mỗi lần phát.
/// Idle/Walk là loop nền (lái theo velocity); Pick là one-shot (phát xong tự quay về loop nền).
/// Đặt trên root Customer (hoặc bất kỳ đâu) — tự tìm Animator trong children (model đã rig + Avatar).
/// </summary>
public class CustomerAnimator : MonoBehaviour
{
    public enum Anim { IdleBrowse, IdleQueue, Walk, Pick }

    [Serializable]
    public class AnimSet
    {
        public Anim type;
        [Tooltip("Kéo nhiều clip vào đây — mỗi lần phát random 1 clip.")]
        public AnimationClip[] clips;
        [Tooltip("Bật cho Idle/Walk (loop nền). Tắt cho Pick (one-shot).")]
        public bool loop = true;
        [Range(0f, 1f)] public float crossfade = 0.15f;
    }

    [SerializeField]
    private List<AnimSet> _sets = new List<AnimSet>
    {
        new AnimSet { type = Anim.IdleBrowse, loop = true },
        new AnimSet { type = Anim.IdleQueue, loop = true },
        new AnimSet { type = Anim.Walk, loop = true },
        new AnimSet { type = Anim.Pick, loop = false },
    };

    private Animator _animator;
    private PlayableGraph _graph;
    private AnimationMixerPlayable _mixer;
    private readonly Dictionary<Anim, AnimSet> _map = new Dictionary<Anim, AnimSet>();

    private int _activePort;
    private AnimationClipPlayable _activeClip;
    private AnimationClipPlayable _incomingClip;
    private bool _blending;
    private float _blendT, _blendDur;

    private bool _hasCurrent;
    private Anim _current;
    private bool _currentIsLoop;
    private bool _oneShotActive;
    private Anim _pendingLoop = Anim.IdleBrowse;

    private void Awake()
    {
        _animator = GetComponentInChildren<Animator>();
        if (_animator == null)
            Debug.LogError("[CustomerAnimator] Không tìm thấy Animator trong children (model cần Animator + Avatar).");
        foreach (var s in _sets) if (s != null) _map[s.type] = s;
    }

    private void OnEnable()
    {
        if (_animator == null || _graph.IsValid()) return;
        _graph = PlayableGraph.Create("CustomerAnim_" + GetInstanceID());
        _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        _mixer = AnimationMixerPlayable.Create(_graph, 2);
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(_graph, "Anim", _animator);
        output.SetSourcePlayable(_mixer);
        _graph.Play();
        PlayLoop(Anim.IdleBrowse);
    }

    private void OnDisable()
    {
        if (_graph.IsValid()) _graph.Destroy();
    }

    // ───────── API cho CustomerAgent ─────────

    /// <summary>
    /// Lái loco: đang đi → Walk; đứng yên → idle theo ngữ cảnh (idleVariant).
    /// Gọi mỗi frame cũng được (không restart thừa).
    /// </summary>
    public void SetLocomotion(bool moving, Anim idleVariant)
    {
        _pendingLoop = moving ? Anim.Walk : idleVariant;
        if (!_oneShotActive) PlayLoop(_pendingLoop);
    }

    /// <summary>Phát one-shot (vd Pick). Xong tự quay về loop nền. Trả về độ dài clip (giây), 0 nếu không phát được.</summary>
    public float PlayOneShot(Anim type)
    {
        float len = PlayInternal(type);
        if (len > 0f) _oneShotActive = true;
        return len;
    }

    // ───────── Nội bộ ─────────

    private void PlayLoop(Anim type)
    {
        if (_hasCurrent && _current == type && _currentIsLoop) return; // đang loop đúng anim → khỏi restart
        PlayInternal(type);
    }

    private float PlayInternal(Anim type)
    {
        if (!_graph.IsValid()) return 0f;
        if (!_map.TryGetValue(type, out var set) || set.clips == null || set.clips.Length == 0) return 0f;
        AnimationClip clip = set.clips[Random.Range(0, set.clips.Length)];
        if (clip == null) return 0f;

        FinishBlendImmediate(); // snap blend dở (nếu có) trước khi mở blend mới

        int incomingPort = 1 - _activePort;
        var incoming = AnimationClipPlayable.Create(_graph, clip);
        incoming.SetApplyFootIK(false);
        _mixer.DisconnectInput(incomingPort);
        _graph.Connect(incoming, 0, _mixer, incomingPort);
        _mixer.SetInputWeight(incomingPort, 0f);

        _incomingClip = incoming;
        _blendDur = Mathf.Max(0.0001f, set.crossfade);
        _blendT = 0f;
        _blending = true;

        _current = type;
        _currentIsLoop = set.loop;
        _hasCurrent = true;
        return clip.length;
    }

    private void Update()
    {
        if (!_graph.IsValid()) return;

        if (_blending)
        {
            _blendT += Time.deltaTime;
            float w = Mathf.Clamp01(_blendT / _blendDur);
            int incomingPort = 1 - _activePort;
            _mixer.SetInputWeight(incomingPort, w);
            _mixer.SetInputWeight(_activePort, 1f - w);
            if (w >= 1f) FinishBlendImmediate();
        }

        if (!_hasCurrent || _blending || !_activeClip.IsValid()) return;

        AnimationClip c = _activeClip.GetAnimationClip();
        double len = c != null ? c.length : 0;
        if (len <= 0) return;
        double t = _activeClip.GetTime();

        if (_currentIsLoop)
        {
            if (t >= len) _activeClip.SetTime(t % len); // manual loop (phòng khi clip chưa bật Loop Time)
        }
        else if (t >= len) // one-shot xong → về loop nền
        {
            _oneShotActive = false;
            PlayLoop(_pendingLoop);
        }
    }

    private void FinishBlendImmediate()
    {
        if (!_blending) return;
        int incomingPort = 1 - _activePort;
        _mixer.SetInputWeight(incomingPort, 1f);
        _mixer.SetInputWeight(_activePort, 0f);
        if (_activeClip.IsValid())
        {
            _mixer.DisconnectInput(_activePort);
            _activeClip.Destroy();
        }
        _activeClip = _incomingClip;
        _activePort = incomingPort;
        _blending = false;
    }
}
