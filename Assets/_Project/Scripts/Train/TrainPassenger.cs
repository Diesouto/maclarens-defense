using UnityEngine;

public class TrainPassenger : MonoBehaviour
{
    public TrainPassengerArea CurrentCarriage { get; private set; }

    public void SetCarriage(TrainPassengerArea carriage)
    {
        if (CurrentCarriage == carriage)
            return;

        CurrentCarriage = carriage;

        if (carriage == null)
        {
            transform.SetParent(null, true);
            return;
        }

        transform.SetParent(carriage.CarriageRoot, true);
    }
}