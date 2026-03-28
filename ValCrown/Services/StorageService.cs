using System.IO;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace ValCrown.Services;

public static class StorageService
{
    private static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ValCrown", "store.dat");

    private static Dictionary<string, string> _data = new();

    public static async Task Init()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            if (File.Exists(_path))
            {
                var encrypted = await File.ReadAllBytesAsync(_path);
                var json = Decrypt(encrypted);
                _data = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
            }
        }
        catch { _data = new(); }
    }

    public static string? Get(string? key)
    {
        if (key == null) return null;
        _data.TryGetValue(key, out var val);
        return val;
    }

    public static bool Set(string? key, string? value)
    {
        if (key == null) return false;
        if (value == null) { _data.Remove(key); }
        else { _data[key] = value; }
        SaveAsync().ConfigureAwait(false);
        return true;
    }

    public static bool Delete(string? key)
    {
        if (key == null) return false;
        _data.Remove(key);
        SaveAsync().ConfigureAwait(false);
        return true;
    }

    public static bool Clear()
    {
        _data.Clear();
        SaveAsync().ConfigureAwait(false);
        return true;
    }

    private static async Task SaveAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_data);
            var encrypted = Encrypt(json);
            await File.WriteAllBytesAsync(_path, encrypted);
        }
        catch { }
    }

    private static byte[] Encrypt(string text)
    {
        var key = GetKey();
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();
        var iv = aes.IV;
        using var enc = aes.CreateEncryptor();
        var data = Encoding.UTF8.GetBytes(text);
        var encrypted = enc.TransformFinalBlock(data, 0, data.Length);
        var result = new byte[iv.Length + encrypted.Length];
        Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
        Buffer.BlockCopy(encrypted, 0, result, iv.Length, encrypted.Length);
        return result;
    }

    private static string Decrypt(byte[] data)
    {
        var key = GetKey();
        using var aes = Aes.Create();
        aes.Key = key;
        var iv = new byte[16];
        Buffer.BlockCopy(data, 0, iv, 0, 16);
        aes.IV = iv;
        using var dec = aes.CreateDecryptor();
        var decrypted = dec.TransformFinalBlock(data, 16, data.Length - 16);
        return Encoding.UTF8.GetString(decrypted);
    }

    private static byte[] GetKey()
    {
        var machineId = Environment.MachineName + Environment.UserName + "ValCrown2026";
        using var sha = SHA256.Create();
        return sha.ComputeHash(Encoding.UTF8.GetBytes(machineId));
    }
}
