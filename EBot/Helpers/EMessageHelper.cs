﻿using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Hime.Redist;
using EBot.Commands;
using EBot.Models;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.Caching;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using E;

namespace EBot.Helpers
{
    public static class EMessageHelper
    {
        public static Dictionary<ulong, EMessage> EMessages = new Dictionary<ulong, EMessage>();

        public static async Task MessageRecevied(SocketMessage msg)
        {
            if (msg.Author.IsBot || !(msg is SocketUserMessage message)) return;

            var parse = new EParser(new ELexer(msg.Content)).Parse();

            if (!parse.IsSuccess) return;

            await HandleEMessagePrompt(message, parse.Root);
        }

        public static async Task UserVoiceStateUpdated(SocketUser user, SocketVoiceState prev, SocketVoiceState curr)
        {
            if (prev.VoiceChannel != curr.VoiceChannel)
            {
                if (curr.VoiceChannel != null && curr.VoiceChannel.Id == DiscordBot.MainInstance.Options.VoiceTargetRoomId)
                {
                    await UpdateEStatuses(user.Id, EState.Ready);
                }
                else if (prev.VoiceChannel != null && prev.VoiceChannel.Id == DiscordBot.MainInstance.Options.VoiceTargetRoomId)
                {
                    await UpdateEStatuses(user.Id, EState.Done);
                }
            }
        }

        private static async Task HandleEMessagePrompt(SocketUserMessage message, ASTNode root)
        {
            var context = new BotCommandContext(DiscordBot.MainInstance.Client, message, DiscordBot.MainInstance);

            // TODO: Update time of sender based on receieved message
            DateTimeOffset? targetTime = null;

            bool existing = EMessages.Any(kvp => kvp.Value.Context.Channel.Id == context.Channel.Id);

            if (existing)
            {
                var confirmMessage = await context.Channel.SendMessageAsync("There is already an active e message in this channel. Create another?");
                ReactionMessageHelper.CreateConfirmReactionMessage(context, confirmMessage, (rm, sr) => Task.WhenAll(CreateEMessage(context, targetTime), confirmMessage.DeleteAsync()), (rm, sr) => confirmMessage.DeleteAsync());
                return;
            }

            await CreateEMessage(context, targetTime);
        }

        public static async Task CreateEMessage(BotCommandContext context, DateTimeOffset? targetTime)
        {
            EMessage emessage = new EMessage(context, targetTime, context.Bot.Options.DefaultUsers);
            await CreateEMessage(emessage);
        }

        public static async Task CreateEMessage(EMessage emessage)
        {
            var message = await emessage.Context.Channel.SendMessageAsync(embed: GenerateEmbed(emessage).Build());
            ulong messageId = message.Id;
            CreateEMessage(emessage.Context, emessage, message, messageId);
        }

