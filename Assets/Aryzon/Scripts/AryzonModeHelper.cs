using UnityEngine;
using Aryzon;

public class AryzonModeHelper : MonoBehaviour
{
    public void StartAryzonMode() {
        AryzonSettings.Instance.aryzonManager.StartAryzonMode();
    }

    public void StopAryzonMode() {
        AryzonSettings.Instance.aryzonManager.StopAryzonMode();
    }
}