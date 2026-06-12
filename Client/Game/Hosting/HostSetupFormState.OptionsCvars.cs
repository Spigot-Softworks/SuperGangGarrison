#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private enum HostSetupOptionsTab
    {
        Basic,
        Advanced,
    }

    private sealed partial class HostSetupFormState
    {
        public int OptionsTabIndex { get; set; }

        public string? ActiveAdvancedCvarName { get; set; }

        public int AdvancedCvarCursorIndex { get; set; }

        public int AdvancedCvarSelectionStart { get; set; }

        public bool RandomSpreadEnabled { get; set; } = true;

        public bool SniperAimIndicatorEnabled { get; set; } = true;

        public bool RoundEndFriendlyFireEnabled { get; set; }

        public bool SwitchTeamsAfterRoundEnd { get; set; }

        public int TeamShuffleAfterWins { get; set; }

        public bool CompetitiveReadyUpEnabled { get; set; }

        public int CompetitiveSetupSeconds { get; set; } = 10;

        public bool BotAutofillEnabled { get; set; }

        public int BotAutofillMinPlayers { get; set; }

        public int BotAutofillPerTeam { get; set; }

        public int TickRate { get; set; } = SimulationConfig.DefaultTicksPerSecond;

        public float PlayerScale { get; set; } = 1f;

        public float MapScale { get; set; } = 1f;

        public float MovementSpeedScale { get; set; } = 1f;

        public float ProjectileSpeedScale { get; set; } = 1f;

        public float DamageScale { get; set; } = 1f;

        public float GravityScale { get; set; } = 1f;

        public float HorizontalSpeedClampPerTick { get; set; } = LegacyMovementModel.MaxStepSpeedPerTick;

        public float VerticalSpeedClampPerTick { get; set; } = LegacyMovementModel.MaxStepSpeedPerTick;

        private readonly Dictionary<string, string> _advancedCvarEditBuffers = new(StringComparer.OrdinalIgnoreCase);

        private static readonly string[] LinkedBasicHostSettingKeys =
        [
            nameof(OpenGarrisonHostSettings.TimeLimitMinutes),
            nameof(OpenGarrisonHostSettings.CapLimit),
            nameof(OpenGarrisonHostSettings.RespawnSeconds),
            nameof(OpenGarrisonHostSettings.AutoBalanceEnabled),
            nameof(OpenGarrisonHostSettings.SecondaryAbilitiesEnabled),
        ];

        public HostSetupOptionsTab OptionsTab => (HostSetupOptionsTab)Math.Clamp(OptionsTabIndex, 0, 1);

        public int GetHostOptionsRowCount()
        {
            return OptionsTab == HostSetupOptionsTab.Basic
                ? HostSetupServerCvarCatalog.BasicOptionCount
                : HostSetupServerCvarCatalog.AdvancedDefinitions.Count;
        }

        public void SetHostOptionsTab(int tabIndex)
        {
            OptionsTabIndex = Math.Clamp(tabIndex, 0, 1);
            OptionsHoverIndex = -1;
            OptionsScrollOffset = 0;
            ClearAdvancedCvarEditFocus();
        }

        public void ClearAdvancedCvarEditFocus()
        {
            CommitActiveAdvancedCvarEdit();
            ActiveAdvancedCvarName = null;
            if (EditField == HostSetupEditField.AdvancedCvar)
            {
                EditField = HostSetupEditField.None;
            }
        }

        public void NotifyLinkedBasicHostSettingsChanged()
        {
            foreach (var hostSettingKey in LinkedBasicHostSettingKeys)
            {
                if (!HostSetupServerCvarCatalog.TryGetDefinitionByHostSettingKey(hostSettingKey, out var definition))
                {
                    continue;
                }

                _advancedCvarEditBuffers[definition.Name] = ReadHostSettingAsCvarString(definition);
            }
        }

        public void ToggleBasicAutoBalance()
        {
            AutoBalanceEnabled = !AutoBalanceEnabled;
            NotifyLinkedBasicHostSettingsChanged();
        }

        public void ToggleBasicSecondaryAbilities()
        {
            SecondaryAbilitiesEnabled = !SecondaryAbilitiesEnabled;
            NotifyLinkedBasicHostSettingsChanged();
        }

        private void CommitActiveAdvancedCvarEdit()
        {
            if (EditField != HostSetupEditField.AdvancedCvar
                || string.IsNullOrWhiteSpace(ActiveAdvancedCvarName)
                || !HostSetupServerCvarCatalog.TryGetDefinition(ActiveAdvancedCvarName, out var definition))
            {
                return;
            }

            var buffer = GetActiveAdvancedCvarEditBuffer();
            if (HostSetupServerCvarCatalog.TryNormalizeInput(definition, buffer, out var normalized))
            {
                ApplyCvarStringToHostSetting(definition, normalized);
            }
            else
            {
                _advancedCvarEditBuffers[definition.Name] = ReadHostSettingAsCvarString(definition);
            }

            NotifyLinkedBasicHostSettingsChanged();
        }

        public bool TryGetHostOptionsRow(int rowIndex, out string label, out string displayValue, out HostSetupCvarEditorKind editorKind, out HostSetupServerCvarDefinition? definition)
        {
            label = string.Empty;
            displayValue = string.Empty;
            editorKind = HostSetupCvarEditorKind.NumericText;
            definition = null;

            if (OptionsTab == HostSetupOptionsTab.Basic)
            {
                switch (rowIndex)
                {
                    case 0:
                        label = "Round Time";
                        displayValue = TimeLimitBuffer;
                        editorKind = HostSetupCvarEditorKind.NumericText;
                        return true;
                    case 1:
                        label = "Cap Limit (CTF)";
                        displayValue = CapLimitBuffer;
                        editorKind = HostSetupCvarEditorKind.NumericText;
                        return true;
                    case 2:
                        label = "Respawn Time (sec)";
                        displayValue = RespawnSecondsBuffer;
                        editorKind = HostSetupCvarEditorKind.NumericText;
                        return true;
                    case 3:
                        label = "Auto-balance";
                        displayValue = AutoBalanceEnabled ? "On" : "Off";
                        editorKind = HostSetupCvarEditorKind.Toggle;
                        return true;
                    case 4:
                        label = "Special Abilities";
                        displayValue = SecondaryAbilitiesEnabled ? "On" : "Off";
                        editorKind = HostSetupCvarEditorKind.Toggle;
                        return true;
                    default:
                        return false;
                }
            }

            if (rowIndex < 0 || rowIndex >= HostSetupServerCvarCatalog.AdvancedDefinitions.Count)
            {
                return false;
            }

            definition = HostSetupServerCvarCatalog.AdvancedDefinitions[rowIndex];
            label = definition.Label;
            var rawValue = GetAdvancedCvarRawValue(definition);
            displayValue = definition.EditorKind == HostSetupCvarEditorKind.Stepped
                ? $"< {HostSetupServerCvarCatalog.FormatDisplayValue(definition, rawValue)} >"
                : HostSetupServerCvarCatalog.FormatDisplayValue(definition, rawValue);
            editorKind = definition.EditorKind;
            return true;
        }

        public string GetAdvancedCvarRawValue(HostSetupServerCvarDefinition definition)
        {
            if (EditField == HostSetupEditField.AdvancedCvar
                && string.Equals(ActiveAdvancedCvarName, definition.Name, StringComparison.OrdinalIgnoreCase))
            {
                return GetActiveAdvancedCvarEditBuffer();
            }

            return ReadHostSettingAsCvarString(definition);
        }

        public void SetAdvancedCvarRawValue(HostSetupServerCvarDefinition definition, string rawValue)
        {
            if (HostSetupServerCvarCatalog.TryNormalizeInput(definition, rawValue, out var normalized))
            {
                rawValue = normalized;
            }

            _advancedCvarEditBuffers[definition.Name] = rawValue;
            ApplyCvarStringToHostSetting(definition, rawValue);
        }

        public void StepAdvancedCvar(HostSetupServerCvarDefinition definition, int direction)
        {
            var next = HostSetupServerCvarCatalog.StepValue(definition, GetAdvancedCvarRawValue(definition), direction);
            SetAdvancedCvarRawValue(definition, next);
        }

        public void ToggleAdvancedCvar(HostSetupServerCvarDefinition definition)
        {
            var next = HostSetupServerCvarCatalog.ToggleValue(GetAdvancedCvarRawValue(definition));
            SetAdvancedCvarRawValue(definition, next);
        }

        public bool TryGetAdvancedDefinitionForRow(int rowIndex, out HostSetupServerCvarDefinition definition)
        {
            if (OptionsTab != HostSetupOptionsTab.Advanced
                || rowIndex < 0
                || rowIndex >= HostSetupServerCvarCatalog.AdvancedDefinitions.Count)
            {
                definition = null!;
                return false;
            }

            definition = HostSetupServerCvarCatalog.AdvancedDefinitions[rowIndex];
            return true;
        }

        public string GetActiveAdvancedCvarEditBuffer()
        {
            if (string.IsNullOrWhiteSpace(ActiveAdvancedCvarName))
            {
                return string.Empty;
            }

            return _advancedCvarEditBuffers.TryGetValue(ActiveAdvancedCvarName, out var buffer)
                ? buffer
                : string.Empty;
        }

        public void SetActiveAdvancedCvarEditBuffer(string value)
        {
            if (string.IsNullOrWhiteSpace(ActiveAdvancedCvarName))
            {
                return;
            }

            _advancedCvarEditBuffers[ActiveAdvancedCvarName] = value;
            if (HostSetupServerCvarCatalog.TryGetDefinition(ActiveAdvancedCvarName, out var definition))
            {
                ApplyCvarStringToHostSetting(definition, value);
            }
        }

        private void LoadAdvancedCvarsFrom(OpenGarrisonHostSettings hostDefaults)
        {
            _advancedCvarEditBuffers.Clear();
            RandomSpreadEnabled = hostDefaults.RandomSpreadEnabled;
            SniperAimIndicatorEnabled = hostDefaults.SniperAimIndicatorEnabled;
            RoundEndFriendlyFireEnabled = hostDefaults.RoundEndFriendlyFireEnabled;
            SwitchTeamsAfterRoundEnd = hostDefaults.SwitchTeamsAfterRoundEnd;
            TeamShuffleAfterWins = OpenGarrisonHostSettings.NormalizeTeamShuffleAfterWins(hostDefaults.TeamShuffleAfterWins);
            CompetitiveReadyUpEnabled = hostDefaults.CompetitiveReadyUpEnabled;
            CompetitiveSetupSeconds = hostDefaults.CompetitiveSetupSeconds;
            BotAutofillEnabled = hostDefaults.BotAutofillEnabled;
            BotAutofillMinPlayers = hostDefaults.BotAutofillMinPlayers;
            BotAutofillPerTeam = hostDefaults.BotAutofillPerTeam;
            TickRate = SimulationConfig.NormalizeTicksPerSecond(hostDefaults.TickRate);
            PlayerScale = hostDefaults.PlayerScale;
            MapScale = hostDefaults.MapScale;
            MovementSpeedScale = hostDefaults.MovementSpeedScale;
            ProjectileSpeedScale = hostDefaults.ProjectileSpeedScale;
            DamageScale = hostDefaults.DamageScale;
            GravityScale = hostDefaults.GravityScale;
            HorizontalSpeedClampPerTick = hostDefaults.HorizontalSpeedClampPerTick;
            VerticalSpeedClampPerTick = hostDefaults.VerticalSpeedClampPerTick;

            foreach (var definition in HostSetupServerCvarCatalog.AdvancedDefinitions)
            {
                _advancedCvarEditBuffers[definition.Name] = ReadHostSettingAsCvarString(definition);
            }
        }

        private void ApplyAdvancedCvarsTo(OpenGarrisonHostSettings hostDefaults)
        {
            foreach (var definition in HostSetupServerCvarCatalog.AdvancedDefinitions)
            {
                var rawValue = GetAdvancedCvarRawValue(definition);
                if (HostSetupServerCvarCatalog.TryNormalizeInput(definition, rawValue, out var normalized))
                {
                    rawValue = normalized;
                }

                ApplyCvarStringToHostSetting(definition, rawValue);
            }

            hostDefaults.RandomSpreadEnabled = RandomSpreadEnabled;
            hostDefaults.SniperAimIndicatorEnabled = SniperAimIndicatorEnabled;
            hostDefaults.RoundEndFriendlyFireEnabled = RoundEndFriendlyFireEnabled;
            hostDefaults.SwitchTeamsAfterRoundEnd = SwitchTeamsAfterRoundEnd;
            hostDefaults.TeamShuffleAfterWins = OpenGarrisonHostSettings.NormalizeTeamShuffleAfterWins(TeamShuffleAfterWins);
            hostDefaults.CompetitiveReadyUpEnabled = CompetitiveReadyUpEnabled;
            hostDefaults.CompetitiveSetupSeconds = Math.Clamp(CompetitiveSetupSeconds, 0, 120);
            hostDefaults.BotAutofillEnabled = BotAutofillEnabled;
            hostDefaults.BotAutofillMinPlayers = Math.Clamp(BotAutofillMinPlayers, 0, SimulationWorld.MaxPlayableNetworkPlayers);
            hostDefaults.BotAutofillPerTeam = Math.Clamp(BotAutofillPerTeam, 0, SimulationWorld.MaxPlayableNetworkPlayers / 2);
            hostDefaults.TickRate = SimulationConfig.NormalizeTicksPerSecond(TickRate);
            hostDefaults.PlayerScale = PlayerScale;
            hostDefaults.MapScale = MapScale;
            hostDefaults.MovementSpeedScale = MovementSpeedScale;
            hostDefaults.ProjectileSpeedScale = ProjectileSpeedScale;
            hostDefaults.DamageScale = DamageScale;
            hostDefaults.GravityScale = GravityScale;
            hostDefaults.HorizontalSpeedClampPerTick = HorizontalSpeedClampPerTick;
            hostDefaults.VerticalSpeedClampPerTick = VerticalSpeedClampPerTick;
        }

        private string ReadHostSettingAsCvarString(HostSetupServerCvarDefinition definition)
        {
            return definition.HostSettingKey switch
            {
                nameof(OpenGarrisonHostSettings.TimeLimitMinutes) => TimeLimitBuffer,
                nameof(OpenGarrisonHostSettings.CapLimit) => CapLimitBuffer,
                nameof(OpenGarrisonHostSettings.RespawnSeconds) => RespawnSecondsBuffer,
                nameof(OpenGarrisonHostSettings.AutoBalanceEnabled) => AutoBalanceEnabled ? "1" : "0",
                nameof(OpenGarrisonHostSettings.SecondaryAbilitiesEnabled) => SecondaryAbilitiesEnabled ? "1" : "0",
                nameof(OpenGarrisonHostSettings.RandomSpreadEnabled) => RandomSpreadEnabled ? "1" : "0",
                nameof(OpenGarrisonHostSettings.SniperAimIndicatorEnabled) => SniperAimIndicatorEnabled ? "1" : "0",
                nameof(OpenGarrisonHostSettings.RoundEndFriendlyFireEnabled) => RoundEndFriendlyFireEnabled ? "1" : "0",
                nameof(OpenGarrisonHostSettings.SwitchTeamsAfterRoundEnd) => SwitchTeamsAfterRoundEnd ? "1" : "0",
                nameof(OpenGarrisonHostSettings.TeamShuffleAfterWins) => TeamShuffleAfterWins.ToString(CultureInfo.InvariantCulture),
                nameof(OpenGarrisonHostSettings.CompetitiveReadyUpEnabled) => CompetitiveReadyUpEnabled ? "1" : "0",
                nameof(OpenGarrisonHostSettings.CompetitiveSetupSeconds) => CompetitiveSetupSeconds.ToString(CultureInfo.InvariantCulture),
                nameof(OpenGarrisonHostSettings.BotAutofillEnabled) => BotAutofillEnabled ? "1" : "0",
                nameof(OpenGarrisonHostSettings.BotAutofillMinPlayers) => BotAutofillMinPlayers.ToString(CultureInfo.InvariantCulture),
                nameof(OpenGarrisonHostSettings.BotAutofillPerTeam) => BotAutofillPerTeam.ToString(CultureInfo.InvariantCulture),
                nameof(OpenGarrisonHostSettings.TickRate) => TickRate.ToString(CultureInfo.InvariantCulture),
                nameof(OpenGarrisonHostSettings.PlayerScale) => PlayerScale.ToString("0.###", CultureInfo.InvariantCulture),
                nameof(OpenGarrisonHostSettings.MapScale) => MapScale.ToString("0.###", CultureInfo.InvariantCulture),
                nameof(OpenGarrisonHostSettings.MovementSpeedScale) => MovementSpeedScale.ToString("0.###", CultureInfo.InvariantCulture),
                nameof(OpenGarrisonHostSettings.ProjectileSpeedScale) => ProjectileSpeedScale.ToString("0.###", CultureInfo.InvariantCulture),
                nameof(OpenGarrisonHostSettings.DamageScale) => DamageScale.ToString("0.###", CultureInfo.InvariantCulture),
                nameof(OpenGarrisonHostSettings.GravityScale) => GravityScale.ToString("0.###", CultureInfo.InvariantCulture),
                nameof(OpenGarrisonHostSettings.HorizontalSpeedClampPerTick) => HorizontalSpeedClampPerTick.ToString("0.###", CultureInfo.InvariantCulture),
                nameof(OpenGarrisonHostSettings.VerticalSpeedClampPerTick) => VerticalSpeedClampPerTick.ToString("0.###", CultureInfo.InvariantCulture),
                _ => "0",
            };
        }

        private void ApplyCvarStringToHostSetting(HostSetupServerCvarDefinition definition, string rawValue)
        {
            switch (definition.HostSettingKey)
            {
                case nameof(OpenGarrisonHostSettings.TimeLimitMinutes):
                    TimeLimitBuffer = rawValue;
                    break;
                case nameof(OpenGarrisonHostSettings.CapLimit):
                    CapLimitBuffer = rawValue;
                    break;
                case nameof(OpenGarrisonHostSettings.RespawnSeconds):
                    RespawnSecondsBuffer = rawValue;
                    break;
                case nameof(OpenGarrisonHostSettings.AutoBalanceEnabled):
                    AutoBalanceEnabled = ParseBool(rawValue);
                    break;
                case nameof(OpenGarrisonHostSettings.SecondaryAbilitiesEnabled):
                    SecondaryAbilitiesEnabled = ParseBool(rawValue);
                    break;
                case nameof(OpenGarrisonHostSettings.RandomSpreadEnabled):
                    RandomSpreadEnabled = ParseBool(rawValue);
                    break;
                case nameof(OpenGarrisonHostSettings.SniperAimIndicatorEnabled):
                    SniperAimIndicatorEnabled = ParseBool(rawValue);
                    break;
                case nameof(OpenGarrisonHostSettings.RoundEndFriendlyFireEnabled):
                    RoundEndFriendlyFireEnabled = ParseBool(rawValue);
                    break;
                case nameof(OpenGarrisonHostSettings.SwitchTeamsAfterRoundEnd):
                    SwitchTeamsAfterRoundEnd = ParseBool(rawValue);
                    break;
                case nameof(OpenGarrisonHostSettings.TeamShuffleAfterWins):
                    if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var teamShuffleAfterWins))
                    {
                        TeamShuffleAfterWins = OpenGarrisonHostSettings.NormalizeTeamShuffleAfterWins(teamShuffleAfterWins);
                    }

                    break;
                case nameof(OpenGarrisonHostSettings.CompetitiveReadyUpEnabled):
                    CompetitiveReadyUpEnabled = ParseBool(rawValue);
                    break;
                case nameof(OpenGarrisonHostSettings.CompetitiveSetupSeconds):
                    if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var setupSeconds))
                    {
                        CompetitiveSetupSeconds = Math.Clamp(setupSeconds, 0, 120);
                    }

                    break;
                case nameof(OpenGarrisonHostSettings.BotAutofillEnabled):
                    BotAutofillEnabled = ParseBool(rawValue);
                    break;
                case nameof(OpenGarrisonHostSettings.BotAutofillMinPlayers):
                    if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minPlayers))
                    {
                        BotAutofillMinPlayers = Math.Clamp(minPlayers, 0, SimulationWorld.MaxPlayableNetworkPlayers);
                    }

                    break;
                case nameof(OpenGarrisonHostSettings.BotAutofillPerTeam):
                    if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var perTeam))
                    {
                        BotAutofillPerTeam = Math.Clamp(perTeam, 0, SimulationWorld.MaxPlayableNetworkPlayers / 2);
                    }

                    break;
                case nameof(OpenGarrisonHostSettings.TickRate):
                    if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tickRate))
                    {
                        TickRate = SimulationConfig.NormalizeTicksPerSecond(tickRate);
                    }

                    break;
                case nameof(OpenGarrisonHostSettings.PlayerScale):
                    if (float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var playerScale))
                    {
                        PlayerScale = PlayerEntity.ClampPlayerScale(playerScale);
                    }

                    break;
                case nameof(OpenGarrisonHostSettings.MapScale):
                    if (float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var mapScale))
                    {
                        MapScale = float.Clamp(mapScale, 0.25f, 4f);
                    }

                    break;
                case nameof(OpenGarrisonHostSettings.MovementSpeedScale):
                    if (float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var movementScale))
                    {
                        MovementSpeedScale = float.Clamp(movementScale, 0.1f, 4f);
                    }

                    break;
                case nameof(OpenGarrisonHostSettings.ProjectileSpeedScale):
                    if (float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var projectileScale))
                    {
                        ProjectileSpeedScale = float.Clamp(projectileScale, 0.1f, 4f);
                    }

                    break;
                case nameof(OpenGarrisonHostSettings.DamageScale):
                    if (float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var damageScale))
                    {
                        DamageScale = float.Clamp(damageScale, 0f, 10f);
                    }

                    break;
                case nameof(OpenGarrisonHostSettings.GravityScale):
                    if (float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var gravityScale))
                    {
                        GravityScale = float.Clamp(gravityScale, 0f, 4f);
                    }

                    break;
                case nameof(OpenGarrisonHostSettings.HorizontalSpeedClampPerTick):
                    if (float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var horizontalClamp))
                    {
                        HorizontalSpeedClampPerTick = float.Clamp(horizontalClamp, 1f, 60f);
                    }

                    break;
                case nameof(OpenGarrisonHostSettings.VerticalSpeedClampPerTick):
                    if (float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var verticalClamp))
                    {
                        VerticalSpeedClampPerTick = float.Clamp(verticalClamp, 1f, 60f);
                    }

                    break;
            }

            _advancedCvarEditBuffers[definition.Name] = ReadHostSettingAsCvarString(definition);
            NotifyLinkedBasicHostSettingsChanged();
        }

        public bool TryAppendAdvancedCvarCharacter(char character)
        {
            if (string.IsNullOrWhiteSpace(ActiveAdvancedCvarName)
                || !HostSetupServerCvarCatalog.TryGetDefinition(ActiveAdvancedCvarName, out var definition))
            {
                return false;
            }

            if (!HostSetupServerCvarCatalog.IsValidInputCharacter(definition, character))
            {
                return false;
            }

            var buffer = GetActiveAdvancedCvarEditBuffer();
            if (buffer.Length >= 24)
            {
                return false;
            }

            SetActiveAdvancedCvarEditBuffer(buffer + character);
            return true;
        }

        public void BackspaceActiveAdvancedCvar()
        {
            var buffer = GetActiveAdvancedCvarEditBuffer();
            if (buffer.Length == 0)
            {
                return;
            }

            SetActiveAdvancedCvarEditBuffer(buffer[..^1]);
        }

        private static bool ParseBool(string value)
        {
            return value.Trim() switch
            {
                "1" or "true" or "True" or "on" or "On" or "yes" or "Yes" => true,
                _ => false,
            };
        }
    }
}
