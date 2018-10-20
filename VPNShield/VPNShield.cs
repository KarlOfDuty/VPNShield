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
using System.Xml;

namespace VPNShield
{
    [PluginDetails(
        author = "Karl Essinger",
        name = "VPNShield",
        description = "Blocks users connecting using VPNs.",
        id = "karlofduty.vpnshield",
        version = "2.0.0",
        SmodMajor = 3,
        SmodMinor = 1,
        SmodRevision = 19
    )]
    public class VPNShield : Plugin
    {
        public JObject config;

        readonly string defaultConfig =
        "{\n"                                       +
        "    \"block-vpns\": true,\n"               +
        "    \"iphub-apikey\": \"put-key-here\",\n" +
        "    \"strictmode\": false,\n"              +
        "    \"block-new-steam-accounts\": true,\n" +
        "}";

        public override void OnDisable()
        {

        }

        public override void Register()
        {
            this.AddEventHandlers(new CheckPlayer(this), Priority.High);
            this.AddCommand("vs_reload", new ReloadCommand(this));
        }

        public override void OnEnable()
        {
            if (!File.Exists(FileManager.AppFolder + "VPNShield/config.json"))
            {
                if (!Directory.Exists(FileManager.AppFolder + "VPNShield"))
                    Directory.CreateDirectory(FileManager.AppFolder + "VPNShield");
                File.WriteAllText(FileManager.AppFolder + "VPNShield/config.json", defaultConfig);
            }
            config = JObject.Parse(File.ReadAllText(FileManager.AppFolder + "VPNShield/config.json"));
            this.Info("VPNShield enabled.");
        }

        public bool CheckVPN(PlayerJoinEvent ev)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://v2.api.iphub.info/ip/" + ev.Player.IpAddress.Replace("::ffff:", ""));
            request.Headers.Add("x-key", "MjgzMDp3cmdVQzk0R0ZCQ1hadEhubXVMZWFKZG9HSW5GWmVrbA==");

            HttpWebResponse response = (HttpWebResponse)request.GetResponse();

            string responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();
            JObject json = JObject.Parse(responseString);

            int verificationLevel = json.Value<int>("block");
            switch (verificationLevel)
            {
                case 0:
                    this.Info(ev.Player.Name + " is not using a detectable VPN.");
                    break;
                case 1:
                    this.Info(ev.Player.Name + " is using a VPN.");
                    if (config.Value<bool>("block-vpn"))
                    {
                        ev.Player.Ban(0, "This server does not allow VPNs.");
                        return true;
                    }
                    break;
                case 2:
                    this.Info(ev.Player.Name + " is possibly using a VPN.");
                    if (config.Value<bool>("block-vpn") && config.Value<bool>("strictmode"))
                    {
                        ev.Player.Ban(0, "This server does not allow VPNs.");
                        return true;
                    }
                    break;
            }
            return false;
        }

        public bool CheckSteamAccount(PlayerJoinEvent ev)
        {
            ServicePointManager.ServerCertificateValidationCallback = SSLValidation;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://steamcommunity.com/profiles/" + ev.Player.SteamId + "?xml=1");
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();

            string xmlResponse = new StreamReader(response.GetResponseStream()).ReadToEnd();

            string[] foundStrings = xmlResponse.Split('\n').Where(w => w.Contains("isLimitedAccount")).ToArray();

            if(foundStrings.Length == 0)
            {
                this.Error("Steam account check failed.");
                return false;
            }

            bool isLimitedAccount = foundStrings[0].Where(c => char.IsDigit(c)).ToArray()[0] != '0';
            if (isLimitedAccount && config.Value<bool>("block-new-steam-accounts"))
            {
                this.Info(ev.Player.Name + " has a new steam account with no games bought on it.");
                ev.Player.Ban(0, "This server does not allow new Steam accounts, you have to buy something on Steam before playing.");
                return true;
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
    class ReloadCommand : ICommandHandler
    {
        private VPNShield plugin;
        public ReloadCommand(VPNShield plugin)
        {
            this.plugin = plugin;
        }

        public string GetCommandDescription()
        {
            return "Reloads the JSON config of VPNShield";
        }

        public string GetUsage()
        {
            return "vs_reload";
        }

        public string[] OnCall(ICommandSender sender, string[] args)
        {
            plugin.config = JObject.Parse(File.ReadAllText(FileManager.AppFolder + "VPNShield/config.json"));
            return new string[] { "VPNShield JSON config has been reloaded." };
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
            if(ev.Player.GetRankName() != "")
            {
                return;
            }

            if(plugin.CheckVPN(ev))
            {
                return;
            }
            plugin.CheckSteamAccount(ev);
        }
    }
}
