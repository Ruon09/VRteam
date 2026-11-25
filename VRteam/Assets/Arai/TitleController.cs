
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

using InputDevice = UnityEngine.XR.InputDevice;
using CommonUsages = UnityEngine.XR.CommonUsages;

public class TitleController : MonoBehaviour
{
    [SerializeField]
    private string nextSceneName = "ModeSelectScene";

    private bool isLoading = false;

    void Update()
    {
        if (isLoading) return;

        InputDevice left =
            InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);

        InputDevice right =
            InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        bool keyboardPressed = false;

#if UNITY_EDITOR
        keyboardPressed = Keyboard.current != null
            && Keyboard.current.anyKey.wasPressedThisFrame;
#endif

        if (CheckButton(left) || CheckButton(right) || keyboardPressed)
        {
            isLoading = true;
            SceneManager.LoadSceneAsync(nextSceneName);
        }
    }

    private bool CheckButton(UnityEngine.XR.InputDevice device)
    {
        return
            IsPressed(device, CommonUsages.primaryButton) ||
            IsPressed(device, CommonUsages.secondaryButton) ||
            IsPressed(device, CommonUsages.triggerButton) ||
            IsPressed(device, CommonUsages.gripButton) ||
            IsPressed(device, CommonUsages.primary2DAxisClick) ||
            IsPressed(device, CommonUsages.menuButton);
    }

    private bool IsPressed(
        UnityEngine.XR.InputDevice device,
        InputFeatureUsage<bool> button)
    {
        return device.TryGetFeatureValue(button, out bool pressed)
            && pressed;
    }
}
