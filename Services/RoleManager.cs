using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Discord.Rest;
using System.Linq;
using System.Collections.Generic;

namespace Applebot.Services
{
    class RoleManager : IGatewayConsumerService
    {
        public string FriendlyName => "Ping Pong";
        string _invalid = "Unknown command.\n*Usage: `!role <create/add/remove/list>`*";

        public async Task ConsumeMessageAsync(IGatewayMessage message, CancellationToken ct)
        {
            if (message is not DiscordMessage discordMessage) { return; }
            var parts = message.Content.ToLower().Split();
            SocketGuild guild = (discordMessage.SocketMessage.Channel as SocketGuildChannel).Guild;
            if (parts.Length >= 1 && parts[0] == "!role")
            {
                if (parts.Length == 1)
                {
                    await message.RespondToSenderAsync("Allows users to create and add themselves to custom roles.\n*Usage: `!role <create, add, remove, list>`*", ct);
                    return;
                }
                if (parts.Length == 2)
                {
                    if (parts[1] == "list")
                    {
                        string roleBuffer = "Roles: ";
                        List<SocketRole> sortedRoles = guild.Roles.Where(r => r.Name.StartsWith("@")).OrderByDescending(r => r.Members.Count()).ToList();
                        foreach (SocketRole r in sortedRoles)
                        {
                            if (r.Name == "@everyone") continue;
                            string pendingRole = $"`{r.Name} [{r.Members.Count()}]` ";
                            if (roleBuffer.Length + pendingRole.Length >= 2000)
                            {
                                await message.RespondInChatAsync(roleBuffer, ct);
                                roleBuffer = pendingRole;
                            }
                            else
                            {
                                roleBuffer += pendingRole;
                            }
                        }
                        await message.RespondInChatAsync(roleBuffer, ct);
                        return;
                    }
                    var helpMessage = parts[1] switch
                    {
                        "create" => "Creates a new role and adds you to it.\n*Usage: `!role create <name of new role>`*",
                        "add" => "Adds you to an existing role. \n*Usage: `!role add <name of desired role>`*",
                        "remove" => "Removes you from a role you have.\n*Usage: `!role remove <name of unwanted role>`*",
                        _ => _invalid
                    };
                    await message.RespondToSenderAsync(helpMessage, ct);
                    return;
                }
                string target = "@" + message.Content.ToLower().Substring(parts[0].Length + parts[1].Length + 2);
                SocketRole role = guild.Roles.FirstOrDefault(r => r.Name == target);
                IGuildUser user = discordMessage.SocketMessage.Author as IGuildUser;
                if (parts[1] == "create")
                {
                    if (role != null)
                    {
                        await message.RespondToSenderAsync($"`{target}` already exists. If you want to add yourself to it, use `!role add`.", ct);
                    }
                    else
                    {
                        RestRole newrole = await guild.CreateRoleAsync(target, null, null, false, true, null);
                        await user.AddRoleAsync(newrole);
                        await message.RespondToSenderAsync($"OK! Created `{target}` and added you to it.", ct);
                    }
                    return;
                }
                if (parts[1] == "add")
                {
                    if (role != null)
                    {
                        await user.AddRoleAsync(role);
                        await message.RespondToSenderAsync($"OK! Added you to `{target}`.", ct);
                    }
                    else
                    {
                        await message.RespondToSenderAsync($"That role doesn't exist. If you want to create it, use `!role add`.", ct);
                    }
                    return;
                }
                if (parts[1] == "remove")
                {
                    await guild.DownloadUsersAsync();
                    if (role != null)
                    {
                        if (role.Members.Contains(user))
                        {
                            if (role.Members.Count() == 1)
                            {
                                await role.DeleteAsync();
                                await message.RespondToSenderAsync($"OK! You were the last person in `{target}`, so it's been deleted.", ct);
                            }
                            else
                            {
                                await user.RemoveRoleAsync(role);
                                await message.RespondToSenderAsync($"OK! You no longer have `{target}`.", ct);
                            }
                        }
                        else
                        {
                            await message.RespondToSenderAsync($"You aren't in `{target}`.", ct);
                        }
                    }
                    else
                    {
                        await message.RespondToSenderAsync($"`{target}` doesn't exist.", ct);
                    }
                    return;
                }
                if (parts[1] == "list")
                {
                    if (target == "@everyone")
                    {
                        await message.RespondToSenderAsync("Nice Try!", ct);
                        return;
                    }
                    await guild.DownloadUsersAsync();
                    if (role != null)
                    {
                        await message.RespondToSenderAsync($"Users in `{target}`: {String.Join(", ", role.Members.Select(m => m.Username))}", ct);
                    }
                    else
                    {
                        await message.RespondToSenderAsync($"`{target}` doesn't exist.", ct);
                    }
                    return;
                }
                await message.RespondToSenderAsync(_invalid, ct);
            }
        }
    }
}