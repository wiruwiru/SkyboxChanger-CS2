using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using PlayerSettings;

namespace SkyboxChanger;

public class SkyData
{
  public ulong SteamID { get; set; }
  public string Skybox { get; set; } = "";
  public float Brightness { get; set; } = 1.0f;
  public int Color { get; set; } = int.MaxValue;

  public bool HasSkybox()
  {
    return Skybox != "";
  }
}

public class Storage
{
  private const string SettingsKey = "skyboxchanger_settings";
  private readonly ISettingsApi? _settingsApi;
  private readonly Dictionary<ulong, SkyData> _PlayerStorage = new();

  public Storage(ISettingsApi? settingsApi)
  {
    _settingsApi = settingsApi;
  }

  public async Task<SkyData> GetPlayerSkydataAsync(ulong steamid)
  {
    if (_PlayerStorage.TryGetValue(steamid, out var cachedData))
    {
      return cachedData;
    }

    // Try to load from PlayerSettingsApi
    var player = await GetPlayerBySteamIdAsync(steamid);
    if (player != null && _settingsApi != null)
    {
      var jsonValue = await GetPlayerSettingsValueAsync(player, SettingsKey, "{}");

      if (!string.IsNullOrWhiteSpace(jsonValue) && jsonValue != "{}")
      {
        try
        {
          // Handle escaped JSON strings
          string unescapedJson = jsonValue;
          if (jsonValue.Contains("\\\""))
          {
            if (jsonValue.StartsWith("\"") && jsonValue.EndsWith("\""))
            {
              try
              {
                unescapedJson = JsonSerializer.Deserialize<string>(jsonValue) ?? "{}";
              }
              catch
              {
                unescapedJson = jsonValue.Trim('"').Replace("\\\"", "\"").Replace("\\\\", "\\");
              }
            }
            else
            {
              try
              {
                var wrappedJson = $"\"{jsonValue}\"";
                unescapedJson = JsonSerializer.Deserialize<string>(wrappedJson) ?? "{}";
              }
              catch
              {
                unescapedJson = jsonValue.Replace("\\\\", "\u0001").Replace("\\\"", "\"").Replace("\u0001", "\\");
              }
            }
          }
          else if (jsonValue.StartsWith("\"") && jsonValue.EndsWith("\""))
          {
            try
            {
              unescapedJson = JsonSerializer.Deserialize<string>(jsonValue) ?? "{}";
            }
            catch
            {
              unescapedJson = jsonValue.Trim('"');
            }
          }

          if (!string.IsNullOrWhiteSpace(unescapedJson) && unescapedJson != "{}")
          {
            // Deserialize to anonymous type first, then create SkyData
            var deserialized = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(unescapedJson);
            if (deserialized != null)
            {
              var data = new SkyData
              {
                SteamID = steamid,
                Skybox = deserialized.TryGetValue("Skybox", out var skyboxEl) ? skyboxEl.GetString() ?? "" : "",
                Brightness = deserialized.TryGetValue("Brightness", out var brightnessEl) ? brightnessEl.GetSingle() : 1.0f,
                Color = deserialized.TryGetValue("Color", out var colorEl) ? colorEl.GetInt32() : int.MaxValue
              };
              _PlayerStorage[steamid] = data;
              return data;
            }
          }
        }
        catch (Exception ex)
        {
          Console.WriteLine($"[SkyboxChanger] Failed to deserialize settings for player {steamid}: {ex.Message}");
        }
      }
    }

    // Return default data if not found
    var defaultData = new SkyData { SteamID = steamid };
    _PlayerStorage[steamid] = defaultData;
    return defaultData;
  }

  public SkyData GetPlayerSkydata(ulong steamid)
  {
    if (_PlayerStorage.TryGetValue(steamid, out var cachedData))
    {
      return cachedData;
    }

    var defaultData = new SkyData { SteamID = steamid };
    _PlayerStorage[steamid] = defaultData;
    return defaultData;
  }

