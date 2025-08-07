using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using System.Globalization;
using System.Text.Json.Serialization;
using static CounterStrikeSharp.API.Core.Listeners;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CounterStrikeSharp.API.Modules.Utils;
using System.Linq;
using System.Drawing;
using System.Text.RegularExpressions;

namespace ColoredSmoke;

// This configuration class has been updated to be more comprehensive and flexible.
public class ConfigGen : BasePluginConfig
{
    [JsonPropertyName("EnableColoredSmoke")]
    public bool EnableColoredSmoke { get; set; } = true;

    [JsonPropertyName("EnableChickensFromSmoke")]
    public bool EnableChickensFromSmoke { get; set; } = true;
    
    [JsonPropertyName("ChickenAdminFlag")]
    public string ChickenAdminFlag { get; set; } = "@css/root";

    [JsonPropertyName("ChatPrefixes")]
    public Dictionary<string, string> ChatPrefixes { get; set; } = new Dictionary<string, string>
    {
        { "ChickenPrefix", "[CHICKEN]" }
    };

    [JsonPropertyName("Commands")]
    public Dictionary<string, string> Commands { get; set; } = new Dictionary<string, string>
    {
        { "SmokeCommand", "smoke" },
        { "ResetSmokeCommand", "rsmoke" },
        { "ChickenCommand", "chicken" }
    };
    
    // All chat messages, including command responses, have been moved here.
    [JsonPropertyName("ChatMessages")]
    public Dictionary<string, string> ChatMessages { get; set; } = new Dictionary<string, string>
    {
        {"SmokeCommandUsage", "Uso: !{0} <R> <G> <B> (0-255) ou !{1}"},
        {"InvalidRgbValues", "Valores inválidos. Exemplo: !{0} 255 0 0"},
        {"RgbValueOutOfRange", "Valores R, G e B devem ser entre 0 e 255."},
        {"SmokeColorSaved", "Cor da smoke salva: RGB({0}, {1}, {2})"},
        {"SmokeColorRemoved", "Sua cor de fumaça personalizada foi removida. A cor agora será a padrão."},
        {"NoCustomSmokeColor", "Você não tem uma cor de fumaça personalizada definida."},
        {"NoPermission", "Você não tem permissão para usar este comando."},
        {"NoLivePlayers", "Não há jogadores vivos no servidor para spawnar a galinha."},
        {"TargetPlayerError", "Erro: O jogador alvo não está disponível para spawnar a galinha."},
        {"TargetPositionError", "Erro: Posição do jogador alvo não disponível para spawnar a galinha."},
        {"FailedToCreateChicken", "Erro: Não foi possível criar a entidade da galinha. Verifique os logs do servidor."},
        {"ChickenScaleWarning", "Não foi possível acessar CBodyComponent, SceneNode ou SkeletonInstance para escalar a galinha. A galinha pode não aparecer gigante."},
        {"FailedToCreateChickenEntityLog", "Falha ao criar a entidade 'chicken'."},
        {"FailedToTeleportChickenLog", "Falha ao teletransportar a galinha, a posição ou rotação da fumaça era nula."},
        {"FailedToCreateChickenForSmokeLog", "Falha ao criar galinha para a fumaça."}
    };
    
    [JsonPropertyName("ChickenCommandPhrases")]
    public string[] ChickenCommandPhrases { get; set; } = new string[]
    {
        "O jogador {player_name} botou um ovo gigante! Cuidado!",
        "Alerta: {player_name} acabou de invocar uma galinha gigante!",
        "{player_name} liberou uma galinha do tamanho de um dinossauro!",
        "Preparem-se! {player_name} soltou uma galinha mutante!",
        "Aviso: galinha gigantesca spawnada por {player_name}!",
        "O poder das galinhas está com {player_name} agora!",
        "{player_name} fez chover galinhas gigantes no servidor!",
        "Galinha liberada por {player_name}, protejam-se!",
        "Quem chamou a galinha? Foi o {player_name}!",
        "Uma galinha colossal apareceu graças a {player_name}!"
    };

    [JsonPropertyName("ChickenSmokePhrases")]
    public string[] ChickenSmokePhrases { get; set; } = new string[]
    {
        "{player_name} invocou uma galinha mutante... CORRE!",
        "{player_name} abriu um portal pro galinheiro do inferno!",
        "A smoke do {player_name} deu cria... nasceu uma galinha radioativa!",
        "{player_name} jogou uma smoke e saiu uma galinha do apocalipse!",
        "{player_name} foi visto conversando com galinhas interdimensionais.",
        "{player_name} soltou a fumacinha... e veio a galinha!",
        "{player_name} acidentalmente misturou granada com ração de galinha.",
        "{player_name} liberou o carnaval das galinhas mutantes!",
        "Uma galinha gigante apareceu atrás de {player_name}!",
        "{player_name} invocou a lendária GALINHA DAS TREVAS!"
    };

