using MailKit;
using MailKitSimplified.Receiver.Abstractions;
using MailKitSimplified.Receiver.Extensions;
using MailKitSimplified.Receiver.Services;
using Microsoft.Extensions.Logging;
using MimeKit;
using System.Collections.ObjectModel;
using System.Net.Http.Json;
using Zuemail.Extensions;

namespace Zuemail;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();
        MessageCollection.ItemsSource = Emails;
    }

    public ObservableCollection<Models.Email> Emails { get; private set; } = new();

    protected override async void OnAppearing()
    {
        LoadingIndicator.IsVisible = true;

        base.OnAppearing();

        using var imapReceiver = ImapReceiver.Create("localhost");
        var mimeMessages = await imapReceiver.ReadMail
            .Take(10).GetMimeMessagesAsync();
        var emails = mimeMessages.Convert();
        emails.ActionEach(email => Emails.Add(email));

        LoadingIndicator.IsVisible = false;
    }
}

