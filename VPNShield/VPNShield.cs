using Newtonsoft.Json.Linq;
using Smod2;
using Smod2.API;
using Smod2.Attributes;
using Smod2.Commands;
using Smod2.Config;
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
using System.Threading.Tasks;

namespace VPNShield
{
	[PluginDetails(
		author = "Karl Essinger",
		name = "VPNShield",
		description = "Blocks users connecting using VPNs.",
		id = "karlofduty.vpnshield",
		version = "3.2.2",
		SmodMajor = 3,
		SmodMinor = 8,
		SmodRevision = 0
	)]
	public class VPNShield : Plugin
	{
		public JObject config;
		public HashSet<string> autoWhitelist;
		public HashSet<string> autoBlacklist;
		public HashSet<string> whitelist;

		public bool autoWhitelistUpdated = false;
		public bool autoBlacklistUpdated = false;

		private readonly string defaultConfig =
		"{\n" +
		"    \"block-vpns\": false,\n" +
		"    \"iphub-apikey\": \"put-key-here\",\n" +
		"    \"block-new-steam-accounts\": true,\n" +
		"    \"block-non-setup-steam-accounts\": true,\n" +
		"    \"no-purchases-kick-message\": \"This server does not allow new Steam accounts, you have to buy something on Steam before playing.\",\n" +
		"    \"non-setup-kick-message\": \"This server does not allow non setup Steam accounts, you have to setup your Steam profile before playing.\",\n" +
		"    \"verbose\": false,\n" +
		"}";

		private readonly string defaultList =
		"[\n" +
		"]";

		public override void OnDisable() {}

		public override void Register()
		{
			this.AddEventHandlers(new CheckPlayer(this));
			this.AddEventHandlers(new SaveData(this));
			this.AddCommand("vs_reload", new ReloadCommand(this));
			this.AddCommand("vs_enable", new EnableCommand(this));
			this.AddCommand("vs_disable", new DisableCommand(this));
			this.AddCommand("vs_whitelist", new WhitelistCommand(this));
			this.AddConfig(new ConfigSetting("vs_global", true, true, "Whether or not to use the global config directory, default is true"));
		}

		public override void OnEnable()
		{
			try
			{
				SetUpFileSystem();
				this.Info("Loading config: " + FileManager.GetAppFolder(true, !GetConfigBool("vs_global")) + "VPNShield/config.json...");
				config = JObject.Parse(File.ReadAllText(FileManager.GetAppFolder(true, !GetConfigBool("vs_global")) + "VPNShield/config.json"));
				this.Info("Loaded config.");
				this.Info("Loading data files from " + FileManager.GetAppFolder(true, !GetConfigBool("vs_global")) + "VPNShield/...");
				autoWhitelist = new HashSet<string>(JArray.Parse(File.ReadAllText(FileManager.GetAppFolder(true, !GetConfigBool("vs_global")) + "VPNShield/auto-whitelist.json")).Values<string>());
				autoBlacklist = new HashSet<string>(JArray.Parse(File.ReadAllText(FileManager.GetAppFolder(true, !GetConfigBool("vs_global")) + "VPNShield/auto-blacklist.json")).Values<string>());
				whitelist = new HashSet<string>(JArray.Parse(File.ReadAllText(FileManager.GetAppFolder(true, !GetConfigBool("vs_global")) + "VPNShield/whitelist.json")).Values<string>());
				this.Info("Loaded data files.");
			}
			catch (Exception e)
			{
				this.Error("Could not load config: " + e.ToString());
			}
		}

		public void SetUpFileSystem()
		{
			if (!Directory.Exists(FileManager.GetAppFolder(true, !GetConfigBool("vs_global")) + "VPNShield"))
			{
				Directory.CreateDirectory(FileManager.GetAppFolder(true, !GetConfigBool("vs_global")) + "VPNShield");
			}

			if (!File.Exists(FileManager.GetAppFolder(true, !GetConfigBool("vs_global")) + "VPNShield/config.json"))
			{
				File.WriteAllText(FileManager.GetAppFolder(true, !GetConfigBool("vs_global")) + "VPNShield/config.json", defaultConfig);
			}

			if (!File.Exists(FileManager.GetAppFolder(true, !GetConfigBool("vs_global")) + "VPNShield/auto-whitelist.json"))
			{
				File.WriteAllText(FileManager.GetAppFolder(true, !GetConfigBool("vs_global")) + "VPNShield/auto-whitelist.json", defaultList);
			}

			if (!File.Exists(FileManager.GetAppFolder(true, !GetConfigBool("vs_global")) + "VPNShield/auto-blacklist.json"))
			{
				File.WriteAllText(FileManager.GetAppFolder(true, !GetConfigBool("vs_global")) + "VPNShield/auto-blacklist.json", defaultList);
			}

			if (!File.Exists(FileManager.GetAppFolder(true, !GetConfigBool("vs_global")) + "VPNShield/whitelist.json"))
			{
				File.WriteAllText(FileManager.GetAppFolder(true, !GetConfigBool("vs_global")) + "VPNShield/whitelist.json", defaultList);
			}
		}

