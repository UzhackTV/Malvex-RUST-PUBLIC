using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("SimpleDonateBroadcast", "OpenAI", "1.0.0")]
    [Description("Broadcasts donate messages to all online players on a fixed interval.")]
    public class SimpleDonateBroadcast : RustPlugin
    {
        private PluginConfig config;
        private Timer broadcastTimer;
        private int messageIndex = 0;

        private class PluginConfig
        {
            public float IntervalSeconds = 3600f;
            public List<string> Messages = new List<string>
            {
                "<color=#ffd479>[DONATE]</color> Serverni qo'llab-quvvatlash uchun donate mavjud. Discord admin ghostuz ga yozing!",
                "<color=#55ff88>[VIP]</color> VIP va bonuslar uchun Discord admin ghostuz ga yozing!",
                "<color=#ff6666>[SUPPORT]</color> Donate qilganingizdan keyin admin ghostuz ga xabar bering!"
            };
        }

        protected override void LoadDefaultConfig()
        {
            config = new PluginConfig();
            SaveConfig();
        }

        private void Init()
        {
            LoadConfigValues();
        }

        private void OnServerInitialized()
        {
            StartBroadcastTimer();
        }

        private void Unload()
        {
            DestroyBroadcastTimer();
        }

        [ChatCommand("donatetest")]
        private void DonateTestCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null || !player.IsAdmin)
            {
                return;
            }

            BroadcastNextMessage();
            player.ChatMessage("<color=#55aaff>[SimpleDonateBroadcast]</color> Test xabar yuborildi.");
        }

        [ConsoleCommand("donate.test")]
        private void DonateTestConsoleCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Player() != null && !arg.Player().IsAdmin)
            {
                return;
            }

            BroadcastNextMessage();
            Puts("Test xabar yuborildi.");
        }

        [ConsoleCommand("donate.reloadcfg")]
        private void DonateReloadConfigCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Player() != null && !arg.Player().IsAdmin)
            {
                return;
            }

            LoadConfigValues();
            StartBroadcastTimer();
            Puts("SimpleDonateBroadcast config qayta yuklandi.");
        }

        private void StartBroadcastTimer()
        {
            DestroyBroadcastTimer();

            if (config == null)
            {
                LoadConfigValues();
            }

            if (config.IntervalSeconds <= 0f)
            {
                PrintWarning("IntervalSeconds 0 dan katta bo'lishi kerak.");
                return;
            }

            broadcastTimer = timer.Every(config.IntervalSeconds, BroadcastNextMessage);
            Puts("Broadcast timer ishga tushdi. Interval: " + config.IntervalSeconds + " sekund.");
        }

        private void DestroyBroadcastTimer()
        {
            if (broadcastTimer != null && !broadcastTimer.Destroyed)
            {
                broadcastTimer.Destroy();
            }

            broadcastTimer = null;
        }

        private void BroadcastNextMessage()
        {
            if (config == null || config.Messages == null || config.Messages.Count == 0)
            {
                return;
            }

            if (messageIndex >= config.Messages.Count)
            {
                messageIndex = 0;
            }

            string message = config.Messages[messageIndex];
            messageIndex++;

            var players = BasePlayer.activePlayerList;
            var playerCount = players.Count;

            for (int i = 0; i < playerCount; i++)
            {
                BasePlayer player = players[i];
                if (player == null || !player.IsConnected)
                {
                    continue;
                }

                player.ChatMessage(message);
            }
        }

        private void LoadConfigValues()
        {
            try
            {
                config = Config.ReadObject<PluginConfig>();
                if (config == null)
                {
                    throw new System.Exception("Config bo'sh.");
                }

                if (config.Messages == null)
                {
                    config.Messages = new List<string>();
                }

                SaveConfig();
            }
            catch
            {
                PrintWarning("Config o'qishda xato. Default config yaratildi.");
                LoadDefaultConfig();
            }
        }

        private void SaveConfig()
        {
            Config.WriteObject(config, true);
        }
    }
}