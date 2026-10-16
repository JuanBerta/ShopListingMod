# Shop Listing

An original [Eco](https://play.eco/) mod (0.14.2) that lets you build a personal shopping list and see, in real time, which player stores sell each item, at what price, how much stock they have and how far away they are.

> **Author:** Beinded (JuanBerta) · Original mod, not a rework.

## Features

- **`/sl add <item> <amount>`** — add an item to your shopping list with the amount you need. The mod immediately scans every store in the world and shows you, in chat, all the stores currently selling that item: price, stock and distance from your current position.
- **`/sl list`** — shows your full shopping list and re-scans the world in real time, reporting for each item the best current deal (lowest price, closest tiebreak), the number of stores selling it and the distance to each.
- **`/sl remove <item|index>`** — remove an entry by item name or by its list index (1-based).
- **`/sl clear`** — empty your shopping list.
- **`/sl help`** — list all available commands.

Your shopping list is persisted per server and survives restarts.

## Installation

1. Download the latest release zip from the [Releases](../../releases) page (or publish the project yourself, see below).
2. Extract the archive into your Eco server's `Mods` folder (the zip already contains the `DLL`, `modinfo.json` and `Locales` folder).
3. Restart the server (or reload mods if supported). Join the game and run `/sl help`.

## Building

```bash
dotnet build -c Debug
dotnet publish -c Release
```

The project targets `net10.0` and references the official `Eco.ReferenceAssemblies` package. Build output lands in `bin/Release/net10.0/`.

## Commands

| Command | Description |
| --- | --- |
| `/sl add <item> <amount>` | Add an item to the list and show current sellers with price, stock and distance |
| `/sl list` | Show the list with real-time best deals per item |
| `/sl remove <item\|index>` | Remove an entry by item name or list index |
| `/sl clear` | Remove every entry |
| `/sl help` | Show command help |

## License

This project is released for the Eco modding community. Feel free to use, modify and share it.