        private static void CreateEMessage(BotCommandContext context, EMessage emessage, RestUserMessage message, ulong messageId)
        {
            EMessages.Add(messageId, emessage);
            ReactionMessageHelper.CreateReactionMessage(
                context,
                message,
                allowMultipleReactions: true,
                anyoneCanInteract: true,
                timeout: (int)TimeSpan.FromHours(12).TotalMilliseconds,
                actions: new List<(string, Func<ReactionMessage, SocketReaction, Task>)>
                {
                    ("<:available:706270615312662568>", (rm, sr) => UpdateEStatus(rm.Message.Id, sr.UserId, EState.Available)),
                    ("<:unavailable:706006786842296480>", (rm, sr) => UpdateEStatus(rm.Message.Id, sr.UserId, EState.Unavailable)),
                    ("<:fiveminutes:706000163738484756>", generateTimeOffsetAction(TimeSpan.FromMinutes(5))),
                    ("<:fifteenminutes:706000163562323979>", generateTimeOffsetAction(TimeSpan.FromMinutes(15))),
                    ("<:onehour:706000163688153088>", generateTimeOffsetAction(TimeSpan.FromHours(1))),
                    ("<:twohours:706000163596009514>", generateTimeOffsetAction(TimeSpan.FromHours(2))),
                    ("<:tenoclock:706000163801399346>", generateTimeSetAction(DateTimeOffset.Parse("10:00 PM"))),
                    ("<:elevenoclock:706000163142893639>", generateTimeSetAction(DateTimeOffset.Parse("11:00 PM"))),
                    ("<:twelveoclock:706000163826565200>",  generateTimeSetAction(DateTimeOffset.Parse("12:00 AM") + TimeSpan.FromDays(1)))
                },
                onTimeout: () =>
                {
                    EMessages.Remove(messageId);
                }
            );

            Func<ReactionMessage, SocketReaction, Task> generateTimeOffsetAction(TimeSpan offset)
            {
                return (rm, sr) =>
                {
                    EStatus s = EMessages[rm.Message.Id].Statuses.GetValueOrDefault(sr.UserId, EStatus.FromState(EState.Unknown));
                    return UpdateEStatus(rm.Message.Id, sr.UserId, EState.AvailableLater, (s.State == EState.AvailableLater ? s.TimeAvailable : DateTimeOffset.Now) + offset);
                };
            }

            Func<ReactionMessage, SocketReaction, Task> generateTimeSetAction(DateTimeOffset time)
            {
                return (rm, sr) => UpdateEStatus(rm.Message.Id, sr.UserId, EState.AvailableLater, time);
            }
        }

        public static void UpdateEMessages()
        {
            foreach (ulong id in EMessages.Keys.ToArray())
            {
                var emessage = EMessages.GetValueOrDefault(id);
                if (emessage == null) continue;

                var role = emessage.Context.Guild.Roles.FirstOrDefault(x => x.Name == DiscordBot.MainInstance.Options.AvailableRoleName);

                foreach (ulong userId in emessage.Statuses.Keys.ToArray())
                {
                    var status = emessage.Statuses.GetValueOrDefault(userId);
                    if (status == null) continue;

                    if (status.State == EState.AvailableLater && status.TimeAvailable < DateTimeOffset.Now && !status.ShamedForLateness)
                    {
                        status.ShamedForLateness = true;
                        string shameMessage = DiscordBot.MainInstance.Options.ShameMessages.OrderBy(x => Guid.NewGuid()).First();
                        _ = emessage.Context.Channel.SendMessageAsync(string.Format(shameMessage, $"<@{userId}>"));
                    }

                    if (role != null)
                    {
                        if (status.State == EState.Available || (status.State == EState.AvailableLater && status.TimeAvailable <= DateTimeOffset.Now))
                        {
                            _ = emessage.Context.Guild.GetUser(userId).AddRoleAsync(role);
                        }
                        else if (status.State != EState.Done)
                        {
                            _ = emessage.Context.Guild.GetUser(userId).RemoveRoleAsync(role);
                        }
                    }
                }

                _ = UpdateEMessage(id, emessage);

                if (DateTimeOffset.Now - EMessages[id].CreatedTimestamp > TimeSpan.FromHours(12))
                {
                    EMessages.Remove(id);
                }
            }
        }

        private static async Task UpdateEMessage(ulong message, EMessage emessage)
        {
            try
            {
                var msg = (IUserMessage)await emessage.Context.Channel.GetMessageAsync(message);
                if (msg != null)
                {
                    await msg.ModifyAsync((props) => props.Embed = GenerateEmbed(emessage).Build());
                }
            }
            catch { }
        }

        public static async Task UpdateEStatus(ulong messageId, ulong userId, EState state, DateTimeOffset? timeAvailable = null)
        {
            EMessage emessage = EMessages.GetValueOrDefault(messageId);
            if (emessage == null) return;

            await DiscordBot.MainInstance.Log(new LogMessage(LogSeverity.Info, "EMessageHelper", $"{userId} updated estatus to {state} at time {timeAvailable}"));

            emessage.Statuses[userId] = EStatus.FromState(state, timeAvailable);
            UpdateEMessages();
        }

