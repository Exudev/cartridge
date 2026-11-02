using System;
using System.Collections.Generic;
using System.IO;

namespace Cartridge.Persistence
{
    /// <summary>
    /// Engine-agnostic storage abstraction. Implement this with Unity's PlayerPrefs/Application.persistentDataPath
    /// or Godot's FileAccess ("user://") or use the built-in FileSaveStorage / InMemorySaveStorage.
    /// </summary>
    public interface ISaveStorage
    {
        void WriteString(string key, string data);
        bool TryReadString(string key, out string data);
        bool Exists(string key);
        void Delete(string key);
    }

    public sealed class InMemorySaveStorage : ISaveStorage
    {
        private readonly Dictionary<string, string> _store = new Dictionary<string, string>();

        public void WriteString(string key, string data) => _store[key] = data;
        public bool TryReadString(string key, out string data) => _store.TryGetValue(key, out data!);
        public bool Exists(string key) => _store.ContainsKey(key);
        public void Delete(string key) => _store.Remove(key);
    }

    public sealed class FileSaveStorage : ISaveStorage
    {
        private readonly string _baseDirectory;

        public FileSaveStorage(string baseDirectory)
        {
            _baseDirectory = baseDirectory ?? throw new ArgumentNullException(nameof(baseDirectory));
            if (!Directory.Exists(_baseDirectory))
            {
                Directory.CreateDirectory(_baseDirectory);
            }
        }

        public void WriteString(string key, string data)
        {
            File.WriteAllText(GetPath(key), data);
        }

        public bool TryReadString(string key, out string data)
        {
            string path = GetPath(key);
            if (!File.Exists(path))
            {
                data = string.Empty;
                return false;
            }
            data = File.ReadAllText(path);
            return true;
        }

        public bool Exists(string key) => File.Exists(GetPath(key));

        public void Delete(string key)
        {
            string path = GetPath(key);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private string GetPath(string key)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                key = key.Replace(invalid, '_');
            }
            return Path.Combine(_baseDirectory, key + ".sav");
        }
    }
}
