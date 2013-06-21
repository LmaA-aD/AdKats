/* 
 * AdKats is a MySQL reflected admin tool for Procon Frostbite.
 * 
 * A MySQL reflected admin toolset that includes editable in-game commands, database reflected punishment and
 * forgiveness, proper player report and admin call handling, player name completion, player muting, yell/say 
 * pre-recording, and internal implementation of TeamSwap.
 * 
 * Requires a MySQL Database connection for proper use. Will set up needed tables in the database if they are 
 * not there already.
 * 
 * AdKats was inspired by the gaming community A Different Kind (ADK). With help from the BF3 Admins within the community the plugin was born. 
 * Visit http://www.adkgamers.com/ to say thanks for the awesome plugin.
 * 
 * Code Credit:
 * Modded Levenshtein Distance algorithm from Micovery's InsaneLimits
 * Threading Examples from Micovery's InsaneLimits
 * Planned Future Usage:
 * Email System from "Notify Me!" By MorpheusX(AUT)
 * Twitter Post System from Micovery's InsaneLimits
 * 
 * AdKats.cs
 */

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Security.Cryptography;
using System.Collections;
using System.Net;
using System.Net.Mail;
using System.Web;
using System.Data;
using System.Threading;
using System.Timers;
using System.Diagnostics;
using System.Reflection;

using MySql.Data.MySqlClient;

using PRoCon.Core;
using PRoCon.Core.Plugin;
using PRoCon.Core.Plugin.Commands;
using PRoCon.Core.Players;
using PRoCon.Core.Players.Items;
using PRoCon.Core.Battlemap;
using PRoCon.Core.Maps;
using PRoCon.Core.HttpServer;


namespace PRoConEvents
{
    //Aliases
    using EventType = PRoCon.Core.Events.EventType;
    using CapturableEvent = PRoCon.Core.Events.CapturableEvents;

    public class AdKats : PRoConPluginAPI, IPRoConPluginInterface
    {
        #region Variables

        string plugin_version = "0.2.7.0";

        //Enumerations
        //Messaging
        public enum MessageTypeEnum
        {
            Warning,
            Error,
            Exception,
            Normal
        };
        //Admin Commands
        public enum AdKat_CommandType
        {
            //Case for use while parsing and handling errors
            Default,
            //Confirm or cancel a command
            ConfirmCommand,
            CancelCommand,
            //Moving players
            MovePlayer,
            ForceMovePlayer,
            Teamswap,
            RoundWhitelistPlayer,
            //Punishing players
            KillPlayer,
            KickPlayer,
            TempBanPlayer,
            PermabanPlayer,
            PunishPlayer,
            ForgivePlayer,
            MutePlayer,
            //Reporting players
            ReportPlayer,
            CallAdmin,
            //Phantom Command
            ConfirmReport,
            //Round Commands
            RestartLevel,
            NextLevel,
            EndLevel,
            //Messaging
            AdminSay,
            PlayerSay,
            AdminYell,
            PlayerYell,
            WhatIs,
            //Power Corner
            NukeServer,
            KickAll
        };
        //Source of commands
        public enum AdKat_CommandSource
        {
            Default,
            InGame,
            Console,
            Settings,
            Database,
            HTTP
        }
        //Player ban types
        public enum AdKat_BanType
        {
            FrostbiteName,
            FrostbiteEaGuid,
            PunkbusterGuid
        };

        // General settings
        private int server_id = -1;
        //Whether to get the release version of plugin description and setup scripts, or the dev version.
        //This setting is unchangeable by users, and will always be TRUE for released versions of the plugin.
        private bool isRelease = false;
        //Should always be false for releases
        private bool isTesting = true;
        //Whether the plugin is enabled
        private volatile bool isEnabled;
        private volatile bool threadsReady;
        //Current debug level
        private volatile int debugLevel;
        private String debugSoldierName = "ColColonCleaner";
        //IDs of the two teams as the server understands it
        private static int USTeamId = 1;
        private static int RUTeamId = 2;
        //last time a manual call to listplayers was made
        private DateTime lastListPlayersRequest = DateTime.Now;
        //All server info
        private CServerInfo serverInfo = null;

        // Player Lists
        Dictionary<string, CPlayerInfo> playerDictionary = new Dictionary<string, CPlayerInfo>();
        List<CPlayerInfo> playerList = new List<CPlayerInfo>();
        //player counts per team
        private int USPlayerCount = 0;
        private int RUPlayerCount = 0;

        // Admin Settings
        private Dictionary<string, int> playerAccessCache = new Dictionary<string, int>();
        private Boolean toldCol = false;

        // MySQL Settings
        private volatile Boolean dbSettingsChanged = true;
        private string mySqlHostname = "";
        private string mySqlPort = "";
        private string mySqlDatabaseName = "";
        private string mySqlUsername = "";
        private string mySqlPassword = "";
        //frequency in seconds to fetch at
        private DateTime lastDBAccessFetch = DateTime.Now;
        private int dbAccessFetchFrequency = 300;
        //Action fetch from database settings
        private Boolean fetchActionsFromDB = false;
        private DateTime lastDBActionFetch = DateTime.Now;
        private int dbActionFrequency = 10;

        //current ban type
        private string m_strBanTypeOption = "Frostbite - EA GUID";
        private AdKat_BanType m_banMethod;
        private Boolean useBanAppend = false;
        private string banAppend = "Appeal at your_site.com";

        //Command Strings for Input
        private DateTime commandStartTime = DateTime.Now;
        //Player Interaction
        private string m_strKillCommand = "kill|log";
        private string m_strKickCommand = "kick|log";
        private string m_strTemporaryBanCommand = "tban|log";
        private string m_strPermanentBanCommand = "ban|log";
        private string m_strPunishCommand = "punish|log";
        private string m_strForgiveCommand = "forgive|log";
        private string m_strMuteCommand = "mute|log";
        private string m_strRoundWhitelistCommand = "roundwhitelist|log";
        private string m_strMoveCommand = "move|log";
        private string m_strForceMoveCommand = "fmove|log";
        private string m_strTeamswapCommand = "moveme|log";
        private string m_strReportCommand = "report|log";
        private string m_strCallAdminCommand = "admin|log";
        //Admin messaging
        private string m_strSayCommand = "say|log";
        private string m_strPlayerSayCommand = "psay|log";
        private string m_strYellCommand = "yell|log";
        private string m_strPlayerYellCommand = "pyell|log";
        private string m_strWhatIsCommand = "whatis";
        private List<string> preMessageList = new List<string>();
        private Boolean requirePreMessageUse = false;
        private int m_iShowMessageLength = 5;
        private string m_strShowMessageLength = "5";
        //Map control
        private string m_strRestartLevelCommand = "restart|log";
        private string m_strNextLevelCommand = "nextlevel|log";
        private string m_strEndLevelCommand = "endround|log";
        //Power corner
        private string m_strNukeCommand = "nuke|log";
        private string m_strKickAllCommand = "kickall|log";
        //Confirm and cancel
        private string m_strConfirmCommand = "yes";
        private string m_strCancelCommand = "no";
        //Used to parse incoming commands quickly
        public Dictionary<string, AdKat_CommandType> AdKat_CommandStrings;
        public Dictionary<AdKat_CommandType, int> AdKat_CommandAccessRank;
        //Database record types
        public Dictionary<AdKat_CommandType, string> AdKat_RecordTypes;
        public Dictionary<string, AdKat_CommandType> AdKat_RecordTypesInv;
        //Logging settings
        public Dictionary<AdKat_CommandType, Boolean> AdKat_LoggingSettings;

        //External Access Settings
        //Randomized on startup
        private string externalCommandAccessKey = "NoPasswordSet";

        //When an action requires confirmation, this dictionary holds those actions until player confirms action
        private Dictionary<string, AdKat_Record> actionConfirmDic = new Dictionary<string, AdKat_Record>();
        //Whether to combine server punishments
        private Boolean combineServerPunishments = false;
        //IRO punishment setting
        private Boolean IROOverridesLowPop = false;
        //Default hierarchy of punishments
        private string[] punishmentHierarchy = 
        {
            "kill",
            "kill",
            "kick",
            "tban60",
            "tbanday",
            "tbanweek",
            "tban2weeks",
            "tbanmonth",
            "ban"
        };
        //When punishing, only kill players when server is in low population
        private Boolean onlyKillOnLowPop = true;
        //Default for low populations
        private int lowPopPlayerCount = 20;
        //Default required reason length
        private int requiredReasonLength = 5;

        //TeamSwap Settings
        //The list of players on RU wishing to move to US (This list takes first priority)
        private Queue<CPlayerInfo> USMoveQueue = new Queue<CPlayerInfo>();
        //the list of players on US wishing to move to RU (This list takes secondary)
        private Queue<CPlayerInfo> RUMoveQueue = new Queue<CPlayerInfo>();
        //whether to allow all players, or just players in the whitelist
        private Boolean requireTeamswapWhitelist = true;
        //the lowest ticket count of either team
        private volatile int lowestTicketCount = 500000;
        //the highest ticket count of either team
        private volatile int highestTicketCount = 0;
        //the highest ticket count of either team to allow self move
        private int teamSwapTicketWindowHigh = 500000;
        //the lowest ticket count of either team to allow self move
        private int teamSwapTicketWindowLow = 0;
        //Round only whitelist
        private Dictionary<string, bool> teamswapRoundWhitelist = new Dictionary<string, bool>();
        //Number of random players to whitelist at the beginning of the round
        private int playersToAutoWhitelist = 2;

        //Reports for the current round
        private Dictionary<string, AdKat_Record> round_reports = new Dictionary<string, AdKat_Record>();

        //Player Muting
        private string mutedPlayerMuteMessage = "You have been muted by an admin, talking will cause punishment. You can speak again next round.";
        private string mutedPlayerKillMessage = "Do not talk while muted. You can speak again next round.";
        private string mutedPlayerKickMessage = "Talking excessively while muted.";
        private int mutedPlayerChances = 5;
        private Dictionary<string, int> round_mutedPlayers = new Dictionary<string, int>();

        //Admin Assistants
        private Boolean enableAdminAssistants = true;
        private Dictionary<string, bool> adminAssistantCache = new Dictionary<string, bool>();
        private int minimumRequiredWeeklyReports = 5;

        //Mail Settings
        private string strHostName;
        private string strPort;
        private Boolean sendmail;
        private Boolean blUseSSL;
        private string strSMTPServer;
        private int iSMTPPort;
        private string strSenderMail;
        private List<string> lstReceiverMail;
        private string strSMTPUser;
        private string strSMTPPassword;

        //Multi-Threading

        //Threads
        Thread MessagingThread;
        Thread CommandParsingThread;
        Thread DatabaseCommThread;
        Thread ActionHandlingThread;
        Thread TeamSwapThread;
        Thread activator;
        Thread finalizer;

        //Mutexes
        public Object playersMutex = new Object();
        public Object banListMutex = new Object();
        public Object reportsMutex = new Object();
        public Object actionConfirmMutex = new Object();
        public Object playerAccessMutex = new Object();
        public Object teamswapMutex = new Object();
        public Object serverInfoMutex = new Object();

        public Object unparsedMessageMutex = new Object();
        public Object unparsedCommandMutex = new Object();
        public Object unprocessedRecordMutex = new Object();
        public Object unprocessedActionMutex = new Object();

        //Handles
        EventWaitHandle teamswapHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        EventWaitHandle listPlayersHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        EventWaitHandle messageParsingHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        EventWaitHandle commandParsingHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        EventWaitHandle dbCommHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        EventWaitHandle actionHandlingHandle = new EventWaitHandle(false, EventResetMode.ManualReset);

        //Threading Queues
        Queue<KeyValuePair<String, String>> unparsedMessageQueue = new Queue<KeyValuePair<String, String>>();
        Queue<KeyValuePair<String, String>> unparsedCommandQueue = new Queue<KeyValuePair<String, String>>();

        Queue<AdKat_Record> unprocessedRecordQueue = new Queue<AdKat_Record>();
        Queue<AdKat_Record> unprocessedActionQueue = new Queue<AdKat_Record>();

        Queue<KeyValuePair<String, int>> playerAccessUpdateQueue = new Queue<KeyValuePair<String, int>>();
        Queue<String> playerAccessRemovalQueue = new Queue<String>();

        Queue<CPlayerInfo> banEnforcementQueue = new Queue<CPlayerInfo>();

        //Force move action queue
        Queue<CPlayerInfo> teamswapForceMoveQueue = new Queue<CPlayerInfo>();
        //Delayed move list
        private Dictionary<String, CPlayerInfo> teamswapOnDeathMoveDic = new Dictionary<String, CPlayerInfo>();
        //Delayed move checking queue
        Queue<CPlayerInfo> teamswapOnDeathCheckingQueue = new Queue<CPlayerInfo>();

        //Ban Lists
        //Bans will be enforced by Name, GUID, and IP
        private Dictionary<string, AdKat_Ban> AdKat_BanList_Name = new Dictionary<string, AdKat_Ban>();
        private Dictionary<string, AdKat_Ban> AdKat_BanList_GUID = new Dictionary<string, AdKat_Ban>();
        private Dictionary<string, AdKat_Ban> AdKat_BanList_IP = new Dictionary<string, AdKat_Ban>();

        #endregion

        public AdKats()
        {
            this.isEnabled = false;
            this.threadsReady = false;
            debugLevel = 0;

            this.externalCommandAccessKey = AdKats.GetRandom64BitHashCode();

            preMessageList.Add("US TEAM: DO NOT BASERAPE, YOU WILL BE PUNISHED.");
            preMessageList.Add("RU TEAM: DO NOT BASERAPE, YOU WILL BE PUNISHED.");
            preMessageList.Add("US TEAM: DO NOT ENTER THE STREETS BEYOND 'A', YOU WILL BE PUNISHED.");
            preMessageList.Add("RU TEAM: DO NOT GO BEYOND THE BLACK LINE ON CEILING BY 'C' FLAG, YOU WILL BE PUNISHED.");
            preMessageList.Add("THIS SERVER IS NO EXPLOSIVES, YOU WILL BE PUNISHED FOR INFRACTIONS.");
            preMessageList.Add("JOIN OUR TEAMSPEAK AT TS.ADKGAMERS.COM:3796");

            //Create command and logging dictionaries
            this.AdKat_CommandStrings = new Dictionary<string, AdKat_CommandType>();
            this.AdKat_LoggingSettings = new Dictionary<AdKat_CommandType, Boolean>();

            //Fill command and logging dictionaries by calling rebind
            this.rebindAllCommands();

            //Create database dictionaries
            this.AdKat_RecordTypes = new Dictionary<AdKat_CommandType, string>();
            this.AdKat_RecordTypesInv = new Dictionary<string, AdKat_CommandType>();
            this.AdKat_CommandAccessRank = new Dictionary<AdKat_CommandType, int>();

            //Fill DB record types for outgoing database commands
            this.AdKat_RecordTypes.Add(AdKat_CommandType.MovePlayer, "Move");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.ForceMovePlayer, "ForceMove");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.Teamswap, "Teamswap");

