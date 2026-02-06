using UnityEngine;

/// <summary>
/// Класс для хранения данных одного Brainrot объекта
/// </summary>
[System.Serializable]
public class BrainrotData
{
    public string name;
    public int rarity;
    public int income;
    public int level;
    public int slotID;
    
    public BrainrotData()
    {
        name = "";
        rarity = 1;
        income = 0;
        level = 1;
        slotID = -1;
    }
    
    public BrainrotData(string name, int rarity, int income, int level, int slotID)
    {
        this.name = name;
        this.rarity = rarity;
        this.income = income;
        this.level = level;
        this.slotID = slotID;
    }
}
