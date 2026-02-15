using UnityEngine;
using DiceOrbit.Core;

namespace DiceOrbit.Data.Passives
{
    /// <summary>
    /// 패시브 생성 및 설정을 위한 전략 클래스 (Strategy Pattern)
    /// 인스펙터에서 [SerializeReference]를 통해 다양한 설정 타입을 선택할 수 있습니다.
    /// </summary>
    [System.Serializable]
    public abstract class PassiveConfig
    {
        /// <summary>
        /// 런타임에 사용할 패시브 인스턴스를 생성하고 초기화하여 반환합니다.
        /// </summary>
        public abstract PassiveAbility CreateRuntimePassive(Unit owner);
    }

    /// <summary>
    /// 별도의 파라미터 없이 패시브 에셋을 그대로 사용하는 기본 설정
    /// </summary>
    [System.Serializable]
    public class SimplePassiveConfig : PassiveConfig
    {
        [Tooltip("적용할 패시브 에셋 원본")]
        public PassiveAbility passiveAsset;

        public override PassiveAbility CreateRuntimePassive(Unit owner)
        {
            if (passiveAsset == null) return null;

            // ScriptableObject 인스턴스화 (복제)
            var instance = Object.Instantiate(passiveAsset);
            instance.name = passiveAsset.name;

            // 초기화
            instance.Initialize(owner);

            return instance;
        }
    }

    /// <summary>
    /// 정수값 파라미터 하나를 설정할 수 있는 패시브 설정
    /// </summary>
    [System.Serializable]
    public class IntValuePassiveConfig : PassiveConfig
    {
        [Tooltip("적용할 패시브 에셋 원본")]
        public PassiveAbility passiveAsset;
        [Tooltip("전달할 정수 값 (예: 스탯 증가량, 지속시간 등)")]
        public int initialValue;

        public override PassiveAbility CreateRuntimePassive(Unit owner)
        {
            if (passiveAsset == null) return null;

            var instance = Object.Instantiate(passiveAsset);
            instance.name = passiveAsset.name;

            instance.Initialize(owner);

            // 인터페이스를 통한 값 주입
            if (instance is IIntValueReceiver receiver)
            {
                receiver.SetValue(initialValue);
            }
            else
            {
                Debug.LogWarning($"[IntValuePassiveConfig] {passiveAsset.name} does not implement IIntValueReceiver.");
            }

            return instance;
        }
    }

    /// <summary>
    /// 정수값 파라미터 두 개를 설정할 수 있는 패시브 설정
    /// </summary>
    [System.Serializable]
    public class TwoIntValuesPassiveConfig : PassiveConfig
    {
        [Tooltip("적용할 패시브 에셋 원본")]
        public PassiveAbility passiveAsset;
        [Tooltip("전달할 첫 번째 정수 값")]
        public int value1;
        [Tooltip("전달할 두 번째 정수 값")]
        public int value2;

        public override PassiveAbility CreateRuntimePassive(Unit owner)
        {
            if (passiveAsset == null) return null;

            var instance = Object.Instantiate(passiveAsset);
            instance.name = passiveAsset.name;

            instance.Initialize(owner);

            if (instance is ITwoIntValuesReceiver receiver)
            {
                receiver.SetValues(value1, value2);
            }
            else
            {
                Debug.LogWarning($"[TwoIntValuesPassiveConfig] {passiveAsset.name} does not implement ITwoIntValuesReceiver.");
            }

            return instance;
        }
    }
}
