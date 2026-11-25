using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

/// <summary>
/// Lets the left stick select a horizontal result-menu option.
/// Either controller trigger confirms the selected option.
/// </summary>
public sealed class TestResultMenuController : MonoBehaviour
{
    private const int RetryOption = 0;
    private const int QuitOption = 1;

    [SerializeField] private GameObject[] selectionFrames = new GameObject[2];
    [SerializeField] private string titleScene = "Assets/Scenes/Yamaguchi/Test_Title.unity";
    [SerializeField, Min(0f)] private float inputReadyDelay = 0.35f;
    [SerializeField, Range(0.1f, 0.95f)] private float stickPressThreshold = 0.65f;
    [SerializeField, Range(0f, 0.5f)] private float stickReleaseThreshold = 0.25f;

    private int selectedOption = RetryOption;
    private bool stickArmed = true;
    private bool previousLeftTrigger;
    private bool previousRightTrigger;
    private bool loading;
    private float inputReadyAt;

    private void Start()
    {
        inputReadyAt = Time.unscaledTime + inputReadyDelay;
        UpdateSelectionFrames();
    }

    private void Update()
    {
        ReadLeftStick();

        bool leftTrigger = ReadTrigger(XRNode.LeftHand);
        bool rightTrigger = ReadTrigger(XRNode.RightHand);
        bool triggerPressed = (leftTrigger && !previousLeftTrigger)
            || (rightTrigger && !previousRightTrigger);

        previousLeftTrigger = leftTrigger;
        previousRightTrigger = rightTrigger;

        if (!loading && Time.unscaledTime >= inputReadyAt && triggerPressed)
            ConfirmSelection();
    }

    private void ReadLeftStick()
    {
        InputDevice leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (!leftHand.isValid
            || !leftHand.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis))
            return;

        if (Mathf.Abs(axis.x) <= stickReleaseThreshold)
            stickArmed = true;

        if (!stickArmed || Mathf.Abs(axis.x) < stickPressThreshold)
            return;

        selectedOption = axis.x < 0f ? RetryOption : QuitOption;
        stickArmed = false;
        UpdateSelectionFrames();
    }

    private void UpdateSelectionFrames()
    {
        for (int i = 0; i < selectionFrames.Length; i++)
        {
            if (selectionFrames[i] != null)
                selectionFrames[i].SetActive(i == selectedOption);
        }
    }

    private void ConfirmSelection()
    {
        if (selectedOption == RetryOption)
            Retry();
        else
            Quit();
    }

    private void Retry()
    {
        if (!Application.CanStreamedLevelBeLoaded(titleScene))
        {
            Debug.LogError($"Title scene is not included in the build settings: {titleScene}", this);
            return;
        }

        loading = true;
        SceneManager.LoadSceneAsync(titleScene, LoadSceneMode.Single);
    }

    private static bool ReadTrigger(XRNode hand)
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(hand);
        return device.isValid
            && device.TryGetFeatureValue(CommonUsages.triggerButton, out bool pressed)
            && pressed;
    }

    private void Quit()
    {
        loading = true;
        Application.Quit();
    }
}
