using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.UserMessages;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using MenuManager;

namespace SkyboxChanger;

public class SkyboxChanger : BasePlugin, IPluginConfig<SkyboxConfig>
{
  public override string ModuleName => "Skybox Changer";
  public override string ModuleVersion => "1.3.4";
  public override string ModuleAuthor => "samyyc (fork by luca.uy)";

  public SkyboxConfig Config { get; set; } = new();

  public required EnvManager EnvManager { get; set; } = new();

  public required Service Service { get; set; }

  public required SpectatorSkyboxManager SpectatorManager { get; set; }

  // MenuManager capability
  private IMenuApi? _menuApi;
  private readonly PluginCapability<IMenuApi?> _menuCapability = new("menu:nfcore");

  private static SkyboxChanger? _Instance { get; set; }

  public override unsafe void Load(bool hotReload)
  {
    if (hotReload)
    {
      Logger.LogError("HOT RELOAD DETECTED. It's NOT recommended to hot reload this plugin, please restart your server.");
    }
    KvLib.SetDllImportResolver();
    MemoryManager.Load();
    _Instance = this;

    SpectatorManager = new SpectatorSkyboxManager(this);

    RegisterListener<Listeners.OnServerPrecacheResources>(OnServerPrecacheResources);
    RegisterListener<Listeners.CheckTransmit>(OnCheckTransmit);
    RegisterListener<Listeners.OnMapStart>((map) =>
    {
      if (!Config.Skyboxs.ContainsKey(""))
      {
        var skybox = Service.GetMapDefaultSkybox(map);
        if (skybox != null)
        {
          var defaultSkybox = new Skybox
          {
            Name = Localizer["menu.defaultskybox"],
            Material = skybox.Material,
          };
          Config.Skyboxs.Add("", defaultSkybox);
          EnvManager.DefaultMaterial = skybox.Material;
        }
      }
      SpectatorManager.Initialize();
    });
    RegisterListener<Listeners.OnMapEnd>(() =>
    {
      SpectatorManager.Shutdown();
      EnvManager.Shutdown();
      Service.Save();
      MemoryManager.RemoveCachedFactory();
    });
    RegisterListener<Listeners.OnServerPreFatalShutdown>(() =>
    {
      SpectatorManager.Shutdown();
      Service.Save();
    });
    RegisterListener<Listeners.OnEntityCreated>((entity) =>
    {
      Server.NextFrame(() =>
      {
        if (entity.DesignerName == "env_cubemap_fog")
        {
          CEnvCubemapFog fog = new CEnvCubemapFog(entity.Handle);
          EnvManager.CubemapFogPointedSkyName = "[PR#]" + fog.SkyEntity;
        }
        if (entity.DesignerName == "env_sky")
        {
          CEnvSky sky = new CEnvSky(entity.Handle);
          if (entity.PrivateVScripts == null || !entity.PrivateVScripts.StartsWith("skyboxchanger_"))
          {
            if (!Config.Skyboxs.ContainsKey(""))
            {
              nint materialptr = *(IntPtr*)sky.SkyMaterial.Value;
              var GetMaterialName = VirtualFunction.Create<IntPtr, string>(materialptr, 0);
              string skyMaterial = GetMaterialName.Invoke(materialptr);
              EnvManager.DefaultMaterial = skyMaterial;
              Config.Skyboxs.Add(
                "",
                new Skybox { Name = Localizer["menu.defaultskybox"], Material = skyMaterial }
              );
            }
            sky.Remove();
          }
          else
          {
            EnvManager.SpawnedSkyboxes.Add(int.Parse(entity.PrivateVScripts.Replace("skyboxchanger_", "")), (int)entity.Index);
          }
        }
      });
    });
    RegisterEventHandler<EventPlayerConnectFull>((@event, info) =>
    {
      var slot = @event.Userid!.Slot;
      var player = @event.Userid!;
      Server.NextFrame(() =>
      {
        foreach (var sky in Utilities.FindAllEntitiesByDesignerName<CEnvSky>("env_sky"))
        {
          if (Helper.IsPlayerSkybox(slot, sky))
          {
            sky.Remove();
            EnvManager.SpawnedSkyboxes.Remove(slot);
          }
        }
        EnvManager.InitializeSkyboxForPlayer(player);
      });
      return HookResult.Continue;
    });
    RegisterListener<Listeners.OnClientDisconnect>(slot =>
    {
      EnvManager.OnPlayerLeave(slot);
      SpectatorManager.OnPlayerDisconnect(slot);
      Service.Save(Utilities.GetPlayerFromSlot(slot)?.SteamID);
    });
    Helper.Initialize();
  }