  public async Task SaveAsync(ulong? steamid = null)
  {
    if (_settingsApi == null)
    {
      return;
    }

    if (steamid == null)
    {
      // Save all cached players
      foreach (var kvp in _PlayerStorage)
      {
        await SavePlayerDataAsync(kvp.Key, kvp.Value);
      }
    }
    else
    {
      if (_PlayerStorage.TryGetValue(steamid.Value, out var data))
      {
        await SavePlayerDataAsync(steamid.Value, data);
      }
    }
  }

  public void Save(ulong? steamid = null)
  {
    // Synchronous wrapper for backward compatibility
    Task.Run(async () => await SaveAsync(steamid));
  }

  private async Task SavePlayerDataAsync(ulong steamid, SkyData data)
  {
    if (_settingsApi == null)
    {
      return;
    }

    var player = await GetPlayerBySteamIdAsync(steamid);
    if (player == null)
    {
      return;
    }

    try
    {
      // Create a DTO without SteamID for serialization
      var dataToSave = new
      {
        Skybox = data.Skybox,
        Brightness = data.Brightness,
        Color = data.Color
      };

      var jsonOptions = new JsonSerializerOptions
      {
        WriteIndented = false
      };

      var json = JsonSerializer.Serialize(dataToSave, jsonOptions);

      var setValueTask = new TaskCompletionSource<bool>();
      var playerSlot = player.Slot;
      Server.NextFrame(() =>
      {
        try
        {
          var currentPlayer = Utilities.GetPlayerFromSlot(playerSlot);
          if (currentPlayer != null && currentPlayer.IsValid && !currentPlayer.IsBot)
          {
            _settingsApi.SetPlayerSettingsValue(currentPlayer, SettingsKey, json);
            setValueTask.SetResult(true);
          }
          else
          {
            setValueTask.SetResult(false);
          }
        }
        catch (Exception ex)
        {
          Console.WriteLine($"[SkyboxChanger] Failed to save settings for player {steamid}: {ex.Message}");
          setValueTask.SetException(ex);
        }
      });

      await setValueTask.Task;
    }
    catch (Exception ex)
    {
      Console.WriteLine($"[SkyboxChanger] Failed to serialize settings for player {steamid}: {ex.Message}");
    }
  }

  private async Task<string> GetPlayerSettingsValueAsync(CCSPlayerController player, string key, string defaultValue)
  {
    if (_settingsApi == null)
    {
      return defaultValue;
    }

    var playerSlot = player.Slot;
    var getValueTask = new TaskCompletionSource<string>();

    Server.NextFrame(() =>
    {
      try
      {
        var currentPlayer = Utilities.GetPlayerFromSlot(playerSlot);
        if (currentPlayer != null && currentPlayer.IsValid && !currentPlayer.IsBot)
        {
          var value = _settingsApi.GetPlayerSettingsValue(currentPlayer, key, defaultValue);
          getValueTask.SetResult(value);
        }
        else
        {
          getValueTask.SetResult(defaultValue);
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine($"[SkyboxChanger] Failed to get settings value: {ex.Message}");
        getValueTask.SetResult(defaultValue);
      }
    });

    return await getValueTask.Task;
  }

  public void InvalidateCache(ulong steamid)
  {
    _PlayerStorage.Remove(steamid);
  }

  private async Task<CCSPlayerController?> GetPlayerBySteamIdAsync(ulong steamId)
  {
    if (steamId == 0)
    {
      return null;
    }

    var tcs = new TaskCompletionSource<CCSPlayerController?>();
    Server.NextFrame(() =>
    {
      try
      {
        var player = Utilities.GetPlayers().FirstOrDefault(p => p.IsValid && p.AuthorizedSteamID?.SteamId64 == steamId);
        tcs.SetResult(player);
      }
      catch (Exception ex)
      {
        tcs.SetException(ex);
      }
    });

    return await tcs.Task;
  }
}