using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Gree.UnityWebView;

public class WebViewController : MonoBehaviour
{
    private WebViewObject webView;
    private int lastScreenWidth;
    private int lastScreenHeight;

    // WebView Android di plugin ini memakai setUseWideViewPort +
    // setLoadWithOverviewMode. Kalau halaman tidak mengunci viewport-nya,
    // seluruh halaman bisa diperkecil agar muat sehingga UI terlihat mungil.
    // Sabuk pengaman ini menyamakan viewport dengan materi.html.
    private const string ViewportTag =
        "width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no, viewport-fit=cover";

    // Pesan yang dikirim halaman web lewat `window.Unity.call(...)`.
    // Harus sama dengan konstanta di src/lib/unityBridge.ts.
    private const string CloseWebViewMessage = "CloseWebView";
    private const string RequestSessionMessage = "RequestSession";

    // Nama fungsi global yang dipanggil halaman web untuk menerima sesi.
    // Harus sama dengan SESSION_HANDLER di src/lib/unityBridge.ts.
    private const string SessionHandlerName = "__onUnityExamSession";

    [Header("Development")]
    [SerializeField]
    private string url = "http://10.159.87.245:5173";

    private IEnumerator Start()
    {
        webView = new GameObject("NativeWebView")
            .AddComponent<WebViewObject>();

        webView.Init(
            cb: msg => HandleMessageFromPage(msg),
            err: msg =>
            {
                Debug.LogError("[WebView Error] " + msg);
            },
            ld: msg =>
            {
                Debug.Log("[WebView Loaded] " + msg);
                ApplyViewport();
            },
            enableWKWebView: true
        );

        // Fullscreen
        webView.SetMargins(0, 0, 0, 0);

        webView.SetVisibility(true);

        // Tunggu native WebView siap
        while (!webView.IsInitialized())
            yield return null;

        // Android: pastikan teks tidak ikut di-skala oleh pengaturan sistem
        // (no-op di platform lain).
        webView.SetTextZoom(100);

        webView.LoadURL(url);
    }

    // Paksa meta viewport "tanpa zoom" di sisi halaman, sebagai cadangan bila
    // halaman yang dimuat belum memuatnya sendiri.
    private void ApplyViewport()
    {
        if (webView == null)
            return;

        webView.EvaluateJS(
            "(function(){var m=document.querySelector('meta[name=viewport]');" +
            "if(!m){m=document.createElement('meta');m.name='viewport';" +
            "document.head.appendChild(m);}" +
            "m.setAttribute('content','" + ViewportTag + "');})();"
        );
    }

    private void Update()
    {
        FitToScreen();

#if UNITY_ANDROID && !UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (webView != null && webView.CanGoBack())
            {
                webView.GoBack();
            }
            else
            {
                CloseWebView();
            }
        }
#endif
    }

    // Ikuti ukuran layar secara dinamis, tanpa ukuran yang ditulis manual.
    //
    // Plugin menghitung ukuran WebView native dari Screen.width/height setiap
    // kali SetMargins dipanggil, tapi SetMargins keluar lebih awal kalau margin
    // tidak berubah, dan hook resize internal plugin hanya menulis ulang
    // elemen input yang sedang difokus. Akibatnya WebView tetap menempel pada
    // ukuran layar saat Init — Game View di-resize atau device dirotasi tidak
    // ikut. Paksa hitung ulang dengan margin 1px, lalu kembalikan ke fullscreen.
    // Menerima pesan dari halaman web. Plugin meneruskan apa pun yang dikirim
    // lewat window.Unity.call(...) ke callback `cb` pada Init.
    private void HandleMessageFromPage(string message)
    {
        // Plugin meloloskan log browser sebagai pesan juga, jadi jangan
        // diperlakukan semua sebagai perintah.
        if (message == CloseWebViewMessage)
        {
            CloseWebView();

            return;
        }

        if (message == RequestSessionMessage)
        {
            SendSessionToPage();

            return;
        }

        Debug.Log("[WebView] " + message);
    }

    // Menyerahkan sesi mahasiswa yang sudah login di game ke halaman ujian,
    // supaya student tidak perlu mengetik NIM + password lagi di sana.
    //
    // Sesi dikirim lewat EvaluateJS karena plugin hanya meneruskan pesan web
    // ke C#; jalan sebaliknya harus dipancing dari sini. Student yang sudah
    // logout di game dibalas null, dan halaman ujian akan membersihkan sesinya
    // sendiri - jadi logout di game ikut berlaku di web tanpa perlu
    // pemberitahuan saat WebView sudah tertutup.
    private void SendSessionToPage()
    {
        if (webView == null)
        {
            return;
        }

        string payload = AuthSession.IsLoggedIn
            ? BuildSessionPayload(
                AuthSession.AccessToken,
                AuthSession.NIM,
                AuthSession.Name,
                AuthSession.Role)
            : "null";

        // Token masuk sebagai string literal JSON supaya karakter khusus
        // (backtick, newline, tanda kutip) tidak bisa merusak sintaks JS.
        webView.EvaluateJS(
            "(function(p){" +
            "if (typeof window." + SessionHandlerName + " === 'function') {" +
            "window." + SessionHandlerName + "(JSON.parse(p));}" +
            "})(" + ToJsonString(payload) + ");");
    }

    private static string BuildSessionPayload(
        string token,
        string nim,
        string name,
        string role)
    {
        return "{" +
            "\"token\":" + ToJsonString(token) + "," +
            "\"nim\":" + ToJsonString(nim) + "," +
            "\"name\":" + ToJsonString(name) + "," +
            "\"role\":" + ToJsonString(
                string.IsNullOrEmpty(role) ? "student" : role) +
            "}";
    }

    // System.Text.Json tidak tersedia di Unity ini, jadi JSON ditulis manual.
    // Nama mahasiswa bisa mengandung tanda kutip, backslash, atau newline, dan
    // nilainya disisipkan ke EvaluateJS - tanpa escaper, satu karakter saja
    // bisa merusak sintaks dan membuat seluruh javascript gagal jalan.
    private static string ToJsonString(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        StringBuilder builder = new StringBuilder(value.Length + 8);
        builder.Append('"');

        foreach (char character in value)
        {
            switch (character)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (character < ' ')
                    {
                        builder.Append("\\u")
                            .Append(((int)character).ToString("x4"));
                    }
                    else
                    {
                        builder.Append(character);
                    }

                    break;
            }
        }

        builder.Append('"');

        return builder.ToString();
    }

    private void FitToScreen()
    {
        if (Screen.width == lastScreenWidth && Screen.height == lastScreenHeight)
            return;

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;

        if (webView == null)
            return;

        webView.SetMargins(1, 0, 0, 0);
        webView.SetMargins(0, 0, 0, 0);
    }

    public void CloseWebView()
    {
        if (webView != null)
        {
            Destroy(webView.gameObject);
            webView = null;
        }

        SceneManager.LoadScene("MainMenu");
    }

    private void OnDestroy()
    {
        if (webView != null)
        {
            Destroy(webView.gameObject);
            webView = null;
        }
    }
}