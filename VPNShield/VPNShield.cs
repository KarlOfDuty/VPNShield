using Newtonsoft.Json.Linq;
using Smod2;
using Smod2.Attributes;
using Smod2.Commands;
using Smod2.EventHandlers;
using Smod2.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace VPNShield
{
    [PluginDetails(
        author = "Karl Essinger",
        name = "VPNShield",
        description = "Blocks users connecting using VPNs.",
        id = "karlofduty.vpnshield",
        version = "3.0.0",
        SmodMajor = 3,
        SmodMinor = 1,
        SmodRevision = 19
    )]
    public class VPNShield : Plugin
    {
        public JObject config;
        public HashSet<string> autoWhitelist;
        public HashSet<string> autoBlacklist;
        public bool whitelistUpdated = false;
        public bool blacklistUpdated = false;

        readonly string defaultConfig =
        "{\n"                                       +
        "    \"block-vpns\": false,\n"              +
        "    \"iphub-apikey\": \"put-key-here\",\n" +
        "    \"block-new-steam-accounts\": true,\n" +
        "    \"verbose\": false,\n"                 +
        "}";

        readonly string defaultlist =
        "[\n" +
        "]";

        public override void OnDisable()
        {

        }

        public override void Register()
        {
            this.AddEventHandlers(new CheckPlayer(this), Priority.High);
            this.AddEventHandlers(new SaveData(this), Priority.High);
            this.AddCommand("vs_reload", new ReloadCommand(this));
            this.AddCommand("vs_enable", new EnableCommand(this));
            this.AddCommand("vs_disable", new DisableCommand(this));
        }

        public override void OnEnable()
        {
            SetUpFileSystem();

            config = JObject.Parse(File.ReadAllText(FileManager.AppFolder + "VPNShield/config.json"));
            autoWhitelist = new HashSet<string>(JArray.Parse(File.ReadAllText(FileManager.AppFolder + "VPNShield/auto-whitelist.json")).Values<string>());
            autoBlacklist = new HashSet<string>(JArray.Parse(File.ReadAllText(FileManager.AppFolder + "VPNShield/auto-blacklist.json")).Values<string>());

            this.Info("VPNShield enabled.");
        }

        public void SetUpFileSystem()
        {
            if (!Directory.Exists(FileManager.AppFolder + "VPNShield"))
            {
                Directory.CreateDirectory(FileManager.AppFolder + "VPNShield");
            }

            if (!File.Exists(FileManager.AppFolder + "VPNShield/config.json"))
            {
                File.WriteAllText(FileManager.AppFolder + "VPNShield/config.json", defaultConfig);
            }

            if (!File.Exists(FileManager.AppFolder + "VPNShield/auto-whitelist.json"))
            {
                File.WriteAllText(FileManager.AppFolder + "VPNShield/auto-whitelist.json", defaultlist);
            }

            if (!File.Exists(FileManager.AppFolder + "VPNShield/auto-blacklist.json"))
            {
                File.WriteAllText(FileManager.AppFolder + "VPNShield/auto-blacklist.json", defaultlist);
            }
        }

        public void SaveWhitelistToFile()
        {
            // Save the state to file
            StringBuilder builder = new StringBuilder();
            builder.Append("[\n");
            foreach (string line in autoWhitelist)
            {
                builder.Append("    \"" + line + "\"," + "\n");
            }
            builder.Append("]\n");
            File.WriteAllText(FileManager.AppFolder + "VPNShield/auto-whitelist.json", builder.ToString());
        }

        public void SaveBlacklistToFile()
        {
            // Save the state to file
            StringBuilder builder = new StringBuilder();
            builder.Append("[\n");
            foreach (string line in autoBlacklist)
            {
                builder.Append("    \"" + line + "\"," + "\n");
            }
            builder.Append("]\n");
            File.WriteAllText(FileManager.AppFolder + "VPNShield/auto-blacklist.json", builder.ToString());
        }

        public bool CheckVPN(PlayerJoinEvent ev)
        {
            if(!config.Value<bool>("block-vpns"))
            {
                return false;
            }
            string ipAddress = ev.Player.IpAddress.Replace("::ffff:", "");
            if (autoWhitelist.Contains(ipAddress))
            {
                if (config.Value<bool>("verbose"))
                {
                    this.Info(ev.Player.Name + "'s IP address has passed a VPN check previously, skipping...");
                }
                return false;
            }

            if (autoBlacklist.Contains(ipAddress))
            {
                this.Info(ev.Player.Name + "'s IP address has failed a VPN check previously, kicking...");
                ev.Player.Ban(0, "This server does not allow VPNs or proxy connections.");
                return true;
            }

            HttpWebResponse response = null;
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://v2.api.iphub.info/ip/" + ipAddress);
                request.Headers.Add("x-key", config.Value<string>("iphub-apikey"));
                request.Method = "GET";

                response = (HttpWebResponse)request.GetResponse();

                string responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();
                JObject json = JObject.Parse(responseString);

                int verificationLevel = json.Value<int>("block");
                if (verificationLevel == 0 || verificationLevel == 2)
                {
                    if (config.Value<bool>("verbose"))
                    {
                        this.Info(ev.Player.Name + " is not using a detectable VPN.");
                    }
                    autoWhitelist.Add(ipAddress);
                    whitelistUpdated = true;
                }
                else if (verificationLevel == 1)
                {
                    this.Info(ev.Player.Name + " is using a VPN.");
                    autoBlacklist.Add(ipAddress);
                    blacklistUpdated = true;
                    ev.Player.Ban(0, "This server does not allow VPNs or proxy connections.");
                    response.Close();
                    return true;
                }
            }
            catch (WebException e)
            {
                if (e.Status == WebExceptionStatus.ProtocolError)
                {
                    response = (HttpWebResponse)e.Response;
                    if((int)response.StatusCode == 429)
                    {
                        this.Warn("Anti-VPN check could not complete, you have reached your API key's rate limit.");
                    }
                    else
                    {
                        this.Warn("Anti-VPN connection error: " + response.StatusCode);
                    }
                }
                else
                {
                    this.Warn("Anti-VPN connection error: " + e.Status.ToString());
                }
            }
            finally
            {
                if (response != null)
                {
                    response.Close();
                }
            }
            return false;
        }

        public bool CheckSteamAccount(PlayerJoinEvent ev)
        {
            ServicePointManager.ServerCertificateValidationCallback = SSLValidation;
            HttpWebResponse response = null;
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://steamcommunity.com/profiles/" + ev.Player.SteamId + "?xml=1");
                request.Method = "GET";

                response = (HttpWebResponse)request.GetResponse();

                string xmlResponse = new StreamReader(response.GetResponseStream()).ReadToEnd();

                string[] foundStrings = xmlResponse.Split('\n').Where(w => w.Contains("isLimitedAccount")).ToArray();

                if (foundStrings.Length == 0)
                {
                    this.Error("Steam account check failed. Their profile did not have the required information.");
                    return false;
                }

                bool isLimitedAccount = foundStrings[0].Where(c => char.IsDigit(c)).ToArray()[0] != '0';
                if (isLimitedAccount)
                {
                    this.Info(ev.Player.Name + " has a new steam account with no purchases.");
                    if(config.Value<bool>("block-new-steam-accounts"))
                    {
                        ev.Player.Ban(0, "This server does not allow new Steam accounts, you have to buy something on Steam before playing.");
                        return true;
                    }

                }
                else if(config.Value<bool>("verbose"))
                {
                    this.Info(ev.Player.Name + " has a legit steam account.");
                }
            }
            catch (WebException e)
            {
                if (e.Status == WebExceptionStatus.ProtocolError)
                {
                    response = (HttpWebResponse)e.Response;
                    this.Warn("Steam profile connection error: " + response.StatusCode);
                }
                else
                {
                    this.Warn("Steam profile connection error: " + e.Status.ToString());
                }
            }
            finally
            {
                if (response != null)
                {
                    response.Close();
                }
            }
            return false;
        }

        public bool SSLValidation(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            bool isOk = true;
            // If there are errors in the certificate chain,
            // look at each error to determine the cause.
            if (sslPolicyErrors != SslPolicyErrors.None)
            {
                for (int i = 0; i < chain.ChainStatus.Length; i++)
                {
                    if (chain.ChainStatus[i].Status == X509ChainStatusFlags.RevocationStatusUnknown)
                    {
                        continue;
                    }
                    chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
                    chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
                    chain.ChainPolicy.UrlRetrievalTimeout = new TimeSpan(0, 1, 0);
                    chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllFlags;
                    bool chainIsValid = chain.Build((X509Certificate2)certificate);
                    if (!chainIsValid)
                    {
                        isOk = false;
                        break;
                    }
                }
            }
            return isOk;
        }
    }

    class EnableCommand : ICommandHandler
    {
        private VPNShield plugin;
        public EnableCommand(VPNShield plugin)
        {
            this.plugin = plugin;
        }

        public string GetCommandDescription()
        {
            return "Enables a feature of VPNShield";
        }

        public string GetUsage()
        {
            return "vs_enable [vpn-check|steam-check]";
        }

        public string[] OnCall(ICommandSender sender, string[] args)
        {
            if (args.Length > 0)
            {
                if (args[0] == "vpn-check")
                {
                    plugin.config["block-vpns"] = true;
                    return new string[] { "Blocking of VPNs enabled." };
                }
                else if(args[0] == "steam-check")
                {
                    plugin.config["block-new-steam-accounts"] = true;
                    return new string[] { "Blocking of new Steam accounts enabled." };
                }
            }
            return new string[] { "Invalid arguments, usage: \"" + GetUsage() + "\"" };
        }
    }

    class DisableCommand : ICommandHandler
    {
        private VPNShield plugin;
        public DisableCommand(VPNShield plugin)
        {
            this.plugin = plugin;
        }

        public string GetCommandDescription()
        {
            return "Disables a feature of VPNShield";
        }

        public string GetUsage()
        {
            return "vs_disable [vpn-check|steam-check]";
        }

        public string[] OnCall(ICommandSender sender, string[] args)
        {
            if (args.Length > 0)
            {
                if (args[0] == "vpn-check")
                {
                    plugin.config["block-vpns"] = false;
                    return new string[] { "Blocking of VPNs disabled." };
                }
                else if (args[0] == "steam-check")
                {
                    plugin.config["block-new-steam-accounts"] = false;
                    return new string[] { "Blocking of new Steam accounts disabled." };
                }
            }
            return new string[] { "Invalid arguments, usage: \"" + GetUsage() + "\"" };
        }
    }

    class ReloadCommand : ICommandHandler
    {
        private VPNShield plugin;
        public ReloadCommand(VPNShield plugin)
        {
            this.plugin = plugin;
        }

        public string GetCommandDescription()
        {
            return "Reloads VPNShield";
        }

        public string GetUsage()
        {
            return "vs_reload";
        }

        public string[] OnCall(ICommandSender sender, string[] args)
        {
            plugin.SetUpFileSystem();
            plugin.config = JObject.Parse(File.ReadAllText(FileManager.AppFolder + "VPNShield/config.json"));
            plugin.autoWhitelist = new HashSet<string>(JArray.Parse(File.ReadAllText(FileManager.AppFolder + "VPNShield/auto-whitelist.json")).Values<string>());
            plugin.autoBlacklist = new HashSet<string>(JArray.Parse(File.ReadAllText(FileManager.AppFolder + "VPNShield/auto-blacklist.json")).Values<string>());
            return new string[] { "VPNShield has been reloaded." };
        }
    }

    class SaveData : IEventHandlerWaitingForPlayers
    {
        private VPNShield plugin;
        public SaveData(VPNShield plugin)
        {
            this.plugin = plugin;
        }

        public void OnWaitingForPlayers(WaitingForPlayersEvent ev)
        {
            if(plugin.whitelistUpdated)
            {
                plugin.SaveWhitelistToFile();
                plugin.whitelistUpdated = false;
            }
            if (plugin.blacklistUpdated)
            {
                plugin.SaveBlacklistToFile();
                plugin.blacklistUpdated = false;
            }
        }
    }

    class CheckPlayer : IEventHandlerPlayerJoin
    {
        private VPNShield plugin;
        public CheckPlayer(VPNShield plugin)
        {
            this.plugin = plugin;
        }
        public void OnPlayerJoin(PlayerJoinEvent ev)
        {
            if (ev.Player.GetRankName() != "")
            {
                return;
            }

            if (plugin.CheckSteamAccount(ev))
            {
                return;
            }

            if(plugin.CheckVPN(ev))
            {
                return;
            }
        }
    }
}
