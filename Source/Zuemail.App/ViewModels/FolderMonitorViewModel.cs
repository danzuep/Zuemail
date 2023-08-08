using MailKit;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Common;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using MailKitSimplified.Receiver.Abstractions;
using MailKitSimplified.Receiver.Extensions;
using Zuemail.Extensions;

namespace Zuemail.ViewModels
{
    public sealed partial class FolderMonitorViewModel : ObservableObject, IDisposable
    {
        private readonly Channel<IMessageSummary> _queue;

        public ObservableCollection<string> MailFolders { get; private set; } = new() { _inbox };
        public string SelectedMailFolders { get; set; } = _inbox;

        public ObservableCollection<Models.Email> Emails { get; private set; } = new();

        [ObservableProperty]
        private Models.Email selectedEmail = new();

        [ObservableProperty]
        private string imapHost = "localhost";

        [ObservableProperty]
        private bool isInProgress;

        [ObservableProperty]
        private int progressBarPercentage;

        [ObservableProperty]
        private string _messageTextBlock = string.Empty;

        private static readonly string _inbox = "INBOX";
        private readonly CancellationTokenSource _cts = new();
        private readonly BackgroundWorker _worker = new();
        private readonly IImapReceiver _imapReceiver;
        private readonly ILogger _logger;

        public FolderMonitorViewModel() : base()
        {
            _imapReceiver = Ioc.Default.GetRequiredService<IImapReceiver>();
            _logger = Ioc.Default.GetRequiredService<ILogger<FolderMonitorViewModel>>();

            int capacity = 0;
            if (capacity > 0)
            {
                var channelOptions = new BoundedChannelOptions(capacity)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    AllowSynchronousContinuations = true,
                    SingleReader = true,
                    SingleWriter = true
                };
                _queue = Channel.CreateBounded<IMessageSummary>(channelOptions);
            }
            else
            {
                var channelOptions = new UnboundedChannelOptions()
                {
                    AllowSynchronousContinuations = true,
                    SingleReader = true,
                    SingleWriter = true
                };
                _queue = Channel.CreateUnbounded<IMessageSummary>(channelOptions);
            }

        }

        [RelayCommand]
        private async Task ConnectHostAsync()
        {
            try
            {
                _logger.LogDebug("Getting mail folder names...");
                IsInProgress = true;
                var mailFolderNames = await _imapReceiver.GetMailFolderNamesAsync();
                if (mailFolderNames.Count > 0)
                {
                    MailFolders = new ObservableCollection<string>(mailFolderNames);
                }
                IsInProgress = false;
                _logger.LogDebug($"Connected to {ImapHost}.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                System.Diagnostics.Debugger.Break();
            }
        }

        [RelayCommand]
        private async Task ReceiveAsync()
        {
            //int progressPercentage = Convert.ToInt32((max * 100d) / 100);
            var tasks = new Task[]
            {
                _imapReceiver.MonitorFolder.OnMessageArrival(EnqueueAsync).IdleAsync(_cts.Token),
                ProcessQueueAsync(OnArrivalAsync, _cts.Token)
            };
            await Task.WhenAll(tasks);
        }

        private async Task EnqueueAsync(IMessageSummary m) => await _queue.Writer.WriteAsync(m, _cts.Token);

        private async Task ProcessQueueAsync(Func<IMessageSummary, ValueTask> messageArrivalMethod, CancellationToken cancellationToken = default)
        {
            IMessageSummary? messageItem = null;
            try
            {
                await foreach (var messageSummary in _queue.Reader.ReadAllAsync(cancellationToken))
                {
                    if (messageSummary != null)
                    {
                        messageItem = messageSummary;
                        await messageArrivalMethod(messageSummary);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogTrace("Arrival queue cancelled.");
            }
            catch (ChannelClosedException ex)
            {
                _logger.LogWarning(ex, "Channel closed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred processing task queue item #{0}.", messageItem);
                if (messageItem != null)
                    await _queue.Writer.WriteAsync(messageItem);
            }
        }

        private async ValueTask OnArrivalAsync(IMessageSummary messageSummary)
        {
            _logger.LogDebug("Downloading email...");
            IsInProgress = true;
            var mimeMessage = await messageSummary.GetMimeMessageAsync(_cts.Token);
            var email = mimeMessage.Convert();
            _logger.LogDebug($"{_imapReceiver} #{messageSummary.Index} received: {email.Subject}.");
            Emails.Add(email);
            if (SelectedEmail == null)
            {
                SelectedEmail = email;
            }
            IsInProgress = false;
        }

        public void Dispose()
        {
            //_queue.Writer.Complete();
            _imapReceiver.Dispose();
            _worker.Dispose();
            _cts.Dispose();
        }
    }
}
