﻿using Kayak;
using Kayak.Http;
using SharedLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

using SharedLibrary.Objects;
using System.Threading.Tasks;
using SharedLibrary.Services;
using System.Linq.Expressions;
using SharedLibrary.Database.Models;
using System.Collections.Specialized;
using SharedLibrary.Dtos;

namespace IW4MAdmin
{
    public class WebService
    {
        public IServer WebServer { get; private set; }
        public IScheduler WebScheduler { get; private set; }
        public Thread SchedulerThread { get; private set; }

        public void StartScheduler()
        {
            WebScheduler = KayakScheduler.Factory.Create(new Scheduler());
            WebServer = KayakServer.Factory.CreateHttp(new Request(), WebScheduler);

            SharedLibrary.WebService.PageList.Add(new Pages());
            SharedLibrary.WebService.PageList.Add(new Homepage());
            SharedLibrary.WebService.PageList.Add(new ServersJSON());
            SharedLibrary.WebService.PageList.Add(new PlayerHistoryJSON());
            SharedLibrary.WebService.PageList.Add(new Penalties());
            SharedLibrary.WebService.PageList.Add(new PenaltiesJSON());
            SharedLibrary.WebService.PageList.Add(new Players());
            SharedLibrary.WebService.PageList.Add(new GetPlayer());
            SharedLibrary.WebService.PageList.Add(new WebConsole());
            SharedLibrary.WebService.PageList.Add(new ConsoleJSON());
            SharedLibrary.WebService.PageList.Add(new PubbansJSON());
            SharedLibrary.WebService.PageList.Add(new AdminsJSON());
            SharedLibrary.WebService.PageList.Add(new Admins());
            SharedLibrary.WebService.PageList.Add(new Profile());

            SchedulerThread = new Thread(() =>
            {
                ScheduleThreadStart(WebScheduler, WebServer);
            })
            {
                Name = "Web Service Thread"
            };

            SchedulerThread.Start();
        }

        private static void ScheduleThreadStart(IScheduler S, IServer ss)
        {
            try
            {
                string[] webConfig = System.IO.File.ReadAllLines("config\\web.cfg");
                var address = Dns.GetHostAddresses(webConfig[0])[0];
                int port = Convert.ToInt32(webConfig[1]);

                try
                {
                    using (ss.Listen(new IPEndPoint(address, port)))
                        S.Start();
                }

                catch (Exception e)
                {
                    ApplicationManager.GetInstance().Logger.WriteError($"Unable to start webservice ( port is probably in use ): {e.Message}");
                }
            }

            catch (Exception)
            {
                using (ss.Listen(new IPEndPoint(IPAddress.Any, 1624)))
                    S.Start();
            }
        }

        public static HttpResponse GetPage(string path, System.Collections.Specialized.NameValueCollection queryset, IDictionary<string, string> headers)
        {
            if (SharedLibrary.WebService.PageList == null || SharedLibrary.WebService.PageList.Count == 0)
                return new HttpResponse() { content = "Error: page list not initialized!", contentType = "text/plaintext" };

            if (path == null)
                return new HttpResponse() { content = "Error: no path specified", contentType = "text/plaintext" };

            IPage requestedPage = SharedLibrary.WebService.PageList.Find(x => x.GetPath().ToLower() == path.ToLower());

            if (requestedPage != null)
                return Task.Run(async () => await requestedPage.GetPage(queryset, headers)).Result;
            else
            {
                if (File.Exists(path.Replace("/", "\\").Substring(1)))
                {
                    var f = File.ReadAllBytes(path.Replace("/", "\\").Substring(1));

                    if (path.Contains(".css"))
                    {
                        HttpResponse css = new HttpResponse()
                        {
                            additionalHeaders = new Dictionary<string, string>(),
                            content = Encoding.ASCII.GetString(f),
                            contentType = "text/css"
                        };
                        return css;

                    }

                    else if (path.Contains(".js"))
                    {
                        HttpResponse css = new HttpResponse()
                        {
                            additionalHeaders = new Dictionary<string, string>(),
                            content = Encoding.ASCII.GetString(f),
                            contentType = "application/javascript"
                        };
                        return css;
                    }

                    else if (path.Contains(".png"))
                    {
                        HttpResponse png = new HttpResponse()
                        {
                            additionalHeaders = new Dictionary<string, string>(),
                            BinaryContent = f,
                            contentType = "image/png"
                        };

                        return png;
                    }

                }

                requestedPage = new Error404();
                return Task.Run(async () => await requestedPage.GetPage(queryset, headers)).Result;
            }
        }
    }

