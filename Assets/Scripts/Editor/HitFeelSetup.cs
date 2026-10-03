using System.IO;
using DiceOrbit.Visuals;
using UnityEditor;
using UnityEngine;

namespace DiceOrbit.EditorTools
{
    /// <summary>
    /// 타격감 프로필 생성·보수 (타격감 리워크 2026-10-03). 메뉴 [DiceOrbit → Create HitFeel Profile].
    /// - 프로필이 없으면 스펙 §4 기본값으로 만든다. 있으면 **빠진 것만** 채운다 (조정해 둔 수치는 건드리지 않는다).
    /// - 플래시 재질(Resources/Combat/HitFlash.mat)과 타격음(Assets/Sounds/Combat/*.wav) 배선.
    /// 기본값으로 완전히 되돌리려면 프로필 에셋을 지우고 다시 실행.
    /// </summary>
    public static class HitFeelSetup
    {
        private const string Dir = "Assets/Resources/Combat";
        private const string ProfilePath = Dir + "/HitFeelProfile.asset";
        private const string FlashMaterialPath = Dir + "/HitFlash.mat";
        private const string FlashShaderName = "DiceOrbit/SpriteSolidFlash";
        private const string SoundDir = "Assets/Sounds/Combat/";

        [MenuItem("DiceOrbit/Create HitFeel Profile")]
        public static void CreateFromMenu() => CreateOrRepair();

        public static HitFeelProfile CreateOrRepair()
        {
            if (!AssetDatabase.IsValidFolder(Dir))
            {
                Directory.CreateDirectory(Dir);
                AssetDatabase.Refresh();
            }

            var profile = AssetDatabase.LoadAssetAtPath<HitFeelProfile>(ProfilePath);
            bool created = profile == null;
            if (created)
            {
                profile = ScriptableObject.CreateInstance<HitFeelProfile>();
                profile.ResetTiersToDefaults();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            else
            {
                // 빠진 등급만 기본값으로 보충
                var defaults = ScriptableObject.CreateInstance<HitFeelProfile>();
                defaults.ResetTiersToDefaults();
                foreach (var feel in defaults.tiers)
                    if (!profile.TryGet(feel.tier, out _)) profile.tiers.Add(feel);
                Object.DestroyImmediate(defaults);
            }

            if (profile.flashMaterial == null) profile.flashMaterial = LoadOrCreateFlashMaterial();

            Assign(profile, HitTier.Tick, "hit_tick", 0.7f);
            Assign(profile, HitTier.Blocked, "hit_block", 0.8f);
            Assign(profile, HitTier.Light, "hit_light", 0.75f);
            Assign(profile, HitTier.Medium, "hit_medium", 0.9f);
            Assign(profile, HitTier.Heavy, "hit_heavy", 1f);
            Assign(profile, HitTier.Kill, "hit_kill", 1f);
            if (profile.characterLaunch.sfx == null) profile.characterLaunch.sfx = LoadClip("attack_swing");
            if (profile.monsterStrike.sfx == null) profile.monsterStrike.sfx = LoadClip("monster_strike");

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log($"[HitFeelSetup] 프로필 {(created ? "생성" : "보수")} 완료 — {ProfilePath}");
            return profile;
        }

        private static void Assign(HitFeelProfile profile, HitTier tier, string clipName, float volume)
        {
            var feel = profile.Get(tier);
            if (feel.sfx != null) return;   // 이미 배선된 소리는 그대로
            feel.sfx = LoadClip(clipName);
            feel.sfxVolume = volume;
        }

        private static AudioClip LoadClip(string name)
        {
            string path = SoundDir + name + ".wav";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) throw new System.InvalidOperationException($"[HitFeelSetup] 타격음 {path} 가 없다 — Tools/synth_hit_sfx.py로 합성해 넣을 것.");
            return clip;
        }

        private static Material LoadOrCreateFlashMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(FlashMaterialPath);
            if (material != null) return material;

            var shader = Shader.Find(FlashShaderName);
            if (shader == null) throw new System.InvalidOperationException($"[HitFeelSetup] 셰이더 {FlashShaderName} 를 찾을 수 없다 (Assets/Shaders/SpriteSolidFlash.shader).");
            material = new Material(shader) { name = "HitFlash" };
            AssetDatabase.CreateAsset(material, FlashMaterialPath);
            return material;
        }
    }
}
