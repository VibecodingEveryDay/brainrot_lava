using System.Collections.Generic;
using YG;

/// <summary>
/// Расширение класса SavesYG для хранения игровых данных
/// </summary>
namespace YG
{
    public partial class SavesYG
    {
        /// <summary>
        /// Баланс игрока (основное значение)
        /// </summary>
        public int balanceCount = 0;
        
        /// <summary>
        /// Множитель баланса (K, M, B, T и т.д.)
        /// </summary>
        public string balanceScaler = "";
        
        /// <summary>
        /// Список всех Brainrot объектов игрока
        /// </summary>
        public List<BrainrotData> Brainrots = new List<BrainrotData>();
        
        /// <summary>
        /// Уровень скорости игрока (начальный уровень 10)
        /// </summary>
        public int PlayerSpeedLevel = 10;
        
        /// <summary>
        /// Уровень скорости открытия клеток (Opening Multiplier)
        /// </summary>
        public int OpeningLevel = 0;
        
        /// <summary>
        /// Список размещенных брейнротов на панелях (placementID, brainrotName, level)
        /// </summary>
        public List<PlacementData> PlacedBrainrots = new List<PlacementData>();
        
        /// <summary>
        /// Список накопленного дохода на панелях (panelID, accumulatedBalance)
        /// </summary>
        public List<EarnPanelData> EarnPanelBalances = new List<EarnPanelData>();
        
        /// <summary>
        /// Уровень силы удара игрока
        /// </summary>
        public int AttackPowerLevel = 0;
        
        /// <summary>
        /// Текущий ультимейт игрока (название триггера анимации, например "IsStrongBeat1")
        /// </summary>
        public string CurrentUltimate = "IsStrongBeat1";
        
        /// <summary>
        /// Список ID разблокированных лестниц (1, 2 и т.д.)
        /// </summary>
        public List<int> UnlockedLadders = new List<int>();
    }
}

/// <summary>
/// Класс для хранения данных размещенного брейнрота на панели
/// </summary>
[System.Serializable]
public class PlacementData
{
    public int placementID;
    public string brainrotName;
    public int level;
    public string rarity; // Редкость брейнрота (Common, Rare, Epic и т.д.)
    public long baseIncome; // Базовый доход брейнрота
    
    public PlacementData()
    {
        placementID = -1;
        brainrotName = "";
        level = 1;
        rarity = "Common"; // Значение по умолчанию
        baseIncome = 0; // Значение по умолчанию
    }
    
    public PlacementData(int placementID, string brainrotName, int level)
    {
        this.placementID = placementID;
        this.brainrotName = brainrotName;
        this.level = level;
        this.rarity = "Common"; // Значение по умолчанию для обратной совместимости
        this.baseIncome = 0; // Значение по умолчанию для обратной совместимости
    }
    
    public PlacementData(int placementID, string brainrotName, int level, string rarity, long baseIncome)
    {
        this.placementID = placementID;
        this.brainrotName = brainrotName;
        this.level = level;
        this.rarity = rarity;
        this.baseIncome = baseIncome;
    }
}

/// <summary>
/// Класс для хранения данных накопленного дохода на панели
/// </summary>
[System.Serializable]
public class EarnPanelData
{
    public int panelID;
    public double accumulatedBalance;
    
    public EarnPanelData()
    {
        panelID = -1;
        accumulatedBalance = 0.0;
    }
    
    public EarnPanelData(int panelID, double accumulatedBalance)
    {
        this.panelID = panelID;
        this.accumulatedBalance = accumulatedBalance;
    }
}