    // New properties for chicken scaling
    [JsonPropertyName("MinChickenScale")]
    public float MinChickenScale { get; set; } = 5.0f;

    [JsonPropertyName("MaxChickenScale")]
    public float MaxChickenScale { get; set; } = 20.0f;

    [JsonPropertyName("ChickenSpawnChance")]
    public int ChickenSpawnChance { get; set; } = 30; // New property to configure chicken spawn chance (0-100)
}

public partial class ColoredSmoke : BasePlugin, IPluginConfig<ConfigGen>
{
    public override string ModuleName => "ChickenSmokeFun";
    public override string ModuleAuthor => "alekfps0";
    public override string ModuleDescription => "Colored smoke for grenades, giant chickens";
    public override string ModuleVersion => "1.0.0";

    public ConfigGen Config { get; set; } = new();

    private Dictionary<string, string> _playerColors = new();

    public void OnConfigParsed(ConfigGen config)
    {
        Config = config;
    }
    
    public override void Unload(bool hotReload)
    {
        RemoveListener("OnEntitySpawned", OnEntitySpawned);
    }

    public override void Load(bool hotReload)
    {
        base.Load(hotReload);
        
        // Load player colors from a separate file.
        LoadPlayerColors();

        // Register commands with names defined in the configuration file.
        if (Config.Commands.ContainsKey("SmokeCommand"))
        {
            AddCommand(Config.Commands["SmokeCommand"], "Sets the player's smoke color.", OnSmokeCommand);
        }
        if (Config.Commands.ContainsKey("ResetSmokeCommand"))
        {
            AddCommand(Config.Commands["ResetSmokeCommand"], "Resets the smoke color to default.", OnResetSmokeCommand);
        }
        if (Config.Commands.ContainsKey("ChickenCommand"))
        {
            AddCommand(Config.Commands["ChickenCommand"], "Spawns a giant chicken", OnChickenCommand);
        }

        // Register the entity listener if colored smokes are enabled.
        if (Config.EnableColoredSmoke)
        {
            RegisterListener<OnEntitySpawned>(OnEntitySpawned);
        }
    }

    // A static array of chat color characters to avoid reflection issues.
    private static readonly char[] _chatColorChars = {
        (char)0x07, // Default
        (char)0x04, // LightGreen
        (char)0x08, // LightRed
        (char)0x05, // Green
        (char)0x09, // LightBlue
        (char)0x06, // Blue
        (char)0x0A, // Purple
        (char)0x0B, // Gold
        (char)0x01, // Red
        (char)0x02, // Grey
        (char)0x03, // TeamBlue
        (char)0x0C, // TeamRed
        (char)0x0D, // DarkRed
        (char)0x0E, // Yellow
        (char)0x0F, // White
        (char)0x10, // Cyan
        (char)0x11  // Magenta
    };
    
    // This helper method now returns a string with a random chat color character.
    private string GetRandomChatColorChar()
    {
        Random random = new Random();
        int randomIndex = random.Next(_chatColorChars.Length);
        return _chatColorChars[randomIndex].ToString();
    }
    
    // Helper method to format chat messages with the prefix and a random color
    private string FormatChatMessage(string prefixKey, string message, string playerName)
    {
        string prefix = Config.ChatPrefixes.GetValueOrDefault(prefixKey, "");
        string formattedMessage = message.Replace("{player_name}", playerName);
        string randomColorChar = GetRandomChatColorChar();
        return string.Format("{0}{1}{2}", prefix, randomColorChar, formattedMessage);
    }