    class Error404 : IPage
    {
        public string GetName()
        {
            return "404";
        }

        public string GetPath()
        {
            return "";
        }

        public async Task<HttpResponse> GetPage(System.Collections.Specialized.NameValueCollection querySet, IDictionary<string, string> headers)
        {
            HttpResponse resp = new HttpResponse()
            {
                additionalHeaders = new Dictionary<string, string>(),
                content = "404 not found!",
                contentType = GetContentType()
            };
            return resp;
        }

        public string GetContentType()
        {
            return "text/html";
        }

        public bool Visible()
        {
            return false;
        }
    }

    class Homepage : HTMLPage
    {
        public override string GetName()
        {
            return "Home";
        }

        public override string GetPath()
        {
            return "/";
        }

        public override string GetContent(System.Collections.Specialized.NameValueCollection querySet, IDictionary<string, string> headers)
        {
            StringBuilder S = new StringBuilder();
            S.Append(LoadHeader());
            IFile p = new IFile("webfront\\main.html");
            S.Append(p.GetText());
            p.Close();
            S.Append(LoadFooter());

            return S.ToString();
        }
    }

    class ServersJSON : IPage
    {
        public string GetName()
        {
            return "Servers";
        }

        public string GetPath()
        {
            return "/_servers";
        }

        public async Task<HttpResponse> GetPage(System.Collections.Specialized.NameValueCollection querySet, IDictionary<string, string> headers)
        {
            var info = new List<ServerInfo>();
            foreach (Server S in ApplicationManager.GetInstance().Servers)
            {
                ServerInfo eachServer = new ServerInfo()
                {
                    serverName = S.Hostname,
                    serverPort = S.GetPort(),
                    maxPlayers = S.MaxClients,
                    mapName = S.CurrentMap.Alias,
                    gameType = Utilities.GetLocalizedGametype(S.Gametype),
                    currentPlayers = S.GetPlayersAsList().Count,
                    chatHistory = S.ChatHistory,
                    players = new List<PlayerInfo>(),
                    PlayerHistory = S.PlayerHistory.ToArray()
                };

                int ip = querySet["ip"].ConvertToIP();
                //var admins = (await (ApplicationManager.GetInstance().GetClientService() as ClientService).GetPrivilegedClients());
                bool authed = true; //admins.FirstOrDefault(a => a.IPAddress == ip) != null;
               // if (ip == 16777343)
                //    authed = true;

                foreach (Player P in S.GetPlayersAsList())
                {
                    PlayerInfo pInfo = new PlayerInfo()
                    {
                        playerID = P.ClientId,
                        playerName = P.Name,
                        playerLevel = authed ? P.Level.ToString() : Player.Permission.User.ToString()
                    };
                    eachServer.players.Add(pInfo);
                }

                info.Add(eachServer);
            }

            HttpResponse resp = new HttpResponse()
            {
                contentType = GetContentType(),
                content = Newtonsoft.Json.JsonConvert.SerializeObject(info),
                additionalHeaders = new Dictionary<string, string>()
            };
            return resp;
        }

        public string GetContentType()
        {
            return "application/json";
        }

        public bool Visible()
        {
            return false;
        }
    }


    class PlayerHistoryJSON : IPage
    {
        public string GetName()
        {
            return "Player History";
        }

        public string GetPath()
        {
            return "/_playerhistory";
        }

