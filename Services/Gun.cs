using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json;
using Discord;
using Discord.WebSocket;

namespace Applebot.Services
{
    class GunConfiguration
    {
        [JsonRequired]
        public string noDM { get; set; }
        public string ban {get; set;}
        public bool loaded {get; set;}
        public List<GunServer> servers { get; set; }
    }

    class GunServer
    {
        public string Id { get; set; }
        public string Message { get; set; }
    }

    class Gun : IGatewayConsumerService
    {
        public string FriendlyName => "Gun";
        public GunConfiguration config;

        public async Task InitializeAsync(CancellationToken ct)
        {
            config = await ConfigurationResolver.LoadConfigurationAsync<Gun, GunConfiguration>();
        }

        public async Task ConsumeMessageAsync(IGatewayMessage message, CancellationToken ct)
        {
            if (message is not DiscordMessage discordMessage) { return; }
            if (discordMessage.SocketMessage.Channel is not SocketGuildChannel channel) { return; }
            var targetServer = config.servers.FirstOrDefault(x => x.Id == channel.Guild.Id.ToString());
            if (targetServer == null) { return; }
            var parts = message.Content.ToLower().Split();
            if (parts[0] == ".gun" || parts[0] == "$gun")
            {
                var sm = discordMessage.SocketMessage;
                var author = sm.Author as SocketGuildUser;
                var guild = channel.Guild;
                await guild.DownloadUsersAsync();
                if (!author.GuildPermissions.BanMembers) { return; }
                if (parts.Count() == 1)
                {
                    await message.RespondToSenderAsync("Usage: `.gun MENTION/ID [BAN REASON]`", ct);
                    return;
                }
                SocketUser target;
                var reason = (parts.Count() > 2) ? message.Content.Substring(parts[0].Length + parts[1].Length + 2) : "No info provided.";
                var privateReason = reason.Length > 200 ? reason.Substring(0, 200) : reason;
                privateReason += " - From " + author.Username;
                if (sm.MentionedUsers.FirstOrDefault() != null)
                {
                    target = guild.GetUser(sm.MentionedUsers.FirstOrDefault().Id);
                }
                else
                {
                    UInt64 id;
                    if (!UInt64.TryParse(parts[1], out id))
                    {
                        await message.RespondToSenderAsync("First argument doesn't seem like an ID?", ct);
                        return;
                    }
                    target = guild.GetUser(id);
                    if (target == null)
                    {
                        if (config.loaded) {
                            await guild.AddBanAsync(id, 0, privateReason);
                        }
                        await message.RespondToSenderAsync($"Preemptively banned <@!{id.ToString()}>.", ct);
                        return;
                    }
                }
                var gunMe = target as SocketGuildUser;
                if (gunMe.GuildPermissions.BanMembers)
                {
                    await message.RespondToSenderAsync("Can't ban someone with ban privileges: footgun prevention.", ct);
                    return;
                }
                try
                {
                    var dm = await target.GetOrCreateDMChannelAsync();
                    await dm.SendMessageAsync(targetServer.Message);
                    if (parts.Count() > 2)
                    {
                        await dm.SendMessageAsync($"Additional information: {reason}");
                    }
                }
                catch
                {
                    await message.RespondToSenderAsync(config.noDM, ct);
                }
                try {
                    var banCelebration = config.ban.Replace("$USER", target.Username + " [ID: " + target.Id + "]");
                    if (config.loaded) {
                        await guild.AddBanAsync(gunMe, 0, privateReason);
                    }
                    await message.RespondToSenderAsync(banCelebration, ct);
                } catch (Exception e) {
                    await message.RespondToSenderAsync("Ban failed. Check permissions?", ct);
                    await message.RespondInChatAsync("```\n" + e.ToString() + "```", ct);
                }
            }
        }
    }
}