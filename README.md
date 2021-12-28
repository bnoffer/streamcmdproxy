# streamcmdproxy

This is a .NET 6 worker application that allows you to proxy Twitch Chat Commands from Youtube and/or Discord Chat to Twitch Chat. This does not depend on any other third party services like Restream.

## Status

Work in progress:

Connect to Twitch:  (-) | Works, but authentication has to be done manually.

Connect to YouTube: (-) | Works, but the bot runs into a rate limit.

Connect to Discord: (√)

Documentation:      ( )

## Setup

Make a copy of appsettings.Development.json-sample in your working directory and rename it to appsettings.Development.json .  Enter the necessary credentials for testing.

## Usage

Run the application.

## Acknowledgements

This project uses [StephenMP/ChatWell.YouTube](https://github.com/StephenMP/ChatWell.YouTube), [discord-net/Discord.Net](https://github.com/discord-net/Discord.Net) and [TwitchLib/TwitchLib](https://github.com/TwitchLib/TwitchLib) libraries to access Youtube and Twitch chat.

## License

[MIT](https://github.com/bnoffer/streamcmdproxy/blob/master/LICENSE.md)

## Author(s)

[@bnoffer](https://github.com/bnoffer)