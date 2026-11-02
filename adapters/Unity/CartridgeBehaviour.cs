// Drop this file into your Unity project's Assets/Scripts/ folder (after adding the Cartridge package).
using UnityEngine;
using Cartridge;
using Cartridge.Persistence;

namespace Cartridge.Adapters.Unity
{
    public sealed class CartridgeBehaviour : MonoBehaviour
    {
        public static CartridgeBehaviour? Instance { get; private set; }
        public CartridgeHost Host { get; private set; } = null!;

        [SerializeField] private string _defaultSaveSlot = "slot_1";
        [SerializeField] private float _comboDecaySeconds = 3.0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            var storage = new FileSaveStorage(Application.persistentDataPath);
            Host = new CartridgeHost(storage: storage, comboDecaySeconds: _comboDecaySeconds);
            Host.LoadProfile(_defaultSaveSlot);
        }

        private void Update()
        {
            Host.Tick(Time.unscaledDeltaTime);
        }

        private void OnApplicationQuit()
        {
            Host.SaveProfile(_defaultSaveSlot);
        }
    }
}
