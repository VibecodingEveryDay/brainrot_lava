using System;
using System.Collections.Generic;
using UnityEngine;
using YG;

/// <summary>
/// Синглтон для управления игровыми данными через YG2 Storage
/// Все скрипты, которым требуются сохранённые данные игрока, должны брать данные из этого синглтона
/// </summary>
public class GameStorage : MonoBehaviour
{
    [Header("Dev Mode Settings")]
    [Tooltip("Режим разработчика - устанавливает стартовый баланс при старте")]
    [SerializeField] private bool devMode = false;
    
    [Tooltip("Стартовый баланс для режима разработчика")]
    [SerializeField] private long startBalance = 1000000;
    
    private static GameStorage _instance;
    
    public static GameStorage Instance
    {
        get
        {
            if (_instance == null)
            {
                // Сначала ищем любой GameStorage в сцене (в т.ч. дочерние объекты)
                _instance = FindFirstObjectByType<GameStorage>(FindObjectsInactive.Include);
                if (_instance == null)
                {
                    // Только если в сцене нет — создаём объект Storage
                    GameObject storageObject = new GameObject("Storage");
                    _instance = storageObject.AddComponent<GameStorage>();
                    DontDestroyOnLoad(storageObject);
                }
            }
            return _instance;
        }
    }
    
    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Подписываемся на событие загрузки данных YG2
            YG2.onGetSDKData += LoadData;
            
