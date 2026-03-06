using UnityEngine;

/// <summary>
/// Интерфейс для переноски BrainrotObject (игрок или бот).
/// </summary>
public interface ICarryController
{
    void CarryObject(BrainrotObject obj);
    bool CanCarry();
    BrainrotObject GetCurrentCarriedObject();
    void DropObject();
    Transform GetCarrierTransform();
}
