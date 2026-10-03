using System.Collections.Generic;
using UnityEngine;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 활성 <see cref="UnitHitReactor"/> 목록과 두 러너 — 변형을 프레임 맨 끝에 덧입히고 다음 프레임 맨 앞에 되돌린다.
    /// 실행 순서가 서로 달라야 해서(적용 = 모든 LateUpdate 뒤, 복원 = 모든 Update 앞) 컴포넌트가 둘이다.
    /// </summary>
    public static class HitReactorRunners
    {
        internal static readonly List<UnitHitReactor> Active = new List<UnitHitReactor>();
        private static GameObject _host;

        public static void Register(UnitHitReactor reactor)
        {
            if (reactor == null) return;
            EnsureHost();
            if (!Active.Contains(reactor)) Active.Add(reactor);
        }

        private static void EnsureHost()
        {
            if (_host != null) return;
            _host = new GameObject("HitReactorRunners");
            _host.AddComponent<HitReactorRestoreRunner>();
            _host.AddComponent<HitReactorApplyRunner>();
        }

        internal static void OnHostDestroyed()
        {
            for (int i = 0; i < Active.Count; i++)
                if (Active[i] != null) Active[i].RestoreVisual();
            Active.Clear();
            _host = null;
        }
    }

    /// <summary>프레임 맨 앞 — 지난 프레임에 덧입힌 변형을 되돌려 게임 로직이 원래 자세를 보게 한다.</summary>
    [DefaultExecutionOrder(-10000)]
    public class HitReactorRestoreRunner : MonoBehaviour
    {
        private void Update()
        {
            var active = HitReactorRunners.Active;
            for (int i = 0; i < active.Count; i++)
                if (active[i] != null) active[i].RestoreVisual();
        }

        private void OnDestroy() => HitReactorRunners.OnHostDestroyed();
    }

    /// <summary>프레임 맨 끝 — 타이머를 진행하고 변형을 덧입힌다 (빌보드 등 다른 LateUpdate가 끝난 뒤).</summary>
    [DefaultExecutionOrder(10000)]
    public class HitReactorApplyRunner : MonoBehaviour
    {
        private void LateUpdate()
        {
            var active = HitReactorRunners.Active;
            float dt = Time.deltaTime;   // scaled — 히트스톱 동안 얼어 있다
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var reactor = active[i];
                if (reactor == null) { active.RemoveAt(i); continue; }

                bool alive = reactor.Advance(dt);
                reactor.ApplyVisual();
                if (!alive)
                {
                    reactor.OnFinished();
                    active.RemoveAt(i);   // 이번 프레임에 덧입힌 것은 다음 프레임의 복원 러너가 못 본다 → 여기서 되돌린다
                    reactor.RestoreVisual();
                }
            }
        }
    }
}
