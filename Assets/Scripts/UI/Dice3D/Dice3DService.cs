using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Data;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 3D 주사위 뷰 풀 — 씬 밖(월드 아래 먼 곳)에 미니 스테이지들을 두고
    /// DiceElement가 Acquire/Release로 빌려 쓴다. 초기화 실패 시 조용히 2D로 폴백.
    /// 씬 오브젝트라 씬 전환 시 뷰/RT도 함께 정리된다.
    /// </summary>
    public class Dice3DService : MonoBehaviour
    {
        private static Dice3DService _instance;
        private static bool _failed;

        // 메인 카메라 프러스텀에 안 걸리게 월드 한참 아래. 셀 간격은 서로 안 비치게 넉넉히.
        private static readonly Vector3 StageBase = new Vector3(0f, -3000f, 0f);
        private const float CellSpacing = 30f;

        private readonly List<Dice3DView> pool = new List<Dice3DView>();

        public static Dice3DView Acquire(DiceData dice)
        {
            if (_failed || dice == null) return null;

            try
            {
                if (_instance == null)
                {
                    var go = new GameObject("Dice3DService");
                    go.transform.position = StageBase;
                    _instance = go.AddComponent<Dice3DService>();
                }
                return _instance.AcquireInternal(dice);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Dice3D] 3D 주사위 초기화 실패 — 2D 표시로 폴백: " + e.Message);
                _failed = true;
                return null;
            }
        }

        public static void Release(Dice3DView view)
        {
            if (view == null) return;
            view.OnRelease();
        }

        private Dice3DView AcquireInternal(DiceData dice)
        {
            Dice3DView view = null;
            foreach (var v in pool)
            {
                if (v != null && !v.gameObject.activeSelf) { view = v; break; }
            }
            if (view == null)
            {
                view = Dice3DView.Create(transform, new Vector3(pool.Count * CellSpacing, 0f, 0f));
                pool.Add(view);
            }

            view.OnAcquire(dice.Source?.Faces, dice.Value);
            return view;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