  public override void OnAllPluginsLoaded(bool hotReload)
  {
    _menuApi = _menuCapability.Get();

    if (_menuApi == null)
    {
      Console.ForegroundColor = ConsoleColor.Red;
      Console.WriteLine("[SkyboxChanger] CRITICAL ERROR: MenuManager API not found!");
      Console.WriteLine("[SkyboxChanger] MenuManager is a required dependency for this plugin to function.");
      Console.WriteLine("[SkyboxChanger] Please install MenuManagerCS2 from: https://github.com/NickFox007/MenuManagerCS2");
      Console.WriteLine("[SkyboxChanger] Plugin will now unload automatically.");
      Console.ResetColor();

      Server.NextFrame(() =>
      {
        try
        {
          Server.ExecuteCommand($"css_plugins unload {ModuleName}");
        }
        catch (Exception ex)
        {
          Console.WriteLine($"[SkyboxChanger] Error during auto-unload: {ex.Message}");
        }
      });

      return;
    }
  }

  private void OnCheckTransmit(CCheckTransmitInfoList infoList)
  {
    EnvManager.OnCheckTransmit(infoList);
  }

  public override void Unload(bool hotReload)
  {
    if (_menuApi != null)
    {
      foreach (var player in Utilities.GetPlayers().Where(p => p.IsValid))
      {
        _menuApi.CloseMenu(player);
      }
    }

    SpectatorManager.Shutdown();
    Service.Save();
    MemoryManager.Unload();
    _menuApi = null;
  }

  public static SkyboxChanger GetInstance()
  {
    if (_Instance == null)
    {
      throw new Exception("SkyboxChanger is not loaded");
    }

    return _Instance;
  }


  public void OnConfigParsed(SkyboxConfig config)
  {
    Config = config;
    if (Config.Version == 1)
    {
      throw new Exception("Please update your config version. Changed: 'Database' field.");
    }
    Service = new Service(this, Config.Database.Host, Config.Database.Port, Config.Database.User, Config.Database.Password, Config.Database.Database, Config.Database.TablePrefix);
  }

  public void OnServerPrecacheResources(ResourceManifest manifest)
  {
    foreach (var skybox in Config.Skyboxs)
    {
      if (skybox.Value.Name == "")
      {
        skybox.Value.Name = skybox.Key;
      }
      manifest.AddResource(skybox.Value.Material);
    }
  }

  [ConsoleCommand("css_sky")]
  [ConsoleCommand("css_skybox")]
  [CommandHelper(0, "Change skybox", CommandUsage.CLIENT_ONLY)]
  public unsafe void SkyboxCommand(CCSPlayerController player, CommandInfo info)
  {
    if (_menuApi == null)
    {
      player.PrintToChat($"[{ChatColors.Green}SkyboxChanger{ChatColors.Default}] {ChatColors.Red}MenuManager is not available!");
      return;
    }

    if (Config.MenuPermission != "" && Config.MenuPermission != null && !AdminManager.PlayerHasPermissions(player, [Config.MenuPermission]))
    {
      return;
    }

    if (SpectatorManager.IsPlayerInSpectatorMode(player.Slot))
    {
      return;
    }

    ShowMainMenu(player);
  }

  [ConsoleCommand("css_skybox_restore")]
  [CommandHelper(1, "Force restore player skybox", CommandUsage.CLIENT_AND_SERVER)]
  [RequiresPermissions("@css/admin")]
  public void RestoreSkyboxCommand(CCSPlayerController? caller, CommandInfo info)
  {
    if (info.ArgCount < 2)
    {
      info.ReplyToCommand("Usage: css_skybox_restore <player_name_or_slot>");
      return;
    }

    var targetIdentifier = info.GetArg(1);
    CCSPlayerController? targetPlayer = null;

    if (int.TryParse(targetIdentifier, out int slot))
    {
      targetPlayer = Utilities.GetPlayerFromSlot(slot);
    }

    if (targetPlayer == null)
    {
      targetPlayer = Utilities.GetPlayers()
        .FirstOrDefault(p => p.IsValid && p.PlayerName.Contains(targetIdentifier, StringComparison.OrdinalIgnoreCase));
    }

    if (targetPlayer == null)
    {
      info.ReplyToCommand($"Player '{targetIdentifier}' not found.");
      return;
    }

    SpectatorManager.ForceRestorePlayer(targetPlayer.Slot);
    info.ReplyToCommand($"Forced skybox restoration for {targetPlayer.PlayerName}");
  }