        public async Task<HttpResponse> GetPage(System.Collections.Specialized.NameValueCollection querySet, IDictionary<string, string> headers)
        {

            var history = new SharedLibrary.Helpers.PlayerHistory[0];
            try
            {
                int id = Int32.Parse(querySet["server"]);
                history = ApplicationManager.GetInstance().GetServers()[id].PlayerHistory.ToArray();
            }

            catch (Exception)
            {

            }

            HttpResponse resp = new HttpResponse()
            {
                contentType = GetContentType(),
                content = Newtonsoft.Json.JsonConvert.SerializeObject(history),
                additionalHeaders = new Dictionary<string, string>()
            };
            return resp;
        }

        public string GetContentType()
        {
            return "application/json";
        }

        public bool Visible()
        {
            return false;
        }
    }

    class Info : IPage
    {
        public string GetName()
        {
            return "Info";
        }

        public string GetPath()
        {
            return "/_info";
        }

        public async Task<HttpResponse> GetPage(System.Collections.Specialized.NameValueCollection querySet, IDictionary<string, string> headers)
        {
            ApplicationInfo info = new ApplicationInfo()
            {
                name = "IW4MAdmin",
                version = Program.Version
            };
            HttpResponse resp = new HttpResponse()
            {
                contentType = GetContentType(),
                content = Newtonsoft.Json.JsonConvert.SerializeObject(info),
                additionalHeaders = new Dictionary<string, string>()
            };
            return resp;
        }

        public string GetContentType()
        {
            return "application/json";
        }

        public bool Visible()
        {
            return false;
        }
    }


    class ConsoleJSON : IPage
    {
        public string GetName()
        {
            return "_Console";
        }

        public string GetPath()
        {
            return "/_console";
        }

        public async Task<HttpResponse> GetPage(System.Collections.Specialized.NameValueCollection querySet, IDictionary<string, string> headers)
        {
            var cmd = new List<SharedLibrary.Helpers.CommandResult>();

            if (querySet["command"] != null)
            {

                if (querySet["server"] != null)
                {
                    Server S = ApplicationManager.GetInstance().Servers.ToList().Find(x => (x.GetPort().ToString() == querySet["server"]));

                    if (S != null)
                    {
                        int ip = querySet["ip"].ConvertToIP();
                        var admins = (await (ApplicationManager.GetInstance().GetClientService() as ClientService).GetPrivilegedClients());
                        Player admin = admins.FirstOrDefault(a => a.IPAddress == ip)?.AsPlayer();
                        if (ip == 16777343)
                            admin = admins.First(a => a.IPAddress == 0).AsPlayer();

                        if (admin == null)
                            admin = new Player() { Name = "RestUser" };

                        Event remoteEvent = new Event(Event.GType.Say, querySet["command"], admin, null, S)
                        {
                            Remote = true
                        };
                        admin.CurrentServer = S;
                        await S.ExecuteEvent(remoteEvent);

                        var results = S.commandResult.Where(c => c.Clientd == admin.ClientId).ToList();
                        cmd.AddRange(results);

                        for (int i = 0; i < results.Count(); i++)
                            S.commandResult.Remove(results[i]);

                    }
                    else
                        cmd.Add(new SharedLibrary.Helpers.CommandResult() { Clientd = 0, Message = "Invalid server selected" });
                }
                else
                    cmd.Add(new SharedLibrary.Helpers.CommandResult() { Clientd = 0, Message = "No server selected" });
            }

            else
            {
                cmd.Add(new SharedLibrary.Helpers.CommandResult() { Clientd = 0, Message = "No command entered" });
            }

            HttpResponse resp = new HttpResponse()
            {
                contentType = GetContentType(),
                content = Newtonsoft.Json.JsonConvert.SerializeObject(cmd),
                additionalHeaders = new Dictionary<string, string>()
            };
            return resp;
        }

        public string GetContentType()
        {
            return "application/json";
        }

        public bool Visible()
        {
            return false;
        }
    }


    class PenaltiesJSON : IPage
    {
        public string GetName()
        {
            return "Penalties";
        }

        public string GetPath()
        {
            return "/_penalties";
        }