		public void SaveWhitelistToFile()
		{
			// Save the state to file
			StringBuilder builder = new StringBuilder();
			builder.Append("[\n");
			foreach (string line in whitelist)
			{
				builder.Append("    \"" + line + "\"," + "\n");
			}
			builder.Append("]\n");
			File.WriteAllText(FileManager.GetAppFolder(true, !GetConfigBool("vs_global")) + "VPNShield/whitelist.json", builder.ToString());
		}

		public void SaveAutoWhitelistToFile()
		{
			// Save the state to file
			StringBuilder builder = new StringBuilder();
			builder.Append("[\n");
			foreach (string line in autoWhitelist)
			{
				builder.Append("    \"" + line + "\"," + "\n");
			}
			builder.Append("]\n");
			File.WriteAllText(FileManager.GetAppFolder(true, !GetConfigBool("vs_global")) + "VPNShield/auto-whitelist.json", builder.ToString());
		}

		public void SaveAutoBlacklistToFile()
		{
			// Save the state to file
			StringBuilder builder = new StringBuilder();
			builder.Append("[\n");
			foreach (string line in autoBlacklist)
			{
				builder.Append("    \"" + line + "\"," + "\n");
			}
			builder.Append("]\n");
			File.WriteAllText(FileManager.GetAppFolder(true, !GetConfigBool("vs_global")) + "VPNShield/auto-blacklist.json", builder.ToString());
		}

		public bool CheckVPN(PlayerJoinEvent ev)
		{
			if (!config.Value<bool>("block-vpns"))
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
					autoWhitelistUpdated = true;
				}
				else if (verificationLevel == 1)
				{
					this.Info(ev.Player.Name + " is using a VPN.");
					autoBlacklist.Add(ipAddress);
					autoBlacklistUpdated = true;
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
					if ((int)response.StatusCode == 429)
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
				response?.Close();
			}
			return false;
		}

