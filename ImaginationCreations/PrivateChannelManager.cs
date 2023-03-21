using DSharpPlus.Entities;
using DSharpPlus.Interactivity.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ImaginationCreations
{
    public class PrivateChannelManager
    {
        DiscordDmChannel Channel;
        DiscordUser User;

        public PrivateChannelManager(DiscordDmChannel channel, DiscordUser user) 
        {
            Channel = channel;
            User = user;
        }

        public async void RunChannel()
        {
            var note = await Channel.GetNextMessageAsync(timeoutOverride: new TimeSpan(1, 0, 0));
            bool result = SqlHelper.CreateNewNote(User.Id, note.Result.Content, out string message);
            if (result)
                await Channel.SendMessageAsync("Saved");
            else
                await Channel.SendMessageAsync("You're not in a current session. Note was not saved.");

            RunChannel();
        }
    }
}
