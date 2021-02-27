using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using Discord;
using Discord.WebSocket;

namespace Applebot.Services
{
    class Quote
    {
        public string response { get; set; }
        public string added_by { get; set; }

        public Quote(string r, string a)
        {
            response = r;
            added_by = a;
        }
    }

    class Quotes : IGatewayConsumerService
    {
        public string FriendlyName => "Quotes";
        public List<Quote> quotes { get; set; }
        public Random random = new Random();
        public string path;
        public async Task InitializeAsync(CancellationToken ct)
        {
            var resources = ResourceResolver.RuntimeDataDirectory;
            path = Path.Combine(resources.FullName, "Quotes.json");
            if (!File.Exists(path))
            {
                quotes = new List<Quote>();
                Save();
            }
            else
            {
                var json = await File.ReadAllTextAsync(path);
                quotes = JsonConvert.DeserializeObject<List<Quote>>(json).ToList();
            }
        }

        public string PrettyQuote(int index)
        {
            return $"(#{index + 1}) {quotes[index].response}";
        }

        public async void Save()
        {
            await File.WriteAllTextAsync(path, JsonConvert.SerializeObject(quotes));
        }

        public async Task ConsumeMessageAsync(IGatewayMessage message, CancellationToken ct)
        {
            var parts = message.Content.ToLower().Split();
            if (parts[0] != "!quote") { return; }

            bool elevated = false;
            if (message is DiscordMessage discordMessage) {
                var sm = discordMessage.SocketMessage;
                var author = sm.Author as SocketGuildUser;
                elevated = author.GuildPermissions.BanMembers;

            switch (parts.Count())
            {
                case 1:
                    if (quotes.Count() == 0)
                    {
                        await message.RespondToSenderAsync("No quotes.", ct);
                        return;
                    }
                    await message.RespondToSenderAsync(PrettyQuote(random.Next(quotes.Count())), ct);
                    break;
                case 2:
                    string arg = parts[1];
                    if (arg == "count")
                    {
                        await message.RespondToSenderAsync($"There are {quotes.Count()} quotes.", ct);
                        break;
                    } else if (arg == "undo" && elevated) {
                        quotes.RemoveAt(quotes.Count()-1);
                        Save();
                        await message.RespondToSenderAsync($"Removed quote #{quotes.Count()+1}.", ct);
                        break;
                    }
                    if (arg[0] == '#')
                    {
                        int target;
                        bool valid = Int32.TryParse(arg.Substring(1), out target);
                        if (valid)
                        {
                            if (target > 0 && target <= quotes.Count())
                            {
                                await message.RespondToSenderAsync(PrettyQuote(target - 1), ct);
                                break;
                            }
                            else
                            {
                                await message.RespondToSenderAsync($"Out of range. There are {quotes.Count()} quotes.", ct);
                                break;
                            }
                        }
                    }
                    else
                    {
                        for (int q = 0; q < quotes.Count(); q++)
                        {
                            if (quotes[q].response.Contains(arg))
                            {
                                await message.RespondToSenderAsync(PrettyQuote(q), ct);
                                return;
                            }
                        }
                        await message.RespondToSenderAsync("Couldn't find a quote matching that text. Try something like `!quote #69` if you want a specific number.", ct);
                    }
                    break;
                default:
                    if (parts.Count() >= 3 && elevated)
                    {
                        string command = parts[1];
                        string payload = String.Join(" ", parts.Skip(2));
                        switch (command)
                        {
                            case "add":
                                quotes.Add(new Quote(payload, "TODO"));
                                Save();
                                await message.RespondToSenderAsync($"Added quote #{quotes.Count()}.", ct);
                                break;
                            case "remove":
                                int target;
                                bool valid = Int32.TryParse(payload, out target);
                                if (valid)
                                {
                                    if (target > 0 && target <= quotes.Count())
                                    {
                                        quotes.RemoveAt(target - 1);
                                        Save();
                                        await message.RespondToSenderAsync($"Removed quote #{target}.", ct);
                                        break;
                                    }
                                }
                                await message.RespondToSenderAsync($"Out of range. There are {quotes.Count()} quotes.", ct);
                                break;
                            default:
                                await message.RespondToSenderAsync("?", ct);
                                break;
                        }
                    }
                    break;
            }
        }
    }
}