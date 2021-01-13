﻿using System.Threading.Tasks;

using Discord;
using Discord.Commands;
using Discord.WebSocket;

using Serilog;

namespace CinemaKeeper.Service.Modules
{
    public class LockModule : ModuleBase<SocketCommandContext>
    {
        [RequireContext(ContextType.Guild)]
        [RequireBotPermission(GuildPermission.ManageChannels | GuildPermission.ManageMessages)]
        [RequireUserPermission(GuildPermission.Connect | GuildPermission.Speak)]
        [Command("lock")]
        public async Task Lock()
        {
            var voiceChannel = (Context.User as SocketGuildUser)?.VoiceChannel;
            
            if (voiceChannel is null)
            {
                await Context.Channel.SendMessageAsync("User must be in a voice channel to use this command.");
                
                return;
            }

            await voiceChannel.ModifyAsync(vcp => vcp.UserLimit = voiceChannel.Users.Count);

            Log.Debug($"Locked channel {voiceChannel} for {voiceChannel.UserLimit} users");
        }
        
        [RequireContext(ContextType.Guild)]
        [RequireBotPermission(GuildPermission.ManageChannels | GuildPermission.ManageMessages)]
        [RequireUserPermission(GuildPermission.Connect | GuildPermission.Speak)]
        [Command("lock")]
        public async Task Lock([Remainder] string membersCountRaw)
        {
            var voiceChannel = (Context.User as SocketGuildUser)?.VoiceChannel;
            var textChannel  = Context.Channel;
            
            if (voiceChannel is null)
            {
                await textChannel.SendMessageAsync("User must be in a voice channel to use this command.");
                
                return;
            }

            if (!int.TryParse(membersCountRaw, out var membersCount))
            {
                await textChannel.SendMessageAsync("Lock user limit must be an integer.");
                
                return;
            }

            if (membersCount < voiceChannel.Users.Count)
            {
                await textChannel.SendMessageAsync("Lock user limit less than current users count.");
                
                return;
            }

            await voiceChannel.ModifyAsync(vcp => vcp.UserLimit = membersCount);

            Log.Debug($"Locked channel {voiceChannel} for {voiceChannel.UserLimit} users");
        }
    }
}