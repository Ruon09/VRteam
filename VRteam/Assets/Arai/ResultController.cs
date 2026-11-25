
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.SceneManagement;
using Keyboard = UnityEngine.InputSystem.Keyboard;

public class ResultController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Text scoreText;
    [SerializeField] private Text defeatText;
    [SerializeField] private Image exitButton;
    [SerializeField] private Image titleButton;

    [Header("Scene")]
    [SerializeField] private string titleSceneName = "TitleScene";

    [Header("Test Result")]
    [SerializeField] private bool isVictory = true;
    [SerializeField] private float clearTime = 120f;
    [SerializeField] private int skillCount = 10;
    [SerializeField] private int damageCount = 3;

    private int selectedIndex = 1;
    private bool confirmReady;
    private bool isLoading;

    private Outline exitOutline;
    private Outline titleOutline;

    private static bool hasResult;
    private static bool resultVictory;
    private static float resultTime;
    private static int resultSkills;
    private static int resultDamage;

    // バトルシーンから結果を受け取る
    public static void SetResult(
        bool victory, float time, int skills, int damage)
    {
        hasResult = true;
        resultVictory = victory;
        resultTime = time;
        resultSkills = skills;
        resultDamage = damage;
    }

    private void Start()
    {
        if (hasResult)
        {
            isVictory = resultVictory;
            clearTime = resultTime;
            skillCount = resultSkills;
            damageCount = resultDamage;
            hasResult = false;
        }

        exitOutline = CreateOutline(exitButton);
        titleOutline = CreateOutline(titleButton);

        ShowResult();
        UpdateSelection();
    }

    private void Update()
    {
        if (isLoading) return;

        InputDevice left =
            InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);

        InputDevice right =
            InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        float stick = GetStick(left);
        float rightStick = GetStick(right);

        if (Mathf.Abs(rightStick) > Mathf.Abs(stick))
            stick = rightStick;

        bool confirm = CheckConfirm(left)
                    || CheckConfirm(right);

#if UNITY_EDITOR
        if (Keyboard.current != null)
        {
            if (Keyboard.current.leftArrowKey.isPressed)
                stick = -1f;

            if (Keyboard.current.rightArrowKey.isPressed)
                stick = 1f;

            confirm |= Keyboard.current.enterKey.isPressed
                    || Keyboard.current.spaceKey.isPressed;
        }
#endif

        int previousIndex = selectedIndex;

        if (stick < -0.5f)
            selectedIndex = 0;

        if (stick > 0.5f)
            selectedIndex = 1;

        if (previousIndex != selectedIndex)
            UpdateSelection();

        // ボタンの押しっぱなしによる誤決定を防止
        if (!confirm)
            confirmReady = true;

        if (confirm && confirmReady)
        {
            confirmReady = false;
            ExecuteSelection();
        }
    }

    private void ShowResult()
    {
        scoreText.gameObject.SetActive(isVictory);
        defeatText.gameObject.SetActive(!isVictory);

        if (isVictory)
        {
            int seconds = Mathf.FloorToInt(
                Mathf.Max(0f, clearTime));

            scoreText.text =
                $"クリア時間　　　：{seconds / 60:00}:{seconds % 60:00}\n" +
                $"スキル発動回数　：{skillCount:00}\n" +
                $"被ダメージ回数　：{damageCount:00}";
        }
        else
        {
            defeatText.text = "敗北";
        }
    }

    private Outline CreateOutline(Image image)
    {
        Outline outline = image.GetComponent<Outline>();

        if (outline == null)
            outline = image.gameObject.AddComponent<Outline>();

        outline.effectColor = Color.yellow;
        outline.effectDistance = new Vector2(4f, -4f);

        return outline;
    }

    private void UpdateSelection()
    {
        exitOutline.enabled = selectedIndex == 0;
        titleOutline.enabled = selectedIndex == 1;

        exitButton.transform.localScale =
            Vector3.one * (selectedIndex == 0 ? 1.1f : 1f);

        titleButton.transform.localScale =
            Vector3.one * (selectedIndex == 1 ? 1.1f : 1f);
    }

    private float GetStick(InputDevice device)
    {
        if (device.TryGetFeatureValue(
            CommonUsages.primary2DAxis, out Vector2 axis))
        {
            return axis.x;
        }

        return 0f;
    }

    private bool CheckConfirm(InputDevice device)
    {
        return IsPressed(device, CommonUsages.primaryButton)
            || IsPressed(device, CommonUsages.triggerButton);
    }

    private bool IsPressed(
        InputDevice device,
        InputFeatureUsage<bool> button)
    {
        return device.TryGetFeatureValue(
            button, out bool pressed) && pressed;
    }

    private void ExecuteSelection()
    {
        if (selectedIndex == 0)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        else
        {
            if (!Application.CanStreamedLevelBeLoaded(titleSceneName))
            {
                Debug.LogError("TitleSceneが登録されていません。");
                return;
            }

            isLoading = true;
            SceneManager.LoadSceneAsync(titleSceneName);
        }
    }
}
