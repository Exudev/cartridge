// Drop this file into your Godot 4 (.NET) project and register it under Project Settings -> Globals -> Autoload.
using Godot;
using Cartridge;
using Cartridge.Persistence;

namespace Cartridge.Adapters.Godot
{
    public partial class CartridgeAutoload : Node
    {
        public static CartridgeAutoload? Instance { get; private set; }
        public CartridgeHost Host { get; private set; } = null!;

        [Export] public string DefaultSaveSlot { get; set; } = "slot_1";
        [Export] public float ComboDecaySeconds { get; set; } = 3.0f;

        public override void _Ready()
        {
            Instance = this;
            ProcessMode = ProcessModeEnum.Always; // Let CartridgeHost handle IsPaused and UnscaledTimers

            var storage = new FileSaveStorage(OS.GetUserDataDir());
            Host = new CartridgeHost(storage: storage, comboDecaySeconds: ComboDecaySeconds);
            Host.LoadProfile(DefaultSaveSlot);
        }

        public override void _Process(double delta)
        {
            Host.Tick((float)delta);
        }

        public override void _Notification(int what)
        {
            if (what == NotificationWMCloseRequest)
            {
                Host?.SaveProfile(DefaultSaveSlot);
            }
        }
    }
}