        public async Task<HttpResponse> GetPage(System.Collections.Specialized.NameValueCollection querySet, IDictionary<string, string> headers)
        {
            int from = 0;
            if (querySet["from"] != null)
                from = Int32.Parse(querySet["from"]);

            List<PenaltyInfo> info = new List<PenaltyInfo>();

            foreach (var penalty in await ApplicationManager.GetInstance().GetPenaltyService().GetRecentPenalties(15, from))
            {

                PenaltyInfo pInfo = new PenaltyInfo()
                {
                    adminName = penalty.Punisher.Name,
                    adminLevel = penalty.Punisher.Level.ToString(),
                    penaltyReason = penalty.Offense,
                    penaltyTime = Utilities.GetTimePassed(penalty.When),
                    penaltyType = penalty.Type.ToString(),
                    playerName = penalty.Offender.Name,
                    playerID = penalty.Offender.ClientId,
                    Expires = penalty.Expires > DateTime.UtcNow ? (penalty.Expires - DateTime.UtcNow).TimeSpanText() : ""
                };
                info.Add(pInfo);
            }

            HttpResponse resp = new HttpResponse()
            {
                contentType = GetContentType(),
                content = Newtonsoft.Json.JsonConvert.SerializeObject(info),
                additionalHeaders = new Dictionary<string, string>()
            };
            return resp;
        }

        public string GetContentType()
        {
            return "application/json";
        }

        public bool Visible()
        {
            return false;
        }
    }

    class Penalties : HTMLPage
    {
        public override string GetName()
        {
            return "Penalties";
        }

        public override string GetPath()
        {
            return "/penalties";
        }

        public override string GetContent(System.Collections.Specialized.NameValueCollection querySet, IDictionary<string, string> headers)
        {
            StringBuilder S = new StringBuilder();
            S.Append(LoadHeader());

            IFile penalities = new IFile("webfront\\penalties.html");
            S.Append(penalities.GetText());
            penalities.Close();

            S.Append(LoadFooter());

            return S.ToString();
        }
    }

    class WebConsole : HTMLPage
    {
        public override string GetName()
        {
            return "Console";
        }

        public override string GetPath()
        {
            return "/console";
        }

        public override string GetContent(System.Collections.Specialized.NameValueCollection querySet, IDictionary<string, string> headers)
        {
            StringBuilder S = new StringBuilder();
            S.Append(LoadHeader());

            IFile console = new IFile("webfront\\console.html");
            S.Append(console.GetText());
            console.Close();

            S.Append(LoadFooter());

            return S.ToString();
        }
    }

    class Players : HTMLPage
    {
        public override string GetName()
        {
            return "Players";
        }

        public override string GetPath()
        {
            return "/players";
        }

        public override string GetContent(System.Collections.Specialized.NameValueCollection querySet, IDictionary<string, string> headers)
        {
            StringBuilder S = new StringBuilder();
            S.Append(LoadHeader());

            IFile penalities = new IFile("webfront\\players.html");
            S.Append(penalities.GetText());
            penalities.Close();

            S.Append(LoadFooter());

            return S.ToString();
        }
    }


    class Admins : HTMLPage
    {
        public override string GetName()
        {
            return "Admins";
        }

        public override string GetPath()
        {
            return "/Admins";
        }

        public override string GetContent(System.Collections.Specialized.NameValueCollection querySet, IDictionary<string, string> headers)
        {
            StringBuilder S = new StringBuilder();
            S.Append(LoadHeader());

            IFile admins = new IFile("webfront\\admins.html");
            S.Append(admins.GetText());
            admins.Close();

            S.Append(LoadFooter());

            return S.ToString();
        }
    }

    class AdminsJSON : IPage
    {
        public string GetName()
        {
            return "Admins Json";
        }

        public string GetPath()
        {
            return "/GetAdmins";
        }

        public async Task<HttpResponse> GetPage(System.Collections.Specialized.NameValueCollection querySet, IDictionary<string, string> headers)
        {
            var Admins = (await ApplicationManager.GetInstance().GetClientService()
                .GetPrivilegedClients())
                .OrderByDescending(a => a.Level);

            HttpResponse resp = new HttpResponse()
            {
                contentType = GetContentType(),
                content = Admins.Select(a => new
                {
                    a.ClientId,
                    a.Level,
                    a.Name,
                    playerID = a.ClientId
                }),
                additionalHeaders = new Dictionary<string, string>()
            };
            return resp;
        }

