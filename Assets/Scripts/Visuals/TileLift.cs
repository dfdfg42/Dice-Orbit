using DiceOrbit.Data;
using UnityEngine;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 타일 비주얼을 트랜스폼 변경 없이 띄우는 고스트 리프트 공용 헬퍼.
    /// (TileData 트랜스폼은 캐릭터 배치/이동이 참조하므로 절대 건드리지 않는다)
    ///
    /// 사용처: IntentTileLiftEffect(몬스터 공격 타일), MovePathPreview(이동 목적지).
    /// SetHeight가 색 오버레이·속성 아이콘 버블도 함께 동승시킨다.
    /// </summary>
    public static class TileLift
    {
        /// <summary>
        /// 타일 리프트 시작. 원본 렌더러를 끄고 고스트를 만든다.
        /// 렌더러가 없거나 이미 다른 리프트가 잡고 있으면(renderer.enabled == false) null.
        /// </summary>
        public static LiftHandle Lift(TileData tile, Transform parent)
        {
            if (tile == null) return null;

            var renderer = tile.GetComponentInChildren<MeshRenderer>();
            var filter   = tile.GetComponentInChildren<MeshFilter>();
            if (renderer == null || filter == null || filter.sharedMesh == null) return null;
            if (!renderer.enabled) return null;   // 이미 다른 리프트가 소유 중

            var ghost = new GameObject("_TileLiftGhost");
            if (parent != null) ghost.transform.SetParent(parent, true);
            var src = filter.transform;
            ghost.transform.SetPositionAndRotation(src.position, src.rotation);
            ghost.transform.localScale = src.lossyScale;

            var mf = ghost.AddComponent<MeshFilter>();
            mf.sharedMesh = filter.sharedMesh;
            var mr = ghost.AddComponent<MeshRenderer>();
            mr.sharedMaterials = renderer.sharedMaterials;   // 현재 머티리얼(타입/하이라이트) 그대로
            mr.shadowCastingMode = renderer.shadowCastingMode;

            renderer.enabled = false;                        // 원본은 숨김 (트랜스폼 무변경)
            return new LiftHandle(tile, renderer, ghost, src.position);
        }

        /// <summary>리프트 1건의 핸들. SetHeight로 올리고 Release로 원상 복구.</summary>
        public sealed class LiftHandle
        {
            public TileData Tile { get; }
            private readonly MeshRenderer _original;
            private readonly GameObject _ghost;
            private readonly Vector3 _basePos;

            internal LiftHandle(TileData tile, MeshRenderer original, GameObject ghost, Vector3 basePos)
            {
                Tile = tile; _original = original; _ghost = ghost; _basePos = basePos;
            }

            /// <summary>고스트 + 색 오버레이 + 속성 아이콘 버블을 함께 지정 높이로.</summary>
            public void SetHeight(float height)
            {
                if (_ghost != null)
                    _ghost.transform.position = _basePos + Vector3.up * height;

                MonsterTileColorOverlayManager.Instance?.SetLiftOffset(Tile, height);
                UI.TileAttributeBubbleManager.Instance?.SetLiftOffset(Tile, height);
            }

            /// <summary>원본 렌더러 복구 + 고스트 제거 + 동승 요소 원위치.</summary>
            public void Release()
            {
                if (_original != null) _original.enabled = true;
                if (_ghost != null) Object.Destroy(_ghost);

                if (Tile != null)
                {
                    MonsterTileColorOverlayManager.Instance?.SetLiftOffset(Tile, 0f);
                    UI.TileAttributeBubbleManager.Instance?.SetLiftOffset(Tile, 0f);
                }
            }
        }
    }
}