        public static async Task UpdateEStatuses(ulong userId, EState state)
        {
            foreach (ulong id in EMessages.Keys.ToArray())
            {
                await UpdateEStatus(id, userId, state);
            }
        }

        private static EmbedBuilder GenerateEmbed(EMessage message)
        {
            EmbedBuilder builder = new EmbedBuilder();
            builder.Title = "eeee?";
            builder.Description = $"{message.Context.NicknameOrUsername(message.Creator)} proposes that we eeee{(message.TargetTime == null ? "" : $" at {message.TargetTime:h:mm tt}")}";

            foreach (ulong user in message.Statuses.Keys.ToArray())
            {
                string name = message.Context.NicknameOrUsername(user);
                if (name == null) continue;

                builder.AddField(name, getStatusMessage(message.Statuses[user]));
            }

            builder.WithFooter("Last Updated");
            builder.WithCurrentTimestamp();

            return builder;

            string getStatusMessage(EStatus status)
            {
                return status.State switch
                {
                    EState.Unavailable => "<:unavailable:706006786842296480> Unavailable",
                    EState.AvailableLater => getAvailableLaterStatus(status.TimeAvailable),
                    EState.Available => "<:available:706270615312662568> Available Now",
                    EState.Ready => "<:ready:706270984973451295> Ready (In Voice)",
                    EState.Done => "🔵 Done",
                    _ => "<:unknown:706271972983701524> Unknown",
                };
            }

            string getAvailableLaterStatus(DateTimeOffset time)
            {
                TimeSpan span = time - DateTimeOffset.Now;
                bool late = span.Ticks < 0;
                if (late)
                {
                    span = span.Negate();
                }

                return $"{(late ? "⏰" : "⌛")} {(span.TotalHours >= 1 ? $"{(int)span.TotalHours} hour{(span.TotalHours >= 2 ? "s" : "")} " : "")}{span.Minutes} min{(span.Minutes != 1 ? "s" : "")}{(late ? " late" : "")}";
            }
        }
    }

    public class EWalker
    {
        public DateTime Read(ASTNode node)
        {
            switch (node.Symbol.ID)
            {
                case EParser.ID.VariableTime: return ReadTime(node);
            }

            return DateTime.MinValue;
        }

        public DateTime ReadTime(ASTNode node)
        {
            var children = node.Children;

            var time = children.First();

            TimeSpan ampm;
            if (DateTime.Now.Hour < 12) ampm = new TimeSpan(0, 0, 0);
            else ampm = new TimeSpan(12, 0, 0);

            if (time.Symbol.ID == ELexer.ID.TerminalTexttime)
            {
                return DateTime.Today + ampm + TexttimeOffset(time.Value);
            }
            else
            {
                var minute = 0;

                if (children.Count == 2) Int32.Parse(children.ElementAt(1).Value);

                var hour = Int32.Parse(time.Value);

                return DateTime.Today + ampm + new TimeSpan(hour, minute, 0);
            }
        }

        public TimeSpan TexttimeOffset(string texttime)
        {
            return texttime switch
            {
                "one" => new TimeSpan(1, 0, 0),
                "two" => new TimeSpan(2, 0, 0),
                "three" => new TimeSpan(3, 0, 0),
                "four" => new TimeSpan(4, 0, 0),
                "five" => new TimeSpan(5, 0, 0),
                "six" => new TimeSpan(6, 0, 0),
                "seven" => new TimeSpan(7, 0, 0),
                "eight" => new TimeSpan(8, 0, 0),
                "nine" => new TimeSpan(9, 0, 0),
                "ten" => new TimeSpan(10, 0, 0),
                "eleven" => new TimeSpan(11, 0, 0),
                "twelve" => new TimeSpan(12, 0, 0),
                "noon" => new TimeSpan(12, 0, 0),
                "midnight" => new TimeSpan(12, 0, 0),
                _ => new TimeSpan(-1, 0, 0),
            };
        }
    }
}
