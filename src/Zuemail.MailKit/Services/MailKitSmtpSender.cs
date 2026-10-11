global using MimeKit;
global using MailKit;
global using MailKit.Net.Smtp;
global using MailKit.Security;
global using System;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using Microsoft.Extensions.Options;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Logging.Abstractions;
global using Zuemail.Core.Abstractions;
global using Zuemail.Core.Models;
global using Zuemail.Core.Services;
using Zuemail.MailKit.Extensions;
using Zuemail.MailKit.Models;

namespace Zuemail.MailKit.Services;

/// <inheritdoc cref="IEmailSender<IEmailMessage>" />
public sealed class MailKitSmtpSender : IEmailSender<IEmailMessage>, IEmailSender<MimeMessage>, IAsyncDisposable
{
    private readonly IValidationStrategy<MimeMessage> _validation;
    private readonly ILogger<MailKitSmtpSender> _logger;
    private readonly ISmtpClient _smtpClient;
    private readonly SmtpOptions _senderOptions;

    public MailKitSmtpSender(IOptions<SmtpOptions> senderOptions, IValidationStrategy<MimeMessage> validation, ILogger<MailKitSmtpSender> logger = null, ISmtpClient smtpClient = null)
    {
        this._validation = validation;
        _logger = logger ?? NullLogger<MailKitSmtpSender>.Instance;
        _senderOptions = senderOptions.Value;
        if (string.IsNullOrWhiteSpace(_senderOptions.Host))
            throw new ArgumentException($"{nameof(SmtpOptions.Host)} is not set.");
        _smtpClient = smtpClient ?? _senderOptions.SmtpClient.Value;
    }

    public static MailKitSmtpSender Create(string smtpHost, ISmtpClient smtpClient = null, IValidationStrategy<MimeMessage> validation = null, ILogger<MailKitSmtpSender> logger = null)
    {
        if (string.IsNullOrWhiteSpace(smtpHost))
            throw new ArgumentNullException(nameof(smtpHost));
        var emailOptions = (SmtpOptions)EmailOptions.Create(smtpHost);
        var senderOptions = Options.Create(emailOptions);
        var sender = new MailKitSmtpSender(senderOptions, validation, logger, smtpClient);
        return sender;
    }

    public ISmtpClient SmtpClient => _smtpClient;

    public IEmailMessageHandler WriteEmail => new EmailMessageHandler(this);

    public async Task SendAsync(IEmailMessage email, CancellationToken cancellationToken = default)
    {
        var mimeMessage = email.ToMimeMessage();
        await _smtpClient.SendAsync(mimeMessage, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> TrySendAsync(IEmailMessage email, CancellationToken cancellationToken = default)
    {
        try
        {
            await SendAsync(email, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (AuthenticationException ex)
        {
            _logger.LogError(ex, $"Failed to authenticate with mail server. {_senderOptions}");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, $"Failed to connect to mail server. {_senderOptions}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to send email. {email}");
        }
        return false;
    }

    public Task SendAsync(MimeMessage mimeMessage, CancellationToken cancellationToken = default) =>
        SendAsync(mimeMessage, null, cancellationToken);

    public Task<bool> TrySendAsync(MimeMessage mimeMessage, CancellationToken cancellationToken = default) =>
        TrySendAsync(mimeMessage, null, cancellationToken);

    public async Task SendAsync(MimeMessage mimeMessage, ITransferProgress transferProgress, CancellationToken cancellationToken = default)
    {
        _ = await _validation.ValidateAsync(mimeMessage).ConfigureAwait(false);
        await _senderOptions.ConnectAuthenticateAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogTrace($"Sending {mimeMessage.GetEnvelope(includeTextBody: true)}");
        string serverResponse = await _smtpClient.SendAsync(mimeMessage, cancellationToken, transferProgress).ConfigureAwait(false);
        _logger.LogTrace($"{_senderOptions} server response: \"{serverResponse}\".");
        _logger.LogDebug($"Sent Message-ID {mimeMessage.MessageId}.");
    }

    public async Task<bool> TrySendAsync(MimeMessage mimeMessage, ITransferProgress transferProgress, CancellationToken cancellationToken = default)
    {
        bool isSent = false;
        try
        {
            await SendAsync(mimeMessage, transferProgress, cancellationToken).ConfigureAwait(false);
            isSent = true;
        }
        catch (AuthenticationException ex)
        {
            _logger.LogError(ex, $"Failed to authenticate with mail server. {_senderOptions}");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, $"Failed to connect to mail server. {_senderOptions}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to send email. {mimeMessage}");
        }
        return isSent;
    }

    public IEmailMessage Copy() => MemberwiseClone() as IEmailMessage;

    public override string ToString() =>
        $"{_senderOptions.Host}:{_senderOptions.Port} {_senderOptions.Username}";

    public async ValueTask DisposeAsync()
    {
        _logger.LogTrace("Disposing MailKit SMTP email client {Sender}...", this);
        if (_smtpClient.IsConnected)
            await _smtpClient.DisconnectAsync(true).ConfigureAwait(false);
        _smtpClient.Dispose();
    }

    public void Dispose()
    {
        _logger.LogTrace("Disposing MailKit SMTP email client {Sender}...", this);
        if (_smtpClient.IsConnected)
            _smtpClient.Disconnect(true);
        _smtpClient.Dispose();
    }
}
