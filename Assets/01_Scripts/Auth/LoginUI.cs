using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class LoginUI : MonoBehaviour
{
    [Header("Input")]
    [SerializeField]
    private TMP_InputField nimInput;

    [SerializeField]
    private TMP_InputField passwordInput;

    [Header("UI")]
    [SerializeField]
    private Button loginButton;

    [SerializeField]
    private TMP_Text errorText;

    [SerializeField] 
    private GameObject errorNotice;
    
    private Coroutine errorCoroutine;

    [SerializeField]
    private TMP_Text loginButtonText;

    private void Start()
    {
        ClearError();

        if (loginButton != null)
            loginButton.onClick.AddListener(Login);
    }

    private void OnDestroy()
    {
        if (loginButton != null)
            loginButton.onClick.RemoveListener(Login);
    }

    public void Login()
    {
        string nim = nimInput.text.Trim();
        string password = passwordInput.text;

        if (string.IsNullOrEmpty(nim))
        {
            ShowError("NIM wajib diisi.");
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            ShowError("Password wajib diisi.");
            return;
        }

        AuthManager authManager = AuthManager.Instance;

        if (authManager == null)
        {
            ShowError("Sistem autentikasi belum siap.");
            Debug.LogError(
                "AuthManager.Instance tidak ditemukan."
            );

            return;
        }

        SetLoading(true);
        ClearError();

        authManager.LoginStudent(
            nim,
            password,
            OnLoginSuccess,
            OnLoginError
        );
    }

    private void OnLoginSuccess()
    {
        SetLoading(false);

        Debug.Log(
            $"Login berhasil. Selamat datang, {AuthSession.Name}"
        );

        AuthManager.Instance.GoToMainMenu();
    }

    private void OnLoginError(string message)
    {
        SetLoading(false);
        ShowError(message);
    }

    private void SetLoading(bool loading)
    {
        if (loginButton != null)
            loginButton.interactable = !loading;

        if (loginButtonText != null)
            loginButtonText.text =
                loading ? "LOGIN..." : "LOGIN";
    }

    private void ShowError(string message)
    {
        if (errorCoroutine != null)
        {
            StopCoroutine(errorCoroutine);
        }

        if (errorText != null)
            errorText.text = message;

        if (errorNotice != null)
            errorNotice.SetActive(true);

        errorCoroutine = StartCoroutine(ClearErrorAfterDelay());
    }

    private IEnumerator ClearErrorAfterDelay()
    {
        yield return new WaitForSeconds(3f);

        ClearError();
    }

    private void ClearError()
    {
        if (errorCoroutine != null)
        {
            StopCoroutine(errorCoroutine);
            errorCoroutine = null;
        }

        if (errorText != null)
            errorText.text = "";

        if (errorNotice != null)
            errorNotice.SetActive(false);
    }
}