        public string GetContentType()
        {
            return "application/json";
        }

        public bool Visible()
        {
            return false;
        }
    }

    class PubbansJSON : IPage
    {
        public string GetName()
        {
            return "Public Ban List";
        }

        public string GetPath()
        {
            return "/pubbans";
        }

        public async Task<HttpResponse> GetPage(System.Collections.Specialized.NameValueCollection querySet, IDictionary<string, string> headers)
        {
            HttpResponse resp = new HttpResponse()
            {
                /*  contentType = GetContentType(),
                  content = Newtonsoft.Json.JsonConvert
                      .SerializeObject(((ApplicationManager.GetInstance().GetClientPenalties()) as PenaltyList)
                      .AsChronoList(Convert.ToInt32(querySet["from"]), 50, Penalty.Type.Ban), Newtonsoft.Json.Formatting.Indented, new Newtonsoft.Json.JsonConverter[] {
                          new Newtonsoft.Json.Converters.StringEnumConverter()
                      }),
                  additionalHeaders = new Dictionary<string, string>()*/
            };
            return resp;
        }

        public string GetContentType()
        {
            return "application/json";
        }

        public bool Visible()
        {
            return false;
        }
    }

    class Pages : IPage
    {
        public string GetName()
        {
            return "Pages";
        }

        public string GetPath()
        {
            return "/pages";
        }

        public async Task<HttpResponse> GetPage(NameValueCollection querySet, IDictionary<string, string> headers)
        {
            var pages = SharedLibrary.WebService.PageList.Select(p => new
            {
                pagePath = p.GetPath(),
                pageName = p.GetName(),
                visible = p.Visible(),
            });

            HttpResponse resp = new HttpResponse()
            {
                contentType = GetContentType(),
                content = Newtonsoft.Json.JsonConvert.SerializeObject(pages.ToArray()),
                additionalHeaders = new Dictionary<string, string>()
            };
            return resp;
        }

        public string GetContentType()
        {
            return "application/json";
        }

        public bool Visible()
        {
            return false;
        }
    }

    class GetPlayer : IPage
    {
        public string GetContentType()
        {
            return "application/json";
        }

        public string GetPath()
        {
            return "/getplayer";
        }

        public string GetName()
        {
            return "GetPlayer";
        }