		public bool CheckSteamAccount(PlayerJoinEvent ev)
		{
			if (ev.Player.UserIdType != UserIdType.STEAM) return false;

			ServicePointManager.ServerCertificateValidationCallback = SSLValidation;
			HttpWebResponse response = null;
			try
			{
				HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://steamcommunity.com/profiles/" + ev.Player.GetParsedUserID() + "?xml=1");
				request.Method = "GET";

				response = (HttpWebResponse)request.GetResponse();

				string xmlResponse = new StreamReader(response.GetResponseStream()).ReadToEnd();

				string[] foundStrings = xmlResponse.Split('\n').Where(w => w.Contains("isLimitedAccount")).ToArray();

				if (foundStrings.Length == 0)
				{
					if (config.Value<bool>("block-non-setup-steam-accounts"))
					{
						this.Info(ev.Player.Name + " has a non setup steam account.");
						ev.Player.Ban(0, config.Value<string>("non-setup-kick-message"));
						return true;
					}
					else
					{
						this.Error("Steam account check failed. Their profile did not have the required information.");
						return false;
					}
				}

				bool isLimitedAccount = foundStrings[0].Where(c => char.IsDigit(c)).ToArray()[0] != '0';
				if (isLimitedAccount)
				{
					this.Info(ev.Player.Name + " has a new steam account with no purchases.");
					if (config.Value<bool>("block-new-steam-accounts"))
					{
						ev.Player.Ban(0, config.Value<string>("no-purchases-kick-message"));
						return true;
					}
				}
				else if (config.Value<bool>("verbose"))
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
				response?.Close();
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

	internal class EnableCommand : ICommandHandler
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
					if (sender is Player player)
					{
						if (!player.HasPermission("vpnshield.enable.vpn"))
						{
							return new[] { "You don't have permission to use that command." };
						}
					}
					plugin.config["block-vpns"] = true;
					return new[] { "Blocking of VPNs enabled." };
				}
				else if (args[0] == "steam-check")
				{
					if (sender is Player player)
					{
						if (!player.HasPermission("vpnshield.enable.steam"))
						{
							return new[] { "You don't have permission to use that command." };
						}
					}
					plugin.config["block-new-steam-accounts"] = true;
					return new[] { "Blocking of new Steam accounts enabled." };
				}
			}
			return new[] { "Invalid arguments, usage: \"" + GetUsage() + "\"" };
		}
	}

	internal class DisableCommand : ICommandHandler
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
					if (sender is Player player)
					{
						if (!player.HasPermission("vpnshield.disable.vpn"))
						{
							return new[] { "You don't have permission to use that command." };
						}
					}
					plugin.config["block-vpns"] = false;
					return new[] { "Blocking of VPNs disabled." };
				}
				else if (args[0] == "steam-check")
				{
					if (sender is Player player)
					{
						if (!player.HasPermission("vpnshield.disable.steam"))
						{
							return new[] { "You don't have permission to use that command." };
						}
					}
					plugin.config["block-new-steam-accounts"] = false;
					return new[] { "Blocking of new Steam accounts disabled." };
				}
			}
			return new[] { "Invalid arguments, usage: \"" + GetUsage() + "\"" };
		}
	}

	internal class WhitelistCommand : ICommandHandler
	{
		private VPNShield plugin;

		public WhitelistCommand(VPNShield plugin)
		{
			this.plugin = plugin;
		}

		public string GetCommandDescription()
		{
			return "Whitelists a player's UserID";
		}

		public string GetUsage()
		{
			return "vs_whitelist <UserID>";
		}

		public string[] OnCall(ICommandSender sender, string[] args)
		{
			if (sender is Player player)
			{
				if (!player.HasPermission("vpnshield.whitelist"))
				{
					return new[] { "You don't have permission to use that command." };
				}
			}

			if (args.Length > 0)
			{
				if (plugin.whitelist.Contains(args[0]))
				{
					plugin.whitelist.Remove(args[0]);
					plugin.SaveWhitelistToFile();
					return new[] { "Player removed from whitelist." };
				}
				else
				{
					plugin.whitelist.Add(args[0]);
					plugin.SaveWhitelistToFile();
					return new[] { "Player added to whitelist." };
				}
			}
			return new[] { "Invalid arguments, usage: \"" + GetUsage() + "\"" };
		}
	}

	internal class ReloadCommand : ICommandHandler
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
			if (sender is Player player)
			{
				if (!player.HasPermission("vpnshield.reload"))
				{
					return new[] { "You don't have permission to use that command." };
				}
			}

			plugin.SetUpFileSystem();
			plugin.config = JObject.Parse(File.ReadAllText(FileManager.GetAppFolder(true, !plugin.GetConfigBool("vs_global")) + "VPNShield/config.json"));
			plugin.autoWhitelist = new HashSet<string>(JArray.Parse(File.ReadAllText(FileManager.GetAppFolder(true, !plugin.GetConfigBool("vs_global")) + "VPNShield/auto-whitelist.json")).Values<string>());
			plugin.autoBlacklist = new HashSet<string>(JArray.Parse(File.ReadAllText(FileManager.GetAppFolder(true, !plugin.GetConfigBool("vs_global")) + "VPNShield/auto-blacklist.json")).Values<string>());
			plugin.whitelist = new HashSet<string>(JArray.Parse(File.ReadAllText(FileManager.GetAppFolder(true, !plugin.GetConfigBool("vs_global")) + "VPNShield/whitelist.json")).Values<string>());
			return new[] { "VPNShield has been reloaded." };
		}
	}

	internal class SaveData : IEventHandlerWaitingForPlayers
	{
		private VPNShield plugin;

		public SaveData(VPNShield plugin)
		{
			this.plugin = plugin;
		}

		public void OnWaitingForPlayers(WaitingForPlayersEvent ev)
		{
			if (plugin.autoWhitelistUpdated)
			{
				plugin.SaveAutoWhitelistToFile();
				plugin.autoWhitelistUpdated = false;
			}
			if (plugin.autoBlacklistUpdated)
			{
				plugin.SaveAutoBlacklistToFile();
				plugin.autoBlacklistUpdated = false;
			}
		}
	}

	internal class CheckPlayer : IEventHandlerPlayerJoin
	{
		private VPNShield plugin;

		public CheckPlayer(VPNShield plugin)
		{
			this.plugin = plugin;
		}

		public void OnPlayerJoin(PlayerJoinEvent ev)
		{
			new Task(() =>
			{
				if (ev.Player.GetRankName() != "")
				{
					return;
				}

				if (ev.Player.HasPermission("vpnshield.exempt"))
				{
					return;
				}

				if (plugin.whitelist.Contains(ev.Player.UserId))
				{
					return;
				}

				if (plugin.CheckSteamAccount(ev))
				{
					return;
				}

				if (plugin.CheckVPN(ev))
				{
					return;
				}
			}).Start();
		}
	}
}