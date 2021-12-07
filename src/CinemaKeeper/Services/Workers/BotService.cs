﻿using System;
using System.Threading;
using System.Threading.Tasks;

using CinemaKeeper.Exceptions;
using CinemaKeeper.Settings;

using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Serilog;

using ExecuteResult = Discord.Commands.ExecuteResult;
using IResult = Discord.Commands.IResult;

namespace CinemaKeeper.Services.Workers;

public class BotService : BackgroundService
{
    private readonly ILogger _logger;

    private readonly IOptions<DiscordSettings> _discordSettings;

    private readonly DiscordSocketClient _client;
    private readonly CommandService _commandService;
    private readonly InteractionService _interactionService;
    private readonly ILocalizationProvider _localizationProvider;

    private readonly IServiceProvider _serviceProvider;

    public BotService(
        ILogger logger,
        IOptions<DiscordSettings> discordSettings,
        DiscordSocketClient client,
        CommandService commandService,
        InteractionService interactionService,
        ILocalizationProvider localizationProvider,
        IServiceProvider serviceProvider)
    {
        _logger = logger.ForContext<BotService>();
        _discordSettings = discordSettings;
        _client = client;
        _commandService = commandService;
        _interactionService = interactionService;
        _localizationProvider = localizationProvider;
        _serviceProvider = serviceProvider;

        _client.MessageReceived += MessageReceived;
        _client.SlashCommandExecuted += SlashCommandHandler;
        _client.Ready += ClientReady;
        _commandService.CommandExecuted += OnCommandExecuted;
    }

    private async Task ClientReady()
    {
        var guildCommand = new SlashCommandBuilder();

        var command = guildCommand
           .WithName("quote")
           .WithDescription("Manage the most stunning quotes of the specified user")
           .AddOption("user", ApplicationCommandOptionType.User, "User who once told this", true)
           .AddOption("message", ApplicationCommandOptionType.String, "Quote")
           .Build();

        await _client.CreateGlobalApplicationCommandAsync(command);
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await base.StartAsync(cancellationToken);

        await _client.LoginAsync(TokenType.Bot, _discordSettings.Value.BotToken);
        await _client.StartAsync();
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        await _client.StopAsync();
        await _client.LogoutAsync();
    }

    public override void Dispose()
    {
        base.Dispose();

        _client.Dispose();
        (_commandService as IDisposable).Dispose();

        GC.SuppressFinalize(this);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;

    private async Task MessageReceived(SocketMessage message)
    {
        if (message is not SocketUserMessage command)
            return;

        var argPos = 0;

        var isInvalidCommand = !command.HasStringPrefix("!", ref argPos)
            || command.HasMentionPrefix(_client.CurrentUser, ref argPos)
            || command.Author.IsBot
            || command.Content.Length == 1;

        if (isInvalidCommand)
            return;

        var commandContext = new SocketCommandContext(_client, command);
        await _commandService.ExecuteAsync(commandContext, argPos, _serviceProvider);
    }

    private async Task SlashCommandHandler(SocketSlashCommand command)
    {
        var commandContext = new SocketInteractionContext(_client, command);
        using var serviceScope = _serviceProvider.CreateScope();
        await _interactionService.ExecuteCommandAsync(commandContext, serviceScope.ServiceProvider);
    }

    private async Task OnCommandExecuted(Optional<CommandInfo> command, ICommandContext context, IResult result)
    {
        if (!result.IsSuccess)
        {
            var response = GetErrorResponse(result);

            if (!string.IsNullOrEmpty(response))
                await context.Channel.SendMessageAsync(response);
        }

        var commandName = command.IsSpecified ? command.Value.Name : "UNKNOWN COMMAND";
        _logger.Information("Executed \"{CommandName}\" for {User}", commandName, context.User);
    }

    private string? GetErrorResponse(IResult result) =>
        result.Error switch
        {
            CommandError.UnknownCommand => null,
            CommandError.ParseFailed => _localizationProvider.Get("errors.parseFailed"),
            CommandError.BadArgCount => _localizationProvider.Get("errors.invalidNumberOfArguments"),
            CommandError.UnmetPrecondition => _localizationProvider.Get(result.ErrorReason),
            CommandError.Exception => ((ExecuteResult) result).Exception switch
            {
                InvalidUserLimitException => _localizationProvider.Get("errors.invalidUserLimit"),
                InvalidVoiceChannelIdentifierException => _localizationProvider.Get("errors.invalidChannelIdentifier"),
                VoiceChannelNotFoundException => _localizationProvider.Get("errors.voiceChannelNotFound"),
                _ => _localizationProvider.Get("errors.unknown")
            },
            _ => _localizationProvider.Get("errors.unknown")
        };
}
