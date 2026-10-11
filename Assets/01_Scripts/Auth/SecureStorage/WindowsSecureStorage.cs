#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN

using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

public class WindowsSecureStorage : ISecureStorage
{
    private const string FileName = "auth.dat";

    private string FilePath =>
        Path.Combine(Application.persistentDataPath, FileName);

    public bool HasRefreshToken()
    {
        return File.Exists(FilePath);
    }

    public bool SaveRefreshToken(string refreshToken)
    {
        try
        {
            if (string.IsNullOrEmpty(refreshToken))
                return false;

            byte[] plainBytes =
                Encoding.UTF8.GetBytes(refreshToken);

            byte[] protectedBytes =
                Protect(plainBytes);

            File.WriteAllBytes(
                FilePath,
                protectedBytes
            );

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"Gagal menyimpan refresh token Windows: {ex}"
            );

            return false;
        }
    }

    public string GetRefreshToken()
    {
        try
        {
            if (!File.Exists(FilePath))
                return null;

            byte[] protectedBytes =
                File.ReadAllBytes(FilePath);

            byte[] plainBytes =
                Unprotect(protectedBytes);

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"Gagal membaca refresh token Windows: {ex}"
            );

            return null;
        }
    }

    public bool DeleteRefreshToken()
    {
        try
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"Gagal menghapus refresh token Windows: {ex}"
            );

            return false;
        }
    }

    private static byte[] Protect(byte[] data)
    {
        DataBlob input = CreateBlob(data);

        try
        {
            if (!CryptProtectData(
                    ref input,
                    null,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    CryptProtectUiForbidden,
                    out DataBlob output))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error());
            }

            return CopyAndFree(output);
        }
        finally
        {
            Marshal.FreeHGlobal(input.data);
        }
    }

    private static byte[] Unprotect(byte[] data)
    {
        DataBlob input = CreateBlob(data);

        try
        {
            if (!CryptUnprotectData(
                    ref input,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    CryptProtectUiForbidden,
                    out DataBlob output))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error());
            }

            return CopyAndFree(output);
        }
        finally
        {
            Marshal.FreeHGlobal(input.data);
        }
    }

    private static DataBlob CreateBlob(byte[] data)
    {
        IntPtr pointer =
            Marshal.AllocHGlobal(data.Length);

        Marshal.Copy(data, 0, pointer, data.Length);

        return new DataBlob
        {
            size = data.Length,
            data = pointer
        };
    }

    private static byte[] CopyAndFree(DataBlob blob)
    {
        try
        {
            byte[] data = new byte[blob.size];
            Marshal.Copy(blob.data, data, 0, blob.size);
            return data;
        }
        finally
        {
            LocalFree(blob.data);
        }
    }

    private const int CryptProtectUiForbidden = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int size;
        public IntPtr data;
    }

    [DllImport("Crypt32.dll", CharSet = CharSet.Unicode,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(
        ref DataBlob dataIn,
        string description,
        IntPtr optionalEntropy,
        IntPtr reserved,
        IntPtr promptStruct,
        int flags,
        out DataBlob dataOut);

    [DllImport("Crypt32.dll", CharSet = CharSet.Unicode,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(
        ref DataBlob dataIn,
        IntPtr description,
        IntPtr optionalEntropy,
        IntPtr reserved,
        IntPtr promptStruct,
        int flags,
        out DataBlob dataOut);

    [DllImport("Kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}

#endif