    // Method for the !smoke command
    private void OnSmokeCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid)
            return;

        if (info.ArgCount != 4)
        {
            player.PrintToChat(string.Format($"{ChatColors.Red}{Config.ChatMessages["SmokeCommandUsage"]}", Config.Commands["SmokeCommand"], Config.Commands["ResetSmokeCommand"]));
            return;
        }

        if (!float.TryParse(info.GetArg(1), NumberStyles.Float, CultureInfo.InvariantCulture, out float r) ||
            !float.TryParse(info.GetArg(2), NumberStyles.Float, CultureInfo.InvariantCulture, out float g) ||
            !float.TryParse(info.GetArg(3), NumberStyles.Float, CultureInfo.InvariantCulture, out float b))
        {
            player.PrintToChat(string.Format($"{ChatColors.Red}{Config.ChatMessages["InvalidRgbValues"]}", Config.Commands["SmokeCommand"]));
            return;
        }

        if (r < 0 || r > 255 || g < 0 || g > 255 || b < 0 || b > 255)
        {
            player.PrintToChat(string.Format($"{ChatColors.Red}{Config.ChatMessages["RgbValueOutOfRange"]}"));
            return;
        }

        string steamId = player.SteamID.ToString();
        string colorString = $"{r} {g} {b}";

        _playerColors[steamId] = colorString;
        
        SavePlayerColors();

        player.PrintToChat(string.Format($"{ChatColors.Green}{Config.ChatMessages["SmokeColorSaved"]}", r, g, b));
    }

    // Method for the !resetsmoke command
    private void OnResetSmokeCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid)
            return;

        string steamId = player.SteamID.ToString();

        if (_playerColors.ContainsKey(steamId))
        {
            _playerColors.Remove(steamId);
            
            SavePlayerColors();

            player.PrintToChat(string.Format($"{ChatColors.Red}{Config.ChatMessages["SmokeColorRemoved"]}"));
        }
        else
        {
            player.PrintToChat(string.Format($"{ChatColors.Red}{Config.ChatMessages["NoCustomSmokeColor"]}"));
        }
    }

    // Method for the !chicken command (spawns a giant, live chicken)
    public void OnChickenCommand(CCSPlayerController? commandIssuer, CommandInfo command)
    {
        if (!AdminManager.PlayerHasPermissions(commandIssuer, Config.ChickenAdminFlag))
        {
            command.ReplyToCommand(string.Format($"{ChatColors.Red}{Config.ChatMessages["NoPermission"]}"));
            return;
        }
        
        var livePlayers = Utilities.GetPlayers()
                                 .Where(p => p.IsValid && p.PlayerPawn != null && p.PlayerPawn.IsValid && p.PlayerPawn.Value != null && p.PlayerPawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE)
                                 .ToList();

        if (!livePlayers.Any())
        {
            command.ReplyToCommand($"{Config.ChatMessages["NoLivePlayers"]}");
            return;
        }

        Random random = new Random();
        CCSPlayerController? targetPlayer = livePlayers[random.Next(livePlayers.Count)];

        if (targetPlayer?.PlayerPawn?.Value == null)
        {
            command.ReplyToCommand($"{Config.ChatMessages["TargetPlayerError"]}");
            return;
        }
        
        var targetPawn = targetPlayer.PlayerPawn.Value;
        var origin = targetPawn.AbsOrigin;
        var angles = targetPawn.EyeAngles;
        var velocity = targetPawn.AbsVelocity;
        
        if (origin == null || angles == null || velocity == null)
        {
            command.ReplyToCommand($"{Config.ChatMessages["TargetPositionError"]}");
            return;
        }

        CChicken? chicken = Utilities.CreateEntityByName<CChicken>("chicken");

        if (chicken == null || !chicken.IsValid)
        {
            command.ReplyToCommand($"{Config.ChatMessages["FailedToCreateChicken"]}");
            Logger.LogError(Config.ChatMessages["FailedToCreateChickenEntityLog"]);
            return;
        }
        
        chicken.Teleport(
            new Vector(origin.X + 50.0f, origin.Y, origin.Z + 10.0f),
            angles,
            velocity
        );

        chicken.DispatchSpawn();

        // Use the new configurable scale values.
        float randomScale = (float)(random.NextDouble() * (Config.MaxChickenScale - Config.MinChickenScale) + Config.MinChickenScale);
        var skeletonInstance = chicken.CBodyComponent?.SceneNode?.GetSkeletonInstance();
        if (skeletonInstance != null)
        {
            skeletonInstance.Scale = randomScale;
        }
        else
        {
            Logger.LogWarning(Config.ChatMessages["ChickenScaleWarning"]);
        }
        
        // Use phrases from the configuration file.
        string selectedPhrase = Config.ChickenCommandPhrases[random.Next(Config.ChickenCommandPhrases.Length)];
        
        // New, fixed chat message format using the helper method
        Server.PrintToChatAll(FormatChatMessage("ChickenPrefix", selectedPhrase, targetPlayer.PlayerName));
    }
    
    private void OnEntitySpawned(CEntityInstance entity)
    {
        if (entity.DesignerName != "smokegrenade_projectile")
            return;

        var grenade = new CSmokeGrenadeProjectile(entity.Handle);

        if (grenade.Handle == IntPtr.Zero)
            return;

        Server.NextFrame(() =>
        {
            var player = grenade.Thrower?.Value?.Controller?.Value;
            if (player == null)
                return;

            string steamId = player.SteamID.ToString();
            string colorToApply = "random";

            if (_playerColors.TryGetValue(steamId, out string? customColorString))
            {
                colorToApply = customColorString;
            }

            int r = 255, g = 255, b = 255;
            var rand = new Random();

            if (colorToApply == "random")
            {
                int dominant = rand.Next(3);
                if (dominant == 0) { r = rand.Next(200, 256); g = rand.Next(0, 50); b = rand.Next(0, 50); }
                else if (dominant == 1) { r = rand.Next(0, 50); g = rand.Next(200, 256); b = rand.Next(0, 50); }
                else { r = rand.Next(0, 50); g = rand.Next(0, 50); b = rand.Next(200, 256); }

                grenade.SmokeColor.X = r;
                grenade.SmokeColor.Y = g;
                grenade.SmokeColor.Z = b;
            }
            else
            {
                string[] rgb = colorToApply.Split(' ');
                if (rgb.Length == 3 &&
                    float.TryParse(rgb[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedR) &&
                    float.TryParse(rgb[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedG) &&
                    float.TryParse(rgb[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedB))
                {
                    r = (int)parsedR;
                    g = (int)parsedG;
                    b = (int)parsedB;
                    grenade.SmokeColor.X = parsedR;
                    grenade.SmokeColor.Y = parsedG;
                    grenade.SmokeColor.Z = parsedB;
                }
            }
            
            // Logic to spawn the colored chicken, now controlled by the configuration.
            if (Config.EnableChickensFromSmoke)
            {
                var random = new Random();
                int chance = random.Next(1, 101);

                // Use the new configurable chance for spawning the chicken
                if (chance <= Config.ChickenSpawnChance)
                {
                    CChicken? chicken = Utilities.CreateEntityByName<CChicken>("chicken");
                    if (chicken != null && chicken.IsValid)
                    {
                        var chickenOrigin = grenade.AbsOrigin;
                        var chickenRotation = grenade.AbsRotation;
                        var chickenVelocity = grenade.AbsVelocity;

                        if (chickenOrigin != null && chickenRotation != null && chickenVelocity != null)
                        {
                            chicken.Teleport(chickenOrigin, chickenRotation, chickenVelocity);
                            chicken.DispatchSpawn();
                            chicken.Render = Color.FromArgb(r, g, b);

                            // Use the new configurable scale values.
                            float scale = (float)(random.NextDouble() * (Config.MaxChickenScale - Config.MinChickenScale) + Config.MinChickenScale);
                            var skeletonInstance = chicken.CBodyComponent?.SceneNode?.GetSkeletonInstance();
                            if (skeletonInstance != null)
                            {
                                skeletonInstance.Scale = scale;
                            }
                            else
                            {
                                Logger.LogWarning(Config.ChatMessages["ChickenScaleWarning"]);
                            }

                            // Use phrases from the configuration file.
                            string selectedPhrase = Config.ChickenSmokePhrases[random.Next(Config.ChickenSmokePhrases.Length)];
                            
                            // New, fixed chat message format using the helper method
                            Server.PrintToChatAll(FormatChatMessage("ChickenPrefix", selectedPhrase, player.PlayerName));
                        }
                        else
                        {
                            Logger.LogWarning(Config.ChatMessages["FailedToTeleportChickenLog"]);
                        }
                    }
                    else
                    {
                        Logger.LogError(Config.ChatMessages["FailedToCreateChickenForSmokeLog"]);
                    }
                }
            }
        });
    }

    // Helper method to save player colors to a separate file.
    private void SavePlayerColors()
    {
        string configDirectory = Path.Combine(ModuleDirectory, "config");
        string configFilePath = Path.Combine(configDirectory, "player_colors.json");

        try
        {
            Directory.CreateDirectory(configDirectory);
            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(_playerColors, options);
            File.WriteAllText(configFilePath, jsonString);
            Logger.LogInformation($"Player colors saved to: {configFilePath}");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error saving player colors: {ex.Message}");
        }
    }

    // Helper method to load player colors from the file.
    private void LoadPlayerColors()
    {
        string configDirectory = Path.Combine(ModuleDirectory, "config");
        string configFilePath = Path.Combine(configDirectory, "player_colors.json");

        if (File.Exists(configFilePath))
        {
            try
            {
                string jsonString = File.ReadAllText(configFilePath);
                var colors = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonString);
                if (colors != null)
                {
                    _playerColors = colors;
                    Logger.LogInformation($"Player colors loaded from: {configFilePath}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error loading player colors: {ex.Message}");
                _playerColors = new Dictionary<string, string>(); // Reset in case of error
            }
        }
        else
        {
            _playerColors = new Dictionary<string, string>();
            Logger.LogInformation($"Player colors file not found. Creating a new dictionary.");
        }
    }
}
