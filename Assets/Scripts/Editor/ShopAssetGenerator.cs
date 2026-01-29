using UnityEngine;
using UnityEditor;
using System.IO;
using DiceOrbit.Data.Items;
using DiceOrbit.Data.Dice;
using DiceOrbit.Data;

public class ShopAssetGenerator : EditorWindow
{
    [MenuItem("Tools/Dice Orbit/Generate Shop Assets")]
    public static void ShowWindow()
    {
        GetWindow<ShopAssetGenerator>("Shop Gen");
    }

    private void OnGUI()
    {
        if (GUILayout.Button("Generate Shop Assets"))
        {
            Generate();
        }
    }

    private static void Generate()
    {
        string basePath = "Assets/Resources/Data/Shop";
        if (!Directory.Exists(basePath)) Directory.CreateDirectory(basePath);

        // 1. Potions
        var healPotion = CreateAsset<PotionItem>(basePath + "/HealPotion.asset");
        healPotion.ItemName = "Health Potion";
        healPotion.EffectType = PotionEffectType.Heal;
        healPotion.Value = 50;
        healPotion.Price = 50;

        var movePotion = CreateAsset<PotionItem>(basePath + "/MovePotion.asset");
        movePotion.ItemName = "Speed Potion";
        movePotion.EffectType = PotionEffectType.Move;
        movePotion.Value = 3;
        movePotion.Price = 80;

        var defPotion = CreateAsset<PotionItem>(basePath + "/DefensePotion.asset");
        defPotion.ItemName = "Iron Skin Potion";
        defPotion.EffectType = PotionEffectType.Buff;
        defPotion.BuffType = StatusEffectType.DefenseUp;
        defPotion.Value = 5;
        defPotion.Duration = 3;
        defPotion.Price = 100;

        // 2. Dice Configs
        var oddDice = CreateAsset<DiceConfig>(basePath + "/OddDice.asset");
        oddDice.ConfigName = "Odd Dice Set";
        oddDice.OnlyOdd = true;
        oddDice.Price = 200;

        var evenDice = CreateAsset<DiceConfig>(basePath + "/EvenDice.asset");
        evenDice.ConfigName = "Even Dice Set";
        evenDice.OnlyEven = true;
        evenDice.Price = 200;

        var highDice = CreateAsset<DiceConfig>(basePath + "/HighDice.asset");
        highDice.ConfigName = "High Dice Set (4-6)";
        highDice.MinValue = 4;
        highDice.MaxValue = 6;
        highDice.Price = 300;

        AssetDatabase.SaveAssets();
        
        Debug.Log("Shop Assets Generated!");
    }

    private static T CreateAsset<T>(string path) where T : ScriptableObject
    {
        T asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }
}
