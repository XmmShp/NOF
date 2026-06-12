using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NOF.Abstraction;
using NOF.Contract;
using NOF.Domain;
using NOF.Hosting;
using System.Diagnostics;

namespace NOF.Infrastructure;

public sealed class TracingInboundMiddleware(
    IHostEnvironment hostEnvironment,
    ILogger<TracingInboundMiddleware> logger) :
    ICommandInboundMiddleware,
    INotificationInboundMiddleware,
    IRequestInboundMiddleware
{
    public TopologyComparison Compare(ICommandInboundMiddleware other)
        => other is TenantInboundMiddleware ? TopologyComparison.Before : TopologyComparison.DoesNotMatter;

    public TopologyComparison Compare(INotificationInboundMiddleware other)
        => other is TenantInboundMiddleware ? TopologyComparison.Before : TopologyComparison.DoesNotMatter;

    public TopologyComparison Compare(IRequestInboundMiddleware other)
        => other is TenantInboundMiddleware ? TopologyComparison.Before : TopologyComparison.DoesNotMatter;

    public async ValueTask InvokeAsync(CommandInboundContext context, object message, CommandHandlerDelegate next, CancellationToken cancellationToken)
    {
        using var activity = CreateCommandActivity(context, hostEnvironment);

        activity?.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.HandlerType, context.HandlerType.DisplayName);
        activity?.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.MessageType, context.MessageType.DisplayName);

        try
        {
            await next(context, message, cancellationToken);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            HandleActivityException(activity, ex);
            HandleCommandException(context, ex, logger);
        }
    }

    private static Activity? CreateCommandActivity(CommandInboundContext context, IHostEnvironment hostEnvironment)
    {
        var handlerName = context.HandlerType.DisplayName;
        var messageName = context.MessageType.DisplayName;
        var activity = NOFInfrastructureConstants.InboundPipeline.Source.StartActivity(
            $"{handlerName}.Handle: {messageName}",
            ActivityKind.Consumer);
        activity?.SetServiceDeploymentTags(hostEnvironment);
        return activity;
    }

    public async ValueTask InvokeAsync(NotificationInboundContext context, object message, NotificationHandlerDelegate next, CancellationToken cancellationToken)
    {
        using var activity = CreateNotificationActivity(context, hostEnvironment);

        activity?.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.HandlerType, context.HandlerType.DisplayName);
        activity?.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.MessageType, context.MessageType.DisplayName);

        try
        {
            await next(context, message, cancellationToken);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            HandleActivityException(activity, ex);
            HandleNotificationException(context, ex, logger);
        }
    }

    private static Activity? CreateNotificationActivity(NotificationInboundContext context, IHostEnvironment hostEnvironment)
    {
        var handlerName = context.HandlerType.DisplayName;
        var messageName = context.MessageType.DisplayName;
        var activity = NOFInfrastructureConstants.InboundPipeline.Source.StartActivity(
            $"{handlerName}.Handle: {messageName}",
            ActivityKind.Consumer);
        activity?.SetServiceDeploymentTags(hostEnvironment);
        return activity;
    }

    public async ValueTask InvokeAsync(RequestInboundContext context, object request, RequestHandlerDelegate next, CancellationToken cancellationToken)
    {
        using var activity = CreateRequestActivity(context, hostEnvironment);

        activity?.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.HandlerType, context.HandlerType.DisplayName);
        activity?.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.MessageType, $"{context.ServiceType.DisplayName}.{context.ServiceMethodInfo.Name}");
        activity?.SetTag("rpc.method", $"{context.ServiceType.DisplayName}.{context.ServiceMethodInfo.Name}");

        try
        {
            await next(context, request, cancellationToken);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            HandleActivityException(activity, ex);
            HandleRequestException(context, ex, logger);
        }
    }

    private static Activity? CreateRequestActivity(RequestInboundContext context, IHostEnvironment hostEnvironment)
    {
        var handlerName = context.HandlerType.DisplayName;
        var requestName = $"{context.ServiceType.DisplayName}.{context.ServiceMethodInfo.Name}";
        var activity = NOFInfrastructureConstants.InboundPipeline.Source.StartActivity(
            $"{handlerName}.Handle: {requestName}",
            ActivityKind.Consumer);
        activity?.SetServiceDeploymentTags(hostEnvironment);
        return activity;
    }

    private static void HandleActivityException(Activity? activity, Exception exception)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.AddException(exception);
    }

    private static void HandleCommandException(
        CommandInboundContext context,
        Exception exception,
        ILogger logger)
    {
        var handlerName = context.HandlerType.DisplayName;
        var messageName = context.MessageType.DisplayName;

        if (exception is DomainException domainException)
        {
            logger.LogWarning(
                domainException,
                "Domain exception occurred while handling {MessageType} with {HandlerType}: {Message}",
                messageName,
                handlerName,
                domainException.Message);
            return;
        }

        logger.LogError(
            exception,
            "Exception occurred while handling {MessageType} with {HandlerType}: {Message}",
            messageName,
            handlerName,
            exception.Message);
    }

    private static void HandleNotificationException(
        NotificationInboundContext context,
        Exception exception,
        ILogger logger)
    {
        var handlerName = context.HandlerType.DisplayName;
        var messageName = context.MessageType.DisplayName;

        if (exception is DomainException domainException)
        {
            logger.LogWarning(
                domainException,
                "Domain exception occurred while handling {MessageType} with {HandlerType}: {Message}",
                messageName,
                handlerName,
                domainException.Message);
            return;
        }

        logger.LogError(
            exception,
            "Exception occurred while handling {MessageType} with {HandlerType}: {Message}",
            messageName,
            handlerName,
            exception.Message);
    }

    private static void HandleRequestException(
        RequestInboundContext context,
        Exception exception,
        ILogger logger)
    {
        var handlerName = context.HandlerType.DisplayName;
        var requestName = $"{context.ServiceType.DisplayName}.{context.ServiceMethodInfo.Name}";

        if (exception is DomainException domainException)
        {
            logger.LogWarning(
                domainException,
                "Domain exception occurred while handling {MessageType} with {HandlerType}: {Message}",
                requestName,
                handlerName,
                domainException.Message);
            context.Response = RequestInboundResponseFactory.CreateFailure(
                context,
                Result.Fail(domainException.ErrorCode, domainException.Message),
                500);
            return;
        }

        logger.LogError(
            exception,
            "Exception occurred while handling {MessageType} with {HandlerType}: {Message}",
            requestName,
            handlerName,
            exception.Message);
        context.Response = RequestInboundResponseFactory.CreateFailure(
            context,
            Result.Fail("500", "Internal server error"));
    }
}
