using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using MailKitSimplified.Sender.Abstractions;

namespace Zuemail.ViewModels
{
    public sealed partial class SenderViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _fromTextBox = string.Empty;

        [ObservableProperty]
        private string _toTextBox = string.Empty;

        [ObservableProperty]
        private string _subjectTextBox = string.Empty;

        [ObservableProperty]
        private string _messageTextBox = string.Empty;

        [ObservableProperty]
        private bool isInProgress;

        private int _count = 0;

        private readonly ILogger<SenderViewModel> _logger;

        public SenderViewModel(ILogger<SenderViewModel>? logger = null)
        {
            _logger = logger ?? NullLogger<SenderViewModel>.Instance;
#if DEBUG
            FromTextBox = "from@localhost";
            ToTextBox = "to@localhost";
            SubjectTextBox = "Hey";
            MessageTextBox = "<p>Hi.<p>";
#endif
        }

        [RelayCommand]
        private async Task SendMailAsync()
        {
            IsInProgress = true;
            try
            {
                using var smtpSender = Ioc.Default.GetRequiredService<ISmtpSender>();
                if (smtpSender != null)
                {
                    await smtpSender.WriteEmail
                    .From(FromTextBox)
                        .To(ToTextBox)
                        .Subject(SubjectTextBox)
                        .BodyHtml(MessageTextBox)
                        .SendAsync();
                    _logger.LogDebug($"Email #{++_count} sent with subject: \"{SubjectTextBox}\".");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                System.Diagnostics.Debugger.Break();
            }
            IsInProgress = false;
        }
    }
}