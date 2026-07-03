using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Tile;
using System.Collections.Generic;
using DiceOrbit.Data.Monsters;

namespace DiceOrbit.Data.MonsterPresets.SamplePreset
{
    // ==========================================
    // 1. ���� ��ų ����
    // ==========================================
    /// <summary>
    /// ���Ͱ� ����� ��ų�Դϴ�. SkillData�� ��ӹ޽��ϴ�.
    /// �������� "AI Pattern" ���ǿ��� ������ �� �ֽ��ϴ�.
    /// </summary>
    [System.Serializable]
    public class SampleAttack : SkillData
    {
        [Header("Skill Settings")]
        [Tooltip("��ų ��� �� ���� ���ط�")]
        [SerializeField] private int damage = 10;

        /// <summary>
        /// �����ڿ����� ��ų�� �̸��� ������ �ʱ�ȭ�ؾ� �մϴ�.
        /// �̸� �����ϸ� ���� �� ���� ��� ������ ���Դϴ�.
        /// </summary>
        public SampleAttack()
        {
            skillName = "���� ����";
            description = "��󿡰� �������� ������ ���� ��ų�Դϴ�.";
        }

        /// <summary>
        /// ���� ��ų�� �ߵ��� �� ����Ǵ� �����Դϴ�.
        /// </summary>
        /// <param name="source">��ų�� ����ϴ� ��ü (����)</param>
        /// <param name="targetUnits">Ÿ���� �� ���ֵ� ����Ʈ</param>
        /// <param name="targetTiles">Ÿ���� �� Ÿ�ϵ� ����Ʈ</param>
        /// <param name="diceValue">��ų �ߵ� �� ���� �ֻ��� �� (�ʿ�� ���)</param>
        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            foreach (var target in targetUnits)
            {
                if (target == null || !target.IsAlive) continue;

                // 1. ���� ������ ���� CombatAction ����
                // (�̸�, Ÿ��, �Ű�����(damage ��))
                // 2. ���������ο� ������ CombatContext ����
                var context = new AttackContext(source, target, SkillName, damage);
                
                // 3. ���� ����������(CombatPipeline)�� ���� ó���� �䫊 (�нú� ���� �߰��� ������ �� ����)
                CombatPipeline.Instance?.Process(context);

                Debug.Log($"[{SkillName}] {source.name} attacks {target.name} for {damage} damage");
            }
        }
    }

    // ==========================================
    // 2. ���� ��� ȿ�� ����
    // ==========================================
    /// <summary>
    /// ���Ͱ� ������� �� �߻��ϴ� ȿ���Դϴ�. DeathEffect�� ��ӹ޽��ϴ�.
    /// </summary>
    [System.Serializable]
    public class SampleDeath : DeathEffect
    {
        public SampleDeath()
        {
            effectName = "Sample Death";
            description = "���Ͱ� ���� �� �ߵ��ϴ� ���� ȿ���Դϴ�.";
        }

        /// <summary>
        /// ��� ȿ���� �ߵ��Ǵ� �����Դϴ�.
        /// </summary>
        /// <param name="deadMonster">���� ���� ���� ��ü</param>
        public override void Execute(Monster deadMonster)
        {
            // ����: ���Ͱ� ���� �� �ʿ� ��ġ�� Ư�� Ÿ�� ȿ���� ��� ����ٰų� �Ʊ����� ������ �� �� �ֽ��ϴ�.
            Debug.Log($"[SampleDeath] {deadMonster.name} died! Executing death effect...");
        }
    }

    // ==========================================
    // 3. ���� �нú� ����
    // ==========================================
    /// <summary>
    /// ������ ���� �� �ڵ����� �ߵ��Ǵ� �нú��Դϴ�. PassiveAbility�� ��ӹ޽��ϴ�.
    /// �������� "Starting Passives" ���ǿ� �߰��� �� �ֽ��ϴ�.
    /// </summary>
    [System.Serializable]
    public class SamplePassive : PassiveAbility
    {
        [Header("Passive Settings")]
        [Tooltip("�� �� ȸ���� ü��")]
        [SerializeField] private int healAmount = 5;

        public SamplePassive()
        {
            passiveName = "���";
            description = "�� �� ���� �� ü���� ȸ���մϴ�.";
            
            // Priority(�켱����)�� �������� ���� Ÿ�ֿ̹� ������ �� ���� ����˴ϴ�.
            priority = 10; 
            
            // stackable�� false�� ��ø���� �ʽ��ϴ� (���� ȿ�� �Ұ�).
            isStackable = false;
        }

        /// <summary>
        /// ���� �߿� �߻��ϴ� ���� �̺�Ʈ(CombatTrigger) ��ȣ�� �����ϰ� �����մϴ�.
        /// </summary>
        public void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            // ���� 1) �ƶ��̳� �׼��� ��� ������ ����
            if (context == null) return;

            // ���� 2) ���� ����Ų(ourceUnit == owner) �̺�Ʈ�̸鼭,
            //         "�� ����" ������ �׼�(ActionType.OnStartTurn)�� "����Ǳ� ����"(OnPreAction)�� �� �����մϴ�.
            if (trigger == CombatTrigger.OnPreAction &&
                context.Phase == EventPhase.TurnStart &&
                context.SourceUnit == owner)
            {
                Debug.Log($"[SamplePassive] �� ���� Ʈ���� �ߵ� - �� ����");
                owner.Heal(healAmount);
            }
        }

        public override bool AllowSamePassive(IPassive incoming)
        {
            return false;
        }
    }
}
