using System.Drawing;
using CounterStrikeSharp.API.Core;

namespace SkyboxChanger;

public class Service
{
  public Storage _Storage { get; set; }

  private SkyboxChanger _Plugin { get; set; }

  public Service(SkyboxChanger plugin, PlayerSettings.ISettingsApi? settingsApi)
  {
    _Plugin = plugin;
    _Storage = new Storage(settingsApi);
  }

  public bool SetSkybox(CCSPlayerController player, string index)
  {
    if (_Plugin.SpectatorManager.IsPlayerInSpectatorMode(player.Slot))
    {
      return false;
    }

    var skyData = _Storage.GetPlayerSkydata(player.SteamID);
    skyData.Skybox = index;
    Skybox skybox = _Plugin.Config.Skyboxs[index];
    if (skybox.Brightness != null)
    {
      _Plugin.EnvManager.SetBrightness(player.Slot, skybox.Brightness.Value);
      skyData.Brightness = skybox.Brightness.Value;
    }
    if (skybox.Color != null)
    {
      var colorData = skybox.Color.Split(" ");
      if (colorData.Length == 4)
      {
        var r = int.Parse(colorData[0]);
        var g = int.Parse(colorData[1]);
        var b = int.Parse(colorData[2]);
        var a = int.Parse(colorData[3]);
        _Plugin.EnvManager.SetTintColor(player.Slot, Color.FromArgb(a, r, g, b));
        skyData.Color = Color.FromArgb(a, r, g, b).ToArgb();
      }
    }
    
    // Save immediately after change
    _ = _Storage.SaveAsync(player.SteamID);
    
    return _Plugin.EnvManager.SetSkybox(player.Slot, skybox);
  }

  public void SetBrightness(CCSPlayerController player, float brightness)
  {
    if (_Plugin.SpectatorManager.IsPlayerInSpectatorMode(player.Slot))
    {
      return;
    }

    var skyData = _Storage.GetPlayerSkydata(player.SteamID);
    skyData.Brightness = brightness;
    _Plugin.EnvManager.SetBrightness(player.Slot, brightness);
    
    // Save immediately after change
    _ = _Storage.SaveAsync(player.SteamID);
  }

  public void SetTintColor(CCSPlayerController player, Color color)
  {
    if (_Plugin.SpectatorManager.IsPlayerInSpectatorMode(player.Slot))
    {
      return;
    }

    var skyData = _Storage.GetPlayerSkydata(player.SteamID);
    skyData.Color = color.ToArgb();
    _Plugin.EnvManager.SetTintColor(player.Slot, color);
    
    // Save immediately after change
    _ = _Storage.SaveAsync(player.SteamID);
  }

  public Skybox? GetPlayerSkybox(CCSPlayerController player)
  {
    var skyboxData = _Storage.GetPlayerSkydata(player.SteamID);
    return _Plugin.Config.Skyboxs.GetValueOrDefault(skyboxData.Skybox);
  }

  public float GetPlayerBrightness(CCSPlayerController player)
  {
    return _Storage.GetPlayerSkydata(player.SteamID).Brightness;
  }

  public Color GetPlayerColor(CCSPlayerController player)
  {
    return Color.FromArgb(_Storage.GetPlayerSkydata(player.SteamID).Color);
  }

  public Skybox? GetMapDefaultSkybox(string map)
  {
    var maps = _Plugin.Config.MapDefault;
    if (maps == null) return null;
    if (maps.ContainsKey(map)) return _Plugin.Config.Skyboxs[maps[map]];
    if (maps.ContainsKey("*")) return _Plugin.Config.Skyboxs[maps["*"]];
    return null;
  }

  public void Save(ulong? steamid = null)
  {
    _Storage.Save(steamid);
  }

}