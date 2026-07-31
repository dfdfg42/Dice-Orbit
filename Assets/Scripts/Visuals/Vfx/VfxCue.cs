using System;
using UnityEngine;
using DiceOrbit.Data.Tile;

namespace DiceOrbit.Visuals
{
    public enum VfxCuePlay { Burst, Looping }

    /// <summary>타일 이벤트 트리거 (구 TileVfxDatabase에서 이전 — 이름 보존).</summary>
    public enum TileVfxTrigger { OnTraverse, OnArrive, OnEndTurn }

    [Serializable]
    public class ShakePreset
    {
        public float amplitude = 0f;   // 0 = 쉐이크 없음
        public float duration = 0f;
    }

    /// <summary>큐 정의 — 태그 하나 = 프리팹 + 재생 방식 + 임팩트 피드백.</summary>
    [Serializable]
    public class VfxCue
    {
        public string tag;
        public VfxCuePlay play = VfxCuePlay.Burst;
        public GameObject prefab;
        public Vector3 offset;
        public float lifetime = 2f;    // Burst 전용
        public ShakePreset shake = new ShakePreset();
        public float hitStop = 0f;     // 초(realtime), 0 = 없음
        // public AudioClip sound;     // 예약 — 이번 미사용
    }

    /// <summary>타일 속성 VFX 엔트리 (구 TileVfxDatabase.TileAttributeEntry 이전).</summary>
    [Serializable]
    public class TileAttributeEntry
    {
        public TileAttributeType attributeType = TileAttributeType.None;
        public TileVfxTrigger trigger = TileVfxTrigger.OnTraverse;
        public GameObject prefab;
        public Vector3 offset = new Vector3(0f, 0.2f, 0f);
        public float lifetime = 2f;
    }
}