        public async Task<HttpResponse> GetPage(NameValueCollection querySet, IDictionary<string, string> headers)
        {
            List<PlayerInfo> pInfo = new List<PlayerInfo>();
            IList<EFClient> matchedPlayers = new List<EFClient>();
            HttpResponse resp = new HttpResponse()
            {
                contentType = GetContentType(),
                additionalHeaders = new Dictionary<string, string>()
            };

            //  int ip = querySet["IP"].ConvertToIP();
            //var admins = (await (ApplicationManager.GetInstance().GetClientService() as ClientService).GetPrivilegedClients());
            //   bool authed = admins.FirstOrDefault(c => c.IPAddress == ip) != null || ip == 16777343;
            bool authed = true;
            bool recent = false;
            bool individual = querySet["id"] != null;

            if (individual)
            {
                matchedPlayers.Add(await ApplicationManager.GetInstance().GetClientService().Get(Convert.ToInt32(querySet["id"])));
            }

            else if (querySet["npID"] != null)
            {
                matchedPlayers.Add(await ApplicationManager.GetInstance().GetClientService().GetUnique(querySet["npID"].ConvertLong()));
            }

            else if (querySet["name"] != null)
            {
                matchedPlayers = (await ApplicationManager.GetInstance().GetClientService().GetClientByName(querySet["name"]));
            }

            else if (querySet["recent"] != null)
            {
                int offset = 0;
                if (querySet["offset"] != null)
                    offset = Int32.Parse(querySet["offset"]);
                if (offset < 0)
                    throw new FormatException("Invalid offset");

                matchedPlayers = await ApplicationManager.GetInstance().GetClientService().GetRecentClients(offset, 15);
                recent = true;
            }

            bool isProfile = querySet["profile"] != null;

            if (matchedPlayers != null && matchedPlayers.Count > 0)
            {
                foreach (var pp in matchedPlayers)
                {
                    if (pp == null) continue;

                    List<ProfileMeta> meta = new List<ProfileMeta>();
                    if (isProfile)
                    {
                        meta.AddRange(await ApplicationManager.GetInstance().GetPenaltyService().ReadGetClientPenaltiesAsync(pp.ClientId));
                        meta.AddRange(await ApplicationManager.GetInstance().GetPenaltyService().ReadGetClientPenaltiesAsync(pp.ClientId, false));
                        meta.AddRange(await MetaService.GetMeta(pp.ClientId));
                    }

                    PlayerInfo eachPlayer = new PlayerInfo()
                    {
                        playerIP = pp.IPAddressString,
                        playerID = pp.ClientId,
                        playerLevel = pp.Level.ToString(),
                        playerName = pp.Name,
                        playernpID = pp.NetworkId.ToString(),
                        authed = authed,
                        playerAliases = new List<string>(),
                        playerIPs = new List<string>(),
                        Meta = meta.OrderByDescending(m => m.When).ToList(),
                        FirstSeen = Utilities.GetTimePassed(pp.FirstConnection, false),
                        TimePlayed = Math.Round(pp.TotalConnectionTime / 3600.0, 1).ToString("#,##0"),
                        LastSeen = Utilities.GetTimePassed(pp.LastConnection, false)
                    };

                    if (!recent)
                    {
                        eachPlayer.playerAliases = pp.AliasLink.Children
                            .Where(a => a.Name != eachPlayer.playerName)
                            .OrderBy(a => a.Name)
                            .Select(a => a.Name)
                            .Distinct()
                            .ToList();

                        if (authed)
                            eachPlayer.playerIPs = pp.AliasLink.Children
                                .Select(a => a.IPAddress.ConvertIPtoString())
                                .Distinct()
                                .ToList();
                    }

                    //eachPlayer.playerAliases = eachPlayer.playerAliases.Distinct().ToList();
                    // eachPlayer.playerIPs = eachPlayer.playerIPs.Distinct().ToList();

                    eachPlayer.playerConnections = pp.Connections;
                    pInfo.Add(eachPlayer);

                }

                resp.content = pInfo;
                return resp;
            }

            resp.content = Newtonsoft.Json.JsonConvert.SerializeObject(null);
            return resp;
        }

        public bool Visible()
        {
            return false;
        }
    }

    class Profile : HTMLPage
    {
        public Profile() : base(false) {}
        public override string GetPath() => "/profile";
        public override string GetContent(NameValueCollection querySet, IDictionary<string, string> headers)
        {
            IFile admins = new IFile("webfront\\profile.html");
            string content = admins.GetText();
            admins.Close();

            return content;
        }

        public override string GetName() => "Client Profile";
    }

    [Serializable]
    struct ServerInfo
    {
        public string serverName;
        public int serverPort;
        public string mapName;
        public string gameType;
        public int currentPlayers;
        public int maxPlayers;
        public List<Chat> chatHistory;
        public List<PlayerInfo> players;
        public SharedLibrary.Helpers.PlayerHistory[] PlayerHistory;
        public int ID;
    }

    [Serializable]
    struct ApplicationInfo
    {
        public double version;
        public string name;
    }

    [Serializable]
    struct PlayerInfo
    {
        public string playerName;
        public int playerID;
        public string playerLevel;
        public string playerIP;
        public string playernpID;
        public List<string> playerAliases;
        public List<string> playerIPs;
        public int playerConnections;
        public string LastSeen;
        public string FirstSeen;
        public string TimePlayed;
        public bool authed;
        public List<ProfileMeta> Meta;
    }

    [Serializable]
    struct PenaltyInfo
    {
        public string playerName;
        public int playerID;
        public string adminName;
        public string adminLevel;
        public string penaltyType;
        public string penaltyReason;
        public string penaltyTime;
        public string Expires;
    }
}