  private void ShowMainMenu(CCSPlayerController player)
  {
    if (_menuApi == null) return;

    var mainMenu = _menuApi.GetMenu(Localizer["menu.title"]);

    mainMenu.AddMenuOption(Localizer["menu.skybox"], (p, option) =>
    {
      ShowSkyboxMenu(p);
    });

    mainMenu.AddMenuOption(Localizer["menu.brightness"], (p, option) =>
    {
      ShowBrightnessMenu(p);
    });

    mainMenu.AddMenuOption(Localizer["menu.tintcolor"], (p, option) =>
    {
      ShowColorMenu(p);
    });

    mainMenu.Open(player);
  }

  private void ShowSkyboxMenu(CCSPlayerController player)
  {
    if (_menuApi == null) return;

    if (SpectatorManager.IsPlayerInSpectatorMode(player.Slot))
    {
      player.PrintToChat(Localizer["spectator.cannot_change"]);
      return;
    }

    var skyboxMenu = _menuApi.GetMenu(Localizer["menu.title"]);

    var skyboxes = Config.Skyboxs.ToList();
    skyboxes.RemoveAll(kv => kv.Key == "");
    if (Config.Skyboxs.ContainsKey(""))
    {
      var def = Config.Skyboxs[""];
      skyboxes.Insert(0, new KeyValuePair<string, Skybox>("", def));
    }

    skyboxes.ForEach(skybox =>
    {
      if (!Helper.PlayerHasPermission(player, skybox.Value.Permissions, skybox.Value.PermissionsOr)) return;

      skyboxMenu.AddMenuOption(skybox.Value.Name, (p, option) =>
      {
        var result = Service.SetSkybox(p, skybox.Key);
        if (result)
        {
          p.PrintToChat(Localizer["change.success"]);
        }
        else
        {
          p.PrintToChat(Localizer["change.failed"]);
        }
        // _menuApi?.CloseMenu(p);
      });
    });

    skyboxMenu.AddMenuOption("← " + Localizer["menu.back"], (p, option) =>
    {
      ShowMainMenu(p);
    });

    skyboxMenu.Open(player);
  }

  private void ShowBrightnessMenu(CCSPlayerController player)
  {
    if (_menuApi == null) return;

    if (SpectatorManager.IsPlayerInSpectatorMode(player.Slot))
    {
      player.PrintToChat(Localizer["spectator.cannot_change"]);
      return;
    }

    var brightnessMenu = _menuApi.GetMenu(Localizer["menu.brightness"]);

    float currentBrightness = Service.GetPlayerBrightness(player);

    brightnessMenu.AddMenuOption("-- (- 0.5)", (p, option) =>
    {
      float newValue = Math.Max(0.0f, currentBrightness - 0.5f);
      Service.SetBrightness(p, newValue);
      ShowBrightnessMenu(p);
    });

    brightnessMenu.AddMenuOption("- (- 0.1)", (p, option) =>
    {
      float newValue = Math.Max(0.0f, currentBrightness - 0.1f);
      Service.SetBrightness(p, newValue);
      ShowBrightnessMenu(p);
    });

    brightnessMenu.AddMenuOption($"{Localizer["menu.current"]}: {currentBrightness:F1}", (p, option) =>
    {
      // Do nothing, just display
    });

    brightnessMenu.AddMenuOption("+ (+ 0.1)", (p, option) =>
    {
      float newValue = Math.Min(10.0f, currentBrightness + 0.1f);
      Service.SetBrightness(p, newValue);
      ShowBrightnessMenu(p);
    });

    brightnessMenu.AddMenuOption("++ (+ 0.5)", (p, option) =>
    {
      float newValue = Math.Min(10.0f, currentBrightness + 0.5f);
      Service.SetBrightness(p, newValue);
      ShowBrightnessMenu(p);
    });

    brightnessMenu.AddMenuOption("← " + Localizer["menu.back"], (p, option) =>
    {
      ShowMainMenu(p);
    });

    brightnessMenu.Open(player);
  }

  private void ShowColorMenu(CCSPlayerController player)
  {
    if (_menuApi == null) return;

    if (SpectatorManager.IsPlayerInSpectatorMode(player.Slot))
    {
      player.PrintToChat(Localizer["spectator.cannot_change"]);
      return;
    }

    var colorMenu = _menuApi.GetMenu(Localizer["menu.tintcolor"]);

    foreach (var knownColor in (KnownColor[])Enum.GetValues(typeof(KnownColor)))
    {
      if (Color.FromKnownColor(knownColor).IsSystemColor) continue;

      colorMenu.AddMenuOption(knownColor.ToString(), (p, option) =>
      {
        Service.SetTintColor(p, Color.FromKnownColor(knownColor));
        // _menuApi?.CloseMenu(p);
      });
    }

    colorMenu.AddMenuOption("← " + Localizer["menu.back"], (p, option) =>
    {
      ShowMainMenu(p);
    });

    colorMenu.Open(player);
  }
}