using Azure;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.SlashCommands;
using ImaginationCreations.Models;
using Microsoft.Extensions.Azure;
using MySql.Data.MySqlClient;
using MySqlX.XDevAPI;
using MySqlX.XDevAPI.Common;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace ImaginationCreations
{
    [SlashCommandGroup("tools", "Various tools to aid in usage of the bot")]
    public class ToolCommands : ApplicationCommandModule
    {
        [SlashCommand("date", "Outputs the date format used for searching for notes and journal entries")]
        public async Task DateTool(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync($"Date format appears like this (yy-MM-dd HH:mm:ss). Hours are in 24-hour format. Todays date and time: {DateTime.Now.ToString("yy-MM-dd HH:mm:ss")}", true);
        }
    }

    [SlashCommandGroup("journal", "Journaling commands")]
    public class JournalCommands : ApplicationCommandModule
    {
        [SlashCommand("new", "Adds a new journal entry (same as '/journal add')")]
        public async Task NewEntry(InteractionContext ctx, [Option("title", "The title for the journal entry", true)] string title, [Option("content", "The content of the journal entry", true)] string content)
        {
            var result = SqlHelper.CreateNewEntry(ctx.Member.Username, title, content);
            await ctx.CreateResponseAsync($"Journal entry {(result ? "created successfully!" : "could not be created. Please try again.")}", true);
        }

        [SlashCommand("add", "Adds a new journal entry (same as '/journal new')")]
        public async Task AddEntry(InteractionContext ctx, [Option("title", "The title for the journal entry", true)] string title, [Option("content", "The content of the journal entry", true)] string content)
        {
            var result = SqlHelper.CreateNewEntry(ctx.Member.Username, title, content);
            await ctx.CreateResponseAsync($"Journal entry {(result ? "created successfully!" : "could not be created. Please try again.")}", true);
        }

        [SlashCommand("search", "Searches all of your journal entries for a word or phrase")]
        public async Task Search(InteractionContext ctx, 
            [Option("search", "A word or phrase to search your journal for", true)] string search,
            [Option("file", "Set to true to output the results to a file")] bool file,
            [Option("soft", "Searches softly, this can take a while and return a lot of results.")] bool softly = false)
        {
            await ctx.CreateResponseAsync("Retrieving search results... Please wait...", true);

            var notes = SqlHelper.SearchJournal(ctx.User.Id, search, softly);

            if (notes.Count == 1)
            {
                var pages = new List<Page>();
                var embed = new DiscordEmbedBuilder();
                embed.Color = DiscordColor.Green;
                embed.Title = notes[0].Title;
                embed.AddField(notes[0].Created.ToString("yyyy-MM-dd HH:mm:ss"), "Created at");
                pages.Add(new Page(content: "Go to the next page to view note content", embed: embed));
                var contentPages = ctx.Client.GetInteractivity().GeneratePagesInContent(notes[0].Content);
                pages.AddRange(contentPages);
                await ctx.Interaction.SendPaginatedResponseAsync(true, ctx.User, pages, null, null, null, default, true);
            }
            else
            {
                if (file)
                {
                    StringBuilder builder = new StringBuilder();
                    foreach (var note in notes)
                    {
                        builder.AppendLine("TITLE: " + note.Title);
                        builder.AppendLine("CREATED: " + note.Created.ToString("yyyy-MM-dd HH:mm:ss"));
                        builder.AppendLine();
                        builder.AppendLine(note.Content);
                        builder.AppendLine();
                        builder.AppendLine("================================================================================");
                        builder.AppendLine();
                    }
                    var content = builder.ToString();
                    var path = Guid.NewGuid() + ".txt";
                    File.WriteAllText(path, content);
                    var stream = File.OpenRead(path);
                    var response = new DiscordWebhookBuilder();
                    response.AddFile(stream);
                    await ctx.Interaction.EditOriginalResponseAsync(response);
                }
                else
                {
                    var pages = new List<Page>();
                    for (int x = 1; x <= notes.Count; x++)
                    {
                        var embed = new DiscordEmbedBuilder();
                        embed.Color = DiscordColor.Green;
                        embed.Title = $"{notes[x - 1].Title} ({x} of {notes.Count})";
                        embed.AddField(notes[x - 1].Created.ToString("yyyy-MM-dd HH:mm:ss"), "Created at");
                        pages.Add(new Page(content: "Go to the next page to view note content", embed: embed));
                        var contentPages = ctx.Client.GetInteractivity().GeneratePagesInContent(notes[x - 1].Content);
                        pages.AddRange(contentPages);
                    }
                    await ctx.Interaction.SendPaginatedResponseAsync(true, ctx.User, pages, null, null, null, default, true);
                }
            }
        }

        [SlashCommand("get", "Gets a journal entry by title or date")]
        public async Task GetEntry(InteractionContext ctx, [Option("search", "Either the title or date for the entry to retrieve", true)] string search)
        {
            var entries = SqlHelper.GetEntry(ctx.Member.Username, search);
            if (entries.Count > 1)
                await ctx.CreateResponseAsync($"Multiple results were returned. There were: {entries.Count} results. Please narrow your search.");
            else
            {
                var builder = new DiscordInteractionResponseBuilder();
                var embed = new DiscordEmbedBuilder();
                embed.AddField("Entry Title", entries[0].Title);
                try { embed.AddField("Entry Content", entries[0].Content); }
                catch (ArgumentException exc)
                {
                    var guid = Guid.NewGuid() + ".txt";
                    File.WriteAllText(guid, entries[0].Content);
                    var data = new FileStream(guid, FileMode.Open);
                    builder.AddFile(data);
                }
                embed.AddField("Date & Time Created", entries[0].Created.ToString("yy-MM-dd HH:mm:ss"));
                builder.AddEmbed(embed);
                builder.AsEphemeral();
                await ctx.CreateResponseAsync(builder);
            }
        }

        [SlashCommand("last", "Gets the last journal entry you submitted")]
        public async Task GetLast(InteractionContext ctx)
        {
            //ctx.DeferAsync(true);
            var entry = SqlHelper.GetLastEntry(ctx.Member.Username);
            var builder = new DiscordInteractionResponseBuilder();
            var embed = new DiscordEmbedBuilder();
            embed.AddField("Entry Title", entry.Title);
            try { embed.AddField("Entry Content", entry.Content); }
            catch (ArgumentException exc)
            {
                var guid = Guid.NewGuid() + ".txt";
                File.WriteAllText(guid, entry.Content);
                var data = new FileStream(guid, FileMode.Open);
                builder.AddFile(data);
            }
            embed.AddField("Date & Time Created", entry.Created.ToString("yy-MM-dd HH:mm:ss"));
            builder.AddEmbed(embed);
            builder.AsEphemeral();
            await ctx.CreateResponseAsync(builder);
        }

        [SlashCommand("upload", "Creates a journal entry based off of an uploaded text file. The file name is the journal's title.")]
        public async Task UploadEntry(InteractionContext ctx, [Option("file", "The journal entry file you wish to upload")] DiscordAttachment att)
        {
            var title = att.FileName;
            var url = att.Url;
            var guid = Guid.NewGuid();
            using (var client = new WebClient())
            {
                client.DownloadFile(url, guid + ".txt");
            }
            var data = File.ReadAllText(guid + ".txt");
            var result = SqlHelper.CreateNewEntry(ctx.Member.Username, title, data);
            await ctx.CreateResponseAsync($"Journal entry {(result ? "created successfully!" : "could not be created. Please try again.")}", true);
        }
    }

    [SlashCommandGroup("session", "Manages sessions for notes and other features")]
    public class SessionCommands : ApplicationCommandModule
    {
        [SlashCommand("new", "Creates a new session for note taking (Admin / GM only)")]
        public async Task NewSession(InteractionContext ctx,
            [Option("game", "The campaign to create a new session for", true)] string game,
            [Option("name", "The name of the game session", true)] string name)
        {
            var result = SqlHelper.CreateNewSession(game, name, out int sessionId);
            var builder = new DiscordInteractionResponseBuilder().AsEphemeral().AddEmbed(new DiscordEmbedBuilder()
            {
                Color = DiscordColor.Green,
                Title = "Session Created Successfully"
            }.AddField(name, "Session Title").AddField(sessionId.ToString(), "Session ID"));
            await ctx.CreateResponseAsync(builder);
        }

        [SlashCommand("start", "Creates a new session for note taking and adds mentioned users to the session (Admin / GM only)")]
        public async Task StartSession(InteractionContext ctx,
            [Option("game", "The campaign to create a new session for", true)] string game)
        {
            var result = SqlHelper.CreateNewSession(game, out int sessionId, out string name);
            var builder = new DiscordInteractionResponseBuilder().AsEphemeral().AddEmbed(new DiscordEmbedBuilder()
            {
                Color = DiscordColor.Green,
                Title = "Session Created Successfully"
            }.AddField(name, "Session Title").AddField(sessionId.ToString(), "Session ID"));
            await ctx.CreateResponseAsync(builder);
        }

        [SlashCommand("add", "Adds a tagged member to the specified session using the session's id")]
        public async Task AddSession(InteractionContext ctx,
            [Option("player", "The player to add to the session")] DiscordUser player,
            [Option("sessionid", "The id of the session to add members to", true)] double? id = null,
            [Option("game", "The campaign in which the session exists", true)] string? game = null,
            [Option("name", "The name of the session to add members to", true)] string? name = null)
        {
            bool result = false;
            if (id != null)
            {
                result = SqlHelper.AddToSession((int)id, player.Id);
                await ctx.CreateResponseAsync($"Player `{player}` {(result ? $"added to session with id `{id}`!" : "could not be added. Please try again.")}", true);
            }
            else if (name != null && game != null)
            {
                result = SqlHelper.AddToSession(game, name, player.Id);
                await ctx.CreateResponseAsync($"Player `{player}` {(result ? $"added to session `{name}`!" : "could not be added. Please try again.")}", true);
            }
            else
            {
                var builder = new DiscordInteractionResponseBuilder().AsEphemeral().AddEmbed(new DiscordEmbedBuilder()
                {
                    Color = DiscordColor.Red,
                    Title = "You must provide a session ID OR a game name and session name."
                });
                await ctx.CreateResponseAsync(builder);
            }
        }

        //[SlashCommand("add", "Adds a tagged member to the specified session")]
        //public async Task AddSession(InteractionContext ctx, [Option("game", "The campaign in which the session exists", true)] string game, [Option("name", "The name of the session to add members to", true)] string name, [Option("player", "The player to add to the session")] DiscordUser player)
        //{
        //    var result = SqlHelper.AddToSession(game, name, player.Id);
        //    await ctx.CreateResponseAsync($"Player `{player}` {(result ? $"added to session {name}!" : "could not be added. Please try again.")}", true);
        //}

        [SlashCommand("role", "Adds all members of a role to the specified session")]
        public async Task AddSession(InteractionContext ctx,
            [Option("role", "The role to add players from")] DiscordRole role,
            [Option("sessionid", "The id of the session to add members to", true)] double? id = null,
            [Option("game", "The campaign in which the session exists", true)] string? game = null,
            [Option("name", "The name of the session to add members to", true)] string? name = null)
        {
            if (id != null)
            {
                var result = SqlHelper.AddToSession((int)id, role.Id);
                await ctx.CreateResponseAsync($"Members with role `{role.Name}` {(result ? $"added to session `{id}`!" : "could not be added. Please try again.")}", true);
            }
            else if (name != null && game != null)
            {
                var result = SqlHelper.AddToSession(game, name, role.Id);
                await ctx.CreateResponseAsync($"Members with role `{role.Name}` {(result ? $"added to session `{name}`!" : "could not be added. Please try again.")}", true);
            }
            else
            {
                var builder = new DiscordInteractionResponseBuilder().AsEphemeral().AddEmbed(new DiscordEmbedBuilder()
                {
                    Color = DiscordColor.Red,
                    Title = "You must provide a session ID OR a game name and session name."
                });
                await ctx.CreateResponseAsync(builder);
            }
        }

        [SlashCommand("delete", "Deletes a session by name (Admin / GM only)")]
        public async Task DeleteSession(InteractionContext ctx,
            [Option("game", "The campaign from which to delete the session", true)] string? game = null,
            [Option("name", "The name of the session to delete", true)] string? name = null,
            [Option("sessionid", "The id of the session to delete", true)] double? sessionId = null)
        {
            if (sessionId != null)
            {
                SqlHelper.DeleteSession((int)sessionId);
                await ctx.CreateResponseAsync($"Session `{sessionId}` deleted successfully.");
            }
            else if (name != null && game != null)
            {
                SqlHelper.DeleteSession(game, name);
                await ctx.CreateResponseAsync($"Session `{name}` deleted successfully.");
            }
            else
            {
                var builder = new DiscordInteractionResponseBuilder().AsEphemeral().AddEmbed(new DiscordEmbedBuilder()
                {
                    Color = DiscordColor.Red,
                    Title = "You must provide a session ID OR a game name and session name."
                });
                await ctx.CreateResponseAsync(builder);
            }
        }

        [SlashCommand("clear", "Clears all notes from a specified session (Admin / GM only) ")]
        public async Task ClearSession(InteractionContext ctx,
            [Option("game", "The campaign in which the session exists", true)] string? game = null,
            [Option("name", "The name of the session to clear notes from", true)] string? name = null,
            [Option("sessionid", "The id of the session to delete", true)] double? sessionId = null)
        {
            if (sessionId != null)
            {
                SqlHelper.ClearSession((int)sessionId);
                await ctx.CreateResponseAsync($"Session `{sessionId}` cleared successfully.");
            }
            else if (name != null && game != null)
            {
                SqlHelper.ClearSession(game, name);
                await ctx.CreateResponseAsync($"Session `{name}` cleared successfully.");
            }
            else
            {
                var builder = new DiscordInteractionResponseBuilder().AsEphemeral().AddEmbed(new DiscordEmbedBuilder()
                {
                    Color = DiscordColor.Red,
                    Title = "You must provide a session ID OR a game name and session name."
                });
                await ctx.CreateResponseAsync(builder);
            }
        }

        [SlashCommand("empty", "Empties all notes from a specified session (Admin / GM only) ")]
        public async Task EmptySession(InteractionContext ctx,
            [Option("game", "The campaign in which the session exists", true)] string? game = null,
            [Option("name", "The name of the session to clear notes from", true)] string? name = null,
            [Option("sessionid", "The id of the session to delete", true)] double? sessionId = null)
        {
            if (sessionId != null)
            {
                SqlHelper.ClearSession((int)sessionId);
                await ctx.CreateResponseAsync($"Session `{sessionId}` cleared successfully.");
            }
            else if (name != null && game != null)
            {
                SqlHelper.ClearSession(game, name);
                await ctx.CreateResponseAsync($"Session `{name}` cleared successfully.");
            }
            else
            {
                var builder = new DiscordInteractionResponseBuilder().AsEphemeral().AddEmbed(new DiscordEmbedBuilder()
                {
                    Color = DiscordColor.Red,
                    Title = "You must provide a session ID OR a game name and session name."
                });
                await ctx.CreateResponseAsync(builder);
            }
        }


        //[SlashCommand("archive", "Archives a session's notes by name (Admin / GM only)")]
        //public async Task ArchiveSession(InteractionContext ctx, [Option("game", "The campaign from which to archive the session", true)] string game, [Option("name", "The name of the session to archive", true)] string name)
        //{
        //    //SqlHelper.ArchiveSession();
        //}

        [SlashCommand("download", "Downloads all notes from a specified session (Admin / GM only)")]
        public async Task DownloadSession(InteractionContext ctx,
            [Option("game", "The campaign in which the session exists", true)] string? game = null,
            [Option("name", "The name of the session to clear notes from", true)] string? name = null,
            [Option("sessionid", "The id of the session to delete", true)] double? sessionId = null)
        {

            if (sessionId != null)
            {
                await ctx.CreateResponseAsync($"Downloading session {name}... Please wait...", true);

                var results = SqlHelper.DownloadSession((int)sessionId);
                StringBuilder builder = new StringBuilder();
                foreach (var note in results)
                {
                    builder.AppendLine("USER: " + note.User);
                    builder.AppendLine("CREATED: " + note.Created.ToString("yyyy-MM-dd HH:mm:ss"));
                    builder.AppendLine("SESSION: " + note.Session);
                    builder.AppendLine("GAME: " + note.Game);
                    builder.AppendLine("VOIDED: " + (note.Voided ? "YES" : "NO"));
                    builder.AppendLine();
                    builder.AppendLine(note.Content);
                    builder.AppendLine();
                    builder.AppendLine("================================================================================");
                    builder.AppendLine();
                }
                var content = builder.ToString();
                var path = Guid.NewGuid() + ".txt";
                File.WriteAllText(path, content);
                var stream = File.OpenRead(path);
                var response = new DiscordWebhookBuilder();
                response.AddFile(stream);
                await ctx.Interaction.EditOriginalResponseAsync(response);
            }
            else if (name != null && game != null)
            {
                await ctx.CreateResponseAsync($"Downloading session {name}... Please wait...", true);

                var results = SqlHelper.DownloadSession(game, name);
                StringBuilder builder = new StringBuilder();
                foreach (var note in results)
                {
                    builder.AppendLine("USER: " + note.User);
                    builder.AppendLine("CREATED: " + note.Created.ToString("yyyy-MM-dd HH:mm:ss"));
                    builder.AppendLine("SESSION: " + note.Session);
                    builder.AppendLine("GAME: " + note.Game);
                    builder.AppendLine("VOIDED: " + (note.Voided ? "YES" : "NO"));
                    builder.AppendLine();
                    builder.AppendLine(note.Content);
                    builder.AppendLine();
                    builder.AppendLine("================================================================================");
                    builder.AppendLine();
                }
                var content = builder.ToString();
                var path = Guid.NewGuid() + ".txt";
                File.WriteAllText(path, content);
                var stream = File.OpenRead(path);
                var response = new DiscordWebhookBuilder();
                response.AddFile(stream);
                await ctx.Interaction.EditOriginalResponseAsync(response);
            }
            else
            {
                var builder = new DiscordInteractionResponseBuilder().AsEphemeral().AddEmbed(new DiscordEmbedBuilder()
                {
                    Color = DiscordColor.Red,
                    Title = "You must provide a session ID OR a game name and session name."
                });
                await ctx.CreateResponseAsync(builder);
            }
        }

        [SlashCommand("all", "Outputs a list of all sessions from all games, most recent first (Admin / GM only)")]
        public async Task AllSessions(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync("Retrieving all sessions... Please wait...", true);

            var sessions = SqlHelper.GetAllSessions().OrderByDescending(s => s.Created).ToList();
            var pages = new List<Page>();

            for (int x = 0; x < sessions.Count; x++)
            {
                if (x >= sessions.Count)
                    break;

                var embed = new DiscordEmbedBuilder();

                embed.Color = DiscordColor.Blue;
                embed.AddField(sessions[x].Name, "Name");
                embed.AddField(sessions[x].GameName, "Campaign");
                embed.AddField(sessions[x].Created.ToString("yyyy-MM-dd HH:mm:ss"), "Created");
                if (!string.IsNullOrWhiteSpace(sessions[x].PlayersRaw)) embed.AddField(sessions[x].Players.Count.ToString(), "# of players");
                else embed.AddField(0.ToString(), "# of players");
                embed.AddField(sessions[x].Id.ToString(), "Id");

                pages.Add(new Page()
                {
                    Embed = embed
                });
            }

            await ctx.Interaction.SendPaginatedResponseAsync(true, ctx.User, pages, null, DSharpPlus.Interactivity.Enums.PaginationBehaviour.Ignore, DSharpPlus.Interactivity.Enums.ButtonPaginationBehavior.DeleteButtons, default, true);
        }

        [SlashCommand("fill", "Creates 10 fake sessions for testing")]
        public async Task Fill(InteractionContext ctx)
        {
            SqlHelper.FillSessions();
            await ctx.CreateResponseAsync("10 fake sessions created...", true);
        }

        [SlashCommand("games", "Outputs a list of all campaigns that have game sessions (Admin / GM only)")]
        public async Task AllGames(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync("Retrieving all campaigns... Please wait...", true);

            var games = SqlHelper.GetAllGames().OrderByDescending(s => s.Created).ToList();
            var builder = new DiscordWebhookBuilder();
            var embed = new DiscordEmbedBuilder();

            for (int x = 0; x < games.Count; x++)
            {
                if (x >= games.Count)
                    break;

                embed.Color = DiscordColor.Blue;
                embed.AddField(games[x].Name, "Name");
                embed.AddField(games[x].Created.ToString("yyyy-MM-dd HH:mm:ss"), "Created");
                embed.AddField(games[x].Id.ToString(), "Id");
            }

            builder.AddEmbed(embed);

            await ctx.Interaction.EditOriginalResponseAsync(builder);
        }

        [SlashCommand("list", "Outputs a list of all sessions for a specified campaign (Admin / GM only)")]
        public async Task GameSessions(InteractionContext ctx, [Option("game", "The campaign to get all sessions from", true)] string game)
        {
            await ctx.CreateResponseAsync("Retrieving sessions for the specified campaign... Please wait...", true);

            var sessions = SqlHelper.GetGameSessions(game).OrderByDescending(s => s.Created).ToList();
            var pages = new List<Page>();

            for (int x = 0; x < sessions.Count; x++)
            {
                if (x >= sessions.Count)
                    break;

                var embed = new DiscordEmbedBuilder();

                embed.Color = DiscordColor.Blue;
                embed.AddField(sessions[x].Name, "Name");
                embed.AddField(sessions[x].GameName, "Campaign");
                embed.AddField(sessions[x].Created.ToString("yyyy-MM-dd HH:mm:ss"), "Created");
                if (!string.IsNullOrWhiteSpace(sessions[x].PlayersRaw)) embed.AddField(sessions[x].Players.Count.ToString(), "# of players");
                else embed.AddField(0.ToString(), "# of players");
                embed.AddField(sessions[x].Id.ToString(), "Id");

                pages.Add(new Page()
                {
                    Embed = embed
                });
            }

            await ctx.Interaction.SendPaginatedResponseAsync(true, ctx.User, pages, null, DSharpPlus.Interactivity.Enums.PaginationBehaviour.Ignore, DSharpPlus.Interactivity.Enums.ButtonPaginationBehavior.DeleteButtons, default, true);
        }

        public async Task GetRunning(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync("Retrieving all un-ended sessions... Please wait...", true);

            var sessions = SqlHelper.GetActiveSessions().OrderByDescending(s => s.Created).ToList();
            var pages = new List<Page>();

            for (int x = 0; x < sessions.Count; x++)
            {
                if (x >= sessions.Count)
                    break;

                var embed = new DiscordEmbedBuilder();

                embed.Color = DiscordColor.Red;
                embed.AddField(sessions[x].Name, "Name");
                embed.AddField(sessions[x].GameName, "Campaign");
                embed.AddField(sessions[x].Created.ToString("yyyy-MM-dd HH:mm:ss"), "Created");
                if (!string.IsNullOrWhiteSpace(sessions[x].PlayersRaw)) embed.AddField(sessions[x].Players.Count.ToString(), "# of players");
                else embed.AddField(0.ToString(), "# of players");
                embed.AddField(sessions[x].Id.ToString(), "Id");

                pages.Add(new Page()
                {
                    Embed = embed
                });
            }

            await ctx.Interaction.SendPaginatedResponseAsync(true, ctx.User, pages, null, DSharpPlus.Interactivity.Enums.PaginationBehaviour.Ignore, DSharpPlus.Interactivity.Enums.ButtonPaginationBehavior.DeleteButtons, default, true);
        }

        [SlashCommand("end", "Ends the specified game session")]
        public async Task EndSession(InteractionContext ctx,
            [Option("game", "The campaign in which the session exists", true)] string? game = null,
            [Option("name", "The name of the session to clear notes from", true)] string? name = null,
            [Option("sessionid", "The id of the session to delete", true)] double? sessionId = null)
        {
            if (sessionId != null)
            {
                SqlHelper.EndSession((int)sessionId);
                await ctx.CreateResponseAsync($"Session `{sessionId}` ended successfully.", true);
            }
            else if (name != null && game != null)
            {
                SqlHelper.EndSession(game, name);
                await ctx.CreateResponseAsync($"Session `{name}` ended successfully.", true);
            }
            else
            {
                var builder = new DiscordInteractionResponseBuilder().AsEphemeral().AddEmbed(new DiscordEmbedBuilder()
                {
                    Color = DiscordColor.Red,
                    Title = "You must provide a session ID OR a game name and session name."
                });
                await ctx.CreateResponseAsync(builder);
            }
        }
    }

    [SlashCommandGroup("note", "Note-taking commands")]
    public class NoteCommands : ApplicationCommandModule
    {
        [SlashCommand("void", "Voids the last note created")]
        public async Task VoidNote(InteractionContext ctx)
        {
            SqlHelper.VoidNote(ctx.User.Id);
            await ctx.CreateResponseAsync("Voided your last note.", true);
        }

        [SlashCommand("status", "Outputs the status of your note taking, you can use this to verify you're in an active game session")]
        public async Task NoteStatus(InteractionContext ctx)
        {
            var status = SqlHelper.NoteStatus(ctx.User.Id);
            var builder = new DiscordInteractionResponseBuilder();
            builder.AsEphemeral();
            var embed = new DiscordEmbedBuilder();
            embed.Color = status.SessionEnded ? DiscordColor.Red : DiscordColor.Green;
            embed.Title = "Note Status";
            embed.AddField(status.NotesThisSession.ToString(), "Notes taken this session");
            embed.AddField(status.NotesAllTime.ToString(), "Notes taken all time");
            embed.AddField(status.LastNote.Created.ToString("yyyy-MM-dd HH:mm:ss"), "Most recent note created at");
            embed.AddField(status.LastNote.Content, "Most recent note content");
            embed.AddField(status.SessionEnded ? "NO SESSION" : $"SESSION: {status.LastNote.Session}", "Session status");
            builder.AddEmbed(embed);
            await ctx.CreateResponseAsync(builder);
        }

        [SlashCommand("new", "Creates a new note for this session")]
        public async Task NewNote(InteractionContext ctx, [Option("content", "The content for the note")] string content)
        {
            var result = SqlHelper.CreateNewNote(ctx.User.Id, content, out string message);
            if (!result)
            {
                var builder = new DiscordInteractionResponseBuilder().AsEphemeral().AddEmbed(new DiscordEmbedBuilder()
                {
                    Color = DiscordColor.Red,
                    Title = message
                });
                await ctx.CreateResponseAsync(builder);
            }
            else
            {
                var builder = new DiscordInteractionResponseBuilder().AsEphemeral().AddEmbed(new DiscordEmbedBuilder()
                {
                    Color = DiscordColor.Green,
                    Title = "Note created successfully!"
                });
                await ctx.CreateResponseAsync(builder);
            }
        }

        [SlashCommand("add", "Creates a new note for this session")]
        public async Task AddNote(InteractionContext ctx, [Option("content", "The content for the note")] string content)
        {
            var result = SqlHelper.CreateNewNote(ctx.User.Id, content, out string message);
            if (!result)
            {
                var builder = new DiscordInteractionResponseBuilder().AsEphemeral().AddEmbed(new DiscordEmbedBuilder()
                {
                    Color = DiscordColor.Red,
                    Title = message
                });
                await ctx.CreateResponseAsync(builder);
            }
            else
            {
                var builder = new DiscordInteractionResponseBuilder().AsEphemeral().AddEmbed(new DiscordEmbedBuilder()
                {
                    Color = DiscordColor.Green,
                    Title = "Note created successfully!"
                });
                await ctx.CreateResponseAsync(builder);
            }
        }

        [SlashCommand("fill", "Fills in 10 fake notes for the specified user")]
        public async Task FillNotes(InteractionContext ctx, [Option("user", "The user to create notes for")] DiscordUser user)
        {
            string response = string.Empty;

            for (int x = 0; x < 10; x++)
            {
                var exists = SqlHelper.CreateNewNote(user.Id, RandomName(), out string message);
                if (!exists)
                {
                    response = "User does not belong to an active session.";
                    break;
                }
                else
                    response += "\n" + message;
            }

            if (string.IsNullOrWhiteSpace(response))
                response = $"Created 10 notes for user {user.Username}";

            await ctx.CreateResponseAsync(response);
        }

        private static string RandomName()
        {
            var lines = File.ReadAllLines("Names.txt");

            var random = new Random().Next(0, 4945);
            var name = lines[random];

            return name;
        }

        [SlashCommand("direct", "Creates a direct message channel for note taking")]
        public async void DirectMessage(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync($"Sharing note with {ctx.User.Username}", true);
            var channel = await ctx.Member.CreateDmChannelAsync();
            var builder = new DiscordMessageBuilder();
            builder.WithContent("Send messages to add them as notes in your active session.");
            await channel.SendMessageAsync(builder);
            PrivateChannelManager manager = new PrivateChannelManager(channel, ctx.User);
        }

        [SlashCommand("share", "Shares the last note you took with the tagged user")]
        public async void Share(InteractionContext ctx, [Option("player", "The player to share the last note with")] DiscordUser user)
        {
            await ctx.CreateResponseAsync($"Sharing note with {user.Username}", true);
            var note = SqlHelper.GetLast(ctx.User.Id);
            var member = await ctx.Guild.GetMemberAsync(user.Id);
            var msg = await member.CreateDmChannelAsync();
            await msg.SendMessageAsync($"Note shared by {ctx.User.Username + "#" + ctx.User.Discriminator}");
            var builder = new DiscordMessageBuilder();
            var pages = new List<Page>();
            var embed = new DiscordEmbedBuilder();
            embed.Color = note.Voided ? DiscordColor.Red : DiscordColor.Green;
            embed.Title = "Last Note Taken";
            embed.AddField(note.Session, "Session");
            embed.AddField(note.Game, "Game");
            embed.AddField(note.Created.ToString("yyyy-MM-dd HH:mm:ss"), "Created at");
            embed.AddField(note.Voided ? "VOIDED" : "NOT VOIDED", "Voided status");
            pages.Add(new Page(content: "Go to the next page to view note content", embed: embed));
            var contentPages = ctx.Client.GetInteractivity().GeneratePagesInContent(note.Content);
            pages.AddRange(contentPages);
            await ctx.Interaction.SendPaginatedResponseAsync(true, ctx.User, pages, null, null, null, default, true);
            await msg.SendMessageAsync(builder);
            await ctx.Interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder().WithContent($"Note shared with {user.Username}"));

        }

        [SlashCommand("search", "Searches all of your notes for the specified word or phrase")]
        public async Task Search(InteractionContext ctx, [Option("search", "The word or phrase to search for")] string search, [Option("soft", "Search softly. This can take time and produce a lot of results.")] bool softly = false, [Option("file", "Set to true to have the bot output the notes in a file")] bool file = false)
        {
            await ctx.CreateResponseAsync("Retrieving search results... Please wait...", true);

            var notes = SqlHelper.SearchNotes(ctx.User.Id, search, softly);

            if (notes.Count == 1)
            {
                var pages = new List<Page>();
                var embed = new DiscordEmbedBuilder();
                embed.Color = notes[0].Voided ? DiscordColor.Red : DiscordColor.Green;
                embed.Title = "One Result Found";
                embed.AddField(notes[0].Session, "Session");
                embed.AddField(notes[0].Game, "Game");
                embed.AddField(notes[0].Created.ToString("yyyy-MM-dd HH:mm:ss"), "Created at");
                embed.AddField(notes[0].Voided ? "VOIDED" : "NOT VOIDED", "Voided status");
                pages.Add(new Page(content: "Go to the next page to view note content", embed: embed));
                var contentPages = ctx.Client.GetInteractivity().GeneratePagesInContent(notes[0].Content);
                pages.AddRange(contentPages);
                await ctx.Interaction.SendPaginatedResponseAsync(true, ctx.User, pages, null, null, null, default, true);
            }
            else
            {
                if (file)
                {
                    StringBuilder builder = new StringBuilder();
                    foreach (var note in notes)
                    {
                        builder.AppendLine("CREATED: " + note.Created.ToString("yyyy-MM-dd HH:mm:ss"));
                        builder.AppendLine("SESSION: " + note.Session);
                        builder.AppendLine("GAME: " + note.Game);
                        builder.AppendLine("VOIDED: " + (note.Voided ? "YES" : "NO"));
                        builder.AppendLine();
                        builder.AppendLine(note.Content);
                        builder.AppendLine();
                        builder.AppendLine("================================================================================");
                        builder.AppendLine();
                    }
                    var content = builder.ToString();
                    var path = Guid.NewGuid() + ".txt";
                    File.WriteAllText(path, content);
                    var stream = File.OpenRead(path);
                    var response = new DiscordWebhookBuilder();
                    response.AddFile(stream);
                    await ctx.Interaction.EditOriginalResponseAsync(response);
                }
                else
                {
                    var pages = new List<Page>();
                    for (int x = 1; x <= notes.Count; x++)
                    {
                        var embed = new DiscordEmbedBuilder();
                        embed.Color = notes[x - 1].Voided ? DiscordColor.Red : DiscordColor.Green;
                        embed.Title = $"Note Taken ({x} of {notes.Count})";
                        embed.AddField(notes[x - 1].Session, "Session");
                        embed.AddField(notes[x - 1].Game, "Game");
                        embed.AddField(notes[x - 1].Created.ToString("yyyy-MM-dd HH:mm:ss"), "Created at");
                        embed.AddField(notes[x - 1].Voided ? "VOIDED" : "NOT VOIDED", "Voided status");
                        pages.Add(new Page(content: "Go to the next page to view note content", embed: embed));
                        var contentPages = ctx.Client.GetInteractivity().GeneratePagesInContent(notes[x - 1].Content);
                        pages.AddRange(contentPages);
                    }
                    await ctx.Interaction.SendPaginatedResponseAsync(true, ctx.User, pages, null, null, null, default, true);
                }
            }
        }

        [SlashCommand("lastsession", "Outputs a long message (or text file it too large) of the last note you created")]
        public async Task GetLastSession(InteractionContext ctx, [Option("file", "Set to true to have the bot output the notes in a file")] bool file = false)
        {
            var notes = SqlHelper.GetLastSession(ctx.User.Id);

            if (file)
            {
                StringBuilder builder = new StringBuilder();
                foreach (var note in notes)
                {
                    builder.AppendLine("CREATED: " + note.Created.ToString("yyyy-MM-dd HH:mm:ss"));
                    builder.AppendLine("SESSION: " + note.Session);
                    builder.AppendLine("GAME: " + note.Game);
                    builder.AppendLine("VOIDED: " + (note.Voided ? "YES" : "NO"));
                    builder.AppendLine();
                    builder.AppendLine(note.Content);
                    builder.AppendLine();
                    builder.AppendLine("================================================================================");
                    builder.AppendLine();
                }
                var content = builder.ToString();
                var path = Guid.NewGuid() + ".txt";
                File.WriteAllText(path, content);
                var stream = File.OpenRead(path);
                var response = new DiscordInteractionResponseBuilder();
                response.AsEphemeral();
                response.AddFile(stream);
                await ctx.CreateResponseAsync(response);
            }
            else
            {
                await ctx.CreateResponseAsync("Retreiving your notes from last session... Please wait...", true);
                var builder = new DiscordInteractionResponseBuilder();
                builder.AsEphemeral();
                var pages = new List<Page>();
                for (int x = 1; x <= notes.Count; x++)
                {
                    var embed = new DiscordEmbedBuilder();
                    embed.Color = notes[x - 1].Voided ? DiscordColor.Red : DiscordColor.Green;
                    embed.Title = $"Note Taken ({x} of {notes.Count})";
                    embed.AddField(notes[x - 1].Session, "Session");
                    embed.AddField(notes[x - 1].Game, "Game");
                    embed.AddField(notes[x - 1].Created.ToString("yyyy-MM-dd HH:mm:ss"), "Created at");
                    embed.AddField(notes[x - 1].Voided ? "VOIDED" : "NOT VOIDED", "Voided status");
                    pages.Add(new Page(content: "Go to the next page to view note content", embed: embed));
                    var contentPages = ctx.Client.GetInteractivity().GeneratePagesInContent(notes[x - 1].Content);
                    pages.AddRange(contentPages);
                }
                await ctx.Interaction.SendPaginatedResponseAsync(true, ctx.User, pages, null, null, null, default, true);
            }
        }

        [SlashCommand("last", "Outputs a long message (or text file it too large) of the last note you created")]
        public async Task GetLast(InteractionContext ctx, [Option("file", "Set to true to have the bot output the notes in a file")] bool file = false)
        {
            await ctx.CreateResponseAsync("Getting last note... Please wait...", true);
            var note = SqlHelper.GetLast(ctx.User.Id);
            var builder = new DiscordInteractionResponseBuilder();
            builder.AsEphemeral();
            var pages = new List<Page>();
            var embed = new DiscordEmbedBuilder();
            embed.Color = note.Voided ? DiscordColor.Red : DiscordColor.Green;
            embed.Title = "Last Note Taken";
            embed.AddField(note.Session, "Session");
            embed.AddField(note.Game, "Game");
            embed.AddField(note.Created.ToString("yyyy-MM-dd HH:mm:ss"), "Created at");
            embed.AddField(note.Voided ? "VOIDED" : "NOT VOIDED", "Voided status");
            pages.Add(new Page(content: "Go to the next page to view note content", embed: embed));
            var contentPages = ctx.Client.GetInteractivity().GeneratePagesInContent(note.Content);
            pages.AddRange(contentPages);
            await ctx.Interaction.SendPaginatedResponseAsync(true, ctx.User, pages, null, null, null, default, true);
        }

        [SlashCommand("this", "Outputs a long message (or text file if too large) of all your notes from this sesssion")]
        public async Task GetThis(InteractionContext ctx, [Option("file", "Set to true to have the bot output the notes in a file")] bool file = false)
        {
            var notes = SqlHelper.GetThis(ctx.User.Id);

            if (file)
            {
                StringBuilder builder = new StringBuilder();
                foreach (var note in notes)
                {
                    builder.AppendLine("CREATED: " + note.Created.ToString("yyyy-MM-dd HH:mm:ss"));
                    builder.AppendLine("SESSION: " + note.Session);
                    builder.AppendLine("GAME: " + note.Game);
                    builder.AppendLine("VOIDED: " + (note.Voided ? "YES" : "NO"));
                    builder.AppendLine();
                    builder.AppendLine(note.Content);
                    builder.AppendLine();
                    builder.AppendLine("================================================================================");
                    builder.AppendLine();
                }
                var content = builder.ToString();
                var path = Guid.NewGuid() + ".txt";
                File.WriteAllText(path, content);
                var stream = File.OpenRead(path);
                var response = new DiscordInteractionResponseBuilder();
                response.AsEphemeral();
                response.AddFile(stream);
                await ctx.CreateResponseAsync(response);
            }
            else
            {
                await ctx.CreateResponseAsync("Retreiving your notes from this session... Please wait...", true);
                var builder = new DiscordInteractionResponseBuilder();
                builder.AsEphemeral();
                var pages = new List<Page>();
                for (int x = 1; x <= notes.Count; x++)
                {
                    var embed = new DiscordEmbedBuilder();
                    embed.Color = notes[x - 1].Voided ? DiscordColor.Red : DiscordColor.Green;
                    embed.Title = $"Note Taken ({x} of {notes.Count})";
                    embed.AddField(notes[x - 1].Session, "Session");
                    embed.AddField(notes[x - 1].Game, "Game");
                    embed.AddField(notes[x - 1].Created.ToString("yyyy-MM-dd HH:mm:ss"), "Created at");
                    embed.AddField(notes[x - 1].Voided ? "VOIDED" : "NOT VOIDED", "Voided status");
                    pages.Add(new Page(content: "Go to the next page to view note content", embed: embed));
                    var contentPages = ctx.Client.GetInteractivity().GeneratePagesInContent(notes[x - 1].Content);
                    pages.AddRange(contentPages);
                }
                await ctx.Interaction.SendPaginatedResponseAsync(true, ctx.User, pages, null, null, null, default, true);
            }
        }

        [SlashCommand("all", "Outputs a text file of all your notes you've ever taken in any campaign")]
        public async Task GetAll(InteractionContext ctx, [Option("file", "Set to true to have the bot output the notes in a file")] bool file = false)
        {
            var notes = SqlHelper.GetAll(ctx.User.Id);

            if (file)
            {
                StringBuilder builder = new StringBuilder();
                foreach (var note in notes)
                {
                    builder.AppendLine("CREATED: " + note.Created.ToString("yyyy-MM-dd HH:mm:ss"));
                    builder.AppendLine("SESSION: " + note.Session);
                    builder.AppendLine("GAME: " + note.Game);
                    builder.AppendLine("VOIDED: " + (note.Voided ? "YES" : "NO"));
                    builder.AppendLine();
                    builder.AppendLine(note.Content);
                    builder.AppendLine();
                    builder.AppendLine("================================================================================");
                    builder.AppendLine();
                }
                var content = builder.ToString();
                var path = Guid.NewGuid() + ".txt";
                File.WriteAllText(path, content);
                var stream = File.OpenRead(path);
                var response = new DiscordInteractionResponseBuilder();
                response.AsEphemeral();
                response.AddFile(stream);
                await ctx.CreateResponseAsync(response);
            }
            else
            {
                await ctx.CreateResponseAsync("Retreiving all of your notes... Please wait...", true);
                var builder = new DiscordInteractionResponseBuilder();
                builder.AsEphemeral();
                var pages = new List<Page>();
                for (int x = 1; x <= notes.Count; x++)
                {
                    var embed = new DiscordEmbedBuilder();
                    embed.Color = notes[x - 1].Voided ? DiscordColor.Red : DiscordColor.Green;
                    embed.Title = $"Note Taken ({x} of {notes.Count})";
                    embed.AddField(notes[x - 1].Session, "Session");
                    embed.AddField(notes[x - 1].Game, "Game");
                    embed.AddField(notes[x - 1].Created.ToString("yyyy-MM-dd HH:mm:ss"), "Created at");
                    embed.AddField(notes[x - 1].Voided ? "VOIDED" : "NOT VOIDED", "Voided status");
                    pages.Add(new Page(content: "Go to the next page to view note content", embed: embed));
                    var contentPages = ctx.Client.GetInteractivity().GeneratePagesInContent(notes[x - 1].Content);
                    pages.AddRange(contentPages);
                }
                await ctx.Interaction.SendPaginatedResponseAsync(true, ctx.User, pages, null, null, null, default, true);
            }
        }
    }

    public class FeedbackCommands : ApplicationCommandModule
    {
        [SlashCommand("feedback", "Provide feedback on the Imagination Creations bot")]
        public async Task Feedback(InteractionContext ctx,
            [Option("feedback", "The feedback you wish to provide, the more detail, the better")] string feedback,
            [Option("dm", "Can the Admins DM you for more details?")] bool dm = false)
        {
            string user = ctx.User.Username + "#" + ctx.User.Discriminator;
            SqlHelper.SubmitFeedback(user, feedback, dm);
            await ctx.CreateResponseAsync("Your feedback was submitted! Thank you!", true);
        }
    }

    public class HelpCommands : ApplicationCommandModule
    {
        [SlashCommand("help", "Outputs a help dialog for all commands")]
        public async Task HelpAll(InteractionContext ctx)
        {
            var builder = new DiscordInteractionResponseBuilder();
            var journal = new DiscordEmbedBuilder();
            journal.Color = DiscordColor.Green;
            journal.Title = "Journaling Commands";
            journal.AddField("==== About =====", "Your journal is tied to your account. Journal entries are not specific to any campaign or game session.");
            journal.AddField("/journal new", "Submits a new journal entry. (same as /journal add)");
            journal.AddField("/journal add", "Submits a new journal entry. (same as /journal new)");
            journal.AddField("/journal get", "Outputs all of your journal entries, and the dates they were created to a text file.");
            journal.AddField("/journal append", "Allows you to add content to the last created journal entry.");
            journal.AddField("/journal delete", "Deletes the last created journal entry. (Not reversable)");
            journal.AddField("/journal upload", "Allows you to upload a file as a journal entry. The file name is used as the title for the entry.");
            var sessionBasic = new DiscordEmbedBuilder();
            sessionBasic.Color = DiscordColor.Green;
            sessionBasic.Title = "Basic Session Commands";
            sessionBasic.AddField("==== About =====", "Commands for GM's and Admins to create game sessions for campaigns for note taking.");
            sessionBasic.AddField("/session start", "(RECOMMENDED) Creates a new session in the specified campaign, and adds users (players) to that game session.");
            sessionBasic.AddField("/session end", "(RECOMMENDED) Ends the specified game session and saves all notes created in it.");
            sessionBasic.AddField("/session add", "Adds users (players) to the specified game session.");
            sessionBasic.AddField("/session role", "Adds all users (players) from the specified role to the specified game session.");
            var sessionList = new DiscordEmbedBuilder();
            sessionList.Color = DiscordColor.Green;
            sessionList.Title = "Session Search / Find Commands";
            sessionList.AddField("==== About =====", "Commands for GM's and Admins to find specific game sessions.");
            sessionList.AddField("/session all", "Lists all game sessions from all campaigns, most recent first.");
            sessionList.AddField("/session games", "Lists all games that have game sessions, most recent first.");
            sessionList.AddField("/session list {game}", "Lists all game sessions from the specified campaign, most recent first.");
            var sessionAdvanced = new DiscordEmbedBuilder();
            sessionAdvanced.Color = DiscordColor.Red;
            sessionAdvanced.Title = "Advanced Session Commands";
            sessionAdvanced.AddField("==== About =====", "Advanced commands for use with note-taking sessions.");
            sessionAdvanced.AddField("/session new", "Creates a new session in the specified campaign.");
            sessionAdvanced.AddField("/session delete", "Deletes a specified game session.");
            sessionAdvanced.AddField("/session clear", "Clears all notes from the specified game session. (same as /session empty");
            sessionAdvanced.AddField("/session empty", "Empties all notes from the specified game session. (same as /session clear");
            sessionAdvanced.AddField("/session download", "Downloads all notes from the specified game session.");
            var note = new DiscordEmbedBuilder();
            note.Color = DiscordColor.Green;
            note.Title = "Note Commands";
            note.AddField("==== About =====", "Commands associated with note taking for active game sessions.");
            note.AddField("/note add", "Creates a new note in your active session with the specified content.");
            note.AddField("/note new", "Creates a new note in your active session with the specified content.");
            note.AddField("/note dm", "Creates a direct message channel with the bot for note taking.");
            note.AddField("/note share", "Shares the last note you took to the specified player.");
            note.AddField("/note void", "Voids the last note you created (Voided notes are still saved, but are marked as not needed / unnecessary / expunged");
            note.AddField("/note status", "Gets the status of your note taking. This can be used to see how many notes you've made, as well as the name of the game session.");
            note.AddField("/note last", "Outputs a message (or file) containing all notes from your previous game session.");
            note.AddField("/note this", "Outputs a message (or file) containing all notes from this game session.");
            note.AddField("/note all", "Outputs a message (or file) containing all notes from all game sessions.");
            if (ctx.Member.Roles.Any(r => r.Name.Contains("admin", StringComparison.InvariantCultureIgnoreCase)))
            {
                builder.AddEmbed(sessionBasic);
                builder.AddEmbed(sessionAdvanced);
                builder.AddEmbed(sessionList);
            }
            builder.AddEmbed(note);
            builder.AddEmbed(journal);
            builder.AsEphemeral();
            await ctx.CreateResponseAsync(builder);
        }
    }
}
