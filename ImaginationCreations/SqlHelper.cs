using ImaginationCreations.Models;
using MySql.Data.MySqlClient;
using System.Diagnostics;

namespace ImaginationCreations
{
    public class SqlHelper
    {
        private static bool RunNonQuery(MySqlCommand command)
        {
            bool status = false;

            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                command.Connection = conn;



                command.ExecuteNonQuery();



            }

            return status;
        }
        private static object RunScalar(MySqlCommand command)
        {
            object result;

            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                command.Connection = conn;


                result = command.ExecuteScalar();

            }

            return result;
        }
        private static int GetSessionID(string game, string name)
        {
            string getGameText = "SELECT id FROM ic.games WHERE name=@game";
            MySqlCommand getGameCommand = new MySqlCommand(getGameText);
            getGameCommand.Parameters.AddWithValue("@game", game);
            var result = RunScalar(getGameCommand);
            var gameId = int.Parse(result.ToString());

            string getSessionText = "SELECT id FROM ic.session WHERE game=@game AND name=@name";
            MySqlCommand getSessionCommand = new MySqlCommand(getSessionText);
            getSessionCommand.Parameters.AddWithValue("@game", gameId);
            getSessionCommand.Parameters.AddWithValue("@name", name);
            result = RunScalar(getSessionCommand);
            var sessionId = int.Parse(result.ToString());

            return sessionId;
        }
        public static KeyValuePair<string, string> GetNames(int sessionId)
        {
            int gameId = -1;
            string session = string.Empty;
            string game = string.Empty;

            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                string sessionText = "SELECT ic.sessions.name, ic.games.name FROM ic.sessions LEFT JOIN ic.games ON (ic.games.id = game) WHERE ic.sessions.id = @sessionId";
                MySqlCommand sessionCommand = new MySqlCommand(sessionText, conn);
                sessionCommand.Parameters.AddWithValue("@sessionId", sessionId);
                using (MySqlDataReader reader = sessionCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        session = reader.GetString(0);
                        game = reader.GetString(1);
                    }
                }
            }

            KeyValuePair<string, string> names = new KeyValuePair<string, string>(game, session);

            return names;
        }

        private const string ConnectionString = "Server=192.168.0.55;Port=3306;User ID=Dustin;Password=Artemis_1997;Database=ic";

        #region Journaling
        public static bool CreateNewEntry(string user, string title, string content)
        {
            bool status = false;

            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                using (MySqlTransaction tran = conn.BeginTransaction())
                {
                    string commandText = @"INSERT INTO `ic`.`journal` (`created`, `user`, `title`, `content`) VALUES (@created, @user, @title, @content);";
                    MySqlCommand command = new MySqlCommand(commandText, conn);
                    command.Parameters.AddWithValue("@created", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    command.Parameters.AddWithValue("@user", user);
                    command.Parameters.AddWithValue("@title", title);
                    command.Parameters.AddWithValue("@content", content);
                    var affected = command.ExecuteNonQuery();

                    if (affected > 1)
                    {
                        tran.Rollback();
                        status = false;
                    }
                    else
                    {
                        tran.Commit();
                        status = true;
                    }
                }
            }

            return status;
        }

        public static List<Models.Journal> GetEntry(string user, string search)
        {
            List<Models.Journal> list = new List<Models.Journal>();

            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                using (MySqlTransaction tran = conn.BeginTransaction())
                {
                    string commandText = @"SELECT * FROM `ic`.`journal` WHERE user=@user AND ('title' LIKE '%@search%' OR 'created' LIKE '%@search%');";
                    MySqlCommand command = new MySqlCommand(commandText, conn);
                    command.Parameters.AddWithValue("@user", user);
                    command.Parameters.AddWithValue("@search", search);

                    var affected = command.ExecuteNonQuery();

                    if (affected > 1)
                    {
                        tran.Rollback();
                        return null;
                    }
                    else
                    {
                        tran.Commit();

                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                Models.Journal result = new Models.Journal()
                                {
                                    Id = reader.GetInt32(0),
                                    Created = reader.GetDateTime(1),
                                    User = reader.GetString(3),
                                    Title = reader.GetString(4),
                                    Content = reader.GetString(5)
                                };
                                list.Add(result);
                            }
                        }
                    }
                }
            }

            return list;
        }

        public static Models.Journal GetLastEntry(string user)
        {
            Models.Journal result = new Models.Journal();

            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                using (MySqlTransaction tran = conn.BeginTransaction())
                {
                    string commandText = @"SELECT * FROM `ic`.`journal` WHERE user=@user ORDER BY `created` DESC LIMIT 1;";
                    MySqlCommand command = new MySqlCommand(commandText, conn);
                    command.Parameters.AddWithValue("@user", user);

                    var affected = command.ExecuteNonQuery();

                    if (affected > 1)
                    {
                        tran.Rollback();
                        return null;
                    }
                    else
                    {
                        tran.Commit();

                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                result.Id = reader.GetInt32(0);
                                result.Created = reader.GetDateTime(1);
                                result.User = reader.GetString(3);
                                result.Title = reader.GetString(4);
                                result.Content = reader.GetString(5);
                            }
                        }
                    }
                }
            }

            return result;
        }

        public static List<Journal> SearchJournal(ulong user, string search, bool softly)
        {
            List<Journal> result = new List<Journal>();

            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                MySqlCommand command;

                if (!softly)
                {
                    string text = @"SELECT * FROM ic.journal WHERE MATCH (content, title) AGAINST (@search) AND user = @user ORDER BY created DESC;";
                    command = new MySqlCommand(text, conn);
                    command.Parameters.AddWithValue("@search", search);
                    command.Parameters.AddWithValue("@user", user);
                }
                else
                {
                    string text = @"SELECT * FROM ic.journal WHERE (content LIKE @search OR title LIKE @search) AND user = @user ORDER BY created DESC;";
                    command = new MySqlCommand(text, conn);
                    command.Parameters.AddWithValue("@search", "%" + search + "%");
                    command.Parameters.AddWithValue("@user", user);
                }

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Journal newJournal = new Journal();
                        newJournal.Id = reader.GetInt32("id");
                        newJournal.User = reader.GetString("user");
                        newJournal.Created = reader.GetDateTime("created");
                        newJournal.Title = reader.GetString("title");
                        newJournal.Content = reader.GetString("content");
                        result.Add(newJournal);
                    }
                }
            }

            return result;
        }
        #endregion

        #region Sessions
        #region CreateNewSession
        public static bool CreateNewSession(string game, string name, out int outId)
        {
            bool status = false;

            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                using (MySqlTransaction tran = conn.BeginTransaction())
                {
                    int id = -1;
                    int affected = -1;

                    string getGame = "SELECT id FROM ic.games WHERE name=@game";
                    MySqlCommand getGameCommand = new MySqlCommand(getGame, conn);
                    getGameCommand.Parameters.AddWithValue("@game", game);
                    try { id = int.Parse(getGameCommand.ExecuteScalar().ToString()); } catch (NullReferenceException NRexc) { id = -1; }

                    if (id == -1)
                    {
                        string newGame = "INSERT INTO ic.games (created, name) VALUES (@created, @game); SELECT LAST_INSERT_ID();";
                        var newGameCommand = new MySqlCommand(newGame, conn);
                        newGameCommand.Parameters.AddWithValue("@created", DateTime.UtcNow);
                        newGameCommand.Parameters.AddWithValue("@game", game);
                        id = int.Parse(newGameCommand.ExecuteScalar().ToString());

                        if (affected > 1)
                        {
                            tran.Rollback();
                            status = false;
                        }
                    }

                    string commandText = @"INSERT INTO ic.sessions(game, created, name, players) VALUES (@game, @created, @name, @players); SELECT LAST_INSERT_ID();";
                    MySqlCommand command = new MySqlCommand(commandText, conn);
                    command.Parameters.AddWithValue("@game", id);
                    command.Parameters.AddWithValue("@created", DateTime.UtcNow);
                    command.Parameters.AddWithValue("@name", name);
                    command.Parameters.AddWithValue("@players", DBNull.Value);
                    outId = int.Parse(command.ExecuteScalar().ToString());

                    tran.Commit();
                    status = true;
                }
            }

            return status;
        }

        public static bool CreateNewSession(string game, out int outId, out string name)
        {
            bool status = false;
            name = string.Empty;

            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                int id = -1;

                string getGame = "SELECT id FROM ic.games WHERE name=@game";
                MySqlCommand getGameCommand = new MySqlCommand(getGame, conn);
                getGameCommand.Parameters.AddWithValue("@game", game);
                try { id = int.Parse(getGameCommand.ExecuteScalar().ToString()); } catch (NullReferenceException NRexc) { id = -1; }

                if (id == -1)
                {
                    string newGame = "INSERT INTO ic.games (created, name) VALUES (@created, @game); SELECT LAST_INSERT_ID();";
                    var newGameCommand = new MySqlCommand(newGame, conn);
                    newGameCommand.Parameters.AddWithValue("@created", DateTime.UtcNow);
                    newGameCommand.Parameters.AddWithValue("@game", game);
                    id = int.Parse(newGameCommand.ExecuteScalar().ToString());
                }

                bool exists = false;
                do
                {
                    name = RandomName();
                    exists = CheckSessionExists(game, name);
                } while (exists);

                string commandText = @"INSERT INTO ic.sessions(game, created, name, players) VALUES (@game, @created, @name, @players); SELECT LAST_INSERT_ID();";
                MySqlCommand command = new MySqlCommand(commandText, conn);
                command.Parameters.AddWithValue("@game", id);
                command.Parameters.AddWithValue("@created", DateTime.UtcNow);
                command.Parameters.AddWithValue("@name", name);
                command.Parameters.AddWithValue("@players", DBNull.Value);
                outId = int.Parse(command.ExecuteScalar().ToString());

                status = true;

            }

            return status;
        }
        #endregion

        #region AddToSession
        public static bool AddToSession(int sessionID, ulong player)
        {
            bool status = false;

            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                using (MySqlTransaction tran = conn.BeginTransaction())
                {
                    int affected = -1;

                    string commandText = "UPDATE ic.sessions SET players = IFNULL (CONCAT(players, @player), @player) WHERE id=@id;";
                    MySqlCommand command = new MySqlCommand(commandText, conn);
                    command.Parameters.AddWithValue("@player", player + ";");
                    command.Parameters.AddWithValue("@id", sessionID);
                    affected = command.ExecuteNonQuery();

                    if (affected > 1)
                    {
                        tran.Rollback();
                        status = false;
                    }
                    else
                    {
                        tran.Commit();
                        status = true;
                    }
                }
            }

            return status;
        }

        public static bool AddToSession(int sessionID, string players)
        {
            bool status = false;

            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                using (MySqlTransaction tran = conn.BeginTransaction())
                {
                    int affected = -1;

                    string commandText = "UPDATE ic.sessions SET players = IFNULL (CONCAT(players, @player), @player) WHERE id=@id;";
                    MySqlCommand command = new MySqlCommand(commandText, conn);
                    command.Parameters.AddWithValue("@player", players);
                    command.Parameters.AddWithValue("@id", sessionID);
                    affected = command.ExecuteNonQuery();

                    if (affected > 1)
                    {
                        tran.Rollback();
                        status = false;
                    }
                    else
                    {
                        tran.Commit();
                        status = true;
                    }
                }
            }

            return status;
        }

        public static bool AddToSession(string game, string name, ulong player)
        {
            bool status = false;

            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                using (MySqlTransaction tran = conn.BeginTransaction())
                {
                    int id = -1;
                    int affected = -1;

                    string getGame = "SELECT id FROM ic.games WHERE name=@game";
                    MySqlCommand getGameCommand = new MySqlCommand(getGame, conn);
                    getGameCommand.Parameters.AddWithValue("@game", game);
                    try { id = int.Parse(getGameCommand.ExecuteScalar().ToString()); } catch (NullReferenceException NRexc) { id = -1; }

                    string commandText = "UPDATE ic.sessions SET players = IFNULL (CONCAT(players, @player), @player) WHERE name=@name AND game=@id;";
                    MySqlCommand command = new MySqlCommand(commandText, conn);
                    command.Parameters.AddWithValue("@player", player + ";");
                    command.Parameters.AddWithValue("@name", name);
                    command.Parameters.AddWithValue("@id", id);
                    affected = command.ExecuteNonQuery();

                    if (affected > 1)
                    {
                        tran.Rollback();
                        status = false;
                    }
                    else
                    {
                        tran.Commit();
                        status = true;
                    }
                }
            }

            return status;
        }

        public static bool AddToSession(string game, string name, string players)
        {
            bool status = false;

            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                using (MySqlTransaction tran = conn.BeginTransaction())
                {
                    int id = -1;
                    int affected = -1;

                    string getGame = "SELECT id FROM ic.games WHERE name=@game";
                    MySqlCommand getGameCommand = new MySqlCommand(getGame, conn);
                    getGameCommand.Parameters.AddWithValue("@game", game);
                    try { id = int.Parse(getGameCommand.ExecuteScalar().ToString()); } catch (NullReferenceException NRexc) { id = -1; }

                    string commandText = "UPDATE ic.sessions SET players = IFNULL (CONCAT(players, @player), @player) WHERE name=@name AND game=@id;";
                    MySqlCommand command = new MySqlCommand(commandText, conn);
                    command.Parameters.AddWithValue("@player", players);
                    command.Parameters.AddWithValue("@name", name);
                    command.Parameters.AddWithValue("@id", id);
                    affected = command.ExecuteNonQuery();

                    if (affected > 1)
                    {
                        tran.Rollback();
                        status = false;
                    }
                    else
                    {
                        tran.Commit();
                        status = true;
                    }
                }
            }

            return status;
        }
        #endregion

        #region DeleteSession
        public static void DeleteSession(string game, string name)
        {
            string getText = "SELECT id FROM ic.games WHERE name=@game";
            MySqlCommand getCommand = new MySqlCommand(getText);
            getCommand.Parameters.AddWithValue("@game", game);
            var result = RunScalar(getCommand);
            var id = int.Parse(result.ToString());

            string text = "DELETE FROM ic.sessions WHERE name=@name AND game=@game";
            MySqlCommand command = new MySqlCommand(text);
            command.Parameters.AddWithValue("@name", name);
            command.Parameters.AddWithValue("@game", id);
            RunNonQuery(command);
        }
        public static void DeleteSession(int sessionId)
        {
            string text = "DELETE FROM ic.sessions WHERE id=@sessionID";
            MySqlCommand command = new MySqlCommand(text);
            command.Parameters.AddWithValue("@sessionID", sessionId);
            RunNonQuery(command);
        }
        #endregion

        #region ClearSession
        public static void ClearSession(string game, string name)
        {
            var sessionId = GetSessionID(game, name);

            string text = "DELETE FROM ic.notes WHERE session=@id";
            MySqlCommand command = new MySqlCommand(text);
            command.Parameters.AddWithValue("@id", sessionId);
            RunNonQuery(command);
        }

        public static void ClearSession(int sessionId)
        {
            string text = "DELETE FROM ic.notes WHERE session=@id";
            MySqlCommand command = new MySqlCommand(text);
            command.Parameters.AddWithValue("@id", sessionId);
            RunNonQuery(command);
        }
        #endregion

        #region DownloadSession
        public static List<Note> DownloadSession(string game, string name)
        {
            List<Note> notes = new List<Note>();

            var sessionId = GetSessionID(game, name);

            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                using (MySqlTransaction tran = conn.BeginTransaction())
                {
                    string commandText = "SELECT * FROM ic.notes WHERE session=@sessionId";
                    MySqlCommand command = new MySqlCommand(commandText, conn);
                    command.Parameters.AddWithValue("@sessionId", sessionId);

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Note newNote = new Note()
                            {
                                Id = reader.GetInt32("id"),
                                Created = reader.GetDateTime("created"),
                                SessionId = reader.GetInt32("session"),
                                User = reader.GetString("user"),
                                Content = reader.GetString("content")
                            };

                            notes.Add(newNote);
                        }
                    }
                }
            }

            return notes;
        }

        public static List<Note> DownloadSession(int sessionId)
        {
            List<Note> notes = new List<Note>();

            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                using (MySqlTransaction tran = conn.BeginTransaction())
                {
                    string commandText = "SELECT * FROM ic.notes WHERE session=@sessionId";
                    MySqlCommand command = new MySqlCommand(commandText, conn);
                    command.Parameters.AddWithValue("@sessionId", sessionId);

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Note newNote = new Note()
                            {
                                Id = reader.GetInt32("id"),
                                Created = reader.GetDateTime("created"),
                                SessionId = reader.GetInt32("session"),
                                User = reader.GetString("user"),
                                Content = reader.GetString("content")
                            };

                            notes.Add(newNote);
                        }
                    }
                }
            }

            return notes;
        }
        #endregion

        #region EndSession
        public static void EndSession(string game, string name)
        {
            int id = GetSessionID(game, name);
            string text = "UPDATE ic.sessions SET ended=1 WHERE id=@id";
            MySqlCommand command = new MySqlCommand(text);
            command.Parameters.AddWithValue("@id", id);
            RunNonQuery(command);
        }
        public static void EndSession(int sessionId)
        {
            string text = "UPDATE ic.sessions SET ended=1 WHERE id=@id";
            MySqlCommand command = new MySqlCommand(text);
            command.Parameters.AddWithValue("@id", sessionId);
            RunNonQuery(command);
        }
        #endregion

        #region List Commands
        public static List<Session> GetAllSessions()
        {
            List<Session> result = new List<Session>();

            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                using (MySqlTransaction tran = conn.BeginTransaction())
                {
                    string text = "SELECT ic.games.name AS 'gamename',ic.sessions.id,@id:=game AS 'game',ic.sessions.name,ic.sessions.created,ic.sessions.players,ic.sessions.ended FROM ic.sessions LEFT JOIN ic.games ON (ic.games.id = @id)";
                    MySqlCommand command = new MySqlCommand(text, conn);
                    command.Parameters.Add(new MySqlParameter("@id", MySqlDbType.Int32));
                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Session newSession = new Session()
                            {
                                Id = reader.GetInt32("id"),
                                Game = reader.GetString("game"),
                                GameName = reader.GetString("gamename"),
                                Name = reader.GetString("name"),
                                Created = reader.GetDateTime("created"),
                                PlayersRaw = reader.IsDBNull(reader.GetOrdinal("players")) ? "" : reader.GetString("players"),
                                Ended = reader.GetBoolean("ended")
                            };

                            result.Add(newSession);
                        }
                        reader.Close();

                    }
                }
            }

            return result;
        }

        public static List<Game> GetAllGames()
        {
            List<Game> result = new List<Game>();

            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                using (MySqlTransaction tran = conn.BeginTransaction())
                {
                    string text = "SELECT * FROM ic.games";
                    MySqlCommand command = new MySqlCommand(text, conn);

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        reader.NextResult();
                        while (reader.Read())
                        {
                            Game newGame = new Game()
                            {
                                Id = reader.GetInt32("id"),
                                Name = reader.GetString("name"),
                                Created = reader.GetDateTime("created")
                            };

                            result.Add(newGame);
                        }

                        reader.Close();
                    }
                }
            }

            return result;
        }

        public static List<Session> GetGameSessions(string game)
        {
            List<Session> result = new List<Session>();

            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                using (MySqlTransaction tran = conn.BeginTransaction())
                {
                    string text = "SELECT @id:=id FROM ic.games WHERE name=@game;SELECT ic.games.name AS 'gamename',ic.sessions.id,@id AS 'game',ic.sessions.name,ic.sessions.created,ic.sessions.players,ic.sessions.ended FROM ic.sessions LEFT JOIN ic.games ON (ic.games.id = @id)";
                    MySqlCommand command = new MySqlCommand(text, conn);
                    command.Parameters.AddWithValue("@game", game);
                    command.Parameters.Add(new MySqlParameter("@id", MySqlDbType.Int32));
                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        reader.NextResult();
                        while (reader.Read())
                        {
                            Session newSession = new Session()
                            {
                                Id = reader.GetInt32("id"),
                                Game = reader.GetString("game"),
                                GameName = reader.GetString("gamename"),
                                Name = reader.GetString("name"),
                                Created = reader.GetDateTime("created"),
                                PlayersRaw = reader.IsDBNull(reader.GetOrdinal("players")) ? "" : reader.GetString("players"),
                                Ended = reader.GetBoolean("ended")
                            };

                            result.Add(newSession);
                        }
                        reader.Close();

                    }
                }
            }

            return result;
        }

        public static List<Session> GetActiveSessions()
        {
            List<Session> result = new List<Session>();

            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                using (MySqlTransaction tran = conn.BeginTransaction())
                {
                    string text = "SELECT ic.games.name AS 'gamename',ic.sessions.id,@id:=game AS 'game',ic.sessions.name,ic.sessions.created,ic.sessions.players,ic.sessions.ended FROM ic.sessions WHERE ended = 0 LEFT JOIN ic.games ON (ic.games.id = @id)";
                    MySqlCommand command = new MySqlCommand(text, conn);
                    command.Parameters.Add(new MySqlParameter("@id", MySqlDbType.Int32));
                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Session newSession = new Session()
                            {
                                Id = reader.GetInt32("id"),
                                Game = reader.GetString("game"),
                                GameName = reader.GetString("gamename"),
                                Name = reader.GetString("name"),
                                Created = reader.GetDateTime("created"),
                                PlayersRaw = reader.IsDBNull(reader.GetOrdinal("players")) ? "" : reader.GetString("players"),
                                Ended = reader.GetBoolean("ended")
                            };

                            result.Add(newSession);
                        }
                        reader.Close();

                    }
                }
            }

            return result;
        }
        #endregion
        #endregion

        #region Notes
        public static bool CreateNewNote(ulong user, string content, out string message)
        {
            var result = CheckSessionCount(user, out message);
            if (!result)
            {
                return false;
            }
            else
            {
                string text = $"INSERT INTO ic.notes SET created = @created, user = @user, content = @content, session = (SELECT id FROM ic.sessions WHERE ended = 0 AND players LIKE '%{user}%')";
                MySqlCommand command = new MySqlCommand(text);
                command.Parameters.AddWithValue("@created", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
                command.Parameters.AddWithValue("@user", user);
                command.Parameters.AddWithValue("@content", content);
                RunNonQuery(command);
                return true;
            }
        }

        public static Note GetLast(ulong user)
        {
            Note newNote = new Note();

            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                string text = @"SELECT * FROM ic.notes WHERE user = @user ORDER BY created DESC LIMIT 1;";
                MySqlCommand command = new MySqlCommand(text, conn);
                command.Parameters.Add(new MySqlParameter("@id", MySqlDbType.Int32));
                command.Parameters.AddWithValue("@user", user);

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    reader.Read();

                    newNote.Id = reader.GetInt32("id");
                    newNote.Created = reader.GetDateTime("created");
                    newNote.Content = reader.GetString("content");
                    newNote.Voided = reader.GetBoolean("voided");
                    newNote.SessionId = reader.GetInt32("session");
                    newNote.User = reader.GetString("user");
                }
            }

            return newNote;
        }

        public static List<Note> SearchNotes(ulong user, string search, bool softly)
        {
            List<Note> result = new List<Note>();

            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                MySqlCommand command;

                if (!softly)
                {
                    string text = @"SELECT * FROM ic.notes WHERE MATCH (content) AGAINST (@search) AND user = @user ORDER BY created DESC;";
                    command = new MySqlCommand(text, conn);
                    command.Parameters.AddWithValue("@search", search);
                    command.Parameters.AddWithValue("@user", user);
                }
                else
                {
                    string text = @"SELECT * FROM ic.notes WHERE content LIKE @search AND user = @user ORDER BY created DESC;";
                    command = new MySqlCommand(text, conn);
                    command.Parameters.AddWithValue("@search", "%" + search + "%");
                    command.Parameters.AddWithValue("@user", user);
                }

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Note newNote = new Note();
                        newNote.Id = reader.GetInt32("id");
                        newNote.User = reader.GetString("user");
                        newNote.Created = reader.GetDateTime("created");
                        newNote.Voided = reader.GetBoolean("voided");
                        newNote.SessionId = reader.GetInt32("session");
                        newNote.Content = reader.GetString("content");
                        result.Add(newNote);
                    }
                }
            }

            return result;
        }

        public static List<Note> GetThis(ulong user)
        {
            List<Note> result = new List<Note>();

            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                string text = @"SELECT @session := session FROM ic.notes WHERE user = @user ORDER BY created DESC LIMIT 1;
                                SELECT * FROM ic.notes WHERE user = @user AND session = @session;";
                MySqlCommand command = new MySqlCommand(text, conn);
                command.Parameters.Add("@session", MySqlDbType.Int32);
                command.Parameters.AddWithValue("@user", user);

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    reader.NextResult();
                    while (reader.Read())
                    {
                        var newNote = new Note();
                        newNote.Id = reader.GetInt32("id");
                        newNote.Created = reader.GetDateTime("created");
                        newNote.User = reader.GetString("user");
                        newNote.SessionId = reader.GetInt32("session");
                        newNote.Voided = reader.GetBoolean("voided");
                        newNote.Content = reader.GetString("content");
                        result.Add(newNote);
                    }
                }
            }

            return result;
        }

        public static List<Note> GetLastSession(ulong user)
        {
            List<Note> result = new List<Note>();

            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                string text = @"SELECT created, @current := session FROM ic.notes WHERE user = @user ORDER BY created DESC LIMIT 1;
                                SELECT * FROM ic.notes WHERE user = @user AND session != @current ORDER BY created DESC;";
                MySqlCommand command = new MySqlCommand(text, conn);
                command.Parameters.Add("@current", MySqlDbType.Int32);
                command.Parameters.AddWithValue("@user", user);

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    reader.NextResult();
                    while (reader.Read())
                    {
                        var newNote = new Note();
                        newNote.Id = reader.GetInt32("id");
                        newNote.Created = reader.GetDateTime("created");
                        newNote.User = reader.GetString("user");
                        newNote.SessionId = reader.GetInt32("session");
                        newNote.Voided = reader.GetBoolean("voided");
                        newNote.Content = reader.GetString("content");
                        result.Add(newNote);
                    }
                }
            }

            return result;
        }

        public static List<Note> GetAll(ulong user)
        {
            List<Note> result = new List<Note>();

            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                string text = @"SELECT * FROM ic.notes WHERE user = @user ORDER BY created DESC;";
                MySqlCommand command = new MySqlCommand(text, conn);
                command.Parameters.AddWithValue("@user", user);

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    reader.NextResult();
                    while (reader.Read())
                    {
                        var newNote = new Note();
                        newNote.Id = reader.GetInt32("id");
                        newNote.Created = reader.GetDateTime("created");
                        newNote.User = reader.GetString("user");
                        newNote.SessionId = reader.GetInt32("session");
                        newNote.Voided = reader.GetBoolean("voided");
                        newNote.Content = reader.GetString("content");
                        result.Add(newNote);
                    }
                }
            }

            return result;
        }

        public static Status NoteStatus(ulong user)
        {
            Status status = new Status();

            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();


                string text = @"SELECT @session := session FROM ic.notes WHERE user = @user ORDER BY created DESC LIMIT 1;
                                SELECT * FROM ic.notes WHERE user = @user ORDER BY created DESC LIMIT 1;
                                SELECT COUNT(id) FROM ic.notes WHERE user = @user AND session = @session;
                                SELECT COUNT(id) FROM ic.notes WHERE user = @user;
                                SELECT ended FROM ic.sessions WHERE id = @session;";
                MySqlCommand command = new MySqlCommand(text, conn);
                command.Parameters.Add(new MySqlParameter("@session", MySqlDbType.Int32));
                command.Parameters.AddWithValue("@user", user);

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    reader.Read();
                    status.SessionId = reader.GetInt32(0);

                    reader.NextResult();
                    reader.Read();
                    Note newNote = new Note()
                    {
                        Id = reader.GetInt32("id"),
                        Created = reader.GetDateTime("created"),
                        User = reader.GetString("user"),
                        SessionId = reader.GetInt32("session"),
                        Content = reader.GetString("content")
                    };
                    status.LastNote = newNote;

                    reader.NextResult();
                    reader.Read();
                    status.NotesThisSession = reader.GetInt32(0);

                    reader.NextResult();
                    reader.Read();
                    status.NotesAllTime = reader.GetInt32(0);

                    reader.NextResult();
                    reader.Read();
                    status.SessionEnded = reader.GetBoolean(0);

                }
            }

            return status;
        }

        public static void VoidNote(ulong user)
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                string text = @"SELECT @id = id FROM ic.notes WHERE user = @user ORDER BY created DESC LIMIT 1;
                                UPDATE ic.notes SET voided = 1 WHERE id = @id;";
                MySqlCommand command = new MySqlCommand(text, conn);
                command.Parameters.Add(new MySqlParameter("@id", MySqlDbType.Int32));
                command.Parameters.AddWithValue("@user", user);

                command.ExecuteNonQuery();
            }
        }

        private static bool CheckSessionCount(ulong user, out string message)
        {
            message = string.Empty;
            var result = -1;

            string text = $"SELECT COUNT(id) FROM ic.sessions WHERE ended = 0 AND players LIKE '%{user}%'";
            MySqlCommand command = new MySqlCommand(text);
            command.Parameters.AddWithValue("@user", user);
            result = int.Parse(RunScalar(command).ToString());
            if (result > 1)
            {
                message = "There are multiple active sessions you are part of, please ask your DM to end the previous session.";
                return false;
            }
            else if (result == 0)
            {
                message = "You are not in an active session.";
                return false;
            }
            else
            {
                return true;
            }
        }
        #endregion

        #region Feedback
        public static void SubmitFeedback(string user, string feedback, bool dm)
        {
            string text = "INSERT INTO ic.feedback user, feedback, dm VALUES (@user, @feedback, @dm);";
            MySqlCommand command = new MySqlCommand(text);
            command.Parameters.AddWithValue("@user", user);
            command.Parameters.AddWithValue("@feedback", feedback);
            command.Parameters.AddWithValue("@dm", dm == true ? 1 : 0);
            RunNonQuery(command);
        }
        #endregion

        #region Tools
        public static void FillSessions()
        {
            var lines = File.ReadAllLines("Names.txt");

            for (int i = 0; i < 10; i++)
            {
                var random = new Random().Next(0, 4945);
                var name = lines[random];
                CreateNewSession("Filler", name, out int id);
            }
        }

        private static string RandomName()
        {
            var lines = File.ReadAllLines("Names.txt");

            var random = new Random().Next(0, 4945);
            var name = lines[random];

            return name;
        }

        private static bool CheckSessionExists(string game, string name)
        {
            bool exists = false;

            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                string text = "SELECT COUNT(id) FROM ic.sessions WHERE name=@name AND game=@game";
                MySqlCommand command = new MySqlCommand(text, conn);
                command.Parameters.AddWithValue("@name", name);
                command.Parameters.AddWithValue("@game", game);
                var result = RunScalar(command);

                if (int.Parse(result.ToString()) > 0)
                {
                    exists = true;
                }
            }


            return exists;
        }

        #endregion
    }
}
