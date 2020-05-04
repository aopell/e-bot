using Discord;
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
            
            bool existing = EMessages.Any(kvp => kvp.Value.Context.Channel.Id == context.Channel.Id);

            var senderStatus = EWalker.Read(root);

            if (existing)
            {
                var confirmMessage = await context.Channel.SendMessageAsync("There is already an active e message in this channel. Create another?");
                ReactionMessageHelper.CreateConfirmReactionMessage(context, confirmMessage, 
                                                                   (rm, sr) => Task.WhenAll(CreateEMessage(context, senderStatus), confirmMessage.DeleteAsync()), 
                                                                   (rm, sr) => confirmMessage.DeleteAsync());
            }
            else
            {
                await CreateEMessage(context, senderStatus);
            }
        }

        public static async Task CreateEMessage(BotCommandContext context, EStatus senderStatus)
        {
            EMessage emessage = new EMessage(context, senderStatus, context.Bot.Options.DefaultUsers);

            var role = emessage.Context.Guild.Roles.FirstOrDefault(x => x.Name == DiscordBot.MainInstance.Options.AvailableRoleName);
            if (role != null)
            {
                foreach (ulong userId in emessage.Statuses.Keys)
                {
                    _ = emessage.Context.Guild.GetUser(userId).RemoveRoleAsync(role);
                }
            }

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
                    ("<:maybe:706702223446376517>", (rm, sr) => UpdateEStatus(rm.Message.Id, sr.UserId, EState.Maybe)),
                    ("<:unavailable:706702240467124345>", (rm, sr) => UpdateEStatus(rm.Message.Id, sr.UserId, EState.Unavailable)),
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
                        _ = emessage.Context.Guild.GetUser(userId).AddRoleAsync(role);
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
        
        public static async Task UpdateEStatus(ulong messageId, ulong userId, EState state)
        {
            await UpdateEStatus(messageId, userId, EStatus.FromState(state));
        }

        public static async Task UpdateEStatus(ulong messageId, ulong userId, EState state, DateTimeOffset timeAvailable)
        {
            await UpdateEStatus(messageId, userId, EStatus.FromState(state, timeAvailable));
        }
        
        public static async Task UpdateEStatus(ulong messageId, ulong userId, EStatus status)
        {
            EMessage emessage = EMessages.GetValueOrDefault(messageId);
            if (emessage == null) return;

            await DiscordBot.MainInstance.Log(new LogMessage(LogSeverity.Info, "UpdateEStatus", $"{userId} updated estatus to {status.State} at time {status.TimeAvailable}"));

            emessage.Statuses[userId] = status;

            var role = emessage.Context.Guild.Roles.FirstOrDefault(x => x.Name == DiscordBot.MainInstance.Options.AvailableRoleName);
            if (role != null)
            {
                if (status.State == EState.Available)
                {
                    _ = emessage.Context.Guild.GetUser(userId).AddRoleAsync(role);
                }
                else
                {
                    _ = emessage.Context.Guild.GetUser(userId).RemoveRoleAsync(role);
                }
            }

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

            builder.Color = getEmbedColor();
            builder.WithFooter("Last Updated");
            builder.WithCurrentTimestamp();

            return builder;

            string getStatusMessage(EStatus status)
            {
                return status.State switch
                {
                    EState.Unavailable => "<:unavailable:706702240467124345> Unavailable",
                    EState.Maybe => "<:maybe:706702223446376517> Maybe Later",
                    EState.AvailableLater => getAvailableLaterStatus(status.TimeAvailable),
                    EState.Available => "<:available:706270615312662568> Available Now",
                    EState.Ready => "<:ready:706270984973451295> Ready (In Voice)",
                    EState.Done => $"<:sleep:706705461486944348> {(status.TimeUpdated.Hour >= 20 || status.TimeUpdated.Hour <= 5 ? "Sleeeep" : "eeeed")}",
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

            Color getEmbedColor()
            {
                var colorMap = new Dictionary<EState, Color>
                {
                    [EState.Unknown] = new Color(0x7a7a7a),
                    [EState.Unavailable] = new Color(0xef5a73),
                    [EState.Maybe] = new Color(0xffac33),
                    [EState.Available] = new Color(0x226699),
                    [EState.Ready] = new Color(0x2cd261),
                    [EState.Done] = new Color(0x9241d4)
                };

                var colors = new List<Color>();
                foreach (var status in message.Statuses.Values)
                {
                    if (status.State == EState.AvailableLater)
                    {
                        double weight = (double)(DateTimeOffset.Now - status.TimeUpdated).Ticks / (status.TimeAvailable - status.TimeUpdated).Ticks;
                        colors.Add(getWeightedAverageColor(colorMap[EState.Maybe], colorMap[EState.Available], weight));
                    }
                    else
                    {
                        colors.Add(colorMap[status.State]);
                    }
                }

                return getAverageColor(colors);
            }

            static Color getWeightedAverageColor(Color c1, Color c2, double w)
            {
                w = Math.Clamp(w, 0d, 1d);
                return new Color((byte)Math.Sqrt(c1.R * c1.R * (1 - w) + c2.R * c2.R * w), (byte)Math.Sqrt(c1.G * c1.G * (1 - w) + c2.G * c2.G * w), (byte)Math.Sqrt(c1.B * c1.B * (1 - w) + c2.B * c2.B * w));
            }

            static Color getAverageColor(IEnumerable<Color> colors)
            {
                double r = 0;
                double g = 0;
                double b = 0;
                int count = 0;
                foreach (Color color in colors)
                {
                    count++;
                    r += Math.Pow(color.R, 2);
                    g += Math.Pow(color.G, 2);
                    b += Math.Pow(color.B, 2);
                }
                r /= count;
                g /= count;
                b /= count;
                return new Color((byte)Math.Sqrt(r), (byte)Math.Sqrt(g), (byte)Math.Sqrt(b));
            }
        }
    }

    public static class EWalker
    {
        public static EStatus Read(ASTNode node)
        {
            return node.Symbol.ID switch
            {
                EParser.ID.VariableTime => ReadTime(node),
                EParser.ID.VariableIn => ReadIn(node),
                EParser.ID.VariableNow => EStatus.FromState(EState.Available),
                _ => EStatus.FromState(EState.Unknown)
            };
        }

        public static EStatus ReadIn(ASTNode node)
        {
            ASTNode child = node.Children.First();

            return child.Symbol.ID switch
            {
                EParser.ID.VariableN => Read(child),
                _ => EStatus.FromState(EState.Unknown)
            };
        }

        public static EStatus ReadN(ASTNode node)
        {
            ASTNode number = node.Children[0];

            int time = int.Parse(number.Value);

            ASTNode timeInterval = node.Children[1];
            
            return EStatus.FromState(EState.AvailableLater, DateTimeOffset.Now + (timeInterval.Symbol.ID == EParser.ID.VariableNhours ? TimeSpan.FromHours(time) : TimeSpan.FromMinutes(time)));
        }
        
        public  static EStatus ReadTime(ASTNode node)
        {
            ASTFamily children = node.Children;

            ASTNode time = children.First();

            TimeSpan ampm = DateTime.Now.Hour < 12 ? new TimeSpan(0, 0, 0) : new TimeSpan(12, 0, 0);

            if (time.Symbol.ID == ELexer.ID.TerminalTexttime)
            {
                return EStatus.FromState(EState.AvailableLater, DateTime.Today + ampm + TexttimeOffset(time.Value));
            }
            
            int minute = 0;

            if (children.Count == 2) minute = int.Parse(children.ElementAt(1).Value);

            int hour = int.Parse(time.Value);

            return EStatus.FromState(EState.AvailableLater, DateTime.Today + ampm + new TimeSpan(hour, minute, 0));
        }

        public static TimeSpan TexttimeOffset(string texttime)
        {
            return texttime switch
            {
                "one" => TimeSpan.FromHours(1),
                "two" => TimeSpan.FromHours(2),
                "three" => TimeSpan.FromHours(3),
                "four" => TimeSpan.FromHours(4),
                "five" => TimeSpan.FromHours(5),
                "six" => TimeSpan.FromHours(6),
                "seven" => TimeSpan.FromHours(7),
                "eight" => TimeSpan.FromHours(8),
                "nine" => TimeSpan.FromHours(9),
                "ten" => TimeSpan.FromHours(10),
                "eleven" => TimeSpan.FromHours(11),
                "twelve" => TimeSpan.FromHours(12),
                "noon" => TimeSpan.FromHours(12),
                "midnight" => TimeSpan.FromHours(12),
                _ => TimeSpan.FromHours(-1)
            };
        }
    }
}