            this.AdKat_RecordTypes.Add(AdKat_CommandType.KillPlayer, "Kill");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.KickPlayer, "Kick");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.TempBanPlayer, "TempBan");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.PermabanPlayer, "PermaBan");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.PunishPlayer, "Punish");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.ForgivePlayer, "Forgive");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.MutePlayer, "Mute");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.RoundWhitelistPlayer, "RoundWhitelist");

            this.AdKat_RecordTypes.Add(AdKat_CommandType.ReportPlayer, "Report");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.CallAdmin, "CallAdmin");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.ConfirmReport, "ConfirmReport");

            this.AdKat_RecordTypes.Add(AdKat_CommandType.AdminSay, "AdminSay");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.PlayerSay, "PlayerSay");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.AdminYell, "AdminYell");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.PlayerYell, "PlayerYell");

            this.AdKat_RecordTypes.Add(AdKat_CommandType.RestartLevel, "RestartLevel");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.NextLevel, "NextLevel");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.EndLevel, "EndLevel");

            this.AdKat_RecordTypes.Add(AdKat_CommandType.NukeServer, "Nuke");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.KickAll, "KickAll");

            //Fill DB Inverse record types for incoming database commands
            this.AdKat_RecordTypesInv.Add("Move", AdKat_CommandType.MovePlayer);
            this.AdKat_RecordTypesInv.Add("ForceMove", AdKat_CommandType.ForceMovePlayer);
            this.AdKat_RecordTypesInv.Add("Teamswap", AdKat_CommandType.Teamswap);

            this.AdKat_RecordTypesInv.Add("Kill", AdKat_CommandType.KillPlayer);
            this.AdKat_RecordTypesInv.Add("Kick", AdKat_CommandType.KickPlayer);
            this.AdKat_RecordTypesInv.Add("TempBan", AdKat_CommandType.TempBanPlayer);
            this.AdKat_RecordTypesInv.Add("PermaBan", AdKat_CommandType.PermabanPlayer);
            this.AdKat_RecordTypesInv.Add("Punish", AdKat_CommandType.PunishPlayer);
            this.AdKat_RecordTypesInv.Add("Forgive", AdKat_CommandType.ForgivePlayer);
            this.AdKat_RecordTypesInv.Add("Mute", AdKat_CommandType.MutePlayer);
            this.AdKat_RecordTypesInv.Add("RoundWhitelist", AdKat_CommandType.RoundWhitelistPlayer);

            this.AdKat_RecordTypesInv.Add("Report", AdKat_CommandType.ReportPlayer);
            this.AdKat_RecordTypesInv.Add("CallAdmin", AdKat_CommandType.CallAdmin);
            this.AdKat_RecordTypesInv.Add("ConfirmReport", AdKat_CommandType.ConfirmReport);

            this.AdKat_RecordTypesInv.Add("AdminSay", AdKat_CommandType.AdminSay);
            this.AdKat_RecordTypesInv.Add("PlayerSay", AdKat_CommandType.PlayerSay);
            this.AdKat_RecordTypesInv.Add("AdminYell", AdKat_CommandType.AdminYell);
            this.AdKat_RecordTypesInv.Add("PlayerYell", AdKat_CommandType.PlayerYell);

            this.AdKat_RecordTypesInv.Add("RestartLevel", AdKat_CommandType.RestartLevel);
            this.AdKat_RecordTypesInv.Add("NextLevel", AdKat_CommandType.NextLevel);
            this.AdKat_RecordTypesInv.Add("EndLevel", AdKat_CommandType.EndLevel);

            this.AdKat_RecordTypesInv.Add("Nuke", AdKat_CommandType.NukeServer);
            this.AdKat_RecordTypesInv.Add("KickAll", AdKat_CommandType.KickAll);

            //Fill all command access ranks
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.RestartLevel, 0);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.NextLevel, 0);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.EndLevel, 0);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.NukeServer, 0);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.KickAll, 0);

            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.PermabanPlayer, 1);

            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.TempBanPlayer, 2);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.RoundWhitelistPlayer, 2);

            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.KillPlayer, 3);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.KickPlayer, 3);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.PunishPlayer, 3);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.ForgivePlayer, 3);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.MutePlayer, 3);

            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.MovePlayer, 4);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.ForceMovePlayer, 4);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.AdminSay, 4);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.AdminYell, 4);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.PlayerSay, 4);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.PlayerYell, 4);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.WhatIs, 4);

            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.Teamswap, 5);

            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.ReportPlayer, 6);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.CallAdmin, 6);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.ConfirmReport, 6);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.ConfirmCommand, 6);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.CancelCommand, 6);

            this.sendmail = false;
            this.blUseSSL = false;
            this.strSMTPServer = String.Empty;
            this.iSMTPPort = 25;
            this.strSenderMail = String.Empty;
            this.lstReceiverMail = new List<string>();
            this.strSMTPUser = String.Empty;
            this.strSMTPPassword = String.Empty;

            //Initialize the threads
            this.InitThreads();
        }

        #region Plugin details

        public string GetPluginName()
        {
            return "AdKats";
        }

        public string GetPluginVersion()
        {
            return this.plugin_version;
        }

        public string GetPluginAuthor()
        {
            return "ColColonCleaner";
        }

        public string GetPluginWebsite()
        {
            return "https://github.com/ColColonCleaner/AdKats/";
        }

        public string GetPluginDescription()
        {
            string pluginDescription = "DESCRIPTION FETCH FAILED|";
            string pluginChangelog = "CHANGELOG FETCH FAILED";
            WebClient client = new WebClient();
            if (this.isRelease)
            {
                pluginDescription = client.DownloadString("https://raw.github.com/ColColonCleaner/AdKats/master/README.md");
                pluginChangelog = client.DownloadString("https://raw.github.com/ColColonCleaner/AdKats/master/CHANGELOG.md");
            }
            else
            {
                pluginDescription = client.DownloadString("https://raw.github.com/ColColonCleaner/AdKats/dev/README.md");
                pluginChangelog = client.DownloadString("https://raw.github.com/ColColonCleaner/AdKats/dev/CHANGELOG.md");
            }
            return pluginDescription + pluginChangelog;
        }

        #endregion

        #region Plugin settings

        public List<CPluginVariable> GetDisplayPluginVariables()
        {
            //Get storage variables
            List<CPluginVariable> lstReturn;

            if (!this.threadsReady)
            {
                lstReturn = new List<CPluginVariable>();

                lstReturn.Add(new CPluginVariable("Complete these settings before enabling.", typeof(string), "Once enabled, more settings will appear."));
                //Server Settings
                lstReturn.Add(new CPluginVariable("1. Server Settings|Server ID", typeof(int), this.server_id));
                //SQL Settings
                lstReturn.Add(new CPluginVariable("2. MySQL Settings|MySQL Hostname", typeof(string), mySqlHostname));
                lstReturn.Add(new CPluginVariable("2. MySQL Settings|MySQL Port", typeof(string), mySqlPort));
                lstReturn.Add(new CPluginVariable("2. MySQL Settings|MySQL Database", typeof(string), mySqlDatabaseName));
                lstReturn.Add(new CPluginVariable("2. MySQL Settings|MySQL Username", typeof(string), mySqlUsername));
                lstReturn.Add(new CPluginVariable("2. MySQL Settings|MySQL Password", typeof(string), mySqlPassword));
                //Debugging Settings
                lstReturn.Add(new CPluginVariable("3. Debugging|Debug level", typeof(int), this.debugLevel));
            }
            else
            {
                lstReturn = this.GetPluginVariables();

                //Add display variables
                //Admin Settings
                lstReturn.Add(new CPluginVariable("3. Player Access Settings|Add Access", typeof(string), ""));
                lstReturn.Add(new CPluginVariable("3. Player Access Settings|Remove Access", typeof(string), ""));
                if (this.playerAccessCache.Count > 0)
                {
                    foreach (string playerName in this.playerAccessCache.Keys)
                    {
                        lstReturn.Add(new CPluginVariable("3. Player Access Settings|" + playerName, typeof(string), this.playerAccessCache[playerName] + ""));
                    }
                }
                else
                {
                    lstReturn.Add(new CPluginVariable("3. Player Access Settings|No Players in Access List", typeof(string), "Add Players with 'Add Access', or Re-Enable AdKats to fetch."));
                }
            }
            return lstReturn;
        }

        public List<CPluginVariable> GetPluginVariables()
        {
            List<CPluginVariable> lstReturn = new List<CPluginVariable>();

            try
            {
                //Server Settings
                lstReturn.Add(new CPluginVariable("1. Server Settings|Server ID", typeof(int), this.server_id));
                CServerInfo info = this.getServerInfo();
                if (info != null)
                {
                    lstReturn.Add(new CPluginVariable("1. Server Settings|Server IP", typeof(string), info.ExternalGameIpandPort));
                }

                //SQL Settings
                lstReturn.Add(new CPluginVariable("2. MySQL Settings|MySQL Hostname", typeof(string), mySqlHostname));
                lstReturn.Add(new CPluginVariable("2. MySQL Settings|MySQL Port", typeof(string), mySqlPort));
                lstReturn.Add(new CPluginVariable("2. MySQL Settings|MySQL Database", typeof(string), mySqlDatabaseName));
                lstReturn.Add(new CPluginVariable("2. MySQL Settings|MySQL Username", typeof(string), mySqlUsername));
                lstReturn.Add(new CPluginVariable("2. MySQL Settings|MySQL Password", typeof(string), mySqlPassword));

                //In-Game Command Settings
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Minimum Required Reason Length", typeof(int), this.requiredReasonLength));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Confirm Command", typeof(string), m_strConfirmCommand));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Cancel Command", typeof(string), m_strCancelCommand));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Kill Player", typeof(string), m_strKillCommand));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Kick Player", typeof(string), m_strKickCommand));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Temp-Ban Player", typeof(string), m_strTemporaryBanCommand));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Permaban Player", typeof(string), m_strPermanentBanCommand));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Punish Player", typeof(string), m_strPunishCommand));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Forgive Player", typeof(string), m_strForgiveCommand));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Mute Player", typeof(string), m_strMuteCommand));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Round Whitelist Player", typeof(string), m_strRoundWhitelistCommand));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|OnDeath Move Player", typeof(string), m_strMoveCommand));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Force Move Player", typeof(string), m_strForceMoveCommand));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Teamswap Self", typeof(string), m_strTeamswapCommand));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Report Player", typeof(string), m_strReportCommand));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Call Admin on Player", typeof(string), m_strCallAdminCommand));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Admin Say", typeof(string), m_strSayCommand));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Player Say", typeof(string), m_strPlayerSayCommand));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Admin Yell", typeof(string), m_strYellCommand));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Player Yell", typeof(string), m_strPlayerYellCommand));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|What Is", typeof(string), m_strWhatIsCommand));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Restart Level", typeof(string), m_strRestartLevelCommand));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Next Level", typeof(string), m_strNextLevelCommand));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|End Level", typeof(string), m_strEndLevelCommand));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Nuke Server", typeof(string), m_strNukeCommand));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Kick All NonAdmins", typeof(string), m_strKickAllCommand));

                //Punishment Settings
                lstReturn.Add(new CPluginVariable("5. Punishment Settings|Punishment Hierarchy", typeof(string[]), this.punishmentHierarchy));
                lstReturn.Add(new CPluginVariable("5. Punishment Settings|Combine Server Punishments", typeof(Boolean), this.combineServerPunishments));
                lstReturn.Add(new CPluginVariable("5. Punishment Settings|Only Kill Players when Server in low population", typeof(Boolean), this.onlyKillOnLowPop));
                if (this.onlyKillOnLowPop)
                {
                    lstReturn.Add(new CPluginVariable("5. Punishment Settings|Low Population Value", typeof(int), this.lowPopPlayerCount));
                    lstReturn.Add(new CPluginVariable("5. Punishment Settings|IRO Punishment Overrides Low Pop", typeof(Boolean), this.IROOverridesLowPop));
                }

                //Player Report Settings
                lstReturn.Add(new CPluginVariable("6. Email Settings|Send Emails", typeof(string), "Disabled Until Implemented"));
                if (this.sendmail == true)
                {
                    lstReturn.Add(new CPluginVariable("6. Email Settings|Email: Use SSL?", typeof(Boolean), this.blUseSSL));
                    lstReturn.Add(new CPluginVariable("6. Email Settings|SMTP-Server address", typeof(string), this.strSMTPServer));
                    lstReturn.Add(new CPluginVariable("6. Email Settings|SMTP-Server port", typeof(int), this.iSMTPPort));
                    lstReturn.Add(new CPluginVariable("6. Email Settings|Sender address", typeof(string), this.strSenderMail));
                    lstReturn.Add(new CPluginVariable("6. Email Settings|Receiver addresses", typeof(string[]), this.lstReceiverMail.ToArray()));
                    lstReturn.Add(new CPluginVariable("6. Email Settings|SMTP-Server username", typeof(string), this.strSMTPUser));
                    lstReturn.Add(new CPluginVariable("6. Email Settings|SMTP-Server password", typeof(string), this.strSMTPPassword));
                }

                //TeamSwap Settings
                lstReturn.Add(new CPluginVariable("7. TeamSwap Settings|Require Whitelist for Access", typeof(Boolean), this.requireTeamswapWhitelist));
                if (this.requireTeamswapWhitelist)
                {
                    lstReturn.Add(new CPluginVariable("7. TeamSwap Settings|Auto-Whitelist Count", typeof(string), this.playersToAutoWhitelist));
                }
                lstReturn.Add(new CPluginVariable("7. TeamSwap Settings|Ticket Window High", typeof(int), this.teamSwapTicketWindowHigh));
                lstReturn.Add(new CPluginVariable("7. TeamSwap Settings|Ticket Window Low", typeof(int), this.teamSwapTicketWindowLow));

                //Admin Assistant Settings
                lstReturn.Add(new CPluginVariable("8. Admin Assistant Settings|Enable Admin Assistant Perk", typeof(Boolean), this.enableAdminAssistants));
                lstReturn.Add(new CPluginVariable("8. Admin Assistant Settings|Minimum Confirmed Reports Per Week", typeof(int), this.minimumRequiredWeeklyReports));

                //Muting Settings
                lstReturn.Add(new CPluginVariable("9. Player Mute Settings|On-Player-Muted Message", typeof(string), this.mutedPlayerMuteMessage));
                lstReturn.Add(new CPluginVariable("9. Player Mute Settings|On-Player-Killed Message", typeof(string), this.mutedPlayerKillMessage));
                lstReturn.Add(new CPluginVariable("9. Player Mute Settings|On-Player-Kicked Message", typeof(string), this.mutedPlayerKickMessage));
                lstReturn.Add(new CPluginVariable("9. Player Mute Settings|# Chances to give player before kicking", typeof(int), this.mutedPlayerChances));

                //Pre-Message Settings
                lstReturn.Add(new CPluginVariable("A10. Messaging Settings|Yell display time seconds", typeof(int), this.m_iShowMessageLength));
                lstReturn.Add(new CPluginVariable("A10. Messaging Settings|Pre-Message List", typeof(string[]), this.preMessageList.ToArray()));
                lstReturn.Add(new CPluginVariable("A10. Messaging Settings|Require Use of Pre-Messages", typeof(Boolean), this.requirePreMessageUse));

                //Ban Settings
                lstReturn.Add(new CPluginVariable("A11. Banning Settings|Ban Type", "enum.AdKats_BanType(Frostbite - Name|Frostbite - EA GUID|Punkbuster - GUID)", this.m_strBanTypeOption));
                lstReturn.Add(new CPluginVariable("A11. Banning Settings|Use Additional Ban Message", typeof(Boolean), this.useBanAppend));
                if (this.useBanAppend)
                {
                    lstReturn.Add(new CPluginVariable("A11. Banning Settings|Additional Ban Message", typeof(string), this.banAppend));
                }

                //External Command Settings
                lstReturn.Add(new CPluginVariable("A12. External Command Settings|HTTP External Access Key", typeof(string), this.externalCommandAccessKey));
                lstReturn.Add(new CPluginVariable("A12. External Command Settings|Fetch Actions from Database", typeof(Boolean), this.fetchActionsFromDB));

                //Debug settings
                lstReturn.Add(new CPluginVariable("A13. Debugging|Debug level", typeof(int), this.debugLevel));
                lstReturn.Add(new CPluginVariable("A13. Debugging|Debug Soldier Name", typeof(string), this.debugSoldierName));
                lstReturn.Add(new CPluginVariable("A13. Debugging|Command Entry", typeof(string), ""));
            }
            catch (Exception e)
            {
                this.ConsoleException("get vars: " + e.ToString());
            }
            return lstReturn;
        }

        public void SetPluginVariable(string strVariable, string strValue)
        {
            if (strVariable.Equals("UpdateSettings"))
            {
                //Do nothing. Settings page will be updated after return.
            }
            #region debugging
            else if (Regex.Match(strVariable, @"Command Entry").Success)
            {
                //Check if the message is a command
                if (strValue.StartsWith("@") || strValue.StartsWith("!"))
                {
                    strValue = strValue.Substring(1);
                }
                else if (strValue.StartsWith("/@") || strValue.StartsWith("/!"))
                {
                    strValue = strValue.Substring(2);
                }
                else if (strValue.StartsWith("/"))
                {
                    strValue = strValue.Substring(1);
                }
                else
                {
                    //If the message does not cause either of the above clauses, then ignore it.
                    return;
                }
                AdKat_Record recordItem = new AdKat_Record();
                recordItem.command_source = AdKat_CommandSource.Settings;
                recordItem.source_name = "SettingsAdmin";
                this.completeRecord(recordItem, strValue);
            }
            else if (Regex.Match(strVariable, @"Debug level").Success)
            {
                int tmp = 2;
                int.TryParse(strValue, out tmp);
                this.debugLevel = tmp;
            }
            else if (Regex.Match(strVariable, @"Debug Soldier Name").Success)
            {
                this.debugSoldierName = strValue;
            }
            #endregion
            #region HTTP settings
            else if (Regex.Match(strVariable, @"External Access Key").Success)
            {
                this.externalCommandAccessKey = strValue;
            }
            else if (Regex.Match(strVariable, @"Fetch Actions from Database").Success)
            {
                if (fetchActionsFromDB = Boolean.Parse(strValue))
                {
                    this.dbCommHandle.Set();
                }
            }
            #endregion
            #region server settings
            if (Regex.Match(strVariable, @"Server ID").Success)
            {
                int tmp = -1;
                int.TryParse(strValue, out tmp);
                this.server_id = tmp;
            }
            #endregion
            #region ban settings
            else if (Regex.Match(strVariable, @"Ban Type").Success)
            {
                this.m_strBanTypeOption = strValue;

                if (String.Compare("Frostbite - Name", this.m_strBanTypeOption, true) == 0)
                {
                    this.m_banMethod = AdKat_BanType.FrostbiteName;
                }
                else if (String.Compare("Frostbite - EA GUID", this.m_strBanTypeOption, true) == 0)
                {
                    this.m_banMethod = AdKat_BanType.FrostbiteEaGuid;
                }
                else if (String.Compare("Punkbuster - GUID", this.m_strBanTypeOption, true) == 0)
                {
                    this.m_banMethod = AdKat_BanType.PunkbusterGuid;
                }
            }
            else if (Regex.Match(strVariable, @"Use Additional Ban Message").Success)
            {
                this.useBanAppend = Boolean.Parse(strValue);
            }
            else if (Regex.Match(strVariable, @"Additional Ban Message").Success)
            {
                if (strValue.Length > 30)
                {
                    strValue = strValue.Substring(0, 30);
                    this.ConsoleError("Ban append cannot be more than 30 characters.");
                }
                this.banAppend = strValue;
            }
            #endregion
            #region In-Game Command Settings
            else if (Regex.Match(strVariable, @"Minimum Required Reason Length").Success)
            {
                this.requiredReasonLength = Int32.Parse(strValue);
            }
            else if (Regex.Match(strVariable, @"Confirm Command").Success)
            {
                if (strValue.Length > 0)
                {
                    //Confirm cannot be logged
                    if (strValue.ToLower().EndsWith("|log"))
                    {
                        strValue = strValue.TrimEnd("|log".ToCharArray());
                        this.ConsoleWrite("Cannot log Confirm Command");
                    }
                    this.m_strConfirmCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strConfirmCommand = AdKat_CommandType.ConfirmCommand + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"Cancel Command").Success)
            {
                if (strValue.Length > 0)
                {
                    //Cancel cannot be logged
                    if (strValue.ToLower().EndsWith("|log"))
                    {
                        strValue = strValue.TrimEnd("|log".ToCharArray());
                        this.ConsoleWrite("Cannot log Cancel Command");
                    }
                    this.m_strCancelCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strCancelCommand = AdKat_CommandType.CancelCommand + " COMMAND BLANK";
                }
            }
            else if (strVariable.EndsWith(@"Kill Player"))
            {
                if (strValue.Length > 0)
                {
                    this.m_strKillCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strKillCommand = AdKat_CommandType.KillPlayer + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"Kick Player").Success)
            {
                if (strValue.Length > 0)
                {
                    this.m_strKickCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strKickCommand = AdKat_CommandType.KickPlayer + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"Temp-Ban Player").Success)
            {
                if (strValue.Length > 0)
                {
                    this.m_strTemporaryBanCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strTemporaryBanCommand = AdKat_CommandType.TempBanPlayer + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"Permaban Player").Success)
            {
                if (strValue.Length > 0)
                {
                    this.m_strPermanentBanCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strPermanentBanCommand = AdKat_CommandType.PermabanPlayer + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"Punish Player").Success)
            {
                if (strValue.Length > 0)
                {
                    //Punish logging is required for functionality
                    if (!strValue.ToLower().EndsWith("|log"))
                    {
                        strValue += "|log";
                    }
                    this.m_strPunishCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strPunishCommand = AdKat_CommandType.PunishPlayer + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"Forgive Player").Success)
            {
                if (strValue.Length > 0)
                {
                    //Forgive logging is required for functionality
                    if (!strValue.ToLower().EndsWith("|log"))
                    {
                        strValue += "|log";
                    }
                    this.m_strForgiveCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strForgiveCommand = AdKat_CommandType.ForgivePlayer + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"Mute Player").Success)
            {
                if (strValue.Length > 0)
                {
                    this.m_strMuteCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strMuteCommand = AdKat_CommandType.MutePlayer + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"Round Whitelist Player").Success)
            {
                if (strValue.Length > 0)
                {
                    this.m_strRoundWhitelistCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strRoundWhitelistCommand = AdKat_CommandType.RoundWhitelistPlayer + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"OnDeath Move Player").Success)
            {
                if (strValue.Length > 0)
                {
                    this.m_strMoveCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strMoveCommand = AdKat_CommandType.MovePlayer + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"Force Move Player").Success)
            {
                if (strValue.Length > 0)
                {
                    this.m_strForceMoveCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strForceMoveCommand = AdKat_CommandType.ForceMovePlayer + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"Teamswap Self").Success)
            {
                if (strValue.Length > 0)
                {
                    this.m_strTeamswapCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strTeamswapCommand = AdKat_CommandType.Teamswap + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"Report Player").Success)
            {
                if (strValue.Length > 0)
                {
                    this.m_strReportCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strReportCommand = AdKat_CommandType.ReportPlayer + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"Call Admin on Player").Success)
            {
                if (strValue.Length > 0)
                {
                    this.m_strCallAdminCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strCallAdminCommand = AdKat_CommandType.CallAdmin + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"Admin Say").Success)
            {
                if (strValue.Length > 0)
                {
                    this.m_strSayCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strSayCommand = AdKat_CommandType.AdminSay + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"Player Say").Success)
            {
                if (strValue.Length > 0)
                {
                    this.m_strPlayerSayCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strPlayerSayCommand = AdKat_CommandType.PlayerSay + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"Admin Yell").Success)
            {
                if (strValue.Length > 0)
                {
                    this.m_strYellCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strYellCommand = AdKat_CommandType.AdminYell + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"Player Yell").Success)
            {
                if (strValue.Length > 0)
                {
                    this.m_strPlayerYellCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strPlayerYellCommand = AdKat_CommandType.PlayerYell + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"What Is").Success)
            {
                if (strValue.Length > 0)
                {
                    //Confirm cannot be logged
                    if (strValue.ToLower().EndsWith("|log"))
                    {
                        strValue = strValue.TrimEnd("|log".ToCharArray());
                        this.ConsoleWrite("Cannot log WhatIs Command");
                    }
                    this.m_strWhatIsCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strWhatIsCommand = AdKat_CommandType.WhatIs + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"Restart Level").Success)
            {
                if (strValue.Length > 0)
                {
                    this.m_strRestartLevelCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strRestartLevelCommand = AdKat_CommandType.RestartLevel + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"Next Level").Success)
            {
                if (strValue.Length > 0)
                {
                    this.m_strNextLevelCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strNextLevelCommand = AdKat_CommandType.NextLevel + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"End Level").Success)
            {
                if (strValue.Length > 0)
                {
                    this.m_strEndLevelCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strEndLevelCommand = AdKat_CommandType.EndLevel + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"Nuke Server").Success)
            {
                if (strValue.Length > 0)
                {
                    this.m_strNukeCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strNukeCommand = AdKat_CommandType.NukeServer + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"Kick All NonAdmins").Success)
            {
                if (strValue.Length > 0)
                {
                    this.m_strKickAllCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strKickAllCommand = AdKat_CommandType.KickAll + " COMMAND BLANK";
                }
            }
            #endregion
            #region punishment settings
            else if (Regex.Match(strVariable, @"Punishment Hierarchy").Success)
            {
                this.punishmentHierarchy = CPluginVariable.DecodeStringArray(strValue);
            }
            else if (Regex.Match(strVariable, @"Combine Server Punishments").Success)
            {
                this.combineServerPunishments = Boolean.Parse(strValue);
            }
            else if (Regex.Match(strVariable, @"Only Kill Players when Server in low population").Success)
            {
                this.onlyKillOnLowPop = Boolean.Parse(strValue);
            }
            else if (Regex.Match(strVariable, @"Low Population Value").Success)
            {
                this.lowPopPlayerCount = Int32.Parse(strValue);
            }
            else if (Regex.Match(strVariable, @"IRO Punishment Overrides Low Pop").Success)
            {
                this.IROOverridesLowPop = Boolean.Parse(strValue);
            }
            #endregion
            #region access settings
            else if (Regex.Match(strVariable, @"Add Access").Success)
            {
                if (this.isEnabled)
                {
                    this.queuePlayerForAccessUpdate(strValue, 6);
                }
                else
                {
                    this.ConsoleError("Enable AdKats before changing admins.");
                }
            }
            else if (Regex.Match(strVariable, @"Remove Access").Success)
            {
                if (this.isEnabled)
                {
                    this.queuePlayerForAccessRemoval(strValue);
                }
                else
                {
                    this.ConsoleError("Enable AdKats before changing admins.");
                }
            }
            else if (this.playerAccessCache.ContainsKey(strVariable))
            {
                if (this.isEnabled)
                {
                    this.queuePlayerForAccessUpdate(strVariable, Int32.Parse(strValue));
                }
                else
                {
                    this.ConsoleError("Enable AdKats before changing admins.");
                }
            }
            #endregion
            #region sql settings
            else if (Regex.Match(strVariable, @"MySQL Hostname").Success)
            {
                mySqlHostname = strValue;
                this.dbSettingsChanged = true;
                this.dbCommHandle.Set();
            }
            else if (Regex.Match(strVariable, @"MySQL Port").Success)
            {
                int tmp = 3306;
                int.TryParse(strValue, out tmp);
                if (tmp > 0 && tmp < 65536)
                {
                    mySqlPort = strValue;
                }
                else
                {
                    ConsoleException("Invalid value for MySQL Port: '" + strValue + "'. Must be number between 1 and 65535!");
                }
                this.dbSettingsChanged = true;
                this.dbCommHandle.Set();
            }
            else if (Regex.Match(strVariable, @"MySQL Database").Success)
            {
                this.mySqlDatabaseName = strValue;
                this.dbSettingsChanged = true;
                this.dbCommHandle.Set();
            }
            else if (Regex.Match(strVariable, @"MySQL Username").Success)
            {
                mySqlUsername = strValue;
                this.dbSettingsChanged = true;
                this.dbCommHandle.Set();
            }
            else if (Regex.Match(strVariable, @"MySQL Password").Success)
            {
                mySqlPassword = strValue;
                this.dbSettingsChanged = true;
                this.dbCommHandle.Set();
            }
            #endregion
            #region email settings
            else if (strVariable.CompareTo("Send Emails") == 0)
            {
                this.sendmail = Boolean.Parse(strValue);
            }
            else if (strVariable.CompareTo("Admin Request Email?") == 0)
            {
                //this.blNotifyEmail = Boolean.Parse(strValue);
            }
            else if (strVariable.CompareTo("Use SSL?") == 0)
            {
                this.blUseSSL = Boolean.Parse(strValue);
            }
            else if (strVariable.CompareTo("SMTP-Server address") == 0)
            {
                this.strSMTPServer = strValue;
            }
            else if (strVariable.CompareTo("SMTP-Server port") == 0)
            {
                int iPort = Int32.Parse(strValue);
                if (iPort > 0)
                {
                    this.iSMTPPort = iPort;
                }
            }
            else if (strVariable.CompareTo("Sender address") == 0)
            {
                if (strValue == null || strValue == String.Empty)
                {
                    this.strSenderMail = "SENDER_CANNOT_BE_EMPTY";
                    this.ConsoleError("No sender for email was given! Canceling Operation.");
                }
                else
                {
                    this.strSenderMail = strValue;
                }
            }
            else if (strVariable.CompareTo("Receiver addresses") == 0)
            {
                List<String> addresses = new List<string>(CPluginVariable.DecodeStringArray(strValue));
                if (addresses.Count > 0)
                {
                    foreach (string mailto in addresses)
                    {
                        if (!Regex.IsMatch(mailto, @"^([\w-\.]+)@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([\w-]+\.)+))([a-zA-Z]{2,4}|[0-9]{1,3})(\]?)$"))
                        {
                            this.ConsoleError("Error in receiver email address: " + mailto);
                        }
                    }
                    this.lstReceiverMail = addresses;
                }
                else
                {
                    this.ConsoleError("No receiver email addresses were given!");
                    this.lstReceiverMail = new List<string>();
                    this.lstReceiverMail.Add("test@test.net");
                }
            }
            else if (strVariable.CompareTo("SMTP-Server username") == 0)
            {
                if (strValue == null || strValue == String.Empty)
                {
                    this.strSMTPUser = "SMTP_USERNAME_CANNOT_BE_EMPTY";
                    this.ConsoleError("No username for SMTP was given! Canceling Operation.");
                }
                else
                {
                    this.strSMTPUser = strValue;
                }
            }
            else if (strVariable.CompareTo("SMTP-Server password") == 0)
            {
                if (strValue == null || strValue == String.Empty)
                {
                    this.strSMTPPassword = "SMTP_PASSWORD_CANNOT_BE_EMPTY";
                    this.ConsoleError("No password for SMTP was given! Canceling Operation.");
                }
                else
                {
                    this.strSMTPPassword = strValue;
                }
            }
            #endregion
            #region mute settings
            else if (Regex.Match(strVariable, @"On-Player-Muted Message").Success)
            {
                this.mutedPlayerMuteMessage = strValue;
            }
            else if (Regex.Match(strVariable, @"On-Player-Killed Message").Success)
            {
                this.mutedPlayerKillMessage = strValue;
            }
            else if (Regex.Match(strVariable, @"On-Player-Kicked Message").Success)
            {
                this.mutedPlayerKickMessage = strValue;
            }
            if (Regex.Match(strVariable, @"# Chances to give player before kicking").Success)
            {
                int tmp = 5;
                int.TryParse(strValue, out tmp);
                this.mutedPlayerChances = tmp;
            }
            #endregion
            #region teamswap settings
            else if (Regex.Match(strVariable, @"Require Whitelist for Access").Success)
            {
                this.requireTeamswapWhitelist = Boolean.Parse(strValue);
            }
            else if (Regex.Match(strVariable, @"Auto-Whitelist Count").Success)
            {
                int tmp = 1;
                int.TryParse(strValue, out tmp);
                if (tmp < 1)
                    tmp = 1;
                this.playersToAutoWhitelist = tmp;
            }
            else if (Regex.Match(strVariable, @"Ticket Window High").Success)
            {
                int tmp = 2;
                int.TryParse(strValue, out tmp);
                this.teamSwapTicketWindowHigh = tmp;
            }
            else if (Regex.Match(strVariable, @"Ticket Window Low").Success)
            {
                int tmp = 2;
                int.TryParse(strValue, out tmp);
                this.teamSwapTicketWindowLow = tmp;
            }
            #endregion
            #region Admin Assistants
            else if (Regex.Match(strVariable, @"Enable Admin Assistant Perk").Success)
            {
                this.enableAdminAssistants = Boolean.Parse(strValue);
            }
            else if (Regex.Match(strVariable, @"Minimum Confirmed Reports Per Week").Success)
            {
                this.minimumRequiredWeeklyReports = Int32.Parse(strValue);
            }
            #endregion
            #region Messaging Settings
            else if (Regex.Match(strVariable, @"Yell display time seconds").Success)
            {
                this.m_iShowMessageLength = Int32.Parse(strValue);
                this.m_strShowMessageLength = m_iShowMessageLength + "";
            }
            else if (Regex.Match(strVariable, @"Pre-Message List").Success)
            {
                this.preMessageList = new List<string>(CPluginVariable.DecodeStringArray(strValue));
            }
            else if (Regex.Match(strVariable, @"Require Use of Pre-Messages").Success)
            {
                this.requirePreMessageUse = Boolean.Parse(strValue);
            }
            #endregion
        }

        private void rebindAllCommands()
        {
            this.DebugWrite("Entering rebindAllCommands", 6);

            Dictionary<String, AdKat_CommandType> tempDictionary = new Dictionary<String, AdKat_CommandType>();

            //Update confirm and cancel 
            this.m_strConfirmCommand = this.parseAddCommand(tempDictionary, this.m_strConfirmCommand, AdKat_CommandType.ConfirmCommand);
            this.m_strCancelCommand = this.parseAddCommand(tempDictionary, this.m_strCancelCommand, AdKat_CommandType.CancelCommand);

            //Update player interaction
            this.m_strKillCommand = this.parseAddCommand(tempDictionary, this.m_strKillCommand, AdKat_CommandType.KillPlayer);
            this.m_strKickCommand = this.parseAddCommand(tempDictionary, this.m_strKickCommand, AdKat_CommandType.KickPlayer);
            this.m_strTemporaryBanCommand = this.parseAddCommand(tempDictionary, this.m_strTemporaryBanCommand, AdKat_CommandType.TempBanPlayer);
            this.m_strPermanentBanCommand = this.parseAddCommand(tempDictionary, this.m_strPermanentBanCommand, AdKat_CommandType.PermabanPlayer);
            this.m_strPunishCommand = this.parseAddCommand(tempDictionary, this.m_strPunishCommand, AdKat_CommandType.PunishPlayer);
            this.m_strForgiveCommand = this.parseAddCommand(tempDictionary, this.m_strForgiveCommand, AdKat_CommandType.ForgivePlayer);
            this.m_strMuteCommand = this.parseAddCommand(tempDictionary, this.m_strMuteCommand, AdKat_CommandType.MutePlayer);
            this.m_strRoundWhitelistCommand = this.parseAddCommand(tempDictionary, this.m_strRoundWhitelistCommand, AdKat_CommandType.RoundWhitelistPlayer);
            this.m_strMoveCommand = this.parseAddCommand(tempDictionary, this.m_strMoveCommand, AdKat_CommandType.MovePlayer);
            this.m_strForceMoveCommand = this.parseAddCommand(tempDictionary, this.m_strForceMoveCommand, AdKat_CommandType.ForceMovePlayer);
            this.m_strTeamswapCommand = this.parseAddCommand(tempDictionary, this.m_strTeamswapCommand, AdKat_CommandType.Teamswap);
            this.m_strReportCommand = this.parseAddCommand(tempDictionary, this.m_strReportCommand, AdKat_CommandType.ReportPlayer);
            this.m_strCallAdminCommand = this.parseAddCommand(tempDictionary, this.m_strCallAdminCommand, AdKat_CommandType.CallAdmin);
            this.m_strNukeCommand = this.parseAddCommand(tempDictionary, this.m_strNukeCommand, AdKat_CommandType.NukeServer);
            this.m_strKickAllCommand = this.parseAddCommand(tempDictionary, this.m_strKickAllCommand, AdKat_CommandType.KickAll);

            //Update Messaging
            this.m_strSayCommand = this.parseAddCommand(tempDictionary, this.m_strSayCommand, AdKat_CommandType.AdminSay);
            this.m_strPlayerSayCommand = this.parseAddCommand(tempDictionary, this.m_strPlayerSayCommand, AdKat_CommandType.PlayerSay);
            this.m_strYellCommand = this.parseAddCommand(tempDictionary, this.m_strYellCommand, AdKat_CommandType.AdminYell);
            this.m_strPlayerYellCommand = this.parseAddCommand(tempDictionary, this.m_strPlayerYellCommand, AdKat_CommandType.PlayerYell);
            this.m_strWhatIsCommand = this.parseAddCommand(tempDictionary, this.m_strWhatIsCommand, AdKat_CommandType.WhatIs);

            //Update level controls
            this.m_strRestartLevelCommand = this.parseAddCommand(tempDictionary, this.m_strRestartLevelCommand, AdKat_CommandType.RestartLevel);
            this.m_strNextLevelCommand = this.parseAddCommand(tempDictionary, this.m_strNextLevelCommand, AdKat_CommandType.NextLevel);
            this.m_strEndLevelCommand = this.parseAddCommand(tempDictionary, this.m_strEndLevelCommand, AdKat_CommandType.EndLevel);

            //Overwrite command string dictionary with the new one
            this.AdKat_CommandStrings = tempDictionary;

            this.DebugWrite("rebindAllCommands finished!", 6);
        }

        private String parseAddCommand(Dictionary<String, AdKat_CommandType> tempDictionary, String strCommand, AdKat_CommandType enumCommand)
        {
            try
            {
                this.DebugWrite("Entering parseAddCommand. Command: " + strCommand, 7);
                //Command can be in two sections, split input string
                String[] split = strCommand.ToLower().Split('|');
                this.DebugWrite("Split command", 7);
                //Attempt to add command to dictionary
                tempDictionary.Add(split[0], enumCommand);
                this.DebugWrite("added command", 7);

                //Check for additional input
                if (split.Length > 1)
                {
                    //There is additional input, check if it's valid
                    //Right now only accepting 'log' as an additional input
                    if (split[1] == "log")
                    {
                        this.setLoggingForCommand(enumCommand, true);
                    }
                    else
                    {
                        this.ConsoleError("Invalid command format for: " + enumCommand);
                        return enumCommand + " INVALID FORMAT";
                    }
                }
                //Set logging to false for this command
                else
                {
                    this.setLoggingForCommand(enumCommand, false);
                }
                this.DebugWrite("parseAddCommand Finished!", 7);
                return strCommand;
            }
            catch (ArgumentException e)
            {
                //The command attempting to add was the same name as another command currently in the dictionary, inform the user.
                this.ConsoleError("Duplicate Command detected for " + enumCommand + ". That command will not work.");
                return enumCommand + " DUPLICATE COMMAND";
            }
            catch (Exception e)
            {
                this.ConsoleError("Unknown error for  " + enumCommand + ". Message: " + e.Message + ". Contact ColColonCleaner.");
                return enumCommand + " UNKNOWN ERROR";
            }
        }

        private void setLoggingForCommand(AdKat_CommandType enumCommand, Boolean newLoggingEnabled)
        {
            try
            {
                //Get current value
                bool currentLoggingEnabled = this.AdKat_LoggingSettings[enumCommand];
                this.DebugWrite("set logging for " + enumCommand + " to " + newLoggingEnabled + " from " + currentLoggingEnabled, 7);
                //Only perform replacement if the current value is different than what we want
                if (currentLoggingEnabled != newLoggingEnabled)
                {
                    this.DebugWrite("Changing logging option for " + enumCommand + " to " + newLoggingEnabled, 2);
                    this.AdKat_LoggingSettings[enumCommand] = newLoggingEnabled;
                }
                else
                {
                    this.DebugWrite("Logging option for " + enumCommand + " still " + currentLoggingEnabled + ".", 3);
                }
            }
            catch (Exception e)
            {
                this.DebugWrite("Current value null?: " + e.Message, 6);
                this.DebugWrite("Setting initial logging option for " + enumCommand + " to " + newLoggingEnabled, 6);
                AdKat_LoggingSettings.Add(enumCommand, newLoggingEnabled);
                this.DebugWrite("Logging option set successfuly", 6);
            }
        }
        #endregion

        #region Threading

        public void InitWaitHandles()
        {
            this.teamswapHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            this.listPlayersHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            this.messageParsingHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            this.commandParsingHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            this.dbCommHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            this.actionHandlingHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        }

        public void setAllHandles()
        {
            this.teamswapHandle.Set();
            this.listPlayersHandle.Set();
            this.messageParsingHandle.Set();
            this.commandParsingHandle.Set();
            this.dbCommHandle.Set();
            this.actionHandlingHandle.Set();
        }

        public void InitThreads()
        {
            try
            {
                this.MessagingThread = new Thread(new ThreadStart(messagingThreadLoop));
                this.MessagingThread.IsBackground = true;

                this.CommandParsingThread = new Thread(new ThreadStart(commandParsingThreadLoop));
                this.CommandParsingThread.IsBackground = true;

                this.DatabaseCommThread = new Thread(new ThreadStart(databaseCommThreadLoop));
                this.DatabaseCommThread.IsBackground = true;

                this.ActionHandlingThread = new Thread(new ThreadStart(actionHandlingThreadLoop));
                this.ActionHandlingThread.IsBackground = true;

                this.TeamSwapThread = new Thread(new ThreadStart(teamswapThreadLoop));
                this.TeamSwapThread.IsBackground = true;
            }
            catch (Exception e)
            {
                this.ConsoleException(e.ToString());
            }
        }

        public void StartThreads()
        {
            this.MessagingThread.Start();
            this.CommandParsingThread.Start();
            this.DatabaseCommThread.Start();
            this.ActionHandlingThread.Start();
            this.TeamSwapThread.Start();
        }

        public Boolean allThreadsReady()
        {
            Boolean ready = true;
            if (this.teamswapHandle.WaitOne(0))
            {
                this.DebugWrite("teamswap not ready.", 6);
                ready = false;
            }
            if (this.messageParsingHandle.WaitOne(0))
            {
                this.DebugWrite("messaging not ready.", 6);
                ready = false;
            }
            if (this.commandParsingHandle.WaitOne(0))
            {
                this.DebugWrite("command parsing not ready.", 6);
                ready = false;
            }
            if (this.dbCommHandle.WaitOne(0))
            {
                this.DebugWrite("db comm not ready.", 6);
                ready = false;
            }
            if (this.actionHandlingHandle.WaitOne(0))
            {
                this.DebugWrite("action handling not ready.", 6);
                ready = false;
            }
            return ready;
        }

        #endregion

        #region Procon Events

        public void OnPluginLoaded(string strHostName, string strPort, string strPRoConVersion)
        {
            this.RegisterEvents(this.GetType().Name,
                "OnVersion",
                "OnServerInfo",
                "OnListPlayers",
                "OnPunkbusterPlayerInfo",
                "OnPlayerKilled",
                "OnPlayerSpawned",
                "OnPlayerTeamChange",
                "OnPlayerJoin",
                "OnPlayerLeft",
                "OnGlobalChat",
                "OnTeamChat",
                "OnSquadChat",
                "OnLevelLoaded",
                "OnBanAdded",
                "OnBanRemoved",
                //"OnBanListClear", 
                //"OnBanListSave", 
                //"OnBanListLoad", 
                "OnBanList");
        }

        public void OnPluginEnable()
        {
            if (this.finalizer != null && this.finalizer.IsAlive)
            {
                ConsoleError("Cannot enable plugin while it is shutting down. Please Wait.");
                return;
            }
            /*if (!this.connectionCapable())
            {
                //Inform the user
                this.ConsoleError("Cannot enable AdKats without database variables entered.");
                //Disable the plugin
                this.ExecuteCommand("procon.protected.plugins.enable", "AdKats", "False");
                return;
            }*/
            try
            {
                this.activator = new Thread(new ThreadStart(delegate()
                {
                    try
                    {
                        ConsoleWrite("Enabling command functionality. Please Wait.");
                        //Set the enabled variable
                        this.isEnabled = true;

                        //Init and start all the threads
                        this.InitWaitHandles();
                        this.setAllHandles();
                        this.InitThreads();
                        this.StartThreads();

                        DateTime startTime = DateTime.Now;
                        TimeSpan duration = TimeSpan.MinValue;
                        while (!this.allThreadsReady())
                        {
                            Thread.Sleep(10);
                            duration = DateTime.Now.Subtract(startTime);
                            if (duration.TotalSeconds > 30)
                            {
                                //Inform the user
                                this.ConsoleError("Failed to enable in 30 seconds. Shutting down. Inform ColColonCleaner.");
                                //Disable the plugin
                                this.ExecuteCommand("procon.protected.plugins.enable", "AdKats", "False");
                                return;
                            }
                        }

                        this.threadsReady = true;
                        this.updateSettingPage();
                        this.ConsoleWrite("^b^2Enabled!^n^0 Version: " + this.GetPluginVersion() + " in " + duration.TotalMilliseconds + "ms.");
                    }
                    catch (Exception e)
                    {
                        ConsoleException(e.ToString());
                    }
                }));

                //Start the thread
                this.activator.Start();
            }
            catch (Exception e)
            {
                ConsoleException(e.ToString());
            }
        }

        public void OnPluginDisable()
        {
            if (this.finalizer != null && this.finalizer.IsAlive)
                return;
            try
            {
                this.finalizer = new Thread(new ThreadStart(delegate()
                {
                    try
                    {
                        ConsoleWrite("Disabling all functionality. Please Wait.");
                        this.isEnabled = false;

                        //Open all handles. Threads will finish on their own.
                        this.setAllHandles();

                        //Make sure all threads are finished.
                        //TODO try removing these and see if it works better
                        JoinWith(this.MessagingThread);
                        JoinWith(this.CommandParsingThread);
                        JoinWith(this.DatabaseCommThread);
                        JoinWith(this.ActionHandlingThread);
                        JoinWith(this.TeamSwapThread);

                        this.playerAccessRemovalQueue.Clear();
                        this.playerAccessUpdateQueue.Clear();
                        this.teamswapForceMoveQueue.Clear();
                        this.teamswapOnDeathCheckingQueue.Clear();
                        this.teamswapOnDeathMoveDic.Clear();
                        this.unparsedCommandQueue.Clear();
                        this.unparsedMessageQueue.Clear();
                        this.unprocessedActionQueue.Clear();
                        this.unprocessedRecordQueue.Clear();

                        this.threadsReady = false;
                        this.updateSettingPage();

                        ConsoleWrite("^b^1AdKats " + this.GetPluginVersion() + " Disabled! =(^n^0");
                    }
                    catch (Exception e)
                    {
                        ConsoleException(e.ToString());
                    }
                }));

                //Start the thread
                this.finalizer.Start();
            }
            catch (Exception e)
            {
                ConsoleException(e.ToString());
            }
        }

        public override void OnListPlayers(List<CPlayerInfo> players, CPlayerSubset subset)
        {
            if (isEnabled)
            {
                //Player list and ban list need to be locked for this operation
                lock (playersMutex)
                {
                    lock (this.banListMutex)
                    {
                        Dictionary<String, CPlayerInfo> playerDictionary = new Dictionary<String, CPlayerInfo>();
                        //Reset the player counts of both sides and recount everything
                        this.USPlayerCount = 0;
                        this.RUPlayerCount = 0;
                        foreach (CPlayerInfo player in players)
                        {
                            //Check with ban enforcer
                            AdKat_Ban ban = null;
                            if (this.AdKat_BanList_Name.TryGetValue(player.SoldierName, out ban) ||
                                this.AdKat_BanList_GUID.TryGetValue(player.GUID, out ban))
                            {
                                //Create the record
                                AdKat_Record record = new AdKat_Record();
                                record.source_name = "BanEnforcer";
                                record.target_name = player.SoldierName;
                                //Use the IP as guid for the record
                                record.target_guid = player.GUID;
                                record.command_source = AdKat_CommandSource.InGame;
                                record.command_type = AdKat_CommandType.KickPlayer;
                                record.record_message = ban.ban_reason;
                                //Queue record for handling
                                this.queueRecordForActionHandling(record);
                                //Don't add the player to player counts as they are about to be kicked
                                continue;
                            }

                            if (player.TeamID == USTeamId)
                            {
                                this.USPlayerCount++;
                            }
                            else
                            {
                                this.RUPlayerCount++;
                            }
                            playerDictionary.Add(player.SoldierName, player);
                        }
                        this.playerDictionary = playerDictionary;
                        this.playerList = players;
                    }
                }

                //Set the handle for teamswap
                this.listPlayersHandle.Set();
            }
        }

        public override void OnServerInfo(CServerInfo serverInfo)
        {
            if (this.isEnabled)
            {
                //Get the team scores
                this.setServerInfo(serverInfo);
                List<TeamScore> listCurrTeamScore = serverInfo.TeamScores;
                int iTeam0Score = listCurrTeamScore[0].Score;
                int iTeam1Score = listCurrTeamScore[1].Score;
                this.lowestTicketCount = (iTeam0Score < iTeam1Score) ? (iTeam0Score) : (iTeam1Score);
                this.highestTicketCount = (iTeam0Score > iTeam1Score) ? (iTeam0Score) : (iTeam1Score);
            }
        }

        public override void OnPunkbusterPlayerInfo(CPunkbusterInfo cPunkbusterInfo)
        {
            if (this.isEnabled)
            {
                lock (this.banListMutex)
                {
                    //PB info only used for IP Bans
                    AdKat_Ban ban = null;
                    if (this.AdKat_BanList_IP.TryGetValue(cPunkbusterInfo.Ip, out ban))
                    {
                        //Create the record
                        AdKat_Record record = new AdKat_Record();
                        record.source_name = "BanEnforcer";
                        record.target_name = cPunkbusterInfo.SoldierName;
                        //Use the IP as guid for the record
                        record.target_guid = cPunkbusterInfo.Ip;
                        record.command_source = AdKat_CommandSource.InGame;
                        record.command_type = AdKat_CommandType.KickPlayer;
                        record.record_message = ban.ban_reason;
                        //Queue record for handling
                        this.queueRecordForActionHandling(record);
                    }
                }
            }
        }

        public override void OnLevelLoaded(string strMapFileName, string strMapMode, int roundsPlayed, int roundsTotal)
        {
            if (this.isEnabled)
            {
                this.round_reports = new Dictionary<string, AdKat_Record>();
                this.round_mutedPlayers = new Dictionary<string, int>();
                this.teamswapRoundWhitelist = new Dictionary<string, Boolean>();
                this.autoWhitelistPlayers();

                //Reset whether they have been informed
                foreach (string assistantName in this.adminAssistantCache.Keys)
                {
                    this.adminAssistantCache[assistantName] = false;
                }
            }
        }

        //Move delayed players when they are killed
        public override void OnPlayerKilled(Kill kKillerVictimDetails)
        {
            //Used for delayed player moving
            if (isEnabled && this.teamswapOnDeathMoveDic.Count > 0)
            {
                lock (this.teamswapMutex)
                {
                    this.teamswapOnDeathCheckingQueue.Enqueue(kKillerVictimDetails.Victim);
                    this.teamswapHandle.Set();
                }
            }
        }

        public override void OnPlayerSpawned(String soldierName, Inventory spawnedInventory)
        {
            if (this.isEnabled)
            {
                Boolean informed = true;
                if (this.enableAdminAssistants && this.adminAssistantCache.TryGetValue(soldierName, out informed))
                {
                    if (informed == false)
                    {
                        string command = this.m_strTeamswapCommand.TrimEnd("|log".ToCharArray());
                        this.ExecuteCommand("procon.protected.send", "admin.yell", "For your consistent player reporting you can now use TeamSwap. Type @" + command + " to move yourself between teams.", "10", "player", soldierName);
                        this.adminAssistantCache[soldierName] = true;
                    }
                }
                else if (this.teamswapRoundWhitelist.Count > 0 && this.teamswapRoundWhitelist.TryGetValue(soldierName, out informed))
                {
                    if (informed == false)
                    {
                        string command = this.m_strTeamswapCommand.TrimEnd("|log".ToCharArray());
                        this.ExecuteCommand("procon.protected.send", "admin.yell", "You can use TeamSwap for this round. Type @" + command + " to move yourself between teams.", "10", "player", soldierName);
                        this.teamswapRoundWhitelist[soldierName] = true;
                    }
                }
                if (soldierName == "ColColonCleaner" && !toldCol && isRelease)
                {
                    this.ExecuteCommand("procon.protected.send", "admin.yell", "CONGRATS! This server has version " + this.plugin_version + " of AdKats installed!", "20", "player", "ColColonCleaner");
                    this.toldCol = true;
                }
            }
        }

        public override void OnPlayerJoin(String soldierName)
        {
            //Do nothing here
        }

        public override void OnPlayerLeft(CPlayerInfo playerInfo)
        {
            /*Optional Removal, dictionary is recreated on player listing
             * 
            if (this.playerDictionary.ContainsKey(playerInfo.SoldierName))
            {
                lock (this.playersMutex)
                {
                    this.playerDictionary.Remove(playerInfo.SoldierName);
                }
            }*/
        }

        public override void OnBanAdded(CBanInfo ban)
        {
            this.DebugWrite("OnBanAdded fired", 6);

            AdKat_Ban aBan = AdKats.createABanFromCBan(ban);


            //Queue ban for Addition

            this.ExecuteCommand("procon.protected.send", "banList.list");
        }

        public override void OnBanRemoved(CBanInfo ban)
        {
            this.DebugWrite("OnBanRemoved fired", 6);

            AdKat_Ban aBan = AdKats.createABanFromCBan(ban);

            //Queue ban for removal

            this.ExecuteCommand("procon.protected.send", "banList.list");
        }

        public override void OnBanList(List<CBanInfo> banList)
        {
            this.DebugWrite("OnBanList fired", 6);

            //Check for initial parse from procon
            if (!this.bansSynced)
            {

            }
            /*foreach (CBanInfo ban in this.lstBanList)
            {
                this.ScaledDebugInfo(3, "Entering OnBanList (Name: " + ban.SoldierName + ", GUID: " + ban.Guid + ", IP: " + ban.IpAddress + ", IdType: " + ban.IdType + ", BanLength: " + ban.BanLength.Subset + ", BanSeconds: " + ban.BanLength.Seconds + ", Reason: " + ban.Reason + ")...");

                // only store the ban if it's permanent or longer than 12 hours
                if (ban.BanLength.Subset == TimeoutSubset.TimeoutSubsetType.Permanent || (ban.BanLength.Subset == TimeoutSubset.TimeoutSubsetType.Seconds && ban.BanLength.Seconds >= 43200))
                {
                    CRemoteBanInfo aBan = new CRemoteBanInfo();
                    aBan.SoldierName = ban.SoldierName;
                    aBan.Reason = ban.Reason;

                    if (ban.BanLength.Subset == TimeoutSubset.TimeoutSubsetType.Permanent)
                    {
                        aBan.Length = BanLength.Permanent;
                    }
                    else if (ban.BanLength.Subset == TimeoutSubset.TimeoutSubsetType.Seconds)
                    {
                        aBan.Length = BanLength.Seconds;
                        aBan.Duration = ban.BanLength.Seconds.ToString();
                    }

                    switch (ban.IdType.ToLower())
                    {
                        case "name":
                            aBan.Type = BanType.Name;
                            break;
                        case "guid":
                            aBan.Type = BanType.EAGUID;
                            break;
                        case "pb guid":
                            aBan.Type = BanType.PBGUID;
                            break;
                        case "ip":
                            aBan.Type = BanType.IP;
                            break;
                        default:
                            aBan.Type = BanType.Name;
                            break;
                    }

                    if (rBan.Type == BanType.Name && (this.dicCPlayerInfo.ContainsKey(ban.SoldierName) && this.dicCPunkbusterInfo.ContainsKey(ban.SoldierName)))
                    {
                        lock (this.dicCPlayerInfo)
                        {
                            aBan.EAGUID = this.dicCPlayerInfo[ban.SoldierName].GUID;
                        }
                        lock (this.dicCPunkbusterInfo)
                        {
                            aBan.PBGUID = this.dicCPunkbusterInfo[ban.SoldierName].GUID;
                            if (!String.IsNullOrEmpty(this.dicCPunkbusterInfo[ban.SoldierName].Ip) && this.dicCPunkbusterInfo[ban.SoldierName].Ip.CompareTo("") != 0 && this.dicCPunkbusterInfo[ban.SoldierName].Ip.Contains(":"))
                            {
                                String[] ipPort = this.dicCPunkbusterInfo[ban.SoldierName].Ip.Split(':');
                                aBan.IP = ipPort[0];
                            }
                            else
                            {
                                aBan.IP = this.dicCPunkbusterInfo[ban.SoldierName].Ip;
                            }
                        }
                    }
                    else
                    {
                        if (!String.IsNullOrEmpty(ban.IpAddress) && ban.IpAddress.CompareTo("") != 0 && ban.IpAddress.Contains(":"))
                        {
                            String[] ipPort = ban.IpAddress.Split(':');
                            aBan.IP = ipPort[0];
                        }
                        else
                        {
                            aBan.IP = ban.IpAddress;
                        }
                        if (rBan.Type == BanType.PBGUID)
                        {
                            aBan.PBGUID = ban.Guid;
                        }
                        else
                        {
                            aBan.EAGUID = ban.Guid;
                        }
                    }


                    this.ScaledDebugInfo(6, "Ban reason Cleanup - original " + aBan.Reason);
                    aBan.Reason = SQLcleanup(rBan.Reason);
                    this.ScaledDebugInfo(6, "Ban reason Cleanup - New      " + aBan.Reason);


                    this.AddBan(rBan);

                    // LEIBHOLD HACK ADDED
                    this.ScaledDebugInfo(3, "Ban Added - now check");

                    if (this.CheckBan(rBan))
                    {
                        this.ScaledDebugInfo(3, "Checkban shows good");
                        //So now we remove the ban from the server

                        if (this.ebTestrun == enumBoolYesNo.Yes)
                        {
                            switch (ban.IdType.ToLower())
                            {
                                case "name":
                                    this.ScaledDebugInfo(4, "banList.remove type " + ban.IdType + " name " + aBan.SoldierName);
                                    break;
                                case "guid":
                                    this.ScaledDebugInfo(4, "banList.remove type " + ban.IdType + " name " + aBan.EAGUID);
                                    break;
                                case "pb guid":
                                    this.ScaledDebugInfo(4, "banList.remove type " + ban.IdType + " name " + aBan.PBGUID);
                                    break;
                                case "ip":
                                    this.ScaledDebugInfo(4, "banList.remove type " + ban.IdType + " info " + aBan.IP);
                                    break;
                                default:
                                    this.ScaledDebugInfo(4, "DEAFULTED banList.remove type " + ban.IdType + " info " + aBan.SoldierName);
                                    break;
                            }
                        }
                        else
                        {
                            if (this.ebEmptyServerBanlist == enumBoolYesNo.Yes)
                            {

                                switch (ban.IdType.ToLower())
                                {
                                    case "name":
                                        this.ExecuteCommand("procon.protected.tasks.add", "RemoveBan", "0", "1", "1", "procon.protected.send", "banList.remove", ban.IdType, aBan.SoldierName);
                                        this.ScaledDebugInfo(4, "REM ban shows name --------------- done");
                                        break;
                                    case "guid":
                                        this.ExecuteCommand("procon.protected.tasks.add", "RemoveBan", "0", "1", "1", "procon.protected.send", "banList.remove", ban.IdType, aBan.EAGUID);
                                        this.ScaledDebugInfo(4, "REM ban shows GUID --------------- done");
                                        break;
                                    case "pb guid":
                                        this.ExecuteCommand("procon.protected.tasks.add", "RemoveBan", "0", "1", "1", "procon.protected.send", "punkBuster.pb_sv_command pb_sv_unbanguid", ban.IdType, aBan.PBGUID);
                                        this.ScaledDebugInfo(4, "REMban shows pbguid --------------- done");
                                        break;
                                    case "ip":
                                        this.ExecuteCommand("procon.protected.tasks.add", "RemoveBan", "0", "1", "1", "procon.protected.send", "banList.remove", ban.IdType, aBan.IP);
                                        this.ScaledDebugInfo(4, "REM ban shows  IP --------------- done");
                                        break;
                                    default:
                                        this.ScaledDebugInfo(4, "REM ban shows NAME DEFAULT --------------- done");
                                        break;
                                }
                            }
                        }

                    }
                    else
                    {
                        this.ScaledDebugInfo(3, "Checkban shows ban wasnt added to the database to leave it alone");
                    }

                }

                this.ScaledDebugInfo(1, "Leaving OnBanList (Name: " + ban.SoldierName + ", GUID: " + ban.Guid + ", IP: " + ban.IpAddress + ", IdType: " + ban.IdType + ", BanLength: " + ban.BanLength.Subset + ", BanSeconds: " + ban.BanLength.Seconds + ", Reason: " + ban.Reason + ")...");
            }*/

        }

        public override void OnBanListClear()
        {
            this.DebugWrite("Ban list cleared", 5);
        }
        public override void OnBanListSave()
        {
            this.DebugWrite("Ban list saved", 5);
        }
        public override void OnBanListLoad()
        {
            this.DebugWrite("Ban list loaded", 5);
        }
        public override void OnBanList(List<CBanInfo> banList)
        {
            this.DebugWrite("Bans listed", 5);
        }

        #endregion

        #region Messaging
        //all messaging is redirected to global chat for analysis
        public override void OnGlobalChat(string speaker, string message)
        {
            if (isEnabled)
            {
                //Performance testing area
                if (speaker == this.debugSoldierName)
                {
                    this.commandStartTime = DateTime.Now;
                }
                this.queueMessageForParsing(speaker, message);
            }
        }
        public override void OnTeamChat(string speaker, string message, int teamId) { this.OnGlobalChat(speaker, message); }
        public override void OnSquadChat(string speaker, string message, int teamId, int squadId) { this.OnGlobalChat(speaker, message); }

        public string sendMessageToSource(AdKat_Record record, string message)
        {
            string response = null;
            switch (record.command_source)
            {
                case AdKat_CommandSource.InGame:
                    this.playerSayMessage(record.source_name, message);
                    break;
                case AdKat_CommandSource.Console:
                    this.ConsoleWrite(message);
                    break;
                case AdKat_CommandSource.Settings:
                    this.ConsoleWrite(message);
                    break;
                case AdKat_CommandSource.Database:
                    //Do nothing, no way to communicate to source when database
                    break;
                case AdKat_CommandSource.HTTP:
                    response = message;
                    break;
                default:
                    this.ConsoleError("Command source not set, or not recognized.");
                    break;
            }
            return response;
        }

        public void playerSayMessage(string target, string message)
        {
            ExecuteCommand("procon.protected.send", "admin.say", message, "player", target);
            ExecuteCommand("procon.protected.chat.write", string.Format("(PlayerSay {0}) ", target) + message);
        }

        private void queueMessageForParsing(string speaker, string message)
        {
            this.DebugWrite("Preparing to queue message for parsing", 6);
            lock (unparsedMessageMutex)
            {
                this.unparsedMessageQueue.Enqueue(new KeyValuePair<String, String>(speaker, message));
                this.DebugWrite("Message queued for parsing.", 6);
                this.messageParsingHandle.Set();
            }
        }

        private void queueCommandForParsing(string speaker, string command)
        {
            this.DebugWrite("Preparing to queue command for parsing", 6);
            lock (unparsedCommandMutex)
            {
                this.unparsedCommandQueue.Enqueue(new KeyValuePair<String, String>(speaker, command));
                this.DebugWrite("Command sent to unparsed commands.", 6);
                this.commandParsingHandle.Set();
            }
        }

        private void messagingThreadLoop()
        {
            try
            {
                this.DebugWrite("MESSAGE: Starting Messaging Thread", 2);
                Thread.CurrentThread.Name = "messaging";
                while (true)
                {
                    this.DebugWrite("MESSAGE: Entering Messaging Thread Loop", 7);
                    if (!this.isEnabled)
                    {
                        this.DebugWrite("MESSAGE: Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                        break;
                    }

                    //Get all unparsed inbound messages
                    Queue<KeyValuePair<String, String>> inboundMessages;
                    if (this.unparsedMessageQueue.Count > 0)
                    {
                        this.DebugWrite("MESSAGE: Preparing to lock messaging to retrive new messages", 7);
                        lock (unparsedMessageMutex)
                        {
                            this.DebugWrite("MESSAGE: Inbound messages found. Grabbing.", 6);
                            //Grab all messages in the queue
                            inboundMessages = new Queue<KeyValuePair<string, string>>(this.unparsedMessageQueue.ToArray());
                            //Clear the queue for next run
                            this.unparsedMessageQueue.Clear();
                        }
                    }
                    else
                    {
                        this.DebugWrite("MESSAGE: No inbound messages. Waiting for Input.", 4);
                        //Wait for input
                        this.messageParsingHandle.Reset();
                        this.messageParsingHandle.WaitOne(Timeout.Infinite);
                        continue;
                    }

                    //Loop through all messages in order that they came in
                    while (inboundMessages != null && inboundMessages.Count > 0)
                    {
                        this.DebugWrite("MESSAGE: begin reading message", 6);
                        //Dequeue the first/next message
                        KeyValuePair<String, String> messagePair = inboundMessages.Dequeue();
                        string speaker = messagePair.Key;
                        string message = messagePair.Value;

                        //check for player mute case
                        //ignore if it's a server call
                        if (speaker != "Server")
                        {
                            lock (playersMutex)
                            {
                                //Check if the player is muted
                                this.DebugWrite("MESSAGE: Checking for mute case.", 7);
                                if (this.round_mutedPlayers.ContainsKey(speaker))
                                {
                                    this.DebugWrite("MESSAGE: Player is muted. Acting.", 7);
                                    //Increment the muted chat count
                                    this.round_mutedPlayers[speaker] = this.round_mutedPlayers[speaker] + 1;
                                    //Get player info
                                    CPlayerInfo player_info = this.playerDictionary[speaker];
                                    //Create record
                                    AdKat_Record record = new AdKat_Record();
                                    record.command_source = AdKat_CommandSource.InGame;
                                    record.server_id = this.server_id;
                                    record.server_ip = this.getServerInfo().ExternalGameIpandPort;
                                    record.record_time = DateTime.Now;
                                    record.record_durationMinutes = 0;
                                    record.source_name = "PlayerMuteSystem";
                                    record.target_guid = player_info.GUID;
                                    record.target_name = speaker;
                                    record.target_playerInfo = player_info;
                                    if (this.round_mutedPlayers[speaker] > this.mutedPlayerChances)
                                    {
                                        record.record_message = this.mutedPlayerKickMessage;
                                        record.command_type = AdKat_CommandType.KickPlayer;
                                        record.command_action = AdKat_CommandType.KickPlayer;
                                    }
                                    else
                                    {
                                        record.record_message = mutedPlayerKillMessage;
                                        record.command_type = AdKat_CommandType.KillPlayer;
                                        record.command_action = AdKat_CommandType.KillPlayer;
                                    }
                                    this.queueRecordForProcessing(record);
                                    continue;
                                }
                            }
                        }

                        //Check if the message is a command
                        if (message.StartsWith("@") || message.StartsWith("!"))
                        {
                            message = message.Substring(1);
                        }
                        else if (message.StartsWith("/@") || message.StartsWith("/!"))
                        {
                            message = message.Substring(2);
                        }
                        else if (message.StartsWith("/"))
                        {
                            message = message.Substring(1);
                        }
                        else
                        {
                            //If the message does not cause either of the above clauses, then ignore it.
                            this.DebugWrite("MESSAGE: Message is regular chat. Ignoring.", 7);
                            continue;
                        }
                        this.queueCommandForParsing(speaker, message);
                    }
                }
                this.DebugWrite("MESSAGE: Ending Messaging Thread", 2);
            }
            catch (Exception e)
            {
                this.ConsoleException(e.ToString());
                if (typeof(ThreadAbortException).Equals(e.GetType()))
                {
                    this.DebugWrite("Thread Exception", 4);
                    Thread.ResetAbort();
                    return;
                }
            }
        }

        #endregion

        #region Teamswap Methods

        private void queuePlayerForForceMove(CPlayerInfo player)
        {
            this.DebugWrite("Preparing to queue player for teamswap", 6);
            lock (teamswapMutex)
            {
                this.teamswapForceMoveQueue.Enqueue(player);
                this.teamswapHandle.Set();
                this.DebugWrite("Player queued for teamswap", 6);
            }
        }

        private void queuePlayerForMove(CPlayerInfo player)
        {
            this.DebugWrite("Preparing to add player to 'on-death' move dictionary.", 6);
            lock (teamswapMutex)
            {
                this.teamswapOnDeathMoveDic.Add(player.SoldierName, player);
                this.teamswapHandle.Set();
                this.DebugWrite("Player added to 'on-death' move dictionary.", 6);
            }
        }

        //runs through both team swap queues and performs the swapping
        public void teamswapThreadLoop()
        {
            //assume the max player count per team is 32 if no server info has been provided
            int maxPlayerCount = 32;
            Queue<CPlayerInfo> checkingQueue;
            Queue<CPlayerInfo> movingQueue;
            try
            {
                this.DebugWrite("TSWAP: Starting TeamSwap Thread", 2);
                Thread.CurrentThread.Name = "teamswap";
                while (true)
                {
                    this.DebugWrite("TSWAP: Entering TeamSwap Thread Loop", 7);
                    if (!this.isEnabled)
                    {
                        this.DebugWrite("TSWAP: Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                        break;
                    }

                    //Sleep for 10ms
                    Thread.Sleep(10);

                    //Call List Players
                    this.listPlayersHandle.Reset();
                    this.ExecuteCommand("procon.protected.send", "admin.listPlayers", "all");
                    //Wait for listPlayers to finish
                    this.listPlayersHandle.WaitOne(5000);

                    //Refresh Max Player Count, needed for responsive server size
                    CServerInfo info = this.getServerInfo();
                    if (info != null && info.MaxPlayerCount != maxPlayerCount)
                    {
                        maxPlayerCount = info.MaxPlayerCount / 2;
                    }

                    //Get players who died that need moving
                    if ((this.teamswapOnDeathMoveDic.Count > 0 && this.teamswapOnDeathCheckingQueue.Count > 0) || this.teamswapForceMoveQueue.Count > 0)
                    {
                        this.DebugWrite("TSWAP: Preparing to lock teamswap queues", 4);
                        lock (teamswapMutex)
                        {
                            this.DebugWrite("TSWAP: Players in ready for teamswap. Grabbing.", 6);
                            //Grab all messages in the queue
                            movingQueue = new Queue<CPlayerInfo>(this.teamswapForceMoveQueue.ToArray());
                            checkingQueue = new Queue<CPlayerInfo>(this.teamswapOnDeathCheckingQueue.ToArray());
                            //Clear the queue for next run
                            this.teamswapOnDeathCheckingQueue.Clear();
                            this.teamswapForceMoveQueue.Clear();

                            //Check for "on-death" move players
                            while (this.teamswapOnDeathMoveDic.Count > 0 && checkingQueue != null && checkingQueue.Count > 0)
                            {
                                //Dequeue the first/next player
                                String playerName = checkingQueue.Dequeue().SoldierName;
                                CPlayerInfo player;
                                //If they are 
                                if (this.teamswapOnDeathMoveDic.TryGetValue(playerName, out player))
                                {
                                    //Player has died, remove from the dictionary
                                    this.teamswapOnDeathMoveDic.Remove(playerName);
                                    //Add to move queue
                                    movingQueue.Enqueue(player);
                                }
                            }

                            while (movingQueue != null && movingQueue.Count > 0)
                            {
                                CPlayerInfo player = movingQueue.Dequeue();
                                if (player.TeamID == USTeamId)
                                {
                                    if (!this.containsCPlayerInfo(this.USMoveQueue, player.SoldierName))
                                    {
                                        this.USMoveQueue.Enqueue(player);
                                        this.playerSayMessage(player.SoldierName, "You have been added to the (US -> RU) TeamSwap queue in position " + (this.indexOfCPlayerInfo(this.USMoveQueue, player.SoldierName) + 1) + ".");
                                    }
                                    else
                                    {
                                        this.playerSayMessage(player.SoldierName, "(US -> RU) queue: Position " + (this.indexOfCPlayerInfo(this.USMoveQueue, player.SoldierName) + 1));
                                    }
                                }
                                else
                                {
                                    if (!this.containsCPlayerInfo(this.RUMoveQueue, player.SoldierName))
                                    {
                                        this.RUMoveQueue.Enqueue(player);
                                        this.playerSayMessage(player.SoldierName, "You have been added to the (RU -> US) TeamSwap queue in position " + (this.indexOfCPlayerInfo(this.RUMoveQueue, player.SoldierName) + 1) + ".");
                                    }
                                    else
                                    {
                                        this.playerSayMessage(player.SoldierName, "(RU -> US) queue: Position " + (this.indexOfCPlayerInfo(this.RUMoveQueue, player.SoldierName) + 1));
                                    }
                                }
                            }
                        }
                    }

                    if (this.RUMoveQueue.Count > 0 || this.USMoveQueue.Count > 0)
                    {
                        //Perform player moving
                        Boolean movedPlayer;
                        do
                        {
                            movedPlayer = false;
                            if (this.RUMoveQueue.Count > 0)
                            {
                                if (this.USPlayerCount < maxPlayerCount)
                                {
                                    CPlayerInfo player = this.RUMoveQueue.Dequeue();
                                    ExecuteCommand("procon.protected.send", "admin.movePlayer", player.SoldierName, USTeamId.ToString(), "1", "true");
                                    this.playerSayMessage(player.SoldierName, "Swapping you from team RU to team US");
                                    movedPlayer = true;
                                    this.USPlayerCount++;
                                }
                            }
                            if (this.USMoveQueue.Count > 0)
                            {
                                if (this.RUPlayerCount < maxPlayerCount)
                                {
                                    CPlayerInfo player = this.USMoveQueue.Dequeue();
                                    ExecuteCommand("procon.protected.send", "admin.movePlayer", player.SoldierName, RUTeamId.ToString(), "1", "true");
                                    this.playerSayMessage(player.SoldierName, "Swapping you from team US to team RU");
                                    movedPlayer = true;
                                    this.RUPlayerCount++;
                                }
                            }
                        } while (movedPlayer);
                    }
                    else
                    {
                        this.DebugWrite("TSWAP: No players to swap. Waiting for input.", 4);
                        //There are no players to swap, wait.
                        this.teamswapHandle.Reset();
                        this.teamswapHandle.WaitOne(Timeout.Infinite);
                        continue;
                    }
                }
                this.DebugWrite("TSWAP: Ending TeamSwap Thread", 2);
            }
            catch (Exception e)
            {
                this.ConsoleException("TSWAP: " + e.ToString());
                if (typeof(ThreadAbortException).Equals(e.GetType()))
                {
                    this.DebugWrite("TSWAP: Thread Exception", 4);
                    Thread.ResetAbort();
                    return;
                }
            }
        }

        //Whether a move queue contains a given player
        private bool containsCPlayerInfo(Queue<CPlayerInfo> queueList, String player)
        {
            CPlayerInfo[] playerArray = queueList.ToArray();
            for (Int32 index = 0; index < queueList.Count; index++)
            {
                if (((CPlayerInfo)playerArray[index]).SoldierName.Equals(player))
                {
                    return true;
                }
            }
            return false;
        }

        //Helper method to find a player's information in the move queue
        private CPlayerInfo getCPlayerInfo(Queue<CPlayerInfo> queueList, String player)
        {
            CPlayerInfo[] playerArray = queueList.ToArray();
            for (Int32 index = 0; index < queueList.Count; index++)
            {
                if (((CPlayerInfo)playerArray[index]).SoldierName.Equals(player))
                {
                    return ((CPlayerInfo)playerArray[index]);
                }
            }
            return null;
        }

        //The index of a player in the move queue
        //TODO make this accessible via in-game command
        private Int32 indexOfCPlayerInfo(Queue<CPlayerInfo> queueList, String player)
        {
            CPlayerInfo[] playerArray = queueList.ToArray();
            for (Int32 index = 0; index < queueList.Count; index++)
            {
                if (((CPlayerInfo)playerArray[index]).SoldierName.Equals(player))
                {
                    return index;
                }
            }
            return -1;
        }

        #endregion

        #region Record Creation and Processing

        private void queueRecordForProcessing(AdKat_Record record)
        {
            this.DebugWrite("Preparing to queue record for processing", 6);
            lock (unprocessedRecordMutex)
            {
                this.unprocessedRecordQueue.Enqueue(record);
                this.DebugWrite("Record queued for processing", 6);
                this.dbCommHandle.Set();
            }
        }

        private void commandParsingThreadLoop()
        {
            try
            {
                this.DebugWrite("COMMAND: Starting Command Parsing Thread", 2);
                Thread.CurrentThread.Name = "Command";
                while (true)
                {
                    this.DebugWrite("COMMAND: Entering Command Parsing Thread Loop", 7);
                    if (!this.isEnabled)
                    {
                        this.DebugWrite("COMMAND: Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                        break;
                    }

                    //Sleep for 10ms
                    Thread.Sleep(10);

                    //Get all unparsed inbound messages
                    Queue<KeyValuePair<String, String>> unparsedCommands;
                    if (this.unparsedCommandQueue.Count > 0)
                    {
                        this.DebugWrite("COMMAND: Preparing to lock command queue to retrive new commands", 7);
                        lock (unparsedCommandMutex)
                        {
                            this.DebugWrite("COMMAND: Inbound commands found. Grabbing.", 6);
                            //Grab all messages in the queue
                            unparsedCommands = new Queue<KeyValuePair<string, string>>(this.unparsedCommandQueue.ToArray());
                            //Clear the queue for next run
                            this.unparsedCommandQueue.Clear();
                        }

                        //Loop through all commands in order that they came in
                        while (unparsedCommands != null && unparsedCommands.Count > 0)
                        {
                            this.DebugWrite("COMMAND: begin reading command", 6);
                            //Dequeue the first/next command
                            KeyValuePair<String, String> commandPair = unparsedCommands.Dequeue();
                            string speaker = commandPair.Key;
                            string command = commandPair.Value;

                            AdKat_Record record = new AdKat_Record();
                            record.command_source = AdKat_CommandSource.InGame;
                            record.source_name = speaker;
                            //Complete the record creation
                            this.completeRecord(record, command);
                        }
                    }
                    else
                    {
                        this.DebugWrite("COMMAND: No inbound commands, ready.", 7);
                        //No commands to parse, ready.
                        this.commandParsingHandle.Reset();
                        this.commandParsingHandle.WaitOne(Timeout.Infinite);
                        continue;
                    }
                }
                this.DebugWrite("COMMAND: Ending Command Thread", 2);
            }
            catch (Exception e)
            {
                this.ConsoleException(e.ToString());
                if (typeof(ThreadAbortException).Equals(e.GetType()))
                {
                    this.DebugWrite("COMMAND: Thread Exception", 4);
                    Thread.ResetAbort();
                    return;
                }
            }
        }

        //Before calling this, the record is initialized, and command_source/source_name are filled
        public void completeRecord(AdKat_Record record, String message)
        {
            try
            {
                //Initial split of command by whitespace
                String[] splitMessage = message.Split(' ');
                if (splitMessage.Length < 1)
                {
                    this.DebugWrite("Completely blank command entered", 5);
                    this.sendMessageToSource(record, "You entered a completely blank command.");
                    return;
                }
                String commandString = splitMessage[0].ToLower();
                DebugWrite("Raw Command: " + commandString, 6);
                String remainingMessage = message.TrimStart(splitMessage[0].ToCharArray()).Trim();

                //GATE 1: Add general data
                record.server_id = this.server_id;
                CServerInfo info = this.getServerInfo();
                if (info != null)
                {
                    record.server_ip = info.ExternalGameIpandPort;
                }
                record.record_time = DateTime.Now;

                //GATE 2: Add Command
                AdKat_CommandType commandType = this.getCommand(commandString);
                if (commandType == AdKat_CommandType.Default)
                {
                    //If command not parsable, return without creating
                    DebugWrite("Command not parsable", 6);
                    return;
                }
                record.command_type = commandType;
                record.command_action = commandType;
                DebugWrite("Command type: " + record.command_type, 6);

                //GATE 3: Check Access Rights
                //Check for server command case
                if (record.source_name == "server")
                {
                    record.source_name = "PRoConAdmin";
                    record.command_source = AdKat_CommandSource.Console;
                }
                //Check if player has the right to perform what he's asking, only perform for InGame actions
                else if (record.command_source == AdKat_CommandSource.InGame && !this.hasAccess(record.source_name, record.command_type))
                {
                    DebugWrite("No rights to call command", 6);
                    this.sendMessageToSource(record, "Cannot use class " + this.AdKat_CommandAccessRank[record.command_type] + " command, " + record.command_type + ". You are access class " + this.getAccessLevel(record.source_name) + ".");
                    //Return without creating if player doesn't have rights to do it
                    return;
                }

                //GATE 4: Add specific data based on command type.
                //Items that need filling before record processing:
                //target_name
                //target_guid
                //target_playerInfo
                //record_message
                switch (record.command_type)
                {
                    #region MovePlayer
                    case AdKat_CommandType.MovePlayer:
                        {
                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = this.parseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.command_source != AdKat_CommandSource.InGame)
                                    {
                                        this.sendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                        break;
                                    }
                                    record.record_message = "Self-Inflicted";
                                    record.target_name = record.source_name;
                                    this.completeTargetInformation(record, true);
                                    break;
                                case 1:
                                    record.record_message = "MovePlayer";
                                    record.target_name = parameters[0];
                                    //Handle based on report ID if possible
                                    if (!this.handleRoundReport(record))
                                    {
                                        this.completeTargetInformation(record, false);
                                    }
                                    break;
                                default:
                                    this.sendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    return;
                            }
                        }
                        break;
                    #endregion
                    #region ForceMovePlayer
                    case AdKat_CommandType.ForceMovePlayer:
                        {
                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = this.parseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.command_source != AdKat_CommandSource.InGame)
                                    {
                                        this.sendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                        break;
                                    }
                                    record.record_message = "Self-Inflicted";
                                    record.target_name = record.source_name;
                                    this.completeTargetInformation(record, true);
                                    break;
                                case 1:
                                    record.record_message = "ForceMovePlayer";
                                    record.target_name = parameters[0];
                                    //Handle based on report ID if possible
                                    if (!this.handleRoundReport(record))
                                    {
                                        this.completeTargetInformation(record, false);
                                    }
                                    break;
                                default:
                                    this.sendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    return;
                            }
                        }
                        break;
                    #endregion
                    #region Teamswap
                    case AdKat_CommandType.Teamswap:
                        {
                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //May only call this command from in-game
                            if (record.command_source != AdKat_CommandSource.InGame)
                            {
                                this.sendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                break;
                            }
                            record.record_message = "TeamSwap";
                            record.target_name = record.source_name;
                            this.completeTargetInformation(record, false);
                        }
                        break;
                    #endregion
                    #region KillPlayer
                    case AdKat_CommandType.KillPlayer:
                        {
                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = this.parseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.command_source != AdKat_CommandSource.InGame)
                                    {
                                        this.sendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                        break;
                                    }
                                    record.record_message = "Self-Inflicted";
                                    record.target_name = record.source_name;
                                    this.completeTargetInformation(record, true);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    //Handle based on report ID as only option
                                    if (!this.handleRoundReport(record))
                                    {
                                        this.sendMessageToSource(record, "No reason given, unable to submit.");
                                    }
                                    break;
                                case 2:
                                    record.target_name = parameters[0];

                                    //attempt to handle via pre-message ID
                                    record.record_message = this.getPreMessage(parameters[1], this.requirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        this.sendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + this.preMessageList.Count);
                                        break;
                                    }

                                    if (record.record_message.Length >= this.requiredReasonLength)
                                    {
                                        //Handle based on report ID if possible
                                        if (!this.handleRoundReport(record))
                                        {
                                            this.completeTargetInformation(record, false);
                                        }
                                    }
                                    else
                                    {
                                        this.sendMessageToSource(record, "Reason too short, unable to submit.");
                                    }
                                    break;
                                default:
                                    this.sendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    return;
                            }
                        }
                        break;
                    #endregion
                    #region KickPlayer
                    case AdKat_CommandType.KickPlayer:
                        {
                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = this.parseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.command_source != AdKat_CommandSource.InGame)
                                    {
                                        this.sendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                        break;
                                    }
                                    record.record_message = "Self-Inflicted";
                                    record.target_name = record.source_name;
                                    this.completeTargetInformation(record, true);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    //Handle based on report ID as only option
                                    if (!this.handleRoundReport(record))
                                    {
                                        this.sendMessageToSource(record, "No reason given, unable to submit.");
                                    }
                                    break;
                                case 2:
                                    record.target_name = parameters[0];

                                    //attempt to handle via pre-message ID
                                    record.record_message = this.getPreMessage(parameters[1], this.requirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        this.sendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + this.preMessageList.Count);
                                        break;
                                    }

                                    if (record.record_message.Length >= this.requiredReasonLength)
                                    {
                                        //Handle based on report ID if possible
                                        if (!this.handleRoundReport(record))
                                        {
                                            this.completeTargetInformation(record, false);
                                        }
                                    }
                                    else
                                    {
                                        this.sendMessageToSource(record, "Reason too short, unable to submit.");
                                    }
                                    break;
                                default:
                                    this.sendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    return;
                            }
                        }
                        break;
                    #endregion
                    #region TempBanPlayer
                    case AdKat_CommandType.TempBanPlayer:
                        {
                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = this.parseParameters(remainingMessage, 3);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.command_source != AdKat_CommandSource.InGame)
                                    {
                                        this.sendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                        break;
                                    }
                                    this.sendMessageToSource(record, "No parameters given, unable to submit.");
                                    break;
                                case 1:
                                    int record_duration = 0;
                                    DebugWrite("Raw Duration: " + parameters[0], 6);
                                    if (!Int32.TryParse(parameters[0], out record_duration))
                                    {
                                        this.sendMessageToSource(record, "Invalid time given, unable to submit.");
                                        return;
                                    }
                                    record.record_durationMinutes = record_duration;
                                    //Target is source
                                    record.record_message = "Self-Inflicted";
                                    record.target_name = record.source_name;
                                    this.completeTargetInformation(record, true);
                                    break;
                                case 2:
                                    DebugWrite("Raw Duration: " + parameters[0], 6);
                                    if (Int32.TryParse(parameters[0], out record_duration))
                                    {
                                        record.record_durationMinutes = record_duration;

                                        record.target_name = parameters[1];
                                        DebugWrite("target: " + record.target_name, 6);

                                        //Handle based on report ID as only option
                                        if (!this.handleRoundReport(record))
                                        {
                                            this.sendMessageToSource(record, "No reason given, unable to submit.");
                                        }
                                    }
                                    else
                                    {
                                        this.sendMessageToSource(record, "Invalid time given, unable to submit.");
                                    }
                                    break;
                                case 3:
                                    DebugWrite("Raw Duration: " + parameters[0], 6);
                                    if (Int32.TryParse(parameters[0], out record_duration))
                                    {
                                        record.record_durationMinutes = record_duration;

                                        record.target_name = parameters[1];
                                        DebugWrite("target: " + record.target_name, 6);

                                        //attempt to handle via pre-message ID
                                        record.record_message = this.getPreMessage(parameters[2], this.requirePreMessageUse);
                                        if (record.record_message == null)
                                        {
                                            this.sendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + this.preMessageList.Count);
                                            break;
                                        }

                                        DebugWrite("reason: " + record.record_message, 6);
                                        if (record.record_message.Length >= this.requiredReasonLength)
                                        {
                                            //Handle based on report ID if possible
                                            if (!this.handleRoundReport(record))
                                            {
                                                this.completeTargetInformation(record, false);
                                            }
                                        }
                                        else
                                        {
                                            DebugWrite("reason too short", 6);
                                            this.sendMessageToSource(record, "Reason too short, unable to submit.");
                                        }
                                    }
                                    else
                                    {
                                        this.sendMessageToSource(record, "Invalid time given, unable to submit.");
                                    }
                                    break;
                                default:
                                    this.sendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    return;
                            }
                        }
                        break;
                    #endregion
                    #region PermabanPlayer
                    case AdKat_CommandType.PermabanPlayer:
                        {
                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = this.parseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.command_source != AdKat_CommandSource.InGame)
                                    {
                                        this.sendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                        break;
                                    }
                                    record.record_message = "Self-Inflicted";
                                    record.target_name = record.source_name;
                                    this.completeTargetInformation(record, true);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    //Handle based on report ID as only option
                                    if (!this.handleRoundReport(record))
                                    {
                                        this.sendMessageToSource(record, "No reason given, unable to submit.");
                                    }
                                    break;
                                case 2:
                                    record.target_name = parameters[0];

                                    //attempt to handle via pre-message ID
                                    record.record_message = this.getPreMessage(parameters[1], this.requirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        this.sendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + this.preMessageList.Count);
                                        break;
                                    }

                                    if (record.record_message.Length >= this.requiredReasonLength)
                                    {
                                        //Handle based on report ID if possible
                                        if (!this.handleRoundReport(record))
                                        {
                                            this.completeTargetInformation(record, false);
                                        }
                                    }
                                    else
                                    {
                                        this.sendMessageToSource(record, "Reason too short, unable to submit.");
                                    }
                                    break;
                                default:
                                    this.sendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    return;
                            }
                        }
                        break;
                    #endregion
                    #region PunishPlayer
                    case AdKat_CommandType.PunishPlayer:
                        {
                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = this.parseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.command_source != AdKat_CommandSource.InGame)
                                    {
                                        this.sendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                        break;
                                    }
                                    record.record_message = "Self-Inflicted";
                                    record.target_name = record.source_name;
                                    this.completeTargetInformation(record, true);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    //Handle based on report ID as only option
                                    if (!this.handleRoundReport(record))
                                    {
                                        this.sendMessageToSource(record, "No reason given, unable to submit.");
                                    }
                                    break;
                                case 2:
                                    record.target_name = parameters[0];

                                    //attempt to handle via pre-message ID
                                    record.record_message = this.getPreMessage(parameters[1], this.requirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        this.sendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + this.preMessageList.Count);
                                        break;
                                    }

                                    if (record.record_message.Length >= this.requiredReasonLength)
                                    {
                                        //Handle based on report ID if possible
                                        if (!this.handleRoundReport(record))
                                        {
                                            this.completeTargetInformation(record, false);
                                        }
                                    }
                                    else
                                    {
                                        this.sendMessageToSource(record, "Reason too short, unable to submit.");
                                    }
                                    break;
                                default:
                                    this.sendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    return;
                            }
                        }
                        break;
                    #endregion
                    #region ForgivePlayer
                    case AdKat_CommandType.ForgivePlayer:
                        {
                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = this.parseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.command_source != AdKat_CommandSource.InGame)
                                    {
                                        this.sendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                        break;
                                    }
                                    record.record_message = "Self-Inflicted";
                                    record.target_name = record.source_name;
                                    this.completeTargetInformation(record, true);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    //Handle based on report ID as only option
                                    if (!this.handleRoundReport(record))
                                    {
                                        this.sendMessageToSource(record, "No reason given, unable to submit.");
                                    }
                                    break;
                                case 2:
                                    record.target_name = parameters[0];

                                    //attempt to handle via pre-message ID
                                    record.record_message = this.getPreMessage(parameters[1], this.requirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        this.sendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + this.preMessageList.Count);
                                        break;
                                    }

                                    if (record.record_message.Length >= this.requiredReasonLength)
                                    {
                                        //Handle based on report ID if possible
                                        if (!this.handleRoundReport(record))
                                        {
                                            this.completeTargetInformation(record, false);
                                        }
                                    }
                                    else
                                    {
                                        this.sendMessageToSource(record, "Reason too short, unable to submit.");
                                    }
                                    break;
                                default:
                                    this.sendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    return;
                            }
                        }
                        break;
                    #endregion
                    #region MutePlayer
                    case AdKat_CommandType.MutePlayer:
                        {
                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = this.parseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.command_source != AdKat_CommandSource.InGame)
                                    {
                                        this.sendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                        break;
                                    }
                                    record.record_message = "Self-Inflicted";
                                    record.target_name = record.source_name;
                                    this.completeTargetInformation(record, true);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    //Handle based on report ID as only option
                                    if (!this.handleRoundReport(record))
                                    {
                                        this.sendMessageToSource(record, "No reason given, unable to submit.");
                                    }
                                    break;
                                case 2:
                                    record.target_name = parameters[0];

                                    //attempt to handle via pre-message ID
                                    record.record_message = this.getPreMessage(parameters[1], this.requirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        this.sendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + this.preMessageList.Count);
                                        break;
                                    }

                                    if (record.record_message.Length >= this.requiredReasonLength)
                                    {
                                        //Handle based on report ID if possible
                                        if (!this.handleRoundReport(record))
                                        {
                                            this.completeTargetInformation(record, false);
                                        }
                                    }
                                    else
                                    {
                                        this.sendMessageToSource(record, "Reason too short, unable to submit.");
                                    }
                                    break;
                                default:
                                    this.sendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    return;
                            }
                        }
                        break;
                    #endregion
                    #region RoundWhitelistPlayer
                    case AdKat_CommandType.RoundWhitelistPlayer:
                        {
                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = this.parseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.command_source != AdKat_CommandSource.InGame)
                                    {
                                        this.sendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                        break;
                                    }
                                    record.record_message = "Self-Inflicted";
                                    record.target_name = record.source_name;
                                    this.completeTargetInformation(record, true);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    //Handle based on report ID as only option
                                    if (!this.handleRoundReport(record))
                                    {
                                        this.sendMessageToSource(record, "No reason given, unable to submit.");
                                    }
                                    break;
                                case 2:
                                    record.target_name = parameters[0];

                                    //attempt to handle via pre-message ID
                                    record.record_message = this.getPreMessage(parameters[1], false);

                                    if (record.record_message.Length >= this.requiredReasonLength)
                                    {
                                        //Handle based on report ID if possible
                                        if (!this.handleRoundReport(record))
                                        {
                                            this.completeTargetInformation(record, false);
                                        }
                                    }
                                    else
                                    {
                                        this.sendMessageToSource(record, "Reason too short, unable to submit.");
                                    }
                                    break;
                                default:
                                    this.sendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    return;
                            }
                        }
                        break;
                    #endregion
                    #region ReportPlayer
                    case AdKat_CommandType.ReportPlayer:
                        {
                            string command = this.m_strReportCommand.TrimEnd("|log".ToCharArray());

                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = this.parseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    this.sendMessageToSource(record, "Format must be: @" + command + " playername reason");
                                    break;
                                case 1:
                                    this.sendMessageToSource(record, "Format must be: @" + command + " playername reason");
                                    break;
                                case 2:
                                    record.target_name = parameters[0];
                                    DebugWrite("target: " + record.target_name, 6);

                                    //attempt to handle via pre-message ID
                                    record.record_message = this.getPreMessage(parameters[1], false);

                                    DebugWrite("reason: " + record.record_message, 6);
                                    if (record.record_message.Length >= this.requiredReasonLength)
                                    {
                                        this.completeTargetInformation(record, false);
                                    }
                                    else
                                    {
                                        DebugWrite("reason too short", 6);
                                        this.sendMessageToSource(record, "Reason too short, unable to submit.");
                                        return;
                                    }
                                    break;
                                default:
                                    this.sendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    return;
                            }
                        }
                        break;
                    #endregion
                    #region CallAdmin
                    case AdKat_CommandType.CallAdmin:
                        {
                            string command = this.m_strCallAdminCommand.TrimEnd("|log".ToCharArray());

                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = this.parseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    this.sendMessageToSource(record, "Format must be: @" + command + " playername reason");
                                    break;
                                case 1:
                                    this.sendMessageToSource(record, "Format must be: @" + command + " playername reason");
                                    break;
                                case 2:
                                    record.target_name = parameters[0];
                                    DebugWrite("target: " + record.target_name, 6);

                                    //attempt to handle via pre-message ID
                                    record.record_message = this.getPreMessage(parameters[1], false);

                                    DebugWrite("reason: " + record.record_message, 6);
                                    if (record.record_message.Length >= this.requiredReasonLength)
                                    {
                                        this.completeTargetInformation(record, false);
                                    }
                                    else
                                    {
                                        DebugWrite("reason too short", 6);
                                        this.sendMessageToSource(record, "Reason too short, unable to submit.");
                                        return;
                                    }
                                    break;
                                default:
                                    this.sendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    return;
                            }
                        }
                        break;
                    #endregion
                    #region NukeServer
                    case AdKat_CommandType.NukeServer:
                        {
                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = this.parseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    this.sendMessageToSource(record, "No parameters given, unable to submit.");
                                    break;
                                case 1:
                                    string targetTeam = parameters[0];
                                    record.record_message = "Nuke Server";
                                    DebugWrite("target: " + targetTeam, 6);
                                    if (targetTeam.ToLower().Contains("us"))
                                    {
                                        record.target_name = "US Team";
                                        record.target_guid = "US Team";
                                        record.record_message += " (US Team)";
                                    }
                                    else if (targetTeam.ToLower().Contains("ru"))
                                    {
                                        record.target_name = "RU Team";
                                        record.target_guid = "RU Team";
                                        record.record_message += " (RU Team)";
                                    }
                                    else if (targetTeam.ToLower().Contains("all"))
                                    {
                                        record.target_name = "Everyone";
                                        record.target_guid = "Everyone";
                                        record.record_message += " (Everyone)";
                                    }
                                    else
                                    {
                                        this.sendMessageToSource(record, "Use 'US', 'RU', or 'ALL' as targets.");
                                    }
                                    //Have the admin confirm the action
                                    this.confirmActionWithSource(record);
                                    break;
                                default:
                                    this.sendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    return;
                            }
                        }
                        break;
                    #endregion
                    #region KickAll
                    case AdKat_CommandType.KickAll:
                        this.cancelSourcePendingAction(record);
                        record.target_name = "Non-Admins";
                        record.target_guid = "Non-Admins";
                        record.record_message = "Kick All Players";
                        this.confirmActionWithSource(record);
                        break;
                    #endregion
                    #region EndLevel
                    case AdKat_CommandType.EndLevel:
                        {
                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = this.parseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    this.sendMessageToSource(record, "No parameters given, unable to submit.");
                                    return;
                                case 1:
                                    string targetTeam = parameters[0];
                                    DebugWrite("target team: " + targetTeam, 6);
                                    record.record_message = "End Round";
                                    if (targetTeam.ToLower().Contains("us"))
                                    {
                                        record.target_name = "US Team";
                                        record.target_guid = "US Team";
                                        record.record_message += " (US Win)";
                                    }
                                    else if (targetTeam.ToLower().Contains("ru"))
                                    {
                                        record.target_name = "RU Team";
                                        record.target_guid = "RU Team";
                                        record.record_message += " (RU Win)";
                                    }
                                    else
                                    {
                                        this.sendMessageToSource(record, "Use 'US' or 'RU' as team names to end round");
                                    }
                                    break;
                                default:
                                    this.sendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    return;
                            }
                            //Have the admin confirm the action
                            this.confirmActionWithSource(record);
                        }
                        break;
                    #endregion
                    #region RestartLevel
                    case AdKat_CommandType.RestartLevel:
                        this.cancelSourcePendingAction(record);
                        record.target_name = "Server";
                        record.target_guid = "Server";
                        record.record_message = "Restart Round";
                        this.confirmActionWithSource(record);
                        break;
                    #endregion
                    #region NextLevel
                    case AdKat_CommandType.NextLevel:
                        this.cancelSourcePendingAction(record);
                        record.target_name = "Server";
                        record.target_guid = "Server";
                        record.record_message = "Run Next Map";
                        this.confirmActionWithSource(record);
                        break;
                    #endregion
                    #region WhatIs
                    case AdKat_CommandType.WhatIs:
                        {
                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = this.parseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    this.sendMessageToSource(record, "No parameters given, unable to submit.");
                                    return;
                                case 1:
                                    record.record_message = this.getPreMessage(parameters[0], true);
                                    if (record.record_message == null)
                                    {
                                        this.sendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + this.preMessageList.Count);
                                    }
                                    else
                                    {
                                        this.sendMessageToSource(record, record.record_message);
                                    }
                                    break;
                                default:
                                    this.sendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    return;
                            }
                            //This type is not processed
                        }
                        break;
                    #endregion
                    #region AdminSay
                    case AdKat_CommandType.AdminSay:
                        {
                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = this.parseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    this.sendMessageToSource(record, "No parameters given, unable to submit.");
                                    return;
                                case 1:
                                    record.record_message = this.getPreMessage(parameters[0], false);
                                    DebugWrite("message: " + record.record_message, 6);
                                    record.target_name = "Server";
                                    record.target_guid = "Server";
                                    break;
                                default:
                                    this.sendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    return;
                            }
                            this.queueRecordForProcessing(record);
                        }
                        break;
                    #endregion
                    #region AdminYell
                    case AdKat_CommandType.AdminYell:
                        {
                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = this.parseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    this.sendMessageToSource(record, "No parameters given, unable to submit.");
                                    return;
                                case 1:
                                    record.record_message = this.getPreMessage(parameters[0], false).ToUpper();
                                    DebugWrite("message: " + record.record_message, 6);
                                    record.target_name = "Server";
                                    record.target_guid = "Server";
                                    break;
                                default:
                                    this.sendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    return;
                            }
                            this.queueRecordForProcessing(record);
                        }
                        break;
                    #endregion
                    #region PlayerSay
                    case AdKat_CommandType.PlayerSay:
                        {
                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = this.parseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    this.sendMessageToSource(record, "No parameters given, unable to submit.");
                                    return;
                                case 1:
                                    this.sendMessageToSource(record, "No message given, unable to submit.");
                                    return;
                                case 2:
                                    record.target_name = parameters[0];
                                    DebugWrite("target: " + record.target_name, 6);

                                    record.record_message = this.getPreMessage(parameters[1], false);
                                    DebugWrite("message: " + record.record_message, 6);

                                    this.completeTargetInformation(record, false);
                                    break;
                                default:
                                    this.sendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    return;
                            }
                        }
                        break;
                    #endregion
                    #region PlayerYell
                    case AdKat_CommandType.PlayerYell:
                        {
                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = this.parseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    this.sendMessageToSource(record, "No parameters given, unable to submit.");
                                    return;
                                case 1:
                                    this.sendMessageToSource(record, "No message given, unable to submit.");
                                    return;
                                case 2:
                                    record.target_name = parameters[0];
                                    DebugWrite("target: " + record.target_name, 6);

                                    record.record_message = this.getPreMessage(parameters[1], false).ToUpper();
                                    DebugWrite("message: " + record.record_message, 6);

                                    this.completeTargetInformation(record, false);
                                    break;
                                default:
                                    this.sendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    return;
                            }
                        }
                        break;
                    #endregion
                    #region ConfirmCommand
                    case AdKat_CommandType.ConfirmCommand:
                        this.DebugWrite("attempting to confirm command", 6);
                        AdKat_Record recordAttempt = null;
                        this.actionConfirmDic.TryGetValue(record.source_name, out recordAttempt);
                        if (recordAttempt != null)
                        {
                            this.DebugWrite("command found, calling processing", 6);
                            this.actionConfirmDic.Remove(record.source_name);
                            this.queueRecordForProcessing(recordAttempt);
                        }
                        else
                        {
                            this.DebugWrite("no command to confirm", 6);
                            this.sendMessageToSource(record, "No command to confirm.");
                        }
                        //This type is not processed
                        break;
                    #endregion
                    #region CancelCommand
                    case AdKat_CommandType.CancelCommand:
                        this.DebugWrite("attempting to cancel command", 6);
                        if (!this.actionConfirmDic.Remove(record.source_name))
                        {
                            this.DebugWrite("no command to cancel", 6);
                            this.sendMessageToSource(record, "No command to cancel.");
                        }
                        //This type is not processed
                        break;
                    #endregion
                    default:
                        break;
                }
                return;
            }
            catch (Exception e)
            {
                this.ConsoleException(e.ToString());
            }
        }

        public string completeTargetInformation(AdKat_Record record, Boolean requireConfirm)
        {
            //string player = record.target_name;
            try
            {
                lock (playersMutex)
                {
                    //Check for an exact match
                    if (playerDictionary.ContainsKey(record.target_name))
                    {
                        //Exact player match, call processing without confirmation
                        record.target_playerInfo = this.playerDictionary[record.target_name];
                        record.target_guid = record.target_playerInfo.GUID;
                        if (!requireConfirm)
                        {
                            //Process record right away
                            this.queueRecordForProcessing(record);
                        }
                        else
                        {
                            this.confirmActionWithSource(record);
                        }
                    }
                    //Get all substring matches
                    Converter<String, List<CPlayerInfo>> ExactNameMatches = delegate(String sub)
                    {
                        List<CPlayerInfo> matches = new List<CPlayerInfo>();
                        if (String.IsNullOrEmpty(sub)) return matches;
                        foreach (CPlayerInfo player in this.playerList)
                        {
                            if (Regex.Match(player.SoldierName, sub, RegexOptions.IgnoreCase).Success)
                            {
                                matches.Add(player);
                            }
                        }
                        return matches;
                    };
                    List<CPlayerInfo> substringMatches = ExactNameMatches(record.target_name);
                    if (substringMatches.Count == 1)
                    {
                        //Only one substring match, call processing without confirmation if able
                        record.target_name = substringMatches[0].SoldierName;
                        record.target_guid = substringMatches[0].GUID;
                        record.target_playerInfo = substringMatches[0];
                        if (!requireConfirm)
                        {
                            //Process record right away
                            this.queueRecordForProcessing(record);
                        }
                        else
                        {
                            this.confirmActionWithSource(record);
                        }
                    }
                    else if (substringMatches.Count > 1)
                    {
                        //Multiple players matched the query, choose correct one
                        string msg = "'" + record.target_name + "' matches multiple players: ";
                        bool first = true;
                        CPlayerInfo suggestion = null;
                        foreach (CPlayerInfo player in substringMatches)
                        {
                            if (first)
                            {
                                msg = msg + player.SoldierName;
                                first = false;
                            }
                            else
                            {
                                msg = msg + ", " + player.SoldierName;
                            }
                            //Suggest player names that start with the text admins entered over others
                            if (player.SoldierName.ToLower().StartsWith(record.target_name.ToLower()))
                            {
                                suggestion = player;
                            }
                        }
                        if (suggestion == null)
                        {
                            //If no player name starts with what admins typed, suggest substring name with lowest Levenshtein distance
                            int bestDistance = Int32.MaxValue;
                            foreach (CPlayerInfo player in substringMatches)
                            {
                                int distance = LevenshteinDistance(record.target_name, player.SoldierName);
                                if (distance < bestDistance)
                                {
                                    bestDistance = distance;
                                    suggestion = player;
                                }
                            }
                        }
                        //If the suggestion is still null, something has failed
                        if (suggestion == null) { this.DebugWrite("name suggestion system failed with substring matches", 5); };

                        //Inform admin of multiple players found
                        this.sendMessageToSource(record, msg);

                        //Use suggestion for target
                        record.target_guid = suggestion.GUID;
                        record.target_name = suggestion.SoldierName;
                        record.target_playerInfo = suggestion;
                        //Send record to attempt list for confirmation
                        return this.confirmActionWithSource(record);
                    }
                    else
                    {
                        //There were no players found, run a fuzzy search using Levenshtein Distance on all players in server
                        CPlayerInfo fuzzyMatch = null;
                        int bestDistance = Int32.MaxValue;
                        foreach (CPlayerInfo player in this.playerList)
                        {
                            int distance = LevenshteinDistance(record.target_name, player.SoldierName);
                            if (distance < bestDistance)
                            {
                                bestDistance = distance;
                                fuzzyMatch = player;
                            }
                        }
                        //If the suggestion is still null, something has failed
                        if (fuzzyMatch == null) { this.DebugWrite("name suggestion system failed fuzzy match", 5); return "ERROR"; };

                        //Use suggestion for target
                        record.target_guid = fuzzyMatch.GUID;
                        record.target_name = fuzzyMatch.SoldierName;
                        record.target_playerInfo = fuzzyMatch;
                        //Send record to attempt list for confirmation
                        return this.confirmActionWithSource(record);
                    }
                }
            }
            catch (Exception e)
            {
                this.ConsoleException(e.ToString());
                return e.ToString();
            }
            return "END OF FUNCTION";
        }

        public string confirmActionWithSource(AdKat_Record record)
        {
            lock (actionConfirmMutex)
            {
                this.cancelSourcePendingAction(record);
                this.actionConfirmDic.Add(record.source_name, record);
                //Send record to attempt list
                return this.sendMessageToSource(record, record.command_type + "->" + record.target_name + " for " + record.record_message + "?");
            }
        }

        public void cancelSourcePendingAction(AdKat_Record record)
        {
            this.DebugWrite("attempting to cancel command", 6);
            lock (actionConfirmMutex)
            {
                if (!this.actionConfirmDic.Remove(record.source_name))
                {
                    this.DebugWrite("No command to cancel.", 6);
                    //this.sendMessageToSource(record, "No command to cancel.");
                }
                else
                {
                    this.DebugWrite("Commmand Canceled", 6);
                    //this.sendMessageToSource(record, "Previous Command Canceled.");
                }
            }
        }

        public void autoWhitelistPlayers()
        {
            try
            {
                lock (playersMutex)
                {
                    if (this.playersToAutoWhitelist > 0)
                    {
                        Random random = new Random();
                        List<string> playerListCopy = new List<string>();
                        foreach (CPlayerInfo player in this.playerList)
                        {
                            this.DebugWrite("Checking for teamswap access on " + player.SoldierName, 6);
                            if (!this.hasAccess(player.SoldierName, AdKat_CommandType.Teamswap))
                            {
                                this.DebugWrite("player doesnt have access, adding them to chance list", 6);
                                playerListCopy.Add(player.SoldierName);
                            }
                        }
                        if (playerListCopy.Count > 0)
                        {
                            int maxIndex = (playerListCopy.Count < this.playersToAutoWhitelist) ? (playerListCopy.Count) : (this.playersToAutoWhitelist);
                            this.DebugWrite("MaxIndex: " + maxIndex, 6);
                            for (int index = 0; index < maxIndex; index++)
                            {
                                string playerName = null;
                                int iterations = 0;
                                do
                                {
                                    playerName = playerListCopy[random.Next(0, playerListCopy.Count - 1)];
                                } while (this.teamswapRoundWhitelist.ContainsKey(playerName) && (iterations++ < 100));
                                this.teamswapRoundWhitelist.Add(playerName, false);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.ConsoleException(e.ToString());
            }
        }

        public Boolean handleRoundReport(AdKat_Record record)
        {
            Boolean acted = false;
            lock (reportsMutex)
            {
                //report ID will be housed in target_name
                if (this.round_reports.ContainsKey(record.target_name))
                {
                    //Get the reported record
                    AdKat_Record reportedRecord = this.round_reports[record.target_name];
                    //Remove it from the reports for this round
                    this.round_reports.Remove(record.target_name);
                    //Update it in the database
                    reportedRecord.command_action = AdKat_CommandType.ConfirmReport;
                    this.updateRecord(reportedRecord);
                    //Get target information
                    record.target_guid = reportedRecord.target_guid;
                    record.target_name = reportedRecord.target_name;
                    record.target_playerInfo = reportedRecord.target_playerInfo;
                    //Update record message if needed
                    //attempt to handle via pre-message ID
                    //record.record_message = this.getPreMessage(record.record_message, this.requirePreMessageUse);
                    this.DebugWrite("MESS: " + record.record_message, 5);
                    if (record.record_message == null || record.record_message.Length < this.requiredReasonLength)
                    {
                        record.record_message = reportedRecord.record_message;
                    }
                    //Inform the reporter that they helped the admins
                    this.sendMessageToSource(reportedRecord, "Your report has been acted on. Thank you.");
                    //Let the admin confirm the action before it is sent
                    this.confirmActionWithSource(record);
                    acted = true;
                }
            }
            return acted;
        }

        //Attempts to parse the command from a in-game string
        private AdKat_CommandType getCommand(string commandString)
        {
            AdKat_CommandType command = AdKat_CommandType.Default;
            this.AdKat_CommandStrings.TryGetValue(commandString.ToLower(), out command);
            return command;
        }

        //Attempts to parse the command from a database string
        private AdKat_CommandType getDBCommand(string commandString)
        {
            AdKat_CommandType command = AdKat_CommandType.Default;
            this.AdKat_RecordTypesInv.TryGetValue(commandString, out command);
            return command;
        }

        //replaces the message with a pre-message
        public string getPreMessage(string message, Boolean required)
        {
            if (message != null && message.Length > 0)
            {
                //Attempt to fill the message via pre-message ID
                int preMessageID = 0;
                DebugWrite("Raw preMessageID: " + message, 6);
                Boolean valid = Int32.TryParse(message, out preMessageID);
                if (valid && (preMessageID > 0) && (preMessageID <= this.preMessageList.Count))
                {
                    message = this.preMessageList[preMessageID - 1];
                }
                else if (required)
                {
                    return null;
                }
            }
            return message;
        }

        #endregion

        #region Action Methods

        private void queueRecordForActionHandling(AdKat_Record record)
        {
            this.DebugWrite("Preparing to queue record for action handling", 6);
            lock (unprocessedActionMutex)
            {
                this.unprocessedActionQueue.Enqueue(record);
                this.DebugWrite("Record queued for action handling", 6);
                this.actionHandlingHandle.Set();
            }
        }

        private void actionHandlingThreadLoop()
        {
            try
            {
                this.DebugWrite("ACTION: Starting Action Thread", 2);
                Thread.CurrentThread.Name = "action";
                Queue<AdKat_Record> unprocessedActions;
                while (true)
                {
                    this.DebugWrite("ACTION: Entering Action Thread Loop", 7);
                    if (!this.isEnabled)
                    {
                        this.DebugWrite("ACTION: Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                        break;
                    }

                    //Sleep for 10ms
                    Thread.Sleep(10);

                    //Handle Inbound Actions
                    if (this.unprocessedActionQueue.Count > 0)
                    {
                        lock (unprocessedActionMutex)
                        {
                            this.DebugWrite("ACTION: Inbound actions found. Grabbing.", 6);
                            //Grab all messages in the queue
                            unprocessedActions = new Queue<AdKat_Record>(this.unprocessedActionQueue.ToArray());
                            //Clear the queue for next run
                            this.unprocessedActionQueue.Clear();
                        }
                        //Loop through all records in order that they came in
                        while (unprocessedActions != null && unprocessedActions.Count > 0)
                        {
                            this.DebugWrite("ACTION: Preparing to Run Actions for record", 6);
                            //Dequeue the record
                            AdKat_Record record = unprocessedActions.Dequeue();
                            //Run the appropriate action
                            this.runAction(record);
                            //If more processing is needed, then perform it
                            this.queueRecordForProcessing(record);
                        }
                    }
                    else
                    {
                        this.DebugWrite("ACTION: No inbound actions. Waiting.", 6);
                        //Wait for new actions
                        this.actionHandlingHandle.Reset();
                        this.actionHandlingHandle.WaitOne(Timeout.Infinite);
                    }
                }
                this.DebugWrite("ACTION: Ending Action Handling Thread", 2);
            }
            catch (Exception e)
            {
                this.ConsoleException(e.ToString());
                if (typeof(ThreadAbortException).Equals(e.GetType()))
                {
                    this.DebugWrite("ACTION: Thread Exception", 4);
                    Thread.ResetAbort();
                    return;
                }
            }
        }

        private string runAction(AdKat_Record record)
        {
            string response = "No Message";
            //Perform Actions
            switch (record.command_type)
            {
                case AdKat_CommandType.MovePlayer:
                    response = this.moveTarget(record);
                    break;
                case AdKat_CommandType.ForceMovePlayer:
                    response = this.forceMoveTarget(record);
                    break;
                case AdKat_CommandType.Teamswap:
                    response = this.forceMoveTarget(record);
                    break;
                case AdKat_CommandType.KillPlayer:
                    response = this.killTarget(record, "");
                    break;
                case AdKat_CommandType.KickPlayer:
                    response = this.kickTarget(record, "");
                    break;
                case AdKat_CommandType.TempBanPlayer:
                    response = this.tempBanTarget(record, "");
                    break;
                case AdKat_CommandType.PermabanPlayer:
                    response = this.permaBanTarget(record, "");
                    break;
                case AdKat_CommandType.PunishPlayer:
                    response = this.punishTarget(record);
                    break;
                case AdKat_CommandType.ForgivePlayer:
                    response = this.forgiveTarget(record);
                    break;
                case AdKat_CommandType.MutePlayer:
                    response = this.muteTarget(record);
                    break;
                case AdKat_CommandType.RoundWhitelistPlayer:
                    response = this.roundWhitelistTarget(record);
                    break;
                case AdKat_CommandType.ReportPlayer:
                    response = this.reportTarget(record);
                    break;
                case AdKat_CommandType.CallAdmin:
                    response = this.callAdminOnTarget(record);
                    break;
                case AdKat_CommandType.RestartLevel:
                    response = this.restartLevel(record);
                    break;
                case AdKat_CommandType.NextLevel:
                    response = this.nextLevel(record);
                    break;
                case AdKat_CommandType.EndLevel:
                    response = this.endLevel(record);
                    break;
                case AdKat_CommandType.NukeServer:
                    response = this.nukeTarget(record);
                    break;
                case AdKat_CommandType.KickAll:
                    response = this.kickAllPlayers(record);
                    break;
                case AdKat_CommandType.AdminSay:
                    response = this.adminSay(record);
                    break;
                case AdKat_CommandType.PlayerSay:
                    response = this.playerSay(record);
                    break;
                case AdKat_CommandType.AdminYell:
                    response = this.adminYell(record);
                    break;
                case AdKat_CommandType.PlayerYell:
                    response = this.playerYell(record);
                    break;
                default:
                    response = "Command not recognized when running action.";
                    this.DebugWrite("Command not found in runAction", 5);
                    break;
            }
            return response;
        }

        public string moveTarget(AdKat_Record record)
        {
            this.queuePlayerForMove(record.target_playerInfo);
            this.playerSayMessage(record.target_name, "On your next death you will be moved to the opposing team.");
            return this.sendMessageToSource(record, record.target_name + " will be sent to teamswap on their next death.");
        }

        public string forceMoveTarget(AdKat_Record record)
        {
            this.DebugWrite("Entering forceMoveTarget", 6);

            string message = null;

            if (record.command_type == AdKat_CommandType.Teamswap)
            {
                if (this.hasAccess(record.source_name, AdKat_CommandType.Teamswap) || ((this.teamSwapTicketWindowHigh >= this.highestTicketCount) && (this.teamSwapTicketWindowLow <= this.lowestTicketCount)))
                {
                    message = "Calling Teamswap on self";
                    this.DebugWrite(message, 6);
                    this.queuePlayerForForceMove(record.target_playerInfo);
                }
                else
                {
                    message = "Player unable to teamswap";
                    this.DebugWrite(message, 6);
                    this.sendMessageToSource(record, "You cannot TeamSwap at this time. Game outside ticket window [" + this.teamSwapTicketWindowLow + ", " + this.teamSwapTicketWindowHigh + "].");
                }
            }
            else
            {
                message = "TeamSwap called on " + record.target_name;
                this.DebugWrite("Calling Teamswap on target", 6);
                this.sendMessageToSource(record, "" + record.target_name + " sent to teamswap.");
                this.queuePlayerForForceMove(record.target_playerInfo);
            }
            this.DebugWrite("Exiting forceMoveTarget", 6);

            return message;
        }

        public string killTarget(AdKat_Record record, string additionalMessage)
        {
            additionalMessage = (additionalMessage != null && additionalMessage.Length > 0) ? (" " + additionalMessage) : ("");
            //Perform actions
            if (!this.isTesting)
                ExecuteCommand("procon.protected.send", "admin.killPlayer", record.target_name);
            this.playerSayMessage(record.target_name, "Killed by admin for " + record.record_message + " " + additionalMessage);
            return this.sendMessageToSource(record, "You KILLED " + record.target_name + " for " + record.record_message + additionalMessage);
        }

        public string kickTarget(AdKat_Record record, string additionalMessage)
        {
            additionalMessage = (additionalMessage != null && additionalMessage.Length > 0) ? (" " + additionalMessage) : ("");
            string kickReason = record.source_name + " - " + record.record_message + additionalMessage;
            int cutLength = kickReason.Length - 80;
            if (cutLength > 0)
            {
                string cutReason = record.record_message.Substring(0, record.record_message.Length - cutLength);
                kickReason = record.source_name + " - " + cutReason + additionalMessage + ((this.useBanAppend) ? (" - " + this.banAppend) : (""));
            }
            this.DebugWrite("Kick Message: '" + kickReason + "'", 3);
            //Perform Actions
            if (!this.isTesting)
                ExecuteCommand("procon.protected.send", "admin.kickPlayer", record.target_name, kickReason);
            this.ExecuteCommand("procon.protected.send", "admin.say", "Player " + record.target_name + " was KICKED by admin for " + record.record_message + " " + additionalMessage, "all");
            return this.sendMessageToSource(record, "You KICKED " + record.target_name + " for " + record.record_message + ". " + additionalMessage);
        }

        public string tempBanTarget(AdKat_Record record, string additionalMessage)
        {
            Int32 seconds = record.record_durationMinutes * 60;
            additionalMessage = (additionalMessage != null && additionalMessage.Length > 0) ? (" " + additionalMessage) : ("");
            string banReason = record.source_name + " - " + record.record_message + additionalMessage + ((this.useBanAppend) ? (" - " + this.banAppend) : (""));
            int cutLength = banReason.Length - 80;
            if (cutLength > 0)
            {
                string cutReason = record.record_message.Substring(0, record.record_message.Length - cutLength);
                banReason = record.source_name + " - " + cutReason + additionalMessage + ((this.useBanAppend) ? (" - " + this.banAppend) : (""));
            }
            this.DebugWrite("Ban Message: '" + banReason + "'", 3);
            //Perform Actions
            if (!this.isTesting)
                switch (this.m_banMethod)
                {
                    case AdKat_BanType.FrostbiteName:
                        this.ExecuteCommand("procon.protected.send", "banList.add", "name", record.target_name, "seconds", seconds + "", banReason);
                        this.ExecuteCommand("procon.protected.send", "banList.save");
                        this.ExecuteCommand("procon.protected.send", "banList.list");
                        break;
                    case AdKat_BanType.FrostbiteEaGuid:
                        this.ExecuteCommand("procon.protected.send", "banList.add", "guid", record.target_guid, "seconds", seconds + "", banReason);
                        this.ExecuteCommand("procon.protected.send", "banList.save");
                        this.ExecuteCommand("procon.protected.send", "banList.list");
                        break;
                    case AdKat_BanType.PunkbusterGuid:
                        this.ExecuteCommand("procon.protected.send", "punkBuster.pb_sv_command", String.Format("pb_sv_kick \"{0}\" {1} \"{2}\"", record.target_name, record.record_durationMinutes.ToString(), "BC2! " + banReason));
                        break;
                    default:
                        this.ConsoleError("Error, ban type not selected.");
                        break;
                }
            this.ExecuteCommand("procon.protected.send", "admin.say", "Player " + record.target_name + " was BANNED by admin for " + record.record_message + " " + additionalMessage, "all");

            return this.sendMessageToSource(record, "You TEMP BANNED " + record.target_name + " for " + record.record_durationMinutes + " minutes. " + additionalMessage);
        }

        public string permaBanTarget(AdKat_Record record, string additionalMessage)
        {
            additionalMessage = (additionalMessage != null && additionalMessage.Length > 0) ? (" " + additionalMessage) : ("");
            string banReason = record.source_name + " - " + record.record_message + additionalMessage + ((this.useBanAppend) ? (" - " + this.banAppend) : (""));
            int cutLength = banReason.Length - 80;
            if (cutLength > 0)
            {
                string cutReason = record.record_message.Substring(0, record.record_message.Length - cutLength);
                banReason = record.source_name + " - " + cutReason + additionalMessage + ((this.useBanAppend) ? (" - " + this.banAppend) : (""));
            }
            this.DebugWrite("Ban Message: '" + banReason + "'", 6);
            //Perform Actions
            if (!this.isTesting)
                switch (this.m_banMethod)
                {
                    case AdKat_BanType.FrostbiteName:
                        this.ExecuteCommand("procon.protected.send", "banList.add", "name", record.target_name, "perm", banReason);
                        this.ExecuteCommand("procon.protected.send", "banList.save");
                        this.ExecuteCommand("procon.protected.send", "banList.list");
                        break;
                    case AdKat_BanType.FrostbiteEaGuid:
                        this.ExecuteCommand("procon.protected.send", "banList.add", "guid", record.target_guid, "perm", banReason);
                        this.ExecuteCommand("procon.protected.send", "banList.save");
                        this.ExecuteCommand("procon.protected.send", "banList.list");
                        break;
                    case AdKat_BanType.PunkbusterGuid:
                        this.ExecuteCommand("procon.protected.send", "punkBuster.pb_sv_command", String.Format("pb_sv_ban \"{0}\" \"{1}\"", record.target_name, "BC2! " + banReason));
                        break;
                    default:
                        break;
                }
            this.ExecuteCommand("procon.protected.send", "admin.say", "Player " + record.target_name + " was BANNED by admin for " + record.record_message + additionalMessage, "all");

            return this.sendMessageToSource(record, "You PERMA BANNED " + record.target_name + "! Get a vet admin NOW!" + additionalMessage);
        }

        public string punishTarget(AdKat_Record record)
        {
            this.DebugWrite("punishing target", 6);
            string message = "ERROR";
            //Get number of points the player from server
            int points = this.fetchPoints(record.target_guid);
            //Get the proper action to take for player punishment
            string action = "noaction";
            if (points > (this.punishmentHierarchy.Length - 1))
            {
                action = this.punishmentHierarchy[this.punishmentHierarchy.Length - 1];
            }
            else if (points > 0)
            {
                action = this.punishmentHierarchy[points - 1];
            }
            //Set additional message
            string additionalMessage = "[" + ((record.isIRO) ? ("IRO ") : ("")) + points + "pts]";

            Boolean isLowPop = this.onlyKillOnLowPop && (this.playerList.Count < this.lowPopPlayerCount);
            Boolean IROOverride = record.isIRO && this.IROOverridesLowPop;

            //Call correct action
            if (action.Equals("kill") || (isLowPop && !IROOverride))
            {
                record.command_action = AdKat_CommandType.KillPlayer;
                message = this.killTarget(record, additionalMessage);
            }
            else if (action.Equals("kick"))
            {
                record.command_action = AdKat_CommandType.KickPlayer;
                message = this.kickTarget(record, additionalMessage);
            }
            else if (action.Equals("tban60"))
            {
                record.record_durationMinutes = 60;
                record.command_action = AdKat_CommandType.TempBanPlayer;
                message = this.tempBanTarget(record, additionalMessage);
            }
            else if (action.Equals("tbanday"))
            {
                record.record_durationMinutes = 1440;
                record.command_action = AdKat_CommandType.TempBanPlayer;
                message = this.tempBanTarget(record, additionalMessage);
            }
            else if (action.Equals("tbanweek"))
            {
                record.record_durationMinutes = 10080;
                record.command_action = AdKat_CommandType.TempBanPlayer;
                message = this.tempBanTarget(record, additionalMessage);
            }
            else if (action.Equals("tban2weeks"))
            {
                record.record_durationMinutes = 20160;
                record.command_action = AdKat_CommandType.TempBanPlayer;
                message = this.tempBanTarget(record, additionalMessage);
            }
            else if (action.Equals("tbanmonth"))
            {
                record.record_durationMinutes = 43200;
                record.command_action = AdKat_CommandType.TempBanPlayer;
                message = this.tempBanTarget(record, additionalMessage);
            }
            else if (action.Equals("ban"))
            {
                record.command_action = AdKat_CommandType.PermabanPlayer;
                message = this.permaBanTarget(record, additionalMessage);
            }
            else
            {
                record.command_action = AdKat_CommandType.KillPlayer;
                this.killTarget(record, additionalMessage);
                message = "Punish options are set incorrectly. Inform plugin setting manager.";
                this.ConsoleError(message);
            }
            return message;
        }

        public string forgiveTarget(AdKat_Record record)
        {
            int points = this.fetchPoints(record.target_guid);
            this.playerSayMessage(record.target_name, "Forgiven 1 infraction point. You now have " + points + " point(s) against you.");
            return this.sendMessageToSource(record, "Forgive Logged for " + record.target_name + ". They now have " + points + " infraction points.");
        }

        public string muteTarget(AdKat_Record record)
        {
            string message = null;
            if (!this.hasAccess(record.target_name, AdKat_CommandType.MutePlayer))
            {
                if (!this.round_mutedPlayers.ContainsKey(record.target_name))
                {
                    this.round_mutedPlayers.Add(record.target_name, 0);
                    this.playerSayMessage(record.target_name, this.mutedPlayerMuteMessage);
                    message = this.sendMessageToSource(record, record.target_name + " has been muted for this round.");
                }
                else
                {
                    message = this.sendMessageToSource(record, record.target_name + " already muted for this round.");
                }
            }
            else
            {
                message = this.sendMessageToSource(record, "You can't mute an admin, dimwit.");
            }
            return this.sendMessageToSource(record, message);
        }

        public string roundWhitelistTarget(AdKat_Record record)
        {
            string message = null;
            try
            {
                if (!this.teamswapRoundWhitelist.ContainsKey(record.target_name))
                {
                    if (this.teamswapRoundWhitelist.Count < this.playersToAutoWhitelist + 2)
                    {
                        this.teamswapRoundWhitelist.Add(record.target_name, false);
                        string command = this.m_strTeamswapCommand.TrimEnd("|log".ToCharArray());
                        message = record.target_name + " can now use @" + command + " for this round.";
                    }
                    else
                    {
                        message = "Cannot whitelist more than two extra people per round.";
                    }
                }
                else
                {
                    message = record.target_name + " is already in this round's teamswap whitelist.";
                }
            }
            catch (Exception e)
            {
                message = e.ToString();
                this.ConsoleException(e.ToString());
            }
            return this.sendMessageToSource(record, message);
        }

        public string reportTarget(AdKat_Record record)
        {
            Random random = new Random();
            int reportID;
            do
            {
                reportID = random.Next(100, 999);
            } while (round_reports.ContainsKey(reportID + ""));
            this.round_reports.Add(reportID + "", record);
            string adminAssistantIdentifier = (this.adminAssistantCache.ContainsKey(record.source_name)) ? ("[AA]") : ("");
            foreach (String admin_name in this.playerAccessCache.Keys)
            {
                if (this.playerAccessCache[admin_name] <= 4)
                {
                    this.playerSayMessage(admin_name, "REPORT " + adminAssistantIdentifier + "[" + reportID + "]: " + record.source_name + " reported " + record.target_name + " for " + record.record_message);
                }
            }
            return this.sendMessageToSource(record, "REPORT [" + reportID + "] sent. " + record.target_name + " for " + record.record_message);
        }

        public string callAdminOnTarget(AdKat_Record record)
        {
            Random random = new Random();
            int reportID;
            do
            {
                reportID = random.Next(100, 999);
            } while (round_reports.ContainsKey(reportID + ""));
            this.round_reports.Add(reportID + "", record);
            string adminAssistantIdentifier = (this.adminAssistantCache.ContainsKey(record.source_name)) ? ("[AA]") : ("");
            foreach (String admin_name in this.playerAccessCache.Keys)
            {
                if (this.playerAccessCache[admin_name] <= 4)
                {
                    this.playerSayMessage(admin_name, "ADMIN CALL " + adminAssistantIdentifier + "[" + reportID + "]: " + record.source_name + " called admin on " + record.target_name + " for " + record.record_message);
                }
            }
            return this.sendMessageToSource(record, "ADMIN CALL [" + reportID + "] sent. " + record.target_name + " for " + record.record_message);
        }

        public string restartLevel(AdKat_Record record)
        {
            string message = "No Message";
            this.ExecuteCommand("procon.protected.send", "mapList.restartRound");
            message = "Round Restarted.";
            return message;
        }

        public string nextLevel(AdKat_Record record)
        {
            string message = "No Message";
            this.ExecuteCommand("procon.protected.send", "mapList.runNextRound");
            message = "Next round has been run.";
            return message;
        }

        public string endLevel(AdKat_Record record)
        {
            string message = "No Message";
            this.ExecuteCommand("procon.protected.send", "mapList.endRound", record.target_guid);
            message = "Ended round with " + record.target_name + " as winner.";
            return message;
        }

        public string nukeTarget(AdKat_Record record)
        {
            //Perform actions
            string message = "No Message";
            foreach (CPlayerInfo player in this.playerList)
            {
                if ((record.target_name == "US Team" && player.TeamID == USTeamId) ||
                    (record.target_name == "RU Team" && player.TeamID == RUTeamId) ||
                    (record.target_name == "Server"))
                {
                    ExecuteCommand("procon.protected.send", "admin.killPlayer", player.SoldierName);
                    this.playerSayMessage(record.target_name, "Killed by admin for: " + record.record_message);
                }
            }
            message = "You NUKED " + record.target_name + " for " + record.record_message + ".";
            this.playerSayMessage(record.source_name, message);
            return message;
        }

        public string kickAllPlayers(AdKat_Record record)
        {
            //Perform Actions
            string message = "No Message";
            foreach (CPlayerInfo player in this.playerList)
            {
                if (!(this.playerAccessCache.ContainsKey(player.SoldierName) && this.playerAccessCache[player.SoldierName] < 5))
                {
                    ExecuteCommand("procon.protected.send", "admin.kickPlayer", player.SoldierName, "(" + record.source_name + ") " + record.record_message);
                    this.playerSayMessage(record.target_name, "Killed by admin for: " + record.record_message);
                }
            }
            this.ExecuteCommand("procon.protected.send", "admin.say", "All players with access class 5 or lower have been kicked.", "all");

            message = "You KICKED EVERYONE for " + record.record_message + ". ";
            this.playerSayMessage(record.source_name, message);
            return message;
        }

        public string adminSay(AdKat_Record record)
        {
            string message = "No Message";
            this.ExecuteCommand("procon.protected.send", "admin.say", record.record_message, "all");
            message = "Server has been told '" + record.record_message + "'";
            return message;
        }

        public string adminYell(AdKat_Record record)
        {
            string message = "No Message";
            this.ExecuteCommand("procon.protected.send", "admin.yell", record.record_message, this.m_strShowMessageLength, "all");
            message = "Server has been told '" + record.record_message + "'";
            return message;
        }

        public string playerSay(AdKat_Record record)
        {
            string message = "No Message";
            this.playerSayMessage(record.target_name, record.record_message);
            message = record.target_name + " has been told '" + record.record_message + "'";
            return message;
        }

        public string playerYell(AdKat_Record record)
        {
            string message = "No Message";
            this.ExecuteCommand("procon.protected.send", "admin.yell", record.record_message, this.m_strShowMessageLength, "player", record.target_name);
            message = record.target_name + " has been told '" + record.record_message + "'";
            return message;
        }

        #endregion

        #region Player Access

        private void queuePlayerForAccessUpdate(String player_name, int access_level)
        {
            this.DebugWrite("Preparing to queue player for access update", 6);
            lock (playerAccessMutex)
            {
                this.playerAccessUpdateQueue.Enqueue(new KeyValuePair<string, int>(player_name, access_level));
                this.DebugWrite("Player queued for access update", 6);
                this.dbCommHandle.Set();
            }
        }

        private void queuePlayerForAccessRemoval(String player_name)
        {
            this.DebugWrite("Preparing to queue player for access removal", 6);
            lock (playerAccessMutex)
            {
                this.playerAccessRemovalQueue.Enqueue(player_name);
                this.DebugWrite("Player queued for access removal", 6);
                this.dbCommHandle.Set();
            }
        }

        private Boolean hasAccess(String player_name, AdKat_CommandType command)
        {
            Boolean access = false;
            //Check if the player can access the desired command
            if (this.getAccessLevel(player_name) <= this.AdKat_CommandAccessRank[command])
            {
                access = true;
            }
            return access;
        }

        private int getAccessLevel(String player_name)
        {
            int access_level = 6;
            //Get access level of player
            if (this.playerAccessCache.ContainsKey(player_name))
            {
                access_level = this.playerAccessCache[player_name];
            }
            else if
                (!this.requireTeamswapWhitelist ||
                this.teamswapRoundWhitelist.ContainsKey(player_name) ||
                (this.enableAdminAssistants && this.adminAssistantCache.ContainsKey(player_name)))
            {
                access_level = this.AdKat_CommandAccessRank[AdKat_CommandType.Teamswap];
            }
            return access_level;
        }

        #endregion

        #region Database Methods

        private void databaseCommThreadLoop()
        {
            try
            {
                this.DebugWrite("DBCOMM: Starting Database Comm Thread", 2);
                Thread.CurrentThread.Name = "databasecomm";

                Queue<AdKat_Record> inboundRecords;
                Queue<KeyValuePair<String, int>> inboundAccessUpdates;
                Queue<String> inboundAccessRemoval;
                while (true)
                {
                    this.DebugWrite("DBCOMM: Entering Database Comm Thread Loop", 7);
                    if (!this.isEnabled)
                    {
                        this.DebugWrite("DBCOMM: Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                        break;
                    }

                    //Sleep for 10ms
                    Thread.Sleep(10);

                    //Check if database connection settings have changed
                    if (this.dbSettingsChanged)
                    {
                        this.DebugWrite("DBCOMM: DB Settings have changed, calling test.", 6);
                        if (this.testDatabaseConnection())
                        {
                            this.DebugWrite("DBCOMM: Database Connection Good. Continuing Thread.", 6);
                        }
                        else
                        {
                            //Reset the handle
                            this.dbCommHandle.Reset();
                            //The database connection failed, wait for settings to change again
                            this.dbCommHandle.WaitOne(Timeout.Infinite);
                            this.DebugWrite("DBCOMM: Settings changed, attempting new connection.", 3);
                            continue;
                        }
                    }

                    //Database access is successful, sync all bans
                    this.ExecuteCommand("procon.protected.send", "banList.list");

                    //Check for new actions from the database at given interval
                    if (this.fetchActionsFromDB && (DateTime.Now > this.lastDBActionFetch.AddSeconds(this.dbActionFrequency)))
                    {
                        this.runActionsFromDB();
                    }
                    else
                    {
                        this.DebugWrite("DBCOMM: Skipping DB action fetch", 7);
                    }

                    //Handle access updates
                    if (this.playerAccessUpdateQueue.Count > 0 || this.playerAccessRemovalQueue.Count > 0)
                    {
                        this.DebugWrite("DBCOMM: Preparing to lock inbound access queues to retrive access changes", 7);
                        lock (playerAccessMutex)
                        {
                            this.DebugWrite("DBCOMM: Inbound access changes found. Grabbing.", 6);
                            //Grab all in the queue
                            inboundAccessUpdates = new Queue<KeyValuePair<string, int>>(this.playerAccessUpdateQueue.ToArray());
                            inboundAccessRemoval = new Queue<String>(this.playerAccessRemovalQueue.ToArray());
                            //Clear the queue for next run
                            this.playerAccessUpdateQueue.Clear();
                            this.playerAccessRemovalQueue.Clear();
                        }
                        //Loop through all records in order that they came in
                        while (inboundAccessUpdates != null && inboundAccessUpdates.Count > 0)
                        {
                            KeyValuePair<String, int> playerAccess = inboundAccessUpdates.Dequeue();
                            this.updatePlayerAccess(playerAccess.Key, playerAccess.Value);
                        }
                        //Loop through all records in order that they came in
                        while (inboundAccessRemoval != null && inboundAccessRemoval.Count > 0)
                        {
                            String playerName = inboundAccessRemoval.Dequeue();
                            this.removePlayerAccess(playerName);
                        }
                        this.fetchAccessList();
                        //Update the setting page with new information
                        this.updateSettingPage();
                    }
                    else if (DateTime.Now > this.lastDBAccessFetch.AddMinutes(this.dbAccessFetchFrequency))
                    {
                        //Handle access updates directly from the database
                        this.fetchAccessList();
                        //Update the setting page with new information
                        this.updateSettingPage();
                    }
                    else
                    {
                        this.DebugWrite("DBCOMM: No inbound access changes.", 7);
                    }

                    //Handle Inbound Records
                    if (this.unprocessedRecordQueue.Count > 0)
                    {
                        this.DebugWrite("DBCOMM: Preparing to lock inbound record queue to retrive new records", 7);
                        lock (unprocessedRecordMutex)
                        {
                            this.DebugWrite("DBCOMM: Inbound records found. Grabbing.", 6);
                            //Grab all messages in the queue
                            inboundRecords = new Queue<AdKat_Record>(this.unprocessedRecordQueue.ToArray());
                            //Clear the queue for next run
                            this.unprocessedRecordQueue.Clear();
                        }
                        //Loop through all records in order that they came in
                        while (inboundRecords != null && inboundRecords.Count > 0)
                        {
                            AdKat_Record record = inboundRecords.Dequeue();

                            //Only run action if the record needs action
                            if (this.handleRecordUpload(record))
                            {
                                //Action is only called after initial upload, not after update
                                this.DebugWrite("DBCOMM: Upload success. Attempting to add to action queue.", 6);
                                this.queueRecordForActionHandling(record);
                            }
                            else
                            {
                                this.DebugWrite("DBCOMM: Record does not need action handling.", 6);
                            }
                        }
                    }
                    else
                    {
                        this.DebugWrite("DBCOMM: No unprocessed records. Waiting for input", 7);
                        this.dbCommHandle.Reset();
                        if (!this.fetchActionsFromDB)
                        {
                            //Maximum wait time is DB access fetch time
                            this.dbCommHandle.WaitOne(this.dbAccessFetchFrequency * 1000);
                        }
                        else
                        {
                            //If waiting on DB input, the maximum time we can wait is "db action frequency"
                            this.dbCommHandle.WaitOne(this.dbActionFrequency * 1000);
                        }
                    }
                }
                this.DebugWrite("DBCOMM: Ending Database Comm Thread", 2);
            }
            catch (Exception e)
            {
                this.ConsoleException(e.ToString());
                if (typeof(ThreadAbortException).Equals(e.GetType()))
                {
                    this.DebugWrite("Thread Exception", 4);
                    Thread.ResetAbort();
                    return;
                }
            }
        }

        #region Connection and Setup

        private Boolean connectionCapable()
        {
            if ((this.mySqlDatabaseName != null && this.mySqlDatabaseName.Length > 0) &&
                (this.mySqlHostname != null && this.mySqlHostname.Length > 0) &&
                (this.mySqlPassword != null && this.mySqlPassword.Length > 0) &&
                (this.mySqlPort != null && this.mySqlPort.Length > 0) &&
                (this.mySqlUsername != null && this.mySqlUsername.Length > 0))
            {
                this.DebugWrite("MySql Connection capable. All variables in place.", 6);
                return true;
            }
            return false;
        }

        private MySqlConnection getDatabaseConnection()
        {
            if (this.connectionCapable())
            {
                MySqlConnection conn = new MySqlConnection(this.PrepareMySqlConnectionString());
                conn.Open();
                return conn;
            }
            else
            {
                this.ConsoleError("Attempted to connect to database without all variables in place");
                return null;
            }
        }

        private Boolean testDatabaseConnection()
        {
            Boolean databaseValid = false;
            DebugWrite("testDatabaseConnection starting!", 6);
            if (this.connectionCapable())
            {
                try
                {
                    Boolean success = false;
                    //Prepare the connection string and create the connection object
                    using (MySqlConnection databaseConnection = this.getDatabaseConnection())
                    {
                        this.ConsoleWrite("Attempting database connection.");
                        //Attempt a ping through the connection
                        if (databaseConnection.Ping())
                        {
                            //Connection good
                            this.ConsoleWrite("Database connection SUCCESS.");
                            success = true;
                        }
                        else
                        {
                            //Connection poor
                            this.ConsoleError("Database connection FAILED ping test.");
                        }
                    } //databaseConnection gets closed here
                    if (success)
                    {
                        //Make sure database structure is good
                        if (confirmDatabaseSetup())
                        {
                            //If the structure is good, fetch all access lists
                            this.fetchAccessList();
                            databaseValid = true;
                        }
                    }
                }
                catch (Exception e)
                {
                    //Invalid credentials or no connection to database
                    this.ConsoleError("Database connection FAILED with EXCEPTION. Bad credentials, invalid hostname, or invalid port.");
                }
            }
            else
            {
                this.ConsoleError("Not DB connection capable yet, complete SQL connection variables.");
            }
            //clear setting change monitor
            this.dbSettingsChanged = false;
            DebugWrite("testDatabaseConnection finished!", 6);

            return databaseValid;
        }

        private Boolean confirmDatabaseSetup()
        {
            this.DebugWrite("Confirming Database Structure.", 3);
            try
            {
                Boolean confirmed = true;
                if (!this.confirmTable("adkat_records"))
                {
                    this.ConsoleError("Main Record table not present in the database.");
                    this.runDBSetupScript();
                    if (!this.confirmTable("adkat_records"))
                    {
                        this.ConsoleError("After running setup script main record table still not present.");
                        confirmed = false;
                    }
                }
                if (!this.confirmTable("adkat_accesslist"))
                {
                    ConsoleError("Access Table not present in the database.");
                    this.runDBSetupScript();
                    if (!this.confirmTable("adkat_accesslist"))
                    {
                        this.ConsoleError("After running setup script access table still not present.");
                        confirmed = false;
                    }
                }
                if (!this.confirmTable("adkat_banlist"))
                {
                    ConsoleError("Ban List not present in the database.");
                    this.runDBSetupScript();
                    if (!this.confirmTable("adkat_accesslist"))
                    {
                        this.ConsoleError("After running setup script banlist still not present.");
                        confirmed = false;
                    }
                }
                //These not needed right now, but will be later
                /*if (!this.confirmTable("adkat_playerlist"))
                {
                    ConsoleError("adkat_playerlist not present in the database. AdKats will not function properly.");
                    confirmed = false;
                }
                if (!this.confirmTable("adkat_playerpoints"))
                {
                    ConsoleError("adkat_playerpoints not present in the database. AdKats will not function properly.");
                    confirmed = false;
                }*/
                if (confirmed)
                {
                    this.DebugWrite("SUCCESS. Database confirmed functional for AdKats use.", 3);
                }
                else
                {
                    this.ConsoleError("Database structure errors detected, not set up for AdKats use.");
                }
                return confirmed;
            }
            catch (Exception e)
            {
                ConsoleException("ERROR in helper_confirmDatabaseSetup: " + e.ToString());
                return false;
            }
        }

        private void runDBSetupScript()
        {
            try
            {
                ConsoleWrite("Running database setup script. You will not lose any data.");
                using (MySqlConnection databaseConnection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = databaseConnection.CreateCommand())
                    {
                        WebClient downloader = new WebClient();
                        //Set the insert command structure
                        if (this.isRelease)
                        {
                            command.CommandText = downloader.DownloadString("https://raw.github.com/ColColonCleaner/AdKats/master/adkats.sql");
                        }
                        else
                        {
                            command.CommandText = downloader.DownloadString("https://raw.github.com/ColColonCleaner/AdKats/dev/adkats.sql");
                        }
                        try
                        {
                            //Attempt to execute the query
                            if (command.ExecuteNonQuery() >= 0)
                            {
                                ConsoleWrite("Setup script successful, your database is now prepared for use by AdKats " + this.GetPluginVersion());
                            }
                        }
                        catch (Exception e)
                        {
                            ConsoleException("Your database did not accept the script. Does your account have access to table creation? AdKats will not function properly. Exception: " + e.ToString());
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.ConsoleException("ERROR when setting up DB, you might not have connection to github: " + e.ToString());
            }
        }

        private Boolean confirmTable(string tablename)
        {
            try
            {
                using (MySqlConnection databaseConnection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = databaseConnection.CreateCommand())
                    {
                        command.CommandText = "SHOW TABLES LIKE '" + tablename + "'";
                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return true;
                            }
                            else
                            {
                                return false;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.ConsoleException("ERROR in helper_confirmTable: " + e.ToString());
                return false;
            }
        }

        private string PrepareMySqlConnectionString()
        {
            return "Server=" + mySqlHostname + ";Port=" + mySqlPort + ";Database=" + this.mySqlDatabaseName + ";Uid=" + mySqlUsername + ";Pwd=" + mySqlPassword + ";";
        }

        #endregion

        #region Data
        /*
         * This method handles uploading of records and calling their action methods
         * Will only upload a record if upload setting for that command is true, or if uploading is required
         */
        private Boolean handleRecordUpload(AdKat_Record record)
        {
            this.DebugWrite("DBCOMM: Entering handle record upload", 6);
            Boolean recordNeedsAction = true;
            //Check whether to call update, or full upload
            if (record.record_id != -1)
            {
                recordNeedsAction = false;
                //Record already has a record ID, it can only be updated
                if (this.AdKat_LoggingSettings[record.command_type])
                {
                    this.DebugWrite("DBCOMM: UPDATING record for " + record.command_type, 6);
                    //Update Record
                    this.updateRecord(record);
                }
                else
                {
                    this.DebugWrite("DBCOMM: Skipping record UPDATE for " + record.command_type, 6);
                }
            }
            else
            {
                this.DebugWrite("DBCOMM: Record needs full upload, checking.", 6);
                //No record ID. Perform full upload
                switch (record.command_type)
                {
                    case AdKat_CommandType.PunishPlayer:
                        //Upload for punish is required
                        //Check if the punish will be double counted
                        if (this.isDoubleCounted(record))
                        {
                            this.DebugWrite("DBCOMM: Punish is double counted.", 6);
                            //Check if player is on timeout
                            if (this.canPunish(record))
                            {
                                //IRO - Immediate Repeat Offence
                                record.isIRO = true;
                                string IROAppend = " [IRO]";
                                record.record_message += IROAppend;
                                //Upload record twice
                                this.DebugWrite("DBCOMM: UPLOADING IRO Punish", 6);
                                this.uploadRecord(record);
                                this.uploadRecord(record);
                                //Trim off the IRO again
                                record.record_message = record.record_message.TrimEnd(IROAppend.ToCharArray());
                            }
                            else
                            {
                                this.sendMessageToSource(record, record.target_name + " already punished in the last 20 seconds.");
                                recordNeedsAction = false;
                            }
                        }
                        else
                        {
                            //Upload record once
                            this.DebugWrite("DBCOMM: UPLOADING Punish", 6);
                            this.uploadRecord(record);
                        }
                        break;
                    case AdKat_CommandType.ForgivePlayer:
                        //Upload for forgive is required
                        //No restriction on forgives/minute
                        this.DebugWrite("DBCOMM: UPLOADING Forgive", 6);
                        this.uploadRecord(record);
                        break;
                    default:
                        if (this.AdKat_LoggingSettings[record.command_type])
                        {
                            this.DebugWrite("UPLOADING record for " + record.command_type, 6);
                            //Upload Record
                            this.uploadRecord(record);
                        }
                        else
                        {
                            this.DebugWrite("Skipping record UPLOAD for " + record.command_type, 6);
                        }
                        break;
                }
            }
            return recordNeedsAction;
        }

        private void uploadRecord(AdKat_Record record)
        {
            DebugWrite("postRecord starting!", 6);

            Boolean success = false;
            try
            {
                using (MySqlConnection databaseConnection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = databaseConnection.CreateCommand())
                    {
                        //Set the insert command structure
                        command.CommandText = "INSERT INTO `" + this.mySqlDatabaseName + "`.`adkat_records` (`server_id`, `server_ip`, `command_type`, `command_action`, `record_durationMinutes`,`target_guid`, `target_name`, `source_name`, `record_message`, `adkats_read`) VALUES (@server_id, @server_ip, @command_type, @command_action, @record_durationMinutes, @target_guid, @target_name, @source_name, @record_message, @adkats_read)";
                        //Fill the command
                        //Convert enum to DB string
                        string type = this.AdKat_RecordTypes[record.command_type];
                        string action = null;
                        if (record.command_action != AdKat_CommandType.Default)
                        {
                            action = this.AdKat_RecordTypes[record.command_type];
                        }
                        else
                        {
                            action = type;
                        }
                        //Set values
                        command.Parameters.AddWithValue("@server_id", record.server_id);
                        command.Parameters.AddWithValue("@server_ip", record.server_ip);
                        command.Parameters.AddWithValue("@command_type", type);
                        command.Parameters.AddWithValue("@command_action", action);
                        command.Parameters.AddWithValue("@record_durationMinutes", record.record_durationMinutes);
                        command.Parameters.AddWithValue("@target_guid", record.target_guid);
                        command.Parameters.AddWithValue("@target_name", record.target_name);
                        command.Parameters.AddWithValue("@source_name", record.source_name);
                        command.Parameters.AddWithValue("@record_message", record.record_message);
                        command.Parameters.AddWithValue("@adkats_read", 'Y');
                        //Attempt to execute the query
                        if (command.ExecuteNonQuery() > 0)
                        {
                            success = true;
                            record.record_id = command.LastInsertedId;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                ConsoleException(e.ToString());
            }

            string temp = this.AdKat_RecordTypes[record.command_type];

            if (success)
            {
                DebugWrite(temp + " log for player " + record.target_name + " by " + record.source_name + " SUCCESSFUL!", 3);
            }
            else
            {
                ConsoleError(temp + " log for player '" + record.target_name + " by " + record.source_name + " FAILED!");
            }

            DebugWrite("postRecord finished!", 6);
        }

        //Only command_action, record_durationMinutes, and adkats_read are allowed to be updated
        private void updateRecord(AdKat_Record record)
        {
            DebugWrite("updateRecord starting!", 6);

            Boolean success = false;
            try
            {
                using (MySqlConnection databaseConnection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = databaseConnection.CreateCommand())
                    {
                        //Convert enum to DB string
                        string action;
                        if (record.command_action == AdKat_CommandType.Default)
                        {
                            action = this.AdKat_RecordTypes[record.command_type];
                        }
                        else
                        {
                            action = this.AdKat_RecordTypes[record.command_action];
                        }
                        //Set values
                        command.CommandText = "UPDATE `" + this.mySqlDatabaseName + "`.`adkat_records` SET `command_action` = '" + action + "', `record_durationMinutes` = " + record.record_durationMinutes + ", `adkats_read` = 'Y' WHERE `record_id` = " + record.record_id;
                        //Attempt to execute the query
                        if (command.ExecuteNonQuery() > 0)
                        {
                            success = true;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                ConsoleException(e.ToString());
            }

            string temp = this.AdKat_RecordTypes[record.command_action];

            if (success)
            {
                DebugWrite(temp + " UPDATE for player " + record.target_name + " by " + record.source_name + " SUCCESSFUL!", 3);
            }
            else
            {
                ConsoleError(temp + " UPDATE for player '" + record.target_name + " by " + record.source_name + " FAILED!");
            }

            if (record.source_name == this.debugSoldierName)
            {
                TimeSpan commandTime = DateTime.Now.Subtract(this.commandStartTime);
                this.playerSayMessage(this.debugSoldierName, "Duration: " + commandTime.TotalMilliseconds + "ms");
            }

            DebugWrite("updateRecord finished!", 6);
        }

        private void removePlayerAccess(string player_name)
        {
            DebugWrite("removePlayerAccess starting!", 6);
            if (!this.playerAccessCache.ContainsKey(player_name))
            {
                this.ConsoleError("Player doesn't have any access to remove.");
                return;
            }
            try
            {
                using (MySqlConnection databaseConnection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = databaseConnection.CreateCommand())
                    {
                        //Set the insert command structure
                        command.CommandText = "DELETE FROM `" + this.mySqlDatabaseName + "`.`adkat_accesslist` WHERE `player_name` = @player_name";
                        //Set values
                        command.Parameters.AddWithValue("@player_name", player_name);
                        //Attempt to execute the query
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception e)
            {
                ConsoleException(e.ToString());
            }

            DebugWrite("removePlayerAccess finished!", 6);
        }

        private void updatePlayerAccess(string player_name, int desiredAccessLevel)
        {
            DebugWrite("updatePlayerAccess starting!", 6);
            if (desiredAccessLevel < 0 || desiredAccessLevel > 6)
            {
                this.ConsoleError("Desired Access Level for " + player_name + " was invalid.");
                return;
            }
            try
            {
                using (MySqlConnection databaseConnection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = databaseConnection.CreateCommand())
                    {
                        //Set the insert command structure
                        command.CommandText = "INSERT INTO `" + this.mySqlDatabaseName + "`.`adkat_accesslist` (`player_name`, `access_level`) VALUES (@player_name, @access_level) ON DUPLICATE KEY UPDATE `access_level` = @access_level";
                        //Set values
                        command.Parameters.AddWithValue("@player_name", player_name);
                        command.Parameters.AddWithValue("@access_level", desiredAccessLevel);
                        //Attempt to execute the query
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception e)
            {
                ConsoleException(e.ToString());
            }

            DebugWrite("updatePlayerAccess finished!", 6);
        }

        private Boolean pushBan(AdKat_Ban aBan)
        {
            DebugWrite("checkBan starting!", 6);

            //Only this server is synced
            aBan.ban_sync = new List<string>();
            aBan.ban_sync.Clear();
            aBan.ban_sync.Add("*" + this.server_id + "*");

            AdKat_Ban matchingDBBan = this.getMatchingDBBan(aBan);

            Boolean success = false;
            if (aBan == null)
            {
                this.ConsoleError("Ban invalid in checkBan.");
            }
            else
            {
                try
                {
                    using (MySqlConnection databaseConnection = this.getDatabaseConnection())
                    {
                        using (MySqlCommand command = databaseConnection.CreateCommand())
                        {
                            //If no existing ban was found in the database, add it
                            if (matchingDBBan == null)
                            {
                                command.CommandText = @"
                                INSERT INTO 
                                `" + this.mySqlDatabaseName + @"`.`adkat_banlist` 
                                (
	                                `admin_name`, 
	                                `player_name`, 
	                                `player_ip`, 
	                                `player_guid`, 
	                                `ban_status`, 
	                                `ban_reason`, 
	                                `ban_notes`, 
	                                `ban_sync`, 
	                                `ban_startTime`, 
	                                `ban_endTime`
                                ) 
                                VALUES 
                                (
	                                @admin_name, 
	                                @player_name, 
	                                @player_ip, 
	                                @player_guid, 
	                                @ban_reason, 
	                                @ban_notes, 
	                                @ban_sync, 
	                                NOW(), 
	                                DATE_ADD(NOW(), INTERVAL " + aBan.ban_durationMinutes + @" MINUTE)
                                )";

                                command.Parameters.AddWithValue("@admin_name", aBan.admin_name);
                                command.Parameters.AddWithValue("@player_name", aBan.player_name);
                                command.Parameters.AddWithValue("@player_ip", aBan.player_ip);
                                command.Parameters.AddWithValue("@player_guid", aBan.player_guid);
                                command.Parameters.AddWithValue("@ban_reason", aBan.ban_reason);
                                command.Parameters.AddWithValue("@ban_notes", aBan.ban_notes);
                                command.Parameters.AddWithValue("@ban_sync", AdKats.EncodeStringArray(aBan.ban_sync.ToArray()));
                            }
                            else
                            {
                                string query = @"UPDATE 
                                `" + this.mySqlDatabaseName + @"`.`adkat_banlist` 
                                SET 
                                `admin_name` = " + aBan.admin_name + @", 
                                `player_name` = " + aBan.player_name + @", ";
                                if (aBan.player_ip != null)
                                    query += "`player_ip` = " + aBan.player_ip + @", ";
                                if (aBan.player_guid != null)
                                    query += "`player_guid` = " + aBan.player_guid + @", ";
                                query += @"
                                `ban_reason` = " + aBan.ban_reason + @", 
                                `ban_notes` = " + aBan.ban_notes + @", 
                                `ban_sync` = " + AdKats.EncodeStringArray(aBan.ban_sync.ToArray()) + @", 
                                `ban_endTime` = " + matchingDBBan.ban_time.AddMinutes(aBan.ban_durationMinutes) + @", 
                                WHERE 
                                `ban_id` = " + matchingDBBan.ban_id;

                                this.DebugWrite("QUERY: " + query, 6);

                                command.CommandText = query;
                            }
                            //Attempt to execute the query
                            if (command.ExecuteNonQuery() > 0)
                            {
                                success = true;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    ConsoleException(e.ToString());
                }
            }

            DebugWrite("checkBan finished!", 6);
            return success;
        }

        private AdKat_Ban getMatchingDBBan(AdKat_Ban aBan)
        {
            DebugWrite("getMatchinDBBans starting!", 6);
            //Create return list
            List<AdKat_Ban> tempBanList = new List<AdKat_Ban>();
            if (aBan == null)
            {
                this.ConsoleError("Ban invalid in checkBan.");
            }
            else
            {
                try
                {
                    using (MySqlConnection databaseConnection = this.getDatabaseConnection())
                    {
                        using (MySqlCommand command = databaseConnection.CreateCommand())
                        {
                            String sql = @"SELECT * FROM `" + this.mySqlDatabaseName + @"`.`adkat_banlist` ";
                            bool sqlender = true;

                            if (!String.IsNullOrEmpty(aBan.player_name))
                            {
                                if (sqlender)
                                {
                                    sql += " WHERE (";
                                    sqlender = false;
                                }
                                sql += " `player_name` LIKE '" + aBan.player_name + "'";
                            }
                            if (!String.IsNullOrEmpty(aBan.player_guid))
                            {
                                if (sqlender)
                                {
                                    sql += " WHERE (";
                                    sqlender = false;
                                }
                                else
                                {
                                    sql += " OR ";
                                }
                                sql += " `player_guid` LIKE '" + aBan.player_guid + "'";
                            }
                            if (!String.IsNullOrEmpty(aBan.player_ip))
                            {
                                if (sqlender)
                                {
                                    sql += " WHERE (";
                                    sqlender = false;
                                }
                                else
                                {
                                    sql += " OR ";
                                }
                                sql += " `player_ip` LIKE '" + aBan.player_ip + "'";
                            }
                            sql += ") AND `ban_endTime` > NOW()";

                            this.DebugWrite("QUERY: " + sql, 6);
                            using (MySqlDataReader reader = command.ExecuteReader())
                            {
                                //Success fetching bans
                                Boolean success = false;
                                //Loop through all incoming bans
                                while (reader.Read())
                                {
                                    success = true;

                                    AdKat_Ban ban = new AdKat_Ban();
                                    ban.ban_id = reader.GetInt32("ban_id");
                                    ban.admin_name = reader.GetString("admin_name");
                                    ban.player_name = reader.GetString("player_name");
                                    ban.player_ip = reader.GetString("player_ip");
                                    ban.player_guid = reader.GetString("player_guid");
                                    ban.ban_reason = reader.GetString("ban_reason");
                                    ban.ban_notes = reader.GetString("ban_notes");
                                    string[] tempSync = AdKats.DecodeStringArray(reader.GetString("ban_sync"));
                                    ban.ban_sync = new List<string>();
                                    foreach (string sync in tempSync)
                                    {
                                        ban.ban_sync.Add(sync);
                                    }
                                    DateTime banStart = reader.GetDateTime("ban_startTime");
                                    DateTime banEnd = reader.GetDateTime("ban_endTime");
                                    ban.ban_time = banStart;
                                    ban.ban_durationMinutes = (int)banEnd.Subtract(banStart).TotalMinutes;

                                    tempBanList.Add(ban);
                                }
                                if (success)
                                {
                                    if (tempBanList.Count > 1)
                                    {
                                        this.ConsoleError("More than one active ban found in database for " + aBan.player_name + ". This is not normal.");
                                    }
                                }
                                else
                                {
                                    this.DebugWrite("No active bans matching this player.", 5);
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    ConsoleException(e.ToString());
                }
            }

            DebugWrite("getMatchinDBBans finished!", 6);
            if (tempBanList.Count > 0)
            {
                return tempBanList[0];
            }
            else
            {
                return null;
            }
        }

        private Boolean fetchBans()
        {
            DebugWrite("canPunish starting!", 6);

            try
            {
                using (MySqlConnection databaseConnection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = databaseConnection.CreateCommand())
                    {
                        command.CommandText = @"
                        SELECT 
                            `ban_id`, 
	                        `admin_name`, 
	                        `player_name`, 
	                        `player_ip`, 
	                        `player_guid`, 
	                        `ban_reason`, 
	                        `ban_durationMinutes`, 
	                        `ban_status`, 
	                        `ban_sync`, 
	                        `ban_time`, 
                        FROM
	                        `adkat_banlist` ";
                        //WHERE 
                        //    `ban_sync` NOT LIKE '/" + this.server_id + "/'";

                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            //Success fetching bans
                            Boolean success = false;
                            //Loop through all incoming bans
                            while (reader.Read())
                            {
                                success = true;

                                AdKat_Ban ban = new AdKat_Ban();
                                ban.ban_id = reader.GetInt32("ban_id");
                                ban.admin_name = reader.GetString("admin_name");
                                ban.player_name = reader.GetString("player_name");
                                ban.player_ip = reader.GetString("player_ip");
                                ban.player_guid = reader.GetString("player_guid");
                                ban.ban_reason = reader.GetString("ban_reason");
                                ban.ban_notes = reader.GetString("ban_notes");
                                string[] tempSync = AdKats.DecodeStringArray(reader.GetString("ban_sync"));
                                ban.ban_sync = new List<string>();
                                foreach (string sync in tempSync)
                                {
                                    ban.ban_sync.Add(sync);
                                }
                                DateTime banStart = reader.GetDateTime("ban_startTime");
                                DateTime banEnd = reader.GetDateTime("ban_endTime");
                                ban.ban_time = banStart;
                                ban.ban_durationMinutes = (int)banEnd.Subtract(banStart).TotalMinutes;

                            }

                            if (success)
                            {

                                //Clear the ban list
                                this.AdKat_BanList.Clear();
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                DebugWrite(e.ToString(), 3);
            }
            DebugWrite("ERROR in fetchBans!", 6);
            return false;
        }

        private Boolean canPunish(AdKat_Record record)
        {
            DebugWrite("canPunish starting!", 6);

            try
            {
                using (MySqlConnection databaseConnection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = databaseConnection.CreateCommand())
                    {
                        if (this.combineServerPunishments)
                        {
                            command.CommandText = "select record_time as `latest_time` from `" + this.mySqlDatabaseName + "`.`adkat_records` where `adkat_records`.`command_type` = 'Punish' and `adkat_records`.`target_guid` = '" + record.target_guid + "' and DATE_ADD(`record_time`, INTERVAL 20 SECOND) > NOW() order by record_time desc limit 1";
                        }
                        else
                        {
                            command.CommandText = "select record_time as `latest_time` from `" + this.mySqlDatabaseName + "`.`adkat_records` where `adkat_records`.`server_id` = '" + this.server_id + "' and `adkat_records`.`command_type` = 'Punish' and `adkat_records`.`target_guid` = '" + record.target_guid + "' and DATE_ADD(`record_time`, INTERVAL 20 SECOND) > NOW() order by record_time desc limit 1";
                        }
                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                this.DebugWrite("can't upload punish", 6);
                                return false;
                            }
                            else
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                DebugWrite(e.ToString(), 3);
            }
            DebugWrite("ERROR in canPunish!", 6);
            return false;
        }

        private Boolean isDoubleCounted(AdKat_Record record)
        {
            DebugWrite("isDoubleCounted starting!", 6);

            try
            {
                using (MySqlConnection databaseConnection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = databaseConnection.CreateCommand())
                    {
                        if (this.combineServerPunishments)
                        {
                            command.CommandText = "select record_time as `latest_time` from `" + this.mySqlDatabaseName + "`.`adkat_records` where `adkat_records`.`command_type` = 'Punish' and `adkat_records`.`target_guid` = '" + record.target_guid + "' and DATE_ADD(`record_time`, INTERVAL 5 MINUTE) > NOW() order by record_time desc limit 1";
                        }
                        else
                        {
                            command.CommandText = "select record_time as `latest_time` from `" + this.mySqlDatabaseName + "`.`adkat_records` where `adkat_records`.`server_id` = '" + this.server_id + "' and `adkat_records`.`command_type` = 'Punish' and `adkat_records`.`target_guid` = '" + record.target_guid + "' and DATE_ADD(`record_time`, INTERVAL 5 MINUTE) > NOW() order by record_time desc limit 1";
                        }
                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                this.DebugWrite("Punish is double counted", 6);
                                return true;
                            }
                            else
                            {
                                return false;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                DebugWrite(e.ToString(), 3);
            }
            DebugWrite("ERROR in isDoubleCounted!", 6);
            return false;
        }

        private void runActionsFromDB()
        {
            DebugWrite("runActionsFromDB starting!", 7);
            try
            {
                List<AdKat_Record> recordsProcessed = new List<AdKat_Record>();
                using (MySqlConnection databaseConnection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = databaseConnection.CreateCommand())
                    {
                        command.CommandText = "SELECT `record_id`, `server_id`, `server_ip`, `command_type`, `command_action`, `record_durationMinutes`, `target_guid`, `target_name`, `source_name`, `record_message`, `record_time` FROM `" + this.mySqlDatabaseName + "`.`adkat_records` WHERE `adkats_read` = 'N' AND `server_id` = '" + this.server_id + "'";
                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            Boolean actionsMade = false;
                            while (reader.Read())
                            {
                                DebugWrite("getPoints found actions for player " + reader.GetString("target_name") + "!", 5);
                                AdKat_Record record = new AdKat_Record();
                                record.command_source = AdKat_CommandSource.Database;
                                record.record_id = reader.GetInt32("record_id");
                                record.server_id = reader.GetInt32("server_id");
                                record.server_ip = reader.GetString("server_ip");
                                string commandString = reader.GetString("command_type");
                                record.command_type = this.getDBCommand(commandString);
                                //If command not parsable, return without creating
                                if (record.command_type == AdKat_CommandType.Default)
                                {
                                    ConsoleError("Command '" + command + "' Not Parsable. Check AdKats doc for valid DB commands.");
                                    //break this loop iteration and go to the next one
                                    continue;
                                }
                                record.command_action = record.command_type;
                                /*commandString = reader.GetString("command_action");
                                record.command_action = this.getDBCommand(commandString);
                                //If command not parsable, return without creating
                                if (record.command_action == AdKat_CommandType.Default)
                                {
                                    ConsoleError("Command '" + command + "' Not Parsable. Check AdKats doc for valid DB commands.");
                                    //break this loop iteration and go to the next one
                                    continue;
                                }*/
                                record.source_name = reader.GetString("source_name");
                                record.target_name = reader.GetString("target_name");
                                record.target_guid = reader.GetString("target_guid");
                                record.record_message = reader.GetString("record_message");
                                record.record_time = reader.GetDateTime("record_time");
                                record.record_durationMinutes = reader.GetInt32("record_durationMinutes");
                                this.runAction(record);
                                recordsProcessed.Add(record);
                                actionsMade = true;
                            }
                            //close and return if no actions were taken
                            if (!actionsMade)
                            {
                                databaseConnection.Close();
                                return;
                            }
                        }
                    }
                }
                foreach (AdKat_Record record in recordsProcessed)
                {
                    this.updateRecord(record);
                }
                //Update the last time this was fetched
                this.lastDBActionFetch = DateTime.Now;
            }
            catch (Exception e)
            {
                DebugWrite(e.ToString(), 3);
            }
        }

        private int fetchPoints(string player_guid)
        {
            DebugWrite("fetchPoints starting!", 6);

            int returnVal = -1;

            try
            {
                using (MySqlConnection databaseConnection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = databaseConnection.CreateCommand())
                    {
                        if (this.combineServerPunishments)
                        {
                            command.CommandText = @"SELECT 
                                                (SELECT count(`adkat_records`.`target_guid`) 
	                                                FROM `adkat_records` 
	                                                WHERE   `adkat_records`.`command_type` = 'Punish' 
		                                                AND `adkat_records`.`target_guid` = @player_guid) - 
                                                (SELECT count(`adkat_records`.`target_guid`)
	                                                FROM `adkat_records`
	                                                WHERE   `adkat_records`.`command_type` = 'Forgive'
		                                                AND `adkat_records`.`target_guid` = @player_guid) as `totalpoints`";
                            command.Parameters.AddWithValue("@player_guid", player_guid);
                        }
                        else
                        {
                            command.CommandText = @"SELECT 
                                                (SELECT count(`adkat_records`.`target_guid`) 
	                                                FROM `adkat_records` 
	                                                WHERE   `adkat_records`.`command_type` = 'Punish' 
		                                                AND `adkat_records`.`target_guid` = @player_guid 
		                                                AND `adkat_records`.`server_id` = @server_id) - 
                                                (SELECT count(`adkat_records`.`target_guid`)
	                                                FROM `adkat_records`
	                                                WHERE   `adkat_records`.`command_type` = 'Forgive'
		                                                AND `adkat_records`.`target_guid` = @player_guid
		                                                AND `adkat_records`.`server_id` = @server_id) as `totalpoints`";
                            command.Parameters.AddWithValue("@player_guid", player_guid);
                            command.Parameters.AddWithValue("@server_id", this.server_id);
                        }
                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                returnVal = reader.GetInt32("totalpoints");
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                DebugWrite(e.ToString(), 3);
            }

            DebugWrite("fetchPoints finished!", 6);

            return returnVal;
        }

        private void fetchAccessList()
        {
            DebugWrite("fetchAccessList starting!", 6);

            Boolean success = false;
            Dictionary<string, int> tempAccessCache = new Dictionary<string, int>();
            try
            {
                using (MySqlConnection databaseConnection = this.getDatabaseConnection())
                {
                    List<string> namesToGUIDUpdate = new List<string>();
                    using (MySqlCommand command = databaseConnection.CreateCommand())
                    {
                        command.CommandText = "SELECT `player_name`, `player_guid`, `access_level` FROM `" + this.mySqlDatabaseName + "`.`adkat_accesslist` ORDER BY `access_level` ASC";
                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                success = true;
                                string playerName = reader.GetString("player_name");
                                string playerGuid = reader.GetString("player_guid");
                                int accessLevel = reader.GetInt32("access_level");
                                tempAccessCache.Add(playerName, accessLevel);
                                if (!Regex.Match(playerGuid, "EA_").Success && this.playerDictionary.ContainsKey(playerName))
                                {
                                    namesToGUIDUpdate.Add(playerName);
                                }
                                DebugWrite("Admin found: " + playerName, 6);
                            }
                        }
                    }
                    if (namesToGUIDUpdate.Count > 0)
                    {
                        using (MySqlCommand command = databaseConnection.CreateCommand())
                        {
                            command.CommandText = "";
                            foreach (string player_name in namesToGUIDUpdate)
                            {
                                this.DebugWrite("Updating GUID for " + player_name, 6);
                                command.CommandText += "UPDATE `" + this.mySqlDatabaseName + "`.`adkat_accesslist` SET `player_guid` = '" + this.playerDictionary[player_name].GUID + "' WHERE `player_name` = '" + player_name + "'; ";
                            }
                            //Attempt to execute the query
                            if (command.ExecuteNonQuery() > 0)
                            {
                                success = true;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                DebugWrite(e.ToString(), 3);
            }
            if (success)
            {
                //Update the access cache
                this.playerAccessCache = tempAccessCache;
                //Update the last update time
                this.lastDBAccessFetch = DateTime.Now;
                ConsoleWrite("Admin List Fetched from Database. Admin Count: " + this.playerAccessCache.Count);
            }
            else
            {
                ConsoleError("No admins in the admin table.");
            }

            DebugWrite("fetchAccessList finished!", 6);
        }

        private void fetchAdminAssistants()
        {
            DebugWrite("fetchAdminAssistants starting!", 6);

            Boolean success = false;
            Dictionary<string, Boolean> tempAssistantCache = new Dictionary<string, Boolean>();
            try
            {
                using (MySqlConnection databaseConnection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = databaseConnection.CreateCommand())
                    {
                        command.CommandText = @"
                        SELECT `player_name` 
                        FROM `adkat_playerlist` 
                        WHERE (
	                        SELECT count(`command_action`) 
	                        FROM `" + this.mySqlDatabaseName + @"`.`adkat_records` 
	                        WHERE `command_action` = 'ConfirmReport' 
	                        AND `source_name` = `player_name` 
	                        AND (`adkat_records`.`record_time` BETWEEN date_sub(now(),INTERVAL 7 DAY) AND now())
                        ) > " + this.minimumRequiredWeeklyReports;

                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                success = true;
                                string playerName = reader.GetString("player_name");
                                tempAssistantCache.Add(playerName, false);
                                DebugWrite("Assistant found: " + playerName, 6);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                DebugWrite(e.ToString(), 3);
            }

            if (success)
            {
                //Update the access cache
                this.adminAssistantCache = tempAssistantCache;
                if (this.enableAdminAssistants)
                {
                    ConsoleWrite("Admin Assistant List Fetched from Database. Assistant Count: " + this.adminAssistantCache.Count);
                }
            }
            else
            {
                ConsoleWrite("There are currently no admin assistants.");
            }

            DebugWrite("fetchAdminAssistants finished!", 6);
        }

        #endregion

        #endregion

        #region HTTP Server Handling

        public override HttpWebServerResponseData OnHttpRequest(HttpWebServerRequestData data)
        {
            string responseString = "AdKats Remote: ";
            try
            {
                /*foreach (String key in data.POSTData.AllKeys)
                {
                    this.DebugWrite("POST Key: " + key + " val: " + data.Headers[key], 6);
                }*/
                foreach (String key in data.Query.AllKeys)
                {
                    this.DebugWrite("Query Key: " + key + " val: " + data.Query[key], 6);
                }
                this.DebugWrite("method: " + data.Method, 6);
                //this.DebugWrite("doc: " + data.Document, 6);
                AdKat_Record record = new AdKat_Record();
                record.command_source = AdKat_CommandSource.HTTP;

                NameValueCollection dataCollection = null;
                if (String.Compare(data.Method, "GET", true) == 0)
                {
                    dataCollection = data.Query;
                }
                else if (String.Compare(data.Method, "POST", true) == 0)
                {
                    return null;//dataCollection = data.POSTData;
                }
                string commandString = dataCollection["command_type"];
                record.command_type = this.getDBCommand(commandString);

                if (dataCollection["access_key"] != null && dataCollection["access_key"].Equals(this.externalCommandAccessKey))
                {
                    //If command not parsable, return without creating
                    if (record.command_type != AdKat_CommandType.Default)
                    {
                        //Set the command action
                        record.command_action = record.command_type;

                        //Set the source
                        string sourceName = dataCollection["source_name"];
                        if (sourceName != null)
                            record.source_name = sourceName;
                        else
                            record.source_name = "HTTPAdmin";

                        string duration = dataCollection["record_durationMinutes"];
                        if (duration != null && duration.Length > 0)
                        {
                            record.record_durationMinutes = Int32.Parse(duration);
                        }
                        else
                        {
                            record.record_durationMinutes = 0;
                        }

                        string message = dataCollection["record_message"];
                        if (message != null)
                        {
                            if (message.Length >= this.requiredReasonLength)
                            {
                                record.record_message = message;

                                //Check the target
                                string targetName = dataCollection["target_name"];
                                //Check for an exact match
                                if (targetName != null && targetName.Length > 0)
                                {
                                    record.target_name = targetName;
                                    responseString += this.completeTargetInformation(record, false);
                                }
                                else
                                {
                                    responseString += "target_name cannot be null";
                                }
                            }
                            else
                            {
                                responseString += "Reason too short. Needs to be at least " + this.requiredReasonLength + " chars.";
                            }
                        }
                        else
                        {
                            responseString += "record_message cannot be null.";
                        }
                    }
                    else
                    {
                        responseString += "Command '" + commandString + "' Not Parsable. Check AdKats doc for valid DB commands.";
                    }
                }
                else
                {
                    responseString += "access_key either not given or incorrect.";
                }
            }
            catch (Exception e)
            {
                responseString += e.ToString();
            }
            return new HttpWebServerResponseData(responseString);
        }

        #endregion

        #region encoding and hash gen

        public static string GetRandom64BitHashCode()
        {
            string randomString = "";
            Random random = new Random();

            for (int i = 0; i < 32; i++)
            {
                randomString += Convert.ToChar(Convert.ToInt32(Math.Floor(91 * random.NextDouble()))).ToString(); ;
            }

            return Encode(randomString);
        }

        public static string Encode(string str)
        {
            byte[] encbuff = System.Text.Encoding.UTF8.GetBytes(str);
            return Convert.ToBase64String(encbuff);
        }

        public static string Decode(string str)
        {
            byte[] decbuff = Convert.FromBase64String(str.Replace(" ", "+"));
            return System.Text.Encoding.UTF8.GetString(decbuff);
        }

        #endregion

        #region Mailing Functions

        private void sendAdminCallEmail(AdKat_Record record)
        {
            CServerInfo info = this.getServerInfo();
            string subject = String.Empty;
            string body = String.Empty;

            subject = "[Admin Call] - " + record.source_name + " requested an admin. Message - " + record.record_message;

            StringBuilder sb = new StringBuilder();
            sb.Append("<b>Admin Request Notification</b><br /><br />");
            sb.Append("Date/Time of call:<b> " + DateTime.Now.ToString() + "</b><br />");
            sb.Append("Servername:<b> " + info.ServerName + "</b><br />");
            sb.Append("Server address:<b> " + this.strHostName + ":" + this.strPort + "</b><br />");
            sb.Append("Playercount:<b> " + info.PlayerCount + "/" + info.MaxPlayerCount + "</b><br />");
            sb.Append("Map:<b> " + info.Map + "</b><br /><br />");
            sb.Append("Request-Sender:<b> " + record.source_name + "</b><br />");
            sb.Append("Message:<b> " + record.record_message + "</b><br /><br />");
            /*sb.Append("<i>Playertable:</i><br />");
            sb.Append("<table border='1' rules='rows'><tr><th>Playername</th><th>Score</th><th>Kills</th><th>Deaths</th><th>HPK%</th><th>KDR</th><th>GUID</th></tr>");
            foreach (CPlayerInfo player in this.playerList)
            {
                double mHeadshots = 0;
                if (this.d_Headshots.ContainsKey(player.SoldierName.ToLower()) == true)
                {
                    if (player.Kills > 0) { mHeadshots = (double)(d_Headshots[player.SoldierName.ToLower()] * 100) / player.Kills; }
                }
                sb.Append("<tr align='center'><td>" + player.SoldierName + "</td><td>" + player.Score + "</td><td>" + player.Kills + "</td><td>" + player.Deaths + "</td><td>" + String.Format("{0:0.##}", mHeadshots) + "</td><td>" + String.Format("{0:0.##}", player.Kdr) + "</td><td>" + player.GUID + "</td></tr>");
            }
            sb.Append("</table>");*/

            body = sb.ToString();

            this.EmailWrite(subject, body);
        }

        private void EmailWrite(string subject, string body)
        {
            try
            {
                MailMessage email = new MailMessage();

                email.From = new MailAddress(this.strSenderMail);

                foreach (string mailto in this.lstReceiverMail)
                {
                    if (Regex.IsMatch(mailto, @"^([\w-\.]+)@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([\w-]+\.)+))([a-zA-Z]{2,4}|[0-9]{1,3})(\]?)$"))
                    {
                        email.To.Add(new MailAddress(mailto));
                    }
                    else
                    {
                        this.ConsoleError("Error in receiver email address: " + mailto);
                    }
                }

                email.Subject = subject;
                email.Body = body;
                email.IsBodyHtml = true;
                email.BodyEncoding = UTF8Encoding.UTF8;

                SmtpClient smtp = new SmtpClient(this.strSMTPServer, this.iSMTPPort);

                smtp.EnableSsl = this.blUseSSL;

                smtp.Timeout = 10000;
                smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
                smtp.UseDefaultCredentials = false;
                smtp.Credentials = new NetworkCredential(this.strSMTPUser, this.strSMTPPassword);
                smtp.Send(email);

                this.DebugWrite("A notification email has been sent.", 1);
            }
            catch (Exception e)
            {
                this.ConsoleError("Error while sending mails: " + e.ToString());
            }
        }

        #endregion

        #region Helper Methods and Classes

        //Decode and encode from CPlayerInfo
        public static string[] DecodeStringArray(string strValue)
        {
            string[] a_strReturn = new string[] { };

            a_strReturn = strValue.Split(new char[] { '|' });

            for (int i = 0; i < a_strReturn.Length; i++)
            {
                a_strReturn[i] = Decode(a_strReturn[i]);
            }

            return a_strReturn;
        }
        public static string EncodeStringArray(string[] a_strValue)
        {

            StringBuilder encodedString = new StringBuilder();

            for (int i = 0; i < a_strValue.Length; i++)
            {
                if (i > 0)
                {
                    encodedString.Append("|");
                    //strReturn += "|";
                }
                encodedString.Append(Encode(a_strValue[i]));
                //strReturn += Encode(a_strValue[i]);
            }

            return encodedString.ToString();
        }

        public void setServerInfo(CServerInfo info)
        {
            lock (this.serverInfoMutex)
            {
                this.serverInfo = info;
            }
        }

        public CServerInfo getServerInfo()
        {
            lock (this.serverInfoMutex)
            {
                return this.serverInfo;
            }
        }

        public static AdKat_Ban createABanFromCBan(CBanInfo ban)
        {
            //Create ban
            AdKat_Ban aBan = new AdKat_Ban();
            //Do not set ban_id
            aBan.admin_name = "PRoConAdmin";
            aBan.player_name = ban.SoldierName;
            if (ban.IpAddress != null)
                aBan.player_ip = ban.IpAddress;
            if (ban.Guid != null)
                aBan.player_guid = ban.Guid;
            aBan.ban_reason = ban.Reason;
            aBan.ban_notes = "";
            aBan.ban_sync = new List<string>();
            //Do not set ban_time

            if (ban.BanLength.Subset == TimeoutSubset.TimeoutSubsetType.Permanent)
            {
                aBan.ban_durationMinutes = 999 * 360 * 24 * 60;
            }
            else
            {
                aBan.ban_durationMinutes = ban.BanLength.Seconds / 60;
            }
            return aBan;
        }

        //Calling this method will make the settings window refresh with new data
        public void updateSettingPage()
        {
            this.ExecuteCommand("procon.protected.plugins.setVariable", "AdKats", "UpdateSettings", "Update");
        }

        //Credit to Micovery and PapaCharlie9 for modified Levenshtein Distance algorithm 
        public static int LevenshteinDistance(string s, string t)
        {
            s = s.ToLower();
            t = t.ToLower();
            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];
            if (n == 0)
                return m;
            if (m == 0)
                return n;
            for (int i = 0; i <= n; d[i, 0] = i++) ;
            for (int j = 0; j <= m; d[0, j] = j++) ;
            for (int i = 1; i <= n; i++)
                for (int j = 1; j <= m; j++)
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 0), d[i - 1, j - 1] + ((t[j - 1] == s[i - 1]) ? 0 : 1));
            return d[n, m];
        }

        //parses single word or number parameters out of a string until param count is reached
        private String[] parseParameters(string message, int maxParamCount)
        {
            //create list for parameters
            List<String> parameters = new List<String>();
            if (message.Length > 0)
            {
                //Add all single word/number parameters
                String[] paramSplit = message.Split(' ');
                int maxLoop = (paramSplit.Length < maxParamCount) ? (paramSplit.Length) : (maxParamCount);
                for (int i = 0; i < maxLoop - 1; i++)
                {
                    this.DebugWrite("Param " + i + ": " + paramSplit[i], 6);
                    parameters.Add(paramSplit[i]);
                    message = message.TrimStart(paramSplit[i].ToCharArray()).Trim();
                }
                //Add final multi-word parameter
                parameters.Add(message);
            }
            this.DebugWrite("Num params: " + parameters.Count, 6);
            return parameters.ToArray();
        }

        public void JoinWith(Thread thread)
        {
            if (thread == null || !thread.IsAlive)
            {
                DebugWrite("^b" + thread.Name + "^n already finished.", 3);
                return;
            }
            DebugWrite("Waiting for ^b" + thread.Name + "^n to finish", 3);
            thread.Join();
        }

        public class AdKat_Record
        {
            public Boolean isIRO = false;
            public long record_id = -1;
            public int server_id = -1;
            public string server_ip = "0.0.0.0:0000";
            public string target_guid = null;
            public string target_name = null;
            public CPlayerInfo target_playerInfo = null;
            //Command source not stored in the database
            public AdKat_CommandSource command_source = AdKat_CommandSource.Default;
            public string source_name = null;
            public AdKat_CommandType command_type = AdKat_CommandType.Default;
            public AdKat_CommandType command_action = AdKat_CommandType.Default;
            public string record_message = null;
            public DateTime record_time;
            public Int32 record_durationMinutes = 0;

            public AdKat_Record()
            {
            }
        }

        public class AdKat_Ban
        {
            /*
            `ban_id` int(11) NOT NULL AUTO_INCREMENT, 
            `admin_name` varchar(45) NOT NULL DEFAULT "NoNameAdmin", 
            `player_name` varchar(45) NOT NULL DEFAULT "NoPlayer", 
            `player_ip` varchar(45) NOT NULL DEFAULT "NoIP", 
            `player_guid` varchar(100) NOT NULL DEFAULT 'NoGUID', 
            `ban_status` enum('Enabled', 'Disabled') NOT NULL DEFAULT 'Enabled';
            `ban_reason` varchar(100) NOT NULL DEFAULT 'NoReason', 
            `ban_notes` varchar(150) NOT NULL DEFAULT 'NoNotes', 
            `ban_sync` varchar(100) NOT NULL DEFAULT "-sync-", 
            `ban_startTime` TIMESTAMP DEFAULT CURRENT_TIMESTAMP, 
            `ban_endTime` TIMESTAMP DEFAULT CURRENT_TIMESTAMP, 
            `ban_displayDurationMinutes` int(11) NOT NULL DEFAULT 0, 
            */
            public long ban_id = -1;
            public string admin_name = null;
            public string player_name = null;
            public string player_ip = null;
            public string player_guid = null;
            public string ban_reason = null;
            public string ban_notes = null;
            public List<string> ban_sync = null;
            public DateTime ban_startTime;
            //ban_endTime is calculated from startTime and durationMinutes
            public int ban_durationMinutes = 0;

            public AdKat_Ban()
            {
            }
        }

        #endregion

        #region Logging

        public string FormatMessage(string msg, MessageTypeEnum type)
        {
            string prefix = "[^bAdKats^n] ";

            if (type.Equals(MessageTypeEnum.Warning))
            {
                prefix += "^1^bWARNING^0^n: ";
            }
            else if (type.Equals(MessageTypeEnum.Error))
            {
                prefix += "^1^bERROR^0^n: ";
            }
            else if (type.Equals(MessageTypeEnum.Exception))
            {
                prefix += "^1^bEXCEPTION^0^n: ";
            }

            return prefix + msg;
        }

        public void LogWrite(string msg)
        {
            this.ExecuteCommand("procon.protected.pluginconsole.write", msg);
        }

        public void ConsoleWrite(string msg, MessageTypeEnum type)
        {
            LogWrite(FormatMessage(msg, type));
        }

        public void ConsoleWrite(string msg)
        {
            ConsoleWrite(msg, MessageTypeEnum.Normal);
        }

        public void ConsoleWarn(string msg)
        {
            ConsoleWrite(msg, MessageTypeEnum.Warning);
        }

        public void ConsoleError(string msg)
        {
            ConsoleWrite(msg, MessageTypeEnum.Error);
        }

        public void ConsoleException(string msg)
        {
            ConsoleWrite(msg, MessageTypeEnum.Exception);
        }

        public void DebugWrite(string msg, int level)
        {
            if (debugLevel >= level)
            {
                ConsoleWrite(msg, MessageTypeEnum.Normal);
            }
        }

        #endregion
    } // end AdKats
} // end namespace PRoConEvents
