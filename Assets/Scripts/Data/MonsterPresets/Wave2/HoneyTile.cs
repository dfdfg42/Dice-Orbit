using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;
using DiceOrbit.UI;
using UnityEngine;

namespace DiceOrbit.Data.Tile
{
    /// <summary>
    /// Ÿ�Ͽ� �ο��Ǵ� Ư���� �Ӽ�(����, ����, ��� ��)�� �����ϴ� ���� Ŭ�����Դϴ�.
    /// TileAttribute�� ��ӹ�����, ĳ���Ͱ� Ÿ���� ��ų� ������ ���� �̺�Ʈ�� ó���մϴ�.
    /// </summary>
    public class HoneyTileAttribute : TileAttribute
    {
        /// <summary>
        /// �Ӽ� ������. (���� �нú곪 ��ų�� ���� Ÿ�Ͽ� ������ �� ȣ��˴ϴ�)
        /// </summary>
        /// <param name="type">�Ӽ��� Ÿ��(enum)</param>
        /// <param name="value">�������� �� ���� ��ġ (�⺻�� ���� ����)</param>
        /// <param name="duration">����� ���ӵǴ� �� �� (-1�̸� ����)</param>
        /// <param name="isStackable">���� Ÿ�Ͽ� ���� �� �ߺ� ��ġ ��������</param>
        public HoneyTileAttribute(TileAttributeType type, int value, int duration, bool isStackable = false) 
            : base(type, value, duration, isStackable)
        {
        }

        /// <summary>
        /// ĳ���Ͱ� �� Ÿ���� ������ �� �ߵ��մϴ�. (���� ���� ����)
        /// </summary>
        /// <param name="character">�� Ÿ���� ���� ĳ���� ��ü</param>
        public override void OnTraverse(Character character)
        {
            // ����: ��� ���� ȿ�� �ߵ��� ���� Activate ȣ��
            Activate(character);
        }

        /// <summary>
        /// ĳ���Ͱ� �̵��� ��ġ�� �� Ÿ�� ������ ���� ������ �� �ߵ��մϴ�.
        /// </summary>
        public override void OnEndTurn(Character character)
        {
            // ����: ���� ������ ���� �� �� �� �ߵ��� ��� ���
            Activate(character);
        }

        /// <summary>
        /// ���� ȿ���� ó���ϴ� Ŀ���� ���� (���� �ֱ�, ���� �ο� ��)
        /// </summary>
        public void Activate(Character target)
        {
            // Ÿ���� ���� �����̰ų� ������ ����
            if (target == null || !target.IsAlive) return;

            //TODO: �̵��� ���� �� ����� ���� ����
            Debug.Log($"[HoneyTileAttribute] {target.name} ������ �� Ÿ���� ��� �����������ϴ�!");
            target.StatusEffects.AddEffect(new DiceOrbit.Systems.Effects.HoneyDebuff(1, 1));
            var statusData = TooltipKeywordFormatter.BuildStatusDisplayData(
                DiceOrbit.Data.EffectType.Honey.ToString(), 1, 1);
            CombatNotifier.NotifyStatus(target, statusData.Name, statusData.Color);
            Owner.RemoveAttribute(this);
        }

        /// <summary>
        /// UI�� �������� Ÿ���� ������ ������ �� ȣ��Ǵ� ���ڿ��Դϴ�.
        /// </summary>
        public override string GetDescription()
        {
            string durationText = Duration < 0 ? "����" : $"{Duration}��";
            return $"����� �̵����� {Value} �����մϴ�";
        }
    }
}

namespace DiceOrbit.Systems.Effects
{
    /// <summary>
    /// ���ݷ� ���� (������ ��� �� �߰�)
    /// </summary>
    public class HoneyDebuff : StatusEffect
    {
        public HoneyDebuff(int value, int duration) : base(EffectType.Honey, value, duration)
        {
            IsStackable = false;
        }


        public override void EffectApplied()
        {
            if (Owner.Stats is CharacterStats c)
            {
                c.MoveBuff -= Value;
            }
        }

        public override void EffectExpired()
        {
            if (Owner.Stats is CharacterStats c)
            {
                c.MoveBuff += Value;
            }
        }
    }
}