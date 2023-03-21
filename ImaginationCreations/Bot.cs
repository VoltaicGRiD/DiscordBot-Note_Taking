using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.SlashCommands;
using DSharpPlus.VoiceNext;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ImaginationCreations
{
    internal class Bot
    {
        public DiscordClient Client { get; private set; }
        public SlashCommandsExtension SlashCommands { get; private set; }
        public CommandsNextExtension Commands { get; private set; }
        public VoiceNextExtension Voice { get; private set; }
        public InteractivityExtension Interactivity { get; private set; }

        public async Task RunAsync()
        {
            var keyVaultName = Environment.GetEnvironmentVariable("KEY_VAULT_NAME");
            var kvUri = $"https://{keyVaultName}.vault.azure.net";

            var client = new SecretClient(new Uri(kvUri), new DefaultAzureCredential());
            var key = await client.GetSecretAsync("ICSecret");

            var config = new DiscordConfiguration
            {
                Token = key.Value.Value,
                TokenType = TokenType.Bot,
                AutoReconnect = true,
                MinimumLogLevel = LogLevel.Debug,
                Intents = DiscordIntents.MessageContents | DiscordIntents.AllUnprivileged | DiscordIntents.GuildMessages | DiscordIntents.GuildIntegrations | DiscordIntents.GuildMembers
            };

            var commandsConfig = new CommandsNextConfiguration
            {
                EnableDms = true
            };

            var interactivityConfig = new InteractivityConfiguration
            {
                Timeout = new TimeSpan(0, 2, 0),
                ResponseBehavior = DSharpPlus.Interactivity.Enums.InteractionResponseBehavior.Respond,
                ResponseMessage = "Your previous interaction was invalid. Please try again.",
                AckPaginationButtons = true
            };

            Client = new DiscordClient(config);
            SlashCommands = Client.UseSlashCommands();
            Commands = Client.UseCommandsNext(commandsConfig);
            Interactivity = Client.UseInteractivity(interactivityConfig);

            SlashCommands.RegisterCommands<NoteCommands>();
            SlashCommands.RegisterCommands<HelpCommands>();
            SlashCommands.RegisterCommands<JournalCommands>();
            SlashCommands.RegisterCommands<SessionCommands>();
            SlashCommands.RegisterCommands<FeedbackCommands>();

            await Client.ConnectAsync(new DSharpPlus.Entities.DiscordActivity("Taking notes..."), DSharpPlus.Entities.UserStatus.Online);

            await Task.Delay(-1);
        }
    }
}
