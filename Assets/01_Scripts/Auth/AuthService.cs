using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class AuthService : MonoBehaviour
{
    [Header("Backend")]
    [SerializeField]
    private string baseUrl = "http://10.159.87.245:5116";

    public IEnumerator LoginStudent(
        string nim,
        string password,
        Action<StudentLoginResponse> onSuccess,
        Action<string> onError)
    {
        StudentLoginRequest requestData =
            new StudentLoginRequest
            {
                nim = nim,
                password = password
            };

        string json =
            JsonUtility.ToJson(requestData);

        yield return SendRequest(
            "/api/v1/Auth/login",
            json,
            response =>
            {
                try
                {
                    StudentLoginResponse data =
                        JsonUtility.FromJson<StudentLoginResponse>(
                            response
                        );

                    if (data == null ||
                        string.IsNullOrEmpty(
                            data.accessToken) ||
                        string.IsNullOrEmpty(
                            data.refreshToken))
                    {
                        onError?.Invoke(
                            "Response login tidak valid."
                        );

                        return;
                    }

                    onSuccess?.Invoke(data);
                }
                catch (Exception ex)
                {
                    onError?.Invoke(
                        "Gagal membaca response server: " +
                        ex.Message
                    );
                }
            },
            onError
        );
    }

    public IEnumerator RefreshSession(
        string refreshToken,
        Action<RefreshTokenResponse> onSuccess,
        Action<string> onError)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            onError?.Invoke(
                "Refresh token tidak tersedia."
            );

            yield break;
        }

        RefreshTokenRequest requestData =
            new RefreshTokenRequest
            {
                refreshToken = refreshToken
            };

        string json =
            JsonUtility.ToJson(requestData);

        yield return SendRequest(
            "/api/v1/Auth/refresh",
            json,
            response =>
            {
                try
                {
                    RefreshTokenResponse data =
                        JsonUtility.FromJson<RefreshTokenResponse>(
                            response
                        );

                    if (data == null ||
                        string.IsNullOrEmpty(
                            data.accessToken) ||
                        string.IsNullOrEmpty(
                            data.refreshToken))
                    {
                        onError?.Invoke(
                            "Response refresh token tidak valid."
                        );

                        return;
                    }

                    onSuccess?.Invoke(data);
                }
                catch (Exception ex)
                {
                    onError?.Invoke(
                        "Gagal membaca response refresh: " +
                        ex.Message
                    );
                }
            },
            onError
        );
    }

    public IEnumerator Logout(
        string refreshToken,
        Action<string> onError)
    {
        RefreshTokenRequest requestData =
            new RefreshTokenRequest
            {
                refreshToken = refreshToken
            };

        string json =
            JsonUtility.ToJson(requestData);

        // Server balas 204 No Content dan idempotent:
        // token tidak ada / sudah dicabut tetap 204.
        yield return SendRequest(
            "/api/v1/Auth/logout",
            json,
            null,
            onError
        );
    }

    private IEnumerator SendRequest(
        string endpoint,
        string json,
        Action<string> onSuccess,
        Action<string> onError)
    {
        string url =
            baseUrl.TrimEnd('/') +
            endpoint;

        using UnityWebRequest request =
            new UnityWebRequest(
                url,
                UnityWebRequest.kHttpVerbPOST
            );

        byte[] body =
            System.Text.Encoding.UTF8.GetBytes(
                json
            );

        request.uploadHandler =
            new UploadHandlerRaw(body);

        request.downloadHandler =
            new DownloadHandlerBuffer();

        request.SetRequestHeader(
            "Content-Type",
            "application/json"
        );

        request.SetRequestHeader(
            "Accept",
            "application/json"
        );

        yield return request.SendWebRequest();

        if (request.result ==
            UnityWebRequest.Result.Success)
        {
            onSuccess?.Invoke(
                request.downloadHandler.text
            );

            yield break;
        }

        if (request.responseCode == 401)
        {
            onError?.Invoke(
                "Unauthorized"
            );

            yield break;
        }

        if (request.responseCode == 0)
        {
            onError?.Invoke(
                "Tidak dapat terhubung ke server."
            );

            yield break;
        }

        onError?.Invoke(
            $"Request gagal. HTTP " +
            $"{request.responseCode}"
        );
    }
}