            // Загружаем данные при старте
            LoadData();
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        // DevMode: применяем стартовый баланс с задержкой, чтобы перезаписать данные после загрузки YG2
        if (devMode && startBalance > 0)
            StartCoroutine(ApplyDevModeBalanceDelayed());
    }
    
    private void OnDestroy()
    {
        // Отписываемся от события
        YG2.onGetSDKData -= LoadData;
    }
    
    /// <summary>
    /// Загружает данные из YG2.saves
    /// </summary>
    private void LoadData()
    {
        // Данные автоматически загружаются из YG2.saves
        // Этот метод вызывается при загрузке сохранений
        
        // Если включен DevMode, применяем стартовый баланс после загрузки данных
        if (devMode)
        {
            ApplyDevModeBalance();
        }
    }
    
    #region Balance Methods
    
    /// <summary>
    /// Получить текущий баланс (int) - для обратной совместимости
    /// </summary>
    public int GetBalance()
    {
        return YG2.saves.balanceCount;
    }
    
    /// <summary>
    /// Получить текущий баланс как double (с учётом множителя)
    /// </summary>
    public double GetBalanceDouble()
    {
        return ConvertBalanceToDouble(YG2.saves.balanceCount, YG2.saves.balanceScaler);
    }
    
    /// <summary>
    /// Получить значение баланса и множитель
    /// </summary>
    public void GetBalance(out int value, out string scaler)
    {
        value = YG2.saves.balanceCount;
        scaler = YG2.saves.balanceScaler ?? "";
    }
    
    /// <summary>
    /// Установить баланс
    /// </summary>
    public void SetBalance(int balance)
    {
        YG2.saves.balanceCount = balance;
        YG2.saves.balanceScaler = "";
    }
    
    /// <summary>
    /// Установить баланс с множителем
    /// </summary>
    public void SetBalance(int value, string scaler)
    {
        YG2.saves.balanceCount = value;
        YG2.saves.balanceScaler = scaler ?? "";
    }
    
    /// <summary>
    /// Добавить к балансу
    /// </summary>
    public void AddBalance(int amount)
    {
        AddBalanceWithScaler(amount, "");
    }
    
    /// <summary>
    /// Добавить к балансу (с поддержкой long для больших значений)
    /// </summary>
    public void AddBalanceLong(long amount)
    {
        // Конвертируем long в значение с множителем
        (int value, string scaler) = ConvertDoubleToBalance(amount);
        AddBalanceWithScaler(value, scaler);
    }
    
    /// <summary>
    /// Добавить к балансу (с поддержкой double для точных значений)
    /// </summary>
    public void AddBalanceDouble(double amount)
    {
        // Конвертируем double в значение с множителем
        (int value, string scaler) = ConvertDoubleToBalance(amount);
        AddBalanceWithScaler(value, scaler);
    }
    
    /// <summary>
    /// Вычесть из баланса (с поддержкой long для больших значений)
    /// </summary>
    public bool SubtractBalanceLong(long amount)
    {
        return SubtractBalanceDouble((double)amount);
    }
    
    /// <summary>
    /// Вычесть из баланса (с поддержкой double для точных значений)
    /// Работает напрямую с double для сохранения точности
    /// </summary>
    public bool SubtractBalanceDouble(double amount)
    {
        if (amount <= 0)
        {
            return false;
        }
        
        double currentBalance = GetBalanceDouble();
        
        if (currentBalance >= amount)
        {
            double newBalance = currentBalance - amount;
            
            // Если результат очень близок к нулю (из-за ошибок округления), устанавливаем 0
            if (newBalance < 0.0001)
            {
                newBalance = 0;
            }
            
            (int newValue, string newScaler) = ConvertDoubleToBalance(newBalance);
            YG2.saves.balanceCount = newValue;
            YG2.saves.balanceScaler = newScaler;
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// Конвертирует double в формат value + scaler (публичный метод для использования в других скриптах)
    /// </summary>
    public (int value, string scaler) ConvertDoubleToValueAndScaler(double amount)
    {
        return ConvertDoubleToBalance(amount);
    }
    
    /// <summary>
    /// Добавить к балансу с множителем
    /// </summary>
    public void AddBalanceWithScaler(int value, string scaler)
    {
        // Получаем текущий баланс как double
        double currentBalance = GetBalanceDouble();
        
        // Конвертируем добавляемое значение в double
        double amountToAdd = ConvertBalanceToDouble(value, scaler ?? "");
        
        // Складываем
        double newBalance = currentBalance + amountToAdd;
        
        // Конвертируем обратно в value + scaler
        (int newValue, string newScaler) = ConvertDoubleToBalance(newBalance);
        
        // Устанавливаем новый баланс
        YG2.saves.balanceCount = newValue;
        YG2.saves.balanceScaler = newScaler;
    }
    
    /// <summary>
    /// Вычесть из баланса
    /// </summary>
    public bool SubtractBalance(int amount)
    {
        return SubtractBalanceWithScaler(amount, "");
    }
    
    /// <summary>
    /// Вычесть из баланса с множителем
    /// </summary>
    public bool SubtractBalanceWithScaler(int value, string scaler)
    {
        double currentBalance = GetBalanceDouble();
        double amountToSubtract = ConvertBalanceToDouble(value, scaler ?? "");
        
        if (currentBalance >= amountToSubtract)
        {
            double newBalance = currentBalance - amountToSubtract;
            
            // Если результат очень близок к нулю (из-за ошибок округления), устанавливаем 0
            if (newBalance < 0.0001)
            {
                newBalance = 0;
            }
            
            (int newValue, string newScaler) = ConvertDoubleToBalance(newBalance);
            YG2.saves.balanceCount = newValue;
            YG2.saves.balanceScaler = newScaler;
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// Конвертирует баланс (value + scaler) в double
    /// </summary>
    private double ConvertBalanceToDouble(int value, string scaler)
    {
        double result = value;
        
        if (!string.IsNullOrEmpty(scaler))
        {
            scaler = scaler.ToUpper();
            
            switch (scaler)
            {
                case "K":
                    result *= 1000.0;
                    break;
                case "M":
                    result *= 1000000.0;
                    break;
                case "B":
                    result *= 1000000000.0;
                    break;
                case "T":
                    result *= 1000000000000.0;
                    break;
                case "QA": // Квадриллионы (10^15)
                    result *= 1000000000000000.0;
                    break;
                case "QI": // Квинтиллионы (10^18)
                    result *= 1000000000000000000.0;
                    break;
                case "SX": // Секстиллионы (10^21)
                    result *= 1000000000000000000000.0;
                    break;
                case "SP": // Септиллионы (10^24)
                    result *= 1000000000000000000000000.0;
                    break;
                case "OC": // Октиллионы (10^27)
                    result *= 1000000000000000000000000000.0;
                    break;
                case "NO": // Нониллионы (10^30)
                    result *= 1000000000000000000000000000000.0;
                    break;
                default:
                    // Пытаемся распарсить как число (например, "1.5M")
                    if (scaler.Length > 1)
                    {
                        char lastChar = scaler[scaler.Length - 1];
                        string numberPart = scaler.Substring(0, scaler.Length - 1);
                        
                        if (double.TryParse(numberPart, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double multiplier))
                        {
                            switch (lastChar)
                            {
                                case 'K':
                                    result *= multiplier * 1000.0;
                                    break;
                                case 'M':
                                    result *= multiplier * 1000000.0;
                                    break;
                                case 'B':
                                    result *= multiplier * 1000000000.0;
                                    break;
                                case 'T':
                                    result *= multiplier * 1000000000000.0;
                                    break;
                            }
                        }
                        // Проверяем двухсимвольные множители
                        if (scaler.Length >= 2)
                        {
                            string lastTwo = scaler.Substring(scaler.Length - 2);
                            string numberPart2 = scaler.Length > 2 ? scaler.Substring(0, scaler.Length - 2) : "";
                            
                            double mult = 1.0;
                            if (!string.IsNullOrEmpty(numberPart2))
                            {
                                if (double.TryParse(numberPart2, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double multiplier2))
                                {
                                    mult = multiplier2;
                                }
                                else
                                {
                                    // Если не удалось распарсить, пропускаем
                                    break;
                                }
                            }
                            
                            switch (lastTwo)
                            {
                                case "QA":
                                    result *= mult * 1000000000000000.0;
                                    break;
                                case "QI":
                                    result *= mult * 1000000000000000000.0;
                                    break;
                                case "SX":
                                    result *= mult * 1000000000000000000000.0;
                                    break;
                                case "SP":
                                    result *= mult * 1000000000000000000000000.0;
                                    break;
                                case "OC":
                                    result *= mult * 1000000000000000000000000000.0;
                                    break;
                                case "NO":
                                    result *= mult * 1000000000000000000000000000000.0;
                                    break;
                            }
                        }
                    }
                    break;
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Конвертирует double в баланс (value + scaler)
    /// Использует округление для сохранения точности при малых значениях
    /// </summary>
    private (int value, string scaler) ConvertDoubleToBalance(double balance)
    {
        if (balance <= 0)
        {
            return (0, "");
        }
        
        // Нониллионы (10^30)
        if (balance >= 1000000000000000000000000000000.0)
        {
            double nonillions = balance / 1000000000000000000000000000000.0;
            return ((int)Math.Round(nonillions), "NO");
        }
        // Октиллионы (10^27)
        else if (balance >= 1000000000000000000000000000.0)
        {
            double octillions = balance / 1000000000000000000000000000.0;
            return ((int)Math.Round(octillions), "OC");
        }
        // Септиллионы (10^24)
        else if (balance >= 1000000000000000000000000.0)
        {
            double septillions = balance / 1000000000000000000000000.0;
            return ((int)Math.Round(septillions), "SP");
        }
        // Секстиллионы (10^21)
        else if (balance >= 1000000000000000000000.0)
        {
            double sextillions = balance / 1000000000000000000000.0;
            return ((int)Math.Round(sextillions), "SX");
        }
        // Квинтиллионы (10^18)
        else if (balance >= 1000000000000000000.0)
        {
            double quintillions = balance / 1000000000000000000.0;
            return ((int)Math.Round(quintillions), "QI");
        }
        // Квадриллионы (10^15)
        else if (balance >= 1000000000000000.0)
        {
            double quadrillions = balance / 1000000000000000.0;
            return ((int)Math.Round(quadrillions), "QA");
        }
        // Триллионы (10^12)
        else if (balance >= 1000000000000.0)
        {
            double trillions = balance / 1000000000000.0;
            return ((int)Math.Round(trillions), "T");
        }
        // Миллиарды (10^9)
        else if (balance >= 1000000000.0)
        {
            double billions = balance / 1000000000.0;
            return ((int)Math.Round(billions), "B");
        }
        // Миллионы (10^6)
        else if (balance >= 1000000.0)
        {
            double millions = balance / 1000000.0;
            return ((int)Math.Round(millions), "M");
        }
        // Тысячи (10^3)
        else if (balance >= 1000.0)
        {
            double thousands = balance / 1000.0;
            return ((int)Math.Round(thousands), "K");
        }
        else
        {
            // Меньше тысячи - просто число
            return ((int)Math.Round(balance), "");
        }
    }
    
    /// <summary>
    /// Форматирует баланс в читаемый формат (600B, 1.5T и т.д.)
    /// </summary>
    public string FormatBalance()
    {
        return FormatBalance(GetBalanceDouble());
    }
    
    /// <summary>
    /// Форматирует баланс из double в читаемый формат (600B, 1.5T и т.д.)
    /// Целые числа отображаются без десятичных знаков
    /// </summary>
    public string FormatBalance(double balance)
    {
        if (balance <= 0)
        {
            return "0";
        }
        
        // Нониллионы (10^30)
        if (balance >= 1000000000000000000000000000000.0)
        {
            double nonillions = balance / 1000000000000000000000000000000.0;
            return FormatBalanceValue(nonillions, "NO");
        }
        // Октиллионы (10^27)
        else if (balance >= 1000000000000000000000000000.0)
        {
            double octillions = balance / 1000000000000000000000000000.0;
            return FormatBalanceValue(octillions, "OC");
        }
        // Септиллионы (10^24)
        else if (balance >= 1000000000000000000000000.0)
        {
            double septillions = balance / 1000000000000000000000000.0;
            return FormatBalanceValue(septillions, "SP");
        }
        // Секстиллионы (10^21)
        else if (balance >= 1000000000000000000000.0)
        {
            double sextillions = balance / 1000000000000000000000.0;
            return FormatBalanceValue(sextillions, "SX");
        }
        // Квинтиллионы (10^18)
        else if (balance >= 1000000000000000000.0)
        {
            double quintillions = balance / 1000000000000000000.0;
            return FormatBalanceValue(quintillions, "QI");
        }
        // Квадриллионы (10^15)
        else if (balance >= 1000000000000000.0)
        {
            double quadrillions = balance / 1000000000000000.0;
            return FormatBalanceValue(quadrillions, "QA");
        }
        // Триллионы (10^12)
        else if (balance >= 1000000000000.0)
        {
            double trillions = balance / 1000000000000.0;
            return FormatBalanceValue(trillions, "T");
        }
        // Миллиарды (10^9)
        else if (balance >= 1000000000.0)
        {
            double billions = balance / 1000000000.0;
            return FormatBalanceValue(billions, "B");
        }
        // Миллионы (10^6)
        else if (balance >= 1000000.0)
        {
            double millions = balance / 1000000.0;
            return FormatBalanceValue(millions, "M");
        }
        // Тысячи (10^3)
        else if (balance >= 1000.0)
        {
            double thousands = balance / 1000.0;
            return FormatBalanceValue(thousands, "K");
        }
        else
        {
            // Меньше тысячи - показываем как целое число
            return ((long)balance).ToString();
        }
    }
    
    /// <summary>
    /// Вспомогательный метод для форматирования значения баланса
    /// Целые числа отображаются без десятичных знаков
    /// </summary>
    private string FormatBalanceValue(double value, string suffix)
    {
        // Проверяем, является ли число целым
        if (value == Mathf.Floor((float)value))
        {
            // Целое число - без десятичных знаков
            return $"{(long)value}{suffix}";
        }
        else
        {
            // Дробное число - с десятичными знаками (убираем лишние нули)
            string formatted = $"{value:F2}{suffix}".TrimEnd('0').TrimEnd('.');
            return formatted;
        }
    }
    
    #endregion
    
    #region Brainrots Methods
    
    /// <summary>
    /// Получить все Brainrot объекты
    /// </summary>
    public List<BrainrotData> GetAllBrainrots()
    {
        return YG2.saves.Brainrots;
    }
    
    /// <summary>
    /// Получить Brainrot по slotID
    /// </summary>
    public BrainrotData GetBrainrotBySlotID(int slotID)
    {
        return YG2.saves.Brainrots.Find(b => b.slotID == slotID);
    }
    
    /// <summary>
    /// Получить Brainrot по имени
    /// </summary>
    public BrainrotData GetBrainrotByName(string name)
    {
        return YG2.saves.Brainrots.Find(b => b.name == name);
    }
    
    /// <summary>
    /// Добавить новый Brainrot
    /// </summary>
    public void AddBrainrot(BrainrotData brainrot)
    {
        if (brainrot != null)
        {
            YG2.saves.Brainrots.Add(brainrot);
        }
    }
    
    /// <summary>
    /// Добавить новый Brainrot с параметрами
    /// </summary>
    public void AddBrainrot(string name, int rarity, int income, int level, int slotID)
    {
        BrainrotData brainrot = new BrainrotData(name, rarity, income, level, slotID);
        YG2.saves.Brainrots.Add(brainrot);
    }
    
    /// <summary>
    /// Удалить Brainrot по slotID
    /// </summary>
    public bool RemoveBrainrotBySlotID(int slotID)
    {
        BrainrotData brainrot = YG2.saves.Brainrots.Find(b => b.slotID == slotID);
        if (brainrot != null)
        {
            return YG2.saves.Brainrots.Remove(brainrot);
        }
        return false;
    }
    
    /// <summary>
    /// Удалить Brainrot по имени
    /// </summary>
    public bool RemoveBrainrotByName(string name)
    {
        BrainrotData brainrot = YG2.saves.Brainrots.Find(b => b.name == name);
        if (brainrot != null)
        {
            return YG2.saves.Brainrots.Remove(brainrot);
        }
        return false;
    }
    
    /// <summary>
    /// Обновить данные Brainrot по slotID
    /// </summary>
    public bool UpdateBrainrot(int slotID, string name = null, int? rarity = null, int? income = null, int? level = null)
    {
        BrainrotData brainrot = YG2.saves.Brainrots.Find(b => b.slotID == slotID);
        if (brainrot != null)
        {
            if (name != null) brainrot.name = name;
            if (rarity.HasValue) brainrot.rarity = rarity.Value;
            if (income.HasValue) brainrot.income = income.Value;
            if (level.HasValue) brainrot.level = level.Value;
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// Получить количество Brainrot объектов
    /// </summary>
    public int GetBrainrotsCount()
    {
        return YG2.saves.Brainrots.Count;
    }
    
    /// <summary>
    /// Очистить все Brainrot объекты
    /// </summary>
    public void ClearAllBrainrots()
    {
        YG2.saves.Brainrots.Clear();
    }
    
    /// <summary>
    /// Удаляет из сохранения все брейнроты, которые не находятся в placement (не на панелях) и не в руках.
    /// Вызывается при 100% огня — остаются брейнроты на PlacementPanel и брейнрот в руках.
    /// </summary>
    public void RemoveBrainrotsNotInPlacement()
    {
        List<PlacementData> placed = GetAllPlacedBrainrots();
        if (placed == null) return;
        var namesToKeep = new HashSet<string>();
        for (int i = 0; i < placed.Count; i++)
        {
            if (placed[i] != null && !string.IsNullOrEmpty(placed[i].brainrotName))
                namesToKeep.Add(placed[i].brainrotName);
        }
        // Не удалять из сохранения брейнрот, который в руках
        PlayerCarryController carry = FindFirstObjectByType<PlayerCarryController>();
        if (carry != null)
        {
            BrainrotObject carried = carry.GetCurrentCarriedObject();
            if (carried != null)
            {
                string name = carried.GetObjectName();
                if (!string.IsNullOrEmpty(name))
                    namesToKeep.Add(name);
            }
        }
        YG2.saves.Brainrots.RemoveAll(b => b != null && !namesToKeep.Contains(b.name));
    }
    
    #endregion
    
    #region Speed Methods
    
    /// <summary>
    /// Получить текущий уровень скорости игрока
    /// </summary>
    public int GetPlayerSpeedLevel()
    {
        return YG2.saves.PlayerSpeedLevel;
    }
    
    /// <summary>
    /// Установить уровень скорости игрока
    /// </summary>
    public void SetPlayerSpeedLevel(int level)
    {
        YG2.saves.PlayerSpeedLevel = level;
        Save();
    }
    
    /// <summary>
    /// Увеличить уровень скорости игрока
    /// </summary>
    public void IncreasePlayerSpeedLevel(int amount = 1)
    {
        YG2.saves.PlayerSpeedLevel += amount;
        Save();
    }
    
    #endregion
    
    #region Opening Level Methods
    
    /// <summary>
    /// Получить уровень скорости открытия клеток
    /// </summary>
    public int GetOpeningLevel()
    {
        return YG2.saves.OpeningLevel;
    }
    
    /// <summary>
    /// Установить уровень скорости открытия клеток
    /// </summary>
    public void SetOpeningLevel(int level)
    {
        YG2.saves.OpeningLevel = level;
        Save();
    }
    
    /// <summary>
    /// Увеличить уровень скорости открытия клеток
    /// </summary>
    public void IncreaseOpeningLevel(int amount = 1)
    {
        YG2.saves.OpeningLevel += amount;
        Save();
    }
    
    #endregion
    
    /// <summary>
    /// Отложенное применение стартового баланса в режиме разработчика.
    /// Ждёт загрузку YG2 и затем перезаписывает баланс, чтобы dev startBalance не затирался сохранением.
    /// </summary>
    private System.Collections.IEnumerator ApplyDevModeBalanceDelayed()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        // Даём YG2 время загрузить сохранения (в редакторе/вебе они могут подгрузиться позже)
        yield return new WaitForSeconds(0.5f);
        
        ApplyDevModeBalance();
    }
    
    /// <summary>
    /// Применить стартовый баланс в режиме разработчика
    /// </summary>
    private void ApplyDevModeBalance()
    {
        if (devMode && startBalance > 0)
        {
            // Конвертируем long в value + scaler и устанавливаем стартовый баланс
            (int value, string scaler) = ConvertDoubleToBalance((double)startBalance);
            SetBalance(value, scaler);
            
            // Сохраняем изменения только если YG2 инициализирован
            if (YG2.isSDKEnabled)
            {
                Save();
                Debug.Log($"[GameStorage] DevMode: установлен стартовый баланс {startBalance}");
            }
            else
            {
                // Если YG2 еще не инициализирован, откладываем сохранение
                StartCoroutine(DelayedSave());
            }
        }
    }
    
    /// <summary>
    /// Отложенное сохранение для случаев, когда YG2 еще не инициализирован
    /// </summary>
    private System.Collections.IEnumerator DelayedSave()
    {
        // Ждем до момента, когда YG2 будет инициализирован
        float waitTime = 0f;
        float maxWaitTime = 2f; // Максимальное время ожидания (2 секунды)
        
        while (waitTime < maxWaitTime)
        {
            yield return new WaitForSeconds(0.1f);
            waitTime += 0.1f;
            
            // Проверяем, инициализирован ли YG2
            if (YG2.isSDKEnabled)
            {
                Save();
                Debug.Log($"[GameStorage] DevMode: установлен стартовый баланс {startBalance} (после ожидания {waitTime:F1}с)");
                yield break;
            }
        }
        
        Debug.LogWarning("[GameStorage] DevMode: не удалось сохранить стартовый баланс - YG2 не инициализирован за отведенное время");
    }
    
    #region Save Methods
    
    /// <summary>
    /// Сохранить прогресс в YG2
    /// </summary>
    public void Save()
    {
        YG2.SaveProgress();
    }
    
    /// <summary>
    /// Очистить все данные storage (баланс, все Brainrot объекты, размещенные брейнроты, доходы earnpanel, уровень скорости и сила игрока).
    /// Можно вызвать из инспектора.
    /// </summary>
    [ContextMenu("Clear Storage")]
    public void ClearStorage()
    {
        // Очищаем баланс
        YG2.saves.balanceCount = 0;
        YG2.saves.balanceScaler = "";
        
        // Очищаем все Brainrot объекты
        YG2.saves.Brainrots.Clear();
        
        // Очищаем все размещенные брейнроты
        if (YG2.saves.PlacedBrainrots != null)
        {
            YG2.saves.PlacedBrainrots.Clear();
        }
        
        // Очищаем все доходы earnpanel
        if (YG2.saves.EarnPanelBalances != null)
        {
            YG2.saves.EarnPanelBalances.Clear();
        }
        
        // Сбрасываем уровень скорости к значению по умолчанию
        YG2.saves.PlayerSpeedLevel = 0;
        
        // Сбрасываем уровень скорости открытия клеток
        YG2.saves.OpeningLevel = 0;
        
        // Сбрасываем силу игрока (уровень атаки)
        YG2.saves.AttackPowerLevel = 0;
        
        // Очищаем список разблокированных лестниц
        YG2.saves.UnlockedLadders.Clear();
        
        // Сохраняем изменения
        YG2.SaveProgress();
        
        // Обновляем все Ladder объекты в сцене, чтобы они снова заблокировали лестницы
        Ladder[] allLadders = FindObjectsByType<Ladder>(FindObjectsSortMode.None);
        foreach (Ladder ladder in allLadders)
        {
            if (ladder != null)
            {
                ladder.UpdateUnlockStatus();
            }
        }
        
        Debug.Log("[GameStorage] Storage очищен: баланс, все Brainrot объекты, размещенные брейнроты, доходы earnpanel, уровень скорости, уровень открытия, сила игрока и разблокированные лестницы сброшены");
    }
    
    /// <summary>
    /// Полностью сбросить storage к значениям по умолчанию (использует YG2.SetDefaultSaves)
    /// </summary>
    public void ResetStorage()
    {
        // Сохраняем idSave перед сбросом
        int savedIdSave = YG2.saves.idSave;
        
        // Сбрасываем все сохранения к значениям по умолчанию
        YG2.SetDefaultSaves();
        
        // Восстанавливаем idSave
        YG2.saves.idSave = savedIdSave;
        
        // Сохраняем изменения
        YG2.SaveProgress();
        
        Debug.Log("[GameStorage] Storage полностью сброшен к значениям по умолчанию");
    }
    
    #endregion
    
    #region Placement Methods
    
    /// <summary>
    /// Сохранить размещенный брейнрот на панели со всеми параметрами (редкость и baseIncome)
    /// </summary>
    public void SavePlacedBrainrot(int placementID, string brainrotName, int level, string rarity, long baseIncome)
    {
        // Ищем существующую запись
        PlacementData existing = YG2.saves.PlacedBrainrots.Find(p => p.placementID == placementID);
        
        if (existing != null)
        {
            // Обновляем существующую запись со всеми параметрами
            existing.brainrotName = brainrotName;
            existing.level = level;
            existing.rarity = rarity;
            existing.baseIncome = baseIncome;
        }
        else
        {
            // Создаем новую запись со всеми параметрами
            PlacementData newPlacement = new PlacementData(placementID, brainrotName, level, rarity, baseIncome);
            YG2.saves.PlacedBrainrots.Add(newPlacement);
        }
        
        Save();
    }
    
    /// <summary>
    /// Сохранить размещенный брейнрот на панели (старый метод для обратной совместимости)
    /// </summary>
    public void SavePlacedBrainrot(int placementID, string brainrotName, int level)
    {
        // Используем значения по умолчанию для редкости и baseIncome
        SavePlacedBrainrot(placementID, brainrotName, level, "Common", 0);
    }
    
    /// <summary>
    /// Удалить размещенный брейнрот с панели
    /// </summary>
    public void RemovePlacedBrainrot(int placementID)
    {
        PlacementData existing = YG2.saves.PlacedBrainrots.Find(p => p.placementID == placementID);
        if (existing != null)
        {
            YG2.saves.PlacedBrainrots.Remove(existing);
            Save();
        }
    }
    
    /// <summary>
    /// Получить размещенный брейнрот по ID панели
    /// </summary>
    public PlacementData GetPlacedBrainrot(int placementID)
    {
        return YG2.saves.PlacedBrainrots.Find(p => p.placementID == placementID);
    }
    
    /// <summary>
    /// Получить все размещенные брейнроты
    /// </summary>
    public List<PlacementData> GetAllPlacedBrainrots()
    {
        return YG2.saves.PlacedBrainrots;
    }
    
    /// <summary>
    /// Финальный доход по данным размещения (та же формула, что в BrainrotObject: baseIncome * rarityMultiplier * (1 + level)).
    /// </summary>
    public static double GetFinalIncomeFromPlacement(PlacementData p)
    {
        if (p == null) return 0;
        long mult = GetRarityMultiplierFromString(p.rarity);
        double levelMult = 1.0 + 1.0 * p.level;
        return p.baseIncome * mult * levelMult;
    }
    
    /// <summary>
    /// Множитель редкости по строке (Common=1, Rare=5, Exclusive=10, Epic=50, Mythic=200, Legendary=1000, Secret=10000).
    /// </summary>
    public static long GetRarityMultiplierFromString(string rarity)
    {
        if (string.IsNullOrEmpty(rarity)) return 1L;
        switch (rarity.ToLower())
        {
            case "common": return 1L;
            case "rare": return 5L;
            case "exclusive": return 10L;
            case "epic": return 50L;
            case "mythic": return 200L;
            case "legendary": return 1000L;
            case "secret": return 10000L;
            default: return 1L;
        }
    }
    
    #endregion
    
    #region EarnPanel Methods
    
    /// <summary>
    /// Сохранить накопленный доход на панели
    /// </summary>
    public void SaveEarnPanelBalance(int panelID, double accumulatedBalance)
    {
        // Ищем существующую запись
        EarnPanelData existing = YG2.saves.EarnPanelBalances.Find(e => e.panelID == panelID);
        
        if (existing != null)
        {
            // Обновляем существующую запись
            existing.accumulatedBalance = accumulatedBalance;
        }
        else
        {
            // Создаем новую запись
            EarnPanelData newEarnPanel = new EarnPanelData(panelID, accumulatedBalance);
            YG2.saves.EarnPanelBalances.Add(newEarnPanel);
        }
        
        Save();
    }
    
    /// <summary>
    /// Получить накопленный доход панели по ID
    /// </summary>
    public double GetEarnPanelBalance(int panelID)
    {
        EarnPanelData existing = YG2.saves.EarnPanelBalances.Find(e => e.panelID == panelID);
        if (existing != null)
        {
            return existing.accumulatedBalance;
        }
        return 0.0;
    }
    
    /// <summary>
    /// Очистить накопленный доход панели
    /// </summary>
    public void ClearEarnPanelBalance(int panelID)
    {
        EarnPanelData existing = YG2.saves.EarnPanelBalances.Find(e => e.panelID == panelID);
        if (existing != null)
        {
            existing.accumulatedBalance = 0.0;
            Save();
        }
    }
    
    /// <summary>
    /// Получить все данные о доходах панелей
    /// </summary>
    public List<EarnPanelData> GetAllEarnPanelBalances()
    {
        return YG2.saves.EarnPanelBalances;
    }
    
    #endregion
    
    #region Attack Power Methods
    
    /// <summary>
    /// Получить уровень силы удара игрока
    /// </summary>
    public int GetAttackPowerLevel()
    {
        return YG2.saves.AttackPowerLevel;
    }
    
    /// <summary>
    /// Установить уровень силы удара игрока
    /// </summary>
    public void SetAttackPowerLevel(int level)
    {
        YG2.saves.AttackPowerLevel = level;
        Save();
    }
    
    /// <summary>
    /// Увеличить уровень силы удара игрока
    /// </summary>
    public void IncreaseAttackPowerLevel(int amount = 1)
    {
        int currentLevel = GetAttackPowerLevel();
        SetAttackPowerLevel(currentLevel + amount);
    }
    
    #endregion
    
    #region Ultimate Methods
    
    /// <summary>
    /// Получить текущий ультимейт игрока
    /// </summary>
    public string GetCurrentUltimate()
    {
        return YG2.saves.CurrentUltimate ?? "IsStrongBeat1";
    }
    
    /// <summary>
    /// Установить текущий ультимейт игрока
    /// </summary>
    public void SetCurrentUltimate(string ultimateName)
    {
        YG2.saves.CurrentUltimate = ultimateName;
        Save();
    }
    
    #endregion
    
    #region Ladder Unlock Methods
    
    /// <summary>
    /// Проверить, разблокирована ли лестница
    /// </summary>
    public bool IsLadderUnlocked(int ladderId)
    {
        return YG2.saves.UnlockedLadders.Contains(ladderId);
    }
    
    /// <summary>
    /// Разблокировать лестницу
    /// </summary>
    public bool UnlockLadder(int ladderId)
    {
        if (!YG2.saves.UnlockedLadders.Contains(ladderId))
        {
            YG2.saves.UnlockedLadders.Add(ladderId);
            Save();
            return true;
        }
        return false; // Лестница уже разблокирована
    }
    
    /// <summary>
    /// Получить список разблокированных лестниц
    /// </summary>
    public List<int> GetUnlockedLadders()
    {
        return new List<int>(YG2.saves.UnlockedLadders);
    }
    
    #endregion
}
