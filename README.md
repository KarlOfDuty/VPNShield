# VPNShield
An Anti-VPN plugin for servermod. Also features blocking new Steam accounts who have not bought anything on Steam. Users with server ranks are ignored.

# Installation

Extract the release zip and place the contents in `sm_plugins`.

# Config

This plugin has it's own config which is placed in your global config folder when the plugin is run for the first time.

Default config:
```json
{
    "block-vpns": true,
    "iphub-apikey": "key-here",
    "block-new-steam-accounts": true,
    "verbose":false
}
```

`block-vpns` - Turns blocking of VPNs on or off.

`iphub-api-key` - API key required for VPN blocking. Get one here: https://iphub.info/apiKey/newFree

`block-new-steam-accounts` - Blocks steam users who have not bought anything on steam as they are likely not real accounts.

`verbose` - Sends more console messages.

# Command

`vs_reload` - Reloads the vs config.

`vs_enable` - Enables features of the plugin.

`vs_disable` - Disables features of the plugin.

`vs_whitelist` - Whitelists a user by steamid.

# Permissions

| Permission | Description |
|----------  |-----------  |
| `vpnshield.exempt` | Makes a player exempt from the checks. |
| `vpnshield.reload` | Lets a player reload the plugin. |
| `vpnshield.enable.vpn` | Lets a player enable the vpn check. |
| `vpnshield.enable.steam` | Lets a player toggle the steam account check. |
| `vpnshield.disable.vpn` | Lets a player disable the vpn check. |
| `vpnshield.disable.steam` | Lets a player disable the steam account check. |
| `vpnshield.whitelist` | Lets a player add players to the whitelist. |
