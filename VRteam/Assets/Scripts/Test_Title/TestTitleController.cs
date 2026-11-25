using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

/// <summary>
/// Starts the battle when either Quest controller trigger is pressed.
/// </summary>
public sealed class TestTitleController : MonoBehaviour
{
    [SerializeField] private string battleScene = "Assets/GameMain/Game.unity";
    [SerializeField, Min(0f)] private float inputReadyDelay = 0.35f;

    private bool previousLeftTrigger;
    private bool previousRightTrigger;
    private bool loading;
    private float inputReadyAt;

    private void Start()
    {
        inputReadyAt = Time.unscaledTime + inputReadyDelay;
    }

    private void Update()
    {
        bool leftTrigger = ReadTrigger(XRNode.LeftHand);
        bool rightTrigger = ReadTrigger(XRNode.RightHand);

        bool triggerPressed = (leftTrigger && !previousLeftTrigger)
            || (rightTrigger && !previousRightTrigger);

        previousLeftTrigger = leftTrigger;
        previousRightTrigger = rightTrigger;

        if (!loading && Time.unscaledTime >= inputReadyAt && triggerPressed)
            LoadBattle();
    }

    private static bool ReadTrigger(XRNode hand)
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(hand);
        return device.isValid
            && device.TryGetFeatureValue(CommonUsages.triggerButton, out bool pressed)
            && pressed;
    }

    private void LoadBattle()
    {
        if (!Application.CanStreamedLevelBeLoaded(battleScene))
        {
            Debug.LogError($"Battle scene is not included in the build settings: {battleScene}", this);
            return;
        }

        loading = true;
        SceneManager.LoadSceneAsync(battleScene, LoadSceneMode.Single);
    }
}
