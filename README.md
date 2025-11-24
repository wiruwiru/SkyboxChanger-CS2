# SkyboxChanger-CS2
![](https://img.shields.io/badge/build-passing-brightgreen) ![](https://img.shields.io/github/downloads/wiruwiru/CS2-SkyboxChanger/total
) ![](https://img.shields.io/github/stars/wiruwiru/CS2-SkyboxChanger?style=flat&logo=github
) ![](https://img.shields.io/github/license/wiruwiru/CS2-SkyboxChanger
)

A CounterStrikeSharp plugin that allows players to change their own skybox material, color and brightness on every map dynamically and seamlessly.
![Preview](https://github.com/user-attachments/assets/57b31334-696e-4ca6-8287-ed9b2121f1a3)

## Installation
Follow these steps to install the CS2-SkyboxChanger plugin:

### Prerequisites
1. Ensure you have the following dependencies installed on your server:
   - [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp)
   - [Metamod:Source](https://www.sourcemm.net/downloads.php/?branch=master)
   - [MenuManagerCS2](https://github.com/NickFox007/MenuManagerCS2) (required dependency)
   - [PlayerSettingsCS2](https://github.com/NickFox007/PlayerSettingsCS2/releases/latest) (required dependency)

### Installation Steps
1. Download the latest release of SkyboxChanger:
   - Get [SkyboxChanger.zip](https://github.com/wiruwiru/SkyboxChanger-CS2/releases/latest) from the Releases section

2. Extract the ZIP archive and upload the contents to your game server

3. Start/Restart your server to:
   - Generate the automatic configuration file (`config.json`)
   - Verify the plugin loads correctly

4. Configure the plugin:
   - Edit the generated `config.json` file:
     - Customize available skyboxes (add/remove as needed)
     - Adjust permissions if needed (default: `@skybox/change`)

5. Finalize the installation:
   - Restart your server to apply all changes
   - Verify the plugin is working by using the `!sky` or `!skybox` command in-game (requires permission)

## Commands
- `!sky` or `!skybox` - Opens the skybox selection menu (requires `@skybox/change` permission)

## Configurations
You can customize available skyboxes and permissions in the `config.json` file. The default configuration includes several examples. For advanced configuration options, please check the [Wiki](https://github.com/samyycX/CS2-SkyboxChanger/wiki).