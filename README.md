# VPNShield
An Anti-VPN plugin for servermod. Also features blocking new Steam accounts who have not bought anything on Steam.

# Installation

Extract the release zip and place the contents in `sm_plugins`.

# Config

This plugin has it's own config which is placed in your global config folder when the plugin is run for the first time.

Default config:
```json
{
    "block-vpns": true,
    "iphub-apikey": "put-key-here",
    "strictmode": false,
    "block-new-steam-accounts": true
}
```

`block-vpns` - Turns blocking of VPNs on or off.
`iphub-api-key` - API key required for VPN blocking. Get one here: https://iphub.info/apiKey/newFree
`strictmode` - Turns on stricter VPN blocking. Not recommended as it may flag some players incorrectly.
`block-new-steam-accounts` - Blocks steam users who have not bought anything on steam as they are likely not real accounts.

# Command

`vs_reload` - Reloads the vs config.
