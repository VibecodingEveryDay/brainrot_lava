using System;
using UnityEngine;

/// <summary>
/// Заглушка менеджера огня/лавы. Пока механика лавы не реализована — прогресс всегда 0, событие не вызывается.
/// Когда реализуете подъём лавы и урон игроку — замените логику в этом классе и добавьте компонент на сцену.
/// </summary>
public class DestroyFireManager : MonoBehaviour
{
    /// <summary>
    /// Вызывается, когда прогресс огня достигает 100% (игрок «сгорел»).
    /// TeleportManager подписывается на это событие для телепорта в лобби при поражении.
    /// </summary>
#pragma warning disable CS0067 // Событие объявлено для подписки TeleportManager, вызывается при реализации механики лавы
    public event Action OnProgressComplete;
#pragma warning restore CS0067

    /// <summary>
    /// Текущий прогресс огня от 0 до 1. Пока заглушка — всегда 0.
    /// </summary>
    public float GetProgress() => 0f;

    // Когда реализуете лаву, можно вызывать:
    // OnProgressComplete?.Invoke();
    // когда HP игрока в зоне лавы достигнет нуля.
}
