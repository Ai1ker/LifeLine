using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Lifeline
{
    /// <summary>Single versioned JSON slot. The backup is recovery data, never a silent replacement for a failed Continue.</summary>
    public static class GameSaveStore
    {
        [Serializable] sealed class Envelope { public int formatVersion = 1; public string checksum; public string payloadJson; }
        public static string DefaultPath => Path.Combine(Application.persistentDataPath, "LifeLine", "save-v1.json");
        static string Checksum(string payload)
        {
            using (SHA256 hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(payload)));
        }
        public static bool TryRead(string path, out GameSaveData data, out string error)
        {
            data = null; error = "";
            try
            {
                if (!File.Exists(path)) { error = "未找到存档。"; return false; }
                if (new FileInfo(path).Length > 16 * 1024 * 1024) { error = "存档文件过大，无法读取。"; return false; }
                Envelope envelope = JsonUtility.FromJson<Envelope>(File.ReadAllText(path, Encoding.UTF8));
                if (envelope == null || envelope.formatVersion != 1 || string.IsNullOrEmpty(envelope.payloadJson) || string.IsNullOrEmpty(envelope.checksum))
                { error = "存档格式或版本不受支持。"; return false; }
                if (!string.Equals(envelope.checksum, Checksum(envelope.payloadJson), StringComparison.Ordinal))
                { error = "存档校验失败。原文件和备份均未修改。"; return false; }
                GameSaveData parsed = JsonUtility.FromJson<GameSaveData>(envelope.payloadJson);
                if (!GameSaveValidation.Validate(parsed, out error)) return false;
                data = parsed; return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            { error = "读取存档失败：" + exception.Message; return false; }
        }
        public static bool TryWrite(string path, GameSaveData data, out string error)
        {
            error = "";
            if (!GameSaveValidation.Validate(data, out error)) return false;
            string temporary = path + ".tmp";
            try
            {
                string directory = Path.GetDirectoryName(Path.GetFullPath(path));
                Directory.CreateDirectory(directory);
                string payload = JsonUtility.ToJson(data);
                string json = JsonUtility.ToJson(new Envelope { payloadJson = payload, checksum = Checksum(payload) }, true);
                byte[] bytes = new UTF8Encoding(false).GetBytes(json);
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (!TryRead(temporary, out _, out error)) return false;
                if (File.Exists(path)) File.Replace(temporary, path, path + ".bak", true);
                else File.Move(temporary, path);
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException || exception is NotSupportedException)
            { error = "写入存档失败：" + exception.Message; return false; }
            finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        }
    }
}
